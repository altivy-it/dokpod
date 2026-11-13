#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T033 (FR-027, SC-003, SC-004): retomada e reexecução da instalação do cenário 1 com engine real. A execução é
# interrompida em cada etapa (pki, banco, migrations, realm, ambiente, serviços) e retomada com o mesmo comando; no fim,
# o estado não tem recurso duplicado e uma reexecução com as mesmas respostas dá o plano todo keep, outcome converged
# em até 2 minutos e nenhuma mudança em containers, segredos, certificados, banco ou realm.
#
# As interrupções vêm de um invólucro do docker que falha uma vez no primeiro comando que casa com o padrão da etapa;
# a da pki simula a queda entre as duas gravações de um par (só a chave, ou só a senha do PFX, gravada).
# Mesmas variáveis de Install.Tests.ps1 (DOKPOD_TEST_FULL_INSTALL=1 e as imagens desta branch).

BeforeAll {
    foreach ($module in 'Common', 'Secrets', 'State', 'Install') {
        Import-Module (Join-Path $PSScriptRoot "../src/$module.psm1") -Force
    }
    $script:enabled = $env:DOKPOD_TEST_FULL_INSTALL -eq '1' -and $env:DOCKER_HOST -and $env:DOKPOD_TEST_SHARED -and $env:DOKPOD_HOST_ENGINE_SOCKET
    $script:project = 'dokpod-t033'

    function Invoke-Captured {
        param([scriptblock] $Action)
        $writer = [System.IO.StringWriter]::new()
        $previous = [Console]::Out
        [Console]::SetOut($writer)
        $started = [System.Diagnostics.Stopwatch]::StartNew()
        try { $result = try { & $Action } catch { $_.Exception } }
        finally { [Console]::SetOut($previous) }
        return [pscustomobject]@{ Result = $result; Output = $writer.ToString(); Seconds = $started.Elapsed.TotalSeconds }
    }

    function Get-Sql {
        param([string] $Container, [string] $User, [string] $Database, [string] $Sql)
        $output = & docker exec $Container psql -At -U $User -d $Database -c $Sql 2>&1
        if ($LASTEXITCODE -ne 0) { throw "psql em $Container falhou: $output" }
        return (@($output) -join "`n").Trim()
    }

    # Retrato do que SC-003 compara: containers, segredos, certificados, banco do Dokpod e banco do Keycloak.
    function Get-InstallSnapshot {
        $secrets = [ordered]@{}
        foreach ($file in (Get-ChildItem -LiteralPath $script:answers.secretsDir -Recurse -File | Sort-Object FullName)) {
            $secrets[$file.FullName.Substring($script:answers.secretsDir.Length)] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
        $state = Open-InstallerState -InstallDirectory $script:answers.installDir
        try {
            $rows = Invoke-StateSql -State $state -Name 'certificates-list' -Rows
            $certificates = @($rows | ForEach-Object { "$($_['role'])=$($_['fingerprint_sha256'])" } | Sort-Object)
        }
        finally { Close-InstallerState -State $state }
        $dokpod = "$($script:project)-dokpod-postgres-1"
        $keycloak = "$($script:project)-keycloak-postgres-1"
        return [ordered]@{
            containers   = @(& docker ps --all --filter "label=com.docker.compose.project=$($script:project)" --format '{{.ID}} {{.Names}}' | Sort-Object)
            secrets      = $secrets
            certificates = $certificates
            dokpod       = Get-Sql $dokpod 'dokpod_admin' 'dokpod' (
                "select (select count(*) from dokpod.environments), (select count(*) from dokpod.agent_identities), " +
                "(select count(*) from dokpod.audit_events), (select count(*) from dokpod.""__EFMigrationsHistory""), " +
                "(select count(*) from pg_roles where rolname like 'dokpod%'), (select count(*) from pg_database where datname = 'dokpod')")
            keycloak     = Get-Sql $keycloak 'keycloak' 'keycloak' (
                "select (select count(*) from realm), (select count(*) from client), (select count(*) from user_entity), " +
                "(select count(*) from credential), (select count(*) from keycloak_group), (select count(*) from resource_server_resource), " +
                "(select count(*) from resource_server_policy), (select count(*) from resource_server_scope)")
        }
    }
}

Describe 'Retomada e reexecução do cenário 1' {
    BeforeAll {
        if (-not $script:enabled) { return }
        $script:root = Join-Path $env:DOKPOD_TEST_SHARED "t033-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $script:answers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18444'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $script:root 'install'); secretsDir = (Join-Path $script:root 'secrets'); manualMigrations = $false
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 18444 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        $script:images = [ordered]@{
            api = $env:DOKPOD_TEST_API_IMAGE; bff = $env:DOKPOD_TEST_BFF_IMAGE; web = $env:DOKPOD_TEST_WEB_IMAGE; agent = $env:DOKPOD_TEST_AGENT_IMAGE
        }
        $script:infra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images

        # Invólucro do docker: falha uma vez no primeiro comando que casa com o padrão de $faults/fail-*; o resto passa.
        $script:faults = Join-Path $script:root 'faults'
        New-Item -ItemType Directory -Path $script:faults -Force | Out-Null
        $script:cli = Join-Path $script:faults 'docker'
        [System.IO.File]::WriteAllText($script:cli, @"
#!/bin/sh
args="`$*"
for rule in '$($script:faults)'/fail-*; do
  [ -f "`$rule" ] || continue
  pattern=`$(cat "`$rule")
  case "`$args" in *"`$pattern"*) rm -f "`$rule"; echo "interrupção simulada: `$pattern" >&2; exit 1 ;; esac
done
exec docker "`$@"
"@.Replace("`r", ''))
        [System.IO.File]::SetUnixFileMode($script:cli, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')

        $script:confirmations = @{ Plan = $true }
        $script:install = {
            param([string] $Fault)
            if ($Fault) { [System.IO.File]::WriteAllText((Join-Path $script:faults "fail-$([guid]::NewGuid().ToString('N'))"), $Fault) }
            Invoke-Captured {
                Invoke-InstallerInstall -Answers $script:answers -Images $script:images -Infrastructure $script:infra -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
                    -Confirmations $script:confirmations -Interactive $false -Project $script:project -WaitTimeoutSeconds 420 -Cli $script:cli
            }
        }
        $script:lastSteps = {
            $state = Open-InstallerState -InstallDirectory $script:answers.installDir
            try {
                $runs = Invoke-StateSql -State $state -Name 'runs-expired' -Parameters @{ keep = 0 } -Rows
                $last = $runs | ForEach-Object { (Invoke-StateSql -State $state -Name 'run-get' -Parameters @{ run_id = $_['run_id'] } -Rows)[0] } |
                    Sort-Object { $_['started_at'] } | Select-Object -Last 1
                $steps = Invoke-StateSql -State $state -Name 'run-steps-list' -Parameters @{ run_id = $last['run_id'] } -Rows
                $lock = Invoke-StateSql -State $state -Name 'lock-get' -Rows
                return [pscustomobject]@{ Outcome = $last['outcome']; Steps = @($steps | ForEach-Object { "$($_['step'])=$($_['status'])" }); Locked = $lock.Count -gt 0 }
            }
            finally { Close-InstallerState -State $state }
        }

        $script:runs = [ordered]@{}
        $script:runs['database'] = & $script:install 'io.dokpod.installer.task=psql'
        $script:runs['database-steps'] = & $script:lastSteps
        # Queda no meio da pki: a chave da borda sem o certificado e a senha do PFX da API sem o PFX.
        Remove-Item -LiteralPath (Join-Path $script:answers.secretsDir 'pki/edge.crt'), (Join-Path $script:answers.secretsDir 'pki/api.pfx')
        $script:runs['migrations-hash'] = & $script:install $null
        $script:confirmations['MigrationsPlanHash'] = [regex]::Match($script:runs['migrations-hash'].Result.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
        $script:runs['migrations'] = & $script:install 'admin migrations apply'
        $script:runs['realm'] = & $script:install 'io.dokpod.installer.task=keycloak'
        $script:runs['environment'] = & $script:install 'agent-identities register'
        $script:runs['environment-steps'] = & $script:lastSteps
        $script:runs['services'] = & $script:install 'api bff web'
        $script:runs['final'] = & $script:install $null
        $script:before = Get-InstallSnapshot
        $script:runs['rerun'] = & $script:install $null
        $script:after = Get-InstallSnapshot
        foreach ($name in @($script:runs.Keys | Where-Object { $_ -notmatch '-steps$' })) {
            $value = $script:runs[$name].Result
            Write-Host "execução $name ($([int] $script:runs[$name].Seconds) s): $(if ($value -is [System.Exception]) { "$($value.ExitCode) $($value.Message)" } else { $value })"
        }
    }

    AfterAll {
        if (-not $script:enabled) { return }
        & docker compose --file (Join-Path $script:answers.installDir 'compose.yaml') --project-name $script:project down --volumes 2>$null | Out-Null
        Remove-Item -LiteralPath $script:root -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'cada interrupção sai com código 1, registra a etapa como failed e libera a trava' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        foreach ($name in 'database', 'migrations', 'realm', 'environment', 'services') {
            "$name=$($script:runs[$name].Result.ExitCode)" | Should -Be "$name=1"
            $script:runs[$name].Result.Message | Should -Match 'interrupção simulada'
        }
        $script:runs['database-steps'].Steps | Should -Be @('secrets=done', 'pki=done', 'config=done', 'services:data=done', 'database=failed')
        $script:runs['database-steps'].Outcome | Should -Be 'failed'
        $script:runs['database-steps'].Locked | Should -BeFalse
        $script:runs['environment-steps'].Steps[-1] | Should -Be 'environment=failed'
        $script:runs['migrations-hash'].Result.ExitCode | Should -Be 4
    }

    It 'a retomada converge: código 0 e os certificados interrompidos na pki foram refeitos e validados' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $script:runs['final'].Result | Should -Be 0
        foreach ($name in 'pki/edge.crt', 'pki/edge.key', 'pki/api.pfx', 'pki/api.pass') {
            Test-InstallerSecret -SecretsDirectory $script:answers.secretsDir -Name $name | Should -BeTrue
        }
        $lines = & docker compose --file (Join-Path $script:answers.installDir 'compose.yaml') --project-name $script:project ps --all --format json
        $services = @($lines | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
        $services.Count | Should -Be 8
        foreach ($service in $services) {
            "$($service.Service)=$($service.State)/$($service.Health)" | Should -Match "^$($service.Service)=running/(healthy)?$"
        }
    }

    It 'sem recurso duplicado: um banco, um ambiente, uma identidade, um realm e um registro de auditoria por cadastro (SC-004)' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $dokpod = "$($script:project)-dokpod-postgres-1"
        Get-Sql $dokpod 'dokpod_admin' 'postgres' "select count(*) from pg_database where datname = 'dokpod'" | Should -Be '1'
        Get-Sql $dokpod 'dokpod_admin' 'dokpod' 'select count(*) from dokpod.environments' | Should -Be '1'
        Get-Sql $dokpod 'dokpod_admin' 'dokpod' 'select count(*) from dokpod.agent_identities' | Should -Be '1'
        Get-Sql $dokpod 'dokpod_admin' 'dokpod' ("select string_agg(action || '=' || n, ',' order by action) from (select action, count(*) n " +
            "from dokpod.audit_events where action like 'environment.%' group by action) t") |
            Should -Be 'environment.agent_identity_register=1,environment.register=1'
        $keycloak = "$($script:project)-keycloak-postgres-1"
        Get-Sql $keycloak 'keycloak' 'keycloak' "select count(*) from realm where name = 'dokpod'" | Should -Be '1'
        Get-Sql $keycloak 'keycloak' 'keycloak' "select count(*) from user_entity where username = 'dokpod-admin'" | Should -Be '1'
        Get-Sql $keycloak 'keycloak' 'keycloak' "select count(*) from client where client_id = 'dokpod-bff'" | Should -Be '1'
        Get-Sql $keycloak 'keycloak' 'keycloak' "select count(*) from user_entity where username = 'dokpod-bootstrap'" | Should -Be '0'
    }

    It 'a reexecução dá o plano todo keep, outcome converged em até 2 minutos e não muda nada (SC-003)' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $rerun = $script:runs['rerun']
        $rerun.Result | Should -Be 0
        $rerun.Seconds | Should -BeLessThan 120
        $actions = @([regex]::Matches($rerun.Output, '(?m)^([a-z:-]+)\s+(create|update|keep|recreate|remove)\s') | ForEach-Object { "$($_.Groups[1].Value)=$($_.Groups[2].Value)" })
        $actions.Count | Should -Be 12
        @($actions | Where-Object { $_ -notmatch '=keep$' }) | Should -BeNullOrEmpty
        (& $script:lastSteps).Outcome | Should -Be 'converged'

        $script:after.containers | Should -Be $script:before.containers
        $script:after.certificates | Should -Be $script:before.certificates
        $script:after.dokpod | Should -Be $script:before.dokpod
        $script:after.keycloak | Should -Be $script:before.keycloak
        @($script:after.secrets.Keys) | Should -Be @($script:before.secrets.Keys)
        foreach ($key in $script:before.secrets.Keys) { "$key=$($script:after.secrets[$key])" | Should -Be "$key=$($script:before.secrets[$key])" }
    }
}
