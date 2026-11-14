#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T032 (FR-016, FR-017, FR-021, FR-031, FR-014, FR-033, D-005): certificados da borda e da API, subida ordenada
# com health, licença Business, resumo final com a senha temporária e o comando install até a primeira falha. A subida
# real usa um projeto compose próprio (dokpod-t032-*), sem tocar no projeto dokpod do host.

BeforeAll {
    foreach ($module in 'Common', 'Secrets', 'State', 'Pki', 'Render', 'Keycloak', 'Migrations', 'Services', 'Install') {
        Import-Module (Join-Path $PSScriptRoot "../src/$module.psm1") -Force
    }
    $script:realNginx = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json).images.nginx

    AfterEach { Clear-InstallerSecrets }

    function New-TestAnswers {
        param([string] $Root, [hashtable] $Override = @{})
        $answers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18443'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $Root 'install'); secretsDir = (Join-Path $Root 'secrets'); manualMigrations = $false
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 18443 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        foreach ($key in $Override.Keys) { $answers[$key] = $Override[$key] }
        return $answers
    }

    function New-TestInstall {
        param([hashtable] $Override = @{})
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        $answers = New-TestAnswers -Root $root -Override $Override
        New-Item -ItemType Directory -Path $answers.installDir, $answers.secretsDir | Out-Null
        $state = Open-InstallerState -InstallDirectory $answers.installDir
        return [pscustomobject]@{ Root = $root; Answers = $answers; State = $state; Ca = (New-LocalCertificateAuthority) }
    }

    function Remove-TestInstall {
        param($Install)
        if ($Install.State) { Close-InstallerState -State $Install.State }
        Remove-Item -LiteralPath $Install.Root -Recurse -Force -ErrorAction SilentlyContinue
    }

    function Get-CertificateRows {
        param($State)
        $rows = Invoke-StateSql -State $State -Name 'certificates-list' -Rows
        return $rows
    }

    # Certificado da organização: CA própria, folha com o SAN pedido, chave em arquivo referenciado por { file }.
    function New-OrganizationFiles {
        param([string] $Root, [string[]] $Names, [string] $Prefix = 'org')
        $orgCa = New-LocalCertificateAuthority
        $leaf = New-InstallerCertificate -Issuer $orgCa -CommonName $Names[0] -Usage Server -DnsNames $Names
        $directory = Join-Path $Root 'org'
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $directory "$Prefix.crt"), (ConvertTo-CertificatePem -Certificate $leaf))
        [System.IO.File]::WriteAllText((Join-Path $directory "$Prefix.key"), (ConvertTo-PrivateKeyPem -Certificate $leaf))
        [System.IO.File]::WriteAllText((Join-Path $directory "$Prefix-chain.crt"), (ConvertTo-CertificatePem -Certificate $orgCa))
        return @{
            source = 'organization'; certificate = (Join-Path $directory "$Prefix.crt"); chain = (Join-Path $directory "$Prefix-chain.crt")
            key = @{ file = (Join-Path $directory "$Prefix.key") }
        }
    }

    # Engine falso: registra cada chamada; o comportamento de cada comando vem de arquivos em $Root/fake.
    function New-FakeEngine {
        param([string] $Root, [int] $UpCode = 0, [string] $PsJson = '', [string] $AdminJson = '{"changed":true}')
        $fake = Join-Path $Root 'fake'
        New-Item -ItemType Directory -Path $fake -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $fake 'ps.json'), $PsJson)
        [System.IO.File]::WriteAllText((Join-Path $fake 'admin.json'), $AdminJson)
        $script = @"
#!/bin/sh
printf '%s\n' "`$@" >> '$fake/calls.log'
echo '----' >> '$fake/calls.log'
case " `$* " in
  *" up --detach --wait "*) echo 'container dokpod-x-1 is unhealthy' >&2; exit $UpCode ;;
  *" compose "*" restart "*) exit 0 ;;
  *" compose "*" ps --all --format json "*) cat '$fake/ps.json'; exit 0 ;;
  *" admin licensing import "*) ls `$(printf '%s\n' "`$@" | sed -n 's/^type=bind,src=\(.*\),dst=\/run\/dokpod-migrations,ro`$/\1/p') > '$fake/work.ls'; cat '$fake/admin.json'; exit 0 ;;
  " image inspect --format {{.Config.User}} "*)
    case "`$*" in *ausente*) [ -f '$fake/pulled' ] || exit 1; echo 70 ;; *keycloak*) echo 1000 ;; *dokpod-api*|*dokpod-bff*) echo 1654:1654 ;; *web*) echo nginx ;; *) echo ;; esac; exit 0 ;;
  " pull --quiet "*) touch '$fake/pulled'; exit 0 ;;
  *" --entrypoint id "*) echo 101; exit 0 ;;
  " ps "*) exit 0 ;;
  *" inspect "*) exit 1 ;;
esac
exit 99
"@
        $path = Join-Path $fake 'docker'
        [System.IO.File]::WriteAllText($path, $script.Replace("`r", ''))
        [System.IO.File]::SetUnixFileMode($path, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        return $path
    }

    function Get-FakeCalls {
        param([string] $Root)
        $log = Join-Path $Root 'fake/calls.log'
        if (-not (Test-Path $log)) { return , @() }
        return , @(([System.IO.File]::ReadAllText($log) -split "----`n") | Where-Object { $_.Trim() } | ForEach-Object { , @($_.TrimEnd("`n") -split "`n") })
    }

    function Invoke-WithConsole {
        param([scriptblock] $Action)
        $writer = [System.IO.StringWriter]::new()
        $previous = [Console]::Out
        [Console]::SetOut($writer)
        try { $result = & $Action }
        finally { [Console]::SetOut($previous) }
        return [pscustomobject]@{ Result = $result; Output = $writer.ToString() }
    }
}

Describe 'Certificados da borda e da API com a CA local' {
    It 'emite a borda em PEM e a API em PFX com os SAN de D-005, registra no estado e reaproveita na reexecução' {
        $install = New-TestInstall
        try {
            $edge = Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role edge -CertificateAuthority $install.Ca
            $api = Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role api -CertificateAuthority $install.Ca
            $edge.Created | Should -BeTrue
            $api.Created | Should -BeTrue
            $edgeNames = Get-CertificateDnsNames -Certificate $edge.Certificate
            $edgeNames | Should -Be @('dokpod.lab.test')
            $apiNames = Get-CertificateDnsNames -Certificate $api.Certificate
            $apiNames | Should -Be @('api')
            foreach ($name in 'pki/edge.crt', 'pki/edge.key', 'pki/api.pfx', 'pki/api.pass') {
                Test-InstallerSecret -SecretsDirectory $install.Answers.secretsDir -Name $name | Should -BeTrue
            }
            # A borda leva a cadeia (folha + CA) e a chave privada que o nginx usa.
            $pem = [System.IO.File]::ReadAllText((Join-Path $install.Answers.secretsDir 'pki/edge.crt'))
            ([regex]::Matches($pem, 'BEGIN CERTIFICATE')).Count | Should -Be 2
            $rows = Get-CertificateRows -State $install.State
            @($rows | Where-Object { $_['role'] -in 'edge', 'api' -and $_['origin'] -eq 'local-ca' }).Count | Should -Be 2
            ($rows | Where-Object { $_['role'] -eq 'edge' })['secret_ref'] | Should -Be 'pki/edge.key'

            $again = Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role api -CertificateAuthority $install.Ca
            $again.Created | Should -BeFalse
            Get-CertificateFingerprint -Certificate $again.Certificate | Should -Be (Get-CertificateFingerprint -Certificate $api.Certificate)
        }
        finally { Remove-TestInstall $install }
    }

    It 'com agentGrpcPort, a API também leva o nome público no SAN' {
        $install = New-TestInstall -Override @{ agentGrpcPort = 17443 }
        try {
            $api = Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role api -CertificateAuthority $install.Ca
            $names = Get-CertificateDnsNames -Certificate $api.Certificate
            $names | Should -Be @('api', 'dokpod.lab.test')
        }
        finally { Remove-TestInstall $install }
    }

    It 'certificado de outra CA interrompe e aponta recreate, sem regravar' {
        $install = New-TestInstall
        try {
            [void] (Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role edge -CertificateAuthority (New-LocalCertificateAuthority))
            $before = [System.IO.File]::ReadAllText((Join-Path $install.Answers.secretsDir 'pki/edge.crt'))
            { Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role edge -CertificateAuthority $install.Ca } |
                Should -Throw '*recreate pki:edge*'
            [System.IO.File]::ReadAllText((Join-Path $install.Answers.secretsDir 'pki/edge.crt')) | Should -Be $before
        }
        finally { Remove-TestInstall $install }
    }

    It 'o certificado sem a chave interrompe; só a chave é sobra de execução interrompida e é substituída' {
        $install = New-TestInstall
        try {
            New-Item -ItemType Directory -Path (Join-Path $install.Answers.secretsDir 'pki') | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $install.Answers.secretsDir 'pki/edge.key'), 'sobra')
            $edge = Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role edge -CertificateAuthority $install.Ca
            $edge.Created | Should -BeTrue
            [System.IO.File]::ReadAllText((Join-Path $install.Answers.secretsDir 'pki/edge.key')) | Should -Match 'BEGIN PRIVATE KEY'

            Remove-Item -LiteralPath (Join-Path $install.Answers.secretsDir 'pki/edge.key')
            { Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role edge -CertificateAuthority $install.Ca } |
                Should -Throw '*sem pki/edge.key*'
        }
        finally { Remove-TestInstall $install }
    }
}

Describe 'Certificado do key ring do BFF' {
    It 'gera RSA autoassinado em PFX com senha e reaproveita na reexecução' {
        $install = New-TestInstall
        try {
            $first = Initialize-DataProtectionCertificate -SecretsDirectory $install.Answers.secretsDir
            $first.Created | Should -BeTrue
            # 3072 bits: módulo de 384 bytes.
            [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($first.Certificate).ExportParameters($false).Modulus.Length | Should -Be 384
            $first.Certificate.HasPrivateKey | Should -BeTrue
            $again = Initialize-DataProtectionCertificate -SecretsDirectory $install.Answers.secretsDir
            $again.Created | Should -BeFalse
            $again.Certificate.Thumbprint | Should -Be $first.Certificate.Thumbprint
            Remove-Item -LiteralPath (Join-Path $install.Answers.secretsDir 'pki/bff-dataprotection.pass')
            { Initialize-DataProtectionCertificate -SecretsDirectory $install.Answers.secretsDir } | Should -Throw '*recreate pki:bff-dataprotection*'
        }
        finally { Remove-TestInstall $install }
    }
}

Describe 'Certificados da organização' {
    It 'valida e grava o certificado informado; reexecução com o mesmo não muda nada; outro exige recreate' {
        $install = New-TestInstall
        try {
            $install.Answers.certificates['edge'] = New-OrganizationFiles -Root $install.Root -Names @('dokpod.lab.test')
            $edge = Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role edge -CertificateAuthority $install.Ca
            $edge.Created | Should -BeTrue
            $edge.Origin | Should -Be 'organization'
            $edge.Chain.Count | Should -Be 1
            ((Get-CertificateRows -State $install.State) | Where-Object { $_['role'] -eq 'edge' })['origin'] | Should -Be 'organization'

            (Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role edge -CertificateAuthority $install.Ca).Created | Should -BeFalse

            $install.Answers.certificates['edge'] = New-OrganizationFiles -Root $install.Root -Names @('dokpod.lab.test') -Prefix 'outro'
            { Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role edge -CertificateAuthority $install.Ca } |
                Should -Throw '*recreate pki:edge*'
        }
        finally { Remove-TestInstall $install }
    }

    It 'SAN errado ou cadeia desconhecida são recusados com código 2 e nada é gravado (FR-017)' {
        $install = New-TestInstall
        try {
            $install.Answers.certificates['api'] = New-OrganizationFiles -Root $install.Root -Names @('dokpod.lab.test')
            $failure = { Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role api -CertificateAuthority $install.Ca } |
                Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 2
            $failure.Exception.Message | Should -Match 'SAN não contém api'

            $files = New-OrganizationFiles -Root $install.Root -Names @('api') -Prefix 'semcadeia'
            $files.Remove('chain')
            $install.Answers.certificates['api'] = $files
            { Initialize-ServiceCertificate -Answers $install.Answers -State $install.State -Role api -CertificateAuthority $install.Ca } |
                Should -Throw '*cadeia não chega*'
            Test-Path (Join-Path $install.Answers.secretsDir 'pki') | Should -BeFalse
        }
        finally { Remove-TestInstall $install }
    }
}

Describe 'Processo que termina antes de ler o stdin' {
    It 'devolve o código de saída em vez de quebrar na escrita (container que não sobe)' {
        $result = Invoke-InstallerProcess -FilePath 'sh' -ArgumentList @('-c', 'echo falhou >&2; exit 3') -InputBytes ([byte[]]::new(8MB))
        $result.ExitCode | Should -Be 3
        $result.Error.Trim() | Should -Be 'falhou'
    }
}

Describe 'Dono dos segredos montados (D-015)' {
    It 'dá a cada arquivo o UID do serviço que o lê; serviço root não muda o dono; usuários diferentes no mesmo arquivo interrompem' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        try {
            $cli = New-FakeEngine -Root $root
            $chown = Join-Path $root 'fake/chown'
            [System.IO.File]::WriteAllText($chown, "#!/bin/sh`nprintf '%s %s\n' `"`$1`" `"`$2`" >> '$root/fake/chown.log'`n")
            [System.IO.File]::SetUnixFileMode($chown, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
            $model = [ordered]@{
                services = [ordered]@{
                    'keycloak-postgres' = [ordered]@{ image = 'postgres'; secrets = @(@{ source = 'kc-db' }) }
                    keycloak            = [ordered]@{ image = 'docker.io/lzocateli/keycloak:26'; secrets = @(@{ source = 'kc-db' }, @{ source = 'kc-boot' }) }
                    api                 = [ordered]@{ image = 'localhost/dokpod-api:t'; secrets = @(@{ source = 'api-conn' }) }
                    agent               = [ordered]@{ image = 'localhost/dokpod-agent:t'; user = '0:0'; secrets = @(@{ source = 'agent-pfx' }) }
                    web                 = [ordered]@{ image = 'localhost/dokpod-web:t' }
                }
                secrets  = [ordered]@{ 'kc-db' = @{ file = '/s/keycloak-db-password' }; 'kc-boot' = @{ file = '/s/keycloak-bootstrap-admin' }
                    'api-conn' = @{ file = '/s/ConnectionStrings__ControlPlane' }; 'agent-pfx' = @{ file = '/s/pki/agent.pfx' } }
            }
            $owners = Set-ServiceSecretOwner -Model $model -Cli $cli -Chown $chown
            $owners['/s/keycloak-db-password'] | Should -Be 1000
            $owners['/s/keycloak-bootstrap-admin'] | Should -Be 1000
            $owners['/s/ConnectionStrings__ControlPlane'] | Should -Be 1654
            $owners.Contains('/s/pki/agent.pfx') | Should -BeFalse
            (Get-Content (Join-Path $root 'fake/chown.log')) | Should -Be @('1000 /s/keycloak-db-password', '1000 /s/keycloak-bootstrap-admin', '1654 /s/ConnectionStrings__ControlPlane')
            Remove-Item (Join-Path $root 'fake/chown.log')

            # Imagem ainda não baixada (host limpo): baixa antes de ler o USER.
            $model.services['postgres-novo'] = [ordered]@{ image = 'docker.io/x/ausente@sha256:1'; secrets = @(@{ source = 'novo' }) }
            $model.secrets['novo'] = @{ file = '/s/novo' }
            (Set-ServiceSecretOwner -Model $model -Services @('postgres-novo') -Cli $cli -Chown $chown)['/s/novo'] | Should -Be 70
            ((Get-FakeCalls -Root $root) | Where-Object { $_[0] -eq 'pull' }).Count | Should -BeGreaterThan 0
            $model.services.Remove('postgres-novo')

            $only = Set-ServiceSecretOwner -Model $model -Services @('api') -Cli $cli -Chown $chown
            @($only.Keys) | Should -Be @('/s/ConnectionStrings__ControlPlane')

            $model.services['web']['secrets'] = @(@{ source = 'api-conn' })
            { Set-ServiceSecretOwner -Model $model -Cli $cli -Chown $chown } | Should -Throw '*api e web com usuários diferentes*'
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Describe 'Subida ordenada' {
    It 'as etapas seguem a ordem bancos, Keycloak, aplicação, borda e agente, só com os serviços do compose' {
        $model = @{ services = [ordered]@{ agent = @{}; api = @{}; web = @{}; bff = @{}; edge = @{}; 'dokpod-postgres' = @{} } }
        $stages = Get-ServiceStage -Model $model
        $stages.Name | Should -Be @('data', 'application', 'edge', 'agent')
        ($stages | Where-Object Name -eq 'application').Services | Should -Be @('api', 'bff', 'web')
        ($stages | Where-Object Name -eq 'data').Services | Should -Be @('dokpod-postgres')
    }

    It 'sobe com up --detach --wait e confere o health de cada serviço' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        try {
            $answers = New-TestAnswers -Root $root
            $cli = New-FakeEngine -Root $root -PsJson ('{"Service":"api","State":"running","Health":"healthy"}' + "`n" + '{"Service":"web","State":"running","Health":""}')
            $health = Invoke-ServiceStage -Answers $answers -Stage application -Services @('api', 'web') -TimeoutSeconds 60 -Cli $cli
            $health.Healthy | Should -Be @($true, $true)
            $calls = Get-FakeCalls -Root $root
            $calls[0] | Should -Be @('compose', '--file', (Join-Path $answers.installDir 'compose.yaml'), '--project-name', 'dokpod',
                'up', '--detach', '--wait', '--wait-timeout', '60', 'api', 'web')
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'serviço que não fica saudável interrompe com o nome e o comando de logs' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        try {
            $answers = New-TestAnswers -Root $root
            $cli = New-FakeEngine -Root $root -UpCode 1 -PsJson '[{"Service":"api","State":"running","Health":"unhealthy"},{"Service":"bff","State":"running","Health":"healthy"}]'
            $failure = { Invoke-ServiceStage -Answers $answers -Stage application -Services @('api', 'bff') -Cli $cli } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 1
            $failure.Exception.Message | Should -Match "'application'"
            $failure.Exception.Message | Should -Match 'api \(running, unhealthy\)'
            $failure.Exception.Message | Should -Not -Match 'bff \('
            $failure.Exception.Message | Should -Match 'docker compose -p dokpod logs api'
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'com engine real: espera o healthcheck, aceita o saudável e recusa o que fica unhealthy' {
        if (-not $env:DOCKER_HOST) {
            Set-ItResult -Skipped -Because 'o socket do engine não foi montado'
            return
        }

        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        $project = "dokpod-t032-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $answers = New-TestAnswers -Root $root
        New-Item -ItemType Directory -Path $answers.installDir -Force | Out-Null
        @"
services:
  edge:
    image: $($script:realNginx)
    healthcheck: { test: ['CMD', 'curl', '--fail', '--silent', 'http://127.0.0.1:8081/nginx-healthz'], interval: 1s, timeout: 3s, retries: 10 }
  broken:
    image: $($script:realNginx)
    healthcheck: { test: ['CMD', 'false'], interval: 1s, timeout: 3s, retries: 2 }
"@ | Set-Content -LiteralPath (Join-Path $answers.installDir 'compose.yaml')
        try {
            $health = Invoke-ServiceStage -Answers $answers -Stage edge -Services @('edge') -Project $project -TimeoutSeconds 60
            $health[0].State | Should -Be 'running'
            $health[0].Health | Should -Be 'healthy'
            { Invoke-ServiceStage -Answers $answers -Stage teste -Services @('broken') -Project $project -TimeoutSeconds 30 } |
                Should -Throw '*broken (*unhealthy*'
        }
        finally {
            & docker compose --file (Join-Path $answers.installDir 'compose.yaml') --project-name $project down --volumes 2>$null | Out-Null
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Borda depois da troca de um upstream' {
    It 'reinicia a borda só quando web, BFF ou Keycloak mudaram e ela não' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        try {
            $answers = New-TestAnswers -Root $root
            $cli = New-FakeEngine -Root $root -PsJson '{"Service":"edge","State":"running","Health":"healthy"}'
            $keep = @([pscustomobject]@{ Component = 'edge'; Action = 'keep' }, [pscustomobject]@{ Component = 'bff'; Action = 'keep' })
            Restart-ServiceForUpstreams -Answers $answers -Plan $keep -Cli $cli | Should -BeFalse
            $edgeToo = @([pscustomobject]@{ Component = 'edge'; Action = 'update' }, [pscustomobject]@{ Component = 'bff'; Action = 'update' })
            Restart-ServiceForUpstreams -Answers $answers -Plan $edgeToo -Cli $cli | Should -BeFalse
            (Get-FakeCalls -Root $root).Count | Should -Be 0

            $bff = @([pscustomobject]@{ Component = 'edge'; Action = 'keep' }, [pscustomobject]@{ Component = 'bff'; Action = 'update' })
            Restart-ServiceForUpstreams -Answers $answers -Plan $bff -Cli $cli | Should -BeTrue
            $calls = Get-FakeCalls -Root $root
            ($calls[0] -join ' ') | Should -Match 'compose .* restart edge$'
            ($calls[1] -join ' ') | Should -Match 'up --detach --wait .* edge$'
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Describe 'Licença da edição Business (FR-031)' {
    It 'na CE a importação não existe; sem licença, a instalação segue com a instrução para depois' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        { Invoke-LicenseImport -Answers (New-TestAnswers -Root $root) -Image 'api' } | Should -Throw '*só existe na edição Business*'
        $result = Invoke-LicenseImport -Answers (New-TestAnswers -Root $root -Override @{ edition = 'business' }) -Image 'api'
        $result.Imported | Should -BeFalse
        $result.Next | Should -Match 'importe-a depois'
        Get-LicenseSlot -Answers (New-TestAnswers -Root $root -Override @{ localEnvironment = @{ stage = 'staging' } }) | Should -Be 'hml'
    }

    It 'importa pelo modo administrativo com o slot do estágio e apaga a cópia temporária' {
        $install = New-TestInstall -Override @{ edition = 'business' }
        try {
            [System.IO.File]::WriteAllText((Join-Path $install.Answers.secretsDir 'dokpod-owner-connection'), 'Host=x')
            $license = Join-Path $install.Root 'license.jws'
            [System.IO.File]::WriteAllText($license, 'eyJ.licenca.de-teste')
            $install.Answers['license'] = $license
            $cli = New-FakeEngine -Root $install.Root
            $result = Invoke-LicenseImport -Answers $install.Answers -Image 'localhost/dokpod-api-business:t' -Cli $cli
            $result.Imported | Should -BeTrue
            $result.Slot | Should -Be 'prd'
            $result.Changed | Should -BeTrue
            $call = (Get-FakeCalls -Root $install.Root)[0]
            ($call -join ' ') | Should -Match 'admin licensing import --file /run/dokpod-migrations/license.jws --slot prd --actor Ana'
            [System.IO.File]::ReadAllText((Join-Path $install.Root 'fake/work.ls')).Trim() | Should -Be 'license.jws'
            @(Get-ChildItem -LiteralPath $install.Answers.installDir -Filter '.license-import-*' -Force).Count | Should -Be 0
        }
        finally { Remove-TestInstall $install }
    }
}

Describe 'Resumo final (FR-014, FR-033)' {
    It 'com TTY mostra a senha uma vez e apaga o arquivo' {
        $install = New-TestInstall
        try {
            Write-InstallerSecret -SecretsDirectory $install.Answers.secretsDir -Name 'initial-admin-password' -Value 'Senha-Temporaria-033-Aa7#'
            $run = Invoke-WithConsole { Write-InstallSummary -Answers $install.Answers -ReportPath '/x/reports/r.json' -Interactive $true }
            $run.Result.PasswordShown | Should -BeTrue
            $run.Output | Should -Match 'https://dokpod.lab.test:18443/dokpod/'
            $run.Output | Should -Match 'Administrador: dokpod-admin'
            $run.Output | Should -Match 'Relatório: /x/reports/r.json'
            ([regex]::Matches($run.Output, 'Senha-Temporaria-033-Aa7#')).Count | Should -Be 1
            Test-InstallerSecret -SecretsDirectory $install.Answers.secretsDir -Name 'initial-admin-password' | Should -BeFalse
        }
        finally { Remove-TestInstall $install }
    }

    It 'sem TTY não mostra a senha, indica o caminho e mantém o arquivo' {
        $install = New-TestInstall
        try {
            Write-InstallerSecret -SecretsDirectory $install.Answers.secretsDir -Name 'initial-admin-password' -Value 'Senha-Temporaria-033-Aa7#'
            $run = Invoke-WithConsole { Write-InstallSummary -Answers $install.Answers -ReportPath '/x/r.json' -NextSteps @('passo um') -Interactive $false }
            $run.Result.PasswordShown | Should -BeFalse
            $run.Output | Should -Not -Match 'Senha-Temporaria-033'
            $run.Output | Should -Match ([regex]::Escape((Join-Path $install.Answers.secretsDir 'initial-admin-password')))
            $run.Output | Should -Match '- passo um'
            Test-InstallerSecret -SecretsDirectory $install.Answers.secretsDir -Name 'initial-admin-password' | Should -BeTrue
        }
        finally { Remove-TestInstall $install }
    }
}

Describe 'Comando install' {
    BeforeAll {
        $script:images = [ordered]@{ api = 'localhost/dokpod-api:t'; bff = 'localhost/dokpod-bff:t'; web = 'localhost/dokpod-web:t'; agent = 'localhost/dokpod-agent:t' }
        $script:infra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images
    }

    It 'Keycloak existente fica para a história 3 (código 2, sem efeito)' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        $answers = New-TestAnswers -Root $root -Override @{ keycloak = @{ mode = 'existing'; url = 'https://sso.lab.test'; realm = 'dokpod' } }
        $failure = { Invoke-InstallerInstall -Answers $answers -Images $script:images -Infrastructure $script:infra -InstallerImage 'i' -Interactive $false } |
            Should -Throw -PassThru
        $failure.Exception.ExitCode | Should -Be 2
        $failure.Exception.Message | Should -Match "keycloak.mode 'existing'.*T039"
        Test-Path $root | Should -BeFalse
    }

    It 'PostgreSQL existing-create com host de loopback é recusado antes de qualquer efeito (código 2)' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        $answers = New-TestAnswers -Root $root -Override @{ postgres = @{ mode = 'existing-create'; host = '127.0.0.1'; port = 5432; admin = @{ username = 'dba' } } }
        $failure = { Invoke-InstallerInstall -Answers $answers -Images $script:images -Infrastructure $script:infra -InstallerImage 'i' -Interactive $false } |
            Should -Throw -PassThru
        $failure.Exception.ExitCode | Should -Be 2
        $failure.Exception.Message | Should -Match 'loopback'
        Test-Path $root | Should -BeFalse
    }

    It 'sem --yes no modo não interativo recusa com código 4, registra a execução e não cria segredo' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        try {
            $answers = New-TestAnswers -Root $root
            $cli = New-FakeEngine -Root $root
            $failure = { Invoke-InstallerInstall -Answers $answers -Images $script:images -Infrastructure $script:infra -InstallerImage 'i' -Interactive $false -Cli $cli } |
                Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 4
            @(Get-ChildItem -LiteralPath $answers.secretsDir -Force).Count | Should -Be 0
            Test-Path (Join-Path $answers.installDir 'compose.yaml') | Should -BeFalse
            $state = Open-InstallerState -InstallDirectory $answers.installDir
            try {
                $runs = Invoke-StateSql -State $state -Name 'runs-expired' -Parameters @{ keep = 0 } -Rows
                $run = (Invoke-StateSql -State $state -Name 'run-get' -Parameters @{ run_id = $runs[0]['run_id'] } -Rows)[0]
                $run['outcome'] | Should -Be 'declined'
                $run['exit_code'] | Should -Be 4
                Test-Path $run['report_path'] | Should -BeTrue
                $lock = Invoke-StateSql -State $state -Name 'lock-get' -Rows
                $lock.Count | Should -Be 0
            }
            finally { Close-InstallerState -State $state }
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'com --yes grava segredos, certificados e arquivos e, na falha da primeira subida, registra a etapa e o relatório' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-svc-$([guid]::NewGuid().ToString('N'))"
        try {
            $answers = New-TestAnswers -Root $root
            $cli = New-FakeEngine -Root $root -UpCode 1 -PsJson '{"Service":"dokpod-postgres","State":"exited","Health":""}'
            $output = Invoke-WithConsole {
                try {
                    Invoke-InstallerInstall -Answers $answers -Images $script:images -Infrastructure $script:infra -InstallerImage 'i' -Interactive $false `
                        -Confirmations @{ Plan = $true } -EngineSocket '/run/user/1000/podman/podman.sock' -Cli $cli
                }
                catch { $_ }
            }
            $output.Result.Exception.ExitCode | Should -Be 1
            $output.Result.Exception.Message | Should -Match "'data'.*dokpod-postgres \(exited\)"
            $output.Output | Should -Match 'Plano para o Dokpod 0.8.0'
            foreach ($name in 'dokpod-db-password', 'keycloak-db-password', 'keycloak-bootstrap-admin', 'pki/ca.key', 'pki/edge.crt', 'pki/api.pfx', 'pki/bff-dataprotection.pfx') {
                Test-InstallerSecret -SecretsDirectory $answers.secretsDir -Name $name | Should -BeTrue
            }
            foreach ($file in 'compose.yaml', 'certs/ca.crt', 'certs/agents-ca.crt', 'certs/ca-bundle.crt', 'edge/dokpod.conf') {
                Test-Path (Join-Path $answers.installDir $file) | Should -BeTrue
            }
            $compose = [System.IO.File]::ReadAllText((Join-Path $answers.installDir 'compose.yaml'))
            $environmentId = [regex]::Match($compose, 'DOKPOD_AGENT_ENVIRONMENT_ID: ([0-9a-f-]{36})').Groups[1].Value
            Test-InstallerSecret -SecretsDirectory $answers.secretsDir -Name "pki/agent-$environmentId.pfx" | Should -BeTrue

            $state = Open-InstallerState -InstallDirectory $answers.installDir
            try {
                $runId = (Invoke-StateSql -State $state -Name 'runs-expired' -Parameters @{ keep = 0 } -Rows)[0]['run_id']
                $steps = Invoke-StateSql -State $state -Name 'run-steps-list' -Parameters @{ run_id = $runId } -Rows
                ($steps | ForEach-Object { "$($_['step'])=$($_['status'])" }) | Should -Be @('secrets=done', 'pki=done', 'config=done', 'services:data=failed')
                # O ambiente já está no estado com o ID do compose.yaml: a retomada não cria outro (FR-027).
                $environments = Invoke-StateSql -State $state -Name 'environments-list' -Rows
                $environments.Count | Should -Be 1
                $environments[0]['environment_id'] | Should -Be $environmentId
                $run = (Invoke-StateSql -State $state -Name 'run-get' -Parameters @{ run_id = $runId } -Rows)[0]
                $run['outcome'] | Should -Be 'failed'
                $report = Get-Content -Raw $run['report_path'] | ConvertFrom-Json
                $report.error.step | Should -Be 'services:data'
            }
            finally { Close-InstallerState -State $state }
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
