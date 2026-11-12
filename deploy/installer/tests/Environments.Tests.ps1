#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T031 (FR-024, FR-033, D-004): primeiro ambiente. Certificado do agente, cadastro do ambiente e da identidade
# pelo modo administrativo, autorização no Keycloak em container avulso e o socket do engine só no agente. A integração
# usa PostgreSQL, Keycloak e as imagens da API (DOKPOD_TEST_API_IMAGE) e do instalador reais, com os diretórios em
# DOKPOD_TEST_SHARED, montado no mesmo caminho no host e no container dos testes, como install.sh faz.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Secrets.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/State.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Pki.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Postgres.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Keycloak.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Migrations.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Render.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Environments.psm1') -Force
    $script:catalog = Join-Path (Get-InstallerHome) 'catalog'
    $script:infrastructure = (Get-Content -LiteralPath (Join-Path (Get-InstallerHome) 'templates/infrastructure-images.json') -Raw | ConvertFrom-Json).images
    # Os scripts do checkout, quando montados em /src (pester.sh), vencem os copiados na imagem.
    $script:scripts = if (Test-Path '/src/tools/scripts/provision-environment-authorization.ps1') { '/src/tools/scripts' } else { Join-Path (Get-InstallerHome) 'keycloak' }
    $script:installerImage = if ($env:DOKPOD_INSTALLER_IMAGE_REF) { $env:DOKPOD_INSTALLER_IMAGE_REF } else { 'localhost/dokpod-installer:test' }

    AfterEach { Clear-InstallerSecrets }

    function New-TestAnswers {
        param([string] $Root)
        return [ordered]@{
            installDir = (Join-Path $Root 'install'); secretsDir = (Join-Path $Root 'secrets'); operator = 'Ana Operadora'
            publicUrl = 'https://dokpod.lab.test:18443'; engine = 'podman-rootless'
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
    }

    function New-TestInstall {
        param([string] $Base = [System.IO.Path]::GetTempPath())
        $root = Join-Path $Base "dokpod-033-env-$([guid]::NewGuid().ToString('N'))"
        $answers = New-TestAnswers -Root $root
        New-Item -ItemType Directory -Path $answers.installDir, $answers.secretsDir | Out-Null
        $state = Open-InstallerState -InstallDirectory $answers.installDir
        return [pscustomobject]@{ Root = $root; Answers = $answers; State = $state }
    }

    function Remove-TestInstall {
        param($Install)
        Close-InstallerState -State $Install.State
        Remove-Item -LiteralPath $Install.Root -Recurse -Force
    }

    # Engine falso: modo administrativo responde com env.* e id.*; o container avulso guarda o stdin e responde authz.*.
    function New-FakeEngine {
        param([string] $Root, [int] $EnvironmentCode = 0, [int] $IdentityCode = 0, [string] $Changed = '"environment:read"')
        [System.IO.File]::WriteAllText((Join-Path $Root 'env.json'), $(if ($EnvironmentCode -eq 0) { '{"environmentId":"x","created":true}' } else { '{"failureCode":"environment_conflict"}' }))
        [System.IO.File]::WriteAllText((Join-Path $Root 'env.code'), [string] $EnvironmentCode)
        [System.IO.File]::WriteAllText((Join-Path $Root 'id.json'), $(if ($IdentityCode -eq 0) { '{"created":true}' } else { '{"failureCode":"fingerprint_conflict"}' }))
        [System.IO.File]::WriteAllText((Join-Path $Root 'id.code'), [string] $IdentityCode)
        $fake = Join-Path $Root 'docker'
        $script = @"
#!/bin/sh
printf '%s\n' "`$@" >> '$Root/calls.log'
echo '----' >> '$Root/calls.log'
case " `$* " in
  *" environments register "*) cat '$Root/env.json'; exit `$(cat '$Root/env.code') ;;
  *" agent-identities register "*) cat '$Root/id.json'; exit `$(cat '$Root/id.code') ;;
  *"io.dokpod.installer.task=environment-authorization"*)
    cat > '$Root/stdin.tar'
    echo 'Provisionamento UMA concluído.'
    echo '@@dokpod-result@@{"scopes":["environment:read","environment:manage"],"changed":[$Changed]}'
    exit 0 ;;
esac
exit 99
"@
        [System.IO.File]::WriteAllText($fake, $script.Replace("`r", ''))
        [System.IO.File]::SetUnixFileMode($fake, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        return $fake
    }

    function Get-FakeCalls {
        param([string] $Root)
        if (-not (Test-Path (Join-Path $Root 'calls.log'))) { return , @() }
        return , @((Get-Content -LiteralPath (Join-Path $Root 'calls.log') -Raw) -split "----`n" | Where-Object { $_.Trim() })
    }

    function Get-TarText {
        param([string] $Path, [string] $Name)
        $reader = [System.Formats.Tar.TarReader]::new([System.IO.File]::OpenRead($Path))
        try {
            while ($null -ne ($entry = $reader.GetNextEntry())) {
                if ($entry.Name -eq $Name) { return [System.IO.StreamReader]::new($entry.DataStream).ReadToEnd() }
            }
        }
        finally { $reader.Dispose() }
    }

    function Invoke-Step {
        # Chama a etapa e devolve o resultado ou o código do InstallerException.
        param([hashtable] $Parameters)
        try { return [pscustomobject]@{ Result = (Invoke-EnvironmentStep @Parameters); Code = 0 } }
        catch {
            if ($_.Exception.PSObject.Properties['ExitCode']) { return [pscustomobject]@{ Result = $null; Code = $_.Exception.ExitCode; Message = $_.Exception.Message } }
            throw
        }
    }
}

Describe 'Certificado do agente' {
    BeforeEach {
        $script:dir = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-agent-$([guid]::NewGuid().ToString('N'))"
        $script:ca = (Initialize-LocalCertificateAuthority -SecretsDirectory $script:dir).Certificate
        $script:id = [guid]::NewGuid().ToString()
    }

    AfterEach { Remove-Item -LiteralPath $script:dir -Recurse -Force -ErrorAction SilentlyContinue }

    It 'emite clientAuth assinado pela CA local, PFX e senha 0600, e a reexecução reaproveita o mesmo fingerprint' {
        $first = Initialize-AgentCertificate -SecretsDirectory $script:dir -EnvironmentId $script:id -CertificateAuthority $script:ca
        $first.Created | Should -BeTrue
        $first.Fingerprint | Should -Match '^[0-9A-F]{64}$'
        Test-InstallerCertificate -Certificate $first.Certificate -Usage Client -TrustedRoots $script:ca -RequirePrivateKey | Should -BeNullOrEmpty
        foreach ($name in "pki/agent-$($script:id).pfx", "pki/agent-$($script:id).pass") {
            [System.IO.File]::GetUnixFileMode((Join-Path $script:dir $name)) | Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite')
        }

        $again = Initialize-AgentCertificate -SecretsDirectory $script:dir -EnvironmentId $script:id -CertificateAuthority $script:ca
        $again.Created | Should -BeFalse
        $again.Fingerprint | Should -Be $first.Fingerprint
    }

    It 'certificado de outra CA ou PFX sem senha interrompem sem tocar nos arquivos; só a senha é sobra substituída' {
        [void] (Initialize-AgentCertificate -SecretsDirectory $script:dir -EnvironmentId $script:id -CertificateAuthority $script:ca)
        $pfx = Join-Path $script:dir "pki/agent-$($script:id).pfx"
        $bytes = [System.IO.File]::ReadAllBytes($pfx)
        { Initialize-AgentCertificate -SecretsDirectory $script:dir -EnvironmentId $script:id -CertificateAuthority (New-LocalCertificateAuthority) } |
            Should -Throw "*recreate pki:agent:$($script:id)*"
        [System.IO.File]::ReadAllBytes($pfx) | Should -Be $bytes

        Remove-Item -LiteralPath (Join-Path $script:dir "pki/agent-$($script:id).pass")
        { Initialize-AgentCertificate -SecretsDirectory $script:dir -EnvironmentId $script:id -CertificateAuthority $script:ca } | Should -Throw '*sem*.pass*'

        Remove-Item -LiteralPath $pfx
        [System.IO.File]::WriteAllText((Join-Path $script:dir "pki/agent-$($script:id).pass"), 'sobra')
        (Initialize-AgentCertificate -SecretsDirectory $script:dir -EnvironmentId $script:id -CertificateAuthority $script:ca).Created | Should -BeTrue
    }

    It 'perto do vencimento (menos de renewWarningDays) só avisa e reaproveita' {
        $first = Initialize-AgentCertificate -SecretsDirectory $script:dir -EnvironmentId $script:id -CertificateAuthority $script:ca
        $late = Initialize-AgentCertificate -SecretsDirectory $script:dir -EnvironmentId $script:id -CertificateAuthority $script:ca -Now ([DateTimeOffset]::UtcNow.AddDays(380))
        $late.Created | Should -BeFalse
        $late.Fingerprint | Should -Be $first.Fingerprint
    }
}

Describe 'Socket do engine só no agente (FR-024)' {
    BeforeAll {
        $script:images = [ordered]@{ api = 'api@sha256:1'; bff = 'bff@sha256:2'; web = 'web@sha256:3'; agent = 'agent@sha256:4' }
        $script:infra = [ordered]@{ postgresql = 'pg@sha256:5'; keycloak = 'kc@sha256:6'; nginx = 'nginx@sha256:7'; cosign = 'cosign@sha256:8' }
        $script:socket = '/run/user/1000/podman/podman.sock'
        function New-TestModel {
            $answers = New-TestAnswers -Root ([System.IO.Path]::GetTempPath())
            $answers['edge'] = @{ mode = 'provision'; httpsPort = 8443 }
            return New-InstallerComposeModel -Answers $answers -Images $script:images -Infrastructure $script:infra -EngineSocket $script:socket -LocalEnvironmentId ([guid]::NewGuid().ToString())
        }
    }

    It 'o compose gerado passa: só o agent monta o socket, sem porta e só na rede interna' {
        { Assert-EngineSocketIsolation -Model (New-TestModel) -EngineSocket $script:socket } | Should -Not -Throw
    }

    It 'recusa socket em outro serviço, porta no agent ou agent fora da rede interna' {
        $model = New-TestModel
        $model.services.api.volumes += "$($script:socket):/var/run/docker.sock"
        { Assert-EngineSocketIsolation -Model $model -EngineSocket $script:socket } | Should -Throw '*serviço api monta o socket*'

        $model = New-TestModel
        $model.services.agent['ports'] = @('7000:7000')
        { Assert-EngineSocketIsolation -Model $model -EngineSocket $script:socket } | Should -Throw '*agent publica porta*'

        $model = New-TestModel
        $model.services.agent.networks = @('dokpod-internal', 'dokpod-edge')
        { Assert-EngineSocketIsolation -Model $model -EngineSocket $script:socket } | Should -Throw '*redes dokpod-internal, dokpod-edge*'
    }
}

Describe 'Etapa do primeiro ambiente (engine falso)' {
    BeforeEach {
        $script:install = New-TestInstall
        $script:fakeRoot = Join-Path $script:install.Root 'fake'
        New-Item -ItemType Directory -Path $script:fakeRoot | Out-Null
        Write-InstallerSecret -SecretsDirectory $script:install.Answers.secretsDir -Name 'dokpod-owner-connection' -Value 'Host=dokpod-postgres;Username=dokpod_owner;Password=canario-owner-031'
        Write-InstallerSecret -SecretsDirectory $script:install.Answers.secretsDir -Name 'keycloak-provisioner-client-secret' -Value 'canario-provisioner-031'
        $script:base = @{ Answers = $script:install.Answers; State = $script:install.State; ApiImage = 'localhost/dokpod-api:t'; InstallerImage = 'localhost/dokpod-installer:t'; ScriptsRoot = $script:scripts }
    }

    AfterEach { Remove-TestInstall $script:install }

    It 'cadastra ambiente e identidade com o operador, autoriza os administradores e grava o estado sem segredo' {
        $cli = New-FakeEngine -Root $script:fakeRoot
        $env:DOKPOD_HOST_NAME = 'servidor-01'
        try { $step = Invoke-Step ($script:base + @{ Cli = $cli }) }
        finally { Remove-Item Env:DOKPOD_HOST_NAME }
        $step.Code | Should -Be 0
        $id = $step.Result.EnvironmentId
        $step.Result.EnvironmentCreated | Should -BeTrue
        $step.Result.CaCreated | Should -BeTrue
        $step.Result.CertificateCreated | Should -BeTrue
        $step.Result.IdentityCreated | Should -BeTrue
        $step.Result.AuthorizationChanged | Should -Be @('environment:read')

        $calls = Get-FakeCalls $script:fakeRoot
        $calls.Count | Should -Be 3
        $calls[0] | Should -Match "(?s)environments\nregister\n--id\n$id\n--name\nhost-local\n--host\nservidor-01\n--stage\nproduction\n--actor\nAna Operadora\n"
        $calls[1] | Should -Match 'io.dokpod.installer.task=environment-authorization'
        $calls[1] | Should -Match '(?m)^dokpod_keycloak-data$'
        $calls[2] | Should -Match "(?s)agent-identities\nregister\n--environment\n$id\n--fingerprint\n$($step.Result.Fingerprint)\n"
        foreach ($call in $calls) { $call | Should -Not -Match 'canario-' }

        $sidecarInput = Get-TarText -Path (Join-Path $script:fakeRoot 'stdin.tar') -Name 'input.json' | ConvertFrom-Json
        $sidecarInput.environmentId | Should -Be $id
        $sidecarInput.groupPath | Should -Be '/dokpod/administrators'
        $sidecarInput.internalHttp | Should -BeTrue
        $sidecarInput.secrets.provisionerSecret | Should -Be 'canario-provisioner-031'

        $rows = Invoke-StateSql -State $script:install.State -Name 'environments-list' -Rows
        $row = $rows[0]
        $row['environment_id'] | Should -Be $id
        $row['engine'] | Should -Be 'podman'
        $row['agent_fingerprint'] | Should -Be $step.Result.Fingerprint
        $certificates = Invoke-StateSql -State $script:install.State -Name 'certificates-list' -Rows
        $roles = @($certificates | ForEach-Object { $_['role'] })
        $roles | Should -Contain 'ca'
        $roles | Should -Contain "agent:$id"
    }

    It 'reexecução usa o mesmo ambiente e o mesmo certificado' {
        $cli = New-FakeEngine -Root $script:fakeRoot
        $first = Invoke-Step ($script:base + @{ Cli = $cli })
        $again = Invoke-Step ($script:base + @{ Cli = $cli })
        $again.Code | Should -Be 0
        $again.Result.EnvironmentId | Should -Be $first.Result.EnvironmentId
        $again.Result.Fingerprint | Should -Be $first.Result.Fingerprint
        $again.Result.CaCreated | Should -BeFalse
        $again.Result.CertificateCreated | Should -BeFalse
    }

    It 'conflito do modo administrativo (código 8) interrompe com a ação e não chega ao Keycloak' {
        $cli = New-FakeEngine -Root $script:fakeRoot -EnvironmentCode 8
        $step = Invoke-Step ($script:base + @{ Cli = $cli })
        $step.Code | Should -Be 1
        $step.Message | Should -Match 'environment_conflict'
        $step.Message | Should -Match 'localEnvironment'
        (Get-FakeCalls $script:fakeRoot).Count | Should -Be 1

        $cli = New-FakeEngine -Root $script:fakeRoot -IdentityCode 8
        $step = Invoke-Step ($script:base + @{ Cli = $cli })
        $step.Code | Should -Be 1
        $step.Message | Should -Match 'recreate pki:agent:'
        (Invoke-StateSql -State $script:install.State -Name 'environments-list' -Rows).Count | Should -Be 0
    }
}

Describe 'Primeiro ambiente contra PostgreSQL, Keycloak e as imagens reais' {
    BeforeAll {
        $script:skip = -not ($env:DOCKER_HOST -and $env:DOKPOD_TEST_SHARED -and $env:DOKPOD_TEST_API_IMAGE) -or -not (& docker image ls --quiet $script:installerImage)
        if ($script:skip) { return }

        $script:api = $env:DOKPOD_TEST_API_IMAGE
        $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
        $script:network = "dokpod-033-env-$suffix"
        $script:server = "dokpod-033-env-pg-$suffix"
        $script:keycloak = "dokpod-033-env-kc-$suffix"
        $script:adminPassword = "canario-env-admin-033-$suffix"
        $script:install = New-TestInstall -Base $env:DOKPOD_TEST_SHARED
        $answers = $script:install.Answers
        [void] (Initialize-KeycloakSecret -Answers $answers)
        $bootstrap = Read-InstallerSecret -SecretsDirectory $answers.secretsDir -Name 'keycloak-bootstrap-admin'

        & docker network create --internal $script:network | Out-Null
        & docker run --detach --name $script:server --network $script:network --network-alias dokpod-postgres --log-driver none `
            --env POSTGRES_USER=dokpod_admin --env POSTGRES_DB=postgres --env "POSTGRES_PASSWORD=$($script:adminPassword)" $script:infrastructure.postgresql | Out-Null
        & docker run --detach --name $script:keycloak --network $script:network --network-alias keycloak --log-driver none `
            --env KC_BOOTSTRAP_ADMIN_USERNAME=dokpod-bootstrap --env "KC_BOOTSTRAP_ADMIN_PASSWORD=$bootstrap" `
            --env KC_HOSTNAME=https://dokpod.lab.test:18443 --env KC_HTTP_ENABLED=true --env KC_PROXY_HEADERS=xforwarded `
            $script:infrastructure.keycloak start-dev | Out-Null
        $deadline = [datetime]::UtcNow.AddSeconds(90)
        do {
            Start-Sleep -Milliseconds 500
            & docker exec $script:server pg_isready -h 127.0.0.1 -U dokpod_admin -d postgres *> $null
        } until ($LASTEXITCODE -eq 0 -or [datetime]::UtcNow -gt $deadline)

        # Banco com todas as migrations, pelo bootstrap da T028 e o apply da T030.
        $server = [ordered]@{ Host = 'dokpod-postgres'; Port = 5432; Username = 'dokpod_admin'; Password = $script:adminPassword }
        Invoke-PostgresBootstrap -SecretsDirectory $answers.secretsDir -Image $script:infrastructure.postgresql -Network $script:network -Server $server -CatalogRoot $script:catalog | Out-Null
        $common = @{ Image = $script:api; Network = $script:network; SecretsDirectory = $answers.secretsDir }
        $plan = Invoke-AdminCommand @common -Arguments @('migrations', 'plan')
        (Invoke-AdminCommand @common -Arguments @('migrations', 'apply', '--confirm', $plan.Data['planHash'])).ExitCode | Should -Be 0
        [void] (Invoke-KeycloakRealm -Answers $answers -Image $script:installerImage -Network $script:network -ScriptsRoot $script:scripts)

        $script:step = @{ Answers = $answers; State = $script:install.State; ApiImage = $script:api; InstallerImage = $script:installerImage
            Network = $script:network; KeycloakNetwork = $script:network; ScriptsRoot = $script:scripts }

        function Invoke-AdminPsql {
            param([string] $Sql)
            # Socket local do container, que a imagem do PostgreSQL aceita sem senha.
            $output = & docker exec $script:server psql -At -U dokpod_admin -d dokpod -c $Sql 2>&1
            return ($output -join "`n").Trim()
        }

        function Invoke-Kcadm {
            param([string[]] $Arguments)
            $output = & docker exec $script:keycloak /opt/keycloak/bin/kcadm.sh @Arguments --config /tmp/kcadm.config 2>&1
            return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
        }
    }

    AfterAll {
        if ($script:skip) { return }
        & docker rm --force --volumes $script:server $script:keycloak *> $null
        & docker network rm $script:network *> $null
        Remove-TestInstall $script:install
    }

    It 'cadastra com auditoria, autoriza os administradores em todos os scopes e a reexecução não muda nada' {
        if ($script:skip) { Set-ItResult -Skipped -Because 'engine, DOKPOD_TEST_SHARED, DOKPOD_TEST_API_IMAGE ou a imagem do instalador ausentes'; return }

        $first = Invoke-Step $script:step
        $first.Code | Should -Be 0 -Because $first.Message
        $id = $first.Result.EnvironmentId
        $first.Result.EnvironmentCreated | Should -BeTrue
        $first.Result.IdentityCreated | Should -BeTrue
        $first.Result.AuthorizationChanged.Count | Should -Be 17

        Invoke-AdminPsql "SELECT name || '|' || host FROM dokpod.environments WHERE environment_id = '$id'" | Should -Be 'host-local|dokpod.lab.test'
        Invoke-AdminPsql "SELECT upper(certificate_fingerprint) FROM dokpod.agent_identities WHERE environment_id = '$id' AND revoked_at_utc IS NULL" |
            Should -Be $first.Result.Fingerprint
        Invoke-AdminPsql "SELECT string_agg(action, ',' ORDER BY action) FROM dokpod.audit_events WHERE environment_id = '$id'" |
            Should -Be 'environment.agent_identity_register,environment.register'

        $secret = Read-InstallerSecret -SecretsDirectory $script:install.Answers.secretsDir -Name 'keycloak-provisioner-client-secret'
        (Invoke-Kcadm @('config', 'credentials', '--server', 'http://localhost:8080', '--realm', 'dokpod', '--client', 'dokpod-provisioner', '--secret', $secret)).ExitCode | Should -Be 0
        $apiId = ((Invoke-Kcadm @('get', 'clients', '-r', 'dokpod', '-q', 'clientId=dokpod-api', '--fields', 'id')).Output | ConvertFrom-Json).id
        $resource = @((Invoke-Kcadm @('get', "clients/$apiId/authz/resource-server/resource", '-r', 'dokpod', '-q', "name=urn:dokpod:environment:$id", '-q', 'exactName=true')).Output | ConvertFrom-Json)[0]
        @($resource.scopes).Count | Should -Be 17
        $policy = @((Invoke-Kcadm @('get', "clients/$apiId/authz/resource-server/policy", '-r', 'dokpod', '-q', "name=environment-$id-group-administrators", '-q', 'exact=true')).Output | ConvertFrom-Json)
        $policy.Count | Should -Be 1

        $again = Invoke-Step $script:step
        $again.Code | Should -Be 0 -Because $again.Message
        $again.Result.EnvironmentId | Should -Be $id
        $again.Result.EnvironmentCreated | Should -BeFalse
        $again.Result.IdentityCreated | Should -BeFalse
        $again.Result.AuthorizationChanged.Count | Should -Be 0
        Invoke-AdminPsql "SELECT count(*) FROM dokpod.audit_events WHERE environment_id = '$id'" | Should -Be '2'
    }
}
