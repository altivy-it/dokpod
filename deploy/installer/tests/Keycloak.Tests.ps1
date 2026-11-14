#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T029 (FR-012, FR-013, FR-014, FR-028, FR-033, L3): segredos do Keycloak provisionado, reconciliação do realm em
# container avulso e remoção do administrador de bootstrap. A integração usa um Keycloak 26.7.0 real (DOCKER_HOST).

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Secrets.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Keycloak.psm1') -Force
    $script:images = (Get-Content -LiteralPath (Join-Path (Get-InstallerHome) 'templates/infrastructure-images.json') -Raw | ConvertFrom-Json).images
    # O configure-keycloak.ps1 do checkout, quando montado em /src (pester.sh), vence o copiado na imagem.
    $script:scripts = if (Test-Path '/src/tools/scripts/configure-keycloak.ps1') { '/src/tools/scripts' } else { Join-Path (Get-InstallerHome) 'keycloak' }
    $script:installerImage = if ($env:DOKPOD_INSTALLER_IMAGE_REF) { $env:DOKPOD_INSTALLER_IMAGE_REF } else { 'localhost/dokpod-installer:test' }

    AfterEach { Clear-InstallerSecrets }

    function New-TestDirectory {
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-kc-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $path | Out-Null
        return $path
    }

    function New-Answers {
        param([string] $SecretsDir)
        return @{
            secretsDir = $SecretsDir; publicUrl = 'https://dokpod.lab.test:18443'
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
        }
    }

    # Engine falso: grava argumentos e stdin e responde como o container avulso, conforme o código pedido.
    function New-FakeEngine {
        param([string] $Root, [int] $ExitCode = 0, [string] $Result = '{"userStateBefore":"absent","passwordSet":true,"updatePasswordPending":true,"bootstrap":true,"bootstrapRemoved":true}')
        $fake = Join-Path $Root 'docker'
        [System.IO.File]::WriteAllText($fake, "#!/bin/sh`nprintf '%s\n' `"`$@`" > '$Root/args.log'`ncat > '$Root/stdin.tar'`necho 'Reconciliado.'`necho '@@dokpod-result@@$Result'`n[ $ExitCode -eq 0 ] || echo 'falhou' >&2`nexit $ExitCode`n")
        [System.IO.File]::SetUnixFileMode($fake, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        return $fake
    }

    function Get-TarText {
        param([string] $Path, [string] $Name)
        $reader = [System.Formats.Tar.TarReader]::new([System.IO.File]::OpenRead($Path))
        try {
            while ($null -ne ($entry = $reader.GetNextEntry())) {
                if ($entry.Name -eq $Name) { return [pscustomobject]@{ Mode = $entry.Mode; Text = [System.IO.StreamReader]::new($entry.DataStream).ReadToEnd() } }
            }
        }
        finally { $reader.Dispose() }
    }
}

Describe 'Segredos do Keycloak provisionado' {
    It 'gera bootstrap, client secret do BFF e do provisioner só quando faltam; bootstrap vazio continua vazio' {
        $dir = New-TestDirectory
        try {
            $answers = New-Answers $dir
            $created = Initialize-KeycloakSecret -Answers $answers
            $created | Should -Be @('keycloak-bootstrap-admin', 'Authentication__Keycloak__ClientSecret', 'keycloak-provisioner-client-secret')
            $bff = Read-InstallerSecret -SecretsDirectory $dir -Name 'Authentication__Keycloak__ClientSecret'
            [System.IO.File]::WriteAllBytes((Join-Path $dir 'keycloak-bootstrap-admin'), [byte[]] @())

            (Initialize-KeycloakSecret -Answers $answers).Count | Should -Be 0
            (Get-Item (Join-Path $dir 'keycloak-bootstrap-admin')).Length | Should -Be 0
            Read-InstallerSecret -SecretsDirectory $dir -Name 'Authentication__Keycloak__ClientSecret' | Should -Be $bff
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }

    It 'senha inicial atende à política do realm e muda a cada geração' {
        $first = New-InitialAdminPassword
        $first.Length | Should -BeGreaterOrEqual 14
        $first | Should -Match '[A-Z]' -Because 'maiúscula'
        $first | Should -Match '[0-9]'
        $first | Should -Not -Be (New-InitialAdminPassword)
    }
}

Describe 'Container avulso da reconciliação' {
    It 'isolado na rede do Keycloak, segredos só no tar do stdin, e esvazia o bootstrap depois da remoção (FR-033)' {
        $dir = New-TestDirectory
        try {
            $answers = New-Answers $dir
            [void] (Initialize-KeycloakSecret -Answers $answers)
            $bootstrap = Read-InstallerSecret -SecretsDirectory $dir -Name 'keycloak-bootstrap-admin'
            $fake = New-FakeEngine -Root $dir

            $state = Invoke-KeycloakRealm -Answers $answers -Image 'localhost/dokpod-installer:test' -ScriptsRoot $script:scripts -Cli $fake

            $state.BootstrapRemoved | Should -BeTrue
            $state.InitialPasswordFile | Should -Be 'initial-admin-password'
            (Get-Item (Join-Path $dir 'keycloak-bootstrap-admin')).Length | Should -Be 0
            $arguments = Get-Content -LiteralPath (Join-Path $dir 'args.log')
            $arguments[[array]::IndexOf($arguments, '--network') + 1] | Should -Be 'dokpod_keycloak-data'
            $arguments | Should -Contain '--read-only'
            $arguments[[array]::IndexOf($arguments, '--log-driver') + 1] | Should -Be 'none'
            ($arguments -join "`n") | Should -Not -Match ([regex]::Escape($bootstrap))
            ($arguments -join "`n") | Should -Not -Match 'sock|--volume|--publish'
            # Sem limite explícito, o Podman 6 cria o container avulso com pids.max 1 (T034).
            ($arguments -join ' ') | Should -Match '--pids-limit 512'

            $inputEntry = Get-TarText -Path (Join-Path $dir 'stdin.tar') -Name 'input.json'
            $inputEntry.Mode | Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite')
            ($inputEntry.Text | ConvertFrom-Json).secrets.bootstrapPassword | Should -Be $bootstrap
            # O BFF responde sob /dokpod: é dele que sai o redirect_uri registrado no client (T034).
            ($inputEntry.Text | ConvertFrom-Json).bffBaseUrl | Should -Be 'https://dokpod.lab.test:18443/dokpod'
            (Get-TarText -Path (Join-Path $dir 'stdin.tar') -Name 'configure-keycloak.ps1').Text | Should -Match 'InternalHttp'
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }

    It 'falha na remoção do bootstrap não converge, mantém o arquivo e diz a ação (L3)' {
        $dir = New-TestDirectory
        try {
            $answers = New-Answers $dir
            [void] (Initialize-KeycloakSecret -Answers $answers)
            $fake = New-FakeEngine -Root $dir -ExitCode 20
            { Invoke-KeycloakRealm -Answers $answers -Image 'img' -ScriptsRoot $script:scripts -Cli $fake } | Should -Throw '*continua no master*reexecute*'
            (Get-Item (Join-Path $dir 'keycloak-bootstrap-admin')).Length | Should -BeGreaterThan 0
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }

    It 'falha na configuração apaga a senha inicial criada nesta execução e não a mostra' {
        $dir = New-TestDirectory
        try {
            $answers = New-Answers $dir
            [void] (Initialize-KeycloakSecret -Answers $answers)
            $fake = New-FakeEngine -Root $dir -ExitCode 1 -Result ''
            { Invoke-KeycloakRealm -Answers $answers -Image 'img' -ScriptsRoot $script:scripts -Cli $fake } | Should -Throw '*Keycloak falhou*'
            Test-Path (Join-Path $dir 'initial-admin-password') | Should -BeFalse
        }
        finally { Remove-Item -LiteralPath $dir -Recurse -Force }
    }
}

Describe 'Reconciliação contra Keycloak real' {
    BeforeAll {
        $script:skip = -not $env:DOCKER_HOST -or -not (& docker image ls --quiet $script:installerImage)
        if ($script:skip) { return }

        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $script:network = "dokpod-033-kc-$suffix"
        $script:keycloak = "dokpod-033-kc-$suffix"
        $script:dir = New-TestDirectory
        $script:answers = New-Answers $script:dir
        [void] (Initialize-KeycloakSecret -Answers $script:answers)
        $script:bootstrap = Read-InstallerSecret -SecretsDirectory $script:dir -Name 'keycloak-bootstrap-admin'
        & docker network create --internal $script:network | Out-Null
        # Mesmo hostname público e HTTP interno do compose (Render.psm1); start-dev só para não precisar do banco.
        & docker run --detach --name $script:keycloak --network $script:network --network-alias keycloak --log-driver none `
            --env KC_BOOTSTRAP_ADMIN_USERNAME=dokpod-bootstrap --env "KC_BOOTSTRAP_ADMIN_PASSWORD=$($script:bootstrap)" `
            --env KC_HOSTNAME=https://dokpod.lab.test:18443 --env KC_HTTP_ENABLED=true --env KC_PROXY_HEADERS=xforwarded `
            $script:images.keycloak start-dev | Out-Null

        function Invoke-Kcadm {
            param([string[]] $Arguments)
            $output = & docker exec $script:keycloak /opt/keycloak/bin/kcadm.sh @Arguments --config /tmp/kcadm.config 2>&1
            return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
        }

        function Connect-Provisioner {
            $secret = Read-InstallerSecret -SecretsDirectory $script:dir -Name 'keycloak-provisioner-client-secret'
            (Invoke-Kcadm @('config', 'credentials', '--server', 'http://localhost:8080', '--realm', 'dokpod', '--client', 'dokpod-provisioner', '--secret', $secret)).ExitCode |
                Should -Be 0
        }

        function Get-InitialUser {
            return (Invoke-Kcadm @('get', 'users', '-r', 'dokpod', '-q', 'username=dokpod-admin', '-q', 'exact=true')).Output | ConvertFrom-Json
        }
    }

    AfterAll {
        if ($script:skip) { return }
        & docker rm --force --volumes $script:keycloak *> $null
        & docker network rm $script:network *> $null
        Remove-Item -LiteralPath $script:dir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'primeira execução cria o realm, aplica a senha temporária, grava o client secret do BFF e remove o bootstrap, sem segredo no inspect' {
        if ($script:skip) {
            Set-ItResult -Skipped -Because 'sem socket do engine ou sem a imagem do instalador'
            return
        }

        $inspects = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
        $stop = [System.Threading.ManualResetEventSlim]::new($false)
        $poller = Start-ThreadJob -ArgumentList $inspects, $stop -ScriptBlock {
            param($bag, $stopSignal)
            while (-not $stopSignal.IsSet) {
                foreach ($id in @(& docker ps --quiet --filter label=io.dokpod.installer.task=keycloak)) {
                    $text = (& docker inspect $id 2>$null) -join "`n"
                    if ($text) { $bag.Add($text) }
                }
                Start-Sleep -Milliseconds 200
            }
        }

        try {
            $state = Invoke-KeycloakRealm -Answers $script:answers -Image $script:installerImage -Network $script:network -ScriptsRoot $script:scripts
            $state.UserStateBefore | Should -Be 'absent'
            $state.PasswordSet | Should -BeTrue
            $state.UpdatePasswordPending | Should -BeTrue
            $state.BootstrapRemoved | Should -BeTrue
            $state.InitialPasswordFile | Should -Be 'initial-admin-password'
            (Get-Item (Join-Path $script:dir 'keycloak-bootstrap-admin')).Length | Should -Be 0
        }
        finally {
            $stop.Set()
            [void] (Wait-Job $poller -Timeout 10)
            Remove-Job $poller -Force -ErrorAction SilentlyContinue
        }

        $inspects.Count | Should -BeGreaterThan 0 -Because 'o teste precisa ter visto o container avulso em execução'
        $values = @($script:bootstrap) + @('Authentication__Keycloak__ClientSecret', 'keycloak-provisioner-client-secret', 'initial-admin-password' |
                ForEach-Object { Read-InstallerSecret -SecretsDirectory $script:dir -Name $_ })
        foreach ($text in $inspects) {
            foreach ($value in $values) { $text | Should -Not -Match ([regex]::Escape($value)) }
        }

        # O bootstrap não autentica mais no master.
        (Invoke-Kcadm @('config', 'credentials', '--server', 'http://localhost:8080', '--realm', 'master', '--user', 'dokpod-bootstrap', '--password', $script:bootstrap)).ExitCode |
            Should -Not -Be 0

        Connect-Provisioner
        $user = Get-InitialUser
        $user.requiredActions | Should -Contain 'UPDATE_PASSWORD'
        $user.email | Should -Be 'ana@lab.test'
        $bffId = ((Invoke-Kcadm @('get', 'clients', '-r', 'dokpod', '-q', 'clientId=dokpod-bff', '--fields', 'id')).Output | ConvertFrom-Json).id
        ((Invoke-Kcadm @('get', "clients/$bffId/client-secret", '-r', 'dokpod')).Output | ConvertFrom-Json).value |
            Should -Be (Read-InstallerSecret -SecretsDirectory $script:dir -Name 'Authentication__Keycloak__ClientSecret')
        $groups = (Invoke-Kcadm @('get', "users/$($user.id)/groups", '-r', 'dokpod')).Output | ConvertFrom-Json
        $groups.path | Should -Contain '/dokpod/administrators'
    }

    It 'reexecução sem bootstrap converge e mantém a troca obrigatória de senha e a mesma senha temporária' {
        if ($script:skip) {
            Set-ItResult -Skipped -Because 'sem socket do engine ou sem a imagem do instalador'
            return
        }

        $before = Read-InstallerSecret -SecretsDirectory $script:dir -Name 'initial-admin-password'
        $state = Invoke-KeycloakRealm -Answers $script:answers -Image $script:installerImage -Network $script:network -ScriptsRoot $script:scripts
        $state.BootstrapMode | Should -BeFalse
        $state.UserStateBefore | Should -Be 'ready'
        $state.PasswordSet | Should -BeFalse
        $state.UpdatePasswordPending | Should -BeTrue
        Read-InstallerSecret -SecretsDirectory $script:dir -Name 'initial-admin-password' | Should -Be $before

        Connect-Provisioner
        (Get-InitialUser).requiredActions | Should -Contain 'UPDATE_PASSWORD'
    }

    It 'depois que o administrador troca a senha, a reexecução apaga initial-admin-password' {
        if ($script:skip) {
            Set-ItResult -Skipped -Because 'sem socket do engine ou sem a imagem do instalador'
            return
        }

        Connect-Provisioner
        $user = Get-InitialUser
        (Invoke-Kcadm @('set-password', '-r', 'dokpod', '--userid', $user.id, '--new-password', "Trocada-033-$([guid]::NewGuid().ToString('N'))")).ExitCode | Should -Be 0
        (Invoke-Kcadm @('update', "users/$($user.id)", '-r', 'dokpod', '-s', 'requiredActions=[]')).ExitCode | Should -Be 0

        $state = Invoke-KeycloakRealm -Answers $script:answers -Image $script:installerImage -Network $script:network -ScriptsRoot $script:scripts
        $state.UpdatePasswordPending | Should -BeFalse
        $state.InitialPasswordFile | Should -BeNullOrEmpty
        Test-Path (Join-Path $script:dir 'initial-admin-password') | Should -BeFalse
    }

    It 'divergências do realm provisionado: nenhuma depois da reconciliação, e o redirect URI alterado à mão aparece (T040)' {
        if ($script:skip) {
            Set-ItResult -Skipped -Because 'sem socket do engine ou sem a imagem do instalador'
            return
        }

        Get-KeycloakRealmDrift -Answers $script:answers -Image $script:installerImage -Network $script:network -ScriptsRoot $script:scripts | Should -BeNullOrEmpty
        Connect-Provisioner
        $bffId = ((Invoke-Kcadm @('get', 'clients', '-r', 'dokpod', '-q', 'clientId=dokpod-bff', '--fields', 'id')).Output | ConvertFrom-Json).id
        (Invoke-Kcadm @('update', "clients/$bffId", '-r', 'dokpod', '-s', 'redirectUris=["https://mudado.lab.test/signin-oidc"]')).ExitCode | Should -Be 0
        Get-KeycloakRealmDrift -Answers $script:answers -Image $script:installerImage -Network $script:network -ScriptsRoot $script:scripts |
            Should -Be @('client dokpod-bff: redirectUris')
        [void] (Invoke-KeycloakRealm -Answers $script:answers -Image $script:installerImage -Network $script:network -ScriptsRoot $script:scripts)
        Get-KeycloakRealmDrift -Answers $script:answers -Image $script:installerImage -Network $script:network -ScriptsRoot $script:scripts | Should -BeNullOrEmpty
    }
}

Describe 'Keycloak existente: credencial, CA e códigos (T039)' {
    BeforeAll {
        function New-ExistingAnswers {
            param([string] $SecretsDir)
            return [ordered]@{
                secretsDir = $SecretsDir; publicUrl = 'https://dokpod.lab.test:18443'
                keycloak = [ordered]@{ mode = 'existing'; url = 'https://sso.lab.test:8443'; realm = 'dokpod'; admin = [ordered]@{ username = 'admin' }; administratorUser = 'ana' }
            }
        }
    }

    It 'senha do master de { env }, do prompt, nula na reexecução com o provisioner, ou código 2 sem terminal; nunca em secretsDir' {
        $dir = New-TestDirectory
        try {
            $answers = New-ExistingAnswers $dir
            Get-ExistingKeycloakAdminCredential -Answers $answers -Interactive $true -AskCredential { 'canario-kc-prompt-033' } | Should -Be 'canario-kc-prompt-033'
            Get-ExistingKeycloakAdminCredential -Answers $answers -Interactive $false -ProvisionerAvailable $true | Should -BeNullOrEmpty
            $failure = { Get-ExistingKeycloakAdminCredential -Answers $answers -Interactive $false } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 2
            $env:DOKPOD_TEST_KC_ADMIN = 'canario-kc-env-033'
            $answers.keycloak.admin['password'] = @{ env = 'DOKPOD_TEST_KC_ADMIN' }
            Get-ExistingKeycloakAdminCredential -Answers $answers -Interactive $false | Should -Be 'canario-kc-env-033'
            Get-ChildItem -LiteralPath $dir | Should -BeNullOrEmpty
        }
        finally { Remove-Item Env:DOKPOD_TEST_KC_ADMIN -ErrorAction SilentlyContinue; Remove-Item -LiteralPath $dir -Recurse -Force }
    }

    It 'no existente gera só os client secrets, recusa CA com chave privada e leva a CA ao container' {
        $dir = New-TestDirectory
        try {
            $answers = New-ExistingAnswers $dir
            (Initialize-KeycloakSecret -Answers $answers) | Sort-Object | Should -Be @('Authentication__Keycloak__ClientSecret', 'keycloak-provisioner-client-secret')
            $ca = Join-Path $dir 'ca.pem'
            [System.IO.File]::WriteAllText($ca, "-----BEGIN CERTIFICATE-----`nAA==`n-----END CERTIFICATE-----`n-----BEGIN PRIVATE KEY-----`nAA==`n-----END PRIVATE KEY-----`n")
            $answers.keycloak['caCertificate'] = $ca
            $failure = { Get-ExistingKeycloakCaPem -Answers $answers } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 2

            [System.IO.File]::WriteAllText($ca, "-----BEGIN CERTIFICATE-----`nAA==`n-----END CERTIFICATE-----`n")
            $fake = New-FakeEngine -Root $dir -Result '{"userStateBefore":"ready","passwordSet":false,"updatePasswordPending":false,"bootstrap":true,"bootstrapRemoved":false}'
            $answers.keycloak.admin['password'] = @{ env = 'DOKPOD_TEST_KC_ADMIN' }
            $env:DOKPOD_TEST_KC_ADMIN = 'canario-kc-env-033'
            [void] (Invoke-ExistingKeycloakRealm -Answers $answers -Image 'img' -Cli $fake -ScriptsRoot $script:scripts)
            $arguments = Get-Content -Raw (Join-Path $dir 'args.log')
            $arguments | Should -Match 'export SSL_CERT_FILE='
            $arguments | Should -Match "`nbridge`n"
            $arguments | Should -Not -Match 'canario-kc-env-033'
            (Get-TarText -Path (Join-Path $dir 'stdin.tar') -Name 'ca.crt').Text | Should -Match 'BEGIN CERTIFICATE'
            # A credencial do master vai só no input.json do tar e não fica em secretsDir.
            foreach ($file in Get-ChildItem -LiteralPath $dir -File | Where-Object Name -notin 'args.log', 'stdin.tar', 'docker') {
                [System.IO.File]::ReadAllText($file.FullName) | Should -Not -Match 'canario-kc-env-033' -Because $file.Name
            }
        }
        finally { Remove-Item Env:DOKPOD_TEST_KC_ADMIN -ErrorAction SilentlyContinue; Remove-Item -LiteralPath $dir -Recurse -Force }
    }

    It 'sem acesso administrativo ou com versão abaixo da homologada, código 3 sem alteração: <Name>' -ForEach @(
        @{ Name = 'sem acesso'; ExitCode = 21 }, @{ Name = 'versão antiga'; ExitCode = 22 }
    ) {
        $dir = New-TestDirectory
        try {
            $answers = New-ExistingAnswers $dir
            $answers.keycloak.admin['password'] = @{ env = 'DOKPOD_TEST_KC_ADMIN' }
            $env:DOKPOD_TEST_KC_ADMIN = 'canario-kc-env-033'
            $fake = New-FakeEngine -Root $dir -ExitCode $ExitCode -Result ''
            $failure = { Invoke-ExistingKeycloakRealm -Answers $answers -Image 'img' -Cli $fake -ScriptsRoot $script:scripts } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 3
            $failure.Exception.Message | Should -Match 'Nada foi alterado'
            Test-Path (Join-Path $dir 'initial-admin-password') | Should -BeFalse
        }
        finally { Remove-Item Env:DOKPOD_TEST_KC_ADMIN -ErrorAction SilentlyContinue; Remove-Item -LiteralPath $dir -Recurse -Force }
    }
}

Describe 'Keycloak existente real com TLS de CA privada (T039, T040, SC-006)' {
    BeforeAll {
        $script:skip = -not $env:DOCKER_HOST -or -not $env:DOKPOD_TEST_SHARED -or -not (& docker image ls --quiet $script:installerImage)
        if ($script:skip) { return }

        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $script:kcNetwork = "dokpod-033-kcx-$suffix"
        $script:kcServer = "dokpod-033-kcx-$suffix"
        $script:kcRoot = Join-Path $env:DOKPOD_TEST_SHARED "t039-$suffix"
        New-Item -ItemType Directory -Path $script:kcRoot | Out-Null
        $script:adminCredential = "canario-kcx-admin-033-$suffix"

        # CA privada e certificado de sso.lab.test, como o Keycloak de uma organização.
        $caKey = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
        $caRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=dokpod-t039-ca', $caKey, 'SHA256')
        $caRequest.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true))
        $caCert = $caRequest.CreateSelfSigned([datetimeoffset]::UtcNow.AddMinutes(-5), [datetimeoffset]::UtcNow.AddDays(2))
        $leafKey = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
        $leafRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=sso.lab.test', $leafKey, 'SHA256')
        $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new(); $san.AddDnsName('sso.lab.test')
        $leafRequest.CertificateExtensions.Add($san.Build())
        $leaf = $leafRequest.Create($caCert, [datetimeoffset]::UtcNow.AddMinutes(-5), [datetimeoffset]::UtcNow.AddDays(2), [byte[]] (1..8))
        $script:caFile = Join-Path $script:kcRoot 'kc-ca.crt'
        [System.IO.File]::WriteAllText($script:caFile, $caCert.ExportCertificatePem() + "`n")
        $certFile = Join-Path $script:kcRoot 'tls.crt'; $keyFile = Join-Path $script:kcRoot 'tls.key'
        [System.IO.File]::WriteAllText($certFile, $leaf.ExportCertificatePem() + "`n")
        [System.IO.File]::WriteAllText($keyFile, $leafKey.ExportPkcs8PrivateKeyPem() + "`n")
        foreach ($file in $certFile, $keyFile) { [System.IO.File]::SetUnixFileMode($file, [System.IO.UnixFileMode]'UserRead, UserWrite, GroupRead, OtherRead') }

        & docker network create $script:kcNetwork | Out-Null
        & docker run --detach --pids-limit 2048 --name $script:kcServer --network $script:kcNetwork --network-alias sso.lab.test --log-driver none `
            --volume "$($certFile):/certs/tls.crt:ro" --volume "$($keyFile):/certs/tls.key:ro" `
            --env KC_BOOTSTRAP_ADMIN_USERNAME=admin --env "KC_BOOTSTRAP_ADMIN_PASSWORD=$($script:adminCredential)" `
            --env KC_HOSTNAME=https://sso.lab.test:8443 --env KC_HTTPS_CERTIFICATE_FILE=/certs/tls.crt --env KC_HTTPS_CERTIFICATE_KEY_FILE=/certs/tls.key `
            $script:images.keycloak start-dev | Out-Null

        function Invoke-Kcadm {
            param([string[]] $Arguments)
            $output = & docker exec $script:kcServer /opt/keycloak/bin/kcadm.sh @Arguments --config /tmp/kcadm.config 2>&1
            return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
        }

        $deadline = [datetime]::UtcNow.AddSeconds(180)
        do {
            Start-Sleep -Seconds 2
            $login = Invoke-Kcadm @('config', 'credentials', '--server', 'http://localhost:8080', '--realm', 'master', '--user', 'admin', '--password', $script:adminCredential)
        } until ($login.ExitCode -eq 0 -or [datetime]::UtcNow -gt $deadline)

        # Outro sistema no mesmo Keycloak: o realm outro com um usuário. O master e ele não podem mudar (SC-006).
        [void] (Invoke-Kcadm @('create', 'realms', '-s', 'realm=outro', '-s', 'enabled=true'))
        [void] (Invoke-Kcadm @('create', 'users', '-r', 'outro', '-s', 'username=beto', '-s', 'enabled=true'))
        $script:snapshot = {
            $master = (Invoke-Kcadm @('get', 'realms/master')).Output | ConvertFrom-Json
            $outro = (Invoke-Kcadm @('get', 'realms/outro')).Output | ConvertFrom-Json
            $clients = @(((Invoke-Kcadm @('get', 'clients', '-r', 'outro', '--fields', 'clientId')).Output | ConvertFrom-Json).clientId) -join ','
            $users = @(((Invoke-Kcadm @('get', 'users', '-r', 'master', '--fields', 'username')).Output | ConvertFrom-Json).username) -join ','
            # O lastModified... muda sozinho; o resto da representação é o que importa.
            foreach ($realm in $master, $outro) { foreach ($name in @($realm.PSObject.Properties.Name | Where-Object { $_ -match 'Timestamp|notBefore' })) { $realm.PSObject.Properties.Remove($name) } }
            return (@($master, $outro) | ConvertTo-Json -Depth 30 -Compress) + "|$clients|$users"
        }
        $script:before = & $script:snapshot
        $script:existingSecrets = Join-Path $script:kcRoot 'secrets'
        New-Item -ItemType Directory -Path $script:existingSecrets | Out-Null
        $script:credentialFile = Join-Path $script:kcRoot 'kc-admin'
        [System.IO.File]::WriteAllText($script:credentialFile, $script:adminCredential)
        $script:existingAnswers = [ordered]@{
            secretsDir = $script:existingSecrets; publicUrl = 'https://dokpod.lab.test:18443'
            keycloak = [ordered]@{ mode = 'existing'; url = 'https://sso.lab.test:8443'; realm = 'dokpod'; caCertificate = $script:caFile
                admin = [ordered]@{ username = 'admin' }; administratorUser = 'ana' }
        }
        # Chave por índice: o par chave=valor de senha não aparece no texto (T056).
        $script:existingAnswers.keycloak.admin['password'] = @{ file = $script:credentialFile }
        [void] (Initialize-KeycloakSecret -Answers $script:existingAnswers)
        $script:existingRealm = { param([bool] $configured) Invoke-ExistingKeycloakRealm -Answers $script:existingAnswers -Image $script:installerImage -Network $script:kcNetwork -ScriptsRoot $script:scripts -RealmConfigured $configured -Interactive $false }
    }

    AfterAll {
        if ($script:skip) { return }
        & docker rm --force --volumes $script:kcServer *> $null
        & docker network rm $script:kcNetwork *> $null
        Remove-Item -LiteralPath $script:kcRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'credencial errada dá código 3 sem criar o realm' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'sem socket do engine, DOKPOD_TEST_SHARED ou a imagem do instalador'; return }
        $wrong = Join-Path $script:kcRoot 'kc-admin-errada'
        [System.IO.File]::WriteAllText($wrong, 'errada-033')
        $answers = [ordered]@{} + $script:existingAnswers
        $answers.keycloak = [ordered]@{} + $script:existingAnswers.keycloak
        $answers.keycloak.admin = [ordered]@{ username = 'admin' }
        $answers.keycloak.admin['password'] = @{ file = $wrong }
        $failure = { Invoke-ExistingKeycloakRealm -Answers $answers -Image $script:installerImage -Network $script:kcNetwork -ScriptsRoot $script:scripts -Interactive $false } |
            Should -Throw -PassThru
        $failure.Exception.ExitCode | Should -Be 3
        (Invoke-Kcadm @('get', 'realms/dokpod')).ExitCode | Should -Not -Be 0
    }

    It 'cria só o realm do Dokpod pela TLS da CA privada, põe ana nos administradores e não muda master nem outro' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'sem socket do engine, DOKPOD_TEST_SHARED ou a imagem do instalador'; return }
        $state = & $script:existingRealm $false
        $state.UserStateBefore | Should -Be 'absent'
        $state.PasswordSet | Should -BeTrue
        $state.UpdatePasswordPending | Should -BeTrue
        $state.Version | Should -Match '^26\.'
        $state.InitialPasswordFile | Should -Be 'initial-admin-password'

        $user = ((Invoke-Kcadm @('get', 'users', '-r', 'dokpod', '-q', 'username=ana', '-q', 'exact=true')).Output | ConvertFrom-Json)
        $user.requiredActions | Should -Contain 'UPDATE_PASSWORD'
        ((Invoke-Kcadm @('get', "users/$($user.id)/groups", '-r', 'dokpod')).Output | ConvertFrom-Json).path | Should -Contain '/dokpod/administrators'
        $bffId = ((Invoke-Kcadm @('get', 'clients', '-r', 'dokpod', '-q', 'clientId=dokpod-bff', '--fields', 'id')).Output | ConvertFrom-Json).id
        ((Invoke-Kcadm @('get', "clients/$bffId/client-secret", '-r', 'dokpod')).Output | ConvertFrom-Json).value |
            Should -Be (Read-InstallerSecret -SecretsDirectory $script:existingSecrets -Name 'Authentication__Keycloak__ClientSecret')
        & $script:snapshot | Should -Be $script:before

        foreach ($file in Get-ChildItem -LiteralPath $script:existingSecrets -Recurse -File) {
            [System.IO.File]::ReadAllText($file.FullName) | Should -Not -Match ([regex]::Escape($script:adminCredential)) -Because $file.Name
        }
    }

    It 'redirect URI alterado à mão aparece nas divergências e a reexecução sem a credencial do master o corrige (T040)' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'sem socket do engine, DOKPOD_TEST_SHARED ou a imagem do instalador'; return }
        Get-KeycloakRealmDrift -Answers $script:existingAnswers -Image $script:installerImage -Network $script:kcNetwork -ScriptsRoot $script:scripts | Should -BeNullOrEmpty

        $bffId = ((Invoke-Kcadm @('get', 'clients', '-r', 'dokpod', '-q', 'clientId=dokpod-bff', '--fields', 'id')).Output | ConvertFrom-Json).id
        (Invoke-Kcadm @('update', "clients/$bffId", '-r', 'dokpod', '-s', 'redirectUris=["https://mudado.lab.test/signin-oidc"]')).ExitCode | Should -Be 0
        $drift = Get-KeycloakRealmDrift -Answers $script:existingAnswers -Image $script:installerImage -Network $script:kcNetwork -ScriptsRoot $script:scripts
        $drift | Should -Contain 'client dokpod-bff: redirectUris'

        Remove-Item -LiteralPath $script:credentialFile
        $state = & $script:existingRealm $true
        $state.BootstrapMode | Should -BeFalse
        ((Invoke-Kcadm @('get', "clients/$bffId", '-r', 'dokpod')).Output | ConvertFrom-Json).redirectUris | Should -Be @('https://dokpod.lab.test:18443/dokpod/signin-oidc')
        Get-KeycloakRealmDrift -Answers $script:existingAnswers -Image $script:installerImage -Network $script:kcNetwork -ScriptsRoot $script:scripts | Should -BeNullOrEmpty
        & $script:snapshot | Should -Be $script:before
    }
}
