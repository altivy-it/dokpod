#Requires -Version 7.4
<#
.SYNOPSIS
Imagens da instalação (spec 033 T025, FR-005, FR-006, D-010): só referência@digest. As do Dokpod vêm do
release-manifest.json e passam por cosign verify antes de qualquer uso; as de infraestrutura vêm fixadas por digest em
templates/infrastructure-images.json. Assinatura ou manifesto inválido dá código 5 sem efeito.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')

# Identidade do workflow de publicação (docs/runbooks/licenciamento-imagem-business.md, docs/release-integrity.md).
$script:CertificateIdentity = '^https://github\.com/altivy-it/dokpod/\.github/workflows/publish\.yml@refs/(heads/main|tags/v[0-9]+\.[0-9]+\.[0-9]+.*)$'
$script:OidcIssuer = 'https://token.actions.githubusercontent.com'
$script:DigestPattern = '^sha256:[0-9a-f]{64}$'

function Read-ReleaseManifest {
    <#
    .SYNOPSIS
    Lê o release-manifest.json da versão e devolve as imagens do Dokpod como nome -> referência@digest.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Version
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Stop-Installer -Code Signature -Message "Manifesto da release ausente: $Path."
    }

    try {
        $manifest = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    }
    catch {
        Stop-Installer -Code Signature -Message 'O manifesto da release não é JSON válido.'
    }

    if ($manifest.version -ne $Version) {
        Stop-Installer -Code Signature -Message "O manifesto é da versão $($manifest.version), não de $Version."
    }

    $images = [ordered]@{}
    foreach ($artifact in @($manifest.artifacts)) {
        if ($artifact.kind -ne 'container') { continue }
        if ($artifact.digest -notmatch $script:DigestPattern -or $artifact.reference -match '@') {
            Stop-Installer -Code Signature -Message "Imagem $($artifact.name) sem digest válido no manifesto."
        }

        $images[$artifact.name] = "$($artifact.reference)@$($artifact.digest)"
    }

    return $images
}

function Select-ReleaseImages {
    <#
    .SYNOPSIS
    Imagens que a edição usa; falta de alguma dá código 5.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Images,
        [Parameter(Mandatory)][ValidateSet('ce', 'business')][string] $Edition
    )

    $api = if ($Edition -eq 'business') { 'dokpod-api-business' } else { 'dokpod-api' }
    $selected = [ordered]@{}
    foreach ($pair in @(@('api', $api), @('bff', 'dokpod-bff'), @('web', 'dokpod-web'), @('agent', 'dokpod-agent'))) {
        if (-not $Images.Contains($pair[1])) {
            Stop-Installer -Code Signature -Message "O manifesto da release não traz a imagem $($pair[1])."
        }

        $selected[$pair[0]] = $Images[$pair[1]]
    }

    return $selected
}

function Get-InfrastructureImages {
    [CmdletBinding()]
    param()

    $images = (Get-Content -LiteralPath (Join-Path (Get-InstallerHome) 'templates/infrastructure-images.json') -Raw | ConvertFrom-Json -AsHashtable).images
    foreach ($name in $images.Keys) {
        if ($images[$name] -notmatch '@sha256:[0-9a-f]{64}$') {
            Stop-Installer -Code Signature -Message "A imagem de infraestrutura $name não está fixada por digest."
        }
    }

    return $images
}

function Confirm-ImageSignature {
    <#
    .SYNOPSIS
    cosign verify (em container, por digest) de uma imagem do Dokpod, antes de qualquer pull ou uso.
    .PARAMETER OfflineDirectory
    Pasta com as assinaturas exportadas (cosign save) da imagem, para hosts sem internet.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Image,
        [string] $OfflineDirectory,
        [scriptblock] $Run = { param([string[]] $arguments) & docker @arguments 2>&1; $LASTEXITCODE }
    )

    if ($Image -notmatch '@sha256:[0-9a-f]{64}$') {
        Stop-Installer -Code Signature -Message "A imagem $Image não está fixada por digest."
    }

    $cosign = (Get-InfrastructureImages).cosign
    $arguments = @('run', '--rm', '--log-driver', 'none', '--network', $(if ($OfflineDirectory) { 'none' } else { 'bridge' }))
    if ($OfflineDirectory) {
        $arguments += @('--volume', "${OfflineDirectory}:/signatures:ro", $cosign, 'verify', '--offline', '--local-image', '/signatures')
    }
    else {
        $arguments += @($cosign, 'verify')
    }

    $arguments += @('--certificate-identity-regexp', $script:CertificateIdentity, '--certificate-oidc-issuer', $script:OidcIssuer)
    if (-not $OfflineDirectory) { $arguments += $Image }
    $output = @(& $Run $arguments)
    $exitCode = $output[-1]
    if ($exitCode -ne 0) {
        Stop-Installer -Code Signature -Message "A assinatura de $Image não confere com a identidade do publish.yml do Dokpod; nada foi alterado."
    }
}

function Import-OfflineImages {
    <#
    .SYNOPSIS
    Carrega as imagens exportadas (docker save) de um diretório offline e confere que cada referência do manifesto chegou.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Directory,
        [Parameter(Mandatory)][string[]] $Images,
        [scriptblock] $Run = { param([string[]] $arguments) & docker @arguments 2>&1; $LASTEXITCODE }
    )

    $archives = @(Get-ChildItem -LiteralPath (Join-Path $Directory 'images') -Filter '*.tar' -ErrorAction SilentlyContinue)
    if ($archives.Count -eq 0) {
        Stop-Installer -Code Signature -Message "Nenhuma imagem exportada em $Directory/images."
    }

    foreach ($archive in $archives) {
        $output = @(& $Run @('load', '--input', $archive.FullName))
        if ($output[-1] -ne 0) {
            Stop-Installer -Code Signature -Message "Falha ao carregar $($archive.Name)."
        }
    }

    foreach ($image in $Images) {
        $output = @(& $Run @('image', 'inspect', '--format', '{{.Id}}', $image))
        if ($output[-1] -ne 0) {
            Stop-Installer -Code Signature -Message "A imagem $image do manifesto não está no pacote offline."
        }
    }
}

Export-ModuleMember -Function Read-ReleaseManifest, Select-ReleaseImages, Get-InfrastructureImages, Confirm-ImageSignature, Import-OfflineImages
