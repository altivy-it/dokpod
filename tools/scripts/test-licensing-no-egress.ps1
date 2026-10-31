<#
.SYNOPSIS
Prova que a API do Dokpod não tenta comunicação externa durante início, operação e reinício (spec 031, D-012).

.DESCRIPTION
Sobe a imagem da API numa rede de containers interna (sem rota para fora), só com PostgreSQL e Keycloak,
e captura com tcpdump todo o tráfego do namespace de rede da API desde antes do início do processo. Depois:

- falha se houver qualquer pacote IPv4/IPv6 para destino fora da rede interna e do loopback;
- falha se houver consulta DNS para nome que não seja um dos serviços internos;
- envia um canário no corpo de uma importação de licença e falha se ele aparecer nos logs da API;
- registra que a exportação OTLP de métricas e traces está desligada (nenhum endpoint configurado).

A rede interna impede a conexão, mas a captura registra a tentativa; o teste exige zero tentativas (SC-002).
Certificado TLS, senha do PostgreSQL e do Keycloak são gerados na hora, ficam fora do repositório e são
apagados no fim; nenhum valor é exibido. O relatório fica em artifacts/licensing-no-egress/report.json.

.PARAMETER ApiImage
Imagem da API a testar. Padrão: dokpod-api:no-egress.

.PARAMETER Build
Constrói antes a imagem da API (CE) a partir de backend/apps/Dokpod.ControlPlane.Api/Dockerfile, alvo runtime.

.PARAMETER Engine
docker ou podman. Padrão: docker quando disponível, senão podman.

.PARAMETER ArtifactsPath
Pasta do relatório e da captura. Padrão: artifacts/licensing-no-egress na raiz do repositório.

.PARAMETER KeepStack
Não remove containers e rede no fim (diagnóstico). Os arquivos temporários de certificado são apagados mesmo assim.

.PARAMETER DryRun
Mostra o plano sem executar nada.

.PARAMETER RemainingArguments
Aceita somente --help.

.EXAMPLE
./tools/scripts/test-licensing-no-egress.ps1 --help

.EXAMPLE
./tools/scripts/test-licensing-no-egress.ps1 -Build

.EXAMPLE
./tools/scripts/test-licensing-no-egress.ps1 -ApiImage ghcr.io/altivy-it/dokpod-api:0.2.0 -Engine podman

.NOTES
Pré-requisitos: PowerShell 7.4+, Docker ou Podman com suporte a redes internas e a --network container:<nome>.
Imagens fixadas: postgres:17.6-bookworm, lzocateli/keycloak:26.7.0 e nicolaka/netshoot:v0.14 (Apache-2.0, só
para a captura; nunca distribuída). Sem rede externa, as imagens precisam estar disponíveis localmente ou no registro.

.LINK
specs/031-licenciamento-business-offline/research.md
#>
#requires -Version 7.4
[CmdletBinding(PositionalBinding = $false)]
param(
    [string] $ApiImage = 'dokpod-api:no-egress',
    [switch] $Build,
    [ValidateSet('', 'docker', 'podman')]
    [string] $Engine = '',
    [string] $ArtifactsPath = '',
    [switch] $KeepStack,
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
if (-not $ArtifactsPath) { $ArtifactsPath = Join-Path $repositoryRoot 'artifacts/licensing-no-egress' }
if (-not $Engine) { $Engine = if (Get-Command docker -ErrorAction SilentlyContinue) { 'docker' } else { 'podman' } }

$images = [ordered]@{
    postgres = 'docker.io/library/postgres:17.6-bookworm@sha256:45cd22f8d32e189d245403954882f88e7a8714301fda80dab6da90f1265b25a3'
    keycloak = 'docker.io/lzocateli/keycloak:26.7.0'
    capture  = 'docker.io/nicolaka/netshoot:v0.14@sha256:34eeca872db74067b1ed7fdc6201f278578bf57df7bd3081e99b5097a28464b5'
}
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$subnetOctet = Get-Random -Minimum 20 -Maximum 240
$subnet = "172.30.$subnetOctet.0/24"
$names = @{
    network  = "dokpod-no-egress-$suffix"
    holder   = "dokpod-no-egress-net-$suffix"
    api      = "dokpod-no-egress-api-$suffix"
    postgres = "dokpod-no-egress-pg-$suffix"
    keycloak = "dokpod-no-egress-kc-$suffix"
}
$internalNames = @('postgres', 'keycloak', 'dokpod-api', 'localhost')

if ($DryRun) {
    [pscustomobject]@{
        engine = $Engine
        apiImage = $ApiImage
        build = [bool]$Build
        network = "$($names.network) (--internal, $subnet)"
        services = @('postgres', 'keycloak', 'api (namespace de rede do container de captura)')
        phases = @('start', 'operation', 'restart')
        artifacts = $ArtifactsPath
    } | ConvertTo-Json -Depth 3
    exit 0
}

function Invoke-Engine {
    param([Parameter(Mandatory)][string[]] $Arguments, [switch] $AllowFailure)
    $output = & $Engine @Arguments 2>&1
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "$Engine $($Arguments[0]) falhou (código $LASTEXITCODE): $(($output | Select-Object -Last 5) -join ' ')"
    }
    return $output
}

function Wait-ApiLive {
    param([int] $TimeoutSeconds = 120)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        & $Engine exec $names.api curl -fsS -o /dev/null http://127.0.0.1:8080/health/live 2>$null
        if ($LASTEXITCODE -eq 0) { return }
        Start-Sleep -Seconds 2
    }
    throw "A API não ficou viva em $TimeoutSeconds s."
}

function Invoke-ApiRequest {
    param([Parameter(Mandatory)][string] $Method, [Parameter(Mandatory)][string] $Url, [string] $Body)
    $arguments = @('exec', $names.api, 'curl', '-sk', '-o', '/dev/null', '-w', '%{http_code}', '-X', $Method, $Url)
    if ($Body) { $arguments += @('-H', 'Content-Type: application/jose', '--data-binary', $Body) }
    $status = (& $Engine @arguments 2>$null) -join ''
    return [pscustomobject]@{ method = $Method; url = $Url; status = $status }
}

function New-EphemeralCertificate {
    param([Parameter(Mandatory)][string] $Directory, [Parameter(Mandatory)][string] $Password)
    $key = [System.Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            'CN=dokpod-api', $key, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        $san.AddDnsName('dokpod-api'); $san.AddDnsName('localhost'); $san.AddIpAddress([System.Net.IPAddress]::Loopback)
        $request.CertificateExtensions.Add($san.Build())
        $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true))
        $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(1))
        try {
            [IO.File]::WriteAllBytes((Join-Path $Directory 'api.pfx'), $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password))
            [IO.File]::WriteAllText((Join-Path $Directory 'ca.crt'), $certificate.ExportCertificatePem())
        }
        finally { $certificate.Dispose() }
    }
    finally { $key.Dispose() }
}

function New-Secret { [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)).ToLowerInvariant() }

$temporary = Join-Path ([IO.Path]::GetTempPath()) "dokpod-no-egress-$suffix"
$report = [ordered]@{
    startedAt = [DateTimeOffset]::UtcNow.ToString('o')
    engine = $Engine
    apiImage = $ApiImage
    subnet = $subnet
    otlpExport = 'disabled (OTEL_EXPORTER_OTLP_*_ENDPOINT vazios)'
    requests = @()
    externalPackets = @()
    unexpectedDnsQueries = @()
    canaryInLogs = $false
    result = 'fail'
}
$exitCode = 1

try {
    New-Item -ItemType Directory -Force -Path $ArtifactsPath | Out-Null
    New-Item -ItemType Directory -Force -Path $temporary | Out-Null
    if (-not $IsWindows) { & chmod 755 $temporary }

    if ($Build) {
        Write-Output "Construindo $ApiImage (CE)..."
        Invoke-Engine @('build', '--file', (Join-Path $repositoryRoot 'backend/apps/Dokpod.ControlPlane.Api/Dockerfile'),
            '--target', 'runtime', '--tag', $ApiImage, $repositoryRoot) | Out-Null
    }

    # Valores aleatórios desta execução, repassados aos containers por herança de ambiente (nunca na linha de comando).
    $pgCredential = New-Secret
    $kcCredential = New-Secret
    $certificateCredential = New-Secret
    $env:DOKPOD_NO_EGRESS_PG_PASSWORD = $pgCredential
    $env:DOKPOD_NO_EGRESS_KC_PASSWORD = $kcCredential
    $env:DOKPOD_API_CERTIFICATE_PASSWORD = $certificateCredential
    $env:POSTGRES_PASSWORD = $pgCredential
    $env:KC_BOOTSTRAP_ADMIN_PASSWORD = $kcCredential
    $env:ConnectionStrings__ControlPlane = "Host=postgres;Port=5432;Database=dokpod;Username=dokpod;Password=$pgCredential;Search Path=dokpod"
    New-EphemeralCertificate -Directory $temporary -Password $env:DOKPOD_API_CERTIFICATE_PASSWORD
    if (-not $IsWindows) { & chmod 644 (Join-Path $temporary 'api.pfx') (Join-Path $temporary 'ca.crt') }

    Invoke-Engine @('network', 'create', '--internal', '--subnet', $subnet, $names.network) | Out-Null
    Invoke-Engine @('run', '-d', '--name', $names.postgres, '--network', $names.network, '--network-alias', 'postgres',
        '-e', 'POSTGRES_USER=dokpod', '-e', 'POSTGRES_DB=dokpod', '-e', 'POSTGRES_PASSWORD', $images.postgres) | Out-Null
    Invoke-Engine @('run', '-d', '--name', $names.keycloak, '--network', $names.network, '--network-alias', 'keycloak',
        '-e', 'KC_BOOTSTRAP_ADMIN_USERNAME=admin', '-e', 'KC_BOOTSTRAP_ADMIN_PASSWORD', $images.keycloak, 'start-dev') | Out-Null

    # O container de captura é dono do namespace de rede; a API entra nele, então a captura cobre o início do processo.
    Invoke-Engine @('run', '-d', '--name', $names.holder, '--network', $names.network, '--network-alias', 'dokpod-api',
        '--cap-add', 'NET_RAW', '--cap-add', 'NET_ADMIN', '--entrypoint', 'sleep', $images.capture, 'infinity') | Out-Null
    # Sem rota padrão, uma conexão a IP externo falharia no kernel sem gerar pacote e escaparia da captura. Uma rota
    # para um vizinho fictício (MAC fixo, sem dono) faz toda tentativa sair como pacote visível, sem chegar a lugar nenhum.
    $blackhole = "172.30.$subnetOctet.254"
    $interface = ((Invoke-Engine @('exec', $names.holder, 'sh', '-c', "ip -o route get 172.30.$subnetOctet.1 | sed -n 's/.* dev \([^ ]*\).*/\1/p'")) -join '').Trim()
    Invoke-Engine @('exec', $names.holder, 'ip', 'neigh', 'replace', $blackhole, 'lladdr', '02:00:00:00:00:fe', 'dev', $interface, 'nud', 'permanent') | Out-Null
    Invoke-Engine @('exec', $names.holder, 'ip', 'route', 'replace', 'default', 'via', $blackhole, 'dev', $interface) | Out-Null
    $report.captureRoute = "default via $blackhole dev $interface"
    Invoke-Engine @('exec', '-d', $names.holder, 'tcpdump', '-i', 'any', '-nn', '-U', '-w', '/tmp/api.pcap') | Out-Null
    Start-Sleep -Seconds 2

    Write-Output 'Fase start: subindo a API...'
    Invoke-Engine @('run', '-d', '--name', $names.api, '--network', "container:$($names.holder)",
        '-e', 'ASPNETCORE_ENVIRONMENT=Development', '-e', 'DOKPOD_API_GRPC_PORT=7443', '-e', 'DOKPOD_API_HEALTH_PORT=8080',
        '-e', 'DOKPOD_API_CERTIFICATE_PATH=/certs/api.pfx', '-e', 'DOKPOD_API_CERTIFICATE_PASSWORD',
        '-e', 'DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH=/certs/ca.crt',
        '-e', 'Authentication__Keycloak__Authority=http://keycloak:8080/realms/dokpod',
        '-e', 'Authentication__Keycloak__BackchannelAuthority=http://keycloak:8080/realms/dokpod',
        '-e', 'Authentication__Keycloak__Audience=dokpod-api',
        '-e', 'ConnectionStrings__ControlPlane',
        '-e', 'OTEL_EXPORTER_OTLP_METRICS_ENDPOINT=', '-e', 'OTEL_EXPORTER_OTLP_TRACES_ENDPOINT=',
        '-v', "$($temporary):/certs:ro", $ApiImage) | Out-Null
    Wait-ApiLive

    Write-Output 'Fase operation: health, licenciamento e canário...'
    $canary = "dokpod-canary-$([Guid]::NewGuid().ToString('N'))"
    $requests = @(
        Invoke-ApiRequest GET 'http://127.0.0.1:8080/health/ready'
        Invoke-ApiRequest GET 'http://127.0.0.1:8080/health/ready/database'
        Invoke-ApiRequest GET 'http://127.0.0.1:8080/health/ready/keycloak'
        Invoke-ApiRequest GET 'http://127.0.0.1:8080/api/v1/licensing/status'
        Invoke-ApiRequest PUT 'http://127.0.0.1:8080/api/v1/licensing/license?slotId=prd' "$canary.$canary.$canary"
        Invoke-ApiRequest GET 'http://127.0.0.1:8080/api/v1/settings/installation'
    )
    Start-Sleep -Seconds 5

    Write-Output 'Fase restart: reiniciando a API...'
    Invoke-Engine @('restart', $names.api) | Out-Null
    Wait-ApiLive
    $requests += Invoke-ApiRequest GET 'http://127.0.0.1:8080/health/ready'
    Start-Sleep -Seconds 5
    $report.requests = $requests

    Invoke-Engine @('exec', $names.holder, 'pkill', '-INT', 'tcpdump') -AllowFailure | Out-Null
    Start-Sleep -Seconds 2
    Invoke-Engine @('cp', "$($names.holder):/tmp/api.pcap", (Join-Path $ArtifactsPath 'api.pcap')) | Out-Null

    $externalIpFilter = "(ip and not (dst net $subnet or dst net 127.0.0.0/8 or dst net 224.0.0.0/4 or dst host 255.255.255.255 or dst host 0.0.0.0)) or (ip6 and not (dst host ::1 or dst net fe80::/10 or dst net ff00::/8))"
    $report.externalPackets = @(Invoke-Engine @('exec', $names.holder, 'tcpdump', '-r', '/tmp/api.pcap', '-nn', $externalIpFilter) -AllowFailure |
        Where-Object { $_ -notmatch '^reading from file|^tcpdump:|^Warning:' } | Select-Object -First 50 | ForEach-Object { "$_" })

    $reverseSubnet = "$subnetOctet.30.172.in-addr.arpa"
    $dnsLines = Invoke-Engine @('exec', $names.holder, 'tcpdump', '-r', '/tmp/api.pcap', '-nn', 'port 53') -AllowFailure
    $report.unexpectedDnsQueries = @($dnsLines | ForEach-Object {
            if ("$_" -match '\s(?:A|AAAA|PTR|SRV|TXT|CNAME|HTTPS|SVCB|MX|NS|ANY)\?\s+(?<name>\S+?)\.?\s') { $Matches.name }
        } | Where-Object {
            $first = ($_ -split '\.')[0].ToLowerInvariant()
            -not ($internalNames -contains $first -or $_ -like "*$reverseSubnet" -or $_ -like '*.127.in-addr.arpa')
        } | Sort-Object -Unique)

    $logs = (Invoke-Engine @('logs', $names.api) -AllowFailure) -join "`n"
    $report.canaryInLogs = $logs.Contains($canary, [StringComparison]::Ordinal)
    $apiStarted = @($requests | Where-Object { $_.url -like '*/health/ready' -and $_.status -match '^\d{3}$' }).Count -ge 2

    $passed = $apiStarted -and $report.externalPackets.Count -eq 0 -and $report.unexpectedDnsQueries.Count -eq 0 -and -not $report.canaryInLogs
    $report.result = if ($passed) { 'pass' } else { 'fail' }
    $exitCode = if ($passed) { 0 } else { 1 }
}
catch {
    $report.error = $_.Exception.Message
    [Console]::Error.WriteLine("Falha: $($_.Exception.Message)")
}
finally {
    $report.finishedAt = [DateTimeOffset]::UtcNow.ToString('o')
    if (Test-Path $ArtifactsPath) {
        $report | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $ArtifactsPath 'report.json') -Encoding utf8
    }
    if (-not $KeepStack) {
        foreach ($name in @($names.api, $names.holder, $names.keycloak, $names.postgres)) {
            & $Engine rm -f $name *> $null
        }
        & $Engine network rm $names.network *> $null
    }
    Remove-Item -Recurse -Force -Path $temporary -ErrorAction SilentlyContinue
    foreach ($variable in @('DOKPOD_NO_EGRESS_PG_PASSWORD', 'DOKPOD_NO_EGRESS_KC_PASSWORD', 'DOKPOD_API_CERTIFICATE_PASSWORD',
            'POSTGRES_PASSWORD', 'KC_BOOTSTRAP_ADMIN_PASSWORD', 'ConnectionStrings__ControlPlane')) {
        Remove-Item -Path "env:$variable" -ErrorAction SilentlyContinue
    }
}

Write-Output "Resultado: $($report.result) (pacotes externos: $(@($report.externalPackets).Count), DNS inesperado: $(@($report.unexpectedDnsQueries).Count), canário nos logs: $($report.canaryInLogs))."
Write-Output "Relatório: $(Join-Path $ArtifactsPath 'report.json')"
exit $exitCode
