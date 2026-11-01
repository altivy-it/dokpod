<#
.SYNOPSIS
Valida a exportação da auditoria para SIEM contra coletores reais (spec 027 T033, SC-001).
.DESCRIPTION
Sobe numa rede de containers interna própria a imagem Business da API, um PostgreSQL e dois coletores:
- rsyslog com TLS (imtcp + driver ossl, porta 6514), gravando em arquivo o horário de chegada e a mensagem;
- otel-collector com receptor OTLP/HTTP com TLS e token Bearer (porta 4318), que marca o horário de chegada em
  observedTime e grava os logs em arquivo.
A CA e os certificados dos coletores, o token, a senha do PostgreSQL e o certificado da API são gerados na execução,
ficam numa pasta temporária fora do repositório e são apagados no fim; nenhum valor é exibido.

Executa os cenários 1 (syslog) e 3 (OTLP) do quickstart da spec 027:
1. cria um destino de cada protocolo com as categorias audit_export e containers;
2. grava 50 ações de container na trilha (função dokpod.dokpod_append_audit_event, a mesma das ações da API);
3. confere que cada eventId chega uma vez a cada coletor, com os mesmos campos da trilha;
4. mede o tempo entre occurredAt e a chegada no coletor e falha se o p95 passar de 10 s (SC-001);
5. desmarca containers nos dois destinos, grava 5 ações e confere que não chegam e continuam na trilha.

Desvio registrado na T033: a stack E2E deste repositório é CE e depende da plataforma de identidade externa; a imagem
Business só aceita licença assinada pela chave do cofre. Como o envio não consulta a licença (FR-008), o script usa a
imagem Business sem licença e grava os destinos direto no PostgreSQL, como faria a API (cursor no fim da trilha). A
criação pela API, a autorização e a tela já são cobertas pelos testes de integração e pelo E2E simulado.
O relatório fica em artifacts/audit-export-collectors/report.json.
.PARAMETER ApiImage
Imagem Business da API. Padrão: localhost/dokpod-api-business:collectors.
.PARAMETER Build
Constrói antes a imagem a partir de backend/apps/Dokpod.ControlPlane.Api/Business.Dockerfile, alvo runtime.
.PARAMETER Events
Ações de container gravadas por cenário. Padrão: 50.
.PARAMETER Engine
docker ou podman. Padrão: docker quando disponível, senão podman.
.PARAMETER ArtifactsPath
Pasta do relatório. Padrão: artifacts/audit-export-collectors na raiz do repositório.
.PARAMETER KeepStack
Não remove containers e rede no fim (diagnóstico). A pasta temporária de certificados é apagada mesmo assim.
.PARAMETER DryRun
Mostra o plano sem executar nada.
.PARAMETER RemainingArguments
Aceita somente --help.
.EXAMPLE
./tools/scripts/test-audit-export-collectors.ps1 --help
.EXAMPLE
./tools/scripts/test-audit-export-collectors.ps1 -Build
.EXAMPLE
./tools/scripts/test-audit-export-collectors.ps1 -DryRun -Engine podman
.NOTES
Pré-requisitos: PowerShell 7.4+, Docker ou Podman com redes internas, host x86_64. As migrations são geradas como SQL (banco vazio, da primeira à última)
num container do SDK (precisa do cache NuGet ou de rede) e aplicadas com psql.
Imagens fixadas, só para teste e nunca distribuídas:
- postgres:17.6-bookworm (PostgreSQL License);
- rsyslog/rsyslog-collector:2026-10 (rótulo Apache-2.0; rsyslog 8.2610.0, GPL-3.0/Apache-2.0);
- otel/opentelemetry-collector-contrib:0.139.0 (Apache-2.0);
- lzocateli/dotnet-sdk:10.0.402-noble-v1 (geração do SQL das migrations).
.LINK
specs/027-exportacao-auditoria-siem/quickstart.md
#>
#requires -Version 7.4
[CmdletBinding(PositionalBinding = $false)]
param(
    [string] $ApiImage = 'localhost/dokpod-api-business:collectors',
    [switch] $Build,
    [ValidateRange(1, 1000)]
    [int] $Events = 50,
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
if (-not $ArtifactsPath) { $ArtifactsPath = Join-Path $repositoryRoot 'artifacts/audit-export-collectors' }
if (-not $Engine) { $Engine = if (Get-Command docker -ErrorAction SilentlyContinue) { 'docker' } else { 'podman' } }

$images = [ordered]@{
    postgres = 'docker.io/library/postgres:17.6-bookworm@sha256:45cd22f8d32e189d245403954882f88e7a8714301fda80dab6da90f1265b25a3'
    rsyslog  = 'docker.io/rsyslog/rsyslog-collector:2026-10@sha256:d94de0a13a02bd243423808bdc85ba256814ee0edaace4b0fb1498ca70966341'
    otel     = 'docker.io/otel/opentelemetry-collector-contrib:0.139.0@sha256:87b591b18a0a330b6246f6d4f53d4867d1b238d1b85a69145e0f4eb38f92154c'
    sdk      = 'docker.io/lzocateli/dotnet-sdk:10.0.402-noble-v1'
}
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$subnet = "172.31.$(Get-Random -Minimum 20 -Maximum 240).0/24"
$names = @{
    network  = "dokpod-collectors-$suffix"
    api      = "dokpod-collectors-api-$suffix"
    postgres = "dokpod-collectors-pg-$suffix"
    rsyslog  = "dokpod-collectors-rsyslog-$suffix"
    otel     = "dokpod-collectors-otel-$suffix"
}
$latencyLimitSeconds = 10.0

if ($DryRun) {
    [pscustomobject]@{
        engine = $Engine
        apiImage = $ApiImage
        build = [bool]$Build
        network = "$($names.network) (--internal, $subnet)"
        images = $images
        scenarios = @("1 (syslog) e 3 (OTLP): $Events ações de container, p95 <= $latencyLimitSeconds s", 'categoria containers desmarcada: 5 ações não chegam')
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

function Invoke-Sql {
    param([Parameter(Mandatory)][string] $Sql)
    $output = $Sql | & $Engine exec -i -e PGTZ=UTC $names.postgres psql -v ON_ERROR_STOP=1 -X -q -t -A -U dokpod -d dokpod 2>&1
    if ($LASTEXITCODE -ne 0) { throw "psql falhou: $(($output | Select-Object -Last 3) -join ' ')" }
    return @($output | ForEach-Object { "$_" } | Where-Object { $_ })
}

function Wait-Until {
    param([Parameter(Mandatory)][scriptblock] $Condition, [int] $TimeoutSeconds = 120, [string] $What = 'condição')
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Tempo esgotado esperando $What ($TimeoutSeconds s)."
}

function New-Secret { [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)).ToLowerInvariant() }

function Write-Pem {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string] $Content)
    [IO.File]::WriteAllText($Path, $Content)
    if (-not $IsWindows) { & chmod 644 $Path }
}

function New-Ca {
    $key = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
    $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=dokpod-collectors-test-ca', $key, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true))
    $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign -bor [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::CrlSign, $true))
    return $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-10), [DateTimeOffset]::UtcNow.AddDays(2))
}

function New-ServerCertificate {
    param([Parameter(Mandatory)] $Ca, [Parameter(Mandatory)][string] $Name, [Parameter(Mandatory)][string] $Directory)
    $key = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
    try {
        $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new("CN=$Name", $key, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
        $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        $san.AddDnsName($Name)
        $request.CertificateExtensions.Add($san.Build())
        $usages = [System.Security.Cryptography.OidCollection]::new()
        [void]$usages.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'))
        $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usages, $false))
        $serial = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
        $certificate = $request.Create($Ca, [DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(1), $serial)
        try {
            Write-Pem (Join-Path $Directory 'server.crt') $certificate.ExportCertificatePem()
            Write-Pem (Join-Path $Directory 'server.key') $key.ExportPkcs8PrivateKeyPem()
        }
        finally { $certificate.Dispose() }
    }
    finally { $key.Dispose() }
}

function New-ApiCertificate {
    param([Parameter(Mandatory)][string] $Directory, [Parameter(Mandatory)][string] $Password)
    $key = [System.Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            'CN=dokpod-api', $key, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        $san.AddDnsName('dokpod-api')
        $request.CertificateExtensions.Add($san.Build())
        $request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true))
        $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(1))
        try {
            [IO.File]::WriteAllBytes((Join-Path $Directory 'api.pfx'), $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password))
            Write-Pem (Join-Path $Directory 'ca.crt') $certificate.ExportCertificatePem()
            if (-not $IsWindows) { & chmod 644 (Join-Path $Directory 'api.pfx') }
        }
        finally { $certificate.Dispose() }
    }
    finally { $key.Dispose() }
}

# ConvertFrom-Json devolve datas ISO como DateTime; o texto do OTLP chega como string.
function ConvertTo-Instant {
    param([Parameter(Mandatory)] $Value)
    if ($Value -is [datetime]) { return [DateTimeOffset]::new($Value.ToUniversalTime()) }
    return [DateTimeOffset]::Parse($Value, [Globalization.CultureInfo]::InvariantCulture)
}

function Get-Percentile {
    param([double[]] $Values, [double] $Percentile)
    if ($Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    return [Math]::Round($sorted[[int][Math]::Ceiling($sorted.Count * $Percentile) - 1], 3)
}

function Get-Distribution {
    param([double[]] $Values)
    [ordered]@{
        count = $Values.Count
        minSeconds = Get-Percentile $Values 0.0001
        p50Seconds = Get-Percentile $Values 0.50
        p95Seconds = Get-Percentile $Values 0.95
        maxSeconds = Get-Percentile $Values 1.0
    }
}

# A imagem do otel-collector não tem shell nem cat: o arquivo sai do container por cp.
function Read-CollectorFile {
    param([Parameter(Mandatory)][string] $Container, [Parameter(Mandatory)][string] $Path)
    $local = Join-Path $temporary "out-$Container"
    Remove-Item -Force -Path $local -ErrorAction SilentlyContinue
    & $Engine cp "$($Container):$Path" $local *> $null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $local)) { return '' }
    return [IO.File]::ReadAllText($local)
}

# Registros recebidos: eventId → (registro, chegada). O rsyslog grava "chegada<TAB>mensagem"; a mensagem é o JSON do
# contrato depois do BOM. O otel-collector grava uma linha JSON por exportação, com observedTime posto na chegada.
function Read-SyslogRecords {
    $text = Read-CollectorFile $names.rsyslog '/var/log/dokpod/syslog.log'
    $records = @{}
    foreach ($line in $text -split "`n") {
        $tab = $line.IndexOf("`t")
        if ($tab -lt 0) { continue }
        $message = $line.Substring($tab + 1).TrimStart([char]0xFEFF)
        $start = $message.IndexOf('{')
        if ($start -lt 0) { continue }
        $record = $message.Substring($start) | ConvertFrom-Json -AsHashtable
        $records[$record.eventId] = [pscustomobject]@{
            record = $record
            arrivedAt = [DateTimeOffset]::Parse($line.Substring(0, $tab), [Globalization.CultureInfo]::InvariantCulture)
            copies = 1 + $(if ($records.ContainsKey($record.eventId)) { $records[$record.eventId].copies } else { 0 })
        }
    }
    return $records
}

function Read-OtlpRecords {
    $text = Read-CollectorFile $names.otel '/out/otlp.json'
    $records = @{}
    foreach ($line in $text -split "`n" | Where-Object { $_.StartsWith('{') }) {
        $export = $line | ConvertFrom-Json -AsHashtable
        foreach ($resource in $export.resourceLogs) {
            foreach ($scope in $resource.scopeLogs) {
                foreach ($log in $scope.logRecords) {
                    $attributes = @{}
                    foreach ($attribute in $log.attributes) {
                        $value = $attribute.value
                        $attributes[$attribute.key] = if ($value.ContainsKey('stringValue')) { $value.stringValue } elseif ($value.ContainsKey('boolValue')) { $value.boolValue } else { $null }
                    }
                    $id = $attributes['dokpod.event.id']
                    if (-not $id) { continue }
                    $records[$id] = [pscustomobject]@{
                        record = @{
                            eventId = $id; action = $log.body.stringValue; outcome = $attributes['dokpod.outcome']
                            actor = @{ id = $attributes['dokpod.actor.id'] }; environmentId = $attributes['dokpod.environment.id']
                            occurredAt = [DateTimeOffset]::FromUnixTimeMilliseconds([long]([decimal]$log.timeUnixNano / 1000000)).ToString('o')
                        }
                        arrivedAt = [DateTimeOffset]::FromUnixTimeMilliseconds([long]([decimal]$log.observedTimeUnixNano / 1000000))
                        copies = 1 + $(if ($records.ContainsKey($id)) { $records[$id].copies } else { 0 })
                    }
                }
            }
        }
    }
    return $records
}

function Add-ContainerEvents {
    param([int] $Count, [string] $Prefix)
    $actions = @('container.restart', 'container.stop', 'container.start')
    $ids = @()
    for ($i = 0; $i -lt $Count; $i++) {
        $id = [Guid]::NewGuid().ToString()
        $action = $actions[$i % $actions.Count]
        # Uma transação por ação, como cada pedido da API; o horário é o do PostgreSQL no momento da gravação.
        Invoke-Sql "SELECT dokpod.dokpod_append_audit_event('$id'::uuid, clock_timestamp(), gen_random_uuid(), 0::smallint, '$Prefix-user', '$action', '$($script:environmentId)'::uuid, NULL, 0::smallint, NULL, sha256(convert_to('$id', 'UTF8')), 'web-$i', NULL, NULL);" | Out-Null
        $ids += $id
        Start-Sleep -Milliseconds 100
    }
    return $ids
}

function Compare-WithTrail {
    param([hashtable] $Received, [string[]] $Ids)
    $list = ($Ids | ForEach-Object { "'$_'" }) -join ','
    $rows = @(Invoke-Sql "SELECT event_id, action, outcome, actor_id, environment_id FROM dokpod.audit_events WHERE event_id IN ($list);")
    $mismatches = @()
    foreach ($row in $rows) {
        $fields = $row -split '\|'
        $item = $Received[$fields[0]]
        if (-not $item) { continue }
        $outcome = @('succeeded', 'failed', 'denied')[[int]$fields[2]]
        $record = $item.record
        if ($record.action -ne $fields[1] -or $record.outcome -ne $outcome -or $record.actor.id -ne $fields[3] -or $record.environmentId -ne $fields[4]) {
            $mismatches += $fields[0]
        }
    }
    return [ordered]@{ trailRows = $rows.Count; mismatches = $mismatches }
}

$temporary = Join-Path ([IO.Path]::GetTempPath()) "dokpod-collectors-$suffix"
$report = [ordered]@{
    startedAt = [DateTimeOffset]::UtcNow.ToString('o')
    engine = $Engine
    apiImage = $ApiImage
    images = $images
    latencyLimitSeconds = $latencyLimitSeconds
    scenarios = [ordered]@{}
    result = 'fail'
}
$exitCode = 1
$variables = @('POSTGRES_PASSWORD', 'DOKPOD_API_CERTIFICATE_PASSWORD', 'ConnectionStrings__ControlPlane', 'DOKPOD_COLLECTORS_TOKEN')

try {
    New-Item -ItemType Directory -Force -Path $ArtifactsPath | Out-Null
    foreach ($directory in @($temporary, "$temporary/api", "$temporary/secrets", "$temporary/secrets/siem-collectors", "$temporary/rsyslog", "$temporary/rsyslog/tls", "$temporary/otel", "$temporary/sql")) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        if (-not $IsWindows) { & chmod 755 $directory }
    }

    if ($Build) {
        Write-Output "Construindo $ApiImage (Business)..."
        Invoke-Engine @('build', '--file', (Join-Path $repositoryRoot 'backend/apps/Dokpod.ControlPlane.Api/Business.Dockerfile'),
            '--target', 'runtime', '--tag', $ApiImage, $repositoryRoot) | Out-Null
    }

    Write-Output 'Gerando o SQL das migrations...'
    Invoke-Engine @('run', '--rm', '--user', '0:0', '-v', "$($repositoryRoot):/src:ro,Z", '-v', "$temporary/sql:/out:Z", '-v', 'dokpod-nuget:/root/.nuget/packages',
        '-e', 'DOKPOD_CONTROLPLANE_CONNECTION=Host=unused;Database=unused', '-w', '/tmp', $images.sdk, 'bash', '-c',
        'cp -r /src /tmp/w && cd /tmp/w && dotnet tool restore >/dev/null && dotnet ef migrations script --project backend/libs/Dokpod.ControlPlane.Infrastructure/Dokpod.ControlPlane.Infrastructure.csproj --startup-project backend/apps/Dokpod.ControlPlane.Api/Dokpod.ControlPlane.Api.csproj -o /out/migrations.sql >/dev/null') | Out-Null

    # Valores desta execução, repassados aos containers por herança de ambiente (nunca na linha de comando).
    $pgCredential = New-Secret
    $certificateCredential = New-Secret
    $tokenCredential = New-Secret
    $env:POSTGRES_PASSWORD = $pgCredential
    $env:DOKPOD_API_CERTIFICATE_PASSWORD = $certificateCredential
    $env:DOKPOD_COLLECTORS_TOKEN = $tokenCredential
    $env:ConnectionStrings__ControlPlane = "Host=postgres;Port=5432;Database=dokpod;Username=dokpod;Password=$pgCredential;Search Path=dokpod"
    New-ApiCertificate -Directory "$temporary/api" -Password $env:DOKPOD_API_CERTIFICATE_PASSWORD
    $ca = New-Ca
    try {
        Write-Pem "$temporary/secrets/siem-collectors/ca.pem" $ca.ExportCertificatePem()
        New-ServerCertificate -Ca $ca -Name 'rsyslog' -Directory "$temporary/rsyslog/tls"
        New-ServerCertificate -Ca $ca -Name 'otel' -Directory "$temporary/otel"
    }
    finally { $ca.Dispose() }
    Write-Pem "$temporary/secrets/siem-collectors/token" $env:DOKPOD_COLLECTORS_TOKEN

    Set-Content -Path "$temporary/rsyslog/rsyslog.conf" -Encoding utf8 -Value @'
global(DefaultNetstreamDriver="ossl"
       DefaultNetstreamDriverCAFile="/etc/rsyslog.d/tls/server.crt"
       DefaultNetstreamDriverCertFile="/etc/rsyslog.d/tls/server.crt"
       DefaultNetstreamDriverKeyFile="/etc/rsyslog.d/tls/server.key")
module(load="imtcp" StreamDriver.Name="ossl" StreamDriver.Mode="1" StreamDriver.AuthMode="anon")
input(type="imtcp" port="6514")
template(name="arrival" type="string" string="%timegenerated:::date-rfc3339%\t%msg%\n")
action(type="omfile" file="/var/log/dokpod/syslog.log" template="arrival")
'@
    Set-Content -Path "$temporary/otel/config.yaml" -Encoding utf8 -Value @'
extensions:
  bearertokenauth:
    filename: /certs/token
receivers:
  otlp:
    protocols:
      http:
        endpoint: 0.0.0.0:4318
        tls:
          cert_file: /certs/server.crt
          key_file: /certs/server.key
        auth:
          authenticator: bearertokenauth
processors:
  transform:
    log_statements:
      - context: log
        statements:
          - set(observed_time, Now())
exporters:
  file:
    path: /out/otlp.json
service:
  extensions: [bearertokenauth]
  pipelines:
    logs:
      receivers: [otlp]
      processors: [transform]
      exporters: [file]
'@
    Copy-Item "$temporary/secrets/siem-collectors/token" "$temporary/otel/token"
    if (-not $IsWindows) { & chmod 644 "$temporary/rsyslog/rsyslog.conf" "$temporary/otel/config.yaml" "$temporary/otel/token" }

    Invoke-Engine @('network', 'create', '--internal', '--subnet', $subnet, $names.network) | Out-Null
    Invoke-Engine @('run', '-d', '--name', $names.postgres, '--network', $names.network, '--network-alias', 'postgres',
        '-e', 'POSTGRES_USER=dokpod', '-e', 'POSTGRES_DB=dokpod', '-e', 'POSTGRES_PASSWORD', $images.postgres) | Out-Null
    # Hosts Ubuntu têm um perfil AppArmor preso ao caminho /usr/sbin/rsyslogd que também confina o binário do container
    # e recusa sinais do runtime (o container não pararia). Pelo loader dinâmico o perfil não se aplica; os caminhos
    # ficam mesmo assim dentro do que ele permite.
    Invoke-Engine @('run', '-d', '--name', $names.rsyslog, '--network', $names.network, '--network-alias', 'rsyslog', '--tmpfs', '/var/log/dokpod:mode=0777',
        '-v', "$temporary/rsyslog/rsyslog.conf:/etc/rsyslog.conf:ro,Z", '-v', "$temporary/rsyslog/tls:/etc/rsyslog.d/tls:ro,Z",
        '--entrypoint', '/lib64/ld-linux-x86-64.so.2', $images.rsyslog, '/usr/sbin/rsyslogd', '-n', '-f', '/etc/rsyslog.conf') | Out-Null
    Invoke-Engine @('run', '-d', '--name', $names.otel, '--network', $names.network, '--network-alias', 'otel', '--tmpfs', '/out:mode=0777',
        '-v', "$temporary/otel:/certs:ro,Z", $images.otel, '--config', '/certs/config.yaml') | Out-Null

    Wait-Until -What 'o PostgreSQL' -TimeoutSeconds 60 -Condition {
        # A imagem sobe um servidor temporário para o initdb e reinicia; só a segunda mensagem é o servidor final.
        @((& $Engine logs $names.postgres 2>&1) -match 'ready to accept connections').Count -ge 2
    }
    Write-Output 'Aplicando as migrations...'
    Invoke-Engine @('cp', "$temporary/sql/migrations.sql", "$($names.postgres):/tmp/migrations.sql") | Out-Null
    Invoke-Engine @('exec', '-e', 'PGTZ=UTC', $names.postgres, 'psql', '-v', 'ON_ERROR_STOP=1', '-X', '-q', '-U', 'dokpod', '-d', 'dokpod', '-f', '/tmp/migrations.sql') | Out-Null

    Write-Output 'Subindo a API Business...'
    Invoke-Engine @('run', '-d', '--name', $names.api, '--network', $names.network, '--network-alias', 'dokpod-api',
        '-e', 'ASPNETCORE_ENVIRONMENT=Development', '-e', 'DOKPOD_API_GRPC_PORT=7443', '-e', 'DOKPOD_API_HEALTH_PORT=8080',
        '-e', 'DOKPOD_API_CERTIFICATE_PATH=/certs/api.pfx', '-e', 'DOKPOD_API_CERTIFICATE_PASSWORD',
        '-e', 'DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH=/certs/ca.crt',
        '-e', 'Authentication__Keycloak__Authority=http://keycloak.invalid/realms/dokpod',
        '-e', 'Authentication__Keycloak__BackchannelAuthority=http://keycloak.invalid/realms/dokpod',
        '-e', 'Authentication__Keycloak__Audience=dokpod-api',
        '-e', 'ConnectionStrings__ControlPlane', '-e', 'DOKPOD_AUDIT_EXPORT_HOSTNAME=dokpod-collectors',
        '-e', 'OTEL_EXPORTER_OTLP_METRICS_ENDPOINT=', '-e', 'OTEL_EXPORTER_OTLP_TRACES_ENDPOINT=',
        '-v', "$temporary/api:/certs:ro,Z", '-v', "$temporary/secrets:/run/secrets/dokpod-audit-export:ro,Z", $ApiImage) | Out-Null
    Wait-Until -What 'a API' -Condition { & $Engine exec $names.api curl -fsS -o /dev/null http://127.0.0.1:8080/health/live *> $null; $LASTEXITCODE -eq 0 }

    # Mesmo efeito do AuditExportStore.CreateAsync: destino e cursor no fim da trilha (D-001).
    $script:environmentId = [Guid]::NewGuid().ToString()
    $destinations = [ordered]@{ syslog = [Guid]::NewGuid().ToString(); otlp = [Guid]::NewGuid().ToString() }
    Invoke-Sql @"
BEGIN;
INSERT INTO dokpod.audit_export_destinations (destination_id, name, protocol, host, port, path, secret_ref, categories, active, version, created_at_utc, updated_at_utc)
VALUES ('$($destinations.syslog)', 'rsyslog-t033', 'syslog', 'rsyslog', 6514, NULL, 'siem-collectors', ARRAY['audit_export','containers'], true, 1, now(), now()),
       ('$($destinations.otlp)', 'otel-t033', 'otlp', 'otel', 4318, '/v1/logs', 'siem-collectors', ARRAY['audit_export','containers'], true, 1, now(), now());
INSERT INTO dokpod.audit_export_cursors (destination_id, last_tx_id, last_seq)
SELECT destination_id, pg_snapshot_xmin(pg_current_snapshot()), 0 FROM dokpod.audit_export_destinations WHERE destination_id IN ('$($destinations.syslog)', '$($destinations.otlp)');
COMMIT;
"@ | Out-Null

    Write-Output "Cenários 1 e 3: gravando $Events ações de container..."
    $ids = Add-ContainerEvents -Count $Events -Prefix 't033'
    $received = @{}
    Wait-Until -What "os $Events registros nos dois coletores" -TimeoutSeconds 90 -Condition {
        $received.syslog = Read-SyslogRecords
        $received.otlp = Read-OtlpRecords
        @($ids | Where-Object { $received.syslog.ContainsKey($_) -and $received.otlp.ContainsKey($_) }).Count -eq $ids.Count
    }

    $passed = $true
    foreach ($protocol in @('syslog', 'otlp')) {
        $records = $received[$protocol]
        $latencies = [double[]]@($ids | ForEach-Object {
                $item = $records[$_]
                ($item.arrivedAt - (ConvertTo-Instant $item.record.occurredAt)).TotalSeconds
            })
        $comparison = Compare-WithTrail -Received $records -Ids $ids
        $duplicates = @($ids | Where-Object { $records[$_].copies -gt 1 })
        $distribution = Get-Distribution $latencies
        $ok = $comparison.trailRows -eq $ids.Count -and $comparison.mismatches.Count -eq 0 -and $duplicates.Count -eq 0 -and $distribution.p95Seconds -le $latencyLimitSeconds
        $report.scenarios[$protocol] = [ordered]@{
            scenario = if ($protocol -eq 'syslog') { 'quickstart 1' } else { 'quickstart 3' }
            events = $ids.Count
            received = @($ids | Where-Object { $records.ContainsKey($_) }).Count
            duplicates = $duplicates.Count
            trailRows = $comparison.trailRows
            fieldMismatches = $comparison.mismatches
            latency = $distribution
            result = if ($ok) { 'pass' } else { 'fail' }
        }
        $passed = $passed -and $ok
    }

    Write-Output 'Desmarcando a categoria containers e gravando mais 5 ações...'
    Invoke-Sql "UPDATE dokpod.audit_export_destinations SET categories = ARRAY['audit_export'], version = version + 1, updated_at_utc = now() WHERE destination_id IN ('$($destinations.syslog)', '$($destinations.otlp)');" | Out-Null
    Start-Sleep -Seconds 2
    $filtered = Add-ContainerEvents -Count 5 -Prefix 't033-filtro'
    Start-Sleep -Seconds 8
    $syslogAfter = Read-SyslogRecords
    $otlpAfter = Read-OtlpRecords
    $inTrail = @(Invoke-Sql "SELECT count(*) FROM dokpod.audit_events WHERE event_id IN ($(($filtered | ForEach-Object { "'$_'" }) -join ','));")[0]
    $leaked = @($filtered | Where-Object { $syslogAfter.ContainsKey($_) -or $otlpAfter.ContainsKey($_) })
    $report.scenarios.categoryUnchecked = [ordered]@{
        events = $filtered.Count
        arrived = $leaked.Count
        inTrail = [int]$inTrail
        result = if ($leaked.Count -eq 0 -and [int]$inTrail -eq $filtered.Count) { 'pass' } else { 'fail' }
    }
    $passed = $passed -and $report.scenarios.categoryUnchecked.result -eq 'pass'

    $states = Invoke-Sql "SELECT d.protocol, coalesce(c.last_error_code, '-') FROM dokpod.audit_export_destinations d JOIN dokpod.audit_export_cursors c USING (destination_id);"
    $report.destinationErrors = $states
    $logs = (Invoke-Engine @('logs', $names.api) -AllowFailure) -join "`n"
    $report.tokenInApiLogs = $logs.Contains($env:DOKPOD_COLLECTORS_TOKEN, [StringComparison]::Ordinal)
    $passed = $passed -and -not $report.tokenInApiLogs
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
        $report | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $ArtifactsPath 'report.json') -Encoding utf8
    }
    if (-not $KeepStack) {
        foreach ($name in @($names.api, $names.otel, $names.rsyslog, $names.postgres)) { & $Engine rm -f $name *> $null }
        & $Engine network rm $names.network *> $null
    }
    Remove-Item -Recurse -Force -Path $temporary -ErrorAction SilentlyContinue
    foreach ($variable in $variables) { Remove-Item -Path "env:$variable" -ErrorAction SilentlyContinue }
}

foreach ($entry in $report.scenarios.GetEnumerator()) {
    $latency = if ($entry.Value.Contains('latency')) { " p50=$($entry.Value.latency.p50Seconds) s p95=$($entry.Value.latency.p95Seconds) s máx=$($entry.Value.latency.maxSeconds) s" } else { '' }
    Write-Output "$($entry.Key): $($entry.Value.result)$latency"
}
Write-Output "Resultado: $($report.result). Relatório: $(Join-Path $ArtifactsPath 'report.json')"
exit $exitCode
