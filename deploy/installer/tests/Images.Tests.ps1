#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T025 (FR-005, FR-006, D-010): imagens só por digest, cosign antes do uso e código 5 sem efeito.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Images.psm1') -Force

    function Write-Manifest {
        param([string] $Version = '0.8.0', [object[]] $Artifacts)
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-manifest-$([guid]::NewGuid().ToString('N')).json"
        @{ version = $Version; artifacts = $Artifacts } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path
        return $path
    }

    function Image {
        param([string] $Name, [string] $Digest = ('sha256:' + ('a' * 64)))
        return @{ kind = 'container'; name = $Name; reference = "ghcr.io/altivy-it/$Name"; digest = $Digest }
    }

    $script:all = @((Image 'dokpod-api'), (Image 'dokpod-api-business'), (Image 'dokpod-bff'), (Image 'dokpod-web'), (Image 'dokpod-agent'),
        @{ kind = 'asset'; name = 'install.sh'; reference = 'x'; digest = 'y' })
}

Describe 'Manifesto da release' {
    It 'devolve só referência@digest e escolhe a API da edição' {
        $images = Read-ReleaseManifest -Path (Write-Manifest -Artifacts $script:all) -Version '0.8.0'
        $ce = Select-ReleaseImages -Images $images -Edition ce
        $business = Select-ReleaseImages -Images $images -Edition business

        $ce.api | Should -Be "ghcr.io/altivy-it/dokpod-api@sha256:$('a' * 64)"
        $business.api | Should -Match 'dokpod-api-business@sha256:'
        $images.Values | ForEach-Object { $_ | Should -Match '@sha256:[0-9a-f]{64}$' }
    }

    It 'recusa com código 5: <Name>' -ForEach @(
        @{ Name = 'outra versão'; Version = '0.7.0'; Artifacts = $null }
        @{ Name = 'digest inválido'; Version = '0.8.0'; Artifacts = @(@{ kind = 'container'; name = 'dokpod-api'; reference = 'ghcr.io/x'; digest = 'latest' }) }
        @{ Name = 'referência já com digest'; Version = '0.8.0'; Artifacts = @(@{ kind = 'container'; name = 'dokpod-api'; reference = "ghcr.io/x@sha256:$('b' * 64)"; digest = "sha256:$('b' * 64)" }) }
    ) {
        $path = Write-Manifest -Version $Version -Artifacts ($Artifacts ?? $script:all)
        $failure = $null
        try { Read-ReleaseManifest -Path $path -Version '0.8.0' | Out-Null } catch { $failure = $_ }
        $failure.Exception.ExitCode | Should -Be 5
    }

    It 'falta de imagem da edição dá código 5' {
        $images = Read-ReleaseManifest -Path (Write-Manifest -Artifacts @((Image 'dokpod-api'), (Image 'dokpod-bff'))) -Version '0.8.0'
        $failure = $null
        try { Select-ReleaseImages -Images $images -Edition ce | Out-Null } catch { $failure = $_ }
        $failure.Exception.ExitCode | Should -Be 5
    }

    It 'as imagens de infraestrutura estão fixadas por digest' {
        $images = Get-InfrastructureImages
        $images.Keys | Sort-Object | Should -Be @('cosign', 'keycloak', 'nginx', 'postgresql')
        $images.Values | ForEach-Object { $_ | Should -Match '@sha256:[0-9a-f]{64}$' }
    }
}

Describe 'Assinatura' {
    It 'chama o cosign por digest com a identidade do publish.yml e sem log do engine' {
        $script:calls = [System.Collections.Generic.List[object]]::new()
        $run = { param([string[]] $arguments) $script:calls.Add($arguments); 0 }

        Confirm-ImageSignature -Image "ghcr.io/altivy-it/dokpod-api@sha256:$('c' * 64)" -Run $run

        $arguments = $script:calls[0]
        $arguments | Should -Contain '--log-driver'
        $arguments[$arguments.IndexOf('--log-driver') + 1] | Should -Be 'none'
        ($arguments | Where-Object { $_ -match '^ghcr.io/sigstore/cosign/cosign:.*@sha256:' }).Count | Should -Be 1
        $arguments[$arguments.IndexOf('--certificate-identity-regexp') + 1] | Should -Match 'altivy-it/dokpod/.*publish'
        $arguments[-1] | Should -Be "ghcr.io/altivy-it/dokpod-api@sha256:$('c' * 64)"
    }

    It 'assinatura inválida dá código 5' {
        $failure = $null
        try { Confirm-ImageSignature -Image "ghcr.io/altivy-it/dokpod-api@sha256:$('c' * 64)" -Run { param($arguments) 'no matching signatures'; 1 } } catch { $failure = $_ }
        $failure.Exception.ExitCode | Should -Be 5
    }

    It 'imagem sem digest nem chega ao cosign' {
        $script:called = $false
        $failure = $null
        try { Confirm-ImageSignature -Image 'ghcr.io/altivy-it/dokpod-api:0.8.0' -Run { param($arguments) $script:called = $true; 0 } } catch { $failure = $_ }
        $failure.Exception.ExitCode | Should -Be 5
        $script:called | Should -BeFalse
    }

    It 'modo offline verifica as assinaturas exportadas sem rede' {
        $script:calls = [System.Collections.Generic.List[object]]::new()
        Confirm-ImageSignature -Image "ghcr.io/altivy-it/dokpod-api@sha256:$('c' * 64)" -OfflineDirectory '/offline/dokpod-api' -Run { param([string[]] $arguments) $script:calls.Add($arguments); 0 }

        $arguments = $script:calls[0]
        $arguments[$arguments.IndexOf('--network') + 1] | Should -Be 'none'
        $arguments | Should -Contain '--offline'
        $arguments | Should -Contain '/offline/dokpod-api:/signatures:ro'
    }

    It 'pacote offline sem a imagem do manifesto dá código 5' {
        $directory = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-offline-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path (Join-Path $directory 'images') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $directory 'images/api.tar') -Value 'x'
        $run = { param([string[]] $arguments) if ($arguments[0] -eq 'load') { 0 } else { 'No such image'; 1 } }

        $failure = $null
        try { Import-OfflineImages -Directory $directory -Images "ghcr.io/altivy-it/dokpod-api@sha256:$('c' * 64)" -Run $run } catch { $failure = $_ }
        $failure.Exception.ExitCode | Should -Be 5
    }
}
