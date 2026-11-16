#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T042 (FR-029): recriação por item. O que cada item muda no plano, a recusa de componentes do usuário e de
# itens desconhecidos, e a invalidação só dos arquivos pedidos. A recriação de ponta a ponta fica em Install.Tests.ps1.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Secrets.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Plan.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Recreate.psm1') -Force

    $script:local = '7d6c1f0e-2b44-4a55-9c3f-1b2a3c4d5e6f'
    $script:services = @('dokpod-postgres', 'keycloak-postgres', 'keycloak', 'edge', 'web', 'bff', 'api', 'agent')

    function New-Answers {
        param([hashtable] $Override = @{})
        $answers = [ordered]@{ secretsDir = '/srv/s'; postgres = @{ mode = 'provision' }; keycloak = @{ mode = 'provision' }; edge = @{ mode = 'provision' } }
        foreach ($key in $Override.Keys) { $answers[$key] = $Override[$key] }
        return $answers
    }

    function New-Desired {
        param($Answers, [string[]] $Services = $script:services)
        $hashes = [ordered]@{}
        foreach ($name in $Services) { $hashes[$name] = "hash-$name" }
        return Get-DesiredComponents -Answers $Answers -ServiceHashes $hashes
    }

    function New-Converged {
        param($Desired)
        $observed = [ordered]@{}
        foreach ($component in $Desired) { $observed[$component.Name] = @{ Exists = $true; Hash = $component.Hash; Running = $true; Pending = $null } }
        return $observed
    }

    function Resolve-Items {
        param([string[]] $Items, $Answers = (New-Answers), [string[]] $Services = $script:services)
        return Resolve-RecreateTarget -Answers $Answers -Items $Items -Services $Services -Desired (New-Desired $Answers $Services) -LocalEnvironmentId $script:local
    }
}

Describe 'recreate <item>' {
    It '<Item>: só o que o item muda vai para o plano, com o efeito colateral' -ForEach @(
        @{ Item = 'pki:edge'; Components = @('pki', 'edge'); Files = @('pki/edge.crt', 'pki/edge.key'); Services = @('edge') }
        @{ Item = 'pki:api'; Components = @('pki', 'api'); Files = @('pki/api.pfx', 'pki/api.pass'); Services = @('api') }
        @{ Item = "pki:agent:7d6c1f0e-2b44-4a55-9c3f-1b2a3c4d5e6f"; Components = @('pki', 'agent'); Services = @('agent')
            Files = @('pki/agent-7d6c1f0e-2b44-4a55-9c3f-1b2a3c4d5e6f.pfx', 'pki/agent-7d6c1f0e-2b44-4a55-9c3f-1b2a3c4d5e6f.pass') }
        @{ Item = 'secret:Authentication__Keycloak__ClientSecret'; Components = @('secrets', 'bff'); Files = @('Authentication__Keycloak__ClientSecret'); Services = @('bff') }
        @{ Item = 'secret:ConnectionStrings__ControlPlane'; Components = @('secrets', 'api'); Files = @('ConnectionStrings__ControlPlane'); Services = @('api') }
        @{ Item = 'service:web'; Components = @('web'); Files = @(); Services = @('web') }
        @{ Item = 'config'; Components = @('edge', 'web', 'bff', 'api', 'agent', 'keycloak'); Files = @(); Services = @('edge', 'web', 'bff', 'api', 'agent', 'keycloak') }
    ) {
        $target = Resolve-Items @($Item)
        $target.Components | Should -Be $Components
        @($target.Files) | Should -Be @($Files)
        $target.Services | Should -Be $Services

        $answers = New-Answers
        $desired = New-Desired $answers
        $plan = New-InstallerPlan -Desired $desired -Observed (New-Converged $desired) -Recreate $target.Components
        @($plan | Where-Object Action -ne 'keep' | ForEach-Object Component) | Sort-Object | Should -Be ($Components | Sort-Object)
        foreach ($step in $plan | Where-Object Action -eq 'recreate') { $step.SideEffect | Should -Not -BeNullOrEmpty -Because $step.Component }
        # Os dados nunca entram numa recriação por item.
        ($plan | Where-Object Component -eq 'dokpod-postgres').Action | Should -Be 'keep'
    }

    It 'vários itens numa lista separada por vírgula somam o que cada um muda, sem repetir' {
        $target = Resolve-Items @('pki:edge,service:edge', 'secret:Authentication__Keycloak__ClientSecret')
        $target.Components | Should -Be @('pki', 'secrets', 'edge', 'bff')
        $target.Services | Should -Be @('edge', 'bff')
        $target.Items | Should -Be @('pki:edge', 'service:edge', 'secret:Authentication__Keycloak__ClientSecret')
    }

    It 'componente do usuário, item desconhecido, segredo não regenerável e agente remoto dão código 2: <Name>' -ForEach @(
        @{ Name = 'serviço existing'; Items = @('service:dokpod-postgres'); Override = @{ postgres = @{ mode = 'existing-create' } }; Expected = '*do usuário*FR-029*' }
        @{ Name = 'serviço ausente'; Items = @('service:edge'); Override = @{ edge = @{ mode = 'existing' } }; Expected = '*do usuário*' }
        @{ Name = 'item desconhecido'; Items = @('banco'); Override = @{}; Expected = '*Item desconhecido*' }
        @{ Name = 'pki desconhecida'; Items = @('pki:bff'); Override = @{}; Expected = '*Item desconhecido*' }
        @{ Name = 'senha do banco provisionado'; Items = @('secret:dokpod-db-password'); Override = @{}; Expected = '*não é regenerável*secret:ConnectionStrings__ControlPlane*' }
        @{ Name = 'conexão do banco preparado'; Items = @('secret:ConnectionStrings__ControlPlane'); Override = @{ postgres = @{ mode = 'existing-prepared' } }; Expected = '*não é regenerável*' }
        @{ Name = 'agente remoto'; Items = @('pki:agent:00000000-0000-0000-0000-000000000009'); Override = @{}; Expected = "*não é o ambiente local*host add*" }
        @{ Name = 'pki:ca junto'; Items = @('pki:ca'); Override = @{}; Expected = '*troca da CA*FR-030*' }
        @{ Name = 'nada'; Items = @(); Override = @{}; Expected = '*Diga o que recriar*' }
    ) {
        $answers = New-Answers $Override
        $services = if ($Override.Contains('edge')) { $script:services | Where-Object { $_ -ne 'edge' } } elseif ($Override.Contains('postgres') -and $Override.postgres.mode -ne 'provision') { $script:services | Where-Object { $_ -ne 'dokpod-postgres' } } else { $script:services }
        $failure = { Resolve-Items -Items $Items -Answers $answers -Services $services } | Should -Throw -PassThru
        $failure.Exception.ExitCode | Should -Be 2
        $failure.Exception.Message | Should -BeLike $Expected
    }

    It 'a invalidação remove só os arquivos pedidos de secretsDir, e nada antes dela' {
        $dir = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-recreate-$([guid]::NewGuid().ToString('N'))"
        try {
            Initialize-SecretsDirectory -Path $dir
            foreach ($name in 'pki/edge.crt', 'pki/edge.key', 'pki/api.pfx', 'pki/api.pass', 'Authentication__Keycloak__ClientSecret') {
                Write-InstallerSecret -SecretsDirectory $dir -Name $name -Value "valor-$name"
            }
            $answers = New-Answers; $answers.secretsDir = $dir
            $target = Resolve-RecreateTarget -Answers $answers -Items @('pki:edge') -Services $script:services -Desired (New-Desired $answers) -LocalEnvironmentId $script:local
            foreach ($name in 'pki/edge.crt', 'pki/edge.key') { Test-InstallerSecret -SecretsDirectory $dir -Name $name | Should -BeTrue }

            $removed = Invoke-RecreateInvalidation -Answers $answers -Target $target
            $removed | Should -Be @('pki/edge.crt', 'pki/edge.key')
            foreach ($name in 'pki/edge.crt', 'pki/edge.key') { Test-InstallerSecret -SecretsDirectory $dir -Name $name | Should -BeFalse }
            foreach ($name in 'pki/api.pfx', 'pki/api.pass', 'Authentication__Keycloak__ClientSecret') { Test-InstallerSecret -SecretsDirectory $dir -Name $name | Should -BeTrue }
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Describe 'recreate --all (T043, SC-008)' {
    It 'a segunda confirmação nomeia os bancos provisionados, os key rings e as identidades; componente do usuário não entra' {
        $all = Get-DataRemovalItem -Answers (New-Answers)
        $all | Should -Be @('dokpod-postgres', 'keycloak-postgres', 'dokpod-api-keys', 'dokpod-bff-keys', 'agent-identities')
        $existing = Get-DataRemovalItem -Answers (New-Answers @{ postgres = @{ mode = 'existing-create' }; keycloak = @{ mode = 'existing' } })
        $existing | Should -Be @('dokpod-api-keys', 'dokpod-bff-keys', 'agent-identities')
    }

    It 'só a lista exata remove os dados; lista diferente dá código 2; sem ela, os dados ficam' {
        $answers = New-Answers
        Resolve-DataRemoval -Answers $answers -Confirmed @('agent-identities', 'dokpod-bff-keys', 'dokpod-api-keys', 'keycloak-postgres', 'dokpod-postgres') -Interactive $false | Should -BeTrue
        $failure = { Resolve-DataRemoval -Answers $answers -Confirmed @('dokpod-postgres') -Interactive $false } | Should -Throw -PassThru
        $failure.Exception.ExitCode | Should -Be 2
        Resolve-DataRemoval -Answers $answers -Interactive $false | Should -BeFalse
        Resolve-DataRemoval -Answers $answers -Interactive $true -Prompt { '' } | Should -BeFalse
        Resolve-DataRemoval -Answers $answers -Interactive $true -Prompt { 'dokpod-postgres' } | Should -BeFalse
        Resolve-DataRemoval -Answers $answers -Interactive $true -Prompt { 'dokpod-postgres, keycloak-postgres,dokpod-api-keys,dokpod-bff-keys,agent-identities' } | Should -BeTrue
    }

    It 'no plano, sem a segunda confirmação, só os serviços sem dados são recriados; com ela, os bancos saem e migrations e realm voltam do zero' {
        $answers = New-Answers
        $desired = New-Desired $answers
        $observed = New-Converged $desired
        $kept = New-InstallerPlan -Desired $desired -Observed $observed -RecreateAll
        foreach ($name in 'dokpod-postgres', 'keycloak-postgres', 'secrets', 'pki', 'realm', 'migrations') { ($kept | Where-Object Component -eq $name).Action | Should -Be 'keep' -Because $name }
        foreach ($name in 'keycloak', 'edge', 'web', 'bff', 'api', 'agent') { ($kept | Where-Object Component -eq $name).Action | Should -Be 'recreate' -Because $name }

        $removed = New-InstallerPlan -Desired $desired -Observed $observed -RecreateAll -ConfirmDataRemoval (Get-DataRemovalItem -Answers $answers)
        foreach ($name in 'dokpod-postgres', 'keycloak-postgres') { ($removed | Where-Object Component -eq $name).Action | Should -Be 'remove' -Because $name }
        foreach ($name in 'realm', 'migrations') { ($removed | Where-Object Component -eq $name).Action | Should -Be 'recreate' -Because $name }
        foreach ($name in 'secrets', 'pki') { ($removed | Where-Object Component -eq $name).Action | Should -Be 'keep' -Because $name }
    }
}

Describe 'certificates check (T049, FR-019)' {
    It 'perto do vencimento sugere recreate com a CA local, host add para o host remoto, e só aviso para a organização' {
        Import-Module (Join-Path $PSScriptRoot '../src/State.psm1') -Force
        Import-Module (Join-Path $PSScriptRoot '../src/Pki.psm1') -Force
        Import-Module (Join-Path $PSScriptRoot '../src/Environments.psm1') -Force
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-certs-$([guid]::NewGuid().ToString('N'))"
        $answers = [ordered]@{ installDir = (Join-Path $root 'install'); secretsDir = (Join-Path $root 'secrets')
            certificates = @{ edge = @{ source = 'organization' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 } }
        Initialize-SecretsDirectory -Path $answers.secretsDir
        $state = Open-InstallerState -InstallDirectory $answers.installDir
        try {
            $ca = (Initialize-LocalCertificateAuthority -SecretsDirectory $answers.secretsDir).Certificate
            $edge = New-InstallerCertificate -Issuer $ca -CommonName 'dokpod.lab.test' -Usage Server -DnsNames @('dokpod.lab.test')
            Write-InstallerSecret -SecretsDirectory $answers.secretsDir -Name 'pki/edge.crt' -Bytes ([System.Text.Encoding]::UTF8.GetBytes((ConvertTo-CertificatePem -Certificate $edge)))
            $local = [guid]::NewGuid().ToString(); $remote = [guid]::NewGuid().ToString()
            foreach ($id in $local, $remote) { [void] (Initialize-AgentCertificate -SecretsDirectory $answers.secretsDir -EnvironmentId $id -CertificateAuthority $ca) }
            Invoke-StateSql -State $state -Name 'environment-upsert' -Parameters @{ environment_id = $local; name = 'host-local'; engine = 'podman'; agent_fingerprint = 'A'; package_generated_at = $null; revoked_at = $null }
            Invoke-StateSql -State $state -Name 'environment-upsert' -Parameters @{ environment_id = $remote; name = 'host-b'; engine = 'docker'; agent_fingerprint = 'B'; package_generated_at = '2026-11-14T00:00:00.000Z'; revoked_at = $null }

            $today = Get-InstallerCertificateStatus -Answers $answers -State $state
            @($today | Where-Object Status -ne 'ok').Count | Should -Be 0
            $soon = Get-InstallerCertificateStatus -Answers $answers -State $state -Now ([DateTimeOffset]::UtcNow.AddDays(380))
            ($soon | Where-Object Role -eq 'ca').Status | Should -Be 'ok'
            ($soon | Where-Object Role -eq 'edge').Action | Should -Match 'certificado da organização'
            ($soon | Where-Object Role -eq "agent:$local").Action | Should -Be "recreate pki:agent:$local"
            ($soon | Where-Object Role -eq "agent:$remote").Action | Should -Match 'host add --name host-b'
            $late = Get-InstallerCertificateStatus -Answers $answers -State $state -Now ([DateTimeOffset]::UtcNow.AddDays(400))
            ($late | Where-Object Role -eq 'edge').Status | Should -Be 'expired'
        }
        finally {
            Close-InstallerState -State $state
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
