#Requires -Version 7.4
<#
.SYNOPSIS
Comando install (spec 033 T032, US1): instalação completa num host vazio, com PostgreSQL, Keycloak e borda provisionados
(cenário 1 do quickstart).

.DESCRIPTION
Ordem das etapas, cada uma registrada no estado só depois de terminar (FR-027):
1. secrets: senhas dos bancos e segredos do Keycloak que faltam;
2. pki: CA local, certificados da borda, da API e do agente do ambiente local, e o do key ring do BFF;
3. config: compose.yaml, dokpod.env, deploy.env, borda e CAs em installDir, com o socket do engine só no agente;
4. services:data: dokpod-postgres e keycloak-postgres;
5. database: banco, roles e conexões pelo catálogo SQL;
6. migrations: pelo modo administrativo da API, com as confirmações de FR-011, FR-036 e FR-041;
7. license: só na edição Business (FR-031);
8. services:identity e realm: Keycloak e a reconciliação do realm;
9. environment: o ambiente local e a identidade do agente;
10. services:application, services:edge e services:agent: API, BFF e web; borda (reiniciada quando web, BFF ou
    Keycloak foram recriados e ela não, porque o nginx guarda o IP resolvido na partida); agente.

Cada etapa services:* dá antes aos segredos montados o dono do usuário do serviço que os lê (D-015).

Toda etapa converge na reexecução; uma falha registra a etapa, o relatório e o código, e a próxima execução retoma.
PostgreSQL, nginx ou Keycloak existentes ficam com as tarefas das histórias 2 e 3 (T035 a T039).
#>
Set-StrictMode -Version Latest
foreach ($module in 'Common', 'Secrets', 'State', 'Plan', 'Render', 'Pki', 'Postgres', 'Migrations', 'Keycloak', 'Environments', 'Services') {
    Import-Module (Join-Path $PSScriptRoot "$module.psm1")
}

function Assert-ProvisionedScenario {
    param([System.Collections.IDictionary] $Answers)

    $existing = @()
    if ($Answers.postgres.mode -ne 'provision') { $existing += "postgres.mode '$($Answers.postgres.mode)'" }
    if ($Answers.keycloak.mode -ne 'provision') { $existing += "keycloak.mode '$($Answers.keycloak.mode)'" }
    if ($Answers.edge.mode -ne 'provision') { $existing += "edge.mode '$($Answers.edge.mode)'" }
    if ($existing.Count -gt 0) {
        Stop-Installer -Code Usage -Message ("Esta versão do instalador só aplica a instalação com PostgreSQL, Keycloak e borda provisionados; " +
            "$($existing -join ', ') fica com as tarefas T035 a T039 da spec 033. Use 'plan' para ver o plano.")
    }
}

function Get-TrustMaterial {
    <#
    O que cada serviço confia: certs/ca.crt valida a API no agente (CA local ou raízes da organização), certs/agents-ca.crt
    valida os agentes na API (sempre a CA local) e o bundle de SSL_CERT_FILE da API e do BFF soma as CAs da instalação.
    #>
    param([System.Collections.IDictionary] $Answers, $Ca, $Edge, $Api)

    $caPem = ConvertTo-CertificatePem -Certificate $Ca
    $serverCaPem = if ($Api.Origin -eq 'organization' -and $Api.Chain.Count -gt 0) {
        ($Api.Chain | ForEach-Object { ConvertTo-CertificatePem -Certificate $_ }) -join ''
    }
    else { $caPem }
    $extra = @($caPem) + @(@($Edge.Chain) + @($Api.Chain) | ForEach-Object { ConvertTo-CertificatePem -Certificate $_ })
    $bundle = if (Test-CaBundleNeeded -Answers $Answers) { New-CaBundle -ExtraPem $extra } else { $null }
    return [pscustomobject]@{ ServerCaPem = $serverCaPem; AgentsCaPem = $caPem; CaBundle = $bundle }
}

function Confirm-InstallPlan {
    param($Plan, [System.Collections.IDictionary] $Confirmations, [bool] $Interactive, [scriptblock] $Prompt)

    if (Test-InstallerPlanConverged -Plan $Plan) { return }
    if ($Confirmations.Contains('Plan') -and $Confirmations.Plan) { return }
    if (-not $Interactive) {
        Stop-Installer -Code Declined -Message 'O plano precisa de confirmação: use --yes no modo não interativo. Nada foi alterado.'
    }

    if ((& $Prompt "Digite 'aplicar' para aplicar o plano") -ne 'aplicar') {
        Stop-Installer -Code Declined -Message 'Plano não confirmado. Nada foi alterado.'
    }
}

function Invoke-InstallStep {
    # Os nomes dos parâmetros não podem coincidir com variáveis que as ações leem do install, pelo escopo dinâmico.
    param($StepState, [string] $StepRunId, [string] $StepName, [scriptblock] $StepAction)

    $script:CurrentStep = $StepName
    Write-InstallerLine -Message "Etapa $StepName..."
    $stepResult = & $StepAction
    Add-InstallerRunStep -State $StepState -RunId $StepRunId -Step $StepName
    $script:CurrentStep = $null
    return $stepResult
}

function Invoke-InstallerInstall {
    <#
    .SYNOPSIS
    Aplica a instalação do cenário 1 e devolve o código de saída do contrato (0, ou 10 na pausa para migration manual).
    .PARAMETER Images
    Imagens do Dokpod da edição (api, bff, web, agent), já verificadas pelo chamador.
    .PARAMETER Infrastructure
    Imagens de infraestrutura (postgresql, keycloak, nginx), fixadas por digest.
    .PARAMETER InstallerImage
    A imagem do instalador em execução, usada nos containers avulsos do Keycloak.
    .PARAMETER Confirmations
    As confirmações da linha de comando (Cli.psm1): Plan (--yes), MigrationsPlanHash, SqlOverrides e Backup.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Images,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Infrastructure,
        [Parameter(Mandatory)][string] $InstallerImage,
        [string] $EngineSocket = ($env:DOKPOD_HOST_ENGINE_SOCKET ?? '/run/engine.sock'),
        [System.Collections.IDictionary] $Confirmations = @{},
        [string] $SqlDirectory,
        [switch] $ManualMigrations,
        [switch] $ConfirmStaleLock,
        [int] $WaitTimeoutSeconds = 300,
        # Só para testes num host que já tem outro projeto compose 'dokpod'; as redes internas seguem o nome.
        [string] $Project = 'dokpod',
        [bool] $Interactive = (Test-InteractiveSession),
        [scriptblock] $Prompt = { param($message) Read-Host -Prompt $message },
        [string] $Cli = 'docker'
    )

    Assert-ProvisionedScenario -Answers $Answers
    [void] [System.IO.Directory]::CreateDirectory($Answers.installDir)
    Initialize-SecretsDirectory -Path $Answers.secretsDir
    $dataNetwork = "$($Project)_dokpod-data"
    $keycloakNetwork = "$($Project)_keycloak-data"
    $observed = Get-ObservedServices -Project $Project -Cli { param([string[]] $arguments) $output = & $Cli @arguments 2>$null; if ($LASTEXITCODE -eq 0) { $output -join "`n" } }
    $state = Open-InstallerState -InstallDirectory $Answers.installDir -ExistingInstallationDetected:($observed.Count -gt 0)
    $runId = [guid]::NewGuid().ToString()
    $locked = $false
    $script:CurrentStep = $null
    try {
        $containerName = if ($env:HOSTNAME) { $env:HOSTNAME } else { "dokpod-installer-$PID" }
        Enter-InstallerRunLock -State $state -RunId $runId -ContainerName $containerName -ConfirmStaleRelease:$ConfirmStaleLock `
            -ContainerExists { param($name) $null = & $Cli inspect --format '{{.Id}}' $name 2>$null; $LASTEXITCODE -eq 0 }
        $locked = $true
        [void] (Start-InstallerRun -State $state -Command 'install' -RunId $runId)

        $environment = Resolve-LocalEnvironment -Answers $Answers -State $state
        $model = New-InstallerComposeModel -Answers $Answers -Images $Images -Infrastructure $Infrastructure -EngineSocket $EngineSocket -LocalEnvironmentId $environment.Id
        $model.name = $Project
        $hashes = Set-ComposeConfigHashLabels -Model $model
        $desired = Get-DesiredComponents -Answers $Answers -ServiceHashes $hashes
        foreach ($component in 'secrets', 'pki', 'realm', 'migrations') { $observed[$component] = @{ Exists = $false; Pending = $null } }
        $observed['secrets'].Exists = Test-Path -LiteralPath (Join-Path $Answers.secretsDir 'Authentication__Keycloak__ClientSecret')
        $observed['pki'].Exists = Test-Path -LiteralPath (Join-Path $Answers.installDir 'certs/ca.crt')
        $components = Invoke-StateSql -State $state -Name 'components-list' -Rows
        foreach ($row in $components) { if ($row['name'] -in 'realm', 'migrations') { $observed[$row['name']].Exists = $true } }
        $plan = New-InstallerPlan -Desired $desired -Observed $observed
        $converged = Test-InstallerPlanConverged -Plan $plan
        Write-InstallerLine -Message "Plano para o Dokpod $($Answers.version) ($($Answers.edition), $($Answers.engine)):"
        Write-InstallerLine -Message (Format-InstallerPlan -Plan $plan)
        Write-InstallerLine -Message ''
        Confirm-InstallPlan -Plan $plan -Confirmations $Confirmations -Interactive $Interactive -Prompt $Prompt

        $stages = Get-ServiceStage -Model $model
        $runStage = { param([string] $stageName)
            $stage = $stages | Where-Object Name -eq $stageName
            if ($stage) {
                [void] (Invoke-InstallStep -StepState $state -StepRunId $runId -StepName "services:$stageName" -StepAction {
                        [void] (Set-ServiceSecretOwner -Model $model -Services $stage.Services -Cli $Cli)
                        Invoke-ServiceStage -Answers $Answers -Stage $stageName -Services $stage.Services -TimeoutSeconds $WaitTimeoutSeconds -Project $Project -Cli $Cli
                    })
            }
        }

        [void] (Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'secrets' -StepAction {
            [void] (Initialize-PostgresSecrets -Answers $Answers)
            [void] (Initialize-KeycloakSecret -Answers $Answers)
        })

        $certificates = Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'pki' -StepAction {
            $algorithm = if ($Answers.certificates.agents.Contains('keyAlgorithm')) { $Answers.certificates.agents.keyAlgorithm } else { 'ecdsa-p256' }
            $ca = (Initialize-LocalCertificateAuthority -SecretsDirectory $Answers.secretsDir -Algorithm $algorithm).Certificate
            $edge = Initialize-ServiceCertificate -Answers $Answers -State $state -Role edge -CertificateAuthority $ca
            $api = Initialize-ServiceCertificate -Answers $Answers -State $state -Role api -CertificateAuthority $ca
            [void] (Initialize-DataProtectionCertificate -SecretsDirectory $Answers.secretsDir)
            [void] (Initialize-AgentCertificate -SecretsDirectory $Answers.secretsDir -EnvironmentId $environment.Id -CertificateAuthority $ca `
                    -Algorithm $algorithm -RenewWarningDays ([int] $Answers.certificates.renewWarningDays))
            [pscustomobject]@{ Ca = $ca; Edge = $edge; Api = $api }
        }

        Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'config' -StepAction {
            Assert-EngineSocketIsolation -Model $model -EngineSocket $EngineSocket
            $trust = Get-TrustMaterial -Answers $Answers -Ca $certificates.Ca -Edge $certificates.Edge -Api $certificates.Api
            [void] (Write-InstallerFiles -Answers $Answers -Model $model -CaBundle $trust.CaBundle -LocalCaPem $trust.ServerCaPem -AgentsCaPem $trust.AgentsCaPem)
        }

        & $runStage 'data'
        Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'database' -StepAction {
            [void] (Invoke-ProvisionedPostgresBootstrap -Answers $Answers -Image $Infrastructure.postgresql -Network $dataNetwork -Cli $Cli)
        }

        $migrations = Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'migrations' -StepAction {
            Invoke-MigrationStep -Answers $Answers -State $state -RunId $runId -Image $Images.api -Network $dataNetwork -Confirmations $Confirmations `
                -SqlDirectory $SqlDirectory -ManualMigrations:($ManualMigrations -or [bool] $Answers.manualMigrations) -Interactive $Interactive -Prompt $Prompt -Cli $Cli
        }

        $license = $null
        if ($Answers.edition -eq 'business') {
            $license = Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'license' -StepAction { Invoke-LicenseImport -Answers $Answers -Image $Images.api -Network $dataNetwork -Cli $Cli }
        }

        & $runStage 'identity'
        $realm = Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'realm' -StepAction { Invoke-KeycloakRealm -Answers $Answers -Image $InstallerImage -Network $keycloakNetwork -Cli $Cli }
        $registered = Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'environment' -StepAction {
            Invoke-EnvironmentStep -Answers $Answers -State $state -ApiImage $Images.api -InstallerImage $InstallerImage -Environment $environment `
                -Network $dataNetwork -KeycloakNetwork $keycloakNetwork -Cli $Cli
        }
        foreach ($stage in 'application', 'edge') { & $runStage $stage }
        if ($stages | Where-Object Name -eq 'edge') {
            [void] (Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'services:edge-reload' -StepAction {
                    Restart-ServiceForUpstreams -Answers $Answers -Plan $plan -TimeoutSeconds $WaitTimeoutSeconds -Project $Project -Cli $Cli
                })
        }
        & $runStage 'agent'

        foreach ($component in $desired) {
            Invoke-StateSql -State $state -Name 'component-upsert' -Parameters @{
                name = $component.Name; origin = $component.Origin; desired_hash = $component.Hash; observed_json = $null
                recreatable = [int] ($component.Origin -eq 'provisioned'); now = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
            }
        }
        Set-InstallationRecord -State $state -Version $Answers.version -Edition $Answers.edition -Engine $Answers.engine -PublicUrl $Answers.publicUrl

        $outcome = if ($converged) { 'converged' } else { 'changed' }
        $report = Export-InstallerRunReport -State $state -RunId $runId -ReportsDirectory (Join-Path $Answers.installDir 'reports') -Extra ([ordered]@{
                environment = [ordered]@{ id = $registered.EnvironmentId; fingerprint = $registered.Fingerprint }
                migrations  = $migrations
                license     = $license
                realm       = [ordered]@{ bootstrapRemoved = $realm.BootstrapRemoved; updatePasswordPending = $realm.UpdatePasswordPending }
            })
        Complete-InstallerRun -State $state -RunId $runId -Outcome $outcome -ExitCode 0 -ReportPath $report
        [void] (Backup-InstallerState -State $state -RunId $runId)
        Invoke-InstallerStateRetention -State $state

        $next = @()
        if ($license -and -not $license.Imported) { $next += $license.Next }
        $next += "Para outros hosts: 'install.sh host add --name <nome>'."
        [void] (Write-InstallSummary -Answers $Answers -ReportPath $report -NextSteps $next -Interactive $Interactive)
        return 0
    }
    catch {
        $code = if ($_.Exception.PSObject.Properties['ExitCode']) { $_.Exception.ExitCode } else { 1 }
        if ($locked) {
            $outcome = switch ($code) {
                (Get-InstallerExitCode -Name PausedForMigration) { 'paused_for_manual_migration' }
                (Get-InstallerExitCode -Name Declined) { 'declined' }
                default { 'failed' }
            }
            if ($script:CurrentStep) {
                Add-InstallerRunStep -State $state -RunId $runId -Step $script:CurrentStep -Status $(if ($outcome -eq 'failed') { 'failed' } else { 'skipped' })
            }

            $report = Export-InstallerRunReport -State $state -RunId $runId -ReportsDirectory (Join-Path $Answers.installDir 'reports') -Extra ([ordered]@{
                    error = [ordered]@{ step = $script:CurrentStep; exitCode = $code; message = $_.Exception.Message }
                })
            Complete-InstallerRun -State $state -RunId $runId -Outcome $outcome -ExitCode $code -ReportPath $report
            Write-InstallerLine -ToStandardError -Message "Relatório: $report"
        }

        throw
    }
    finally {
        if ($locked) { Exit-InstallerRunLock -State $state -RunId $runId }
        Close-InstallerState -State $state
    }
}

Export-ModuleMember -Function Invoke-InstallerInstall
