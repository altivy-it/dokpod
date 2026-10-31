<#
.SYNOPSIS
Inspeciona as imagens CE e Business da API: nenhum emissor, nenhuma chave privada e a edição certa (spec 031 T062, SC-008).

.DESCRIPTION
Para cada imagem, exporta o sistema de arquivos e lê o histórico de build, e então:

- falha se algum arquivo da aplicação (/app) contiver uma chave privada PEM;
- falha se houver emissor de licença (CLI dokpod-license-admin) em qualquer camada ou no histórico;
- consulta o próprio binário (`--describe-edition`, sem iniciar o host): a CE precisa ser `community`, sem trust store
  embarcado; a Business precisa ser `business`, sem nenhum `kid` das chaves sintéticas dos testes (`dokpod-test-*`).

Nada é publicado. O relatório fica em artifacts/licensing-image-contents/report.json.

.PARAMETER CeImage
Imagem CE a inspecionar. Padrão: dokpod-api:ce-inspect.

.PARAMETER BusinessImage
Imagem Business a inspecionar. Padrão: dokpod-api:business-inspect.

.PARAMETER Build
Constrói antes as duas imagens a partir de backend/apps/Dokpod.ControlPlane.Api/Dockerfile (alvo runtime); a Business
com `--build-arg DOKPOD_EDITION=Business`.

.PARAMETER Engine
docker ou podman. Padrão: docker quando disponível, senão podman.

.PARAMETER ArtifactsPath
Pasta do relatório. Padrão: artifacts/licensing-image-contents na raiz do repositório.

.PARAMETER DryRun
Mostra o plano sem executar nada.

.PARAMETER RemainingArguments
Aceita somente --help.

.EXAMPLE
./tools/scripts/test-licensing-image-contents.ps1 --help

.EXAMPLE
./tools/scripts/test-licensing-image-contents.ps1 -Build

.NOTES
Pré-requisitos: PowerShell 7.4+, Docker ou Podman e o comando tar.

.LINK
specs/031-licenciamento-business-offline/quickstart.md
#>
#requires -Version 7.4
[CmdletBinding(PositionalBinding = $false)]
param(
    [string] $CeImage = 'dokpod-api:ce-inspect',
    [string] $BusinessImage = 'dokpod-api:business-inspect',
    [switch] $Build,
    [ValidateSet('', 'docker', 'podman')]
    [string] $Engine = '',
    [string] $ArtifactsPath = '',
    [switch] $DryRun,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $RemainingArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RemainingArguments = @($RemainingArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($RemainingArguments -contains '--help') { Get-Help $PSCommandPath -Full; exit 0 }
if ($RemainingArguments.Count -gt 0) {
    [Console]::Error.WriteLine("Argumento desconhecido: $($RemainingArguments -join ' '). Use --help.")
    exit 2
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $ArtifactsPath) { $ArtifactsPath = Join-Path $repositoryRoot 'artifacts/licensing-image-contents' }
if (-not $Engine) { $Engine = if (Get-Command docker -ErrorAction SilentlyContinue) { 'docker' } else { 'podman' } }
$dockerfile = Join-Path $repositoryRoot 'backend/apps/Dokpod.ControlPlane.Api/Dockerfile'
$targets = @(
    @{ name = 'ce'; image = $CeImage; edition = 'community'; buildArgs = @() }
    @{ name = 'business'; image = $BusinessImage; edition = 'business'; buildArgs = @('--build-arg', 'DOKPOD_EDITION=Business') }
)

if ($DryRun) {
    [pscustomobject]@{
        engine = $Engine
        build = [bool]$Build
        images = @($targets | ForEach-Object { "$($_.name): $($_.image)" })
        checks = @('sem chave privada em /app', 'sem emissor nas camadas e no histórico', 'edição e trust store pelo próprio binário')
        artifacts = $ArtifactsPath
    } | ConvertTo-Json -Depth 3
    exit 0
}

function Invoke-Engine {
    param([Parameter(Mandatory)][string[]] $Arguments)
    $output = & $Engine @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "$Engine $($Arguments[0]) falhou (código $LASTEXITCODE): $(($output | Select-Object -Last 5) -join ' ')" }
    return $output
}

$privateKeyPattern = '-----BEGIN ([A-Z ]+ )?PRIVATE KEY-----'
$emitterPattern = 'dokpod-license-admin|Dokpod\.LicenseAdmin'
$work = Join-Path ([IO.Path]::GetTempPath()) "dokpod-image-contents-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$results = @()
$exitCode = 0

try {
    New-Item -ItemType Directory -Force -Path $ArtifactsPath, $work | Out-Null
    foreach ($target in $targets) {
        if ($Build) {
            Write-Output "Construindo $($target.image)..."
            Invoke-Engine (@('build', '--file', $dockerfile, '--target', 'runtime', '--tag', $target.image) + $target.buildArgs + @($repositoryRoot)) | Out-Null
        }

        $root = Join-Path $work $target.name
        New-Item -ItemType Directory -Force -Path $root | Out-Null
        $container = ((Invoke-Engine @('create', $target.image)) | Select-Object -Last 1).Trim()
        try {
            Invoke-Engine @('export', '--output', (Join-Path $work "$($target.name).tar"), $container) | Out-Null
        }
        finally {
            & $Engine rm -f $container *> $null
        }

        & tar -xf (Join-Path $work "$($target.name).tar") -C $root 2>$null
        $appFiles = @(Get-ChildItem -Path (Join-Path $root 'app') -Recurse -File -ErrorAction SilentlyContinue)
        $privateKeys = @($appFiles | Where-Object { Select-String -Path $_.FullName -Pattern $privateKeyPattern -Quiet -ErrorAction SilentlyContinue } |
            ForEach-Object { $_.FullName.Substring($root.Length) })
        $emitterFiles = @(Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match $emitterPattern } |
            ForEach-Object { $_.FullName.Substring($root.Length) })
        $history = (Invoke-Engine @('history', '--no-trunc', '--format', '{{.CreatedBy}}', $target.image)) -join "`n"
        $historyFindings = @(
            if ($history -match $privateKeyPattern) { 'chave privada no histórico' }
            if ($history -match $emitterPattern) { 'emissor no histórico' }
        )

        $described = (Invoke-Engine @('run', '--rm', '--network', 'none', '--entrypoint', 'dotnet', $target.image, 'Dokpod.ControlPlane.Api.dll', '--describe-edition')) |
            Where-Object { "$_".TrimStart().StartsWith('{') } | Select-Object -Last 1 | ConvertFrom-Json
        $keyIds = @($described.trustStoreKeyIds)
        $editionFindings = @(
            if ($described.edition -ne $target.edition) { "edição $($described.edition), esperada $($target.edition)" }
            if ($target.name -eq 'ce' -and $keyIds.Count -gt 0) { 'a CE embarca trust store' }
            if ($keyIds | Where-Object { $_ -like 'dokpod-test*' }) { "kid de teste no trust store: $($keyIds -join ', ')" }
        )

        $failed = $privateKeys.Count + $emitterFiles.Count + $historyFindings.Count + $editionFindings.Count -gt 0
        if ($failed) { $exitCode = 1 }
        $results += [ordered]@{
            image = $target.image
            edition = $described.edition
            trustStoreKeyIds = $keyIds
            verifierLibraryPresent = Test-Path (Join-Path $root 'app/Dokpod.Licensing.dll')
            privateKeyFiles = $privateKeys
            emitterFiles = $emitterFiles
            historyFindings = $historyFindings
            editionFindings = $editionFindings
            result = if ($failed) { 'fail' } else { 'pass' }
        }
        Write-Output "$($target.name): $(if ($failed) { 'fail' } else { 'pass' }) (edição $($described.edition), kids: $(if ($keyIds.Count) { $keyIds -join ', ' } else { 'nenhum' }))"
    }
}
catch {
    [Console]::Error.WriteLine("Falha: $($_.Exception.Message)")
    $exitCode = 1
}
finally {
    $results | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $ArtifactsPath 'report.json') -Encoding utf8
    Remove-Item -Recurse -Force -Path $work -ErrorAction SilentlyContinue
}

Write-Output "Relatório: $(Join-Path $ArtifactsPath 'report.json')"
exit $exitCode
