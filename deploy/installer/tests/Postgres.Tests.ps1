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
                @{ Path = 'Installation/bootstrap/03-grants.sql'; Database = 'postgres'; Variables = @{ database = 'dokpod'; owner_role = 'o'; runtime_role = 'r' } })
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

Describe 'PostgreSQL existente (existing-create): respostas, credencial e TLS' {
    BeforeAll {
        function New-ExistingAnswers {
            param([string] $Dir, [System.Collections.IDictionary] $Override = @{})
            $section = [ordered]@{ mode = 'existing-create'; host = 'db.lab.test'; port = 5432; admin = [ordered]@{ username = 'dba' } }
            foreach ($key in $Override.Keys) { $section[$key] = $Override[$key] }
            return [ordered]@{ secretsDir = $Dir; installDir = $Dir; postgres = $section; keycloak = @{ mode = 'provision' } }
        }
    }

    It 'recusa host de loopback com código 2: <Address>' -ForEach @(
        @{ Address = 'localhost' }, @{ Address = '127.0.0.1' }, @{ Address = '127.0.1.1' }, @{ Address = '::1' }, @{ Address = '[::1]' }, @{ Address = '0.0.0.0' }, @{ Address = 'db.localhost' }
    ) {
        $answers = New-ExistingAnswers -Dir '/tmp' -Override @{ host = $Address }
        try { Assert-ExistingPostgresAnswers -Answers $answers; throw 'deveria falhar' }
        catch {
            $_.Exception.ExitCode | Should -Be 2
            $_.Exception.Message | Should -Match 'alcançam o PostgreSQL'
        }
    }

    It 'aceita nome ou IP de rede e recusa caCertificate com chave privada' {
        $root = New-TestDirectory
        try {
            { Assert-ExistingPostgresAnswers -Answers (New-ExistingAnswers -Dir $root -Override @{ host = '172.17.0.1' }) } | Should -Not -Throw
            $ca = Join-Path $root 'ca.pem'
            [System.IO.File]::WriteAllText($ca, "-----BEGIN CERTIFICATE-----`nAAAA`n-----END CERTIFICATE-----`n-----BEGIN PRIVATE KEY-----`nBBBB`n-----END PRIVATE KEY-----`n")
            try { Assert-ExistingPostgresAnswers -Answers (New-ExistingAnswers -Dir $root -Override @{ caCertificate = $ca }); throw 'deveria falhar' }
            catch { $_.Exception.ExitCode | Should -Be 2 }
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force }
    }

    It 'senha administrativa de { env }, do prompt no terminal, ou código 2 sem terminal; nunca em secretsDir' {
        $root = New-TestDirectory
        try {
            $env:DOKPOD_TEST_PG_ADMIN = 'canario-admin-env-033'
            $withEnv = New-ExistingAnswers -Dir $root
            $withEnv.postgres.admin['password'] = @{ env = 'DOKPOD_TEST_PG_ADMIN' }
            (Get-ExistingPostgresAdminServer -Answers $withEnv -Interactive $false).Password | Should -Be 'canario-admin-env-033'
            (Protect-InstallerText -Text 'x canario-admin-env-033') | Should -Not -Match 'canario' -Because 'a senha fica registrada para redação'

            $prompted = Get-ExistingPostgresAdminServer -Answers (New-ExistingAnswers -Dir $root) -Interactive $true -AskCredential { param($message) $message | Should -Match 'não é gravada'; 'canario-admin-prompt-033' }
            $prompted.Password | Should -Be 'canario-admin-prompt-033'

            try { Get-ExistingPostgresAdminServer -Answers (New-ExistingAnswers -Dir $root) -Interactive $false; throw 'deveria falhar' }
            catch { $_.Exception.ExitCode | Should -Be 2 }
            @(Get-ChildItem -LiteralPath $root -Recurse -File).Count | Should -Be 0
        }
        finally {
            Remove-Item Env:DOKPOD_TEST_PG_ADMIN -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'sslMode chega ao psql (PGSSLMODE e a CA no tar) e à connection string do Npgsql' {
        $root = New-TestDirectory
        try {
            $fake = New-FakeEngine -Root $root
            $pem = "-----BEGIN CERTIFICATE-----`nAAAA`n-----END CERTIFICATE-----`n"
            $server = [ordered]@{ Host = 'db'; Port = 5432; Username = 'dba'; Password = 'senha-qualquer'; SslMode = 'verify-full'; CaCertificate = $pem }
            [void] (Invoke-PsqlCatalog -Image 'img' -Network 'bridge' -Server $server -CatalogRoot $script:catalog -Cli $fake -Steps @(
                    @{ Path = 'Installation/checks/server-version.sql'; Database = 'postgres' }))
            $entries = Get-TarEntries -Path (Join-Path $root 'stdin.tar')
            $entries['run.sh'].Text | Should -Match 'export PGSSLMODE=verify-full'
            $entries['run.sh'].Text | Should -Match 'export PGSSLROOTCERT=/run/dokpod-psql/ca.crt'
            $entries['ca.crt'].Text | Should -Be $pem

            $server.Remove('CaCertificate')
            [void] (Invoke-PsqlCatalog -Image 'img' -Network 'bridge' -Server $server -CatalogRoot $script:catalog -Cli $fake -Steps @(
                    @{ Path = 'Installation/checks/server-version.sql'; Database = 'postgres' }))
            (Get-TarEntries -Path (Join-Path $root 'stdin.tar'))['run.sh'].Text | Should -Match 'export PGSSLROOTCERT=system'

            $connection = ConvertTo-PostgresConnectionString -Server 'db' -Port 5432 -Database 'dokpod' -Username 'u' -Password 'p' -SslMode 'verify-ca'
            $connection | Should -Match 'SSL Mode=VerifyCA'
            ConvertTo-PostgresConnectionString -Server 'db' -Port 5432 -Database 'dokpod' -Username 'u' -Password 'p' | Should -Not -Match 'SSL'
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force }
    }
}

Describe 'existing-create contra PostgreSQL real com TLS e administrador sem superusuário' {
    BeforeAll {
        $script:skip = -not $env:DOCKER_HOST
        if ($script:skip) { return }

        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $script:existingNetwork = "dokpod-033-pgx-$suffix"
        $script:existingServer = "dokpod-033-pgx-$suffix"
        $script:superPassword = "canario-pgx-super-033-$suffix"
        $script:dbaPassword = "canario-pgx-dba-033-$suffix"
        $script:existingHost = 'db.existing.test'
        # O servidor gera CA e certificado na partida: TLS com CA privada, como num PostgreSQL da organização.
        $tls = 'set -e; cd /tmp; ' +
            'openssl req -x509 -newkey ec -pkeyopt ec_paramgen_curve:P-256 -nodes -days 2 -subj /CN=dokpod-test-ca -keyout ca.key -out ca.crt 2>/dev/null; ' +
            "openssl req -newkey ec -pkeyopt ec_paramgen_curve:P-256 -nodes -subj /CN=$($script:existingHost) -addext subjectAltName=DNS:$($script:existingHost) -keyout server.key -out server.csr 2>/dev/null; " +
            'openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -days 2 -copy_extensions copy -out server.crt 2>/dev/null; ' +
            'chown postgres server.key server.crt; chmod 600 server.key; ' +
            'exec docker-entrypoint.sh postgres -c ssl=on -c ssl_cert_file=/tmp/server.crt -c ssl_key_file=/tmp/server.key'
        & docker network create $script:existingNetwork | Out-Null
        & docker run --detach --pids-limit 512 --name $script:existingServer --network $script:existingNetwork --network-alias $script:existingHost --log-driver none `
            --env POSTGRES_USER=postgres --env POSTGRES_DB=postgres --env "POSTGRES_PASSWORD=$($script:superPassword)" --entrypoint sh $script:image -c $tls | Out-Null
        $deadline = [datetime]::UtcNow.AddSeconds(90)
        do {
            Start-Sleep -Milliseconds 500
            & docker exec $script:existingServer pg_isready -h 127.0.0.1 -U postgres -d postgres *> $null
        } until ($LASTEXITCODE -eq 0 -or [datetime]::UtcNow -gt $deadline)

        # Administrador como num PostgreSQL gerenciado (sem SUPERUSER), e um banco e uma role de outro sistema (SC-006).
        & docker exec $script:existingServer psql -v ON_ERROR_STOP=1 -q -U postgres -d postgres `
            -c "CREATE ROLE dba LOGIN CREATEROLE CREATEDB PASSWORD '$($script:dbaPassword)'" -c "CREATE ROLE outra LOGIN PASSWORD 'x'" -c 'CREATE DATABASE outro OWNER outra' | Out-Null
        $script:caPem = (& docker exec $script:existingServer cat /tmp/ca.crt) -join "`n"
        $script:inventory = {
            (& docker exec $script:existingServer psql -At -U postgres -d postgres -c "SELECT string_agg(datname || ':' || pg_get_userbyid(datdba), ',' ORDER BY datname) FROM pg_database WHERE datname NOT LIKE 'dokpod%'; SELECT string_agg(rolname || ':' || rolsuper || rolcreaterole || rolcreatedb, ',' ORDER BY rolname) FROM pg_roles WHERE rolname NOT LIKE 'dokpod%' AND rolname NOT LIKE 'pg\_%';") -join "`n"
        }
        $script:before = & $script:inventory
    }

    AfterAll {
        if ($script:skip) { return }
        & docker rm --force --volumes $script:existingServer *> $null
        & docker network rm $script:existingNetwork *> $null
    }

    It 'cria banco e roles com o dba, valida a cadeia TLS, não grava a credencial e reexecuta sem ela' {
        if ($script:skip) {
            Set-ItResult -Skipped -Because 'o socket do engine não foi montado'
            return
        }

        $dir = New-TestDirectory
        try {
            $caFile = Join-Path $dir 'postgres-ca.crt'
            [System.IO.File]::WriteAllText($caFile, $script:caPem)
            $passwordFile = Join-Path $dir 'pg-admin-password'
            [System.IO.File]::WriteAllText($passwordFile, $script:dbaPassword)
            $secrets = Join-Path $dir 'secrets'
            New-Item -ItemType Directory -Path $secrets | Out-Null
            $answers = [ordered]@{
                secretsDir = $secrets; installDir = $dir; keycloak = @{ mode = 'provision' }
                postgres = [ordered]@{ mode = 'existing-create'; host = $script:existingHost; port = 5432; sslMode = 'verify-full'; caCertificate = $caFile
                    admin = [ordered]@{ username = 'dba' } }
            }
            # Chave por índice: o par chave=valor de senha não aparece no texto (T056).
            $answers.postgres.admin['password'] = @{ file = $passwordFile }
            $common = @{ Image = $script:image; Network = $script:existingNetwork; CatalogRoot = $script:catalog; Interactive = $false }

            $first = Invoke-ExistingPostgresBootstrap -Answers $answers @common
            $first.ServerVersion | Should -BeGreaterOrEqual 180000
            $first.ConnectionsWritten | Should -Be @('dokpod-owner-connection', 'ConnectionStrings__ControlPlane')
            $runtimeText = [System.IO.File]::ReadAllText((Join-Path $secrets 'ConnectionStrings__ControlPlane'))
            $runtimeText | Should -Match "Host=$([regex]::Escape($script:existingHost))"
            $runtimeText | Should -Match 'SSL Mode=VerifyFull'
            $runtime = Read-PostgresConnection -SecretsDirectory $secrets -Name 'ConnectionStrings__ControlPlane'
            $runtime.Username | Should -Be 'dokpod_app'

            # A credencial administrativa não fica em lugar nenhum da instalação.
            foreach ($file in Get-ChildItem -LiteralPath $dir -Recurse -File | Where-Object FullName -ne $passwordFile) {
                [System.IO.File]::ReadAllText($file.FullName) | Should -Not -Match ([regex]::Escape($script:dbaPassword)) -Because $file.FullName
            }

            # Sem a CA, verify-full recusa: a cadeia é validada de fato.
            $noCa = [ordered]@{} + $answers; $noCa.postgres = [ordered]@{} + $answers.postgres; $noCa.postgres.Remove('caCertificate')
            $server = Get-ExistingPostgresAdminServer -Answers $noCa -Interactive $false
            { Get-PostgresServerVersion -Image $script:image -Network $script:existingNetwork -Server $server -CatalogRoot $script:catalog } | Should -Throw '*server-version.sql*'

            # Reexecução sem a credencial administrativa (arquivo apagado, sem terminal): só confere com a role dona.
            Remove-Item -LiteralPath $passwordFile
            $second = Invoke-ExistingPostgresBootstrap -Answers $answers @common
            $second.ConnectionsWritten.Count | Should -Be 0
            $second.OwnerRole | Should -Be 'dokpod_owner'
            (Read-PostgresConnection -SecretsDirectory $secrets -Name 'ConnectionStrings__ControlPlane').Password | Should -Be $runtime.Password

            # Nada fora do Dokpod mudou no servidor (SC-006).
            & $script:inventory | Should -Be $script:before
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'conexões de outro servidor em secretsDir são recusadas, sem sobrescrever' {
        if ($script:skip) {
            Set-ItResult -Skipped -Because 'o socket do engine não foi montado'
            return
        }

        $dir = New-TestDirectory
        try {
            foreach ($name in 'dokpod-owner-connection', 'ConnectionStrings__ControlPlane') {
                Write-InstallerSecret -SecretsDirectory $dir -Name $name -Value (ConvertTo-PostgresConnectionString -Server 'outro.lab.test' -Port 5432 -Database 'dokpod' -Username 'dokpod_owner' -Password 'senha-033')
            }
            $answers = [ordered]@{ secretsDir = $dir; installDir = $dir; keycloak = @{ mode = 'provision' }
                postgres = [ordered]@{ mode = 'existing-create'; host = $script:existingHost; port = 5432; admin = [ordered]@{ username = 'dba' } } }
            { Invoke-ExistingPostgresBootstrap -Answers $answers -Image $script:image -Network $script:existingNetwork -CatalogRoot $script:catalog -Interactive $false } |
                Should -Throw '*outro.lab.test*recreate secret:dokpod-owner-connection*'
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }
}
