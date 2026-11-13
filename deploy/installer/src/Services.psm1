#Requires -Version 7.4
<#
.SYNOPSIS
Serviços da instalação (spec 033 T032, FR-016, FR-017, FR-021, FR-031, D-005): certificados da borda e da API, subida
ordenada pelo Compose com o health de cada serviço, licença da edição Business e o resumo final.

.DESCRIPTION
A subida é por etapa, cada uma com `docker compose up --detach --wait`: bancos, Keycloak, API/BFF/web, borda e agente.
Uma etapa só termina com todos os seus serviços rodando e, quando têm healthcheck, saudáveis; senão interrompe com o
nome do serviço e o comando de logs, sem copiar os logs (eles podem ter dados do ambiente).

Os certificados da borda (PEM) e da API (PFX com senha) ficam em secretsDir/pki. Gerados pela CA local ou informados
pela organização, são validados antes do uso (FR-017) e nunca sobrescritos sem `recreate pki:edge` ou `pki:api`.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module (Join-Path $PSScriptRoot 'Secrets.psm1')
Import-Module (Join-Path $PSScriptRoot 'State.psm1')
Import-Module (Join-Path $PSScriptRoot 'Pki.psm1')
Import-Module (Join-Path $PSScriptRoot 'Keycloak.psm1')
Import-Module (Join-Path $PSScriptRoot 'Migrations.psm1')

$script:Project = 'dokpod'
$script:Stages = [ordered]@{
    data        = @('dokpod-postgres', 'keycloak-postgres')
    identity    = @('keycloak')
    application = @('api', 'bff', 'web')
    edge        = @('edge')
    agent       = @('agent')
}
$script:WaitTimeoutSeconds = 300
$script:SystemCaBundle = '/etc/ssl/certs/ca-certificates.crt'
# Invoke-AdminCommand (Migrations.psm1) monta -WorkDirectory neste caminho, somente leitura.
$script:AdminWorkTarget = '/run/dokpod-migrations'
$script:AdminNetwork = 'dokpod_dokpod-data'

function Get-UtcText {
    param([DateTimeOffset] $Value = [DateTimeOffset]::UtcNow)
    return $Value.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
}

function Get-ServiceCertificateNames {
    <#
    .SYNOPSIS
    Nomes que o certificado precisa ter no SAN (D-005): o nome público na borda; `api` na API, mais o nome público quando
    agentes remotos usam o gRPC publicado (agentGrpcPort).
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][ValidateSet('edge', 'api')][string] $Role
    )

    $publicHost = ([Uri] $Answers.publicUrl).Host
    if ($Role -eq 'edge') { return , [string[]] @($publicHost) }
    $names = @('api')
    if ($Answers.Contains('agentGrpcPort') -and $Answers.agentGrpcPort) { $names += $publicHost }
    return , [string[]] $names
}

function Read-PemCertificates {
    param([string] $Path)

    $collection = [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
    if ($Path -and (Test-Path -LiteralPath $Path -PathType Leaf)) { $collection.ImportFromPemFile($Path) }
    return , @($collection)
}

function Read-SystemRoots {
    $collection = [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
    if (Test-Path -LiteralPath $script:SystemCaBundle -PathType Leaf) { $collection.ImportFromPemFile($script:SystemCaBundle) }
    return , @($collection)
}

function Read-ExistingServiceCertificate {
    # O certificado gravado em secretsDir/pki, com a chave; $null quando não há nenhum arquivo do par.
    param([string] $SecretsDirectory, [string] $Role)

    if ($Role -eq 'edge') {
        $hasCertificate = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/edge.crt'
        $hasKey = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/edge.key'
        if (-not $hasCertificate) { return [pscustomobject]@{ Certificate = $null; Orphan = $hasKey } }
        if (-not $hasKey) { Stop-Installer -Code Error -Message "pki/edge.crt existe sem pki/edge.key em secretsDir; use 'recreate pki:edge'." }
        try {
            # O certificado é público e não passa por Read-InstallerSecret, que o registraria para redação.
            $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem(
                [System.IO.File]::ReadAllText((Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name 'pki/edge.crt')),
                (Read-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/edge.key'))
        }
        catch { Stop-Installer -Code Error -Message "pki/edge.crt e pki/edge.key em secretsDir não formam um par válido; use 'recreate pki:edge'." }
        return [pscustomobject]@{ Certificate = $certificate; Orphan = $false }
    }

    $passName = 'pki/api.pass'
    $hasPfx = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/api.pfx'
    $hasPass = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $passName
    if (-not $hasPfx) { return [pscustomobject]@{ Certificate = $null; Orphan = $hasPass } }
    if (-not $hasPass) { Stop-Installer -Code Error -Message "pki/api.pfx existe sem pki/api.pass em secretsDir; use 'recreate pki:api'." }
    $password = Read-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $passName
    try {
        $certificate = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12(
            [System.IO.File]::ReadAllBytes((Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name 'pki/api.pfx')), $password)
    }
    catch { Stop-Installer -Code Error -Message "pki/api.pfx não abre com a senha de pki/api.pass; use 'recreate pki:api'." }
    return [pscustomobject]@{ Certificate = $certificate; Orphan = $false }
}

function Write-ServiceCertificate {
    # A chave (ou a senha do PFX) vai antes do certificado: só ela gravada é sobra de execução interrompida e é substituída.
    param([string] $SecretsDirectory, [string] $Role, $Certificate, [object[]] $Chain, [bool] $Orphan)

    if ($Role -eq 'edge') {
        Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/edge.key' -Value (ConvertTo-PrivateKeyPem -Certificate $Certificate) -Recreate:$Orphan
        $pem = (@($Certificate) + @($Chain) | ForEach-Object { ConvertTo-CertificatePem -Certificate $_ }) -join ''
        Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/edge.crt' -Bytes ([System.Text.Encoding]::UTF8.GetBytes($pem))
        return
    }

    $password = New-InstallerSecret
    Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/api.pass' -Value $password -Recreate:$Orphan
    Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/api.pfx' -Bytes (ConvertTo-CertificatePfx -Certificate $Certificate -Password $password -Chain $Chain)
}

function Initialize-ServiceCertificate {
    <#
    .SYNOPSIS
    Certificado de servidor da borda (pki/edge.crt e edge.key) ou da API (pki/api.pfx e api.pass) em secretsDir (FR-016, FR-017).
    .DESCRIPTION
    Com `local-ca`, um certificado existente é validado contra a CA e reaproveitado, com aviso perto do vencimento;
    inválido interrompe e aponta `recreate pki:<papel>`. Sem certificado, um novo é emitido.

    Com `organization`, o certificado e a chave informados são validados (SAN, serverAuth, validade, cadeia até as raízes
    do sistema ou até a raiz da cadeia informada e o par chave/certificado) e gravados na primeira vez. Se secretsDir já
    tem outro certificado, a troca exige `recreate pki:<papel>`.
    .OUTPUTS
    Role, Origin, Created, Certificate e Chain (a cadeia informada pela organização; vazia com a CA local).
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][ValidateSet('edge', 'api')][string] $Role,
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $CertificateAuthority,
        [DateTimeOffset] $Now = [DateTimeOffset]::UtcNow
    )

    $secretsDir = $Answers.secretsDir
    $source = $Answers.certificates[$Role]
    $names = Get-ServiceCertificateNames -Answers $Answers -Role $Role
    $existing = Read-ExistingServiceCertificate -SecretsDirectory $secretsDir -Role $Role
    $chain = @()
    $created = $false

    if ($source.source -eq 'organization') {
        $keyPem = Read-SecretReference -Reference $source.key -Purpose "chave do certificado $Role"
        $certificate = Import-OrganizationCertificate -CertificatePath $source.certificate -KeyPem $keyPem
        $informed = Read-PemCertificates -Path $(if ($source.Contains('chain')) { $source.chain } else { $null })
        $system = Read-SystemRoots
        $chain = @($informed | Where-Object { $_.Thumbprint -ne $certificate.Thumbprint })
        [System.Security.Cryptography.X509Certificates.X509Certificate2[]] $roots = @($chain | Where-Object { $_.Subject -eq $_.Issuer }) + $system
        [System.Security.Cryptography.X509Certificates.X509Certificate2[]] $intermediates = @($chain | Where-Object { $_.Subject -ne $_.Issuer })
        $problems = Test-InstallerCertificate -Certificate $certificate -Usage Server -RequiredNames $names -TrustedRoots $roots `
            -Intermediates $intermediates -Now $Now -RequirePrivateKey
        if ($problems.Count -gt 0) {
            Stop-Installer -Code Usage -Message "O certificado da organização para $Role ($($source.certificate)) não serve: $($problems -join '; '). Nada foi gravado."
        }

        if ($existing.Certificate) {
            if ((Get-CertificateFingerprint -Certificate $existing.Certificate) -ne (Get-CertificateFingerprint -Certificate $certificate)) {
                Stop-Installer -Code Declined -Message ("secretsDir já tem outro certificado para $Role; o informado em certificates.$Role só substitui o atual " +
                    "com 'recreate pki:$Role'.")
            }
        }
        else {
            Write-ServiceCertificate -SecretsDirectory $secretsDir -Role $Role -Certificate $certificate -Chain $chain -Orphan $existing.Orphan
            $created = $true
        }
    }
    elseif ($existing.Certificate) {
        $certificate = $existing.Certificate
        $problems = Test-InstallerCertificate -Certificate $certificate -Usage Server -RequiredNames $names -TrustedRoots @($CertificateAuthority) -Now $Now -RequirePrivateKey
        if ($problems.Count -gt 0) {
            Stop-Installer -Code Error -Message "O certificado de $Role em secretsDir não serve: $($problems -join '; '). Use 'recreate pki:$Role'."
        }
    }
    else {
        $algorithm = if ($source.Contains('keyAlgorithm')) { $source.keyAlgorithm } else { 'ecdsa-p256' }
        $certificate = New-InstallerCertificate -Issuer $CertificateAuthority -CommonName $names[0] -Usage Server -DnsNames $names -Algorithm $algorithm
        Write-ServiceCertificate -SecretsDirectory $secretsDir -Role $Role -Certificate $certificate -Chain @($CertificateAuthority) -Orphan $existing.Orphan
        $created = $true
    }

    $days = ($certificate.NotAfter.ToUniversalTime() - $Now.UtcDateTime).TotalDays
    if (-not $created -and $days -lt [int] $Answers.certificates.renewWarningDays) {
        $action = if ($source.source -eq 'organization') { "troque o certificado da organização e use 'recreate pki:$Role'" } else { "use 'recreate pki:$Role'" }
        Write-InstallerLine -ToStandardError -Message "Aviso: o certificado de $Role vence em $([int] [math]::Floor($days)) dias; $action."
    }

    Invoke-StateSql -State $State -Name 'certificate-upsert' -Parameters @{
        role = $Role; origin = $source.source; subject_alt_names_json = (ConvertTo-Json -InputObject @(Get-CertificateDnsNames -Certificate $certificate) -Compress)
        not_after = Get-UtcText ([DateTimeOffset] $certificate.NotAfter); fingerprint_sha256 = Get-CertificateFingerprint -Certificate $certificate
        secret_ref = $(if ($Role -eq 'edge') { 'pki/edge.key' } else { 'pki/api.pfx' }); ca_state = $null; now = Get-UtcText
    }

    return [pscustomobject]@{ Role = $Role; Origin = $source.source; Created = $created; Certificate = $certificate; Chain = $chain }
}

function Resolve-ServiceUserId {
    # UID do processo do serviço: o `user` do compose ou o USER da imagem; nome é resolvido pelo `id` da própria imagem.
    param($Service, [string] $Cli)

    $user = if ($Service.Contains('user')) { [string] $Service.user } else {
        $inspect = Invoke-InstallerProcess -FilePath $Cli -ArgumentList @('image', 'inspect', '--format', '{{.Config.User}}', $Service.image)
        if ($inspect.ExitCode -ne 0) { Stop-Installer -Code Error -Message "A imagem $($Service.image) não está no engine: $($inspect.Error.Trim())" }
        $inspect.Output.Trim()
    }
    $name = ($user -split ':')[0]
    if (-not $name) { return 0 }
    if ($name -match '^[0-9]+$') { return [int] $name }
    $id = Invoke-InstallerProcess -FilePath $Cli -ArgumentList @('run', '--rm', '--log-driver', 'none', '--network', 'none', '--entrypoint', 'id', $Service.image, '-u', $name)
    if ($id.ExitCode -ne 0 -or $id.Output.Trim() -notmatch '^[0-9]+$') { Stop-Installer -Code Error -Message "Não foi possível resolver o usuário '$name' da imagem $($Service.image)." }
    return [int] $id.Output.Trim()
}

function Set-ServiceSecretOwner {
    <#
    .SYNOPSIS
    Dá a cada arquivo de secretsDir montado nos serviços o dono que o processo do serviço usa (D-015). Roda antes de cada
    etapa da subida, porque alguns segredos só existem depois de etapas anteriores (a conexão da API, depois do banco).
    .DESCRIPTION
    O Compose monta os `secrets:` de arquivo sem trocar dono nem modo, e os arquivos são 0600 do usuário do host, que é
    o root dos containers no engine rootless. Um serviço que roda com outro usuário (Keycloak, API, BFF) não os leria.
    O instalador roda como root no mesmo namespace de usuários e troca o dono para o UID do serviço, que no host vira um
    subuid do operador; o modo continua 0600. Serviço que roda como root lê qualquer arquivo e não muda o dono. Dois
    serviços com usuários diferentes no mesmo arquivo interrompem, porque um deles não conseguiria ler.
    .OUTPUTS
    Caminho do arquivo -> UID aplicado.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([System.Collections.IDictionary])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Model,
        [string[]] $Services = @($Model.services.Keys),
        [string] $Cli = 'docker',
        [string] $Chown = 'chown'
    )

    $owners = [ordered]@{}
    $consumers = @{}
    foreach ($name in @($Model.services.Keys | Where-Object { $_ -in $Services })) {
        $service = $Model.services[$name]
        if (-not $service.Contains('secrets') -or @($service.secrets).Count -eq 0) { continue }
        $uid = Resolve-ServiceUserId -Service $service -Cli $Cli
        if ($uid -eq 0) { continue }
        foreach ($mount in @($service.secrets)) {
            $file = $Model.secrets[$mount.source].file
            if ($owners.Contains($file) -and $owners[$file] -ne $uid) {
                Stop-Installer -Code Error -Message "O segredo $($mount.source) é lido por $($consumers[$file]) e $name com usuários diferentes; um deles não conseguiria lê-lo."
            }
            $owners[$file] = $uid
            $consumers[$file] = $name
        }
    }

    foreach ($file in $owners.Keys) {
        if ($PSCmdlet.ShouldProcess($file, "dono $($owners[$file])")) {
            $result = Invoke-InstallerProcess -FilePath $Chown -ArgumentList @([string] $owners[$file], $file)
            if ($result.ExitCode -ne 0) {
                Stop-Installer -Code Error -Message "Não foi possível dar o dono $($owners[$file]) a $file ($($result.Error.Trim())). O instalador precisa rodar como root do engine rootless, como o install.sh faz."
            }
        }
    }

    return $owners
}

function Initialize-DataProtectionCertificate {
    <#
    .SYNOPSIS
    Certificado que protege o key ring do Data Protection do BFF (pki/bff-dataprotection.pfx e .pass em secretsDir).
    .DESCRIPTION
    RSA-3072 autoassinado de 10 anos, sem EKU: o XML cifrado do Data Protection só aceita RSA, e o certificado não
    autentica ninguém. Um existente é reaproveitado se abrir com a senha e tiver a chave RSA; senão interrompe e aponta
    `recreate pki:bff-dataprotection`, que invalida as sessões abertas no console. Só a senha, sem o PFX, é sobra de
    execução interrompida e é substituída.
    .OUTPUTS
    Certificate e Created.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param([Parameter(Mandatory)][string] $SecretsDirectory)

    $pfxName = 'pki/bff-dataprotection.pfx'
    $passName = 'pki/bff-dataprotection.pass'
    $hasPfx = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $pfxName
    $hasPass = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $passName
    if ($hasPfx) {
        if (-not $hasPass) { Stop-Installer -Code Error -Message "$pfxName existe sem $passName em secretsDir; use 'recreate pki:bff-dataprotection'." }
        $password = Read-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $passName
        try {
            $certificate = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12(
                [System.IO.File]::ReadAllBytes((Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name $pfxName)), $password)
        }
        catch { Stop-Installer -Code Error -Message "$pfxName não abre com a senha de $passName; use 'recreate pki:bff-dataprotection'." }
        if (-not [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)) {
            Stop-Installer -Code Error -Message "$pfxName não tem chave RSA privada; use 'recreate pki:bff-dataprotection'."
        }
        return [pscustomobject]@{ Certificate = $certificate; Created = $false }
    }

    $key = [System.Security.Cryptography.RSA]::Create(3072)
    $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=dokpod-bff-dataprotection, O=Dokpod', $key,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new('KeyEncipherment, DataEncipherment', $true))
    $now = [DateTimeOffset]::UtcNow
    $certificate = $request.CreateSelfSigned($now.AddMinutes(-5), $now.AddYears(10))
    $password = New-InstallerSecret
    Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $passName -Value $password -Recreate:$hasPass
    Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $pfxName -Bytes (ConvertTo-CertificatePfx -Certificate $certificate -Password $password)
    return [pscustomobject]@{ Certificate = $certificate; Created = $true }
}

function Get-ServiceStage {
    <#
    .SYNOPSIS
    As etapas da subida, na ordem, com os serviços de cada uma que existem no compose.yaml gerado.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject[]])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Model)

    $stages = foreach ($name in $script:Stages.Keys) {
        $services = @($script:Stages[$name] | Where-Object { $Model.services.Contains($_) })
        if ($services.Count -gt 0) { [pscustomobject]@{ Name = $name; Services = $services } }
    }

    return , @($stages)
}

function Get-ComposeArguments {
    param([System.Collections.IDictionary] $Answers, [string] $Project)
    return @('compose', '--file', (Join-Path $Answers.installDir 'compose.yaml'), '--project-name', $Project)
}

function Get-ServiceHealth {
    <#
    .SYNOPSIS
    Estado e health de cada serviço pedido, pelo `docker compose ps`. Healthy: rodando e, com healthcheck, saudável.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string[]] $Services,
        [string] $Project = $script:Project,
        [string] $Cli = 'docker'
    )

    $result = Invoke-InstallerProcess -FilePath $Cli -ArgumentList (@(Get-ComposeArguments -Answers $Answers -Project $Project) + @('ps', '--all', '--format', 'json') + $Services)
    if ($result.ExitCode -ne 0) {
        Stop-Installer -Code Error -Message "Não foi possível consultar os serviços do compose (código $($result.ExitCode)): $($result.Error.Trim())"
    }

    # O Compose escreve um objeto JSON por linha; versões antigas, um array só.
    $containers = @(foreach ($line in ($result.Output -split "`n" | Where-Object { $_.Trim() })) { $line | ConvertFrom-Json -AsHashtable })
    $containers = @($containers | ForEach-Object { $_ })
    $health = foreach ($service in $Services) {
        $container = $containers | Where-Object { $_['Service'] -eq $service } | Select-Object -First 1
        $state = if ($container) { [string] $container['State'] } else { 'ausente' }
        $check = if ($container -and $container.Contains('Health')) { [string] $container['Health'] } else { '' }
        [pscustomobject]@{ Service = $service; State = $state; Health = $check; Healthy = $state -eq 'running' -and $check -in '', 'healthy' }
    }

    return , @($health)
}

function Invoke-ServiceStage {
    <#
    .SYNOPSIS
    Sobe os serviços de uma etapa com `docker compose up --detach --wait` e confere o health de cada um (FR-021).
    .OUTPUTS
    O health de cada serviço da etapa. Serviço que não fica saudável interrompe com o nome e o comando de logs.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Stage,
        [Parameter(Mandatory)][string[]] $Services,
        [int] $TimeoutSeconds = $script:WaitTimeoutSeconds,
        [string] $Project = $script:Project,
        [string] $Cli = 'docker'
    )

    $arguments = @(Get-ComposeArguments -Answers $Answers -Project $Project) + @('up', '--detach', '--wait', '--wait-timeout', [string] $TimeoutSeconds) + $Services
    $result = Invoke-InstallerProcess -FilePath $Cli -ArgumentList $arguments
    $health = Get-ServiceHealth -Answers $Answers -Services $Services -Project $Project -Cli $Cli
    $failing = @($health | Where-Object { -not $_.Healthy })
    if ($result.ExitCode -ne 0 -or $failing.Count -gt 0) {
        $detail = ($failing | ForEach-Object { "$($_.Service) ($($_.State)$(if ($_.Health) { ", $($_.Health)" }))" }) -join ', '
        if ($result.ExitCode -ne 0 -and $result.Error.Trim()) {
            $compose = "compose: $(($result.Error.Trim() -split "`n" | Select-Object -Last 1).Trim())"
            $detail = if ($detail) { "$detail; $compose" } else { $compose }
        }
        $logs = ($Services | ForEach-Object { "docker compose -p $Project logs $_" }) -join ' | '
        Stop-Installer -Code Error -Message ("A etapa de serviços '$Stage' não ficou saudável (espera de até $TimeoutSeconds s): $detail. Veja os logs com $logs; " +
            'corrija e reexecute o instalador, que retoma desta etapa.')
    }

    return , $health
}

function Restart-ServiceForUpstreams {
    <#
    .SYNOPSIS
    Reinicia a borda e espera o health dela quando um upstream (web, BFF, Keycloak) foi recriado e ela não.
    .DESCRIPTION
    O nginx resolve web:8080, bff:8080 e keycloak:8080 na partida e guarda o IP; um container recriado ganha outro IP, e
    a borda passaria a responder 502. A configuração não usa `resolver`, porque o DNS interno muda entre Docker e
    Podman. Devolve se reiniciou.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][object[]] $Plan,
        [string] $Service = 'edge',
        [string[]] $Upstreams = @('web', 'bff', 'keycloak'),
        [int] $TimeoutSeconds = $script:WaitTimeoutSeconds,
        [string] $Project = $script:Project,
        [string] $Cli = 'docker'
    )

    $edge = $Plan | Where-Object Component -eq $Service | Select-Object -First 1
    $changed = @($Plan | Where-Object { $_.Component -in $Upstreams -and $_.Action -ne 'keep' })
    if (-not $edge -or $edge.Action -ne 'keep' -or $changed.Count -eq 0) { return $false }
    if (-not $PSCmdlet.ShouldProcess($Service, 'reiniciar')) { return $false }

    $result = Invoke-InstallerProcess -FilePath $Cli -ArgumentList (@(Get-ComposeArguments -Answers $Answers -Project $Project) + @('restart', $Service))
    if ($result.ExitCode -ne 0) {
        Stop-Installer -Code Error -Message "Não foi possível reiniciar $Service depois da troca de $($changed.Component -join ', '): $($result.Error.Trim())"
    }

    [void] (Invoke-ServiceStage -Answers $Answers -Stage $Service -Services @($Service) -TimeoutSeconds $TimeoutSeconds -Project $Project -Cli $Cli)
    return $true
}

function Get-LicenseSlot {
    <#
    .SYNOPSIS
    Slot da licença (spec 031) pelo estágio do ambiente local: `prd` em produção, `hml` nos demais.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    return $(if ($Answers.localEnvironment.stage -eq 'production') { 'prd' } else { 'hml' })
}

function Invoke-LicenseImport {
    <#
    .SYNOPSIS
    Importa a licença da edição Business pelo `admin licensing import` da imagem da API (FR-031). Sem licença nas
    respostas, não faz nada e devolve a instrução para importar depois.
    .DESCRIPTION
    O arquivo é copiado para uma pasta temporária em installDir, montada só para leitura no container avulso do modo
    administrativo, e apagado ao fim. A mesma licença já ativa no slot não muda nada (idempotente).
    .OUTPUTS
    Imported, Slot e Changed; ou Imported falso com a instrução em Next.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Image,
        [string] $Network = $script:AdminNetwork,
        [string] $Cli = 'docker'
    )

    if ($Answers.edition -ne 'business') {
        Stop-Installer -Code Usage -Message 'A licença só existe na edição Business.'
    }

    $slot = Get-LicenseSlot -Answers $Answers
    if (-not $Answers.Contains('license') -or -not $Answers.license) {
        return [pscustomobject]@{ Imported = $false; Slot = $slot; Changed = $false
            Next = "Sem licença nas respostas: importe-a depois pelo console ou informe 'license' e reexecute o instalador." }
    }

    if (-not (Test-Path -LiteralPath $Answers.license -PathType Leaf)) {
        Stop-Installer -Code Usage -Message "O arquivo de licença $($Answers.license) não existe."
    }

    $work = Join-Path $Answers.installDir ".license-import-$([guid]::NewGuid().ToString('N'))"
    [void] [System.IO.Directory]::CreateDirectory($work)
    try {
        Copy-Item -LiteralPath $Answers.license -Destination (Join-Path $work 'license.jws')
        $result = Invoke-AdminCommand -Image $Image -Network $Network -SecretsDirectory $Answers.secretsDir -WorkDirectory $work -Cli $Cli -Arguments @(
            'licensing', 'import', '--file', "$($script:AdminWorkTarget)/license.jws", '--slot', $slot,
            '--actor', [string] $Answers.operator, '--reason', 'instalação: licença da edição Business')
        if ($result.ExitCode -ne 0) {
            Stop-Installer -Code Error -Message "A importação da licença falhou (código $($result.ExitCode)): $($result.Error)"
        }
    }
    finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }

    $changed = $result.Data -and $result.Data.Contains('changed') -and [bool] $result.Data['changed']
    return [pscustomobject]@{ Imported = $true; Slot = $slot; Changed = $changed; Next = $null }
}

function Write-InstallSummary {
    <#
    .SYNOPSIS
    Resumo final: URL pública, usuário administrador, relatório e próximos passos. A senha temporária só aparece aqui.
    .DESCRIPTION
    Com TTY, a senha de secretsDir/initial-admin-password é mostrada uma vez, num bloco separado, e o arquivo é apagado.
    Sem TTY, o resumo indica o caminho do arquivo, que sai na primeira execução seguinte em que o Keycloak mostrar a
    senha já trocada (Invoke-KeycloakRealm). A senha nunca vai para o relatório (FR-014, FR-033).
    .OUTPUTS
    PasswordShown e PasswordFile (caminho, quando ficou para o operador).
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $ReportPath,
        [string[]] $NextSteps = @(),
        [bool] $Interactive = (Test-InteractiveSession)
    )

    $url = "$($Answers.publicUrl.TrimEnd('/'))/dokpod/"
    Write-InstallerLine -Message ''
    Write-InstallerLine -Message "Dokpod $($Answers.version) ($($Answers.edition)) instalado."
    Write-InstallerLine -Message "Console: $url"
    if ($Answers.keycloak.mode -eq 'provision') { Write-InstallerLine -Message "Administrador: $($Answers.keycloak.initialAdmin.username)" }
    Write-InstallerLine -Message "Relatório: $ReportPath"

    $name = (Get-KeycloakDefaults).InitialPasswordSecret
    $passwordFile = Resolve-SecretPath -SecretsDirectory $Answers.secretsDir -Name $name
    $shown = $false
    $left = $null
    if (Test-Path -LiteralPath $passwordFile -PathType Leaf) {
        if ($Interactive) {
            $temporary = [System.IO.File]::ReadAllText($passwordFile)
            # Única saída de um segredo (contracts/installer-cli.md): direto no console, sem passar pela redação.
            [Console]::Out.WriteLine('')
            [Console]::Out.WriteLine('---- senha temporária (mostrada só agora) ----')
            [Console]::Out.WriteLine($temporary)
            [Console]::Out.WriteLine('---- o primeiro login pede a troca ----')
            Remove-Item -LiteralPath $passwordFile -Force
            $shown = $true
        }
        else {
            $left = $passwordFile
            Write-InstallerLine -Message "Senha temporária: em $passwordFile (0600). O primeiro login pede a troca; o arquivo sai na próxima execução depois dela."
        }
    }

    if ($NextSteps.Count -gt 0) {
        Write-InstallerLine -Message ''
        Write-InstallerLine -Message 'Próximos passos:'
        foreach ($step in $NextSteps) { Write-InstallerLine -Message "- $step" }
    }

    return [pscustomobject]@{ PasswordShown = $shown; PasswordFile = $left }
}

Export-ModuleMember -Function Get-ServiceCertificateNames, Initialize-ServiceCertificate, Initialize-DataProtectionCertificate, Set-ServiceSecretOwner, Get-ServiceStage, Get-ServiceHealth, Invoke-ServiceStage, Restart-ServiceForUpstreams,
Get-LicenseSlot, Invoke-LicenseImport, Write-InstallSummary
