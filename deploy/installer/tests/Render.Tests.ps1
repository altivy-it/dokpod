#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T026 (FR-020 a FR-023, FR-043, D-006, D-008, L1): compose.yaml de produção, segredos só por arquivo, borda,
# deploy.env do hook e redes confiáveis do BFF.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Render.psm1') -Force

    $script:images = [ordered]@{
        api   = "ghcr.io/altivy-it/dokpod-ce-api@sha256:$('1' * 64)"
        bff   = "ghcr.io/altivy-it/dokpod-ce-bff@sha256:$('2' * 64)"
        web   = "ghcr.io/altivy-it/dokpod-ce-web@sha256:$('3' * 64)"
        agent = "ghcr.io/altivy-it/dokpod-ce-agent@sha256:$('4' * 64)"
    }
    $script:infra = [ordered]@{
        postgresql = "docker.io/lzocateli/postgresql:18@sha256:$('5' * 64)"; keycloak = "docker.io/lzocateli/keycloak:26@sha256:$('6' * 64)"
        nginx = "docker.io/lzocateli/nginx:1.28@sha256:$('7' * 64)"; cosign = "ghcr.io/sigstore/cosign/cosign:v3@sha256:$('8' * 64)"
    }
    $script:realNginx = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json).images.nginx
    $script:environmentId = '7d6c1f0e-2b44-4a55-9c3f-1b2a3c4d5e6f'

    function New-Answers {
        param([hashtable] $Override = @{})
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-render-$([guid]::NewGuid().ToString('N'))"
        $answers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:8443'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $root 'install'); secretsDir = (Join-Path $root 'secrets'); manualMigrations = $false
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 8443 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        foreach ($key in $Override.Keys) { $answers[$key] = $Override[$key] }
        return $answers
    }

    function New-Model {
        param($Answers)
        return New-InstallerComposeModel -Answers $Answers -Images $script:images -Infrastructure $script:infra -EngineSocket '/run/user/1000/podman/podman.sock' -LocalEnvironmentId $script:environmentId
    }

    function Test-ComposeConfig {
        param($Answers, $Model)
        $Model.secrets.Values | ForEach-Object { New-Item -ItemType File -Path $_.file -Force | Out-Null }
        Write-InstallerFiles -Answers $Answers -Model $Model -CaBundle (New-CaBundle) -LocalCaPem "-----BEGIN CERTIFICATE-----`n" -AgentsCaPem "-----BEGIN CERTIFICATE-----`n" | Out-Null
        $output = & docker compose --project-directory $Answers.installDir -f (Join-Path $Answers.installDir 'compose.yaml') config --quiet 2>&1
        return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }
}

Describe 'compose.yaml com tudo provisionado' {
    BeforeAll {
        $script:answers = New-Answers
        $script:model = New-Model $script:answers
        $script:yaml = ConvertTo-ComposeYaml -Model $script:model
    }

    It 'passa no docker compose config' {
        $result = Test-ComposeConfig -Answers $script:answers -Model $script:model
        $result.Output | Should -BeNullOrEmpty
        $result.ExitCode | Should -Be 0
    }

    It 'roda em produção e sem IP fixo' {
        $script:model.services.api.environment.ASPNETCORE_ENVIRONMENT | Should -Be 'Production'
        $script:model.services.bff.environment.ASPNETCORE_ENVIRONMENT | Should -Be 'Production'
        $script:yaml | Should -Not -Match 'ipv4_address'
        $script:yaml | Should -Not -Match 'Development'
    }

    It 'só a borda publica porta, e o gRPC dos agentes só com agentGrpcPort' {
        $published = @($script:model.services.Keys | Where-Object { $script:model.services[$_].Contains('ports') })
        $published | Should -Be @('edge')

        $withAgents = New-Model (New-Answers @{ agentGrpcPort = 17443 })
        $withAgents.services.api.ports[0].published | Should -Be '17443'
        $withAgents.services.api.ports[0].target | Should -Be 7443
    }

    It 'segredos só por arquivo em secrets:, nunca em environment' {
        $script:model.services.api.secrets.target | Should -Contain '/run/secrets/ConnectionStrings__ControlPlane'
        $script:model.services.bff.secrets.target | Should -Contain '/run/secrets/Authentication__Keycloak__ClientSecret'
        foreach ($service in $script:model.services.Values) {
            foreach ($key in @($service.environment?.Keys)) {
                $key | Should -Not -Match '(?i)(password|secret|connection)$'
            }
        }
        $script:model.secrets.Values.file | ForEach-Object { $_ | Should -Match ([regex]::Escape($script:answers.secretsDir)) }
        $script:yaml | Should -Match 'read -r KC_DB_PASSWORD < /run/secrets/keycloak-db-password'
    }

    It 'o socket do engine só é montado no agente' {
        foreach ($name in $script:model.services.Keys) {
            $mounts = @($script:model.services[$name].volumes) -join ' '
            if ($name -eq 'agent') { $mounts | Should -Match 'podman.sock:/run/engine.sock' }
            else { $mounts | Should -Not -Match '\.sock' }
        }
    }

    It 'a borda responde pelo nome público dentro da rede interna' {
        $script:model.services.edge.networks['dokpod-edge'].aliases | Should -Be @('dokpod.lab.test')
        $script:model.services.api.environment.Authentication__Keycloak__Authority | Should -Be 'https://dokpod.lab.test:8443/realms/dokpod'
    }

    It 'API e BFF confiam no bundle de CAs (D-008)' {
        $script:model.services.api.environment.SSL_CERT_FILE | Should -Be '/certs/ca-bundle.crt'
        $script:model.services.bff.environment.SSL_CERT_FILE | Should -Be '/certs/ca-bundle.crt'
    }

    It 'o BFF só confia em X-Forwarded-* da rede da borda (L1)' {
        $script:model.services.bff.environment['Security__TrustedNetworks__0'] | Should -Be '10.253.33.0/24'
        $script:model.services.bff.environment.Contains('Security__TrustedNetworks__1') | Should -BeFalse
    }

    It 'deploy.env no formato do hook' {
        $deploy = New-DeployEnvironment -Answers $script:answers -Model $script:model
        $deploy | Should -Match "COMPOSE_DIR=$([regex]::Escape($script:answers.installDir))"
        $deploy | Should -Match 'SERVICES="dokpod-ce-api=api dokpod-ce-bff=bff dokpod-ce-web=web dokpod-ce-agent=agent"'
        $deploy | Should -Match 'ORDER="bff web agent api"'
        foreach ($key in 'COMPOSE_FILE', 'ENV_FILE', 'PROJECT') { $deploy | Should -Match "(?m)^$key=" }
    }

    It 'Business usa a imagem da API Business no deploy.env' {
        New-DeployEnvironment -Answers (New-Answers @{ edition = 'business' }) -Model $script:model | Should -Match 'dokpod-api-business=api'
    }

    It 'a borda tem /dokpod/, BFF com SignalR e as rotas do Keycloak provisionado' {
        $edge = New-EdgeConfiguration -Answers $script:answers
        $edge['dokpod.locations'] | Should -Match 'proxy_pass http://bff:8080;'
        $edge['dokpod.locations'] | Should -Match 'Upgrade \$http_upgrade'
        $edge['dokpod.locations'] | Should -Match 'X-Forwarded-Port 8443'
        $edge['dokpod.locations'] | Should -Match '\^/\(realms\|resources\)/'
        $edge['dokpod.conf'] | Should -Match 'server_name dokpod.lab.test;'
        $edge['dokpod.conf'] | Should -Match 'listen 8443 ssl;'
        $edge['dokpod.conf'] | Should -Not -Match 'listen 443'
    }

    It 'com porta pública diferente de 8443, a borda também escuta nela para o tráfego interno (D-008)' {
        $edge = New-EdgeConfiguration -Answers (New-Answers @{ publicUrl = 'https://dokpod.lab.test' })
        $edge['dokpod.conf'] | Should -Match '(?m)^\s+listen 8443 ssl;'
        $edge['dokpod.conf'] | Should -Match '(?m)^\s+listen 443 ssl;'
    }

    It 'produção sem HTTP para o Keycloak na API e com o certificado do key ring no BFF (T032)' {
        $script:model.services['api'].environment.Contains('Authentication__Keycloak__BackchannelAuthority') | Should -BeFalse
        $script:model.services['bff'].environment['Security__DataProtectionCertificatePath'] | Should -Be '/certs/bff-dataprotection.pfx'
        $targets = @($script:model.services['bff'].secrets | ForEach-Object { $_.target })
        $targets | Should -Contain '/certs/bff-dataprotection.pfx'
        $targets | Should -Contain '/run/secrets/Security__DataProtectionCertificatePassword'
        $script:yaml | Should -Not -Match 'http://keycloak'
        $script:model.services['api'].environment['DOKPOD_API_HTTPS_PORT'] | Should -Be '8443'
        $script:model.services['bff'].environment['Downstream__Api__BaseUrl'] | Should -Be 'https://api:8443'
        $script:model.services['bff'].environment['SSL_CERT_FILE'] | Should -Be '/certs/ca-bundle.crt'
        $script:model.services['bff'].environment['AllowedHosts'] | Should -Be 'dokpod.lab.test;localhost'
    }

    It 'Keycloak e borda têm healthcheck, para a subida ordenada esperar por eles (T032)' {
        $script:model.services['keycloak'].healthcheck.test -join ' ' | Should -Match '/health/ready'
        $script:model.services['keycloak'].healthcheck.test -join ' ' | Should -Match '\$\$status'
        $script:model.services['edge'].healthcheck.test -join ' ' | Should -Match 'nginx-healthz'
    }

    It 'dokpod.env não tem segredos' {
        Register-InstallerSecret -Value 'canario-033-render'
        New-DokpodEnvironmentFile -Answers $script:answers | Should -Not -Match 'canario'
        New-DokpodEnvironmentFile -Answers $script:answers | Should -Match 'DOKPOD_VERSION=0.8.0'
    }
}

Describe 'nginx -t da borda' {
    # Precisa do socket do engine montado no container de testes (DOCKER_HOST); sem ele, o teste é pulado.
    It 'a configuração da borda provisionada, com o Keycloak, passa no nginx -t da imagem fixada' {
        if (-not $env:DOCKER_HOST) {
            Set-ItResult -Skipped -Because 'o socket do engine não foi montado'
            return
        }

        Import-Module (Join-Path $PSScriptRoot '../src/Pki.psm1') -Force
        # Porta pública 443: a borda escuta em 8443 e 443.
        $answers = New-Answers @{ publicUrl = 'https://dokpod.lab.test' }
        $edge = New-EdgeConfiguration -Answers $answers
        $edge['dokpod.conf'] | Should -Match 'listen 443 ssl;'
        $staging = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-nginx-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path (Join-Path $staging 'certs') -Force | Out-Null
        $ca = New-LocalCertificateAuthority
        $leaf = New-InstallerCertificate -Issuer $ca -CommonName 'dokpod.lab.test' -Usage Server -DnsNames 'dokpod.lab.test'
        [System.IO.File]::WriteAllText((Join-Path $staging 'certs/tls.crt'), (ConvertTo-CertificatePem -Certificate $leaf))
        [System.IO.File]::WriteAllText((Join-Path $staging 'certs/tls.key'), (ConvertTo-PrivateKeyPem -Certificate $leaf))
        foreach ($name in $edge.Keys) { [System.IO.File]::WriteAllText((Join-Path $staging $name), $edge[$name]) }

        $container = "dokpod-033-nginx-t-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        try {
            & docker create --name $container --log-driver none --add-host bff:127.0.0.1 --add-host web:127.0.0.1 --add-host keycloak:127.0.0.1 `
                $script:realNginx nginx -t | Out-Null
            $LASTEXITCODE | Should -Be 0
            & docker cp (Join-Path $staging 'certs') "$($container):/etc/nginx/" | Out-Null
            & docker cp (Join-Path $staging 'dokpod.conf') "$($container):/etc/nginx/conf.d/dokpod.conf" | Out-Null
            & docker cp (Join-Path $staging 'dokpod.locations') "$($container):/etc/nginx/conf.d/dokpod.locations" | Out-Null
            # Com --log-driver none o engine não devolve a saída; vale o código de saída do nginx -t.
            & docker start $container | Out-Null
            $exit = & docker wait $container
            [int] ($exit | Select-Object -Last 1) | Should -Be 0
        }
        finally {
            & docker rm --force $container 2>$null | Out-Null
            Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Infraestrutura existente' {
    It 'com nginx e PostgreSQL existentes não sobe edge nem bancos, e publica web e BFF só em loopback' {
        $answers = New-Answers @{
            edge     = @{ mode = 'existing'; upstreamHost = '127.0.0.1'; webPort = 18080; bffPort = 18081 }
            postgres = @{ mode = 'existing-prepared'; host = 'db.lab.test'; port = 5432; database = 'dokpod' }
            keycloak = @{ mode = 'existing'; url = 'https://sso.lab.test'; realm = 'dokpod'; caCertificate = '/tmp/ca.pem' }
            certificates = @{ edge = @{ source = 'organization' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
        }
        $model = New-Model $answers

        $model.services.Keys | Should -Not -Contain 'edge'
        $model.services.Keys | Should -Not -Contain 'dokpod-postgres'
        $model.services.Keys | Should -Not -Contain 'keycloak'
        $model.services.bff.ports[0].host_ip | Should -Be '127.0.0.1'
        $model.services.web.ports[0].published | Should -Be '18080'
        $model.services.bff.environment['Security__TrustedNetworks__0'] | Should -Be '127.0.0.1/32'
        $model.services.api.environment.Authentication__Keycloak__Authority | Should -Be 'https://sso.lab.test/realms/dokpod'
        $model.services.api.environment.SSL_CERT_FILE | Should -Be '/certs/ca-bundle.crt'

        $edge = New-EdgeConfiguration -Answers $answers
        $edge['dokpod.locations'] | Should -Match 'proxy_pass http://127.0.0.1:18081;'
        $edge['dokpod.locations'] | Should -Not -Match 'realms'
        $edge['dokpod.conf'] | Should -Match 'include .*/edge/dokpod.locations;'
        (Test-ComposeConfig -Answers $answers -Model $model).ExitCode | Should -Be 0
    }

    It 'com PostgreSQL existing-create a API sai pela dokpod-data até o servidor, sem dokpod-postgres, e confia na CA dele' {
        $provisioned = New-Model (New-Answers)
        $provisioned.networks['dokpod-data'].internal | Should -BeTrue

        $answers = New-Answers @{
            postgres = @{ mode = 'existing-create'; host = 'db.lab.test'; port = 5432; sslMode = 'verify-full'; caCertificate = '/tmp/pg-ca.pem'; admin = @{ username = 'dba' } }
            certificates = @{ edge = @{ source = 'organization' }; api = @{ source = 'organization' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            keycloak = @{ mode = 'existing'; url = 'https://sso.lab.test'; realm = 'dokpod' }
        }
        $model = New-Model $answers
        $model.services.Keys | Should -Not -Contain 'dokpod-postgres'
        $model.volumes.Keys | Should -Not -Contain 'dokpod-postgres-data'
        $model.services.api.Contains('depends_on') | Should -BeFalse
        $model.services.api.networks.Keys | Should -Contain 'dokpod-data'
        $model.networks['dokpod-data'].internal | Should -BeFalse
        # Só a CA do PostgreSQL já exige o bundle de SSL_CERT_FILE, que o Npgsql usa para validar a cadeia.
        Test-CaBundleNeeded -Answers $answers | Should -BeTrue
        $model.services.api.environment.SSL_CERT_FILE | Should -Be '/certs/ca-bundle.crt'
        $answers.postgres.Remove('caCertificate')
        Test-CaBundleNeeded -Answers $answers | Should -BeFalse
    }

    It 'recusa rede confiável maior que a rede interna (L1)' {
        InModuleScope Render { { Assert-TrustedNetworks -Networks '10.0.0.0/8' } | Should -Throw -ExpectedMessage '*maior que a rede interna*' }
    }

    It 'template com placeholder sem valor é erro' {
        { Expand-InstallerTemplate -Name 'deploy.env.template' -Values @{ INSTALL_DIR = '/x' } } | Should -Throw -ExpectedMessage '*SERVICES*'
    }
}
