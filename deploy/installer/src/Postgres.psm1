#Requires -Version 7.4
<#
.SYNOPSIS
PostgreSQL do Dokpod (spec 033 T028, FR-007, FR-008, FR-033, D-009, L5): credenciais das instâncias provisionadas,
bootstrap de banco e roles pelos arquivos do catálogo e conexões gravadas em secretsDir.

.DESCRIPTION
O SQL vem só do catálogo (Installation/bootstrap e Installation/checks), conferido pelo SHA-256 do manifesto. O psql
roda num container avulso da imagem do PostgreSQL, com raiz somente leitura, sem socket do engine, sem porta publicada
e só na rede do banco. Os arquivos (pgpass, senhas das roles, SQL e o roteiro) entram por stdin, num tar extraído para
um tmpfs que só esse container monta e que some com ele: nada sensível passa por argumento ou variável de ambiente, e
o `docker inspect` do container não mostra nenhum valor secreto.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module (Join-Path $PSScriptRoot 'Secrets.psm1')

# Instância provisionada (compose.yaml do Render.psm1): serviço, rede interna do projeto e role administrativa.
$script:Provisioned = [ordered]@{
    Host          = 'dokpod-postgres'
    Port          = 5432
    Network       = 'dokpod_dokpod-data'
    AdminUser     = 'dokpod_admin'
    AdminSecret   = 'dokpod-db-password'
    MaintenanceDb = 'postgres'
}
$script:Database = 'dokpod'
$script:OwnerRole = 'dokpod_owner'
$script:RuntimeRole = 'dokpod_app'
$script:OwnerConnection = 'dokpod-owner-connection'
$script:RuntimeConnection = 'ConnectionStrings__ControlPlane'
# PostgreSQL homologado: 17 e 18 (impressão do schema igual nos dois, T006).
$script:MinimumServerVersion = 170000
$script:PsqlUser = '999:999'
$script:PsqlRoot = '/run/dokpod-psql'
$script:StepMarker = '--dokpod-psql-step--'

function Get-PostgresDefaults {
    <#
    .SYNOPSIS
    Nomes fixos do PostgreSQL do Dokpod: banco, roles, segredos de conexão e a instância provisionada.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param()

    return [ordered]@{
        Database          = $script:Database
        OwnerRole         = $script:OwnerRole
        RuntimeRole       = $script:RuntimeRole
        OwnerConnection   = $script:OwnerConnection
        RuntimeConnection = $script:RuntimeConnection
        Provisioned       = $script:Provisioned
    }
}

function Initialize-PostgresSecrets {
    <#
    .SYNOPSIS
    Gera as senhas administrativas das instâncias provisionadas que ainda não existem em secretsDir (FR-007, FR-028).
    Cada instância tem a sua: dokpod-db-password e, com Keycloak provisionado, keycloak-db-password.
    .OUTPUTS
    Os nomes dos segredos criados nesta execução.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $names = @()
    if ($Answers.postgres.mode -eq 'provision') { $names += $script:Provisioned.AdminSecret }
    if ($Answers.keycloak.mode -eq 'provision') { $names += 'keycloak-db-password' }

    $created = @()
    foreach ($name in $names) {
        if (-not (Test-InstallerSecret -SecretsDirectory $Answers.secretsDir -Name $name)) {
            Write-InstallerSecret -SecretsDirectory $Answers.secretsDir -Name $name -Value (New-InstallerSecret)
            $created += $name
        }
    }

    return , $created
}

function ConvertTo-PostgresConnectionString {
    <#
    .SYNOPSIS
    Connection string no formato do Npgsql, com valores escapados pelo DbConnectionStringBuilder.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string] $Server,
        [Parameter(Mandatory)][int] $Port,
        [Parameter(Mandatory)][string] $Database,
        [Parameter(Mandatory)][string] $Username,
        [Parameter(Mandatory)][string] $Password
    )

    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder['Host'] = $Server
    $builder['Port'] = $Port
    $builder['Database'] = $Database
    $builder['Username'] = $Username
    $builder['Password'] = $Password
    return $builder.get_ConnectionString()
}

function Read-PostgresConnection {
    <#
    .SYNOPSIS
    Lê uma conexão gravada em secretsDir e devolve as partes; $null quando o arquivo não existe.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $SecretsDirectory,
        [Parameter(Mandatory)][string] $Name
    )

    if (-not (Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $Name)) {
        return $null
    }

    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    # Atribuição pelo setter: num dicionário, `$builder.ConnectionString = ...` criaria uma chave com esse nome.
    $builder.set_ConnectionString((Read-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $Name))
    foreach ($key in 'Host', 'Port', 'Database', 'Username', 'Password') {
        if (-not $builder.ContainsKey($key)) {
            Stop-Installer -Code Error -Message "A conexão '$Name' em secretsDir não tem '$key'; recrie com 'recreate secret:$Name'."
        }
    }

    Register-InstallerSecret -Value ([string] $builder['Password'])
    return [pscustomobject]@{
        Host     = [string] $builder['Host']
        Port     = [int] $builder['Port']
        Database = [string] $builder['Database']
        Username = [string] $builder['Username']
        Password = [string] $builder['Password']
    }
}

function Get-CatalogSqlFile {
    <#
    .SYNOPSIS
    Conteúdo de um arquivo do catálogo SQL, conferido pelo SHA-256 do manifesto, e a declaração dos seus parâmetros.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Path,
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog')
    )

    $manifestPath = Join-Path $CatalogRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Stop-Installer -Code Error -Message 'O catálogo SQL não está nesta imagem do instalador.'
    }

    $entry = @((Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable).files | Where-Object { $_.path -eq $Path })
    if ($entry.Count -ne 1) {
        Stop-Installer -Code Error -Message "O arquivo $Path não está no manifesto do catálogo SQL."
    }

    $content = [System.IO.File]::ReadAllText((Join-Path $CatalogRoot $Path))
    if ((Get-Sha256Hex -Text $content) -ne $entry[0].sha256) {
        Stop-Installer -Code Error -Message "O arquivo $Path do catálogo não confere com o SHA-256 do manifesto."
    }

    if ($entry[0].executor -ne 'psql') {
        Stop-Installer -Code Error -Message "O arquivo $Path é do executor '$($entry[0].executor)', não do psql."
    }

    return [pscustomobject]@{ Path = $Path; Content = $content; Parameters = @($entry[0].parameters) }
}

function ConvertTo-ShellLiteral {
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Value)

    return "'" + $Value.Replace("'", "'\''") + "'"
}

function ConvertTo-PgpassField {
    param([Parameter(Mandatory)][string] $Value)

    return $Value.Replace('\', '\\').Replace(':', '\:')
}

function New-PsqlBundle {
    <#
    .SYNOPSIS
    Tar (ustar) com pgpass, senhas, SQL e o roteiro run.sh que executa cada passo. Só existe em memória e no tmpfs.
    #>
    [CmdletBinding()]
    [OutputType([byte[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Server,
        [Parameter(Mandatory)][object[]] $Steps,
        [Parameter(Mandatory)][string] $CatalogRoot
    )

    $files = [ordered]@{}
    $files['pgpass'] = '*:*:*:{0}:{1}' -f (ConvertTo-PgpassField $Server.Username), (ConvertTo-PgpassField $Server.Password)
    $script = [System.Text.StringBuilder]::new()
    [void] $script.AppendLine('set -eu')
    [void] $script.AppendLine("trap 'rm -rf $($script:PsqlRoot)/pgpass $($script:PsqlRoot)/secrets' EXIT")
    [void] $script.AppendLine("export PGPASSFILE=$($script:PsqlRoot)/pgpass PGCONNECT_TIMEOUT=10")
    [void] $script.AppendLine('unset PGPASSWORD')

    $index = 0
    foreach ($step in $Steps) {
        $index++
        $file = Get-CatalogSqlFile -Path $step.Path -CatalogRoot $CatalogRoot
        $variables = if ($step.Contains('Variables')) { $step.Variables } else { @{} }
        $secrets = if ($step.Contains('Secrets')) { $step.Secrets } else { @{} }
        $arguments = @('psql', '-X', '-q', '-At', '-F', '|', '-v', 'ON_ERROR_STOP=1', '-h', $Server.Host, '-p', [string] $Server.Port,
            '-U', $Server.Username, '-d', $step.Database)

        # Cada parâmetro declarado no manifesto chega pelo canal do seu tipo: segredo só como arquivo, o resto por -v.
        foreach ($parameter in $file.Parameters) {
            $name = $parameter.name
            if ($parameter.kind -eq 'secret') {
                if (-not $secrets.Contains($name) -or $variables.Contains($name)) {
                    Stop-Installer -Code Error -Message "O parâmetro secreto $name de $($step.Path) precisa vir como arquivo."
                }

                $files["secrets/$index-$name"] = [string] $secrets[$name]
                $arguments += @('-v', "$name=$($script:PsqlRoot)/secrets/$index-$name")
            }
            else {
                if (-not $variables.Contains($name) -or $secrets.Contains($name)) {
                    Stop-Installer -Code Error -Message "O parâmetro $name de $($step.Path) não foi informado."
                }

                $arguments += @('-v', "$name=$($variables[$name])")
            }
        }

        $declared = @($file.Parameters | ForEach-Object { $_.name })
        foreach ($name in @($variables.Keys) + @($secrets.Keys)) {
            if ($name -notin $declared) {
                Stop-Installer -Code Error -Message "O parâmetro $name não é declarado por $($step.Path) no manifesto."
            }
        }

        $files["sql/$index.sql"] = $file.Content
        $arguments += @('-f', "$($script:PsqlRoot)/sql/$index.sql")
        [void] $script.AppendLine("echo $($script:StepMarker)")
        [void] $script.AppendLine(($arguments | ForEach-Object { ConvertTo-ShellLiteral $_ }) -join ' ')
    }

    $files['run.sh'] = $script.ToString()
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.Formats.Tar.TarWriter]::new($stream, [System.Formats.Tar.TarEntryFormat]::Ustar, $true)
    try {
        foreach ($directory in 'secrets', 'sql') {
            $entry = [System.Formats.Tar.UstarTarEntry]::new([System.Formats.Tar.TarEntryType]::Directory, "$directory/")
            $entry.Mode = [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute'
            $writer.WriteEntry($entry)
        }

        foreach ($name in $files.Keys) {
            $entry = [System.Formats.Tar.UstarTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, $name)
            $entry.Mode = [System.IO.UnixFileMode]'UserRead, UserWrite'
            $entry.DataStream = [System.IO.MemoryStream]::new([System.Text.Encoding]::UTF8.GetBytes($files[$name]))
            $writer.WriteEntry($entry)
        }
    }
    finally {
        $writer.Dispose()
    }

    return , $stream.ToArray()
}

function Invoke-PsqlCatalog {
    <#
    .SYNOPSIS
    Executa arquivos do catálogo com o psql da imagem do PostgreSQL, num container avulso e isolado (FR-033, L5).
    .PARAMETER Server
    Host, Port, Username e Password da conexão administrativa.
    .PARAMETER Steps
    Lista de @{ Path; Database; Variables; Secrets }, executados em ordem e interrompidos no primeiro erro.
    .PARAMETER Network
    Rede do banco: a interna do projeto no PostgreSQL provisionado. Nunca a do engine nem a borda.
    .OUTPUTS
    A saída de cada passo, na ordem.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)][string] $Image,
        [Parameter(Mandatory)][string] $Network,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Server,
        [Parameter(Mandatory)][object[]] $Steps,
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog'),
        [string] $Cli = 'docker'
    )

    Register-InstallerSecret -Value $Server.Password
    $bundle = New-PsqlBundle -Server $Server -Steps $Steps -CatalogRoot $CatalogRoot
    $arguments = @('run', '--rm', '--interactive', '--log-driver', 'none', '--read-only', '--network', $Network,
        '--user', $script:PsqlUser, '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
        '--tmpfs', "$($script:PsqlRoot):rw,noexec,nosuid,nodev,size=4m,mode=1777",
        '--label', 'io.dokpod.installer.task=psql', '--name', "dokpod-installer-psql-$([guid]::NewGuid().ToString('N').Substring(0, 12))",
        '--entrypoint', 'sh', $Image, '-c', "umask 077 && tar -x -f - -C $($script:PsqlRoot) && exec sh $($script:PsqlRoot)/run.sh")

    $result = Invoke-InstallerProcess -FilePath $Cli -ArgumentList $arguments -InputBytes $bundle
    $outputs = @(($result.Output -split [regex]::Escape("$($script:StepMarker)`n")) | Select-Object -Skip 1 | ForEach-Object { $_.TrimEnd("`n") })
    if ($result.ExitCode -ne 0) {
        $failed = $Steps[[System.Math]::Max(0, $outputs.Count - 1)].Path
        Stop-Installer -Code Error -Message "O psql falhou em $failed (código $($result.ExitCode)): $($result.Error.Trim())"
    }

    return , $outputs
}

function Get-PostgresServerVersion {
    <#
    .SYNOPSIS
    server_version_num do servidor (Installation/checks/server-version.sql); abaixo do homologado dá código 3 (FR-003).
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)][string] $Image,
        [Parameter(Mandatory)][string] $Network,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Server,
        [string] $MaintenanceDatabase = 'postgres',
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog'),
        [string] $Cli = 'docker'
    )

    $output = Invoke-PsqlCatalog -Image $Image -Network $Network -Server $Server -CatalogRoot $CatalogRoot -Cli $Cli -Steps @(
        @{ Path = 'Installation/checks/server-version.sql'; Database = $MaintenanceDatabase })
    $version = [int] $output[0].Trim()
    if ($version -lt $script:MinimumServerVersion) {
        Stop-Installer -Code Prerequisite -Message "PostgreSQL $version é anterior ao homologado ($($script:MinimumServerVersion)); use o 17 ou o 18."
    }

    return $version
}

function Get-PostgresRolePrivilege {
    <#
    .SYNOPSIS
    Resultado de Installation/checks/role-privileges.sql para uma role, como dicionário verificação → bool.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param(
        [Parameter(Mandatory)][string] $Image,
        [Parameter(Mandatory)][string] $Network,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Server,
        [Parameter(Mandatory)][string] $Database,
        [Parameter(Mandatory)][string] $Role,
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog'),
        [string] $Cli = 'docker'
    )

    $output = Invoke-PsqlCatalog -Image $Image -Network $Network -Server $Server -CatalogRoot $CatalogRoot -Cli $Cli -Steps @(
        @{ Path = 'Installation/checks/role-privileges.sql'; Database = $Database; Variables = [ordered]@{ role = $Role; database = $Database } })
    $checks = [ordered]@{}
    foreach ($line in ($output[0] -split "`n" | Where-Object { $_ })) {
        $name, $value = $line -split '\|', 2
        $checks[$name] = $value -eq 't'
    }

    return $checks
}

function Assert-PostgresRole {
    <#
    .SYNOPSIS
    Confere o menor privilégio (D-009): nenhuma role com atributo administrativo; a dona é dona do schema; a de runtime
    conecta, está no grupo dokpod_runtime e não cria objetos no schema.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Owner,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Runtime
    )

    $problems = @()
    foreach ($pair in @(@{ Name = 'dona'; Checks = $Owner }, @{ Name = 'de runtime'; Checks = $Runtime })) {
        foreach ($check in 'exists', 'login', 'database_connect') {
            if (-not $pair.Checks[$check]) { $problems += "role $($pair.Name) sem $check" }
        }

        foreach ($check in 'superuser', 'createrole', 'createdb', 'replication', 'bypassrls') {
            if ($pair.Checks[$check]) { $problems += "role $($pair.Name) com $check" }
        }
    }

    if (-not $Owner['schema_owner']) { $problems += 'role dona não é dona do schema dokpod' }
    if (-not $Runtime['runtime_group_member']) { $problems += 'role de runtime fora do grupo dokpod_runtime' }
    if ($Runtime['schema_create'] -or $Runtime['schema_owner']) { $problems += 'role de runtime pode criar objetos no schema dokpod' }
    if ($problems) {
        Stop-Installer -Code Error -Message "As roles do Dokpod não ficaram com o menor privilégio: $($problems -join '; ')."
    }
}

function Invoke-PostgresBootstrap {
    <#
    .SYNOPSIS
    Banco, role dona e role de runtime pelos arquivos Installation/bootstrap do catálogo, conferência do menor privilégio
    e gravação das conexões dokpod-owner-connection e ConnectionStrings__ControlPlane em secretsDir (FR-007, FR-008).
    .DESCRIPTION
    Idempotente: as senhas das roles vêm das conexões já gravadas e só são geradas quando faltam; o bootstrap reaplica
    atributos, senha e participação no grupo. Uma conexão existente nunca é sobrescrita (FR-028).
    .PARAMETER Server
    Conexão administrativa (Host, Port, Username, Password), alcançável pela rede -Network.
    .PARAMETER ApplicationHost
    Host e porta que a API e o modo administrativo usam; no provisionado, o próprio serviço dokpod-postgres.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string] $SecretsDirectory,
        [Parameter(Mandatory)][string] $Image,
        [Parameter(Mandatory)][string] $Network,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Server,
        [string] $MaintenanceDatabase = 'postgres',
        [string] $Database = $script:Database,
        [string] $OwnerRole = $script:OwnerRole,
        [string] $RuntimeRole = $script:RuntimeRole,
        [string] $ApplicationHost = $Server.Host,
        [int] $ApplicationPort = $Server.Port,
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog'),
        [string] $Cli = 'docker'
    )

    $common = @{ Image = $Image; Network = $Network; Server = $Server; CatalogRoot = $CatalogRoot; Cli = $Cli }
    $version = Get-PostgresServerVersion @common -MaintenanceDatabase $MaintenanceDatabase

    $connections = [ordered]@{}
    foreach ($item in @(@{ Name = $script:OwnerConnection; Role = $OwnerRole }, @{ Name = $script:RuntimeConnection; Role = $RuntimeRole })) {
        $existing = Read-PostgresConnection -SecretsDirectory $SecretsDirectory -Name $item.Name
        if ($existing -and $existing.Username -ne $item.Role) {
            Stop-Installer -Code Error -Message "A conexão '$($item.Name)' é da role $($existing.Username), não de $($item.Role); recrie com 'recreate secret:$($item.Name)'."
        }

        $connections[$item.Name] = [pscustomobject]@{
            Role     = $item.Role
            Password = if ($existing) { $existing.Password } else { New-InstallerSecret }
            Exists   = [bool] $existing
        }
    }

    $owner = $connections[$script:OwnerConnection]
    $runtime = $connections[$script:RuntimeConnection]
    [void] (Invoke-PsqlCatalog @common -Steps @(
            @{
                Path = 'Installation/bootstrap/01-create-roles.sql'; Database = $MaintenanceDatabase
                Variables = [ordered]@{ owner_role = $OwnerRole; runtime_role = $RuntimeRole }
                Secrets = [ordered]@{ owner_password_file = $owner.Password; runtime_password_file = $runtime.Password }
            }
            @{
                Path = 'Installation/bootstrap/02-create-database.sql'; Database = $MaintenanceDatabase
                Variables = [ordered]@{ database = $Database; owner_role = $OwnerRole }
            }
            @{
                Path = 'Installation/bootstrap/03-grants.sql'; Database = $Database
                Variables = [ordered]@{ database = $Database; runtime_role = $RuntimeRole }
            }))

    $ownerChecks = Get-PostgresRolePrivilege @common -Database $Database -Role $OwnerRole
    $runtimeChecks = Get-PostgresRolePrivilege @common -Database $Database -Role $RuntimeRole
    Assert-PostgresRole -Owner $ownerChecks -Runtime $runtimeChecks

    $written = @()
    foreach ($name in $connections.Keys) {
        $item = $connections[$name]
        if (-not $item.Exists) {
            $value = ConvertTo-PostgresConnectionString -Server $ApplicationHost -Port $ApplicationPort -Database $Database -Username $item.Role -Password $item.Password
            Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $name -Value $value
            $written += $name
        }
    }

    return [pscustomobject]@{ ServerVersion = $version; Database = $Database; OwnerRole = $OwnerRole; RuntimeRole = $RuntimeRole; ConnectionsWritten = $written }
}

function Invoke-ProvisionedPostgresBootstrap {
    <#
    .SYNOPSIS
    Bootstrap no dokpod-postgres provisionado (já em execução pelo compose), com a senha administrativa de secretsDir.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Image,
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog'),
        [string] $Cli = 'docker'
    )

    if ($Answers.postgres.mode -ne 'provision') {
        Stop-Installer -Code Error -Message "postgres.mode '$($Answers.postgres.mode)' não é o provisionado."
    }

    $server = [ordered]@{
        Host     = $script:Provisioned.Host
        Port     = $script:Provisioned.Port
        Username = $script:Provisioned.AdminUser
        Password = Read-InstallerSecret -SecretsDirectory $Answers.secretsDir -Name $script:Provisioned.AdminSecret
    }

    return Invoke-PostgresBootstrap -SecretsDirectory $Answers.secretsDir -Image $Image -Network $script:Provisioned.Network -Server $server `
        -MaintenanceDatabase $script:Provisioned.MaintenanceDb -CatalogRoot $CatalogRoot -Cli $Cli
}

Export-ModuleMember -Function Get-PostgresDefaults, Initialize-PostgresSecrets, ConvertTo-PostgresConnectionString, Read-PostgresConnection,
Get-CatalogSqlFile, Invoke-PsqlCatalog, Get-PostgresServerVersion, Get-PostgresRolePrivilege, Assert-PostgresRole, Invoke-PostgresBootstrap,
Invoke-ProvisionedPostgresBootstrap
