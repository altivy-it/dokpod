#!/usr/bin/env pwsh
# Gerenciado pelo Altivy.Groundwork (componente publish-containers). Não edite: mudanças vão para o catálogo.

<#
.SYNOPSIS
Constrói e publica uma imagem OCI uma única vez por versão e registra seu digest.

.DESCRIPTION
Se <registro>/<nome>:<versão> já existe, não reconstrói nem sobrescreve: registra o
digest publicado. Caso contrário, executa docker buildx build --push com labels OCI
de versão e revisão e registra o digest gerado. Emite repository, digest e pushed
na saída do GitHub Actions e grava a entrada do manifesto da release.
Requer login prévio no registro; não lê nem exibe credenciais.

.PARAMETER Tag
Tag da release (vX.Y.Z[-pre]).

.PARAMETER Name
Nome da imagem (minúsculas, dígitos, ponto, hífen ou sublinhado).

.PARAMETER Context
Contexto de build relativo à raiz do repositório.

.PARAMETER Dockerfile
Dockerfile relativo à raiz do repositório.

.PARAMETER Registry
Prefixo do registro. Padrão: ghcr.io/<owner em minúsculas>.

.PARAMETER EntriesPath
Arquivo JSON de saída com a entrada do manifesto.

.PARAMETER OutputPath
Arquivo de saída chave=valor (por exemplo $env:GITHUB_OUTPUT).

.PARAMETER DryRun
Mostra o comando de build sem executá-lo.

.EXAMPLE
./tools/scripts/ci/Publish-ContainerImage.ps1 -Tag v1.4.0 -Name api -Context . -Dockerfile deploy/api/Dockerfile -DryRun

.EXAMPLE
./tools/scripts/ci/Publish-ContainerImage.ps1 --help

.NOTES
Requer PowerShell 7+ e Docker com buildx. Código de saída 0 sucesso, 1 erro e 2 argumento desconhecido.

.LINK
../../../docs/runbooks/release.md
#>
#Requires -Version 7.0
[CmdletBinding(PositionalBinding = $false)]
param(
    [string] $Tag,
    [string] $Name,
    [string] $Context = '.',
    [string] $Dockerfile = 'Dockerfile',
    [string] $Registry = '',
    [string] $EntriesPath = '',
    [string] $OutputPath = '',
    [switch] $DryRun,
    [Alias('h')]
    [switch] $Help,
    [Parameter(ValueFromRemainingArguments)]
    [string[]] $RemainingArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Help -or ($RemainingArguments | Where-Object { $_ -in @('--help', '-h', '/?', 'help') })) {
    Get-Help -Full $PSCommandPath
    exit 0
}

if ($RemainingArguments) {
    [Console]::Error.WriteLine("Argumento desconhecido: $($RemainingArguments -join ' '). Use --help.")
    exit 2
}

Import-Module (Join-Path $PSScriptRoot 'GroundworkRelease.psm1') -Force

function Assert-RelativePath([string] $Path, [string] $Label) {
    $root = [System.IO.Path]::GetFullPath('.')
    $full = [System.IO.Path]::GetFullPath((Join-Path $root $Path))
    if ([System.IO.Path]::IsPathRooted($Path) -or ($full -ne $root -and -not $full.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::Ordinal))) {
        throw "$Label fora do repositório: $Path."
    }

    return $full
}

try {
    if (-not (Test-ReleaseTagName ($Tag ?? ''))) { throw "Tag inválida: '$Tag'." }
    if ($Name -notmatch '^[a-z0-9][a-z0-9._-]{0,127}$') { throw "Nome de imagem inválido: '$Name'." }
    if (-not $Registry) {
        if ($env:GITHUB_REPOSITORY_OWNER -notmatch '^[A-Za-z0-9-]+$') { throw 'Informe -Registry ou GITHUB_REPOSITORY_OWNER.' }
        $Registry = "ghcr.io/$($env:GITHUB_REPOSITORY_OWNER.ToLowerInvariant())"
    }

    if ($Registry -notmatch '^[a-z0-9.-]+(:\d+)?(/[a-z0-9._-]+)*$') { throw "Registro inválido: '$Registry'." }
    $contextPath = Assert-RelativePath $Context 'Contexto'
    $dockerfilePath = Assert-RelativePath $Dockerfile 'Dockerfile'
    if (-not (Test-Path -LiteralPath $dockerfilePath -PathType Leaf)) { throw "Dockerfile ausente: $Dockerfile." }

    $version = $Tag.Substring(1)
    $repository = "$Registry/$Name"
    $reference = "${repository}:$version"
    $digest = ''
    $pushed = $false

    $inspect = & docker buildx imagetools inspect $reference --format '{{json .Manifest}}' 2>$null
    if ($LASTEXITCODE -eq 0 -and $inspect) {
        $digest = [string](($inspect | Out-String | ConvertFrom-Json -AsHashtable)['digest'])
        Write-Output "$reference já publicada ($digest); não reconstruída."
    }
    elseif ($DryRun) {
        Write-Output "[dry-run] docker buildx build --push --tag $reference --file $Dockerfile $Context"
        exit 0
    }
    else {
        $commit = (git rev-parse HEAD | Out-String).Trim()
        $metadata = New-TemporaryFile
        try {
            & docker buildx build --push --provenance=false `
                --tag $reference `
                --file $dockerfilePath `
                --label "org.opencontainers.image.version=$version" `
                --label "org.opencontainers.image.revision=$commit" `
                --label "org.opencontainers.image.source=$($env:GITHUB_SERVER_URL)/$($env:GITHUB_REPOSITORY)" `
                --metadata-file $metadata.FullName `
                $contextPath
            if ($LASTEXITCODE -ne 0) { throw "Falha no build/push de $reference." }
            $digest = [string]((Get-Content -LiteralPath $metadata.FullName -Raw | ConvertFrom-Json -AsHashtable)['containerimage.digest'])
            $pushed = $true
        }
        finally {
            Remove-Item -LiteralPath $metadata.FullName -Force -ErrorAction SilentlyContinue
        }
    }

    if ($digest -notmatch '^sha256:[0-9a-f]{64}$') { throw "Digest não obtido para $reference." }
    if ($EntriesPath) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent ([System.IO.Path]::GetFullPath($EntriesPath))) | Out-Null
        ConvertTo-Json -InputObject @([ordered]@{ kind = 'container'; name = $Name; reference = $repository; digest = $digest }) -Depth 4 |
            Set-Content -LiteralPath $EntriesPath -Encoding utf8NoBOM
    }

    Write-GitHubOutput -Path $OutputPath -Values @{ repository = $repository; digest = $digest; pushed = $pushed.ToString().ToLowerInvariant() } | Out-Null
    Write-Output "$repository@$digest"
}
catch {
    Write-Output "::error title=Imagem OCI::$($_.Exception.Message)"
    exit 1
}

exit 0
