#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T022 (FR-016 a FR-018, D-005): CA local, certificados de servidor e de cliente, PFX e validação.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Secrets.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Pki.psm1') -Force
    $script:ca = New-LocalCertificateAuthority
}

Describe 'CA local persistida em secretsDir (T031)' {
    BeforeEach {
        $script:dir = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-ca-$([guid]::NewGuid().ToString('N'))"
    }

    AfterEach {
        Remove-Item -LiteralPath $script:dir -Recurse -Force -ErrorAction SilentlyContinue
        Clear-InstallerSecrets
    }

    It 'cria uma vez com chave 0600 e a reexecução carrega a mesma CA, com a chave' {
        $first = Initialize-LocalCertificateAuthority -SecretsDirectory $script:dir
        $first.Created | Should -BeTrue
        [System.IO.File]::GetUnixFileMode((Join-Path $script:dir 'pki/ca.key')) | Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite')

        $again = Initialize-LocalCertificateAuthority -SecretsDirectory $script:dir
        $again.Created | Should -BeFalse
        $again.Certificate.Thumbprint | Should -Be $first.Certificate.Thumbprint
        $again.Certificate.HasPrivateKey | Should -BeTrue
    }

    It 'certificado sem a chave interrompe sem gravar; só a chave (execução interrompida) é substituída' {
        [void] (Initialize-LocalCertificateAuthority -SecretsDirectory $script:dir)
        $certificate = [System.IO.File]::ReadAllText((Join-Path $script:dir 'pki/ca.crt'))
        Remove-Item -LiteralPath (Join-Path $script:dir 'pki/ca.key')
        { Initialize-LocalCertificateAuthority -SecretsDirectory $script:dir } | Should -Throw '*recreate pki:ca*'
        [System.IO.File]::ReadAllText((Join-Path $script:dir 'pki/ca.crt')) | Should -Be $certificate
        Test-Path (Join-Path $script:dir 'pki/ca.key') | Should -BeFalse

        Remove-Item -LiteralPath (Join-Path $script:dir 'pki/ca.crt')
        [System.IO.File]::WriteAllText((Join-Path $script:dir 'pki/ca.key'), 'sobra')
        (Initialize-LocalCertificateAuthority -SecretsDirectory $script:dir).Created | Should -BeTrue
        [System.IO.File]::ReadAllText((Join-Path $script:dir 'pki/ca.key')) | Should -Not -Be 'sobra'
    }
}

Describe 'CA local' {
    It 'é CA com pathlen 0, ECDSA P-256 e 10 anos' {
        $constraints = $script:ca.Extensions | Where-Object { $_ -is [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension] }
        $constraints.CertificateAuthority | Should -BeTrue
        $constraints.HasPathLengthConstraint | Should -BeTrue
        $constraints.PathLengthConstraint | Should -Be 0
        [System.Security.Cryptography.X509Certificates.ECDsaCertificateExtensions]::GetECDsaPublicKey($script:ca).KeySize | Should -Be 256
        ($script:ca.NotAfter - $script:ca.NotBefore).TotalDays | Should -BeGreaterThan 3650
    }

    It 'aceita RSA 3072' {
        [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey((New-LocalCertificateAuthority -Algorithm rsa-3072)).ExportParameters($false).Modulus.Length * 8 | Should -Be 3072
    }
}

Describe 'Certificados finais' {
    It 'servidor da API tem serverAuth, SAN api e o nome público, 397 dias e passa na validação' {
        $api = New-InstallerCertificate -Issuer $script:ca -CommonName 'api' -Usage Server -DnsNames 'api', 'dokpod.lab.test'

        Test-InstallerCertificate -Certificate $api -Usage Server -RequiredNames 'api', 'dokpod.lab.test' -TrustedRoots $script:ca -RequirePrivateKey | Should -BeNullOrEmpty
        [math]::Round(($api.NotAfter - $api.NotBefore).TotalDays) | Should -Be 397
        $names = Get-CertificateDnsNames -Certificate $api
        $names | Should -Be @('api', 'dokpod.lab.test')
    }

    It 'cliente do agente tem clientAuth e é recusado como servidor' {
        $agent = New-InstallerCertificate -Issuer $script:ca -CommonName 'agent-host-local' -Usage Client

        Test-InstallerCertificate -Certificate $agent -Usage Client -TrustedRoots $script:ca | Should -BeNullOrEmpty
        Test-InstallerCertificate -Certificate $agent -Usage Server -TrustedRoots $script:ca | Should -Match 'serverAuth'
    }

    It 'fingerprint é o SHA-256 do DER em hexadecimal maiúsculo' {
        $agent = New-InstallerCertificate -Issuer $script:ca -CommonName 'agent' -Usage Client

        Get-CertificateFingerprint -Certificate $agent | Should -Match '^[0-9A-F]{64}$'
        Get-CertificateFingerprint -Certificate $agent | Should -Be ([System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($agent.RawData)))
    }

    It 'PFX abre com a senha gerada, guarda a chave e não abre sem ela' {
        $api = New-InstallerCertificate -Issuer $script:ca -CommonName 'api' -Usage Server -DnsNames 'api'
        $pfx = ConvertTo-CertificatePfx -Certificate $api -Password 'canario-033-pfx' -Chain $script:ca

        $loaded = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12($pfx, 'canario-033-pfx')
        $loaded.Thumbprint | Should -Be $api.Thumbprint
        $loaded.HasPrivateKey | Should -BeTrue
        { [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12($pfx, 'errada') } | Should -Throw
    }

    It 'não passa da validade da CA' {
        $shortCa = New-LocalCertificateAuthority -NotBefore ([DateTimeOffset]::UtcNow.AddYears(-10).AddDays(10))
        $leaf = New-InstallerCertificate -Issuer $shortCa -CommonName 'edge' -Usage Server -DnsNames 'edge'

        $leaf.NotAfter | Should -BeLessOrEqual $shortCa.NotAfter
    }
}

Describe 'Validação (FR-017)' {
    It 'recusa SAN errado com o motivo' {
        $edge = New-InstallerCertificate -Issuer $script:ca -CommonName 'edge' -Usage Server -DnsNames 'outro.lab.test'

        Test-InstallerCertificate -Certificate $edge -Usage Server -RequiredNames 'dokpod.lab.test' -TrustedRoots $script:ca | Should -Match 'SAN não contém dokpod.lab.test'
    }

    It 'aceita curinga só de um nível' {
        $edge = New-InstallerCertificate -Issuer $script:ca -CommonName 'edge' -Usage Server -DnsNames '*.lab.test'

        Test-InstallerCertificate -Certificate $edge -Usage Server -RequiredNames 'dokpod.lab.test' -TrustedRoots $script:ca | Should -BeNullOrEmpty
        Test-InstallerCertificate -Certificate $edge -Usage Server -RequiredNames 'a.dokpod.lab.test' -TrustedRoots $script:ca | Should -Not -BeNullOrEmpty
    }

    It 'recusa certificado vencido' {
        $pastCa = New-LocalCertificateAuthority -NotBefore ([DateTimeOffset]::UtcNow.AddDays(-60))
        $old = New-InstallerCertificate -Issuer $pastCa -CommonName 'edge' -Usage Server -DnsNames 'edge' -NotBefore ([DateTimeOffset]::UtcNow.AddDays(-30)) -Days 10

        Test-InstallerCertificate -Certificate $old -Usage Server -TrustedRoots $pastCa | Should -Match 'venceu'
    }

    It 'recusa cadeia que não chega à raiz confiável' {
        $other = New-LocalCertificateAuthority -CommonName 'Outra CA'
        $edge = New-InstallerCertificate -Issuer $other -CommonName 'edge' -Usage Server -DnsNames 'edge'

        Test-InstallerCertificate -Certificate $edge -Usage Server -TrustedRoots $script:ca | Should -Match 'cadeia'
    }

    It 'certificado da organização com a chave certa entra; com outra chave é recusado' {
        $edge = New-InstallerCertificate -Issuer $script:ca -CommonName 'edge' -Usage Server -DnsNames 'dokpod.lab.test'
        $other = New-InstallerCertificate -Issuer $script:ca -CommonName 'outro' -Usage Server -DnsNames 'x'
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-org-$([guid]::NewGuid().ToString('N')).pem"
        [System.IO.File]::WriteAllText($path, (ConvertTo-CertificatePem -Certificate $edge))

        (Import-OrganizationCertificate -CertificatePath $path -KeyPem (ConvertTo-PrivateKeyPem -Certificate $edge)).HasPrivateKey | Should -BeTrue
        { Import-OrganizationCertificate -CertificatePath $path -KeyPem (ConvertTo-PrivateKeyPem -Certificate $other) } | Should -Throw -ExpectedMessage '*par válido*'
    }

    It 'a chave privada em PEM é registrada para redação' {
        Clear-InstallerSecrets
        $edge = New-InstallerCertificate -Issuer $script:ca -CommonName 'edge' -Usage Server -DnsNames 'edge'
        $pem = ConvertTo-PrivateKeyPem -Certificate $edge

        Protect-InstallerText -Text $pem | Should -Be '***'
    }
}
