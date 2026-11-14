#Requires -Version 7.4
<#
.SYNOPSIS
Linha de comando do instalador (spec 033 T027, FR-001, FR-004, FR-035; contrato contracts/installer-cli.md): leitura
dos argumentos, confirmações explícitas e os comandos sem efeito (help, plan, --dry-run, status, sql export).
#>
Set-StrictMode -Version Latest
foreach ($module in 'Common', 'Answers', 'State', 'Prerequisites', 'Images', 'Render', 'Edge', 'Plan', 'Install') {
    Import-Module (Join-Path $PSScriptRoot "$module.psm1")
}

$script:Commands = 'install', 'plan', 'upgrade', 'migrations', 'sql', 'recreate', 'host', 'certificates', 'status', 'help'
$script:ValueOptions = 'answers', 'install-dir', 'secrets-dir', 'version', 'offline', 'sql-dir', 'out', 'confirm-data-removal',
'confirm-migrations', 'confirm-sql-overrides', 'confirm-backup', 'name', 'engine', 'environment', 'reason'
$script:FlagOptions = 'dry-run', 'yes', 'manual-migrations', 'non-interactive', 'all', 'help', 'confirm-stale-lock'
# Tarefa que entrega cada comando que aplica mudanças (specs/033-instalador-guiado/tasks.md).
$script:Pending = @{ upgrade = 'T048'; migrations = 'T030'; recreate = 'T042 a T044'; host = 'T046 e T047'; certificates = 'T049' }

function ConvertFrom-InstallerArguments {
    <#
    .SYNOPSIS
    Comando, subcomandos e opções. --yes confirma só o plano normal: remoção de dados, migrations e sobreposição de SQL
    exigem as confirmações nomeadas (FR-029, FR-036, FR-041).
    #>
    [CmdletBinding()]
    param([string[]] $Arguments = @())

    $parsed = [ordered]@{ Command = 'install'; Positional = [System.Collections.Generic.List[string]]::new(); Options = @{}; Flags = @{} }
    $index = 0
    if ($Arguments.Count -gt 0 -and -not $Arguments[0].StartsWith('--', [System.StringComparison]::Ordinal)) {
        $parsed.Command = $Arguments[0]
        $index = 1
    }

    if ($parsed.Command -notin $script:Commands) {
        Stop-Installer -Code Usage -Message "Comando desconhecido: $($parsed.Command). Consulte 'help'."
    }

    for (; $index -lt $Arguments.Count; $index++) {
        $token = $Arguments[$index]
        if (-not $token.StartsWith('--', [System.StringComparison]::Ordinal)) {
            $parsed.Positional.Add($token)
            continue
        }

        $name = $token.Substring(2)
        if ($name -in $script:FlagOptions) {
            $parsed.Flags[$name] = $true
        }
        elseif ($name -in $script:ValueOptions) {
            if ($index + 1 -ge $Arguments.Count -or $Arguments[$index + 1].StartsWith('--', [System.StringComparison]::Ordinal)) {
                Stop-Installer -Code Usage -Message "A opção $token precisa de um valor."
            }

            if ($parsed.Options.Contains($name)) {
                Stop-Installer -Code Usage -Message "A opção $token foi repetida."
            }

            $parsed.Options[$name] = $Arguments[++$index]
        }
        else {
            Stop-Installer -Code Usage -Message "Opção desconhecida: $token. Consulte 'help'."
        }
    }

    $parsed['Confirmations'] = [ordered]@{
        Plan         = [bool] $parsed.Flags['yes']
        DataRemoval  = @(Split-List $parsed.Options['confirm-data-removal'])
        MigrationsPlanHash = $parsed.Options['confirm-migrations']
        SqlOverrides = @(Split-List $parsed.Options['confirm-sql-overrides'])
        Backup       = $parsed.Options['confirm-backup']
    }
    if ($parsed.Confirmations.MigrationsPlanHash -and $parsed.Confirmations.MigrationsPlanHash -notmatch '^[0-9a-f]{64}$') {
        Stop-Installer -Code Usage -Message '--confirm-migrations precisa ser o planHash (64 caracteres hexadecimais) mostrado no plano.'
    }

    if ($parsed.Flags['non-interactive']) { $env:DOKPOD_NON_INTERACTIVE = '1' }
    return $parsed
}

function Split-List {
    param([string] $Value)

    if (-not $Value) { return @() }
    return @($Value -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

function Invoke-InstallerCli {
    <#
    .SYNOPSIS
    Executa a linha de comando e devolve o código de saída do contrato (0 a 7 e 10).
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param([string[]] $Arguments = @())

    try {
        if ($Arguments.Count -eq 0 -or $Arguments[0] -in '--help', '-h', '-?' -or ($Arguments[0] -eq 'help')) {
            Write-InstallerHelp -Topic $(if ($Arguments.Count -gt 1) { $Arguments[1] } else { $null })
            return 0
        }

        $parsed = ConvertFrom-InstallerArguments -Arguments $Arguments
        if ($parsed.Flags['help']) {
            Write-InstallerHelp -Topic $parsed.Command
            return 0
        }

        switch ($parsed.Command) {
            'plan' { return Invoke-InstallerPlanCommand -Parsed $parsed }
            'status' { return Invoke-InstallerStatusCommand -Parsed $parsed }
            'sql' { return Invoke-InstallerSqlCommand -Parsed $parsed }
            default {
                if ($parsed.Flags['dry-run'] -and $parsed.Command -in 'install', 'recreate') {
                    return Invoke-InstallerPlanCommand -Parsed $parsed
                }

                if ($parsed.Command -eq 'install') { return Invoke-InstallerInstallCommand -Parsed $parsed }

                Stop-Installer -Code Usage -Message "O comando '$($parsed.Command)' ainda não aplica mudanças nesta versão (spec 033, $($script:Pending[$parsed.Command])); use --dry-run para ver o plano."
            }
        }
    }
    catch {
        $exitCode = if ($_.Exception.PSObject.Properties['ExitCode']) { $_.Exception.ExitCode } else { 1 }
        Write-InstallerLine -ToStandardError -Message $_.Exception.Message
        return $exitCode
    }
}

function Write-InstallerHelp {
    param([string] $Topic)

    $text = @"
Instalador do Dokpod (spec 033): instala, atualiza e recria a stack em Docker ou Podman rootless.

Uso: sh install.sh [comando] [opções]

Comandos:
  install                   instala ou converge a instalação (padrão); com --dry-run, só mostra o plano
  plan                      mostra o plano, sem efeitos
  upgrade --version X.Y.Z   atualiza para uma versão maior
  migrations status|plan|export --out <arquivo>|verify
  sql export --out <pasta>  extrai o catálogo SQL da versão para revisão
  recreate <item>[,...] | --all
  host add --name <nome> [--engine docker|podman|windows] | host revoke --environment <id> --reason <texto>
  certificates check
  status                    estado por componente, sem efeitos
  help [comando]

Opções: --answers <arquivo> (absoluto), --install-dir, --secrets-dir, --version, --offline <pasta>, --dry-run, --yes,
--confirm-data-removal <lista>, --confirm-migrations <planHash>, --sql-dir <pasta>, --confirm-sql-overrides <lista>,
--confirm-backup <banco>, --manual-migrations, --non-interactive.

--yes confirma só o plano normal; remoção de dados, migrations, sobreposição de SQL e backup pedem a confirmação nomeada.

Códigos de saída: 0 sucesso; 1 erro na aplicação; 2 uso inválido; 3 pré-requisito; 4 confirmação recusada ou ausente;
5 assinatura ou manifesto; 6 divergência de migrations; 7 outra execução em andamento; 10 pausado para migration manual.

Contrato: specs/033-instalador-guiado/contracts/installer-cli.md. Runbook: docs/runbooks/instalacao.md.
"@
    Write-InstallerLine -Message $text
    if ($Topic -and $script:Pending.Contains($Topic)) {
        Write-InstallerLine -Message "`nO comando '$Topic' aplica mudanças a partir das tarefas $($script:Pending[$Topic]) da spec 033."
    }
}

function Get-CliAnswers {
    param($Parsed)

    if ($Parsed.Options['answers']) {
        $answers = Read-InstallerAnswers -Path $Parsed.Options['answers']
    }
    else {
        $answers = Request-InstallerAnswers
    }

    if ($Parsed.Options['install-dir']) { $answers['installDir'] = $Parsed.Options['install-dir'] }
    if ($Parsed.Options['secrets-dir']) { $answers['secretsDir'] = $Parsed.Options['secrets-dir'] }
    if ($Parsed.Options['version']) { $answers['version'] = $Parsed.Options['version'] }
    Assert-InstallPaths -InstallDirectory $answers.installDir -SecretsDirectory $answers.secretsDir
    return $answers
}

function Get-ReleaseManifestPath {
    <#
    .SYNOPSIS
    release-manifest.json da versão: do pacote offline, de DOKPOD_RELEASE_MANIFEST (testes) ou baixado da release.
    #>
    param([string] $Version, [string] $Offline)

    if ($env:DOKPOD_RELEASE_MANIFEST) { return $env:DOKPOD_RELEASE_MANIFEST }
    if ($Offline) { return Join-Path $Offline 'release-manifest.json' }
    $target = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-release-$Version.json"
    try {
        Invoke-WebRequest -Uri "https://github.com/altivy-it/dokpod/releases/download/v$Version/release-manifest.json" -OutFile $target -MaximumRetryCount 2 | Out-Null
    }
    catch {
        Stop-Installer -Code Signature -Message "Não foi possível obter o manifesto da release v$Version."
    }

    return $target
}

function Get-StateIfPresent {
    param([string] $InstallDirectory)

    if (-not (Test-Path -LiteralPath (Join-Path $InstallDirectory 'state/installation.db'))) { return $null }
    return Open-InstallerState -InstallDirectory $InstallDirectory
}

function Invoke-InstallerPlanCommand {
    <#
    .SYNOPSIS
    Plano completo sem nenhum efeito: não grava arquivos, estado, segredos nem containers (FR-004).
    #>
    param($Parsed)

    $answers = Get-CliAnswers -Parsed $Parsed
    $engine = Get-EngineFacts
    $state = Get-StateIfPresent -InstallDirectory $answers.installDir
    try {
        $observed = Get-ObservedServices
        $ownedPorts = @()
        if ($observed.Contains('edge')) { $ownedPorts += [int] $answers.edge.httpsPort }
        if ($answers.edge.mode -eq 'existing' -and $answers.edge.upstreamHost -isnot [System.Collections.IDictionary]) {
            foreach ($pair in @(@('web', 'webPort'), @('bff', 'bffPort'), @('keycloak', 'keycloakPort'))) {
                if ($observed.Contains($pair[0])) { $ownedPorts += [int] $answers.edge[$pair[1]] }
            }
        }
        if ($observed.Contains('api') -and $answers.Contains('agentGrpcPort') -and $answers.agentGrpcPort) { $ownedPorts += [int] $answers.agentGrpcPort }
        Write-InstallerLine -Message 'Pré-requisitos:'
        Assert-InstallerPrerequisites -Results (Test-InstallerPrerequisites -Answers $answers -Engine $engine -HostFacts (Get-HostFacts) -OwnedPorts $ownedPorts)

        $release = Read-ReleaseManifest -Path (Get-ReleaseManifestPath -Version $answers.version -Offline $Parsed.Options['offline']) -Version $answers.version
        $images = Select-ReleaseImages -Images $release -Edition $answers.edition
        $environmentId = if ($state) { (Invoke-StateSql -State $state -Name 'environments-list' -Rows | Select-Object -First 1)?.environment_id } else { $null }
        Assert-ExistingEdgeAnswers -Answers $answers
        $infrastructure = Get-InfrastructureImages
        $model = New-InstallerComposeModel -Answers $answers -Images $images -Infrastructure $infrastructure `
            -EngineSocket ($env:DOKPOD_HOST_ENGINE_SOCKET ?? '/run/engine.sock') -LocalEnvironmentId ($environmentId ?? '00000000-0000-0000-0000-000000000000') `
            -TrustedNetworks (Get-EdgeTrustedNetworks -Answers $answers)
        $hashes = Set-ComposeConfigHashLabels -Model $model
        $desired = Get-DesiredComponents -Answers $answers -ServiceHashes $hashes

        foreach ($component in 'secrets', 'pki', 'realm', 'migrations') {
            $observed[$component] = @{ Exists = $false; Pending = $null }
        }
        $observed['secrets'].Exists = Test-Path -LiteralPath (Join-Path $answers.secretsDir 'Authentication__Keycloak__ClientSecret')
        $observed['pki'].Exists = Test-Path -LiteralPath (Join-Path $answers.installDir 'certs/ca.crt')
        if ($state) {
            foreach ($row in (Invoke-StateSql -State $state -Name 'components-list' -Rows)) {
                if ($row.name -in 'realm', 'migrations') { $observed[$row.name].Exists = $true }
            }
        }

        $recreate = if ($Parsed.Command -eq 'recreate') { @($Parsed.Positional | ForEach-Object { ($_ -split '[:,]')[0] } | Select-Object -Unique) } else { @() }
        $plan = New-InstallerPlan -Desired $desired -Observed $observed -Recreate $recreate -RecreateAll:([bool] $Parsed.Flags['all']) `
            -ConfirmDataRemoval $Parsed.Confirmations.DataRemoval
        Write-InstallerLine -Message ''
        Write-InstallerLine -Message "Plano para o Dokpod $($answers.version) ($($answers.edition), $($answers.engine)):"
        Write-InstallerLine -Message (Format-InstallerPlan -Plan $plan)
        Write-InstallerLine -Message ''
        Write-InstallerLine -Message $(if (Test-InstallerPlanConverged -Plan $plan) { 'Convergido: nada a mudar.' } else { 'Nada foi alterado (plano sem efeitos).' })
        return 0
    }
    finally {
        if ($state) { Close-InstallerState -State $state }
    }
}

function Invoke-InstallerInstallCommand {
    <#
    .SYNOPSIS
    install: pré-requisitos, manifesto e assinaturas antes de qualquer efeito; depois as etapas de Install.psm1.
    #>
    param($Parsed)

    $answers = Get-CliAnswers -Parsed $Parsed
    if (-not $env:DOKPOD_INSTALLER_IMAGE_REF) {
        Stop-Installer -Code Usage -Message 'Rode o instalador pelo install.sh, que informa a imagem em execução (DOKPOD_INSTALLER_IMAGE_REF).'
    }

    $engine = Get-EngineFacts
    $observed = Get-ObservedServices
    $ownedPorts = @()
    if ($observed.Contains('edge')) { $ownedPorts += [int] $answers.edge.httpsPort }
    if ($observed.Contains('api') -and $answers.Contains('agentGrpcPort') -and $answers.agentGrpcPort) { $ownedPorts += [int] $answers.agentGrpcPort }
    Write-InstallerLine -Message 'Pré-requisitos:'
    Assert-InstallerPrerequisites -Results (Test-InstallerPrerequisites -Answers $answers -Engine $engine -HostFacts (Get-HostFacts) -OwnedPorts $ownedPorts)

    $offline = $Parsed.Options['offline']
    $release = Read-ReleaseManifest -Path (Get-ReleaseManifestPath -Version $answers.version -Offline $offline) -Version $answers.version
    $images = Select-ReleaseImages -Images $release -Edition $answers.edition
    if ($offline) { Import-OfflineImages -Directory $offline -Images @($images.Values) }
    # No pacote offline, as assinaturas de cada imagem (cosign save) ficam em signatures/<nome no manifesto>.
    foreach ($artifact in @($release.Keys | Where-Object { $release[$_] -in $images.Values })) {
        $signatures = if ($offline) { Join-Path $offline "signatures/$artifact" } else { $null }
        Confirm-ImageSignature -Image $release[$artifact] -OfflineDirectory $signatures
    }

    return Invoke-InstallerInstall -Answers $answers -Images $images -Infrastructure (Get-InfrastructureImages) -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
        -Confirmations $Parsed.Confirmations -SqlDirectory $Parsed.Options['sql-dir'] -ManualMigrations:([bool] $Parsed.Flags['manual-migrations']) `
        -ConfirmStaleLock:([bool] $Parsed.Flags['confirm-stale-lock'])
}

function Invoke-InstallerStatusCommand {
    param($Parsed)

    $installDir = $Parsed.Options['install-dir'] ?? (Join-Path ([System.Environment]::GetFolderPath('UserProfile')) '.local/share/dokpod')
    $state = Get-StateIfPresent -InstallDirectory $installDir
    if (-not $state) {
        Write-InstallerLine -Message "Nenhuma instalação do Dokpod em $installDir."
        return 0
    }

    try {
        $installation = Get-InstallationRecord -State $state
        Write-InstallerLine -Message "Dokpod $($installation?.version) ($($installation?.edition), $($installation?.engine)) em $($installation?.public_url)"
        $observed = Get-ObservedServices
        foreach ($row in (Invoke-StateSql -State $state -Name 'components-list' -Rows)) {
            $seen = if ($observed.Contains($row.name)) { $observed[$row.name] } else { $null }
            $status = if (-not $seen) { if ($row.name -in 'secrets', 'pki', 'realm', 'migrations') { 'aplicado' } else { 'ausente' } }
            elseif ($seen.Hash -ne $row.desired_hash) { 'divergente' }
            elseif (-not $seen.Running) { 'parado' }
            else { 'convergido' }
            Write-InstallerLine -Message ('  {0,-18} {1}' -f $row.name, $status)
        }

        $limit = [DateTimeOffset]::UtcNow.AddDays(-7)
        foreach ($environment in (Invoke-StateSql -State $state -Name 'environments-list' -Rows)) {
            if ($environment.package_generated_at -and -not $environment.revoked_at -and [DateTimeOffset]::Parse($environment.package_generated_at) -lt $limit) {
                Write-InstallerLine -Message "Aviso: o pacote do host $($environment.name) foi gerado há mais de 7 dias. Se o host não conectou, revogue: host revoke --environment $($environment.environment_id) --reason <motivo> (L6)."
            }
        }

        return 0
    }
    finally {
        Close-InstallerState -State $state
    }
}

function Invoke-InstallerSqlCommand {
    <#
    .SYNOPSIS
    sql export --out <pasta>: copia o catálogo SQL da versão (arquivos e manifest.json) para revisão (FR-040). Sem efeitos.
    #>
    param($Parsed)

    if ($Parsed.Positional.Count -ne 1 -or $Parsed.Positional[0] -ne 'export' -or -not $Parsed.Options['out']) {
        Stop-Installer -Code Usage -Message 'Uso: sql export --out <pasta>.'
    }

    $target = $Parsed.Options['out']
    if ((Test-Path -LiteralPath $target) -and (Get-ChildItem -LiteralPath $target -Force | Select-Object -First 1)) {
        Stop-Installer -Code Usage -Message "A pasta $target já tem arquivos; escolha uma vazia (nada é sobrescrito)."
    }

    $catalog = Join-Path (Get-InstallerHome) 'catalog'
    if (-not (Test-Path -LiteralPath (Join-Path $catalog 'manifest.json'))) {
        Stop-Installer -Code Error -Message 'O catálogo SQL não está nesta imagem do instalador.'
    }

    [void] [System.IO.Directory]::CreateDirectory($target)
    Copy-Item -Path (Join-Path $catalog '*') -Destination $target -Recurse
    Write-InstallerLine -Message "Catálogo SQL exportado em $target ($((Get-ChildItem -LiteralPath $target -Recurse -Filter '*.sql').Count) arquivos e manifest.json)."
    return 0
}

Export-ModuleMember -Function ConvertFrom-InstallerArguments, Invoke-InstallerCli
