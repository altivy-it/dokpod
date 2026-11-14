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
# sslMode das respostas → libpq (psql) e Npgsql (API e modo administrativo).
$script:SslModes = [ordered]@{ 'require' = 'Require'; 'verify-ca' = 'VerifyCA'; 'verify-full' = 'VerifyFull' }
# Containers avulsos com PostgreSQL existente: a rede padrão do engine, que tem saída (a dokpod-data só existe no compose).
$script:ExistingNetwork = 'bridge'

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
        ExistingNetwork   = $script:ExistingNetwork
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
        [Parameter(Mandatory)][string] $Password,
        # require, verify-ca ou verify-full; a cadeia é validada pelas raízes de SSL_CERT_FILE (o bundle da instalação).
        [ValidateSet('', 'require', 'verify-ca', 'verify-full')][string] $SslMode = ''
    )

    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder['Host'] = $Server
    $builder['Port'] = $Port
    $builder['Database'] = $Database
    $builder['Username'] = $Username
    $builder['Password'] = $Password
    if ($SslMode) { $builder['SSL Mode'] = $script:SslModes[$SslMode] }
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
    if ($Server.Contains('SslMode') -and $Server.SslMode) {
        [void] $script.AppendLine("export PGSSLMODE=$($Server.SslMode)")
        if ($Server.Contains('CaCertificate') -and $Server.CaCertificate) {
            $files['ca.crt'] = $Server.CaCertificate
            [void] $script.AppendLine("export PGSSLROOTCERT=$($script:PsqlRoot)/ca.crt")
        }
        elseif ($Server.SslMode -ne 'require') {
            [void] $script.AppendLine('export PGSSLROOTCERT=system')
        }
    }

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
    return , (New-InstallerTarBundle -Files $files)
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
    $arguments = @('run', '--rm') + (Get-ContainerLimitArguments) + @('--interactive', '--log-driver', 'none', '--read-only', '--network', $Network,
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
    conecta, está no grupo dokpod_runtime e não cria objetos no schema nem no banco.
    .PARAMETER Code
    Error depois de um bootstrap do próprio instalador; Prerequisite para roles preparadas pelo usuário (existing-prepared).
    .PARAMETER AllowMissingSchema
    Banco preparado antes das migrations: sem o schema dokpod, basta a role dona poder criá-lo (CREATE no banco).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Owner,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Runtime,
        [string] $OwnerRole = $script:OwnerRole,
        [string] $RuntimeRole = $script:RuntimeRole,
        [string] $Database = $script:Database,
        [ValidateSet('Error', 'Prerequisite')][string] $Code = 'Error',
        [switch] $AllowMissingSchema
    )

    $missing = [ordered]@{ exists = 'não existe'; login = 'sem LOGIN'; database_connect = "sem CONNECT no banco $Database" }
    $excess = [ordered]@{ superuser = 'com SUPERUSER'; createrole = 'com CREATEROLE'; createdb = 'com CREATEDB'; replication = 'com REPLICATION'; bypassrls = 'com BYPASSRLS' }
    $problems = @()
    foreach ($pair in @(@{ Name = "dona $OwnerRole"; Checks = $Owner }, @{ Name = "de runtime $RuntimeRole"; Checks = $Runtime })) {
        if (-not $pair.Checks['exists']) { $problems += "role $($pair.Name) não existe"; continue }
        foreach ($check in 'login', 'database_connect') {
            if (-not $pair.Checks[$check]) { $problems += "role $($pair.Name) $($missing[$check])" }
        }

        foreach ($check in $excess.Keys) {
            if ($pair.Checks[$check]) { $problems += "role $($pair.Name) $($excess[$check])" }
        }
    }

    if ($Owner['exists'] -and -not $Owner['schema_owner']) {
        if (-not ($AllowMissingSchema -and -not $Owner['schema_exists'])) { $problems += "role dona $OwnerRole não é dona do schema dokpod" }
        elseif (-not $Owner['database_create']) { $problems += "role dona $OwnerRole sem CREATE no banco $Database (para criar o schema dokpod)" }
    }

    if ($Runtime['exists']) {
        if (-not $Runtime['runtime_group_member']) { $problems += "role de runtime $RuntimeRole fora do grupo dokpod_runtime" }
        if ($Runtime['schema_create'] -or $Runtime['schema_owner']) { $problems += "role de runtime $RuntimeRole com CREATE no schema dokpod" }
        if ($Runtime['database_create']) { $problems += "role de runtime $RuntimeRole com CREATE no banco $Database" }
    }

    if ($problems) {
        Stop-Installer -Code $Code -Message "As roles do Dokpod não têm o menor privilégio: $($problems -join '; ')."
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
    $sslMode = if ($Server.Contains('SslMode')) { [string] $Server.SslMode } else { '' }
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
                Path = 'Installation/bootstrap/03-grants.sql'; Database = $MaintenanceDatabase
                Variables = [ordered]@{ database = $Database; owner_role = $OwnerRole; runtime_role = $RuntimeRole }
            }))

    # A conferência roda no banco do Dokpod com a própria role dona: um administrador sem SUPERUSER não conecta mais
    # nele, e o catálogo de roles e os privilégios de schema são legíveis por qualquer role.
    $asOwner = [ordered]@{} + $Server
    $asOwner.Username = $OwnerRole
    $asOwner.Password = $owner.Password
    $checking = @{} + $common
    $checking.Server = $asOwner
    $ownerChecks = Get-PostgresRolePrivilege @checking -Database $Database -Role $OwnerRole
    $runtimeChecks = Get-PostgresRolePrivilege @checking -Database $Database -Role $RuntimeRole
    Assert-PostgresRole -Owner $ownerChecks -Runtime $runtimeChecks -OwnerRole $OwnerRole -RuntimeRole $RuntimeRole -Database $Database

    $written = @()
    foreach ($name in $connections.Keys) {
        $item = $connections[$name]
        if (-not $item.Exists) {
            $value = ConvertTo-PostgresConnectionString -Server $ApplicationHost -Port $ApplicationPort -Database $Database -Username $item.Role -Password $item.Password -SslMode $sslMode
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
        [string] $Network = $script:Provisioned.Network,
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

    return Invoke-PostgresBootstrap -SecretsDirectory $Answers.secretsDir -Image $Image -Network $Network -Server $server `
        -MaintenanceDatabase $script:Provisioned.MaintenanceDb -CatalogRoot $CatalogRoot -Cli $Cli
}

function Assert-ExistingPostgresAnswers {
    <#
    .SYNOPSIS
    Confere o PostgreSQL existente antes de qualquer efeito: o host precisa ser alcançável pelos containers (loopback
    aponta para o próprio container) e a CA informada precisa ser um certificado PEM.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $postgres = $Answers.postgres
    if ($postgres.mode -eq 'provision') { return }

    $hostName = ([string] $postgres.host).Trim('[', ']')
    $address = $null
    $loopback = $hostName -eq 'localhost' -or $hostName.EndsWith('.localhost') -or
        ([System.Net.IPAddress]::TryParse($hostName, [ref] $address) -and ([System.Net.IPAddress]::IsLoopback($address) -or $address.Equals([System.Net.IPAddress]::Any)))
    if ($loopback) {
        Stop-Installer -Code Usage -Message ("postgres.host '$hostName' é o loopback, que dentro do container da API é o próprio container. " +
            'Informe o nome ou o IP pelo qual os containers alcançam o PostgreSQL: o do servidor na rede ou, com ele no próprio host, ' +
            'host.containers.internal no Podman rootless e o IP do host na rede no Docker rootless.')
    }

    $null = Get-ExistingPostgresCaPem -Answers $Answers
}

function Get-ExistingPostgresCaPem {
    <#
    .SYNOPSIS
    O PEM de postgres.caCertificate (público, vai para o bundle de SSL_CERT_FILE), ou $null quando não foi informado.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $postgres = $Answers.postgres
    if ($postgres.mode -eq 'provision' -or -not $postgres.Contains('caCertificate')) { return $null }

    $path = [string] $postgres.caCertificate
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Stop-Installer -Code Usage -Message "O arquivo de postgres.caCertificate não existe: $path."
    }

    $pem = [System.IO.File]::ReadAllText($path)
    if ($pem -notmatch '-----BEGIN CERTIFICATE-----' -or $pem -match 'PRIVATE KEY-----') {
        Stop-Installer -Code Usage -Message "postgres.caCertificate ($path) precisa ter só certificados PEM."
    }

    return $pem.TrimEnd() + "`n"
}

function Get-ExistingPostgresAdminServer {
    <#
    .SYNOPSIS
    Conexão administrativa do PostgreSQL existente: a senha vem de { file }/{ env } ou do prompt, fica só em memória
    e nunca é gravada em secretsDir, no estado ou no relatório (FR-008, FR-015).
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [bool] $Interactive = (Test-InteractiveSession),
        [scriptblock] $AskCredential = { param($message) Read-Host -Prompt $message -MaskInput }
    )

    $postgres = $Answers.postgres
    $admin = $postgres.admin
    $adminCredential = if ($admin.Contains('password')) {
        Read-SecretReference -Reference $admin.password -Purpose "postgres.admin ($($admin.username))"
    }
    elseif ($Interactive) {
        [string] (& $AskCredential "Senha de $($admin.username) no PostgreSQL $($postgres.host) (usada só nesta execução, não é gravada)")
    }
    else {
        Stop-Installer -Code Usage -Message 'Sem terminal, postgres.admin.password precisa vir como { file } ou { env } no arquivo de respostas.'
    }

    if ([string]::IsNullOrEmpty($adminCredential)) {
        Stop-Installer -Code Usage -Message "A senha de $($admin.username) no PostgreSQL está vazia."
    }

    Register-InstallerSecret -Value $adminCredential
    return New-ExistingPostgresServer -Answers $Answers -Username $admin.username -Password $adminCredential
}

function New-ExistingPostgresServer {
    param([System.Collections.IDictionary] $Answers, [string] $Username, [string] $Password)

    $postgres = $Answers.postgres
    $server = [ordered]@{ Host = [string] $postgres.host; Port = [int] $postgres.port; Username = $Username; Password = $Password }
    if ($postgres.Contains('sslMode')) { $server['SslMode'] = [string] $postgres.sslMode }
    $ca = Get-ExistingPostgresCaPem -Answers $Answers
    if ($ca) { $server['CaCertificate'] = $ca }
    return $server
}

function Invoke-ExistingPostgresBootstrap {
    <#
    .SYNOPSIS
    postgres.mode existing-create (T035, FR-008, D-009): banco, role dona e role de runtime num PostgreSQL existente
    pelos mesmos arquivos de bootstrap do catálogo, com a credencial administrativa usada uma vez e descartada.
    .DESCRIPTION
    Com as duas conexões já em secretsDir (reexecução ou retomada depois do bootstrap), não pede a credencial
    administrativa: confere versão e menor privilégio com a própria role dona e devolve sem mudar nada.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Image,
        [string] $Network = $script:ExistingNetwork,
        [bool] $Interactive = (Test-InteractiveSession),
        [scriptblock] $AskCredential = { param($message) Read-Host -Prompt $message -MaskInput },
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog'),
        [string] $Cli = 'docker'
    )

    $postgres = $Answers.postgres
    if ($postgres.mode -ne 'existing-create') {
        Stop-Installer -Code Error -Message "postgres.mode '$($postgres.mode)' não é o existing-create."
    }

    Assert-ExistingPostgresAnswers -Answers $Answers
    $database = if ($postgres.Contains('database') -and $postgres.database) { [string] $postgres.database } else { $script:Database }
    $owner = Read-PostgresConnection -SecretsDirectory $Answers.secretsDir -Name $script:OwnerConnection
    $runtime = Read-PostgresConnection -SecretsDirectory $Answers.secretsDir -Name $script:RuntimeConnection
    if ($owner -and $runtime) {
        if ($owner.Host -ne $postgres.host -or $owner.Port -ne [int] $postgres.port -or $owner.Database -ne $database) {
            Stop-Installer -Code Error -Message ("As conexões em secretsDir apontam para $($owner.Host):$($owner.Port)/$($owner.Database), não para " +
                "$($postgres.host):$($postgres.port)/$database; recrie com 'recreate secret:$($script:OwnerConnection)'.")
        }

        $server = New-ExistingPostgresServer -Answers $Answers -Username $owner.Username -Password $owner.Password
        $common = @{ Image = $Image; Network = $Network; Server = $server; CatalogRoot = $CatalogRoot; Cli = $Cli }
        $version = Get-PostgresServerVersion @common -MaintenanceDatabase $database
        Assert-PostgresRole -Owner (Get-PostgresRolePrivilege @common -Database $database -Role $owner.Username) `
            -Runtime (Get-PostgresRolePrivilege @common -Database $database -Role $runtime.Username) -OwnerRole $owner.Username -RuntimeRole $runtime.Username -Database $database
        return [pscustomobject]@{ ServerVersion = $version; Database = $database; OwnerRole = $owner.Username; RuntimeRole = $runtime.Username; ConnectionsWritten = @() }
    }

    $server = Get-ExistingPostgresAdminServer -Answers $Answers -Interactive $Interactive -AskCredential $AskCredential
    try {
        return Invoke-PostgresBootstrap -SecretsDirectory $Answers.secretsDir -Image $Image -Network $Network -Server $server `
            -MaintenanceDatabase $script:Provisioned.MaintenanceDb -Database $database -CatalogRoot $CatalogRoot -Cli $Cli
    }
    finally {
        $server.Password = $null
    }
}

function Invoke-PreparedPostgresBootstrap {
    <#
    .SYNOPSIS
    postgres.mode existing-prepared (T036, FR-008, D-009): banco e roles preparados pelo usuário. Sem credencial
    administrativa, confere conexão, versão e menor privilégio com Installation/checks e grava as conexões.
    .DESCRIPTION
    Tudo com a role dona, no banco do Dokpod: a versão (server-version.sql) e os privilégios das duas roles
    (role-privileges.sql). A de runtime também precisa conectar com a senha informada. Excesso ou falta de privilégio,
    e uma role que não conecta, dão código 3 com o que falta ou sobra; nada é alterado no servidor.
    As conexões já gravadas em secretsDir nunca são sobrescritas (FR-028): diferentes das respostas, a execução para.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Image,
        [string] $Network = $script:ExistingNetwork,
        [string] $CatalogRoot = (Join-Path (Get-InstallerHome) 'catalog'),
        [string] $Cli = 'docker'
    )

    $postgres = $Answers.postgres
    if ($postgres.mode -ne 'existing-prepared') {
        Stop-Installer -Code Error -Message "postgres.mode '$($postgres.mode)' não é o existing-prepared."
    }

    Assert-ExistingPostgresAnswers -Answers $Answers
    $database = [string] $postgres.database
    $roles = [ordered]@{}
    foreach ($item in @(@{ Name = $script:OwnerConnection; Key = 'owner' }, @{ Name = $script:RuntimeConnection; Key = 'runtime' })) {
        $section = $postgres[$item.Key]
        $credential = Read-SecretReference -Reference $section.password -Purpose "postgres.$($item.Key) ($($section.username))"
        if ([string]::IsNullOrEmpty($credential)) {
            Stop-Installer -Code Usage -Message "A senha de postgres.$($item.Key) ($($section.username)) está vazia."
        }

        Register-InstallerSecret -Value $credential
        $roles[$item.Name] = [pscustomobject]@{ Name = $item.Name; Username = [string] $section.username; Password = $credential }
    }

    $owner = $roles[$script:OwnerConnection]
    $runtime = $roles[$script:RuntimeConnection]
    if ($owner.Username -eq $runtime.Username) {
        Stop-Installer -Code Usage -Message "postgres.owner e postgres.runtime precisam ser roles diferentes ($($owner.Username)): a de runtime não pode ter DDL (D-009)."
    }

    $existing = [ordered]@{}
    foreach ($item in $roles.Values) {
        $saved = Read-PostgresConnection -SecretsDirectory $Answers.secretsDir -Name $item.Name
        if ($saved -and ($saved.Host -ne $postgres.host -or $saved.Port -ne [int] $postgres.port -or $saved.Database -ne $database -or
                $saved.Username -ne $item.Username -or $saved.Password -ne $item.Password)) {
            Stop-Installer -Code Error -Message ("A conexão '$($item.Name)' em secretsDir não é a das respostas ($($item.Username) em " +
                "$($postgres.host):$($postgres.port)/$database); recrie com 'recreate secret:$($item.Name)'.")
        }

        $existing[$item.Name] = [bool] $saved
    }

    $asOwner = New-ExistingPostgresServer -Answers $Answers -Username $owner.Username -Password $owner.Password
    $asRuntime = New-ExistingPostgresServer -Answers $Answers -Username $runtime.Username -Password $runtime.Password
    $common = @{ Image = $Image; Network = $Network; CatalogRoot = $CatalogRoot; Cli = $Cli }
    $connect = {
        param($Server, [string] $Label)
        try { return Get-PostgresServerVersion @common -Server $Server -MaintenanceDatabase $database }
        catch {
            if ($_.Exception.PSObject.Properties['ExitCode'] -and $_.Exception.ExitCode -eq (Get-InstallerExitCode -Name Error)) {
                Stop-Installer -Code Prerequisite -Message "A role $Label ($($Server.Username)) não conecta ao banco $database em $($postgres.host):$($postgres.port): $($_.Exception.Message)"
            }

            throw
        }
    }

    try {
        $version = & $connect $asOwner 'dona'
        $ownerChecks = Get-PostgresRolePrivilege @common -Server $asOwner -Database $database -Role $owner.Username
        $runtimeChecks = Get-PostgresRolePrivilege @common -Server $asOwner -Database $database -Role $runtime.Username
        Assert-PostgresRole -Owner $ownerChecks -Runtime $runtimeChecks -OwnerRole $owner.Username -RuntimeRole $runtime.Username -Database $database `
            -Code Prerequisite -AllowMissingSchema
        [void] (& $connect $asRuntime 'de runtime')
    }
    finally {
        $asOwner.Password = $null
        $asRuntime.Password = $null
    }

    $sslMode = if ($postgres.Contains('sslMode')) { [string] $postgres.sslMode } else { '' }
    $written = @()
    foreach ($item in $roles.Values) {
        if (-not $existing[$item.Name]) {
            $value = ConvertTo-PostgresConnectionString -Server $postgres.host -Port ([int] $postgres.port) -Database $database -Username $item.Username -Password $item.Password -SslMode $sslMode
            Write-InstallerSecret -SecretsDirectory $Answers.secretsDir -Name $item.Name -Value $value
            $written += $item.Name
        }
    }

    return [pscustomobject]@{ ServerVersion = $version; Database = $database; OwnerRole = $owner.Username; RuntimeRole = $runtime.Username; ConnectionsWritten = $written }
}

Export-ModuleMember -Function Get-PostgresDefaults, Initialize-PostgresSecrets, ConvertTo-PostgresConnectionString, Read-PostgresConnection,
Get-CatalogSqlFile, Invoke-PsqlCatalog, Get-PostgresServerVersion, Get-PostgresRolePrivilege, Assert-PostgresRole, Invoke-PostgresBootstrap,
Invoke-ProvisionedPostgresBootstrap, Assert-ExistingPostgresAnswers, Get-ExistingPostgresCaPem, Get-ExistingPostgresAdminServer, Invoke-ExistingPostgresBootstrap,
Invoke-PreparedPostgresBootstrap
