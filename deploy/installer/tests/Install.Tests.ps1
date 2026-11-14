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
        $label = "label=com.docker.compose.project=$($script:project)"
        @(& docker ps -aq --filter $label) | ForEach-Object { & docker rm -f $_ 2>$null | Out-Null }
        @(& docker volume ls -q --filter $label) | ForEach-Object { & docker volume rm $_ 2>$null | Out-Null }
        @(& docker network ls -q --filter $label) | ForEach-Object { & docker network rm $_ 2>$null | Out-Null }
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
        $label = "label=com.docker.compose.project=$($script:existingProject)"
        & docker compose --file (Join-Path $script:existingAnswers.installDir 'compose.yaml') --project-name $script:existingProject down --volumes 2>$null | Out-Null
        @(& docker ps -aq --filter $label) | ForEach-Object { & docker rm -f $_ 2>$null | Out-Null }
        @(& docker volume ls -q --filter $label) | ForEach-Object { & docker volume rm $_ 2>$null | Out-Null }
        @(& docker network ls -q --filter $label) | ForEach-Object { & docker network rm $_ 2>$null | Out-Null }
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
