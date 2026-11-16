#Requires -Version 7.4
<#
.SYNOPSIS
Certificados da instalação (spec 033 T022, FR-016 a FR-018, D-005): CA local, certificados de servidor e de cliente e a
validação de todo certificado, gerado ou da organização. Usa as APIs de criptografia do .NET; a chave da CA só existe
em secretsDir/pki.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module (Join-Path $PSScriptRoot 'Secrets.psm1')

$script:ServerAuth = '1.3.6.1.5.5.7.3.1'
$script:ClientAuth = '1.3.6.1.5.5.7.3.2'
$script:LeafDays = 397
$script:CaYears = 10

function New-InstallerKey {
    param([ValidateSet('ecdsa-p256', 'rsa-3072')][string] $Algorithm)

    if ($Algorithm -eq 'rsa-3072') {
        return [System.Security.Cryptography.RSA]::Create(3072)
    }

    return [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
}

function New-CertificateRequestFor {
    param($Key, [string] $Subject)

    $name = [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new($Subject)
    if ($Key -is [System.Security.Cryptography.RSA]) {
        return [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $name, $Key, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    }

    return [System.Security.Cryptography.X509Certificates.CertificateRequest]::new($name, $Key, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
}

function New-LocalCertificateAuthority {
    <#
    .SYNOPSIS
    CA local de 10 anos, CA:true e pathlen:0, que só assina certificados finais.
    #>
    [CmdletBinding()]
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2])]
    param(
        [string] $CommonName = 'Dokpod CA local',
        [ValidateSet('ecdsa-p256', 'rsa-3072')][string] $Algorithm = 'ecdsa-p256',
        [DateTimeOffset] $NotBefore = [DateTimeOffset]::UtcNow.AddMinutes(-5)
    )

    $key = New-InstallerKey -Algorithm $Algorithm
    $request = New-CertificateRequestFor -Key $key -Subject "CN=$CommonName, O=Dokpod"
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $true, 0, $true))
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]'KeyCertSign, CrlSign', $true))
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension]::new($request.PublicKey, $false))
    return $request.CreateSelfSigned($NotBefore, $NotBefore.AddYears($script:CaYears))
}

function Initialize-LocalCertificateAuthority {
    <#
    .SYNOPSIS
    CA local da instalação em secretsDir/pki (ca.crt e ca.key): carrega a existente ou cria uma nova. O certificado sem a
    chave interrompe sem gravar nada; a troca da CA é `recreate pki:ca`.
    .OUTPUTS
    Certificate (com a chave privada) e Created.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string] $SecretsDirectory,
        [ValidateSet('ecdsa-p256', 'rsa-3072')][string] $Algorithm = 'ecdsa-p256'
    )

    $hasCertificate = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/ca.crt'
    $hasKey = Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/ca.key'
    if ($hasCertificate -and $hasKey) {
        # O certificado é público: lido sem Read-InstallerSecret, que o registraria para redação e barraria certs/ca.crt.
        $certificatePem = [System.IO.File]::ReadAllText((Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name 'pki/ca.crt'))
        $keyPem = Read-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/ca.key'
        try { $ca = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem($certificatePem, $keyPem) }
        catch { Stop-Installer -Code Error -Message "pki/ca.crt e pki/ca.key em secretsDir não formam um par válido; use 'recreate pki:ca'." }
        return [pscustomobject]@{ Certificate = $ca; Created = $false }
    }

    if ($hasCertificate) {
        Stop-Installer -Code Error -Message "pki/ca.crt existe sem pki/ca.key em secretsDir; nada foi gravado. Use 'recreate pki:ca'."
    }

    # A chave vai antes do certificado. Só a chave quer dizer execução interrompida entre as duas gravações: nenhum
    # certificado foi assinado por ela, então ela é substituída. Só o certificado (acima) quer dizer chave perdida.
    $ca = New-LocalCertificateAuthority -Algorithm $Algorithm
    Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/ca.key' -Value (ConvertTo-PrivateKeyPem -Certificate $ca) -Recreate:$hasKey
    Write-InstallerSecret -SecretsDirectory $SecretsDirectory -Name 'pki/ca.crt' -Bytes ([System.Text.Encoding]::UTF8.GetBytes((ConvertTo-CertificatePem -Certificate $ca)))
    return [pscustomobject]@{ Certificate = $ca; Created = $true }
}

function New-InstallerCertificate {
    <#
    .SYNOPSIS
    Certificado final de 397 dias assinado pela CA local: Server (serverAuth, borda e API) ou Client (clientAuth, agentes).
    #>
    [CmdletBinding()]
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2])]
    param(
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Issuer,
        [Parameter(Mandatory)][string] $CommonName,
        [Parameter(Mandatory)][ValidateSet('Server', 'Client')][string] $Usage,
        [string[]] $DnsNames = @(),
        [ValidateSet('ecdsa-p256', 'rsa-3072')][string] $Algorithm = 'ecdsa-p256',
        [DateTimeOffset] $NotBefore = [DateTimeOffset]::UtcNow.AddMinutes(-5),
        [int] $Days = $script:LeafDays
    )

    $key = New-InstallerKey -Algorithm $Algorithm
    $request = New-CertificateRequestFor -Key $key -Subject "CN=$CommonName, O=Dokpod"
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
    $keyUsage = if ($key -is [System.Security.Cryptography.RSA]) { 'DigitalSignature, KeyEncipherment' } else { 'DigitalSignature' }
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new($keyUsage, $true))
    $oids = [System.Security.Cryptography.OidCollection]::new()
    [void] $oids.Add([System.Security.Cryptography.Oid]::new($(if ($Usage -eq 'Server') { $script:ServerAuth } else { $script:ClientAuth })))
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids, $false))
    if ($DnsNames.Count -gt 0) {
        $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        foreach ($name in $DnsNames) { $san.AddDnsName($name) }
        $request.CertificateExtensions.Add($san.Build($false))
    }

    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension]::new($request.PublicKey, $false))
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509AuthorityKeyIdentifierExtension]::CreateFromCertificate($Issuer, $true, $false))
    $notAfter = $NotBefore.AddDays($Days)
    if ($notAfter -gt $Issuer.NotAfter) { $notAfter = [DateTimeOffset] $Issuer.NotAfter }
    $serial = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
    $serial[0] = $serial[0] -band 0x7F
    $certificate = $request.Create($Issuer, $NotBefore, $notAfter, $serial)
    if ($key -is [System.Security.Cryptography.RSA]) {
        return [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($certificate, $key)
    }

    return [System.Security.Cryptography.X509Certificates.ECDsaCertificateExtensions]::CopyWithPrivateKey($certificate, $key)
}

function Get-CertificateFingerprint {
    <#
    .SYNOPSIS
    SHA-256 do certificado (DER) em hexadecimal maiúsculo, como o canal mTLS e agent_identities comparam.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)

    return [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($Certificate.RawData))
}

function ConvertTo-CertificatePem {
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)

    return $Certificate.ExportCertificatePem() + "`n"
}

function ConvertTo-PrivateKeyPem {
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)

    $key = [System.Security.Cryptography.X509Certificates.ECDsaCertificateExtensions]::GetECDsaPrivateKey($Certificate) ??
        [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
    if (-not $key) { Stop-Installer -Code Error -Message 'O certificado não tem chave privada.' }
    $pem = $key.ExportPkcs8PrivateKeyPem() + "`n"
    Register-InstallerSecret -Value $pem
    return $pem
}

function ConvertTo-CertificatePfx {
    <#
    .SYNOPSIS
    PFX protegido pela senha informada (AES-256 e SHA-256, 100 000 iterações, abaixo do limite que o .NET aceita ao carregar),
    com a cadeia opcional.
    #>
    [CmdletBinding()]
    [OutputType([byte[]])]
    param(
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [Parameter(Mandatory)][string] $Password,
        [System.Security.Cryptography.X509Certificates.X509Certificate2[]] $Chain = @()
    )

    Register-InstallerSecret -Value $Password
    $collection = [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
    [void] $collection.Add($Certificate)
    foreach ($item in $Chain) { [void] $collection.Add([System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificate($item.RawData)) }
    $parameters = [System.Security.Cryptography.PbeParameters]::new(
        [System.Security.Cryptography.PbeEncryptionAlgorithm]::Aes256Cbc, [System.Security.Cryptography.HashAlgorithmName]::SHA256, 100000)
    return , $collection.ExportPkcs12($parameters, $Password)
}

function Import-OrganizationCertificate {
    <#
    .SYNOPSIS
    Certificado e chave da organização em PEM. Uma chave que não corresponde ao certificado é recusada.
    #>
    [CmdletBinding()]
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2])]
    param(
        [Parameter(Mandatory)][string] $CertificatePath,
        [Parameter(Mandatory)][string] $KeyPem
    )

    Register-InstallerSecret -Value $KeyPem
    try {
        return [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([System.IO.File]::ReadAllText($CertificatePath), $KeyPem)
    }
    catch {
        Stop-Installer -Code Usage -Message "O certificado $CertificatePath e a chave informada não formam um par válido."
    }
}

function Test-InstallerCertificate {
    <#
    .SYNOPSIS
    Problemas de um certificado para o uso (FR-017): nomes no SAN, EKU, validade, cadeia até as raízes e chave privada.
    Sem problemas, devolve uma lista vazia.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [Parameter(Mandatory)][ValidateSet('Server', 'Client')][string] $Usage,
        [string[]] $RequiredNames = @(),
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2[]] $TrustedRoots,
        [System.Security.Cryptography.X509Certificates.X509Certificate2[]] $Intermediates = @(),
        [DateTimeOffset] $Now = [DateTimeOffset]::UtcNow,
        [switch] $RequirePrivateKey
    )

    $problems = [System.Collections.Generic.List[string]]::new()
    $names = Get-CertificateDnsNames -Certificate $Certificate
    foreach ($name in $RequiredNames) {
        if (-not ($names | Where-Object { Test-DnsNameMatch -Pattern $_ -Name $name })) {
            $problems.Add("o SAN não contém $name (tem: $(if ($names) { $names -join ', ' } else { 'nenhum' }))")
        }
    }

    $wanted = if ($Usage -eq 'Server') { $script:ServerAuth } else { $script:ClientAuth }
    $eku = $Certificate.Extensions | Where-Object { $_ -is [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension] }
    if (-not $eku -or -not ($eku.EnhancedKeyUsages | Where-Object Value -eq $wanted)) {
        $problems.Add("falta o EKU $(if ($Usage -eq 'Server') { 'serverAuth' } else { 'clientAuth' })")
    }

    if ($Now.UtcDateTime -lt $Certificate.NotBefore.ToUniversalTime()) { $problems.Add("ainda não é válido (a partir de $($Certificate.NotBefore.ToString('u')))") }
    if ($Now.UtcDateTime -gt $Certificate.NotAfter.ToUniversalTime()) { $problems.Add("venceu em $($Certificate.NotAfter.ToString('u'))") }
    if ($RequirePrivateKey -and -not $Certificate.HasPrivateKey) { $problems.Add('não tem a chave privada') }

    $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
        $chain.ChainPolicy.TrustMode = [System.Security.Cryptography.X509Certificates.X509ChainTrustMode]::CustomRootTrust
        $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
        $chain.ChainPolicy.VerificationTime = $Now.UtcDateTime
        foreach ($root in $TrustedRoots) { [void] $chain.ChainPolicy.CustomTrustStore.Add($root) }
        foreach ($item in $Intermediates) { [void] $chain.ChainPolicy.ExtraStore.Add($item) }
        if (-not $chain.Build($Certificate)) {
            $status = ($chain.ChainStatus | Where-Object Status -ne 'NoError' | ForEach-Object { $_.Status }) -join ', '
            if ($status -notmatch '^(NotTimeValid)?$') {
                $problems.Add("a cadeia não chega a uma raiz confiável ($status)")
            }
        }
    }
    finally {
        $chain.Dispose()
    }

    return , [string[]] $problems
}

function Get-CertificateDnsNames {
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)

    $extension = $Certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' } | Select-Object -First 1
    if (-not $extension) { return , [string[]] @() }
    $san = [System.Security.Cryptography.X509Certificates.X509SubjectAlternativeNameExtension]::new($extension.RawData, $extension.Critical)
    $names = [System.Collections.Generic.List[string]]::new()
    foreach ($name in $san.EnumerateDnsNames()) { $names.Add($name) }
    return , $names.ToArray()
}

function Test-DnsNameMatch {
    param([string] $Pattern, [string] $Name)

    if ($Pattern.StartsWith('*.', [System.StringComparison]::Ordinal)) {
        $suffix = $Pattern.Substring(1)
        return $Name.EndsWith($suffix, [System.StringComparison]::OrdinalIgnoreCase) -and $Name.Split('.').Count -eq $Pattern.Split('.').Count
    }

    return [string]::Equals($Pattern, $Name, [System.StringComparison]::OrdinalIgnoreCase)
}

Export-ModuleMember -Function New-LocalCertificateAuthority, Initialize-LocalCertificateAuthority, New-InstallerCertificate, Get-CertificateFingerprint, ConvertTo-CertificatePem,
ConvertTo-PrivateKeyPem, ConvertTo-CertificatePfx, Import-OrganizationCertificate, Test-InstallerCertificate, Get-CertificateDnsNames
