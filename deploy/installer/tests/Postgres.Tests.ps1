#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T028 (FR-007, FR-008, FR-028, FR-033, D-009, L5): credenciais das instâncias, bootstrap pelo catálogo com psql
# em container avulso e conexões em secretsDir. A integração usa um PostgreSQL real no engine (DOCKER_HOST).

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Secrets.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Postgres.psm1') -Force
    $script:catalog = Join-Path (Get-InstallerHome) 'catalog'
    $script:image = (Get-Content -LiteralPath (Join-Path (Get-InstallerHome) 'templates/infrastructure-images.json') -Raw | ConvertFrom-Json).images.postgresql

    AfterEach { Clear-InstallerSecrets }

    function New-TestDirectory {
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-pg-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $path | Out-Null
        return $path
    }

    # Engine falso: grava os argumentos e o stdin (o tar) e responde como o psql, conforme FAKE_PSQL_OUTPUT.
    function New-FakeEngine {
        param([string] $Root, [string] $Output = "--dokpod-psql-step--`n180004", [int] $ExitCode = 0)
        $fake = Join-Path $Root 'docker'
        [System.IO.File]::WriteAllText($fake, "#!/bin/sh`nprintf '%s\n' `"`$@`" > '$Root/args.log'`ncat > '$Root/stdin.tar'`nprintf '%s\n' '$($Output.Replace("`n", "' '"))'`nexit $ExitCode`n")
        [System.IO.File]::SetUnixFileMode($fake, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        return $fake
    }

    function Get-TarEntries {
        param([string] $Path)
        $entries = [ordered]@{}
        $reader = [System.Formats.Tar.TarReader]::new([System.IO.File]::OpenRead($Path))
        try {
            while ($null -ne ($entry = $reader.GetNextEntry())) {
                $text = if ($entry.DataStream) { [System.IO.StreamReader]::new($entry.DataStream).ReadToEnd() } else { $null }
                $entries[$entry.Name] = [pscustomobject]@{ Mode = $entry.Mode; Text = $text }
            }
        }
        finally { $reader.Dispose() }
        return $entries
    }
}


Describe 'Credenciais das instâncias provisionadas' {
    It 'gera uma senha por instância, só as que faltam, e nunca sobrescreve (FR-007, FR-028)' {
        $dir = New-TestDirectory
        try {
            $answers = @{ secretsDir = $dir; postgres = @{ mode = 'provision' }; keycloak = @{ mode = 'provision' } }
            $created = Initialize-PostgresSecrets -Answers $answers
            $created | Should -Be @('dokpod-db-password', 'keycloak-db-password')
            $first = Read-InstallerSecret -SecretsDirectory $dir -Name 'dokpod-db-password'
            $first | Should -Not -Be (Read-InstallerSecret -SecretsDirectory $dir -Name 'keycloak-db-password')
            [System.IO.File]::GetUnixFileMode((Join-Path $dir 'dokpod-db-password')) | Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite')

            (Initialize-PostgresSecrets -Answers $answers).Count | Should -Be 0
            Read-InstallerSecret -SecretsDirectory $dir -Name 'dokpod-db-password' | Should -Be $first
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }

    It 'sem PostgreSQL nem Keycloak provisionados não gera nada' {
        $dir = New-TestDirectory
        try {
            (Initialize-PostgresSecrets -Answers @{ secretsDir = $dir; postgres = @{ mode = 'existing-create' }; keycloak = @{ mode = 'existing' } }).Count | Should -Be 0
            @(Get-ChildItem -LiteralPath $dir).Count | Should -Be 0
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }
}

Describe 'Conexões' {
    It 'escapa a senha e lê de volta as partes' {
        $dir = New-TestDirectory
        try {
            $password = "a;b'c=d `"e"
            $value = ConvertTo-PostgresConnectionString -Server 'dokpod-postgres' -Port 5432 -Database 'dokpod' -Username 'dokpod_app' -Password $password
            Write-InstallerSecret -SecretsDirectory $dir -Name 'ConnectionStrings__ControlPlane' -Value $value
            $read = Read-PostgresConnection -SecretsDirectory $dir -Name 'ConnectionStrings__ControlPlane'
            $read.Password | Should -Be $password
            $read.Username | Should -Be 'dokpod_app'
            $read.Port | Should -Be 5432
            Read-PostgresConnection -SecretsDirectory $dir -Name 'dokpod-owner-connection' | Should -BeNullOrEmpty
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }
}

Describe 'Catálogo e canal dos parâmetros' {
    It 'recusa arquivo do catálogo alterado sem conferir com o manifesto' {
        $copy = New-TestDirectory
        try {
            Copy-Item -Path (Join-Path $script:catalog '*') -Destination $copy -Recurse
            Add-Content -LiteralPath (Join-Path $copy 'Installation/checks/server-version.sql') -Value 'SELECT 1;'
            { Get-CatalogSqlFile -Path 'Installation/checks/server-version.sql' -CatalogRoot $copy } | Should -Throw '*SHA-256*'
            { Get-CatalogSqlFile -Path 'Installation/nao-existe.sql' -CatalogRoot $script:catalog } | Should -Throw '*manifesto*'
            { Get-CatalogSqlFile -Path 'Installation/status/conference-counts.sql' -CatalogRoot $script:catalog } | Should -Throw '*admin*'
        }
        finally { Remove-Item -LiteralPath $copy -Recurse -Force }
    }

    It 'segredo declarado só entra como arquivo; parâmetro não declarado é recusado' {
        $root = New-TestDirectory
        try {
            $fake = New-FakeEngine -Root $root
            $server = @{ Host = 'db'; Port = 5432; Username = 'admin'; Password = 'canario-admin-033' }
            $roles = @{ owner_role = 'o'; runtime_role = 'r' }
            {
                Invoke-PsqlCatalog -Image 'img' -Network 'n' -Server $server -CatalogRoot $script:catalog -Cli $fake -Steps @(
                    @{ Path = 'Installation/bootstrap/01-create-roles.sql'; Database = 'postgres'; Variables = $roles + @{ owner_password_file = 'x'; runtime_password_file = 'y' } })
            } | Should -Throw '*arquivo*'
            {
                Invoke-PsqlCatalog -Image 'img' -Network 'n' -Server $server -CatalogRoot $script:catalog -Cli $fake -Steps @(
                    @{ Path = 'Installation/checks/server-version.sql'; Database = 'postgres'; Variables = @{ extra = '1' } })
            } | Should -Throw '*não é declarado*'
            Test-Path (Join-Path $root 'args.log') | Should -BeFalse
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force }
    }

    It 'container do psql isolado e segredos só no tar do stdin, em arquivos 0600 (FR-033, L5)' {
        $root = New-TestDirectory
        try {
            $fake = New-FakeEngine -Root $root
            $server = @{ Host = 'db'; Port = 5432; Username = 'admin'; Password = 'canario:admin\033' }
            [void] (Invoke-PsqlCatalog -Image 'img' -Network 'dokpod_dokpod-data' -Server $server -CatalogRoot $script:catalog -Cli $fake -Steps @(
                    @{
                        Path = 'Installation/bootstrap/01-create-roles.sql'; Database = 'postgres'
                        Variables = @{ owner_role = 'dokpod_owner'; runtime_role = "role'com aspas" }
                        Secrets = @{ owner_password_file = 'canario-dono-033'; runtime_password_file = 'canario-runtime-033' }
                    }))

            $arguments = Get-Content -LiteralPath (Join-Path $root 'args.log')
            $text = $arguments -join "`n"
            foreach ($canary in 'canario', 'PGPASSWORD', 'engine.sock', '--volume', '--publish', '-p') {
                $arguments | Should -Not -Contain $canary
            }
            $text | Should -Not -Match 'canario'
            $arguments | Should -Contain '--read-only'
            $arguments | Should -Contain '--log-driver'
            $arguments[[array]::IndexOf($arguments, '--network') + 1] | Should -Be 'dokpod_dokpod-data'
            $arguments[[array]::IndexOf($arguments, '--cap-drop') + 1] | Should -Be 'ALL'
            ($arguments | Where-Object { $_ -like '/run/dokpod-psql:*' }) | Should -Match 'noexec'

            $entries = Get-TarEntries -Path (Join-Path $root 'stdin.tar')
            $entries['pgpass'].Text | Should -Be '*:*:*:admin:canario\:admin\\033'
            $entries['secrets/1-owner_password_file'].Text | Should -Be 'canario-dono-033'
            foreach ($name in 'pgpass', 'secrets/1-owner_password_file', 'secrets/1-runtime_password_file') {
                $entries[$name].Mode | Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite')
            }
            $entries['run.sh'].Text | Should -Match 'export PGPASSFILE=/run/dokpod-psql/pgpass'
            $entries['run.sh'].Text | Should -Match "runtime_role=role'\\''com aspas"
            $entries['run.sh'].Text | Should -Not -Match 'canario'
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force }
    }

    It 'PostgreSQL abaixo do homologado dá código 3' {
        $root = New-TestDirectory
        try {
            $fake = New-FakeEngine -Root $root -Output "--dokpod-psql-step--`n160004"
            $server = @{ Host = 'db'; Port = 5432; Username = 'admin'; Password = 'senha-qualquer' }
            try {
                Get-PostgresServerVersion -Image 'img' -Network 'n' -Server $server -CatalogRoot $script:catalog -Cli $fake
                throw 'deveria falhar'
            }
            catch { $_.Exception.ExitCode | Should -Be 3 }
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force }
    }

    It 'falha do psql aponta o arquivo do passo e não mostra a senha' {
        $root = New-TestDirectory
        try {
            $fake = New-FakeEngine -Root $root -Output "--dokpod-psql-step--`n--dokpod-psql-step--`nerro senha-do-admin-033" -ExitCode 3
            $server = @{ Host = 'db'; Port = 5432; Username = 'admin'; Password = 'senha-do-admin-033' }
            $steps = @(
                @{ Path = 'Installation/checks/server-version.sql'; Database = 'postgres' }
                @{ Path = 'Installation/bootstrap/03-grants.sql'; Database = 'dokpod'; Variables = @{ database = 'dokpod'; runtime_role = 'r' } })
            { Invoke-PsqlCatalog -Image 'img' -Network 'n' -Server $server -CatalogRoot $script:catalog -Cli $fake -Steps $steps } |
                Should -Throw '*03-grants.sql*'
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force }
    }

    It 'roles com privilégio administrativo ou runtime que cria objetos são recusadas (D-009)' {
        $ok = [ordered]@{ exists = $true; login = $true; superuser = $false; createrole = $false; createdb = $false; replication = $false; bypassrls = $false
            database_connect = $true; schema_usage = $true; schema_create = $false; schema_owner = $false; runtime_group_member = $true }
        $owner = [ordered]@{} + $ok; $owner.schema_owner = $true; $owner.schema_create = $true
        { Assert-PostgresRole -Owner $owner -Runtime $ok } | Should -Not -Throw
        $bad = [ordered]@{} + $ok; $bad.createrole = $true; $bad.schema_create = $true
        { Assert-PostgresRole -Owner $owner -Runtime $bad } | Should -Throw '*createrole*criar objetos*'
    }
}

Describe 'Bootstrap contra PostgreSQL real' {
    BeforeAll {
        $script:skip = -not $env:DOCKER_HOST
        if ($script:skip) { return }

        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $script:network = "dokpod-033-pg-$suffix"
        $script:server = "dokpod-033-pg-$suffix"
        $script:adminPassword = "canario-pg-admin-033-$suffix"
        & docker network create --internal $script:network | Out-Null
        & docker run --detach --name $script:server --network $script:network --network-alias dokpod-postgres --log-driver none `
            --env POSTGRES_USER=dokpod_admin --env POSTGRES_DB=postgres --env "POSTGRES_PASSWORD=$($script:adminPassword)" $script:image | Out-Null
        $deadline = [datetime]::UtcNow.AddSeconds(90)
        do {
            Start-Sleep -Milliseconds 500
            & docker exec $script:server pg_isready -h 127.0.0.1 -U dokpod_admin -d postgres *> $null
        } until ($LASTEXITCODE -eq 0 -or [datetime]::UtcNow -gt $deadline)
    }

    AfterAll {
        if ($script:skip) { return }
        & docker rm --force --volumes $script:server *> $null
        & docker network rm $script:network *> $null
    }

    It 'cria banco e roles com menor privilégio, grava as conexões e reexecuta sem mudar nada, sem segredo no inspect' {
        if ($script:skip) {
            Set-ItResult -Skipped -Because 'o socket do engine não foi montado'
            return
        }

        $dir = New-TestDirectory
        $inspects = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
        $stop = [System.Threading.ManualResetEventSlim]::new($false)
        $poller = Start-ThreadJob -ArgumentList $inspects, $stop -ScriptBlock {
            param($bag, $stopSignal)
            while (-not $stopSignal.IsSet) {
                foreach ($id in @(& docker ps --quiet --filter label=io.dokpod.installer.task=psql)) {
                    $text = (& docker inspect $id 2>$null) -join "`n"
                    if ($text) { $bag.Add($text) }
                }
                Start-Sleep -Milliseconds 50
            }
        }

        try {
            $server = [ordered]@{ Host = 'dokpod-postgres'; Port = 5432; Username = 'dokpod_admin'; Password = $script:adminPassword }
            $first = Invoke-PostgresBootstrap -SecretsDirectory $dir -Image $script:image -Network $script:network -Server $server -CatalogRoot $script:catalog
            $first.ServerVersion | Should -BeGreaterOrEqual 180000
            $first.ConnectionsWritten | Should -Be @('dokpod-owner-connection', 'ConnectionStrings__ControlPlane')
            $owner = Read-PostgresConnection -SecretsDirectory $dir -Name 'dokpod-owner-connection'
            $runtime = Read-PostgresConnection -SecretsDirectory $dir -Name 'ConnectionStrings__ControlPlane'
            $owner.Username | Should -Be 'dokpod_owner'
            $runtime.Username | Should -Be 'dokpod_app'
            $runtime.Host | Should -Be 'dokpod-postgres'
            $runtime.Database | Should -Be 'dokpod'

            # A role de runtime conecta com a própria senha, mas não cria objetos no schema.
            $asRuntime = [ordered]@{ Host = 'dokpod-postgres'; Port = 5432; Username = 'dokpod_app'; Password = $runtime.Password }
            Get-PostgresServerVersion -Image $script:image -Network $script:network -Server $asRuntime -MaintenanceDatabase 'dokpod' -CatalogRoot $script:catalog |
                Should -BeGreaterOrEqual 180000
            $checks = Get-PostgresRolePrivilege -Image $script:image -Network $script:network -Server $server -Database 'dokpod' -Role 'dokpod_app' -CatalogRoot $script:catalog
            $checks.schema_create | Should -BeFalse
            $checks.runtime_group_member | Should -BeTrue

            $second = Invoke-PostgresBootstrap -SecretsDirectory $dir -Image $script:image -Network $script:network -Server $server -CatalogRoot $script:catalog
            $second.ConnectionsWritten.Count | Should -Be 0
            (Read-PostgresConnection -SecretsDirectory $dir -Name 'ConnectionStrings__ControlPlane').Password | Should -Be $runtime.Password

            $stop.Set()
            [void] (Wait-Job $poller -Timeout 10)
            $inspects.Count | Should -BeGreaterThan 0 -Because 'o teste precisa ter visto o container do psql em execução'
            foreach ($text in $inspects) {
                $text | Should -Not -Match ([regex]::Escape($script:adminPassword))
                $text | Should -Not -Match ([regex]::Escape($owner.Password))
                $text | Should -Not -Match ([regex]::Escape($runtime.Password))
                $text | Should -Not -Match 'PGPASSWORD'
                $text | Should -Not -Match 'engine\.sock|docker\.sock|podman\.sock'
            }
            @(& docker ps --all --quiet --filter label=io.dokpod.installer.task=psql).Count | Should -Be 0 -Because 'o container do psql é removido ao fim'
        }
        finally {
            $stop.Set()
            Remove-Job $poller -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'conexão existente de outra role é recusada, sem sobrescrever' {
        if ($script:skip) {
            Set-ItResult -Skipped -Because 'o socket do engine não foi montado'
            return
        }

        $dir = New-TestDirectory
        try {
            $other = ConvertTo-PostgresConnectionString -Server 'dokpod-postgres' -Port 5432 -Database 'dokpod' -Username 'outra' -Password 'senha-outra-033'
            Write-InstallerSecret -SecretsDirectory $dir -Name 'ConnectionStrings__ControlPlane' -Value $other
            $server = [ordered]@{ Host = 'dokpod-postgres'; Port = 5432; Username = 'dokpod_admin'; Password = $script:adminPassword }
            { Invoke-PostgresBootstrap -SecretsDirectory $dir -Image $script:image -Network $script:network -Server $server -CatalogRoot $script:catalog } |
                Should -Throw '*recreate secret:ConnectionStrings__ControlPlane*'
            (Read-PostgresConnection -SecretsDirectory $dir -Name 'ConnectionStrings__ControlPlane').Username | Should -Be 'outra'
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }
}
