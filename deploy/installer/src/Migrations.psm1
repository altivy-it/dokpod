#Requires -Version 7.4
<#
.SYNOPSIS
Etapa de migrations (spec 033 T030, FR-010, FR-011, FR-036 a FR-038, FR-041, FR-042, L4, L5): modo administrativo da
imagem da API, confirmação ligada ao planHash, catálogo SQL sobreposto, backup e migrations manuais.

.DESCRIPTION
Todo SQL vem do catálogo da imagem da API: o instalador só chama `admin migrations status|plan|script|apply|fingerprint|
verify` num container avulso, com a conexão de dokpod_owner montada só para leitura, sem socket do engine, sem porta e
só na rede do banco (L5).

O diretório --sql-dir é conferido contra o manifesto da release e copiado para installDir/sql-override/, que o modo
administrativo lê em /run/sql-override. Cada arquivo alterado exige confirmação nomeada. Na reexecução, os hashes da
cópia são recalculados e comparados com sql_overrides.applied_sha256: o que não confere volta a pedir confirmação (L4).
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module (Join-Path $PSScriptRoot 'State.psm1')

$script:OwnerConnection = 'dokpod-owner-connection'
$script:AdminConnectionTarget = '/run/secrets/ConnectionStrings__ControlPlane'
$script:OverrideTarget = '/run/sql-override'
$script:WorkTarget = '/run/dokpod-migrations'
$script:ProvisionedNetwork = 'dokpod_dokpod-data'
$script:ProvisionedService = 'dokpod-postgres'
$script:ProvisionedAdminUser = 'dokpod_admin'
$script:ProvisionedAdminSecret = 'dokpod-db-password'
$script:ComposeProject = 'dokpod'
$script:AcceptedFingerprintsFile = 'accepted-fingerprints.json'

function Invoke-AdminCommand {
    <#
    .SYNOPSIS
    Executa `admin <grupo> <comando>` da imagem da API num container avulso e devolve o código e o JSON de stdout.
    .DESCRIPTION
    O container roda com raiz somente leitura, sem capabilities, sem log, sem porta, sem socket do engine e só na rede
    do banco (L5). A conexão de dokpod_owner é montada só para leitura em /run/secrets; os demais mounts são o catálogo
    sobreposto (somente leitura) e a pasta de trabalho das migrations. Os caminhos montados existem no host com o mesmo
    nome, porque install.sh monta installDir e secretsDir assim.
    Códigos 0, 6 e 8 devolvem o resultado; os demais interrompem com a mensagem do modo administrativo.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string] $Image,
        [Parameter(Mandatory)][string] $Network,
        [Parameter(Mandatory)][string] $SecretsDirectory,
        [Parameter(Mandatory)][string[]] $Arguments,
        [string] $OverrideDirectory,
        [string] $WorkDirectory,
        [switch] $WritableWork,
        [string] $Cli = 'docker'
    )

    $connection = Join-Path $SecretsDirectory $script:OwnerConnection
    if (-not (Test-Path -LiteralPath $connection -PathType Leaf)) {
        Stop-Installer -Code Error -Message "A conexão de dokpod_owner ($script:OwnerConnection) não existe em secretsDir; o bootstrap do banco não rodou."
    }

    $run = @('run', '--rm', '--log-driver', 'none', '--read-only', '--network', $Network,
        '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges', '--user', '0:0',
        '--tmpfs', '/tmp:rw,noexec,nosuid,nodev,size=64m,mode=1777',
        '--env', 'DOTNET_CLI_TELEMETRY_OPTOUT=1', '--env', 'DOTNET_EnableDiagnostics=0',
        '--label', 'io.dokpod.installer.task=admin', '--name', "dokpod-installer-admin-$([guid]::NewGuid().ToString('N').Substring(0, 12))",
        '--mount', "type=bind,src=$connection,dst=$($script:AdminConnectionTarget),ro")
    if ($env:DOKPOD_HOST_ENGINE -eq 'podman') { $run += '--security-opt', 'label=disable' }
    if ($OverrideDirectory) { $run += '--mount', "type=bind,src=$OverrideDirectory,dst=$($script:OverrideTarget),ro" }
    if ($WorkDirectory) {
        $mode = if ($WritableWork) { '' } else { ',ro' }
        $run += '--mount', "type=bind,src=$WorkDirectory,dst=$($script:WorkTarget)$mode"
    }

    $run += $Image, 'admin'
    $run += $Arguments
    $run += '--output', 'json'
    $result = Invoke-InstallerProcess -FilePath $Cli -ArgumentList $run
    if ($result.ExitCode -notin 0, 6, 8) {
        Stop-Installer -Code Error -Message "O modo administrativo da API falhou em '$($Arguments[0..1] -join ' ')' (código $($result.ExitCode)): $($result.Error.Trim())"
    }

    $data = $null
    if ($result.Output.Trim()) {
        try { $data = $result.Output | ConvertFrom-Json -AsHashtable }
        catch { Stop-Installer -Code Error -Message "O modo administrativo da API devolveu uma saída que não é JSON em '$($Arguments[0..1] -join ' ')'." }
    }

    return [pscustomobject]@{ ExitCode = $result.ExitCode; Data = $data; Error = $result.Error.Trim() }
}

function Read-CatalogManifest {
    param([string] $Root)

    $path = Join-Path $Root 'manifest.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
}

function Get-SqlOverrideSet {
    <#
    .SYNOPSIS
    Confere um catálogo sobreposto contra o manifesto da release e devolve só os arquivos alterados, com os dois hashes
    (FR-041). Recusa manifesto ausente ou de outra versão, link simbólico, arquivo vazio, caminho sem correspondente e a
    impressão do schema esperado.
    #>
    [CmdletBinding()]
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory)][string] $Directory,
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog')
    )

    $release = Read-CatalogManifest -Root $CatalogRoot
    if (-not $release) { Stop-Installer -Code Error -Message 'O catálogo SQL não está nesta imagem do instalador.' }
    $root = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($Directory))
    $item = Get-Item -LiteralPath $root -Force -ErrorAction SilentlyContinue
    if (-not $item -or -not $item.PSIsContainer -or $item.LinkTarget) {
        Stop-Installer -Code Usage -Message "O --sql-dir $Directory não existe ou é um link simbólico."
    }

    $manifest = Read-CatalogManifest -Root $root
    if (-not $manifest) {
        Stop-Installer -Code Usage -Message "O --sql-dir $Directory não tem o manifest.json do catálogo; use 'sql export' da mesma versão."
    }
    if ($manifest.version -ne $release.version) {
        Stop-Installer -Code Usage -Message "O manifest.json do --sql-dir é da versão $($manifest.version); esta versão é $($release.version)."
    }

    $entries = @{}
    foreach ($entry in $release.files) { $entries[$entry.path] = $entry }
    $changes = [System.Collections.Generic.List[object]]::new()
    foreach ($file in (Get-ChildItem -LiteralPath $root -Recurse -Force)) {
        $path = [System.IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        if ($file.LinkTarget) { Stop-Installer -Code Usage -Message "O --sql-dir tem link simbólico: $path." }
        if ($file.PSIsContainer -or $path -eq 'manifest.json') { continue }
        if ($release.Contains('expectedSchema') -and $path -eq $release.expectedSchema.path) {
            if ((Get-Sha256Hex -Text ([System.IO.File]::ReadAllText($file.FullName))) -ne $release.expectedSchema.sha256) {
                Stop-Installer -Code Usage -Message "A impressão do schema esperado ($path) não pode ser sobreposta (FR-042)."
            }
            continue
        }
        if (-not $entries.ContainsKey($path)) {
            Stop-Installer -Code Usage -Message "O arquivo $path do --sql-dir não tem correspondente nesta versão (FR-041)."
        }

        $content = [System.IO.File]::ReadAllText($file.FullName)
        if ([string]::IsNullOrWhiteSpace($content)) { Stop-Installer -Code Usage -Message "O arquivo $path do --sql-dir está vazio." }
        $applied = Get-Sha256Hex -Text $content
        if ($applied -eq $entries[$path].sha256) { continue }

        # Migrations/Script/<id>/Up|Down/<arquivo>.sql
        $segments = $path.Split('/')
        $migration = if ($entries[$path].purpose -eq 'migration' -and $segments.Count -ge 3) { $segments[2] } else { $null }
        $changes.Add([pscustomobject]@{
                Path           = $path
                Purpose        = $entries[$path].purpose
                MigrationId    = $migration
                OriginalSha256 = $entries[$path].sha256
                AppliedSha256  = $applied
            })
    }

    return , @($changes | Sort-Object Path)
}

function Copy-SqlOverrideSet {
    # Só o manifesto e os arquivos alterados: o modo administrativo usa o da release para o resto.
    param([string] $Source, [string] $Target, [object[]] $Changes)

    if (Test-Path -LiteralPath $Target) { Remove-Item -LiteralPath $Target -Recurse -Force }
    [void] [System.IO.Directory]::CreateDirectory($Target)
    Copy-Item -LiteralPath (Join-Path $Source 'manifest.json') -Destination (Join-Path $Target 'manifest.json')
    foreach ($change in $Changes) {
        $destination = Join-Path $Target $change.Path
        [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $destination))
        Copy-Item -LiteralPath (Join-Path $Source $change.Path) -Destination $destination
    }
}

function Resolve-SqlOverride {
    <#
    .SYNOPSIS
    Catálogo sobreposto desta execução: o --sql-dir conferido, numa cópia temporária até a confirmação, ou a cópia de
    installDir/sql-override/ de uma execução anterior, com os hashes recalculados (L4). Uma cópia de outra versão não
    vale mais e é ignorada com aviso.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string] $InstallDirectory,
        [string] $SqlDirectory,
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog')
    )

    $current = Join-Path $InstallDirectory 'sql-override'
    if ($SqlDirectory) {
        $changes = Get-SqlOverrideSet -Directory $SqlDirectory -CatalogRoot $CatalogRoot
        $staging = Join-Path $InstallDirectory 'sql-override.pendente'
        Copy-SqlOverrideSet -Source $SqlDirectory -Target $staging -Changes $changes
        return [pscustomobject]@{ Directory = $staging; Staging = $true; Changes = $changes }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $current 'manifest.json'))) {
        return [pscustomobject]@{ Directory = $null; Staging = $false; Changes = @() }
    }

    $release = Read-CatalogManifest -Root $CatalogRoot
    $copy = Read-CatalogManifest -Root $current
    if ($copy.version -ne $release.version) {
        Write-InstallerLine -ToStandardError -Message ("Aviso: a sobreposição de SQL em $current é da versão $($copy.version) e não vale para a " +
            "$($release.version); ela não é usada. Para manter um ajuste, exporte o catálogo desta versão e passe --sql-dir de novo.")
        return [pscustomobject]@{ Directory = $null; Staging = $false; Changes = @() }
    }

    $changes = Get-SqlOverrideSet -Directory $current -CatalogRoot $CatalogRoot
    return [pscustomobject]@{ Directory = $(if ($changes.Count -gt 0) { $current } else { $null }); Staging = $false; Changes = $changes }
}

function Get-UnconfirmedOverride {
    # Alterados sem registro com o mesmo hash aplicado em sql_overrides (L4).
    param($State, [object[]] $Changes)

    $recorded = Invoke-StateSql -State $State -Name 'sql-overrides-list' -Rows
    return , @($Changes | Where-Object {
            $change = $_
            -not ($recorded | Where-Object { $_.path -eq $change.Path -and $_.applied_sha256 -eq $change.AppliedSha256 })
        })
}

function Get-AcceptedFingerprint {
    param($State)

    $rows = Invoke-StateSql -State $State -Name 'sql-overrides-list' -Rows
    return , @($rows |
            Where-Object { $_.accepted_schema_fingerprint } | ForEach-Object { $_.accepted_schema_fingerprint } | Select-Object -Unique)
}

function Format-MigrationPlan {
    <#
    .SYNOPSIS
    Linhas do plano de migrations para o operador: versão atual, pendentes com objetos afetados, sobreposições e planHash
    (FR-036, FR-041).
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Plan)

    $lines = [System.Collections.Generic.List[string]]::new()
    $from = if ($Plan.from) { $Plan.from } else { '(banco vazio)' }
    $lines.Add("Migrations: banco em $from, versão alvo $($Plan.to), $(@($Plan.migrations).Count) pendente(s).")
    foreach ($migration in @($Plan.migrations)) {
        $lines.Add("  $($migration.id)")
        foreach ($kind in @(@('created', 'cria'), @('altered', 'altera'), @('dropped', 'remove'))) {
            $objects = @($migration.objects[$kind[0]])
            if ($objects.Count -gt 0) { $lines.Add("    $($kind[1]): $($objects -join ', ')") }
        }
        foreach ($override in @($migration.overrides | Where-Object { $_.originalSha256 -ne $_.appliedSha256 })) {
            $lines.Add("    SQL sobreposto: $($override.path) original=$($override.originalSha256) aplicado=$($override.appliedSha256)")
        }
    }
    $lines.Add("planHash: $($Plan.planHash)")
    return , $lines.ToArray()
}

function Confirm-SqlOverride {
    # Confirmação que nomeia cada arquivo (FR-041): --confirm-sql-overrides com exatamente os caminhos, ou digitando cada um.
    param([object[]] $Pending, [string[]] $Confirmed, [bool] $Interactive, [scriptblock] $Prompt)

    if ($Pending.Count -eq 0) { return }
    if ($Interactive) {
        foreach ($change in $Pending) {
            if ((& $Prompt "Digite o caminho $($change.Path) para confirmar a sobreposição") -ne $change.Path) {
                Stop-Installer -Code Declined -Message "A sobreposição de $($change.Path) não foi confirmada; nada foi aplicado."
            }
        }
        return
    }

    $expected = @($Pending | ForEach-Object { $_.Path } | Sort-Object)
    $given = @($Confirmed | Where-Object { $_ } | Sort-Object -Unique)
    if (($expected -join "`n") -cne ($given -join "`n")) {
        Stop-Installer -Code Declined -Message ("A sobreposição de SQL exige --confirm-sql-overrides com exatamente: $($expected -join ','). " +
            '--yes não confirma sobreposição (FR-041); nada foi aplicado.')
    }
}

function Confirm-MigrationPlan {
    param([string] $PlanHash, [string] $Confirmed, [bool] $Interactive, [scriptblock] $Prompt)

    if ($Interactive) {
        if ((& $Prompt "Digite 'aplicar' para aplicar estas migrations (planHash $PlanHash)") -ne 'aplicar') {
            Stop-Installer -Code Declined -Message 'As migrations não foram confirmadas; nada foi aplicado.'
        }
        return
    }

    if ($Confirmed -ne $PlanHash) {
        Stop-Installer -Code Declined -Message ("As migrations exigem --confirm-migrations $PlanHash, o planHash do plano acima. " +
            '--yes não confirma migrations (FR-036); nada foi aplicado.')
    }
}

function Find-ProvisionedPostgresContainer {
    param([string] $Cli)

    $ids = @(& $Cli ps --quiet --filter "label=com.docker.compose.project=$($script:ComposeProject)" `
            --filter "label=com.docker.compose.service=$($script:ProvisionedService)" 2>$null | Where-Object { $_ })
    if ($ids.Count -ne 1) {
        Stop-Installer -Code Error -Message "O container $($script:ProvisionedService) do projeto $($script:ComposeProject) não está em execução; não há como fazer o backup."
    }
    return $ids[0]
}

function Backup-ProvisionedDatabase {
    <#
    .SYNOPSIS
    pg_dump em formato custom do banco dokpod no dokpod-postgres provisionado, como no runbook de backup (FR-011), para
    installDir/backups/<arquivo>.dump (0600). A senha administrativa é lida dentro do container do próprio arquivo de
    segredo do serviço; nada dela passa por argumento.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string] $InstallDirectory,
        [Parameter(Mandatory)][string] $Name,
        [string] $Container,
        [string] $Database = 'dokpod',
        [string] $Cli = 'docker'
    )

    if (-not $Container) { $Container = Find-ProvisionedPostgresContainer -Cli $Cli }
    $directory = Join-Path $InstallDirectory 'backups'
    [void] [System.IO.Directory]::CreateDirectory($directory)
    if (-not $IsWindows) { [System.IO.File]::SetUnixFileMode($directory, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute') }
    $target = Join-Path $directory "$Name.dump"
    $command = "PGPASSWORD=`"`$(cat /run/secrets/$($script:ProvisionedAdminSecret))`" exec pg_dump -U $($script:ProvisionedAdminUser) -d $Database -Fc -Z 6"
    $result = Invoke-InstallerProcess -FilePath $Cli -ArgumentList @('exec', $Container, 'sh', '-c', $command) -StandardOutputPath $target
    if ($result.ExitCode -ne 0 -or (Get-Item -LiteralPath $target).Length -eq 0) {
        Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
        Stop-Installer -Code Error -Message "O backup do banco $Database falhou (código $($result.ExitCode)): $($result.Error.Trim()) Nada foi aplicado."
    }

    return $target
}

function Confirm-DatabaseBackup {
    <#
    .SYNOPSIS
    Backup antes de migrations num banco com dados (FR-011): pg_dump no dokpod-postgres provisionado; nos demais,
    confirmação nomeada (--confirm-backup <banco> ou o nome do banco digitado).
    #>
    param([System.Collections.IDictionary] $Answers, [string] $Database, [string] $Confirmed, [bool] $Interactive, [scriptblock] $Prompt,
        [string] $RunId, [string] $Container, [string] $Cli)

    if ($Answers.postgres.mode -eq 'provision') {
        return Backup-ProvisionedDatabase -InstallDirectory $Answers.installDir -Name "dokpod-$RunId" -Container $Container -Database $Database -Cli $Cli
    }

    $runbook = 'docs/runbooks/backup-restore-e-atualizacao.md'
    if ($Interactive) {
        if ((& $Prompt "O banco $Database já tem dados. Faça o backup ($runbook) e digite o nome do banco para confirmar") -ne $Database) {
            Stop-Installer -Code Declined -Message "O backup do banco $Database não foi confirmado; nada foi aplicado."
        }
    }
    elseif ($Confirmed -ne $Database) {
        Stop-Installer -Code Declined -Message ("O banco $Database já tem dados: faça o backup ($runbook) e confirme com --confirm-backup $Database. " +
            'Nada foi aplicado (FR-011).')
    }

    return $null
}

function Add-SqlOverrideRecord {
    param($State, [string] $RunId, [object[]] $Changes, [string] $Operator, [string] $Fingerprint, [string[]] $FingerprintFor)

    foreach ($change in $Changes) {
        $accepted = if ($Fingerprint -and $change.MigrationId -in $FingerprintFor) { $Fingerprint } else { $null }
        Invoke-StateSql -State $State -Name 'sql-override-insert' -Parameters @{
            path = $change.Path; purpose = $change.Purpose; original_sha256 = $change.OriginalSha256; applied_sha256 = $change.AppliedSha256
            accepted_schema_fingerprint = $accepted; confirmed_by = $Operator; now = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ'); run_id = $RunId
        }
    }
}

function Publish-SqlOverride {
    # A cópia confirmada passa a ser a de installDir/sql-override/, a que a reexecução confere.
    param([pscustomobject] $Override, [string] $InstallDirectory)

    if (-not $Override.Staging) { return $Override.Directory }
    $current = Join-Path $InstallDirectory 'sql-override'
    if (Test-Path -LiteralPath $current) { Remove-Item -LiteralPath $current -Recurse -Force }
    [System.IO.Directory]::Move($Override.Directory, $current)
    $Override.Staging = $false
    $Override.Directory = if ($Override.Changes.Count -gt 0) { $current } else { $null }
    return $Override.Directory
}

function Invoke-MigrationVerify {
    param([hashtable] $Admin, $State, [string] $WorkDirectory, [string] $Script)

    $arguments = @('migrations', 'verify')
    $accepted = Get-AcceptedFingerprint -State $State
    if ($accepted.Count -gt 0) {
        [System.IO.File]::WriteAllText((Join-Path $WorkDirectory $script:AcceptedFingerprintsFile), (ConvertTo-Json -InputObject @($accepted) -Compress))
        $arguments += '--accepted-fingerprints', "$($script:WorkTarget)/$($script:AcceptedFingerprintsFile)"
    }
    if ($Script) { $arguments += '--script', "$($script:WorkTarget)/$Script" }
    return (Invoke-AdminCommand @Admin -Arguments $arguments -WorkDirectory $WorkDirectory).Data
}

function Format-VerifyFailure {
    param([System.Collections.IDictionary] $Verify)

    $parts = @()
    if (@($Verify['missing']).Count) { $parts += "faltam $(@($Verify['missing']) -join ', ')" }
    if (@($Verify['unexpected']).Count) { $parts += "inesperadas $(@($Verify['unexpected']) -join ', ')" }
    if (-not $Verify['schemaAccepted']) {
        $parts += "schema diferente do esperado em $((@($Verify['schemaDiff']) | ForEach-Object { "$($_.kind) $($_.name)" }) -join ', ')"
    }
    if (@($Verify['fileHashMismatch']).Count) { $parts += "arquivo exportado alterado em $(@($Verify['fileHashMismatch']) -join ', ')" }
    return $parts -join '; '
}

function Invoke-MigrationStep {
    <#
    .SYNOPSIS
    Leva o banco à versão da release pelo modo administrativo, com as confirmações de FR-011, FR-036 e FR-041.
    .DESCRIPTION
    Ordem: catálogo sobreposto conferido; status (banco mais novo dá código 6); plano mostrado; confirmações nomeadas
    das sobreposições e do planHash; backup quando o banco já tem dados; apply; impressão aceita quando houve
    sobreposição; verify. Toda recusa acontece antes de qualquer efeito no banco (código 4).

    Com -ManualMigrations, grava o SQL em installDir/migrations/<de>-<para>.sql e pausa com código 10. Na retomada, com
    o arquivo presente, roda verify --script e só segue com ok; senão, código 6 dizendo o que falta (FR-038).
    .PARAMETER Confirmations
    As confirmações da linha de comando (Cli.psm1): MigrationsPlanHash, SqlOverrides e Backup.
    .PARAMETER Prompt
    Pergunta ao operador no modo interativo; recebe o texto e devolve a resposta.
    .OUTPUTS
    O resumo sem segredos: versões, migrations aplicadas, planHash, sobreposições registradas, backup e modo.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $RunId,
        [Parameter(Mandatory)][string] $Image,
        [string] $Network = $script:ProvisionedNetwork,
        [System.Collections.IDictionary] $Confirmations = @{},
        [string] $SqlDirectory,
        [switch] $ManualMigrations,
        [string] $Database = 'dokpod',
        [string] $PostgresContainer,
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog'),
        [string] $Cli = 'docker',
        [bool] $Interactive = (Test-InteractiveSession),
        [scriptblock] $Prompt = { param($message) Read-Host -Prompt $message }
    )

    $installDir = $Answers.installDir
    $operator = if ($Answers.Contains('operator') -and $Answers.operator) { $Answers.operator } else { 'installer' }
    $manual = $ManualMigrations -or ($Answers.Contains('manualMigrations') -and $Answers.manualMigrations)
    $work = Join-Path $installDir 'migrations'
    [void] [System.IO.Directory]::CreateDirectory($work)
    $override = Resolve-SqlOverride -InstallDirectory $installDir -SqlDirectory $SqlDirectory -CatalogRoot $CatalogRoot
    try {
        $admin = @{ Image = $Image; Network = $Network; SecretsDirectory = $Answers.secretsDir; OverrideDirectory = $override.Directory; Cli = $Cli }
        if ($override.Staging -and $override.Changes.Count -eq 0) { $admin.OverrideDirectory = $null }

        $status = (Invoke-AdminCommand @admin -Arguments @('migrations', 'status')).Data
        if (@($status.newerThanTarget).Count -gt 0) {
            Stop-Installer -Code MigrationDivergence -Message ("O banco tem migrations que esta versão não conhece: $(@($status.newerThanTarget) -join ', '). " +
                'As migrations são forward-only; use a versão que as criou ou restaure o backup (FR-010).')
        }

        $applied = @($status.applied)
        $pending = @($status.pending)
        $unconfirmed = Get-UnconfirmedOverride -State $State -Changes $override.Changes

        $from = if ($status.databaseVersion) { $status.databaseVersion } else { 'vazio' }
        $existingScript = @(Get-ChildItem -LiteralPath $work -Filter "*-$($status.targetVersion).sql" -File | Sort-Object LastWriteTimeUtc -Descending) | Select-Object -First 1
        $result = [ordered]@{
            From = $status.databaseVersion; To = $status.targetVersion; Applied = @(); PlanHash = $null; Overrides = @($unconfirmed | ForEach-Object { $_.Path })
            BackupPath = $null; Manual = [bool] $manual; ScriptPath = $null
        }

        # Retomada da migration manual (FR-038): o arquivo exportado para esta versão alvo já existe.
        if ($manual -and $existingScript) {
            $verify = Invoke-MigrationVerify -Admin $admin -State $State -WorkDirectory $work -Script $existingScript.Name
            $onlySchema = @($verify['missing']).Count -eq 0 -and @($verify['unexpected']).Count -eq 0 -and @($verify['fileHashMismatch']).Count -eq 0
            $carried = @($unconfirmed | Where-Object { $_.MigrationId })
            if ($onlySchema -and $unconfirmed.Count -gt 0) {
                Confirm-SqlOverride -Pending $unconfirmed -Confirmed $Confirmations['SqlOverrides'] -Interactive $Interactive -Prompt $Prompt
                # O arquivo entregue, sem alteração (fileHashMismatch vazio), carregava a sobreposição confirmada: a impressão
                # observada depois dele é a aceita.
                $fingerprint = if ($carried.Count -gt 0 -and (Select-String -LiteralPath $existingScript.FullName -Pattern '^-- override: ' -Quiet)) {
                    (Invoke-AdminCommand @admin -Arguments @('migrations', 'fingerprint')).Data.sha256
                }
                Add-SqlOverrideRecord -State $State -RunId $RunId -Changes $unconfirmed -Operator $operator -Fingerprint $fingerprint `
                    -FingerprintFor @($carried | ForEach-Object { $_.MigrationId })
                [void] (Publish-SqlOverride -Override $override -InstallDirectory $installDir)
                $verify = Invoke-MigrationVerify -Admin $admin -State $State -WorkDirectory $work -Script $existingScript.Name
            }
            if (-not $verify['ok']) {
                Stop-Installer -Code MigrationDivergence -Message ("A conferência da migration manual falhou: $(Format-VerifyFailure -Verify $verify). " +
                    "Ação: aplique $($existingScript.FullName) com psql -v ON_ERROR_STOP=1 -f e reexecute; para exportar de novo, apague o arquivo.")
            }

            Write-InstallerLine -Message "Migration manual conferida: banco na versão $($status.targetVersion)."
            $result.ScriptPath = $existingScript.FullName
            $result.Overrides = @()
            return [pscustomobject] $result
        }

        foreach ($change in @($unconfirmed | Where-Object { $_.MigrationId -and $_.MigrationId -in $applied })) {
            Write-InstallerLine -Message "Aviso: $($change.Path) é de uma migration já aplicada neste banco; a alteração não tem efeito aqui e só fica registrada."
        }
        foreach ($change in @($unconfirmed | Where-Object { $_.Purpose -ne 'migration' })) {
            Write-InstallerLine -Message "SQL sobreposto fora das migrations: $($change.Path) original=$($change.OriginalSha256) aplicado=$($change.AppliedSha256)"
        }

        if ($pending.Count -eq 0) {
            Confirm-SqlOverride -Pending $unconfirmed -Confirmed $Confirmations['SqlOverrides'] -Interactive $Interactive -Prompt $Prompt
            Add-SqlOverrideRecord -State $State -RunId $RunId -Changes $unconfirmed -Operator $operator
            [void] (Publish-SqlOverride -Override $override -InstallDirectory $installDir)
            Write-InstallerLine -Message "Migrations: banco já na versão $($status.targetVersion)."
            return [pscustomobject] $result
        }

        $plan = (Invoke-AdminCommand @admin -Arguments @('migrations', 'plan')).Data
        foreach ($line in (Format-MigrationPlan -Plan $plan)) { Write-InstallerLine -Message $line }
        $result.PlanHash = $plan.planHash

        Confirm-SqlOverride -Pending $unconfirmed -Confirmed $Confirmations['SqlOverrides'] -Interactive $Interactive -Prompt $Prompt
        if (-not $manual) {
            Confirm-MigrationPlan -PlanHash $plan.planHash -Confirmed $Confirmations['MigrationsPlanHash'] -Interactive $Interactive -Prompt $Prompt
        }
        if ($applied.Count -gt 0) {
            $result.BackupPath = Confirm-DatabaseBackup -Answers $Answers -Database $Database -Confirmed $Confirmations['Backup'] -Interactive $Interactive `
                -Prompt $Prompt -RunId $RunId -Container $PostgresContainer -Cli $Cli
        }

        $admin.OverrideDirectory = Publish-SqlOverride -Override $override -InstallDirectory $installDir
        if ($manual) {
            $name = "$from-$($status.targetVersion).sql"
            $exported = (Invoke-AdminCommand @admin -Arguments @('migrations', 'script', '--out', "$($script:WorkTarget)/$name") -WorkDirectory $work -WritableWork).Data
            Add-SqlOverrideRecord -State $State -RunId $RunId -Changes @($unconfirmed | Where-Object { -not $_.MigrationId -or $_.MigrationId -notin $pending }) -Operator $operator
            Stop-Installer -Code PausedForMigration -Message ("SQL das migrations exportado em $(Join-Path $work $name) (sha256 $($exported.sha256), planHash $($exported.planHash)). " +
                'Ação: aplique com psql -v ON_ERROR_STOP=1 -f <arquivo> e reexecute o mesmo comando; a retomada confere o banco (FR-038).')
        }

        $apply = Invoke-AdminCommand @admin -Arguments @('migrations', 'apply', '--confirm', $plan.planHash)
        if ($apply.ExitCode -ne 0) {
            Stop-Installer -Code MigrationDivergence -Message "O apply recusou o plano confirmado: $($apply.Error) Rode o instalador de novo para ver o plano atual."
        }
        $result.Applied = @($apply.Data.applied)

        $overridden = @(@($plan.migrations) | Where-Object { @($_.overrides | Where-Object { $_.originalSha256 -ne $_.appliedSha256 }).Count -gt 0 } | ForEach-Object { $_.id })
        $fingerprint = if ($overridden.Count -gt 0) { (Invoke-AdminCommand @admin -Arguments @('migrations', 'fingerprint')).Data.sha256 } else { $null }
        Add-SqlOverrideRecord -State $State -RunId $RunId -Changes $unconfirmed -Operator $operator -Fingerprint $fingerprint -FingerprintFor $overridden

        $verify = Invoke-MigrationVerify -Admin $admin -State $State -WorkDirectory $work
        if (-not $verify['ok']) {
            Stop-Installer -Code MigrationDivergence -Message "As migrations foram aplicadas, mas a conferência falhou: $(Format-VerifyFailure -Verify $verify)."
        }

        Write-InstallerLine -Message "Migrations aplicadas: $($result.Applied.Count), banco na versão $($status.targetVersion)."
        return [pscustomobject] $result
    }
    finally {
        if ($override.Staging -and (Test-Path -LiteralPath $override.Directory)) { Remove-Item -LiteralPath $override.Directory -Recurse -Force }
    }
}

Export-ModuleMember -Function Invoke-AdminCommand, Get-SqlOverrideSet, Resolve-SqlOverride, Format-MigrationPlan, Backup-ProvisionedDatabase, Invoke-MigrationStep
