#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T046 e T047 (FR-025, FR-022, L2, L6): pacote do host remoto. Conteúdo, cifra compatível com o openssl do
# host de destino e as recusas antes de qualquer efeito. O cadastro de ponta a ponta fica em Install.Tests.ps1.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Secrets.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Pki.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Environments.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Hosts.psm1') -Force
    $script:image = (Get-Content -LiteralPath (Join-Path (Get-InstallerHome) 'templates/infrastructure-images.json') -Raw | ConvertFrom-Json).images.postgresql

    AfterEach { Clear-InstallerSecrets }

    function New-HostAnswers {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-hosts-$([guid]::NewGuid().ToString('N'))"
        $answers = [ordered]@{
            installDir = (Join-Path $root 'install'); secretsDir = (Join-Path $root 'secrets'); publicUrl = 'https://dokpod.lab.test'; agentGrpcPort = 17443
            operator = 'Ana'; certificates = @{ agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
        }
        Initialize-SecretsDirectory -Path $answers.secretsDir
        New-Item -ItemType Directory -Path (Join-Path $answers.installDir 'certs') -Force | Out-Null
        $ca = (Initialize-LocalCertificateAuthority -SecretsDirectory $answers.secretsDir).Certificate
        [System.IO.File]::WriteAllText((Join-Path $answers.installDir 'certs/ca.crt'), (ConvertTo-CertificatePem -Certificate $ca))
        $script:hostId = [guid]::NewGuid().ToString()
        [void] (Initialize-AgentCertificate -SecretsDirectory $answers.secretsDir -EnvironmentId $script:hostId -CertificateAuthority $ca)
        return $answers
    }
}

Describe 'Pacote do host remoto' {
    It 'Docker e Podman: certificado, senha, CA, agent.env, compose.agent.yaml e README, e nenhum outro segredo' {
        $answers = New-HostAnswers
        try {
            foreach ($engine in 'docker', 'podman') {
                $files = New-HostPackageFiles -Answers $answers -EnvironmentId $script:hostId -Engine $engine -AgentImage 'ghcr.io/altivy-it/dokpod-ce-agent@sha256:abc'
                @($files.Keys) | Should -Be @('agent.pfx', 'agent.pass', 'ca.crt', 'agent.env', 'compose.agent.yaml', 'README.txt')
                $files['agent.env'] | Should -Match "DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT=https://dokpod.lab.test:17443"
                $files['agent.env'] | Should -Match "DOKPOD_AGENT_ENVIRONMENT_ID=$($script:hostId)"
                $files['compose.agent.yaml'] | Should -Match 'image: ghcr.io/altivy-it/dokpod-ce-agent@sha256:abc'
                $files['compose.agent.yaml'] | Should -Match '\$\{DOKPOD_ENGINE_SOCKET:\?'
                # Nenhum outro segredo da instalação: nem a chave da CA, nem o client secret do BFF.
                $text = ($files.Values | ForEach-Object { if ($_ -is [byte[]]) { '' } else { $_ } }) -join "`n"
                $text | Should -Not -Match 'PRIVATE KEY'
            }
            $socket = (New-HostPackageFiles -Answers $answers -EnvironmentId $script:hostId -Engine 'podman' -AgentImage 'a')['README.txt']
            $socket | Should -Match 'podman/podman.sock'
        }
        finally { Remove-Item -LiteralPath (Split-Path $answers.installDir) -Recurse -Force }
    }

    It 'Windows: windows-service.txt com o endpoint e o environmentId no lugar do compose' {
        $answers = New-HostAnswers
        try {
            $files = New-HostPackageFiles -Answers $answers -EnvironmentId $script:hostId -Engine 'windows' -AgentImage 'a'
            @($files.Keys) | Should -Be @('agent.pfx', 'agent.pass', 'ca.crt', 'windows-service.txt', 'README.txt')
            $files['windows-service.txt'] | Should -Match '-ControlPlaneEndpoint https://dokpod.lab.test:17443'
            $files['windows-service.txt'] | Should -Match "-EnvironmentId $($script:hostId)"
        }
        finally { Remove-Item -LiteralPath (Split-Path $answers.installDir) -Recurse -Force }
    }

    It 'a cifra abre com a frase e não com outra' {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes('conteúdo do pacote 033')
        $cipher = Protect-HostPackage -Bytes $bytes -Passphrase 'frase-certa-033'
        [System.Text.Encoding]::ASCII.GetString($cipher[0..7]) | Should -Be 'Salted__'
        [System.Text.Encoding]::UTF8.GetString((Unprotect-HostPackage -Bytes $cipher -Passphrase 'frase-certa-033')) | Should -Be 'conteúdo do pacote 033'
        { Unprotect-HostPackage -Bytes $cipher -Passphrase 'frase-errada' } | Should -Throw
    }

    It 'o openssl do host de destino abre o pacote com o comando do README' {
        if (-not $env:DOCKER_HOST) { Set-ItResult -Skipped -Because 'o socket do engine não foi montado'; return }
        $cipher = Protect-HostPackage -Bytes ([System.Text.Encoding]::UTF8.GetBytes("pacote 033`n")) -Passphrase 'frase-033-openssl'
        $base64 = [System.Convert]::ToBase64String($cipher)
        $output = & docker run --rm --pids-limit 512 --entrypoint sh $script:image -c "echo $base64 | base64 -d > /tmp/a.enc && openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 -md sha256 -in /tmp/a.enc -pass pass:frase-033-openssl" 2>&1
        ($output -join "`n") | Should -Be 'pacote 033'
    }

    It 'host add sem agentGrpcPort ou com nome inválido dá código 2 antes de qualquer efeito' {
        $answers = New-HostAnswers
        try {
            $common = @{ State = [pscustomobject]@{}; ApiImage = 'a'; AgentImage = 'g'; InstallerImage = 'i'; Network = 'n'; KeycloakNetwork = 'k'; KeycloakUrl = 'http://k' }
            $answers.Remove('agentGrpcPort')
            $failure = { Invoke-InstallerHostAdd -Answers $answers -Name 'host-b' @common } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 2
            $failure.Exception.Message | Should -Match 'agentGrpcPort'
            $answers['agentGrpcPort'] = 17443
            $failure = { Invoke-InstallerHostAdd -Answers $answers -Name 'Host B;rm' @common } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 2
            Test-Path (Join-Path $answers.installDir 'packages') | Should -BeFalse
        }
        finally { Remove-Item -LiteralPath (Split-Path $answers.installDir) -Recurse -Force }
    }
}
