#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T037 (FR-021, FR-043, D-008, L1): nginx existente do usuário. Onde web e BFF escutam, em quem o BFF confia,
# o dokpod.conf validado com nginx -t em container e os passos manuais. O nginx -t e a rede usam o engine (DOCKER_HOST).

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Render.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Edge.psm1') -Force

    $script:images = [ordered]@{
        api   = "ghcr.io/altivy-it/dokpod-ce-api@sha256:$('1' * 64)"
        bff   = "ghcr.io/altivy-it/dokpod-ce-bff@sha256:$('2' * 64)"
        web   = "ghcr.io/altivy-it/dokpod-ce-web@sha256:$('3' * 64)"
        agent = "ghcr.io/altivy-it/dokpod-ce-agent@sha256:$('4' * 64)"
    }
    $script:infra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images

    function New-EdgeAnswers {
        param($Upstream = '127.0.0.1', [string] $Engine = 'podman-rootless', [hashtable] $Keycloak)
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-edge-$([guid]::NewGuid().ToString('N'))"
        $answers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test'; engine = $Engine; operator = 'Ana'
            installDir = (Join-Path $root 'install'); secretsDir = (Join-Path $root 'secrets'); manualMigrations = $false
            postgres = @{ mode = 'provision' }
            keycloak = if ($Keycloak) { $Keycloak } else { @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } } }
            edge = [ordered]@{ mode = 'existing'; upstreamHost = $Upstream; webPort = 18080; bffPort = 18081; keycloakPort = 18082 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        return $answers
    }

    function New-Model {
        param($Answers, [string[]] $Trusted = @())
        return New-InstallerComposeModel -Answers $Answers -Images $script:images -Infrastructure $script:infra -EngineSocket '/run/user/1000/podman/podman.sock' `
            -LocalEnvironmentId '7d6c1f0e-2b44-4a55-9c3f-1b2a3c4d5e6f' -TrustedNetworks $Trusted
    }

    function Get-Trusted {
        param($Model)
        $environment = $Model.services.bff.environment
        return @($environment.Keys | Where-Object { $_ -like 'Security__TrustedNetworks__*' } | Sort-Object | ForEach-Object { $environment[$_] })
    }
}

Describe 'Respostas do nginx existente (FR-043)' {
    It 'recusa upstreamHost fora de loopback com código 2: <Address>' -ForEach @(
        @{ Address = '0.0.0.0' }, @{ Address = '10.0.0.5' }, @{ Address = '::' }, @{ Address = '192.168.1.10' }, @{ Address = 'localhost' }
    ) {
        $failure = { Assert-ExistingEdgeAnswers -Answers (New-EdgeAnswers -Upstream $Address) } | Should -Throw -PassThru
        $failure.Exception.ExitCode | Should -Be 2
        $failure.Exception.Message | Should -Match 'não é loopback.*FR-043'
    }

    It 'aceita 127.0.0.1, ::1 e uma rede compartilhada; recusa nome de rede inválido e portas repetidas' {
        foreach ($upstream in '127.0.0.1', '::1', @{ network = 'nginx-dokpod' }) {
            { Assert-ExistingEdgeAnswers -Answers (New-EdgeAnswers -Upstream $upstream) } | Should -Not -Throw
        }
        $failure = { Assert-ExistingEdgeAnswers -Answers (New-EdgeAnswers -Upstream @{ network = '-x;rm' }) } | Should -Throw -PassThru
        $failure.Exception.ExitCode | Should -Be 2

        $answers = New-EdgeAnswers
        $answers.edge.keycloakPort = 18080
        $failure = { Assert-ExistingEdgeAnswers -Answers $answers } | Should -Throw -PassThru
        $failure.Exception.ExitCode | Should -Be 2
        $failure.Exception.Message | Should -Match 'precisam ser diferentes'
        # Sem Keycloak provisionado, a porta dele não conta.
        $answers.keycloak = @{ mode = 'existing'; url = 'https://sso.lab.test'; realm = 'dokpod' }
        { Assert-ExistingEdgeAnswers -Answers $answers } | Should -Not -Throw
    }
}

Describe 'compose.yaml e dokpod.conf com nginx existente' {
    It 'em loopback, web, BFF e Keycloak escutam só em upstreamHost, e o BFF confia só no loopback e na dokpod-edge (L1)' {
        $answers = New-EdgeAnswers
        $model = New-Model $answers

        $model.services.Keys | Should -Not -Contain 'edge'
        foreach ($name in 'web', 'bff', 'keycloak') {
            $model.services[$name].ports.Count | Should -Be 1 -Because $name
            $model.services[$name].ports[0].host_ip | Should -Be '127.0.0.1' -Because $name
        }
        $model.services.keycloak.ports[0].published | Should -Be '18082'
        $model.services.api.Contains('ports') | Should -BeFalse
        # O encaminhador rootless entrega a conexão do nginx do host com um endereço da rede da porta, não com 127.0.0.1:
        # o BFF fica só na dokpod-edge, que é confiável.
        $model.services.bff.networks | Should -Be @('dokpod-edge')
        Get-Trusted $model | Should -Be @('127.0.0.1/32', '::1/128', '10.253.33.0/24')
        Get-EdgeTrustedNetworks -Answers $answers | Should -BeNullOrEmpty
        # No Podman, o nome público aponta para o host, onde está o nginx do usuário.
        $model.services.api.extra_hosts | Should -Be @('dokpod.lab.test:host-gateway')
        $model.services.bff.extra_hosts | Should -Be @('dokpod.lab.test:host-gateway')

        $edge = New-EdgeConfiguration -Answers $answers
        $edge['dokpod.locations'] | Should -Match 'proxy_pass http://127.0.0.1:18081;'
        $edge['dokpod.locations'] | Should -Match 'proxy_pass http://127.0.0.1:18080;'
        $edge['dokpod.locations'] | Should -Match 'proxy_pass http://127.0.0.1:18082;'
        $edge['dokpod.conf'] | Should -Match ([regex]::Escape("include $(Join-Path $answers.installDir 'edge/dokpod.conf');"))
    }

    It 'em ::1 o upstream vai entre colchetes; no Docker, sem host-gateway; com Keycloak existente, sem a porta dele' {
        $answers = New-EdgeAnswers -Upstream '::1' -Engine 'docker-rootless'
        $model = New-Model $answers
        $model.services.bff.ports[0].host_ip | Should -Be '::1'
        $model.services.api.Contains('extra_hosts') | Should -BeFalse
        (New-EdgeConfiguration -Answers $answers)['dokpod.locations'] | Should -Match ([regex]::Escape('proxy_pass http://[::1]:18081;'))

        $existing = New-EdgeAnswers -Keycloak @{ mode = 'existing'; url = 'https://sso.lab.test'; realm = 'dokpod' }
        $model = New-Model $existing
        $model.services.Keys | Should -Not -Contain 'keycloak'
        $model.services.bff.Contains('extra_hosts') | Should -BeFalse
        (New-EdgeConfiguration -Answers $existing)['dokpod.locations'] | Should -Not -Match 'realms'
    }

    It 'na rede compartilhada, nada é publicado no host, os serviços têm nome próprio e o BFF confia só na sub-rede dela' {
        $answers = New-EdgeAnswers -Upstream @{ network = 'nginx-dokpod' }
        $model = New-Model $answers -Trusted @('10.253.34.0/24')

        $model.networks['dokpod-shared'].external | Should -BeTrue
        $model.networks['dokpod-shared'].name | Should -Be 'nginx-dokpod'
        foreach ($pair in @(@('web', 'dokpod-web'), @('bff', 'dokpod-bff'), @('keycloak', 'dokpod-keycloak'))) {
            $service = $model.services[$pair[0]]
            $service.Contains('ports') | Should -BeFalse -Because $pair[0]
            $service.networks['dokpod-shared'].aliases | Should -Be @($pair[1])
        }
        # API e BFF entram para chegar ao Keycloak pelo nome público, que o nginx recebe como alias (passo manual).
        $model.services.api.networks.Keys | Should -Contain 'dokpod-shared'
        $model.services.api.Contains('extra_hosts') | Should -BeFalse
        Get-Trusted $model | Should -Be @('10.253.34.0/24')

        $edge = New-EdgeConfiguration -Answers $answers
        $edge['dokpod.locations'] | Should -Match 'proxy_pass http://dokpod-bff:8080;'
        $edge['dokpod.locations'] | Should -Match 'proxy_pass http://dokpod-keycloak:8080;'

        $failure = { New-Model $answers } | Should -Throw -PassThru
        $failure.Exception.Message | Should -Match 'sub-redes da rede nginx-dokpod'
    }

    It 'o compose.yaml da rede compartilhada passa no docker compose config' {
        $answers = New-EdgeAnswers -Upstream @{ network = 'nginx-dokpod' }
        $model = New-Model $answers -Trusted @('10.253.34.0/24')
        try {
            $model.secrets.Values | ForEach-Object { New-Item -ItemType File -Path $_.file -Force | Out-Null }
            Write-InstallerFiles -Answers $answers -Model $model -CaBundle (New-CaBundle) -LocalCaPem "-----BEGIN CERTIFICATE-----`n" -AgentsCaPem "-----BEGIN CERTIFICATE-----`n" | Out-Null
            $output = & docker compose --project-directory $answers.installDir -f (Join-Path $answers.installDir 'compose.yaml') config --quiet 2>&1
            $LASTEXITCODE | Should -Be 0 -Because ($output -join "`n")
        }
        finally { Remove-Item -LiteralPath (Split-Path $answers.installDir) -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'os passos manuais dizem o certificado, os dois include, o alias na rede e o reload, sem tocar no nginx' {
        $steps = Get-ExistingEdgeSteps -Answers (New-EdgeAnswers -Upstream @{ network = 'nginx-dokpod' })
        ($steps -join "`n") | Should -Match 'pki/edge.crt'
        ($steps -join "`n") | Should -Match 'include .*/edge/dokpod.conf;'
        ($steps -join "`n") | Should -Match 'include .*/edge/dokpod.locations;'
        ($steps -join "`n") | Should -Match 'docker network connect --alias dokpod.lab.test nginx-dokpod'
        $steps[-1] | Should -Match 'nginx -t && nginx -s reload'
        Get-ExistingEdgeSteps -Answers ([ordered]@{ edge = @{ mode = 'provision' } }) | Should -BeNullOrEmpty
    }
}

Describe 'nginx -t e rede compartilhada com engine real' {
    BeforeAll {
        # O nginx -t monta installDir/edge: o caminho precisa ser o mesmo no host e no container dos testes.
        $script:skip = -not $env:DOCKER_HOST -or -not $env:DOKPOD_TEST_SHARED
    }

    It 'nginx -t aceita o dokpod.conf gerado em loopback e na rede compartilhada, e recusa um quebrado com código 1' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'o socket do engine ou DOKPOD_TEST_SHARED não está disponível'; return }
        foreach ($upstream in '127.0.0.1', @{ network = 'nginx-dokpod' }) {
            $answers = New-EdgeAnswers -Upstream $upstream
            $answers.installDir = Join-Path $env:DOKPOD_TEST_SHARED "t037-$([guid]::NewGuid().ToString('N').Substring(0, 8))/install"
            $directory = Join-Path $answers.installDir 'edge'
            try {
                New-Item -ItemType Directory -Path $directory -Force | Out-Null
                $edge = New-EdgeConfiguration -Answers $answers
                foreach ($name in $edge.Keys) { [System.IO.File]::WriteAllText((Join-Path $directory $name), $edge[$name]) }
                Test-EdgeConfiguration -Answers $answers -Image $script:infra.nginx | Should -Match 'syntax is ok'

                [System.IO.File]::AppendAllText((Join-Path $directory 'dokpod.locations'), "location /quebrado { proxy_pass }`n")
                $failure = { Test-EdgeConfiguration -Answers $answers -Image $script:infra.nginx } | Should -Throw -PassThru
                $failure.Exception.ExitCode | Should -Be 1
                $failure.Exception.Message | Should -Match 'nginx -t recusou'
            }
            finally { Remove-Item -LiteralPath (Split-Path $answers.installDir) -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }

    It 'a rede compartilhada precisa existir e ter sub-rede de até /24 (código 3)' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'o socket do engine ou DOKPOD_TEST_SHARED não está disponível'; return }
        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $small = "dokpod-033-edge-$suffix"; $large = "dokpod-033-edge-large-$suffix"
        try {
            & docker network create --subnet 10.253.200.0/24 $small | Out-Null
            & docker network create --subnet 10.254.0.0/16 $large | Out-Null
            Get-SharedEdgeNetworkSubnet -Network $small | Should -Be @('10.253.200.0/24')
            $failure = { Get-SharedEdgeNetworkSubnet -Network $large } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 3
            $failure = { Get-SharedEdgeNetworkSubnet -Network "ausente-$suffix" } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 3
            $failure.Exception.Message | Should -Match 'docker network create --subnet'
        }
        finally {
            & docker network rm $small $large 2>$null | Out-Null
        }
    }
}
