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
4. services:data: dokpod-postgres e keycloak-postgres (só os provisionados);
5. database: banco, roles e conexões pelo catálogo SQL, no provisionado ou no PostgreSQL existente (existing-create, T035, ou existing-prepared, T036);
6. migrations: pelo modo administrativo da API, com as confirmações de FR-011, FR-036 e FR-041;
7. license: só na edição Business (FR-031);
8. services:identity e realm: Keycloak e a reconciliação do realm;
9. environment: o ambiente local e a identidade do agente;
10. services:application, services:edge e services:agent: API, BFF e web; borda (reiniciada quando web, BFF ou
    Keycloak foram recriados e ela não, porque o nginx guarda o IP resolvido na partida); agente.

Cada etapa services:* dá antes aos segredos montados o dono do usuário do serviço que os lê (D-015).

Toda etapa converge na reexecução; uma falha registra a etapa, o relatório e o código, e a próxima execução retoma.
Com nginx existente (T037), o config valida o dokpod.conf com nginx -t e o resumo lista os passos no nginx do usuário.
Com Keycloak existente (T039), o realm é reconciliado nele com a credencial do master informada na execução, e o plano
mostra as divergências do realm (T040) antes da confirmação.
#>
Set-StrictMode -Version Latest
foreach ($module in 'Common', 'Secrets', 'State', 'Plan', 'Render', 'Edge', 'Pki', 'Postgres', 'Migrations', 'Keycloak', 'Environments', 'Services', 'Recreate', 'Hosts') {
    Import-Module (Join-Path $PSScriptRoot "$module.psm1")
}

function Get-TrustMaterial {
    <#
    O que cada serviço confia: certs/ca.crt valida a API no agente (CA local ou raízes da organização), certs/agents-ca.crt
    valida os agentes na API (sempre a CA local) e o bundle de SSL_CERT_FILE da API e do BFF soma as CAs da instalação,
    inclusive a do PostgreSQL existente (o Npgsql valida a cadeia pelas raízes do sistema).
    #>
    param([System.Collections.IDictionary] $Answers, $Ca, $Edge, $Api)

    # Na troca da CA (T044), a raiz antiga fica junto com a nova até todos os agentes voltarem com o certificado novo.
    $caPem = (ConvertTo-CertificatePem -Certificate $Ca) + "$(Get-PreviousCaPem -SecretsDirectory $Answers.secretsDir)"
    $serverCaPem = if ($Api.Origin -eq 'organization' -and $Api.Chain.Count -gt 0) {
        ($Api.Chain | ForEach-Object { ConvertTo-CertificatePem -Certificate $_ }) -join ''
    }
    else { $caPem }
    $extra = @($caPem) + @(@($Edge.Chain) + @($Api.Chain) | ForEach-Object { ConvertTo-CertificatePem -Certificate $_ })
    $extra += Get-ExistingPostgresCaPem -Answers $Answers
    $extra += Get-ExistingKeycloakCaPem -Answers $Answers
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
    .PARAMETER Recreate
    Itens de `recreate` (T042, Recreate.psm1): o plano mostra a recriação com o efeito colateral, e os arquivos pedidos só
    são invalidados depois da confirmação; as etapas normais os reemitem e recriam os serviços que os usam.
    .PARAMETER RecreateAll
    `recreate --all` (T043): recria todos os serviços provisionados e preserva os dados, salvo com a segunda
    confirmação nomeada (Confirmations.DataRemoval exata, ou digitada no terminal depois da confirmação normal).
    #>
    [CmdletBinding()]
    [OutputType([int])]
    # As etapas leem SqlDirectory e ManualMigrations pelo escopo dinâmico dos scriptblocks de Invoke-InstallStep.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'SqlDirectory', Justification = 'Lido nos scriptblocks das etapas.')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'ManualMigrations', Justification = 'Lido nos scriptblocks das etapas.')]
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
        [string[]] $Recreate = @(),
        [switch] $RecreateAll,
        # recreate pki:ca (T044): uma rodada da troca da CA; Invoke-InstallerCaRotation encadeia as duas fases.
        [switch] $RotateCa,
        [int] $WaitTimeoutSeconds = 300,
        # Só para testes num host que já tem outro projeto compose 'dokpod'; as redes internas seguem o nome.
        [string] $Project = 'dokpod',
        [bool] $Interactive = (Test-InteractiveSession),
        [scriptblock] $Prompt = { param($message) Read-Host -Prompt $message },
        [string] $Cli = 'docker'
    )

    Assert-ExistingPostgresAnswers -Answers $Answers
    Assert-ExistingEdgeAnswers -Answers $Answers
    [void] (Get-ExistingKeycloakCaPem -Answers $Answers)
    [void] [System.IO.Directory]::CreateDirectory($Answers.installDir)
    Initialize-SecretsDirectory -Path $Answers.secretsDir
    $dataNetwork = "$($Project)_dokpod-data"
    # Os containers avulsos que falam com o banco (psql e modo administrativo) usam a rede do banco: a interna do
    # projeto no provisionado, a padrão do engine (com saída) no existente.
    $databaseNetwork = if ($Answers.postgres.mode -eq 'provision') { $dataNetwork } else { (Get-PostgresDefaults).ExistingNetwork }
    $database = if ($Answers.postgres.Contains('database') -and $Answers.postgres.database) { [string] $Answers.postgres.database } else { (Get-PostgresDefaults).Database }
    $trustBundle = if ($Answers.postgres.mode -ne 'provision' -and (Test-CaBundleNeeded -Answers $Answers)) { Join-Path $Answers.installDir 'certs/ca-bundle.crt' } else { $null }
    # Containers avulsos do Keycloak: a rede interna dele no provisionado, a padrão do engine (com saída) no existente.
    $keycloakExisting = $Answers.keycloak.mode -eq 'existing'
    $keycloakNetwork = if ($keycloakExisting) { (Get-KeycloakDefaults).ExistingNetwork } else { "$($Project)_keycloak-data" }
    $keycloakUrl = if ($keycloakExisting) { $Answers.keycloak.url.TrimEnd('/') } else { (Get-KeycloakDefaults).InternalUrl }
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
        $command = if ($Recreate.Count -gt 0 -or $RecreateAll -or $RotateCa) { 'recreate' } else { 'install' }
        [void] (Start-InstallerRun -State $state -Command $command -RunId $runId)

        $environment = Resolve-LocalEnvironment -Answers $Answers -State $state
        $edgeTrusted = Get-EdgeTrustedNetworks -Answers $Answers -Cli $Cli
        $model = New-InstallerComposeModel -Answers $Answers -Images $Images -Infrastructure $Infrastructure -EngineSocket $EngineSocket -LocalEnvironmentId $environment.Id `
            -TrustedNetworks $edgeTrusted
        $model.name = $Project
        $hashes = Set-ComposeConfigHashLabels -Model $model
        $desired = Get-DesiredComponents -Answers $Answers -ServiceHashes $hashes
        foreach ($component in 'secrets', 'pki', 'realm', 'migrations') { $observed[$component] = @{ Exists = $false; Pending = $null } }
        $observed['secrets'].Exists = Test-Path -LiteralPath (Join-Path $Answers.secretsDir 'Authentication__Keycloak__ClientSecret')
        $observed['pki'].Exists = Test-Path -LiteralPath (Join-Path $Answers.installDir 'certs/ca.crt')
        $components = Invoke-StateSql -State $state -Name 'components-list' -Rows
        foreach ($row in $components) { if ($row['name'] -in 'realm', 'migrations') { $observed[$row['name']].Exists = $true } }
        # Divergências do realm já configurado (T040): entram no plano e só são corrigidas depois da confirmação.
        if ($observed['realm'].Exists -and ($keycloakExisting -or ($observed.Contains('keycloak') -and $observed['keycloak'].Running))) {
            $drift = Get-KeycloakRealmDrift -Answers $Answers -Image $InstallerImage -Network $keycloakNetwork -Cli $Cli
            if ($drift) { $observed['realm'].Pending = "$(@($drift).Count) divergência(s): $(@($drift) -join '; ')" }
        }
        $target = $null
        $removeData = $false
        if (($Recreate.Count -gt 0 -or $RecreateAll -or $RotateCa) -and @($components).Count -eq 0) {
            Stop-Installer -Code Usage -Message "Não há instalação em $($Answers.installDir) para recriar; use 'install'."
        }
        if ($RecreateAll -and $Confirmations.Contains('DataRemoval') -and @($Confirmations.DataRemoval).Count -gt 0) {
            $removeData = Resolve-DataRemoval -Answers $Answers -Confirmed @($Confirmations.DataRemoval) -Interactive $false
        }
        if ($Recreate.Count -gt 0) {
            if (@($components).Count -eq 0) {
                Stop-Installer -Code Usage -Message "Não há instalação em $($Answers.installDir) para recriar; use 'install'."
            }

            $target = Resolve-RecreateTarget -Answers $Answers -Items $Recreate -Services @($model.services.Keys) -Desired $desired -LocalEnvironmentId $environment.Id
        }
        if ($RotateCa) { $target = Start-CaRotation -Answers $Answers -State $state -LocalEnvironmentId $environment.Id -WhatIf }
        $plan = New-InstallerPlan -Desired $desired -Observed $observed -Recreate $(if ($target) { $target.Components } else { @() }) -RecreateAll:$RecreateAll `
            -ConfirmDataRemoval $(if ($removeData) { Get-DataRemovalItem -Answers $Answers } else { @() })
        $converged = Test-InstallerPlanConverged -Plan $plan
        Write-InstallerLine -Message "Plano para o Dokpod $($Answers.version) ($($Answers.edition), $($Answers.engine)):"
        Write-InstallerLine -Message (Format-InstallerPlan -Plan $plan)
        Write-InstallerLine -Message ''
        Confirm-InstallPlan -Plan $plan -Confirmations $Confirmations -Interactive $Interactive -Prompt $Prompt
        if ($RotateCa) { $target = Start-CaRotation -Answers $Answers -State $state -LocalEnvironmentId $environment.Id }
        if ($target) {
            $removed = Invoke-RecreateInvalidation -Answers $Answers -Target $target
            Write-InstallerLine -Message "Recriação: $(@($target.Items) -join ', ')$(if ($removed.Count) { " (reemitidos: $($removed -join ', '))" })."
        }
        $forceRecreate = if ($target) { @($target.Services) } else { @() }
        $revokeIdentity = $target -and $environment.Id -in @($target.Agents)
        if ($RecreateAll) {
            # A segunda confirmação vem depois da normal; sem ela, os dados ficam (FR-029, SC-008).
            if (-not $removeData -and $Interactive) { $removeData = Resolve-DataRemoval -Answers $Answers -Interactive $true -Prompt $Prompt }
            $data = @('dokpod-postgres', 'keycloak-postgres')
            if ($removeData) {
                $volumes = Remove-InstallationData -Answers $Answers -Model $model -Project $Project -Cli $Cli
                $dataItems = Get-DataRemovalItem -Answers $Answers
                Write-InstallerLine -Message "Recriação total com os dados removidos: $($dataItems -join ', ') (volumes: $($volumes -join ', '))."
                # Com o PostgreSQL do usuário, as identidades saem por revogação, e o agente local ganha certificado novo.
                if ($Answers.postgres.mode -ne 'provision') {
                    [void] (Invoke-RecreateInvalidation -Answers $Answers -Target ([pscustomobject]@{ Files = @("pki/agent-$($environment.Id).pfx", "pki/agent-$($environment.Id).pass") }))
                    $revokeIdentity = $true
                }
                $environmentRows = Invoke-StateSql -State $state -Name 'environments-list' -Rows
                $remote = @($environmentRows | Where-Object { $_['environment_id'] -ne $environment.Id -and -not $_['revoked_at'] })
                if ($remote.Count -gt 0) {
                    Write-InstallerLine -ToStandardError -Message "Aviso: os hosts remotos ($(@($remote | ForEach-Object { $_['name'] }) -join ', ') precisam de pacote novo ('host add'); a identidade deles saiu com os dados."
                }
                $data = @()
            }
            else {
                Write-InstallerLine -Message 'Recriação total com os dados preservados: banco, login, key rings e identidades dos agentes ficam como estão.'
            }
            $forceRecreate = @($model.services.Keys | Where-Object { $_ -notin $data })
        }

        $stages = Get-ServiceStage -Model $model
        $runStage = { param([string] $stageName)
            $stage = $stages | Where-Object Name -eq $stageName
            if ($stage) {
                [void] (Invoke-InstallStep -StepState $state -StepRunId $runId -StepName "services:$stageName" -StepAction {
                        [void] (Set-ServiceSecretOwner -Model $model -Services $stage.Services -Cli $Cli)
                        Invoke-ServiceStage -Answers $Answers -Stage $stageName -Services $stage.Services -TimeoutSeconds $WaitTimeoutSeconds -Project $Project -Cli $Cli `
                            -ForceRecreate @($forceRecreate | Where-Object { $_ -in $stage.Services })
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
            $agent = Initialize-AgentCertificate -SecretsDirectory $Answers.secretsDir -EnvironmentId $environment.Id -CertificateAuthority $ca `
                -Algorithm $algorithm -RenewWarningDays ([int] $Answers.certificates.renewWarningDays)
            # O ID já está no compose.yaml e no certificado: a retomada precisa reencontrá-lo (FR-027).
            Save-LocalEnvironment -State $state -Environment $environment -Fingerprint $agent.Fingerprint
            [pscustomobject]@{ Ca = $ca; Edge = $edge; Api = $api }
        }

        Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'config' -StepAction {
            Assert-EngineSocketIsolation -Model $model -EngineSocket $EngineSocket
            $trust = Get-TrustMaterial -Answers $Answers -Ca $certificates.Ca -Edge $certificates.Edge -Api $certificates.Api
            [void] (Write-InstallerFiles -Answers $Answers -Model $model -CaBundle $trust.CaBundle -LocalCaPem $trust.ServerCaPem -AgentsCaPem $trust.AgentsCaPem)
            if ($Answers.edge.mode -eq 'existing') { [void] (Test-EdgeConfiguration -Answers $Answers -Image $Infrastructure.nginx -Cli $Cli) }
        }

        & $runStage 'data'
        Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'database' -StepAction {
            if ($Answers.postgres.mode -eq 'provision') {
                [void] (Invoke-ProvisionedPostgresBootstrap -Answers $Answers -Image $Infrastructure.postgresql -Network $dataNetwork -Cli $Cli)
            }
            elseif ($Answers.postgres.mode -eq 'existing-prepared') {
                [void] (Invoke-PreparedPostgresBootstrap -Answers $Answers -Image $Infrastructure.postgresql -Network $databaseNetwork -Cli $Cli)
            }
            else {
                [void] (Invoke-ExistingPostgresBootstrap -Answers $Answers -Image $Infrastructure.postgresql -Network $databaseNetwork -Interactive $Interactive -Cli $Cli)
            }
        }

        $migrations = Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'migrations' -StepAction {
            Invoke-MigrationStep -Answers $Answers -State $state -RunId $runId -Image $Images.api -Network $databaseNetwork -Database $database -TrustBundle $trustBundle -Confirmations $Confirmations `
                -SqlDirectory $SqlDirectory -ManualMigrations:($ManualMigrations -or [bool] $Answers.manualMigrations) -Interactive $Interactive -Prompt $Prompt -Cli $Cli
        }

        $license = $null
        if ($Answers.edition -eq 'business') {
            $license = Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'license' -StepAction { Invoke-LicenseImport -Answers $Answers -Image $Images.api -Network $databaseNetwork -TrustBundle $trustBundle -Cli $Cli }
        }

        & $runStage 'identity'
        $realmConfigured = $observed['realm'].Exists
        $realm = Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'realm' -StepAction {
            if ($keycloakExisting) {
                Invoke-ExistingKeycloakRealm -Answers $Answers -Image $InstallerImage -Network $keycloakNetwork -RealmConfigured $realmConfigured -Interactive $Interactive -Cli $Cli
            }
            else { Invoke-KeycloakRealm -Answers $Answers -Image $InstallerImage -Network $keycloakNetwork -Cli $Cli }
        }
        $registered = Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'environment' -StepAction {
            Invoke-EnvironmentStep -Answers $Answers -State $state -ApiImage $Images.api -InstallerImage $InstallerImage -Environment $environment `
                -Network $databaseNetwork -TrustBundle $trustBundle -KeycloakNetwork $keycloakNetwork -KeycloakUrl $keycloakUrl -Cli $Cli `
                -RevokeIdentity:$revokeIdentity
        }
        foreach ($stage in 'application', 'edge') { & $runStage $stage }
        if ($stages | Where-Object Name -eq 'edge') {
            [void] (Invoke-InstallStep -StepState $state -StepRunId $runId -StepName 'services:edge-reload' -StepAction {
                    Restart-ServiceForUpstreams -Answers $Answers -Plan $plan -TimeoutSeconds $WaitTimeoutSeconds -Project $Project -Cli $Cli
                })
        }
        & $runStage 'agent'
        if ($RotateCa) {
            $script:CaRotationCompleted = Complete-CaRotation -Answers $Answers -State $state -LocalEnvironmentId $environment.Id -TimeoutSeconds $WaitTimeoutSeconds -Connection {
                Get-AgentConnection -Answers $Answers -EnvironmentId $environment.Id -Image $Infrastructure.postgresql -Network $databaseNetwork -Cli $Cli
            }
        }

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

        # Certificados perto de vencer (FR-019): CA, borda, API e cada agente, com a ação sugerida.
        Write-CertificateWarning -Status (Get-InstallerCertificateStatus -Answers $Answers -State $state)
        $next = @()
        if ($license -and -not $license.Imported) { $next += $license.Next }
        $next += Get-ExistingEdgeSteps -Answers $Answers
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

function Invoke-InstallerCaRotation {
    <#
    .SYNOPSIS
    `recreate pki:ca` (T044, FR-030): primeira rodada com as duas raízes, a CA nova e o agente local reemitido; quando
    ele reconecta e nenhum host remoto está pendente, a segunda rodada regrava a confiança só com a raiz nova e recria
    API, BFF e agente. Com hosts pendentes, a troca fica em `rotating` até um novo `recreate pki:ca`.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param([Parameter(Mandatory)][hashtable] $Parameters)

    $script:CaRotationCompleted = $false
    $code = Invoke-InstallerInstall @Parameters -RotateCa
    if ($code -ne 0 -or -not $script:CaRotationCompleted) { return $code }

    Write-InstallerLine -Message 'Troca da CA: os agentes voltaram com o certificado novo; a raiz antiga sai da confiança.'
    return Invoke-InstallerInstall @Parameters -Recreate @('service:api', 'service:bff', 'service:agent')
}

function Invoke-InstallerHostCommand {
    <#
    .SYNOPSIS
    `host add` e `host revoke` (T046, T047) numa instalação existente: o lock do estado, as redes e a confiança iguais
    às do install, e o registro da execução.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][ValidateSet('add', 'revoke')][string] $Action,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Images,
        [Parameter(Mandatory)][string] $InstallerImage,
        [string] $Name,
        [string] $Engine = 'podman',
        [string] $EnvironmentId,
        [string] $Reason,
        [string] $Project = 'dokpod',
        [bool] $Interactive = (Test-InteractiveSession),
        [string] $Cli = 'docker'
    )

    if (-not (Test-Path -LiteralPath (Join-Path $Answers.installDir 'state'))) {
        Stop-Installer -Code Usage -Message "Não há instalação em $($Answers.installDir); rode 'install' antes de 'host $Action'."
    }

    $state = Open-InstallerState -InstallDirectory $Answers.installDir
    $runId = [guid]::NewGuid().ToString()
    $locked = $false
    try {
        $containerName = if ($env:HOSTNAME) { $env:HOSTNAME } else { "dokpod-installer-$PID" }
        Enter-InstallerRunLock -State $state -RunId $runId -ContainerName $containerName `
            -ContainerExists { param($name) $null = & $Cli inspect --format '{{.Id}}' $name 2>$null; $LASTEXITCODE -eq 0 }
        $locked = $true
        [void] (Start-InstallerRun -State $state -Command "host $Action" -RunId $runId)
        $network = if ($Answers.postgres.mode -eq 'provision') { "$($Project)_dokpod-data" } else { (Get-PostgresDefaults).ExistingNetwork }
        $trustBundle = if ($Answers.postgres.mode -ne 'provision' -and (Test-CaBundleNeeded -Answers $Answers)) { Join-Path $Answers.installDir 'certs/ca-bundle.crt' } else { $null }
        if ($Action -eq 'add') {
            $existing = $Answers.keycloak.mode -eq 'existing'
            [void] (Invoke-InstallerHostAdd -Answers $Answers -State $state -Name $Name -Engine $Engine -ApiImage $Images.api -AgentImage $Images.agent `
                    -InstallerImage $InstallerImage -Network $network -TrustBundle $trustBundle -Interactive $Interactive -Cli $Cli `
                    -KeycloakNetwork $(if ($existing) { (Get-KeycloakDefaults).ExistingNetwork } else { "$($Project)_keycloak-data" }) `
                    -KeycloakUrl $(if ($existing) { $Answers.keycloak.url.TrimEnd('/') } else { (Get-KeycloakDefaults).InternalUrl }))
        }
        else {
            [void] (Invoke-InstallerHostRevoke -Answers $Answers -State $state -EnvironmentId $EnvironmentId -Reason $Reason -ApiImage $Images.api `
                    -Network $network -TrustBundle $trustBundle -Cli $Cli)
        }

        Complete-InstallerRun -State $state -RunId $runId -Outcome 'changed' -ExitCode 0 -ReportPath $null
        return 0
    }
    catch {
        $code = if ($_.Exception.PSObject.Properties['ExitCode']) { $_.Exception.ExitCode } else { 1 }
        if ($locked) { Complete-InstallerRun -State $state -RunId $runId -Outcome 'failed' -ExitCode $code -ReportPath $null }
        throw
    }
    finally {
        if ($locked) { Exit-InstallerRunLock -State $state -RunId $runId }
        Close-InstallerState -State $state
    }
}

function Invoke-InstallerMigrationsCommand {
    <#
    .SYNOPSIS
    `migrations status | plan | export --out <arquivo> | verify` (FR-036 a FR-038): repassa ao modo administrativo da
    imagem da API da versão das respostas (ou de --version), com a conexão da role dona. Nenhum altera o banco.
    .DESCRIPTION
    O export grava em installDir/migrations, a pasta que o container do instalador e o do modo administrativo enxergam;
    com --out, o arquivo é copiado para lá também.
    .OUTPUTS
    O código do modo administrativo (0, ou 6 quando verify acha divergência).
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][ValidateSet('status', 'plan', 'export', 'verify')][string] $Action,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Images,
        [string] $Out,
        [string] $Project = 'dokpod',
        [string] $Cli = 'docker'
    )

    $network = if ($Answers.postgres.mode -eq 'provision') { "$($Project)_dokpod-data" } else { (Get-PostgresDefaults).ExistingNetwork }
    $trustBundle = if ($Answers.postgres.mode -ne 'provision' -and (Test-CaBundleNeeded -Answers $Answers)) { Join-Path $Answers.installDir 'certs/ca-bundle.crt' } else { $null }
    $admin = @{ Image = $Images.api; Network = $network; SecretsDirectory = $Answers.secretsDir; TrustBundle = $trustBundle; Cli = $Cli }
    if ($Action -ne 'export') {
        $result = Invoke-AdminCommand @admin -Arguments @('migrations', $Action)
        Write-InstallerLine -Message ($result.Data | ConvertTo-Json -Depth 8)
        return $result.ExitCode
    }

    $work = Join-Path $Answers.installDir 'migrations'
    [void] [System.IO.Directory]::CreateDirectory($work)
    $name = "export-$([DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss')).sql"
    $result = Invoke-AdminCommand @admin -Arguments @('migrations', 'script', '--out', "/run/dokpod-migrations/$name") -WorkDirectory $work -WritableWork
    $path = Join-Path $work $name
    if ($Out) {
        Copy-Item -LiteralPath $path -Destination $Out -Force
        $path = $Out
    }

    Write-InstallerLine -Message "SQL das migrations pendentes em $path (sha256 $($result.Data.sha256), planHash $($result.Data.planHash)). Nada foi aplicado."
    return $result.ExitCode
}

Export-ModuleMember -Function Invoke-InstallerInstall, Invoke-InstallerCaRotation, Invoke-InstallerHostCommand, Invoke-InstallerMigrationsCommand
