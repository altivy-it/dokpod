#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T030 (FR-010, FR-011, FR-036 a FR-038, FR-041, FR-042, L4, L5): etapa de migrations pelo modo administrativo
# da imagem da API. A integração usa PostgreSQL real e a imagem da API desta branch (DOKPOD_TEST_API_IMAGE), com os
# diretórios em DOKPOD_TEST_SHARED, montado no mesmo caminho no host e no container dos testes, como install.sh faz com
# installDir e secretsDir.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Secrets.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/State.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Postgres.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Migrations.psm1') -Force
    $script:catalog = Join-Path (Get-InstallerHome) 'catalog'
    $script:manifest = Get-Content -LiteralPath (Join-Path $script:catalog 'manifest.json') -Raw | ConvertFrom-Json -AsHashtable
    $script:lastMigration = @($script:manifest.files | Where-Object purpose -eq 'migration' | ForEach-Object { $_.path.Split('/')[2] } | Sort-Object -Unique)[-1]
    $script:lastUp = @($script:manifest.files | Where-Object { $_.path -like "Migrations/Script/$($script:lastMigration)/Up/*" })[0].path
    $script:firstUp = @($script:manifest.files | Where-Object { $_.purpose -eq 'migration' -and $_.path -like 'Migrations/Script/*/Up/*' } | Sort-Object path)[0].path

    AfterEach { Clear-InstallerSecrets }

    function New-TestDirectory {
        param([string] $Base = [System.IO.Path]::GetTempPath())
        $path = Join-Path $Base "dokpod-033-mig-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $path | Out-Null
        return $path
    }

    function New-TestInstall {
        param([string] $Base = [System.IO.Path]::GetTempPath(), [string] $Mode = 'existing-prepared')
        $root = New-TestDirectory -Base $Base
        $answers = [ordered]@{
            installDir = (Join-Path $root 'install'); secretsDir = (Join-Path $root 'secrets'); operator = 'Ana Operadora'
            postgres = [ordered]@{ mode = $Mode }
        }
        New-Item -ItemType Directory -Path $answers.installDir, $answers.secretsDir | Out-Null
        $state = Open-InstallerState -InstallDirectory $answers.installDir
        $run = Start-InstallerRun -State $state -Command 'install'
        return [pscustomobject]@{ Root = $root; Answers = $answers; State = $state; RunId = $run }
    }

    function Remove-TestInstall {
        param($Install)
        Close-InstallerState -State $Install.State
        Remove-Item -LiteralPath $Install.Root -Recurse -Force
    }

    function New-SqlDirectory {
        # Cópia do catálogo com os arquivos indicados alterados (texto acrescentado ao fim).
        param([string] $Base = [System.IO.Path]::GetTempPath(), [hashtable] $Append = @{})
        $dir = New-TestDirectory -Base $Base
        Copy-Item -Path (Join-Path $script:catalog '*') -Destination $dir -Recurse
        foreach ($path in $Append.Keys) { Add-Content -LiteralPath (Join-Path $dir $path) -Value $Append[$path] }
        return $dir
    }

    # Engine falso do modo administrativo: grava cada chamada e responde com <comando>.json e <comando>.code de $Root.
    function New-FakeAdminEngine {
        param([string] $Root, [hashtable] $Responses)
        foreach ($name in $Responses.Keys) {
            $response = $Responses[$name]
            [System.IO.File]::WriteAllText((Join-Path $Root "$name.json"), ($response.Data | ConvertTo-Json -Depth 8))
            [System.IO.File]::WriteAllText((Join-Path $Root "$name.code"), [string] ($response.Code ?? 0))
        }
        $fake = Join-Path $Root 'docker'
        $script = @"
#!/bin/sh
printf '%s\n' "`$@" >> '$Root/calls.log'
echo '----' >> '$Root/calls.log'
case " `$* " in
  *" migrations status "*) c=status ;;
  *" migrations plan "*) c=plan ;;
  *" migrations apply "*) c=apply ;;
  *" migrations script "*) c=script ;;
  *" migrations fingerprint "*) c=fingerprint ;;
  *" migrations verify "*) c=verify ;;
  *) c=other ;;
esac
cat '$Root/'`$c.json 2>/dev/null
exit `$(cat '$Root/'`$c.code 2>/dev/null || echo 0)
"@
        [System.IO.File]::WriteAllText($fake, $script.Replace("`r", ''))
        [System.IO.File]::SetUnixFileMode($fake, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        return $fake
    }

    function Get-FakeCalls {
        param([string] $Root)
        if (-not (Test-Path (Join-Path $Root 'calls.log'))) { return @() }
        return , @((Get-Content -LiteralPath (Join-Path $Root 'calls.log') -Raw) -split "----`n" | Where-Object { $_.Trim() })
    }

    function New-PlanResponse {
        param([string[]] $Pending = @('m2'), [string] $From = 'm1', [object[]] $Overrides = @())
        return @{
            from = $From; to = $Pending[-1]; planHash = ('a' * 64); newerThanTarget = @()
            migrations = @($Pending | ForEach-Object { @{ id = $_; sha256 = ('b' * 64); overridden = $Overrides.Count -gt 0; overrides = $Overrides
                        objects = @{ created = @('dokpod.tabela_nova'); altered = @('dokpod.environments'); dropped = @() } } })
        }
    }

    function Join-Parameters {
        # Une hashtables de parâmetros; a última vence.
        param([Parameter(ValueFromRemainingArguments)][hashtable[]] $Parts)
        $merged = @{}
        foreach ($part in $Parts) { foreach ($key in $part.Keys) { $merged[$key] = $part[$key] } }
        return $merged
    }

    function Invoke-Step {
        # Chama a etapa e devolve o resultado ou o código do InstallerException.
        param([Parameter(ValueFromRemainingArguments)][hashtable[]] $Parts)
        $Parameters = Join-Parameters @Parts
        try { return [pscustomobject]@{ Result = (Invoke-MigrationStep @Parameters); Code = 0 } }
        catch {
            if ($_.Exception.PSObject.Properties['ExitCode']) { return [pscustomobject]@{ Result = $null; Code = $_.Exception.ExitCode; Message = $_.Exception.Message } }
            throw
        }
    }
}

Describe 'Catálogo sobreposto (--sql-dir)' {
    It 'devolve só os arquivos alterados, com os dois hashes e a migration de cada um (FR-041)' {
        $dir = New-SqlDirectory -Append @{ $script:lastUp = '-- ajuste do DBA' }
        try {
            $changes = Get-SqlOverrideSet -Directory $dir -CatalogRoot $script:catalog
            $changes.Count | Should -Be 1
            $changes[0].Path | Should -Be $script:lastUp
            $changes[0].MigrationId | Should -Be $script:lastMigration
            $changes[0].Purpose | Should -Be 'migration'
            $changes[0].OriginalSha256 | Should -Be (@($script:manifest.files | Where-Object path -eq $script:lastUp)[0].sha256)
            $changes[0].AppliedSha256 | Should -Be (Get-Sha256Hex -Text ([System.IO.File]::ReadAllText((Join-Path $dir $script:lastUp))))
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }

    It 'recusa manifesto ausente ou de outra versão, caminho sem correspondente, arquivo vazio, link e a impressão do schema' {
        $dir = New-SqlDirectory
        try {
            Remove-Item -LiteralPath (Join-Path $dir 'manifest.json')
            { Get-SqlOverrideSet -Directory $dir -CatalogRoot $script:catalog } | Should -Throw '*manifest.json*'

            $other = Get-Content -LiteralPath (Join-Path $script:catalog 'manifest.json') -Raw | ConvertFrom-Json -AsHashtable
            $other.version = '0.0.1'
            [System.IO.File]::WriteAllText((Join-Path $dir 'manifest.json'), ($other | ConvertTo-Json -Depth 8))
            { Get-SqlOverrideSet -Directory $dir -CatalogRoot $script:catalog } | Should -Throw '*0.0.1*'

            Copy-Item -LiteralPath (Join-Path $script:catalog 'manifest.json') -Destination (Join-Path $dir 'manifest.json') -Force
            [System.IO.File]::WriteAllText((Join-Path $dir 'Installation/extra.sql'), 'SELECT 1;')
            { Get-SqlOverrideSet -Directory $dir -CatalogRoot $script:catalog } | Should -Throw '*não tem correspondente*'
            Remove-Item -LiteralPath (Join-Path $dir 'Installation/extra.sql')

            [System.IO.File]::WriteAllText((Join-Path $dir $script:lastUp), '   ')
            { Get-SqlOverrideSet -Directory $dir -CatalogRoot $script:catalog } | Should -Throw '*vazio*'
            Copy-Item -LiteralPath (Join-Path $script:catalog $script:lastUp) -Destination (Join-Path $dir $script:lastUp) -Force

            Add-Content -LiteralPath (Join-Path $dir $script:manifest.expectedSchema.path) -Value ' '
            { Get-SqlOverrideSet -Directory $dir -CatalogRoot $script:catalog } | Should -Throw '*impressão do schema*'
            Copy-Item -LiteralPath (Join-Path $script:catalog $script:manifest.expectedSchema.path) -Destination (Join-Path $dir $script:manifest.expectedSchema.path) -Force

            Remove-Item -LiteralPath (Join-Path $dir $script:firstUp)
            New-Item -ItemType SymbolicLink -Path (Join-Path $dir $script:firstUp) -Target '/etc/hostname' | Out-Null
            { Get-SqlOverrideSet -Directory $dir -CatalogRoot $script:catalog } | Should -Throw '*link simbólico*'
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }
}

Describe 'Modo administrativo em container avulso (L5)' {
    It 'monta só a conexão do owner somente leitura, sem socket nem porta, e lê o JSON' {
        $install = New-TestInstall
        try {
            Write-InstallerSecret -SecretsDirectory $install.Answers.secretsDir -Name 'dokpod-owner-connection' -Value 'Host=db;Password=canario-owner-033'
            $fake = New-FakeAdminEngine -Root $install.Root -Responses @{ status = @{ Data = @{ applied = @('m1'); pending = @(); newerThanTarget = @() } } }
            $result = Invoke-AdminCommand -Image 'api@sha256:x' -Network 'rede-db' -SecretsDirectory $install.Answers.secretsDir -Arguments @('migrations', 'status') -Cli $fake
            $result.Data.applied | Should -Be @('m1')

            $call = (Get-FakeCalls -Root $install.Root)[0]
            $call | Should -Match '--read-only'
            $call | Should -Match '--cap-drop\nALL'
            $call | Should -Match '--network\nrede-db'
            $call | Should -Match ([regex]::Escape("type=bind,src=$($install.Answers.secretsDir)/dokpod-owner-connection,dst=/run/secrets/ConnectionStrings__ControlPlane,ro"))
            $call | Should -Match 'admin\nmigrations\nstatus\n--output\njson'
            $call | Should -Not -Match 'sock|--publish|-p\n|canario'
        }
        finally { Remove-TestInstall $install }
    }

    It 'falha do modo administrativo interrompe com a mensagem dele; sem conexão do owner nem chama o engine' {
        $install = New-TestInstall
        try {
            $fake = New-FakeAdminEngine -Root $install.Root -Responses @{ status = @{ Data = @{}; Code = 2 } }
            { Invoke-AdminCommand -Image 'api' -Network 'n' -SecretsDirectory $install.Answers.secretsDir -Arguments @('migrations', 'status') -Cli $fake } |
                Should -Throw '*dokpod-owner-connection*'
            (Get-FakeCalls -Root $install.Root).Count | Should -Be 0
            Write-InstallerSecret -SecretsDirectory $install.Answers.secretsDir -Name 'dokpod-owner-connection' -Value 'Host=db'
            { Invoke-AdminCommand -Image 'api' -Network 'n' -SecretsDirectory $install.Answers.secretsDir -Arguments @('migrations', 'status') -Cli $fake } |
                Should -Throw '*código 2*'
        }
        finally { Remove-TestInstall $install }
    }
}

Describe 'Confirmações da etapa de migrations (engine falso)' {
    BeforeEach {
        $script:install = New-TestInstall
        Write-InstallerSecret -SecretsDirectory $script:install.Answers.secretsDir -Name 'dokpod-owner-connection' -Value 'Host=db'
        $script:base = @{ Answers = $script:install.Answers; State = $script:install.State; RunId = $script:install.RunId; Image = 'api'; Network = 'n'
            CatalogRoot = $script:catalog; Interactive = $false }
    }
    AfterEach { Remove-TestInstall $script:install }

    It 'banco com migration mais nova dá código 6 sem plano nem apply (FR-010)' {
        $fake = New-FakeAdminEngine -Root $script:install.Root -Responses @{ status = @{ Data = @{ applied = @('m1', 'm9'); pending = @(); newerThanTarget = @('m9') } } }
        $step = Invoke-Step $script:base @{ Cli = $fake }
        $step.Code | Should -Be 6
        $step.Message | Should -Match 'm9'
        (Get-FakeCalls -Root $script:install.Root).Count | Should -Be 1
    }

    It 'sem --confirm-migrations com o planHash, ou com outro, dá código 4 e não aplica; com ele, aplica e confere (FR-036)' {
        $fake = New-FakeAdminEngine -Root $script:install.Root -Responses @{
            status = @{ Data = @{ databaseVersion = $null; targetVersion = 'm2'; applied = @(); pending = @('m1', 'm2'); newerThanTarget = @() } }
            plan = @{ Data = (New-PlanResponse -Pending @('m1', 'm2') -From $null) }
            apply = @{ Data = @{ applied = @('m1', 'm2'); durationMs = 10 } }
            verify = @{ Data = @{ ok = $true; missing = @(); unexpected = @(); schemaAccepted = $true; schemaDiff = @(); fileHashMismatch = @() } }
        }
        $parameters = Join-Parameters $script:base @{ Cli = $fake }
        (Invoke-Step $parameters @{ Confirmations = @{ Plan = $true } }).Code | Should -Be 4
        (Invoke-Step $parameters @{ Confirmations = @{ MigrationsPlanHash = ('c' * 64) } }).Code | Should -Be 4
        (Get-FakeCalls -Root $script:install.Root) -match 'apply' | Should -BeNullOrEmpty

        $step = Invoke-Step $parameters @{ Confirmations = @{ MigrationsPlanHash = ('a' * 64) } }
        $step.Code | Should -Be 0
        $step.Result.Applied | Should -Be @('m1', 'm2')
        $step.Result.BackupPath | Should -BeNullOrEmpty
        $calls = Get-FakeCalls -Root $script:install.Root
        @($calls -match "apply\n--confirm\n$('a' * 64)").Count | Should -Be 1
        @($calls -match 'verify').Count | Should -Be 1
    }

    It 'no modo interativo mostra o plano e só aplica digitando aplicar' {
        $fake = New-FakeAdminEngine -Root $script:install.Root -Responses @{
            status = @{ Data = @{ databaseVersion = $null; targetVersion = 'm1'; applied = @(); pending = @('m1'); newerThanTarget = @() } }
            plan = @{ Data = (New-PlanResponse -Pending @('m1') -From $null) }
            apply = @{ Data = @{ applied = @('m1') } }
            verify = @{ Data = @{ ok = $true } }
        }
        $parameters = Join-Parameters $script:base @{ Cli = $fake; Interactive = $true }
        (Invoke-Step $parameters @{ Prompt = { param($m) 'sim' } }).Code | Should -Be 4
        $script:asked = @()
        $step = Invoke-Step $parameters @{ Prompt = { param($m) $script:asked += $m; 'aplicar' } }
        $step.Code | Should -Be 0
        $script:asked[0] | Should -Match ('a' * 64)
    }

    It 'banco com dados exige --confirm-backup com o nome do banco quando o PostgreSQL não é provisionado (FR-011)' {
        $fake = New-FakeAdminEngine -Root $script:install.Root -Responses @{
            status = @{ Data = @{ databaseVersion = 'm1'; targetVersion = 'm2'; applied = @('m1'); pending = @('m2'); newerThanTarget = @() } }
            plan = @{ Data = (New-PlanResponse) }
            apply = @{ Data = @{ applied = @('m2') } }
            verify = @{ Data = @{ ok = $true } }
        }
        $parameters = Join-Parameters $script:base @{ Cli = $fake }
        $step = Invoke-Step $parameters @{ Confirmations = @{ MigrationsPlanHash = ('a' * 64) } }
        $step.Code | Should -Be 4
        $step.Message | Should -Match '--confirm-backup dokpod'
        (Get-FakeCalls -Root $script:install.Root) -match 'apply' | Should -BeNullOrEmpty
        (Invoke-Step $parameters @{ Confirmations = @{ MigrationsPlanHash = ('a' * 64); Backup = 'dokpod' } }).Code | Should -Be 0
    }

    It '--yes não confirma sobreposição: o plano lista os hashes e só aplica com --confirm-sql-overrides exato (FR-041)' {
        $sqlDir = New-SqlDirectory -Base $script:install.Root -Append @{ $script:lastUp = '-- ajuste' }
        $applied = Get-Sha256Hex -Text ([System.IO.File]::ReadAllText((Join-Path $sqlDir $script:lastUp)))
        $overrides = @(@{ path = $script:lastUp; originalSha256 = ('d' * 64); appliedSha256 = $applied })
        $fake = New-FakeAdminEngine -Root $script:install.Root -Responses @{
            status = @{ Data = @{ databaseVersion = $null; targetVersion = $script:lastMigration; applied = @(); pending = @($script:lastMigration); newerThanTarget = @() } }
            plan = @{ Data = (New-PlanResponse -Pending @($script:lastMigration) -From $null -Overrides $overrides) }
            apply = @{ Data = @{ applied = @($script:lastMigration) } }
            fingerprint = @{ Data = @{ sha256 = ('f' * 64) } }
            verify = @{ Data = @{ ok = $true } }
        }
        $parameters = Join-Parameters $script:base @{ Cli = $fake; SqlDirectory = $sqlDir }
        $confirm = @{ Plan = $true; MigrationsPlanHash = ('a' * 64) }
        $step = Invoke-Step $parameters @{ Confirmations = $confirm }
        $step.Code | Should -Be 4
        $step.Message | Should -Match ([regex]::Escape($script:lastUp))
        (Invoke-Step $parameters @{ Confirmations = $confirm + @{ SqlOverrides = @($script:lastUp, 'outro.sql') } }).Code | Should -Be 4
        Test-Path (Join-Path $script:install.Answers.installDir 'sql-override') | Should -BeFalse
        Test-Path (Join-Path $script:install.Answers.installDir 'sql-override.pendente') | Should -BeFalse

        $step = Invoke-Step $parameters @{ Confirmations = $confirm + @{ SqlOverrides = @($script:lastUp) } }
        $step.Code | Should -Be 0
        $rows = Invoke-StateSql -State $script:install.State -Name 'sql-overrides-list' -Rows
        $rows.Count | Should -Be 1
        $rows[0].path | Should -Be $script:lastUp
        $rows[0].applied_sha256 | Should -Be $applied
        $rows[0].accepted_schema_fingerprint | Should -Be ('f' * 64)
        $rows[0].confirmed_by | Should -Be 'Ana Operadora'
        $copy = Join-Path $script:install.Answers.installDir 'sql-override'
        @(Get-ChildItem -LiteralPath $copy -Recurse -File).Count | Should -Be 2
        $calls = Get-FakeCalls -Root $script:install.Root
        @($calls -match 'apply') | Should -Match ([regex]::Escape("src=$copy,dst=/run/sql-override,ro"))
        @($calls -match 'verify') | Should -Match '--accepted-fingerprints'
    }
}

Describe 'Etapa de migrations contra PostgreSQL e a imagem da API reais' {
    BeforeAll {
        $script:skip = -not ($env:DOCKER_HOST -and $env:DOKPOD_TEST_SHARED -and $env:DOKPOD_TEST_API_IMAGE)
        if ($script:skip) { return }

        $script:api = $env:DOKPOD_TEST_API_IMAGE
        $script:pgImage = (Get-Content -LiteralPath (Join-Path (Get-InstallerHome) 'templates/infrastructure-images.json') -Raw | ConvertFrom-Json).images.postgresql
        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $script:network = "dokpod-033-mig-$suffix"
        $script:server = "dokpod-033-mig-$suffix"
        $script:adminPassword = "canario-mig-admin-033-$suffix"
        & docker network create --internal $script:network | Out-Null
        & docker run --detach --name $script:server --network $script:network --network-alias dokpod-postgres --log-driver none `
            --env POSTGRES_USER=dokpod_admin --env POSTGRES_DB=postgres --env "POSTGRES_PASSWORD=$($script:adminPassword)" `
            --label com.docker.compose.project=dokpod-teste-033 $script:pgImage | Out-Null
        $deadline = [datetime]::UtcNow.AddSeconds(90)
        do {
            Start-Sleep -Milliseconds 500
            & docker exec $script:server pg_isready -h 127.0.0.1 -U dokpod_admin -d postgres *> $null
        } until ($LASTEXITCODE -eq 0 -or [datetime]::UtcNow -gt $deadline)

        # Banco novo a cada teste: apaga dokpod e roda o bootstrap da T028 de novo, que é idempotente.
        function Reset-TestDatabase {
            param($Install)
            & docker exec $script:server psql -q -U dokpod_admin -d postgres -c 'DROP DATABASE IF EXISTS dokpod WITH (FORCE)' *> $null
            $server = [ordered]@{ Host = 'dokpod-postgres'; Port = 5432; Username = 'dokpod_admin'; Password = $script:adminPassword }
            Invoke-PostgresBootstrap -SecretsDirectory $Install.Answers.secretsDir -Image $script:pgImage -Network $script:network -Server $server -CatalogRoot $script:catalog | Out-Null
        }

        function Invoke-OwnerPsql {
            # psql como dokpod_owner, como o DBA aplicaria o arquivo exportado.
            param($Install, [string] $Sql)
            $owner = Read-PostgresConnection -SecretsDirectory $Install.Answers.secretsDir -Name 'dokpod-owner-connection'
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($Sql)
            return Invoke-InstallerProcess -FilePath 'docker' -InputBytes $bytes -ArgumentList @('exec', '-i', '--env', "PGPASSWORD=$($owner.Password)", $script:server,
                'psql', '-q', '-v', 'ON_ERROR_STOP=1', '-h', '127.0.0.1', '-U', 'dokpod_owner', '-d', 'dokpod', '-f', '-')
        }

        function Set-PenultimateDatabase {
            # Banco com todas as migrations menos a última, pelo arquivo exportado e cortado antes do último bloco.
            param($Install)
            $scratch = New-TestDirectory -Base $Install.Root
            $common = @{ Image = $script:api; Network = $script:network; SecretsDirectory = $Install.Answers.secretsDir }
            Invoke-AdminCommand @common -Arguments @('migrations', 'script', '--out', '/run/dokpod-migrations/all.sql') -WorkDirectory $scratch -WritableWork | Out-Null
            $all = [System.IO.File]::ReadAllText((Join-Path $scratch 'all.sql'))
            $cut = $all.Substring(0, $all.IndexOf("-- dokpod-migration-begin: $($script:lastMigration)"))
            (Invoke-OwnerPsql -Install $Install -Sql $cut).ExitCode | Should -Be 0
            Remove-Item -LiteralPath $scratch -Recurse -Force
        }
    }

    AfterAll {
        if ($script:skip) { return }
        & docker rm --force --volumes $script:server *> $null
        & docker network rm $script:network *> $null
    }

    BeforeEach {
        if ($script:skip) { return }
        $script:install = New-TestInstall -Base $env:DOKPOD_TEST_SHARED
        Reset-TestDatabase -Install $script:install
        $script:base = @{ Answers = $script:install.Answers; State = $script:install.State; RunId = $script:install.RunId; Image = $script:api
            Network = $script:network; CatalogRoot = $script:catalog; Interactive = $false }
    }

    AfterEach {
        if ($script:skip) { return }
        Remove-TestInstall $script:install
    }

    It 'banco vazio: aplica todas com o planHash, confere e a reexecução não muda nada' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'engine, DOKPOD_TEST_SHARED ou DOKPOD_TEST_API_IMAGE ausentes'; return }

        $first = Invoke-Step $script:base @{ Confirmations = @{} }
        $first.Code | Should -Be 4
        $hash = [regex]::Match($first.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        $hash | Should -Not -BeNullOrEmpty

        $applied = Invoke-Step $script:base @{ Confirmations = @{ MigrationsPlanHash = $hash } }
        $applied.Code | Should -Be 0
        $applied.Result.Applied[-1] | Should -Be $script:lastMigration
        $applied.Result.From | Should -BeNullOrEmpty

        $again = Invoke-Step $script:base @{ Confirmations = @{} }
        $again.Code | Should -Be 0
        $again.Result.Applied.Count | Should -Be 0
        $again.Result.From | Should -Be $script:lastMigration
    }

    It 'banco com dados no PostgreSQL provisionado: pg_dump antes do apply (FR-011)' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'engine, DOKPOD_TEST_SHARED ou DOKPOD_TEST_API_IMAGE ausentes'; return }

        Set-PenultimateDatabase -Install $script:install
        $existing = Invoke-Step $script:base @{ Confirmations = @{ MigrationsPlanHash = ('0' * 64) } }
        $existing.Code | Should -Be 4
        $hash = [regex]::Match($existing.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value

        $noBackup = Invoke-Step $script:base @{ Confirmations = @{ MigrationsPlanHash = $hash } }
        $noBackup.Code | Should -Be 4
        $noBackup.Message | Should -Match 'confirm-backup'

        $script:install.Answers.postgres = [ordered]@{ mode = 'provision' }
        $step = Invoke-Step $script:base @{ Confirmations = @{ MigrationsPlanHash = $hash }; PostgresContainer = $script:server }
        $step.Code | Should -Be 0
        $step.Result.Applied | Should -Be @($script:lastMigration)
        $bytes = [System.IO.File]::ReadAllBytes($step.Result.BackupPath)
        [System.Text.Encoding]::ASCII.GetString($bytes, 0, 5) | Should -Be 'PGDMP'
        [System.IO.File]::GetUnixFileMode($step.Result.BackupPath) | Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite')
    }

    It 'sobreposição confirmada: aplica o arquivo alterado, aceita a impressão e a reexecução reconfere a cópia (FR-041, FR-042, L4)' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'engine, DOKPOD_TEST_SHARED ou DOKPOD_TEST_API_IMAGE ausentes'; return }

        Set-PenultimateDatabase -Install $script:install
        $index = 'CREATE INDEX ix_dokpod_033_override ON dokpod.environments (name);'
        $sqlDir = New-SqlDirectory -Base $script:install.Root -Append @{ $script:lastUp = $index; $script:firstUp = '-- ajuste numa migration já aplicada' }
        $confirm = @{ Backup = 'dokpod'; SqlOverrides = @($script:firstUp, $script:lastUp) }
        $preview = Invoke-Step $script:base @{ SqlDirectory = $sqlDir; Confirmations = $confirm }
        $preview.Code | Should -Be 4
        $hash = [regex]::Match($preview.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value

        $step = Invoke-Step $script:base @{ SqlDirectory = $sqlDir; Confirmations = $confirm + @{ MigrationsPlanHash = $hash } }
        $step.Code | Should -Be 0
        $rows = Invoke-StateSql -State $script:install.State -Name 'sql-overrides-list' -Rows
        $rows.Count | Should -Be 2
        ($rows | Where-Object { $_.path -eq $script:lastUp }).accepted_schema_fingerprint | Should -Match '^[0-9a-f]{64}$'
        ($rows | Where-Object { $_.path -eq $script:firstUp }).accepted_schema_fingerprint | Should -BeNullOrEmpty
        (& docker exec $script:server psql -tA -U dokpod_admin -d dokpod -c "SELECT count(*) FROM pg_indexes WHERE indexname = 'ix_dokpod_033_override'") | Should -Be '1'

        # Reexecução sem --sql-dir: a cópia confere com o registrado e nada é pedido.
        $again = Invoke-Step $script:base @{ Confirmations = @{} }
        $again.Code | Should -Be 0
        $again.Result.Overrides.Count | Should -Be 0

        # Cópia alterada depois da confirmação: volta a exigir a confirmação nomeada (L4).
        Add-Content -LiteralPath (Join-Path $script:install.Answers.installDir "sql-override/$($script:firstUp)") -Value '-- outra alteração'
        $tampered = Invoke-Step $script:base @{ Confirmations = @{} }
        $tampered.Code | Should -Be 4
        $tampered.Message | Should -Match ([regex]::Escape($script:firstUp))
    }

    It 'alteração manual sem registro (<Kind>) é detectada pela impressão do schema no verify, com código 6 (FR-042)' -ForEach @(
        @{ Kind = 'coluna'; Change = 'ALTER TABLE dokpod.environments ADD COLUMN t059_extra integer'; Undo = 'ALTER TABLE dokpod.environments DROP COLUMN t059_extra' }
        @{ Kind = 'índice'; Change = 'CREATE INDEX ix_t059 ON dokpod.environments (name)'; Undo = 'DROP INDEX dokpod.ix_t059' }
        @{ Kind = 'restrição'; Change = "ALTER TABLE dokpod.environments ADD CONSTRAINT ck_t059 CHECK (name <> '')"; Undo = 'ALTER TABLE dokpod.environments DROP CONSTRAINT ck_t059' }
        @{ Kind = 'função'; Change = 'CREATE FUNCTION dokpod.t059() RETURNS integer LANGUAGE sql AS $$ SELECT 1 $$'; Undo = 'DROP FUNCTION dokpod.t059()' }
        @{ Kind = 'gatilho'; Change = 'CREATE FUNCTION dokpod.t059_trg() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END $$; CREATE TRIGGER t059 BEFORE INSERT ON dokpod.environments FOR EACH ROW EXECUTE FUNCTION dokpod.t059_trg()'; Undo = 'DROP TRIGGER t059 ON dokpod.environments; DROP FUNCTION dokpod.t059_trg()' }
    ) {
        if ($script:skip) { Set-ItResult -Skipped -Because 'engine, DOKPOD_TEST_SHARED ou DOKPOD_TEST_API_IMAGE ausentes'; return }

        $preview = Invoke-Step $script:base @{ Confirmations = @{} }
        $hash = [regex]::Match($preview.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        (Invoke-Step $script:base @{ Confirmations = @{ MigrationsPlanHash = $hash } }).Code | Should -Be 0
        $common = @{ Image = $script:api; Network = $script:network; SecretsDirectory = $script:install.Answers.secretsDir }
        (Invoke-AdminCommand @common -Arguments @('migrations', 'verify')).ExitCode | Should -Be 0

        (Invoke-OwnerPsql -Install $script:install -Sql "$Change;").ExitCode | Should -Be 0
        (Invoke-AdminCommand @common -Arguments @('migrations', 'verify')).ExitCode | Should -Be 6
        (Invoke-OwnerPsql -Install $script:install -Sql "$Undo;").ExitCode | Should -Be 0
        (Invoke-AdminCommand @common -Arguments @('migrations', 'verify')).ExitCode | Should -Be 0
    }

    It 'migration manual: exporta e pausa com 10, a retomada sem aplicar dá 6, e depois de aplicar confere (FR-037, FR-038)' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'engine, DOKPOD_TEST_SHARED ou DOKPOD_TEST_API_IMAGE ausentes'; return }

        $parameters = Join-Parameters $script:base @{ ManualMigrations = $true; Confirmations = @{} }
        $paused = Invoke-Step $parameters
        $paused.Code | Should -Be 10
        $file = Join-Path $script:install.Answers.installDir "migrations/vazio-$($script:lastMigration).sql"
        Test-Path -LiteralPath $file | Should -BeTrue
        $sql = [System.IO.File]::ReadAllText($file)
        $sql | Should -Match '^-- dokpod-migrations: 1'
        $sql | Should -Match "-- to: $($script:lastMigration)"

        $early = Invoke-Step $parameters
        $early.Code | Should -Be 6
        $early.Message | Should -Match 'faltam'

        (Invoke-OwnerPsql -Install $script:install -Sql $sql).ExitCode | Should -Be 0
        $resumed = Invoke-Step $parameters
        $resumed.Code | Should -Be 0
        $resumed.Result.ScriptPath | Should -Be $file
    }
}
