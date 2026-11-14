#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T032 (FR-021, FR-024, FR-014, US1): instalação completa do cenário 1 com engine real e as imagens desta
# branch, num projeto compose próprio (dokpod-t032), sem tocar no projeto dokpod do host. Confere a subida ordenada, o
# health de cada serviço, a borda servindo o console e o OIDC, o agente conectado e a reexecução convergida.
#
# Só roda com DOKPOD_TEST_FULL_INSTALL=1, o socket do engine (DOCKER_HOST), DOKPOD_HOST_ENGINE_SOCKET (o caminho do
# socket no host, montado no agente) e DOKPOD_TEST_SHARED (montado no mesmo caminho no host e no container dos testes,
# como install.sh faz). Imagens: DOKPOD_TEST_API_IMAGE, DOKPOD_TEST_BFF_IMAGE, DOKPOD_TEST_WEB_IMAGE,
# DOKPOD_TEST_AGENT_IMAGE e DOKPOD_INSTALLER_IMAGE_REF, todas construídas com --format docker (o OCI descarta o HEALTHCHECK).

BeforeAll {
    . (Join-Path $PSScriptRoot 'TestCleanup.ps1')
    foreach ($module in 'Common', 'Secrets', 'State', 'Install') {
        Import-Module (Join-Path $PSScriptRoot "../src/$module.psm1") -Force
    }
    $script:enabled = $env:DOKPOD_TEST_FULL_INSTALL -eq '1' -and $env:DOCKER_HOST -and $env:DOKPOD_TEST_SHARED -and $env:DOKPOD_HOST_ENGINE_SOCKET
    $script:project = 'dokpod-t032'
}

Describe 'Instalação completa do cenário 1' {
    BeforeAll {
        if (-not $script:enabled) { return }
        $script:root = Join-Path $env:DOKPOD_TEST_SHARED "t032-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $script:answers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18443'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $script:root 'install'); secretsDir = (Join-Path $script:root 'secrets'); manualMigrations = $false
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 18443 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        $script:images = [ordered]@{
            api = $env:DOKPOD_TEST_API_IMAGE; bff = $env:DOKPOD_TEST_BFF_IMAGE; web = $env:DOKPOD_TEST_WEB_IMAGE; agent = $env:DOKPOD_TEST_AGENT_IMAGE
        }
        $script:infra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images
        $script:confirmations = @{ Plan = $true }
        $script:install = {
            Invoke-InstallerInstall -Answers $script:answers -Images $script:images -Infrastructure $script:infra -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
                -Confirmations $script:confirmations -Interactive $false -Project $script:project -WaitTimeoutSeconds 420
        }
        # Como o operador: --yes não confirma migrations (FR-036); a execução para com o planHash e a retomada o confirma.
        $script:declined = try { & $script:install } catch { $_.Exception }
        $script:planHash = [regex]::Match($script:declined.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        $script:confirmations['MigrationsPlanHash'] = $script:planHash
        $script:first = & $script:install
    }

    AfterAll {
        if (-not $script:enabled) { return }
        & docker compose --file (Join-Path $script:answers.installDir 'compose.yaml') --project-name $script:project down --volumes 2>$null | Out-Null
        # No Podman 6 o down pode deixar container parado e redes; o que sobrar sai pelo rótulo do projeto.
        Remove-TestComposeProject -Project $script:project
        # Os arquivos que os containers gravaram são do usuário do host (engine rootless, --user 0:0): removíveis aqui.
        Remove-Item -LiteralPath $script:root -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'sem --confirm-migrations para com código 4 antes das migrations, e a retomada com o planHash termina com código 0' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $script:declined.ExitCode | Should -Be 4
        $script:planHash | Should -Match '^[0-9a-f]{64}$'
        $script:first | Should -Be 0
    }

    It 'todos os serviços ficam rodando e saudáveis' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $lines = & docker compose --file (Join-Path $script:answers.installDir 'compose.yaml') --project-name $script:project ps --all --format json
        $services = @($lines | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
        ($services.Service | Sort-Object) | Should -Be @('agent', 'api', 'bff', 'dokpod-postgres', 'edge', 'keycloak', 'keycloak-postgres', 'web')
        foreach ($service in $services) {
            "$($service.Service)=$($service.State)" | Should -Be "$($service.Service)=running"
            if ($service.Health) { "$($service.Service)=$($service.Health)" | Should -Be "$($service.Service)=healthy" }
        }
    }

    It 'a borda serve o console e o OIDC do Keycloak pelo nome público, com o certificado da CA local' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $ca = Join-Path $script:answers.installDir 'certs/ca.crt'
        $nginx = $script:infra.nginx
        $check = {
            param([string] $path)
            # --pids-limit explícito: no Podman 6, o docker run sem ele cria o container com pids.max 1.
            & docker run --rm --pids-limit 512 --log-driver none --network "$($script:project)_dokpod-edge" --volume "$($ca):/ca.crt:ro" --entrypoint curl $nginx `
                --silent --output /dev/null --write-out '%{http_code}' --cacert /ca.crt "https://dokpod.lab.test:18443$path"
        }
        & $check '/dokpod/' | Should -Be '200'
        & $check '/realms/dokpod/.well-known/openid-configuration' | Should -Be '200'
        # O BFF chega à Authority pelo nome público (D-008) e manda o navegador para o login do Keycloak.
        & $check '/dokpod/bff/login' | Should -Match '^30[23]$'
    }

    It 'o ambiente e a identidade do agente estão no estado, e a senha temporária ficou em secretsDir sem TTY' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $state = Open-InstallerState -InstallDirectory $script:answers.installDir
        try {
            $environments = Invoke-StateSql -State $state -Name 'environments-list' -Rows
            $environments.Count | Should -Be 1
            $environments[0]['agent_fingerprint'] | Should -Match '^[0-9A-F]{64}$'
            $installation = Invoke-StateSql -State $state -Name 'installation-get' -Rows
            $installation[0]['version'] | Should -Be '0.8.0'
        }
        finally { Close-InstallerState -State $state }
        Test-InstallerSecret -SecretsDirectory $script:answers.secretsDir -Name 'initial-admin-password' | Should -BeTrue
        [System.IO.File]::ReadAllText((Join-Path $script:answers.secretsDir 'keycloak-bootstrap-admin')) | Should -BeNullOrEmpty
    }

    It 'a reexecução com as mesmas respostas termina convergida, sem recriar serviços' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $before = & docker ps --filter "label=com.docker.compose.project=$($script:project)" --format '{{.ID}}' | Sort-Object
        & $script:install | Should -Be 0
        $after = & docker ps --filter "label=com.docker.compose.project=$($script:project)" --format '{{.ID}}' | Sort-Object
        $after | Should -Be $before
        $state = Open-InstallerState -InstallDirectory $script:answers.installDir
        try {
            $runs = Invoke-StateSql -State $state -Name 'runs-expired' -Parameters @{ keep = 0 } -Rows
            $last = $runs | ForEach-Object { (Invoke-StateSql -State $state -Name 'run-get' -Parameters @{ run_id = $_['run_id'] } -Rows)[0] } |
                Sort-Object { $_['started_at'] } | Select-Object -Last 1
            $last['outcome'] | Should -Be 'converged'
        }
        finally { Close-InstallerState -State $state }
    }
}

# Spec 033 T035 (FR-008, D-009): o mesmo cenário com postgres.mode existing-create, contra um PostgreSQL de fora do
# projeto, publicado no host, com TLS de CA privada (verify-full) e um administrador sem SUPERUSER. Exige também
# DOKPOD_TEST_EXISTING_PG_HOST: o endereço pelo qual os containers alcançam uma porta publicada no host
# (host.containers.internal no Podman rootless; o IP do host na rede no Docker rootless).
Describe 'Instalação com PostgreSQL existente (existing-create)' {
    BeforeAll {
        $script:existingEnabled = $script:enabled -and $env:DOKPOD_TEST_EXISTING_PG_HOST
        if (-not $script:existingEnabled) { return }
        $script:existingProject = 'dokpod-t035'
        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $script:root = Join-Path $env:DOKPOD_TEST_SHARED "t035-$suffix"
        New-Item -ItemType Directory -Path $script:root | Out-Null
        $script:infra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images
        $pgHost = $env:DOKPOD_TEST_EXISTING_PG_HOST
        $address = $null
        $san = if ([System.Net.IPAddress]::TryParse($pgHost, [ref] $address)) { "IP:$pgHost" } else { "DNS:$pgHost" }
        $script:pgServer = "dokpod-t035-pg-$suffix"
        $script:pgPort = Get-Random -Minimum 25000 -Maximum 26000
        $script:superPassword = "canario-t035-super-$suffix"
        $script:dbaPassword = "canario-t035-dba-$suffix"
        $tls = 'set -e; cd /tmp; ' +
            'openssl req -x509 -newkey ec -pkeyopt ec_paramgen_curve:P-256 -nodes -days 2 -subj /CN=dokpod-t035-ca -keyout ca.key -out ca.crt 2>/dev/null; ' +
            "openssl req -newkey ec -pkeyopt ec_paramgen_curve:P-256 -nodes -subj /CN=postgres -addext subjectAltName=$san -keyout server.key -out server.csr 2>/dev/null; " +
            'openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -days 2 -copy_extensions copy -out server.crt 2>/dev/null; ' +
            'chown postgres server.key server.crt; chmod 600 server.key; ' +
            'exec docker-entrypoint.sh postgres -c ssl=on -c ssl_cert_file=/tmp/server.crt -c ssl_key_file=/tmp/server.key'
        & docker run --detach --pids-limit 512 --name $script:pgServer --publish "$($script:pgPort):5432" --log-driver none `
            --env POSTGRES_USER=postgres --env POSTGRES_DB=postgres --env "POSTGRES_PASSWORD=$($script:superPassword)" --entrypoint sh $script:infra.postgresql -c $tls | Out-Null
        $deadline = [datetime]::UtcNow.AddSeconds(90)
        do {
            Start-Sleep -Milliseconds 500
            & docker exec $script:pgServer pg_isready -h 127.0.0.1 -U postgres -d postgres *> $null
        } until ($LASTEXITCODE -eq 0 -or [datetime]::UtcNow -gt $deadline)
        & docker exec $script:pgServer psql -v ON_ERROR_STOP=1 -q -U postgres -d postgres -c "CREATE ROLE dba LOGIN CREATEROLE CREATEDB PASSWORD '$($script:dbaPassword)'" | Out-Null
        $script:caFile = Join-Path $script:root 'postgres-ca.crt'
        [System.IO.File]::WriteAllText($script:caFile, ((& docker exec $script:pgServer cat /tmp/ca.crt) -join "`n") + "`n")
        $script:passwordFile = Join-Path $script:root 'pg-admin-password'
        [System.IO.File]::WriteAllText($script:passwordFile, $script:dbaPassword)

        $script:existingAnswers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18453'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $script:root 'install'); secretsDir = (Join-Path $script:root 'secrets'); manualMigrations = $false
            postgres = [ordered]@{ mode = 'existing-create'; host = $pgHost; port = $script:pgPort; sslMode = 'verify-full'; caCertificate = $script:caFile
                admin = [ordered]@{ username = 'dba' } }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 18453 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        # Chave por índice: o par chave=valor de senha não aparece no texto (T056).
        $script:existingAnswers.postgres.admin['password'] = @{ file = $script:passwordFile }
        $images = [ordered]@{ api = $env:DOKPOD_TEST_API_IMAGE; bff = $env:DOKPOD_TEST_BFF_IMAGE; web = $env:DOKPOD_TEST_WEB_IMAGE; agent = $env:DOKPOD_TEST_AGENT_IMAGE }
        $script:existingConfirmations = @{ Plan = $true }
        $script:existingInstall = {
            Invoke-InstallerInstall -Answers $script:existingAnswers -Images $images -Infrastructure $script:infra -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
                -Confirmations $script:existingConfirmations -Interactive $false -Project $script:existingProject -WaitTimeoutSeconds 420
        }
        $declined = try { & $script:existingInstall } catch { $_.Exception }
        $script:existingConfirmations['MigrationsPlanHash'] = [regex]::Match($declined.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        $script:existingFirst = & $script:existingInstall
    }

    AfterAll {
        if (-not $script:existingEnabled) { return }
        Remove-TestComposeProject -Project $script:existingProject
        & docker rm --force --volumes $script:pgServer 2>$null | Out-Null
        Remove-Item -LiteralPath $script:root -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'instala sem dokpod-postgres, com todos os serviços saudáveis' {
        if (-not $script:existingEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou DOKPOD_TEST_EXISTING_PG_HOST não está ativo'; return }
        $script:existingFirst | Should -Be 0
        $lines = & docker compose --file (Join-Path $script:existingAnswers.installDir 'compose.yaml') --project-name $script:existingProject ps --all --format json
        $services = @($lines | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
        ($services.Service | Sort-Object) | Should -Be @('agent', 'api', 'bff', 'edge', 'keycloak', 'keycloak-postgres', 'web')
        foreach ($service in $services) {
            "$($service.Service)=$($service.State)" | Should -Be "$($service.Service)=running"
            if ($service.Health) { "$($service.Service)=$($service.Health)" | Should -Be "$($service.Service)=healthy" }
        }
    }

    It 'a API conecta com a role de runtime por TLS, e as roles do Dokpod não têm privilégio administrativo' {
        if (-not $script:existingEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou DOKPOD_TEST_EXISTING_PG_HOST não está ativo'; return }
        $sessions = (& docker exec $script:pgServer psql -At -U postgres -d postgres -c "SELECT DISTINCT usename || ':' || ssl FROM pg_stat_activity JOIN pg_stat_ssl USING (pid) WHERE datname = 'dokpod'") -join ','
        $sessions | Should -Match 'dokpod_app:t'
        $roles = (& docker exec $script:pgServer psql -At -U postgres -d postgres -c "SELECT string_agg(rolname || ':' || (rolsuper OR rolcreaterole OR rolcreatedb OR rolreplication OR rolbypassrls), ',' ORDER BY rolname) FROM pg_roles WHERE rolname LIKE 'dokpod%'") -join ''
        $roles | Should -Be 'dokpod_app:false,dokpod_owner:false,dokpod_runtime:false'
    }

    It 'a credencial administrativa não fica em installDir nem em secretsDir' {
        if (-not $script:existingEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou DOKPOD_TEST_EXISTING_PG_HOST não está ativo'; return }
        foreach ($directory in $script:existingAnswers.installDir, $script:existingAnswers.secretsDir) {
            foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
                $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
                [System.Text.Encoding]::UTF8.GetString($bytes) | Should -Not -Match ([regex]::Escape($script:dbaPassword)) -Because $file.FullName
            }
        }
    }

    It 'a reexecução sem a credencial administrativa termina convergida, sem recriar serviços' {
        if (-not $script:existingEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou DOKPOD_TEST_EXISTING_PG_HOST não está ativo'; return }
        Remove-Item -LiteralPath $script:passwordFile
        $before = & docker ps --filter "label=com.docker.compose.project=$($script:existingProject)" --format '{{.ID}}' | Sort-Object
        & $script:existingInstall | Should -Be 0
        $after = & docker ps --filter "label=com.docker.compose.project=$($script:existingProject)" --format '{{.ID}}' | Sort-Object
        $after | Should -Be $before
    }
}

Describe 'Instalação com PostgreSQL existente (existing-prepared)' {
    BeforeAll {
        $script:preparedEnabled = $script:enabled -and $env:DOKPOD_TEST_EXISTING_PG_HOST
        if (-not $script:preparedEnabled) { return }
        $script:preparedProject = 'dokpod-t036'
        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $script:preparedRoot = Join-Path $env:DOKPOD_TEST_SHARED "t036-$suffix"
        New-Item -ItemType Directory -Path $script:preparedRoot | Out-Null
        $infra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images
        $script:preparedServer = "dokpod-t036-pg-$suffix"
        $port = Get-Random -Minimum 26000 -Maximum 27000
        # TLS com certificado autoassinado e sslMode require: cifra sem validar a cadeia, o caminho sem caCertificate.
        $tls = 'set -e; cd /tmp; ' +
            'openssl req -x509 -newkey ec -pkeyopt ec_paramgen_curve:P-256 -nodes -days 2 -subj /CN=postgres -keyout server.key -out server.crt 2>/dev/null; ' +
            'chown postgres server.key server.crt; chmod 600 server.key; ' +
            'exec docker-entrypoint.sh postgres -c ssl=on -c ssl_cert_file=/tmp/server.crt -c ssl_key_file=/tmp/server.key'
        & docker run --detach --pids-limit 512 --name $script:preparedServer --publish "$($port):5432" --log-driver none `
            --env POSTGRES_USER=postgres --env POSTGRES_DB=postgres --env "POSTGRES_PASSWORD=canario-t036-super-$suffix" --entrypoint sh $infra.postgresql -c $tls | Out-Null
        $deadline = [datetime]::UtcNow.AddSeconds(90)
        do {
            Start-Sleep -Milliseconds 500
            & docker exec $script:preparedServer pg_isready -h 127.0.0.1 -U postgres -d postgres *> $null
        } until ($LASTEXITCODE -eq 0 -or [datetime]::UtcNow -gt $deadline)

        # O que o usuário prepara (README): grupo dokpod_runtime, dona do banco e role de runtime só com CONNECT.
        $credentials = @{ owner = "canario-t036-owner-$suffix"; runtime = "canario-t036-app-$suffix" }
        & docker exec $script:preparedServer psql -v ON_ERROR_STOP=1 -q -U postgres -d postgres -c 'CREATE ROLE dokpod_runtime NOLOGIN' `
            -c "CREATE ROLE prep_owner LOGIN PASSWORD '$($credentials.owner)'" -c "CREATE ROLE prep_app LOGIN PASSWORD '$($credentials.runtime)' IN ROLE dokpod_runtime" `
            -c 'CREATE DATABASE dokpod_prep OWNER prep_owner' -c 'REVOKE CONNECT ON DATABASE dokpod_prep FROM PUBLIC' -c 'GRANT CONNECT ON DATABASE dokpod_prep TO prep_app' | Out-Null

        $script:preparedAnswers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18454'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $script:preparedRoot 'install'); secretsDir = (Join-Path $script:preparedRoot 'secrets'); manualMigrations = $false
            postgres = [ordered]@{ mode = 'existing-prepared'; host = $env:DOKPOD_TEST_EXISTING_PG_HOST; port = $port; sslMode = 'require'; database = 'dokpod_prep'
                owner = [ordered]@{ username = 'prep_owner' }; runtime = [ordered]@{ username = 'prep_app' } }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 18454 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        foreach ($key in 'owner', 'runtime') {
            $file = Join-Path $script:preparedRoot "pg-$key-credential"
            [System.IO.File]::WriteAllText($file, $credentials[$key])
            # Chave por índice: o par chave=valor de senha não aparece no texto (T056).
            $script:preparedAnswers.postgres[$key]['password'] = @{ file = $file }
        }
        $images = [ordered]@{ api = $env:DOKPOD_TEST_API_IMAGE; bff = $env:DOKPOD_TEST_BFF_IMAGE; web = $env:DOKPOD_TEST_WEB_IMAGE; agent = $env:DOKPOD_TEST_AGENT_IMAGE }
        $script:preparedConfirmations = @{ Plan = $true }
        $script:preparedInstall = {
            Invoke-InstallerInstall -Answers $script:preparedAnswers -Images $images -Infrastructure $infra -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
                -Confirmations $script:preparedConfirmations -Interactive $false -Project $script:preparedProject -WaitTimeoutSeconds 420
        }
        $declined = try { & $script:preparedInstall } catch { $_.Exception }
        $script:preparedConfirmations['MigrationsPlanHash'] = [regex]::Match($declined.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        $script:preparedFirst = & $script:preparedInstall
    }

    AfterAll {
        if (-not $script:preparedEnabled) { return }
        Remove-TestComposeProject -Project $script:preparedProject
        & docker rm --force --volumes $script:preparedServer 2>$null | Out-Null
        Remove-Item -LiteralPath $script:preparedRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'instala no banco preparado, com as migrations pela role dona e a API pela de runtime' {
        if (-not $script:preparedEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou DOKPOD_TEST_EXISTING_PG_HOST não está ativo'; return }
        $script:preparedFirst | Should -Be 0
        $lines = & docker compose --file (Join-Path $script:preparedAnswers.installDir 'compose.yaml') --project-name $script:preparedProject ps --all --format json
        $services = @($lines | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
        ($services.Service | Sort-Object) | Should -Be @('agent', 'api', 'bff', 'edge', 'keycloak', 'keycloak-postgres', 'web')
        foreach ($service in $services) {
            "$($service.Service)=$($service.State)" | Should -Be "$($service.Service)=running"
            if ($service.Health) { "$($service.Service)=$($service.Health)" | Should -Be "$($service.Service)=healthy" }
        }

        (& docker exec $script:preparedServer psql -At -U postgres -d dokpod_prep -c "SELECT pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname = 'dokpod'") | Should -Be 'prep_owner'
        $sessions = (& docker exec $script:preparedServer psql -At -U postgres -d postgres -c "SELECT DISTINCT usename || ':' || ssl FROM pg_stat_activity JOIN pg_stat_ssl USING (pid) WHERE datname = 'dokpod_prep'") -join ','
        $sessions | Should -Match 'prep_app:t'
        # Nenhuma role nova no servidor: o instalador não teve credencial administrativa.
        (& docker exec $script:preparedServer psql -At -U postgres -d postgres -c "SELECT string_agg(rolname, ',' ORDER BY rolname) FROM pg_roles WHERE rolname NOT LIKE 'pg\_%'") |
            Should -Be 'dokpod_runtime,postgres,prep_app,prep_owner'
    }

    It 'a reexecução termina convergida, sem recriar serviços' {
        if (-not $script:preparedEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou DOKPOD_TEST_EXISTING_PG_HOST não está ativo'; return }
        $before = & docker ps --filter "label=com.docker.compose.project=$($script:preparedProject)" --format '{{.ID}}' | Sort-Object
        & $script:preparedInstall | Should -Be 0
        $after = & docker ps --filter "label=com.docker.compose.project=$($script:preparedProject)" --format '{{.ID}}' | Sort-Object
        $after | Should -Be $before
    }
}

Describe 'Instalação com nginx existente em loopback (edge.mode existing)' {
    BeforeAll {
        $script:edgeEnabled = $script:enabled -and $env:DOKPOD_HOST_ENGINE -eq 'podman'
        if (-not $script:edgeEnabled) { return }
        $script:edgeProject = 'dokpod-t037'
        $script:edgeRoot = Join-Path $env:DOKPOD_TEST_SHARED "t037-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        New-Item -ItemType Directory -Path $script:edgeRoot | Out-Null
        $script:edgeInfra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images
        $script:edgeAnswers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18455'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $script:edgeRoot 'install'); secretsDir = (Join-Path $script:edgeRoot 'secrets'); manualMigrations = $false
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = [ordered]@{ mode = 'existing'; upstreamHost = '127.0.0.1'; webPort = 18480; bffPort = 18481; keycloakPort = 18482 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        $images = [ordered]@{ api = $env:DOKPOD_TEST_API_IMAGE; bff = $env:DOKPOD_TEST_BFF_IMAGE; web = $env:DOKPOD_TEST_WEB_IMAGE; agent = $env:DOKPOD_TEST_AGENT_IMAGE }
        $script:edgeConfirmations = @{ Plan = $true }
        $script:edgeInstall = {
            Invoke-InstallerInstall -Answers $script:edgeAnswers -Images $images -Infrastructure $script:edgeInfra -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
                -Confirmations $script:edgeConfirmations -Interactive $false -Project $script:edgeProject -WaitTimeoutSeconds 420
        }
        $declined = try { & $script:edgeInstall } catch { $_.Exception }
        $script:edgeConfirmations['MigrationsPlanHash'] = [regex]::Match($declined.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        $script:edgeFirst = & $script:edgeInstall

        # O nginx "do usuário": na rede do host, com o certificado gerado e os dois include, como mandam os passos.
        $edgeDirectory = Join-Path $script:edgeAnswers.installDir 'edge'
        $site = Join-Path $script:edgeRoot 'user-nginx.conf'
        [System.IO.File]::WriteAllText($site, @"
pid /tmp/nginx.pid;
error_log stderr;
events {}
http {
    access_log off;
    client_body_temp_path /tmp/b; proxy_temp_path /tmp/p; fastcgi_temp_path /tmp/f; uwsgi_temp_path /tmp/u; scgi_temp_path /tmp/s;
    include $edgeDirectory/dokpod.conf;
    server {
        listen 18455 ssl;
        server_name dokpod.lab.test;
        ssl_certificate /certs/tls.crt;
        ssl_certificate_key /certs/tls.key;
        location = /antigo { return 200 "site anterior"; }
        include $edgeDirectory/dokpod.locations;
    }
}
"@)
        $script:userNginx = "dokpod-t037-user-nginx"
        & docker run --detach --pids-limit 512 --name $script:userNginx --network host --log-driver none `
            --volume "$($edgeDirectory):$($edgeDirectory):ro" --volume "$($site):/user.conf:ro" `
            --volume "$(Join-Path $script:edgeAnswers.secretsDir 'pki/edge.crt'):/certs/tls.crt:ro" --volume "$(Join-Path $script:edgeAnswers.secretsDir 'pki/edge.key'):/certs/tls.key:ro" `
            --entrypoint nginx $script:edgeInfra.nginx -c /user.conf -g 'daemon off;' | Out-Null
        $script:edgeCa = Join-Path $script:edgeAnswers.installDir 'certs/ca.crt'
        $script:edgeCurl = {
            param([string] $path, [string[]] $extra = @())
            & docker run --rm --pids-limit 512 --log-driver none --network host --volume "$($script:edgeCa):/ca.crt:ro" --entrypoint curl $script:edgeInfra.nginx `
                --silent --cacert /ca.crt --resolve 'dokpod.lab.test:18455:127.0.0.1' @extra "https://dokpod.lab.test:18455$path"
        }
        $deadline = [datetime]::UtcNow.AddSeconds(30)
        do { Start-Sleep -Seconds 1 } until ((& $script:edgeCurl '/antigo') -eq 'site anterior' -or [datetime]::UtcNow -gt $deadline)
    }

    AfterAll {
        if (-not $script:edgeEnabled) { return }
        & docker rm --force $script:userNginx 2>$null | Out-Null
        Remove-TestComposeProject -Project $script:edgeProject
        Remove-Item -LiteralPath $script:edgeRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'instala sem a borda do Dokpod, com web, BFF e Keycloak só no loopback' {
        if (-not $script:edgeEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou o engine Podman não está ativo'; return }
        $script:edgeFirst | Should -Be 0
        $lines = & docker compose --file (Join-Path $script:edgeAnswers.installDir 'compose.yaml') --project-name $script:edgeProject ps --all --format json
        $services = @($lines | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
        ($services.Service | Sort-Object) | Should -Be @('agent', 'api', 'bff', 'dokpod-postgres', 'keycloak', 'keycloak-postgres', 'web')
        foreach ($service in $services | Where-Object Service -in 'web', 'bff', 'keycloak') {
            @($service.Publishers | Where-Object PublishedPort | ForEach-Object URL | Sort-Object -Unique) | Should -Be @('127.0.0.1') -Because $service.Service
        }
    }

    It 'pelo nginx do usuário, o console, o OIDC e o login respondem, com o BFF confiando nos X-Forwarded-* (L1)' {
        if (-not $script:edgeEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou o engine Podman não está ativo'; return }
        & $script:edgeCurl '/dokpod/' @('--output', '/dev/null', '--write-out', '%{http_code}') | Should -Be '200'
        & $script:edgeCurl '/realms/dokpod/.well-known/openid-configuration' @('--output', '/dev/null', '--write-out', '%{http_code}') | Should -Be '200'
        # O BFF chega à Authority pelo host (host-gateway) e monta o redirect_uri com o esquema, o host e o prefixo públicos.
        $location = & $script:edgeCurl '/dokpod/bff/login' @('--output', '/dev/null', '--write-out', '%{redirect_url}')
        $location | Should -Match '^https://dokpod\.lab\.test:18455/realms/dokpod/protocol/openid-connect/auth\?'
        [uri]::UnescapeDataString($location) | Should -Match 'redirect_uri=https://dokpod\.lab\.test:18455/dokpod/signin-oidc'
        # O site anterior do nginx continua igual.
        & $script:edgeCurl '/antigo' | Should -Be 'site anterior'
    }

    It 'o resumo lista os passos no nginx do usuário, e a reexecução termina convergida' {
        if (-not $script:edgeEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou o engine Podman não está ativo'; return }
        $before = & docker ps --filter "label=com.docker.compose.project=$($script:edgeProject)" --format '{{.ID}}' | Sort-Object
        # O resumo sai por [Console]::Out.
        $writer = [System.IO.StringWriter]::new()
        $previous = [Console]::Out
        [Console]::SetOut($writer)
        try { $code = & $script:edgeInstall }
        finally { [Console]::SetOut($previous) }
        $code | Should -Be 0
        $output = $writer.ToString()
        $output | Should -Match 'include .*/edge/dokpod.locations;'
        $output | Should -Match 'nginx -t && nginx -s reload'
        $after = & docker ps --filter "label=com.docker.compose.project=$($script:edgeProject)" --format '{{.ID}}' | Sort-Object
        $after | Should -Be $before
    }
}

Describe 'Instalação com Keycloak existente (keycloak.mode existing)' {
    BeforeAll {
        # O Keycloak do usuário é publicado no host, e containers o alcançam pelo mesmo endereço do PostgreSQL existente.
        $script:kcEnabled = $script:enabled -and $env:DOKPOD_TEST_EXISTING_PG_HOST
        if (-not $script:kcEnabled) { return }
        $script:kcProject = 'dokpod-t039'
        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $script:kcRoot = Join-Path $env:DOKPOD_TEST_SHARED "t039-$suffix"
        New-Item -ItemType Directory -Path $script:kcRoot | Out-Null
        $kcInfra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images
        $kcHost = $env:DOKPOD_TEST_EXISTING_PG_HOST
        $port = Get-Random -Minimum 27000 -Maximum 28000
        $script:kcUrl = "https://$($kcHost):$port"
        $script:kcAdmin = "canario-t039-admin-$suffix"

        $caKey = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
        $caRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=dokpod-t039-ca', $caKey, 'SHA256')
        $caRequest.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true))
        $caCert = $caRequest.CreateSelfSigned([datetimeoffset]::UtcNow.AddMinutes(-5), [datetimeoffset]::UtcNow.AddDays(2))
        $leafKey = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
        $leafRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new("CN=$kcHost", $leafKey, 'SHA256')
        $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        $address = $null
        if ([System.Net.IPAddress]::TryParse($kcHost, [ref] $address)) { $san.AddIpAddress($address) } else { $san.AddDnsName($kcHost) }
        $leafRequest.CertificateExtensions.Add($san.Build())
        $leaf = $leafRequest.Create($caCert, [datetimeoffset]::UtcNow.AddMinutes(-5), [datetimeoffset]::UtcNow.AddDays(2), [byte[]] (1..8))
        $script:kcCa = Join-Path $script:kcRoot 'kc-ca.crt'
        [System.IO.File]::WriteAllText($script:kcCa, $caCert.ExportCertificatePem() + "`n")
        $certFile = Join-Path $script:kcRoot 'tls.crt'; $keyFile = Join-Path $script:kcRoot 'tls.key'
        [System.IO.File]::WriteAllText($certFile, $leaf.ExportCertificatePem() + "`n")
        [System.IO.File]::WriteAllText($keyFile, $leafKey.ExportPkcs8PrivateKeyPem() + "`n")
        foreach ($file in $certFile, $keyFile) { [System.IO.File]::SetUnixFileMode($file, [System.IO.UnixFileMode]'UserRead, UserWrite, GroupRead, OtherRead') }

        $script:kcServer = "dokpod-t039-kc-$suffix"
        & docker run --detach --pids-limit 2048 --name $script:kcServer --publish "$($port):8443" --log-driver none `
            --volume "$($certFile):/certs/tls.crt:ro" --volume "$($keyFile):/certs/tls.key:ro" `
            --env KC_BOOTSTRAP_ADMIN_USERNAME=admin --env "KC_BOOTSTRAP_ADMIN_PASSWORD=$($script:kcAdmin)" `
            --env "KC_HOSTNAME=$($script:kcUrl)" --env KC_HTTPS_CERTIFICATE_FILE=/certs/tls.crt --env KC_HTTPS_CERTIFICATE_KEY_FILE=/certs/tls.key `
            $kcInfra.keycloak start-dev | Out-Null
        $script:kcadm = {
            param([string[]] $Arguments)
            $output = & docker exec $script:kcServer /opt/keycloak/bin/kcadm.sh @Arguments --config /tmp/kcadm.config 2>&1
            [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
        }
        $deadline = [datetime]::UtcNow.AddSeconds(180)
        do {
            Start-Sleep -Seconds 2
            $login = & $script:kcadm @('config', 'credentials', '--server', 'http://localhost:8080', '--realm', 'master', '--user', 'admin', '--password', $script:kcAdmin)
        } until ($login.ExitCode -eq 0 -or [datetime]::UtcNow -gt $deadline)
        [void] (& $script:kcadm @('create', 'realms', '-s', 'realm=outro', '-s', 'enabled=true'))
        $script:kcSnapshot = {
            $realms = foreach ($name in 'master', 'outro') {
                $realm = (& $script:kcadm @('get', "realms/$name")).Output | ConvertFrom-Json
                foreach ($property in @($realm.PSObject.Properties.Name | Where-Object { $_ -match 'Timestamp|notBefore' })) { $realm.PSObject.Properties.Remove($property) }
                $realm
            }
            $users = @(((& $script:kcadm @('get', 'users', '-r', 'master', '--fields', 'username')).Output | ConvertFrom-Json).username) -join ','
            return ($realms | ConvertTo-Json -Depth 30 -Compress) + "|$users"
        }
        $script:kcBefore = & $script:kcSnapshot

        $script:kcCredential = Join-Path $script:kcRoot 'kc-admin'
        [System.IO.File]::WriteAllText($script:kcCredential, $script:kcAdmin)
        $script:kcAnswers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18456'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $script:kcRoot 'install'); secretsDir = (Join-Path $script:kcRoot 'secrets'); manualMigrations = $false
            postgres = @{ mode = 'provision' }
            keycloak = [ordered]@{ mode = 'existing'; url = $script:kcUrl; realm = 'dokpod'; caCertificate = $script:kcCa; admin = [ordered]@{ username = 'admin' }; administratorUser = 'ana' }
            edge = @{ mode = 'provision'; httpsPort = 18456 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        # Chave por índice: o par chave=valor de senha não aparece no texto (T056).
        $script:kcAnswers.keycloak.admin['password'] = @{ file = $script:kcCredential }
        $script:kcImages = [ordered]@{ api = $env:DOKPOD_TEST_API_IMAGE; bff = $env:DOKPOD_TEST_BFF_IMAGE; web = $env:DOKPOD_TEST_WEB_IMAGE; agent = $env:DOKPOD_TEST_AGENT_IMAGE }
        $script:kcInfra = $kcInfra
        $script:kcConfirmations = @{ Plan = $true }
        $script:kcInstall = {
            Invoke-InstallerInstall -Answers $script:kcAnswers -Images $script:kcImages -Infrastructure $script:kcInfra -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
                -Confirmations $script:kcConfirmations -Interactive $false -Project $script:kcProject -WaitTimeoutSeconds 420
        }
        $declined = try { & $script:kcInstall } catch { $_.Exception }
        $script:kcConfirmations['MigrationsPlanHash'] = [regex]::Match($declined.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        $script:kcFirst = & $script:kcInstall
        $script:kcEdgeCa = Join-Path $script:kcAnswers.installDir 'certs/ca.crt'
        $script:kcCurl = {
            param([string] $path, [string[]] $extra = @())
            & docker run --rm --pids-limit 512 --log-driver none --network "$($script:kcProject)_dokpod-edge" --volume "$($script:kcEdgeCa):/ca.crt:ro" --entrypoint curl $script:kcInfra.nginx `
                --silent --cacert /ca.crt @extra "https://dokpod.lab.test:18456$path"
        }
    }

    AfterAll {
        if (-not $script:kcEnabled) { return }
        Remove-TestComposeProject -Project $script:kcProject
        & docker rm --force --volumes $script:kcServer 2>$null | Out-Null
        Remove-Item -LiteralPath $script:kcRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'instala sem Keycloak próprio, e o login do BFF vai ao Keycloak do usuário pela TLS da CA privada' {
        if (-not $script:kcEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou DOKPOD_TEST_EXISTING_PG_HOST não está ativo'; return }
        $script:kcFirst | Should -Be 0
        $lines = & docker compose --file (Join-Path $script:kcAnswers.installDir 'compose.yaml') --project-name $script:kcProject ps --all --format json
        $services = @($lines | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
        ($services.Service | Sort-Object) | Should -Be @('agent', 'api', 'bff', 'dokpod-postgres', 'edge', 'web')
        # O BFF só monta o redirect depois de ler o discovery do Authority: sem confiar na CA privada, seria 500.
        $location = & $script:kcCurl '/dokpod/bff/login' @('--output', '/dev/null', '--write-out', '%{redirect_url}')
        $location | Should -Match ('^' + [regex]::Escape("$($script:kcUrl)/realms/dokpod/protocol/openid-connect/auth?"))
        [uri]::UnescapeDataString($location) | Should -Match 'redirect_uri=https://dokpod\.lab\.test:18456/dokpod/signin-oidc'
    }

    It 'ana recebe as roles do Dokpod, o master e o outro não mudam, e a credencial do master não fica em lugar nenhum' {
        if (-not $script:kcEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou DOKPOD_TEST_EXISTING_PG_HOST não está ativo'; return }
        $user = (& $script:kcadm @('get', 'users', '-r', 'dokpod', '-q', 'username=ana', '-q', 'exact=true')).Output | ConvertFrom-Json
        ((& $script:kcadm @('get', "users/$($user.id)/groups", '-r', 'dokpod')).Output | ConvertFrom-Json).path | Should -Contain '/dokpod/administrators'
        & $script:kcSnapshot | Should -Be $script:kcBefore
        foreach ($directory in $script:kcAnswers.installDir, $script:kcAnswers.secretsDir) {
            foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
                [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($file.FullName)) | Should -Not -Match ([regex]::Escape($script:kcAdmin)) -Because $file.FullName
            }
        }
    }

    It 'redirect URI alterado à mão: o plano mostra a divergência, recusa sem --yes e a corrige com ele (T040)' {
        if (-not $script:kcEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou DOKPOD_TEST_EXISTING_PG_HOST não está ativo'; return }
        $bffId = ((& $script:kcadm @('get', 'clients', '-r', 'dokpod', '-q', 'clientId=dokpod-bff', '--fields', 'id')).Output | ConvertFrom-Json).id
        (& $script:kcadm @('update', "clients/$bffId", '-r', 'dokpod', '-s', 'redirectUris=["https://mudado.lab.test/signin-oidc"]')).ExitCode | Should -Be 0
        # Reexecução sem a credencial do master: compara e corrige com o dokpod-provisioner.
        Remove-Item -LiteralPath $script:kcCredential

        $script:kcConfirmations.Remove('Plan')
        $writer = [System.IO.StringWriter]::new()
        $previous = [Console]::Out
        [Console]::SetOut($writer)
        try { $declined = try { & $script:kcInstall } catch { $_.Exception } }
        finally { [Console]::SetOut($previous) }
        $declined.ExitCode | Should -Be (Get-InstallerExitCode -Name Declined)
        $writer.ToString() | Should -Match 'realm.*update.*client dokpod-bff: redirectUris'
        ((& $script:kcadm @('get', "clients/$bffId", '-r', 'dokpod')).Output | ConvertFrom-Json).redirectUris | Should -Be @('https://mudado.lab.test/signin-oidc')

        $script:kcConfirmations['Plan'] = $true
        & $script:kcInstall | Should -Be 0
        ((& $script:kcadm @('get', "clients/$bffId", '-r', 'dokpod')).Output | ConvertFrom-Json).redirectUris | Should -Be @('https://dokpod.lab.test:18456/dokpod/signin-oidc')
        & $script:kcSnapshot | Should -Be $script:kcBefore
    }
}

# Spec 033 T042 (FR-029): recriação por item numa instalação completa. Só o item pedido muda.
Describe 'Recriação por item (recreate)' {
    BeforeAll {
        $script:recreateEnabled = $script:enabled
        if (-not $script:recreateEnabled) { return }
        $script:rcProject = 'dokpod-t042'
        $script:rcRoot = Join-Path $env:DOKPOD_TEST_SHARED "t042-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $script:rcInfra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images
        $script:rcImages = [ordered]@{ api = $env:DOKPOD_TEST_API_IMAGE; bff = $env:DOKPOD_TEST_BFF_IMAGE; web = $env:DOKPOD_TEST_WEB_IMAGE; agent = $env:DOKPOD_TEST_AGENT_IMAGE }
        $script:rcAnswers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18457'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $script:rcRoot 'install'); secretsDir = (Join-Path $script:rcRoot 'secrets'); manualMigrations = $false
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 18457 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        $script:rcConfirmations = @{ Plan = $true }
        $script:rcRun = {
            param([string[]] $Recreate = @(), [switch] $All)
            Invoke-InstallerInstall -Answers $script:rcAnswers -Images $script:rcImages -Infrastructure $script:rcInfra -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
                -Confirmations $script:rcConfirmations -Interactive $false -Project $script:rcProject -WaitTimeoutSeconds 420 -Recreate $Recreate -RecreateAll:$All
        }
        $declined = try { & $script:rcRun } catch { $_.Exception }
        $script:rcConfirmations['MigrationsPlanHash'] = [regex]::Match($declined.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        $script:rcFirst = & $script:rcRun

        $script:rcContainers = {
            $map = [ordered]@{}
            foreach ($line in @(& docker ps --filter "label=com.docker.compose.project=$($script:rcProject)" --format '{{.Label "com.docker.compose.service"}}={{.ID}}')) {
                $name, $id = $line -split '=', 2
                $map[$name] = $id
            }
            return $map
        }
        $script:rcChanged = {
            param($before, $after)
            return @($after.Keys | Where-Object { $before[$_] -ne $after[$_] } | Sort-Object)
        }
        $script:rcSql = {
            param([string] $sql)
            (& docker exec "$($script:rcProject)-dokpod-postgres-1" psql -At -U dokpod_admin -d dokpod -c $sql) -join "`n"
        }
        $script:rcCurl = {
            param([string] $path, [string[]] $extra = @())
            $ca = Join-Path $script:rcAnswers.installDir 'certs/ca.crt'
            & docker run --rm --pids-limit 512 --log-driver none --network "$($script:rcProject)_dokpod-edge" --volume "$($ca):/ca.crt:ro" --entrypoint curl $script:rcInfra.nginx `
                --silent --cacert /ca.crt @extra "https://dokpod.lab.test:18457$path"
        }
    }

    AfterAll {
        if (-not $script:recreateEnabled) { return }
        Remove-TestComposeProject -Project $script:rcProject
        Remove-Item -LiteralPath $script:rcRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'recreate pki:edge troca só o certificado da borda e o container edge' {
        if (-not $script:recreateEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $script:rcFirst | Should -Be 0
        $certificate = { [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([System.IO.File]::ReadAllText((Join-Path $script:rcAnswers.secretsDir 'pki/edge.crt'))).Thumbprint }
        $edgeBefore = & $certificate
        $apiBefore = [System.IO.File]::ReadAllBytes((Join-Path $script:rcAnswers.secretsDir 'pki/api.pfx'))
        $before = & $script:rcContainers
        & $script:rcRun @('pki:edge') | Should -Be 0
        $after = & $script:rcContainers
        & $script:rcChanged $before $after | Should -Be @('edge')
        & $certificate | Should -Not -Be $edgeBefore
        [System.IO.File]::ReadAllBytes((Join-Path $script:rcAnswers.secretsDir 'pki/api.pfx')) | Should -Be $apiBefore
        & $script:rcCurl '/dokpod/' @('--output', '/dev/null', '--write-out', '%{http_code}') | Should -Be '200'
    }

    It 'recreate service:web recria só o web, e a borda volta a encaminhar para ele' {
        if (-not $script:recreateEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $before = & $script:rcContainers
        & $script:rcRun @('service:web') | Should -Be 0
        $after = & $script:rcContainers
        # A borda guarda o IP dos upstreams: é reiniciada (mesmo container) quando um deles é recriado.
        & $script:rcChanged $before $after | Should -Be @('web')
        & $script:rcCurl '/dokpod/' @('--output', '/dev/null', '--write-out', '%{http_code}') | Should -Be '200'
    }

    It 'recreate secret:Authentication__Keycloak__ClientSecret troca o segredo no realm e no BFF, e o login continua' {
        if (-not $script:recreateEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $secretBefore = Read-InstallerSecret -SecretsDirectory $script:rcAnswers.secretsDir -Name 'Authentication__Keycloak__ClientSecret'
        $before = & $script:rcContainers
        & $script:rcRun @('secret:Authentication__Keycloak__ClientSecret') | Should -Be 0
        $after = & $script:rcContainers
        & $script:rcChanged $before $after | Should -Be @('bff')
        Read-InstallerSecret -SecretsDirectory $script:rcAnswers.secretsDir -Name 'Authentication__Keycloak__ClientSecret' | Should -Not -Be $secretBefore
        & $script:rcCurl '/dokpod/bff/login' @('--output', '/dev/null', '--write-out', '%{http_code}') | Should -Match '^30[23]$'
    }

    It 'recreate pki:agent:<id> reemite o agente, revoga a identidade antiga e registra a nova' {
        if (-not $script:recreateEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $state = Open-InstallerState -InstallDirectory $script:rcAnswers.installDir
        try { $environment = (Invoke-StateSql -State $state -Name 'environments-list' -Rows)[0] }
        finally { Close-InstallerState -State $state }
        $id = $environment['environment_id']
        $old = $environment['agent_fingerprint']
        $before = & $script:rcContainers
        & $script:rcRun @("pki:agent:$id") | Should -Be 0
        $after = & $script:rcContainers
        & $script:rcChanged $before $after | Should -Be @('agent')
        $state = Open-InstallerState -InstallDirectory $script:rcAnswers.installDir
        try { $new = (Invoke-StateSql -State $state -Name 'environments-list' -Rows)[0]['agent_fingerprint'] }
        finally { Close-InstallerState -State $state }
        $new | Should -Not -Be $old
        $rows = & $script:rcSql "SELECT upper(certificate_fingerprint) || ':' || (revoked_at_utc IS NOT NULL) FROM dokpod.agent_identities WHERE environment_id = '$id' ORDER BY 1"
        $rows | Should -Match ([regex]::Escape("$($old.ToUpperInvariant()):true"))
        $rows | Should -Match ([regex]::Escape("$($new.ToUpperInvariant()):false"))
    }

    It 'componente do usuário e item desconhecido são recusados com código 2, sem mudar nada' {
        if (-not $script:recreateEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $before = & $script:rcContainers
        $failure = try { & $script:rcRun @('secret:dokpod-db-password') } catch { $_.Exception }
        $failure.ExitCode | Should -Be 2
        @((& $script:rcContainers).Values) -join ',' | Should -Be (@($before.Values) -join ',')
    }
    It 'recreate --all sem a segunda confirmação recria os serviços e preserva banco, login, key rings e identidades (SC-008)' {
        if (-not $script:recreateEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $counts = { & $script:rcSql "SELECT (SELECT count(*) FROM dokpod.audit_events) || ',' || (SELECT count(*) FROM dokpod.agent_identities)" }
        $countsBefore = & $counts
        $before = & $script:rcContainers
        & $script:rcRun -All | Should -Be 0
        $after = & $script:rcContainers
        & $script:rcChanged $before $after | Should -Be @('agent', 'api', 'bff', 'edge', 'keycloak', 'web')
        & $counts | Should -Be $countsBefore
        & $script:rcCurl '/dokpod/bff/login' @('--output', '/dev/null', '--write-out', '%{http_code}') | Should -Match '^30[23]$'
    }

    It 'lista de dados diferente da exata dá código 2 sem efeito; com a exata, os dados saem e a instalação volta do zero' {
        if (-not $script:recreateEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $before = & $script:rcContainers
        $script:rcConfirmations['DataRemoval'] = @('dokpod-postgres')
        $failure = try { & $script:rcRun -All } catch { $_.Exception }
        $failure.ExitCode | Should -Be 2
        @((& $script:rcContainers).Values) -join ',' | Should -Be (@($before.Values) -join ',')

        $auditBefore = [int] (& $script:rcSql 'SELECT count(*) FROM dokpod.audit_events')
        $script:rcConfirmations['DataRemoval'] = @('dokpod-postgres', 'keycloak-postgres', 'dokpod-api-keys', 'dokpod-bff-keys', 'agent-identities')
        # O banco volta vazio: as migrations pedem de novo a confirmação do planHash (FR-036), e a retomada é um install.
        $paused = try { & $script:rcRun -All } catch { $_.Exception }
        $script:rcConfirmations.Remove('DataRemoval')
        $paused.ExitCode | Should -Be 4
        $script:rcConfirmations['MigrationsPlanHash'] = [regex]::Match($paused.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        & $script:rcRun | Should -Be 0

        [int] (& $script:rcSql 'SELECT count(*) FROM dokpod.audit_events') | Should -BeLessThan $auditBefore
        $after = & $script:rcContainers
        & $script:rcChanged $before $after | Should -Be @('agent', 'api', 'bff', 'dokpod-postgres', 'edge', 'keycloak', 'keycloak-postgres', 'web')
        # O realm saiu com o banco do Keycloak: o primeiro administrador volta com senha temporária nova, e o bootstrap sai.
        Test-InstallerSecret -SecretsDirectory $script:rcAnswers.secretsDir -Name 'initial-admin-password' | Should -BeTrue
        [System.IO.File]::ReadAllText((Join-Path $script:rcAnswers.secretsDir 'keycloak-bootstrap-admin')) | Should -BeNullOrEmpty
        & $script:rcCurl '/dokpod/bff/login' @('--output', '/dev/null', '--write-out', '%{http_code}') | Should -Match '^30[23]$'
    }
    It 'recreate pki:ca passa pelas duas raízes, reemite API, borda e agente, espera a reconexão e termina só com a raiz nova (T044)' {
        if (-not $script:recreateEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $read = { param([string] $path) [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([System.IO.File]::ReadAllText($path)) }
        $oldCa = (& $read (Join-Path $script:rcAnswers.secretsDir 'pki/ca.crt')).Thumbprint
        $parameters = @{
            Answers = $script:rcAnswers; Images = $script:rcImages; Infrastructure = $script:rcInfra; InstallerImage = $env:DOKPOD_INSTALLER_IMAGE_REF
            Confirmations = $script:rcConfirmations; Interactive = $false; Project = $script:rcProject; WaitTimeoutSeconds = 420
        }
        Invoke-InstallerCaRotation -Parameters $parameters | Should -Be 0

        $newCa = & $read (Join-Path $script:rcAnswers.secretsDir 'pki/ca.crt')
        $newCa.Thumbprint | Should -Not -Be $oldCa
        Test-InstallerSecret -SecretsDirectory $script:rcAnswers.secretsDir -Name 'pki/ca-previous.crt' | Should -BeFalse
        foreach ($file in 'certs/agents-ca.crt', 'certs/ca.crt') {
            $pem = [System.IO.File]::ReadAllText((Join-Path $script:rcAnswers.installDir $file))
            ([regex]::Matches($pem, 'BEGIN CERTIFICATE')).Count | Should -Be 1 -Because $file
            (& $read (Join-Path $script:rcAnswers.installDir $file)).Thumbprint | Should -Be $newCa.Thumbprint -Because $file
        }
        (& $read (Join-Path $script:rcAnswers.secretsDir 'pki/edge.crt')).Issuer | Should -Be $newCa.Subject
        $state = Open-InstallerState -InstallDirectory $script:rcAnswers.installDir
        try {
            (Invoke-StateSql -State $state -Name 'certificates-list' -Rows | Where-Object { $_['role'] -eq 'ca' })['ca_state'] | Should -Be 'active'
            $environment = (Invoke-StateSql -State $state -Name 'environments-list' -Rows)[0]
        }
        finally { Close-InstallerState -State $state }
        $active = & $script:rcSql "SELECT upper(certificate_fingerprint) FROM dokpod.agent_identities WHERE environment_id = '$($environment['environment_id'])' AND revoked_at_utc IS NULL"
        $active | Should -Be $environment['agent_fingerprint'].ToUpperInvariant()
        & $script:rcSql "SELECT connected FROM dokpod.agent_connections WHERE environment_id = '$($environment['environment_id'])'" | Should -Be 't'
        & $script:rcCurl '/dokpod/' @('--output', '/dev/null', '--write-out', '%{http_code}') | Should -Be '200'
    }
}

# Spec 033 T046 e T047 (FR-025, FR-022): host remoto pelo pacote, com o agente no mesmo engine num projeto próprio, e a
# revogação. Só no Podman: o agente chega ao gRPC publicado pelo host-gateway.
Describe 'Host remoto (host add e host revoke)' {
    BeforeAll {
        $script:hostEnabled = $script:enabled -and $env:DOKPOD_HOST_ENGINE -eq 'podman'
        if (-not $script:hostEnabled) { return }
        # Os nomes dos projetos antes de qualquer coisa que possa falhar: o AfterAll limpa por eles.
        $script:hoProject = 'dokpod-t046'
        $script:hoAgentProject = 'dokpod-t046-host-b'
        $script:hoRoot = Join-Path $env:DOKPOD_TEST_SHARED "t046-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $script:hoInfra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images
        $script:hoImages = [ordered]@{ api = $env:DOKPOD_TEST_API_IMAGE; bff = $env:DOKPOD_TEST_BFF_IMAGE; web = $env:DOKPOD_TEST_WEB_IMAGE; agent = $env:DOKPOD_TEST_AGENT_IMAGE }
        $script:hoAnswers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18458'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $script:hoRoot 'install'); secretsDir = (Join-Path $script:hoRoot 'secrets'); manualMigrations = $false; agentGrpcPort = 17458
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 18458 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        $confirmations = @{ Plan = $true }
        $run = { Invoke-InstallerInstall -Answers $script:hoAnswers -Images $script:hoImages -Infrastructure $script:hoInfra -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
                -Confirmations $confirmations -Interactive $false -Project $script:hoProject -WaitTimeoutSeconds 420 }
        $declined = try { & $run } catch { $_.Exception }
        $confirmations['MigrationsPlanHash'] = [regex]::Match($declined.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        $script:hoFirst = & $run
        $script:hoSql = { param([string] $sql) (& docker exec "$($script:hoProject)-dokpod-postgres-1" psql -At -U dokpod_admin -d dokpod -c $sql) -join "`n" }
        $script:hoAgentDir = Join-Path $script:hoRoot 'host-b'
    }

    AfterAll {
        if (-not $script:hostEnabled) { return }
        foreach ($project in @($script:hoAgentProject, $script:hoProject) | Where-Object { $_ }) {
            Remove-TestComposeProject -Project $project
        }
        Remove-Item -LiteralPath $script:hoRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'host add gera o pacote 0600 cifrado, sem outro segredo, e o agente do pacote conecta em até 5 minutos' {
        if (-not $script:hostEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou o engine Podman não está ativo'; return }
        $script:hoFirst | Should -Be 0
        Invoke-InstallerHostCommand -Answers $script:hoAnswers -Action add -Name 'host-b' -Engine podman -Images $script:hoImages `
            -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF -Project $script:hoProject -Interactive $false | Should -Be 0

        $state = Open-InstallerState -InstallDirectory $script:hoAnswers.installDir
        try { $rows = Invoke-StateSql -State $state -Name 'environments-list' -Rows }
        finally { Close-InstallerState -State $state }
        $row = $rows | Where-Object { $_['name'] -eq 'host-b' }
        $script:hoId = $row['environment_id']
        $row['package_generated_at'] | Should -Not -BeNullOrEmpty
        $package = Join-Path $script:hoAnswers.installDir "packages/$($script:hoId).tar"
        [System.IO.File]::GetUnixFileMode($package) | Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite')

        # Abre como o README manda: o tar de fora, o SHA-256 e a cifra com a frase entregue à parte.
        $outer = [ordered]@{}
        $reader = [System.Formats.Tar.TarReader]::new([System.IO.File]::OpenRead($package))
        try { while ($null -ne ($entry = $reader.GetNextEntry())) { $buffer = [System.IO.MemoryStream]::new(); $entry.DataStream.CopyTo($buffer); $outer[$entry.Name] = $buffer.ToArray() } }
        finally { $reader.Dispose() }
        @($outer.Keys) | Should -Be @('README.txt', 'agent.tar.enc', 'agent.tar.enc.sha256')
        [System.Text.Encoding]::UTF8.GetString($outer['agent.tar.enc.sha256']) | Should -Match ([System.Convert]::ToHexStringLower([System.Security.Cryptography.SHA256]::HashData($outer['agent.tar.enc'])))
        $passphrase = Read-InstallerSecret -SecretsDirectory $script:hoAnswers.secretsDir -Name "packages/$($script:hoId).passphrase"
        $inner = Unprotect-HostPackage -Bytes $outer['agent.tar.enc'] -Passphrase $passphrase
        New-Item -ItemType Directory -Path $script:hoAgentDir | Out-Null
        $reader = [System.Formats.Tar.TarReader]::new([System.IO.MemoryStream]::new($inner))
        try { while ($null -ne ($entry = $reader.GetNextEntry())) { $entry.ExtractToFile((Join-Path $script:hoAgentDir $entry.Name), $false) } }
        finally { $reader.Dispose() }
        (Get-ChildItem -LiteralPath $script:hoAgentDir).Name | Sort-Object | Should -Be @('README.txt', 'agent.env', 'agent.pass', 'agent.pfx', 'ca.crt', 'compose.agent.yaml')
        foreach ($name in 'ca.key', 'Authentication__Keycloak__ClientSecret', 'dokpod-db-password') {
            $value = [System.IO.File]::ReadAllText((Resolve-SecretPath -SecretsDirectory $script:hoAnswers.secretsDir -Name $(if ($name -eq 'ca.key') { 'pki/ca.key' } else { $name })))
            foreach ($file in Get-ChildItem -LiteralPath $script:hoAgentDir) { [System.IO.File]::ReadAllText($file.FullName) | Should -Not -Match ([regex]::Escape($value.Trim())) }
        }

        # O agente do "host b": o mesmo engine, outro projeto; o nome público aponta para o host, onde a API publica o gRPC.
        [System.IO.File]::WriteAllText((Join-Path $script:hoAgentDir 'override.yaml'), "services:`n  agent:`n    extra_hosts:`n      - dokpod.lab.test:host-gateway`n")
        $env:DOKPOD_ENGINE_SOCKET = $env:DOKPOD_HOST_ENGINE_SOCKET
        try {
            & docker compose --project-directory $script:hoAgentDir -f (Join-Path $script:hoAgentDir 'compose.agent.yaml') -f (Join-Path $script:hoAgentDir 'override.yaml') `
                --project-name $script:hoAgentProject up --detach 2>&1 | Out-Null
            $LASTEXITCODE | Should -Be 0
        }
        finally { Remove-Item Env:DOKPOD_ENGINE_SOCKET }
        $deadline = [datetime]::UtcNow.AddMinutes(5)
        do { Start-Sleep -Seconds 5; $connected = & $script:hoSql "SELECT connected FROM dokpod.agent_connections WHERE environment_id = '$($script:hoId)'" }
        until ($connected -eq 't' -or [datetime]::UtcNow -gt $deadline)
        $connected | Should -Be 't'
    }

    It 'host revoke derruba a sessão, registra a auditoria e marca o pacote como revogado' {
        if (-not $script:hostEnabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL ou o engine Podman não está ativo'; return }
        Invoke-InstallerHostCommand -Answers $script:hoAnswers -Action revoke -EnvironmentId $script:hoId -Reason 'teste 033' -Images $script:hoImages `
            -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF -Project $script:hoProject -Interactive $false | Should -Be 0
        $deadline = [datetime]::UtcNow.AddMinutes(2)
        do { Start-Sleep -Seconds 3; $connected = & $script:hoSql "SELECT connected FROM dokpod.agent_connections WHERE environment_id = '$($script:hoId)'" }
        until ($connected -eq 'f' -or [datetime]::UtcNow -gt $deadline)
        $connected | Should -Be 'f'
        & $script:hoSql "SELECT count(*) FROM dokpod.audit_events WHERE action = 'environment.revoke' AND environment_id = '$($script:hoId)'" | Should -Not -Be '0'
        $state = Open-InstallerState -InstallDirectory $script:hoAnswers.installDir
        try { $rows = Invoke-StateSql -State $state -Name 'environments-list' -Rows }
        finally { Close-InstallerState -State $state }
        ($rows | Where-Object { $_['environment_id'] -eq $script:hoId })['revoked_at'] | Should -Not -BeNullOrEmpty
    }
}
