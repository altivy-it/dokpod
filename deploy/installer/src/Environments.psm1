#Requires -Version 7.4
<#
.SYNOPSIS
Primeiro ambiente da instalação (spec 033 T031, FR-024, D-004): cadastro do ambiente e da identidade do agente pelo modo
administrativo da API, autorização do ambiente no Keycloak, certificado do agente e o socket do engine só no agente.

.DESCRIPTION
Ordem da etapa, que converge numa reexecução:
1. o ambiente local reaproveita o ID gravado no estado; sem ele, recebe um novo;
2. a CA local e o certificado de cliente do agente (pki/agent-<id>.pfx e .pass em secretsDir) são reaproveitados
   quando válidos; inválido interrompe e aponta `recreate pki:agent:<id>`;
3. `admin environments register` e `admin agent-identities register` gravam o ambiente e a identidade com auditoria
   do Dokpod; dados diferentes ou fingerprint de outro ambiente dão conflito (código 8 do modo administrativo);
4. provision-environment-authorization.ps1 concede ao grupo /dokpod/administrators todos os scopes do ambiente, num
   container avulso só na rede do Keycloak;
5. o estado guarda o ambiente, o fingerprint e os certificados, sem segredos.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module (Join-Path $PSScriptRoot 'Secrets.psm1')
Import-Module (Join-Path $PSScriptRoot 'State.psm1')
Import-Module (Join-Path $PSScriptRoot 'Pki.psm1')
Import-Module (Join-Path $PSScriptRoot 'Keycloak.psm1')
Import-Module (Join-Path $PSScriptRoot 'Migrations.psm1')

$script:AdministratorsGroup = '/dokpod/administrators'
$script:AdminNetwork = 'dokpod_dokpod-data'
$script:SocketTarget = '/run/engine.sock'
$script:Reason = 'instalação: primeiro ambiente'

function Get-UtcText {
    param([DateTimeOffset] $Value = [DateTimeOffset]::UtcNow)
    return $Value.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
}

function Resolve-LocalEnvironment {
    <#
    .SYNOPSIS
    Ambiente local da instalação: o ID gravado no estado (docker ou podman, não revogado) ou um novo. Nome e estágio vêm
    de localEnvironment; o host é o nome da máquina que o install.sh repassa (DOKPOD_HOST_NAME), ou o host da publicUrl.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $State
    )

    $engine = ([string] $Answers.engine).Replace('-rootless', '')
    # -Rows devolve o array como um objeto só no pipeline: atribuído antes de filtrar.
    $rows = Invoke-StateSql -State $State -Name 'environments-list' -Rows
    $known = $rows | Where-Object { $_['engine'] -in 'docker', 'podman' -and -not $_['revoked_at'] } | Select-Object -First 1
    $hostName = if ($env:DOKPOD_HOST_NAME) { $env:DOKPOD_HOST_NAME } else { ([Uri] $Answers.publicUrl).Host }
    return [pscustomobject]@{
        Id     = if ($known) { [string] $known['environment_id'] } else { [guid]::NewGuid().ToString() }
        Known  = [bool] $known
        Name   = [string] $Answers.localEnvironment.name
        Stage  = [string] $Answers.localEnvironment.stage
        Host   = $hostName
        Engine = $engine
    }
}

function Set-CertificateRecord {
    param($State, [string] $Role, $Certificate, [string] $SecretRef, [string] $CaState)

    Invoke-StateSql -State $State -Name 'certificate-upsert' -Parameters @{
        role = $Role; origin = 'local-ca'; subject_alt_names_json = (ConvertTo-Json -InputObject @(Get-CertificateDnsNames -Certificate $Certificate) -Compress)
        not_after = Get-UtcText ([DateTimeOffset] $Certificate.NotAfter); fingerprint_sha256 = Get-CertificateFingerprint -Certificate $Certificate
        secret_ref = $SecretRef; ca_state = $(if ($CaState) { $CaState } else { $null }); now = Get-UtcText
    }
}

function Save-LocalEnvironment {
    <#
    .SYNOPSIS
    Grava no estado o ambiente local e o fingerprint do agente, preservando package_generated_at.
    .DESCRIPTION
    O install chama logo depois de emitir o certificado do agente: o ID vai para o compose.yaml e para o banco, e uma
    execução interrompida depois disso precisa retomar com o mesmo ID, sem outro certificado nem outro cadastro (FR-027).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][pscustomobject] $Environment,
        [Parameter(Mandatory)][string] $Fingerprint
    )

    $rows = Invoke-StateSql -State $State -Name 'environments-list' -Rows
    $row = $rows | Where-Object { $_['environment_id'] -eq $Environment.Id } | Select-Object -First 1
    Invoke-StateSql -State $State -Name 'environment-upsert' -Parameters @{
        environment_id = $Environment.Id; name = $Environment.Name; engine = $Environment.Engine; agent_fingerprint = $Fingerprint
        package_generated_at = if ($row) { $row['package_generated_at'] } else { $null }; revoked_at = $null
    }
}

function Initialize-AgentCertificate {
    <#
    .SYNOPSIS
    Certificado de cliente do agente de um ambiente (clientAuth, assinado pela CA local), em secretsDir/pki como PFX e senha.
    .DESCRIPTION
    Um PFX existente é aberto com a sua senha e validado contra a CA (EKU, validade, cadeia e chave privada). Válido, é
    reaproveitado, com aviso quando vence em menos de RenewWarningDays; inválido, interrompe sem tocar em nada. Só a
    senha, sem o PFX, é sobra de execução interrompida e é substituída; o PFX sem a senha interrompe.
    .OUTPUTS
    Certificate, Created e Fingerprint (SHA-256 do DER, o que vai para dokpod.agent_identities).
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string] $SecretsDirectory,
        [Parameter(Mandatory)][string] $EnvironmentId,
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $CertificateAuthority,
        [ValidateSet('ecdsa-p256', 'rsa-3072')][string] $Algorithm = 'ecdsa-p256',
        [int] $RenewWarningDays = 30,
        [DateTimeOffset] $Now = [DateTimeOffset]::UtcNow
    )

    $pfxName = "pki/agent-$EnvironmentId.pfx"
    $passName = "pki/agent-$EnvironmentId.pass"
    $hasPfx = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $pfxName
    $hasPass = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $passName
    if ($hasPfx) {
        if (-not $hasPass) {
            Stop-Installer -Code Error -Message "$pfxName existe sem $passName em secretsDir; use 'recreate pki:agent:$EnvironmentId'."
        }

        $password = Read-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $passName
        try {
            $certificate = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12(
                [System.IO.File]::ReadAllBytes((Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name $pfxName)), $password)
        }
        catch {
            Stop-Installer -Code Error -Message "$pfxName não abre com a senha de $passName; use 'recreate pki:agent:$EnvironmentId'."
        }

        $problems = Test-InstallerCertificate -Certificate $certificate -Usage Client -TrustedRoots @($CertificateAuthority) -Now $Now -RequirePrivateKey
        if ($problems.Count -gt 0) {
            Stop-Installer -Code Error -Message "O certificado do agente $EnvironmentId não serve: $($problems -join '; '). Use 'recreate pki:agent:$EnvironmentId'."
        }

        $days = ($certificate.NotAfter.ToUniversalTime() - $Now.UtcDateTime).TotalDays
        if ($days -lt $RenewWarningDays) {
            Write-InstallerLine -ToStandardError -Message "Aviso: o certificado do agente $EnvironmentId vence em $([int] [math]::Floor($days)) dias; use 'recreate pki:agent:$EnvironmentId'."
        }

        return [pscustomobject]@{ Certificate = $certificate; Created = $false; Fingerprint = Get-CertificateFingerprint -Certificate $certificate }
    }

    $password = New-InstallerSecret
    $certificate = New-InstallerCertificate -Issuer $CertificateAuthority -CommonName "dokpod-agent-$EnvironmentId" -Usage Client -Algorithm $Algorithm
    # A senha antes do PFX: só a senha gravada é sobra que a próxima execução substitui (acima).
    Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $passName -Value $password -Recreate:$hasPass
    Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $pfxName -Bytes (ConvertTo-CertificatePfx -Certificate $certificate -Password $password -Chain @($CertificateAuthority))
    return [pscustomobject]@{ Certificate = $certificate; Created = $true; Fingerprint = Get-CertificateFingerprint -Certificate $certificate }
}

function Invoke-EnvironmentAdmin {
    param([string] $Image, [string] $Network, [string] $SecretsDirectory, [string[]] $Arguments, [string] $Conflict, [string] $Cli)

    $result = Invoke-AdminCommand -Image $Image -Network $Network -SecretsDirectory $SecretsDirectory -Arguments $Arguments -Cli $Cli
    if ($result.ExitCode -ne 0) {
        $code = if ($result.Data -and $result.Data['failureCode']) { " ($($result.Data['failureCode']))" } else { '' }
        Stop-Installer -Code Error -Message "$Conflict$code Nada foi alterado no banco."
    }

    return $result.Data
}

function Invoke-EnvironmentAuthorization {
    <#
    .SYNOPSIS
    Concede ao grupo /dokpod/administrators todos os scopes do ambiente pelo provision-environment-authorization.ps1,
    num container avulso só na rede do Keycloak (FR-024). O client secret do dokpod-provisioner entra pelo tar do stdin.
    .OUTPUTS
    Scopes provisionados e Changed, os que mudaram algo no Keycloak.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $EnvironmentId,
        [Parameter(Mandatory)][string] $Image,
        [string] $Network = (Get-KeycloakDefaults).Network,
        [string] $BaseUrl = (Get-KeycloakDefaults).InternalUrl,
        [string] $ScriptsRoot = (Join-Path (Get-InstallerHome) 'keycloak'),
        [string] $Cli = 'docker'
    )

    if ($Answers.keycloak.mode -ne 'provision') {
        Stop-Installer -Code Error -Message "keycloak.mode '$($Answers.keycloak.mode)' ainda não é suportado na autorização do ambiente (T039)."
    }

    $defaults = Get-KeycloakDefaults
    $settings = [ordered]@{
        baseUrl       = $BaseUrl
        realm         = $Answers.keycloak.realm
        environmentId = $EnvironmentId
        groupPath     = $script:AdministratorsGroup
        internalHttp  = $BaseUrl.StartsWith('http://', [System.StringComparison]::OrdinalIgnoreCase)
        secrets       = [ordered]@{ provisionerSecret = Read-InstallerSecret -SecretsDirectory $Answers.secretsDir -Name $defaults.ProvisionerSecret }
    }
    $files = [ordered]@{
        'input.json'                             = $settings | ConvertTo-Json -Depth 4
        'sidecar.ps1'                            = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Environments.Sidecar.ps1'))
        'provision-environment-authorization.ps1' = [System.IO.File]::ReadAllText((Join-Path $ScriptsRoot 'provision-environment-authorization.ps1'))
    }
    $result = Invoke-KeycloakSidecar -Image $Image -Network $Network -Task 'environment-authorization' -Files $files -Cli $Cli
    if ($result.ExitCode -ne 0 -or $null -eq $result.Result) {
        Stop-Installer -Code Error -Message "A autorização do ambiente $EnvironmentId no Keycloak falhou (código $($result.ExitCode)): $($result.Error.Trim())"
    }

    return [pscustomobject]@{ Scopes = @($result.Result.scopes); Changed = @($result.Result.changed) }
}

function Assert-EngineSocketIsolation {
    <#
    .SYNOPSIS
    Confere no modelo do compose que o socket do engine só é montado no serviço agent, que o agent não publica porta e
    só fica na rede interna (FR-024, threat model). Qualquer desvio interrompe antes de gravar o compose.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Model,
        [Parameter(Mandatory)][string] $EngineSocket
    )

    $problems = [System.Collections.Generic.List[string]]::new()
    foreach ($name in $Model.services.Keys) {
        $service = $Model.services[$name]
        $volumes = if ($service.Contains('volumes')) { @($service.volumes) } else { @() }
        $socket = @($volumes | Where-Object { "$_".StartsWith("$($EngineSocket):", [System.StringComparison]::Ordinal) -or "$_" -match '(docker|podman)\.sock(:|$)' })
        if ($name -ne 'agent' -and $socket.Count -gt 0) { $problems.Add("o serviço $name monta o socket do engine") }
        if ($name -eq 'agent') {
            if (@($socket | Where-Object { "$_" -eq "$($EngineSocket):$($script:SocketTarget)" }).Count -ne 1) { $problems.Add('o agent não monta o socket do engine') }
            if ($service.Contains('ports')) { $problems.Add('o agent publica porta') }
            if (@($service.networks) -join ',' -ne 'dokpod-internal') { $problems.Add("o agent está nas redes $(@($service.networks) -join ', ')") }
        }
    }

    if (-not $Model.services.Contains('agent')) { $problems.Add('o compose não tem o serviço agent') }
    if ($problems.Count -gt 0) {
        Stop-Installer -Code Error -Message "O compose gerado expõe o socket do engine além do agente: $($problems -join '; '). Nada foi gravado."
    }
}

function Invoke-EnvironmentStep {
    <#
    .SYNOPSIS
    Etapa do primeiro ambiente (FR-024): certificado do agente, cadastro do ambiente e da identidade com auditoria e
    autorização do grupo de administradores no Keycloak. Reexecutada com as mesmas respostas, não muda nada.
    .PARAMETER ApiImage
    Imagem da API da versão (modo administrativo).
    .PARAMETER InstallerImage
    Imagem do instalador em execução (container avulso da autorização).
    .PARAMETER Environment
    O ambiente já resolvido por Resolve-LocalEnvironment. O install o resolve antes do compose.yaml, que leva o ID; sem
    ele, a etapa resolve de novo e, na primeira execução, criaria outro ID.
    .OUTPUTS
    EnvironmentId, EnvironmentCreated, CaCreated, CertificateCreated, IdentityCreated, Fingerprint, AuthorizationChanged.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $ApiImage,
        [Parameter(Mandatory)][string] $InstallerImage,
        [pscustomobject] $Environment,
        [string] $Network = $script:AdminNetwork,
        [string] $KeycloakNetwork = (Get-KeycloakDefaults).Network,
        [string] $KeycloakUrl = (Get-KeycloakDefaults).InternalUrl,
        [string] $ScriptsRoot = (Join-Path (Get-InstallerHome) 'keycloak'),
        [string] $Cli = 'docker'
    )

    $secretsDir = $Answers.secretsDir
    $environment = if ($Environment) { $Environment } else { Resolve-LocalEnvironment -Answers $Answers -State $State }
    $algorithm = if ($Answers.certificates.agents.Contains('keyAlgorithm')) { $Answers.certificates.agents.keyAlgorithm } else { 'ecdsa-p256' }
    $ca = Initialize-LocalCertificateAuthority -SecretsDirectory $secretsDir -Algorithm $algorithm
    $certificates = Invoke-StateSql -State $State -Name 'certificates-list' -Rows
    $known = @($certificates | ForEach-Object { $_['role'] })
    # A linha da CA guarda ca_state, que a troca da CA (T044) muda; aqui ela só é criada.
    if ($ca.Created -or 'ca' -notin $known) { Set-CertificateRecord -State $State -Role 'ca' -Certificate $ca.Certificate -SecretRef 'pki/ca.key' -CaState 'active' }

    $agent = Initialize-AgentCertificate -SecretsDirectory $secretsDir -EnvironmentId $environment.Id -CertificateAuthority $ca.Certificate `
        -Algorithm $algorithm -RenewWarningDays ([int] $Answers.certificates.renewWarningDays)
    Set-CertificateRecord -State $State -Role "agent:$($environment.Id)" -Certificate $agent.Certificate -SecretRef "pki/agent-$($environment.Id).pfx"

    $operator = [string] $Answers.operator
    $registered = Invoke-EnvironmentAdmin -Image $ApiImage -Network $Network -SecretsDirectory $secretsDir -Cli $Cli -Arguments @(
        'environments', 'register', '--id', $environment.Id, '--name', $environment.Name, '--host', $environment.Host,
        '--stage', $environment.Stage, '--actor', $operator, '--reason', $script:Reason) `
        -Conflict ("O ambiente $($environment.Id) já está no banco com outro nome, host ou estágio. Volte localEnvironment ao valor " +
            "cadastrado ou altere o ambiente pelo console.")

    $authorization = Invoke-EnvironmentAuthorization -Answers $Answers -EnvironmentId $environment.Id -Image $InstallerImage `
        -Network $KeycloakNetwork -BaseUrl $KeycloakUrl -ScriptsRoot $ScriptsRoot -Cli $Cli

    $identity = Invoke-EnvironmentAdmin -Image $ApiImage -Network $Network -SecretsDirectory $secretsDir -Cli $Cli -Arguments @(
        'agent-identities', 'register', '--environment', $environment.Id, '--fingerprint', $agent.Fingerprint,
        '--actor', $operator, '--reason', $script:Reason) `
        -Conflict ("O fingerprint do agente $($environment.Id) já está ligado a outro ambiente ou foi revogado, ou o ambiente já tem " +
            "outra identidade. Use 'recreate pki:agent:$($environment.Id)'.")

    Save-LocalEnvironment -State $State -Environment $environment -Fingerprint $agent.Fingerprint

    return [pscustomobject]@{
        EnvironmentId        = $environment.Id
        EnvironmentCreated   = [bool] $registered['created']
        CaCreated            = $ca.Created
        CertificateCreated   = $agent.Created
        IdentityCreated      = [bool] $identity['created']
        Fingerprint          = $agent.Fingerprint
        AuthorizationChanged = $authorization.Changed
    }
}

Export-ModuleMember -Function Resolve-LocalEnvironment, Save-LocalEnvironment, Initialize-AgentCertificate, Invoke-EnvironmentAuthorization, Assert-EngineSocketIsolation,
Invoke-EnvironmentStep
