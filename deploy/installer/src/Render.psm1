#Requires -Version 7.4
<#
.SYNOPSIS
Arquivos da instalação (spec 033 T026, FR-020 a FR-023, FR-043, D-006, D-008, L1): compose.yaml, dokpod.env,
deploy.env, configuração da borda e o bundle de CAs. Nenhum arquivo daqui tem segredo; os segredos entram nos serviços
pelos `secrets:` do Compose, montados em /run/secrets.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')

# Sub-rede da rede interna da borda: a única em que o BFF confia para X-Forwarded-* com a borda provisionada (L1).
$script:EdgeSubnet = '10.253.33.0/24'
# Com nginx existente numa rede compartilhada, o nome dos serviços nela (FR-043).
$script:SharedAliases = [ordered]@{ web = 'dokpod-web'; bff = 'dokpod-bff'; keycloak = 'dokpod-keycloak' }
$script:SystemCaBundle = '/etc/ssl/certs/ca-certificates.crt'

function Expand-InstallerTemplate {
    <#
    .SYNOPSIS
    Substitui {{CHAVE}} pelos valores; placeholder sem valor é erro.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Values
    )

    $text = [System.IO.File]::ReadAllText((Join-Path (Get-InstallerHome) "templates/$Name"))
    foreach ($key in $Values.Keys) {
        $text = $text.Replace("{{$key}}", [string] $Values[$key])
    }

    if ($text -match '\{\{([A-Z_]+)\}\}') {
        Stop-Installer -Code Error -Message "O template $Name ficou com o placeholder $($Matches[1]) sem valor."
    }

    return $text
}

function Get-PublicEndpoint {
    param([System.Collections.IDictionary] $Answers)

    $uri = [uri] $Answers.publicUrl
    return [pscustomobject]@{ Host = $uri.Host; Port = $(if ($uri.IsDefaultPort) { 443 } else { $uri.Port }); Origin = $uri.GetLeftPart([UriPartial]::Authority) }
}

function Get-KeycloakAuthority {
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $base = if ($Answers.keycloak.mode -eq 'existing') { $Answers.keycloak.url.TrimEnd('/') } else { (Get-PublicEndpoint $Answers).Origin }
    return "$base/realms/$($Answers.keycloak.realm)"
}

function Get-BffTrustedNetworks {
    <#
    .SYNOPSIS
    Redes cujos X-Forwarded-* o BFF aceita (L1): a rede interna da borda; com nginx existente em loopback, também o
    loopback; numa rede compartilhada, só as sub-redes dela, medidas por Get-EdgeTrustedNetworks (Edge.psm1) e passadas
    em -Detected.
    .DESCRIPTION
    Em loopback, a conexão do nginx do host não chega ao BFF como 127.0.0.1: o encaminhador de portas do modo rootless
    a entrega com um endereço da rede pela qual a porta é publicada (no Podman 5.7, o do próprio container nela; no
    Docker, o gateway). Por isso, nesse modo, o BFF fica só na dokpod-edge, e a sub-rede dela, onde só há containers do
    Dokpod, continua confiável.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [string[]] $Detected = @()
    )

    if ($Answers.edge.mode -eq 'provision') { return , [string[]] @($script:EdgeSubnet) }
    if ($Answers.edge.upstreamHost -is [System.Collections.IDictionary]) {
        if ($Detected.Count -eq 0) {
            Stop-Installer -Code Error -Message "As sub-redes da rede $($Answers.edge.upstreamHost.network) não foram lidas; o BFF não teria em quem confiar."
        }

        return , [string[]] @($Detected)
    }

    return , [string[]] @('127.0.0.1/32', '::1/128', $script:EdgeSubnet)
}

function Assert-TrustedNetworks {
    # Nenhuma faixa maior que a rede interna (/24) nem universal (L1).
    param([string[]] $Networks)

    foreach ($network in $Networks) {
        $parsed = [System.Net.IPNetwork]::Parse($network)
        $minimum = if ($parsed.BaseAddress.AddressFamily -eq 'InterNetworkV6') { 120 } else { 24 }
        if ($parsed.PrefixLength -lt $minimum) {
            Stop-Installer -Code Usage -Message "A rede confiável $network é maior que a rede interna; o BFF aceitaria X-Forwarded-* de fora da borda."
        }
    }
}

function New-InstallerComposeModel {
    <#
    .SYNOPSIS
    Modelo do compose.yaml: produção, redes internas, só a borda (e a porta mTLS dos agentes, com agentGrpcPort) publicadas.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Images,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Infrastructure,
        [Parameter(Mandatory)][string] $EngineSocket,
        [Parameter(Mandatory)][string] $LocalEnvironmentId,
        # Com nginx existente numa rede compartilhada, as sub-redes dela (Get-EdgeTrustedNetworks, Edge.psm1).
        [string[]] $TrustedNetworks = @()
    )

    $installDir = $Answers.installDir
    $secretsDir = $Answers.secretsDir
    $endpoint = Get-PublicEndpoint $Answers
    $authority = Get-KeycloakAuthority -Answers $Answers
    $trusted = Get-BffTrustedNetworks -Answers $Answers -Detected $TrustedNetworks
    Assert-TrustedNetworks -Networks $trusted
    $caBundle = if (Test-CaBundleNeeded -Answers $Answers) { '/certs/ca-bundle.crt' } else { $null }
    $secrets = [ordered]@{}
    $mount = {
        param([string] $name, [string] $file, [string] $target)
        $secrets[$name] = [ordered]@{ file = (Join-Path $secretsDir $file) }
        return [ordered]@{ source = $name; target = $target }
    }

    $services = [ordered]@{}
    if ($Answers.postgres.mode -eq 'provision') {
        $services['dokpod-postgres'] = [ordered]@{
            image       = $Infrastructure.postgresql
            restart     = 'unless-stopped'
            environment = [ordered]@{ POSTGRES_USER = 'dokpod_admin'; POSTGRES_DB = 'postgres'; POSTGRES_PASSWORD_FILE = '/run/secrets/dokpod-db-password' }
            secrets     = @(& $mount 'dokpod-db-password' 'dokpod-db-password' '/run/secrets/dokpod-db-password')
            volumes     = @('dokpod-postgres-data:/var/lib/postgresql')
            networks    = @('dokpod-data')
            healthcheck = [ordered]@{ test = @('CMD-SHELL', 'pg_isready -U dokpod_admin -d postgres'); interval = '5s'; timeout = '5s'; retries = 12 }
        }
    }

    if ($Answers.keycloak.mode -eq 'provision') {
        $services['keycloak-postgres'] = [ordered]@{
            image       = $Infrastructure.postgresql
            restart     = 'unless-stopped'
            environment = [ordered]@{ POSTGRES_USER = 'keycloak'; POSTGRES_DB = 'keycloak'; POSTGRES_PASSWORD_FILE = '/run/secrets/keycloak-db-password' }
            secrets     = @(& $mount 'keycloak-db-password' 'keycloak-db-password' '/run/secrets/keycloak-db-password')
            volumes     = @('keycloak-postgres-data:/var/lib/postgresql')
            networks    = @('keycloak-data')
            healthcheck = [ordered]@{ test = @('CMD-SHELL', 'pg_isready -U keycloak -d keycloak'); interval = '5s'; timeout = '5s'; retries = 12 }
        }
        # Os segredos entram no processo pelo shell do entrypoint, lidos dos arquivos montados: nunca em `environment`.
        $services['keycloak'] = [ordered]@{
            image       = $Infrastructure.keycloak
            restart     = 'unless-stopped'
            entrypoint  = @('/bin/sh', '-c')
            command     = @('read -r KC_DB_PASSWORD < /run/secrets/keycloak-db-password; export KC_DB_PASSWORD; ' +
                'if [ -s /run/secrets/keycloak-bootstrap-admin ]; then read -r KC_BOOTSTRAP_ADMIN_PASSWORD < /run/secrets/keycloak-bootstrap-admin; ' +
                'export KC_BOOTSTRAP_ADMIN_USERNAME=dokpod-bootstrap KC_BOOTSTRAP_ADMIN_PASSWORD; fi; ' +
                'exec /opt/keycloak/bin/kc.sh start')
            environment = [ordered]@{
                KC_DB = 'postgres'; KC_DB_URL = 'jdbc:postgresql://keycloak-postgres/keycloak'; KC_DB_USERNAME = 'keycloak'
                KC_HOSTNAME = $endpoint.Origin; KC_HTTP_ENABLED = 'true'; KC_PROXY_HEADERS = 'xforwarded'; KC_HEALTH_ENABLED = 'true'
            }
            secrets     = @((& $mount 'keycloak-db-password' 'keycloak-db-password' '/run/secrets/keycloak-db-password'),
                (& $mount 'keycloak-bootstrap-admin' 'keycloak-bootstrap-admin' '/run/secrets/keycloak-bootstrap-admin'))
            networks    = @('keycloak-data', 'dokpod-edge')
            depends_on  = [ordered]@{ 'keycloak-postgres' = [ordered]@{ condition = 'service_healthy' } }
            # O teste da imagem, a cada 5 s em vez de 30 s, para a subida ordenada (T032) não esperar à toa.
            healthcheck = [ordered]@{
                # $$ é o $ escapado da interpolação do Compose.
                test     = @('CMD', 'bash', '-c', ('exec 3<>/dev/tcp/127.0.0.1/9000 && printf ''GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n'' >&3 && ' +
                        'read -r status <&3 && [[ "$$status" == *" 200 "* ]]'))
                interval = '5s'; timeout = '5s'; retries = 6; start_period = '120s'
            }
        }
    }

    # Sem BackchannelAuthority: fora de Development a API só aceita autoridade HTTPS, e a Authority pública chega à borda
    # pelo alias do nome público na rede dokpod-edge (D-008), validada pelo bundle de CAs.
    $apiEnvironment = [ordered]@{
        ASPNETCORE_ENVIRONMENT                           = 'Production'
        DOKPOD_API_GRPC_PORT                             = '7443'
        DOKPOD_API_HEALTH_PORT                           = '8080'
        # REST do BFF em HTTPS na rede interna (o BFF recusa downstream HTTP fora de Development).
        DOKPOD_API_HTTPS_PORT                            = '8443'
        DOKPOD_API_CERTIFICATE_PATH                      = '/certs/api.pfx'
        DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH            = '/certs/agents-ca.crt'
        Authentication__Keycloak__Authority              = $authority
        Authentication__Keycloak__Audience               = 'dokpod-api'
        DataProtection__KeysPath                         = '/var/lib/dokpod-api/keys'
    }
    if ($caBundle) { $apiEnvironment['SSL_CERT_FILE'] = $caBundle }
    $api = [ordered]@{
        image       = $Images.api
        restart     = 'unless-stopped'
        environment = $apiEnvironment
        secrets     = @((& $mount 'api-connection' 'ConnectionStrings__ControlPlane' '/run/secrets/ConnectionStrings__ControlPlane'),
            (& $mount 'api-pfx-password' 'pki/api.pass' '/run/secrets/DOKPOD_API_CERTIFICATE_PASSWORD'),
            (& $mount 'api-pfx' 'pki/api.pfx' '/certs/api.pfx'))
        volumes     = @('dokpod-api-keys:/var/lib/dokpod-api', "$(Join-Path $installDir 'certs/agents-ca.crt'):/certs/agents-ca.crt:ro")
        networks    = [ordered]@{ 'dokpod-internal' = [ordered]@{ aliases = @('api') }; 'dokpod-data' = [ordered]@{}; 'dokpod-edge' = [ordered]@{} }
    }
    if ($caBundle) { $api.volumes += "$(Join-Path $installDir 'certs/ca-bundle.crt'):$($caBundle):ro" }
    if ($Answers.Contains('agentGrpcPort') -and $Answers.agentGrpcPort) {
        $api['ports'] = @([ordered]@{ target = 7443; published = [string] $Answers.agentGrpcPort; protocol = 'tcp' })
    }
    if ($Answers.postgres.mode -eq 'provision') { $api['depends_on'] = [ordered]@{ 'dokpod-postgres' = [ordered]@{ condition = 'service_healthy' } } }
    $services['api'] = $api

    $bffEnvironment = [ordered]@{
        ASPNETCORE_ENVIRONMENT                = 'Production'
        Authentication__Keycloak__Authority   = $authority
        Authentication__Keycloak__ClientId    = 'dokpod-bff'
        Security__AllowedOrigins__0           = $endpoint.Origin
        # O padrão do BFF é só localhost (o healthcheck da imagem); a borda repassa o Host público.
        AllowedHosts                          = "$($endpoint.Host);localhost"
        Downstream__Api__BaseUrl              = 'https://api:8443'
    }
    for ($index = 0; $index -lt $trusted.Count; $index++) { $bffEnvironment["Security__TrustedNetworks__$index"] = $trusted[$index] }
    if ($caBundle) { $bffEnvironment['SSL_CERT_FILE'] = $caBundle }
    # Fora de Development o BFF exige o certificado que protege o key ring (RSA, pelo XML cifrado do Data Protection).
    $bffEnvironment['Security__DataProtectionCertificatePath'] = '/certs/bff-dataprotection.pfx'
    $bff = [ordered]@{
        image       = $Images.bff
        restart     = 'unless-stopped'
        environment = $bffEnvironment
        secrets     = @((& $mount 'bff-client-secret' 'Authentication__Keycloak__ClientSecret' '/run/secrets/Authentication__Keycloak__ClientSecret'),
            (& $mount 'bff-dataprotection-pfx' 'pki/bff-dataprotection.pfx' '/certs/bff-dataprotection.pfx'),
            (& $mount 'bff-dataprotection-password' 'pki/bff-dataprotection.pass' '/run/secrets/Security__DataProtectionCertificatePassword'))
        volumes     = @('dokpod-bff-keys:/var/lib/dokpod-bff')
        networks    = @('dokpod-internal', 'dokpod-edge')
        depends_on  = [ordered]@{ api = [ordered]@{ condition = 'service_started' } }
    }
    if ($caBundle) { $bff.volumes += "$(Join-Path $installDir 'certs/ca-bundle.crt'):$($caBundle):ro" }
    $web = [ordered]@{ image = $Images.web; restart = 'unless-stopped'; networks = @('dokpod-edge') }
    if ($Answers.edge.mode -eq 'existing' -and $Answers.edge.upstreamHost -isnot [System.Collections.IDictionary]) {
        $bindHost = [string] $Answers.edge.upstreamHost
        $bff['ports'] = @([ordered]@{ target = 8080; published = [string] $Answers.edge.bffPort; host_ip = $bindHost; protocol = 'tcp' })
        # Só na dokpod-edge: o encaminhador rootless entrega a conexão do nginx do host com um endereço da rede pela qual
        # a porta sai, e com o BFF também na dokpod-internal seria o dela, fora das redes confiáveis. A API continua
        # alcançável como api na dokpod-edge.
        $bff.networks = @('dokpod-edge')
        $web['ports'] = @([ordered]@{ target = 8080; published = [string] $Answers.edge.webPort; host_ip = $bindHost; protocol = 'tcp' })
    }
    # Nginx do usuário: numa rede compartilhada, web, BFF e o Keycloak provisionado entram nela com nomes próprios; em
    # loopback, o Keycloak provisionado também escuta só em upstreamHost.
    $sharedNetwork = if ($Answers.edge.mode -eq 'existing' -and $Answers.edge.upstreamHost -is [System.Collections.IDictionary]) { [string] $Answers.edge.upstreamHost.network } else { $null }
    $join = {
        param([System.Collections.IDictionary] $service, [string] $alias)
        if ($service.networks -isnot [System.Collections.IDictionary]) {
            $map = [ordered]@{}
            foreach ($name in $service.networks) { $map[$name] = [ordered]@{} }
            $service.networks = $map
        }
        if (-not $service.networks.Contains('dokpod-shared')) { $service.networks['dokpod-shared'] = [ordered]@{} }
        if ($alias) { $service.networks['dokpod-shared']['aliases'] = @($alias) }
    }
    if ($sharedNetwork) {
        & $join $web $script:SharedAliases.web
        & $join $bff $script:SharedAliases.bff
        if ($services.Contains('keycloak')) { & $join $services['keycloak'] $script:SharedAliases.keycloak }
    }
    elseif ($Answers.edge.mode -eq 'existing' -and $services.Contains('keycloak')) {
        $services['keycloak']['ports'] = @([ordered]@{ target = 8080; published = [string] $Answers.edge.keycloakPort; host_ip = [string] $Answers.edge.upstreamHost; protocol = 'tcp' })
    }

    # A Authority do Keycloak provisionado é o nome público, que agora é o nginx do usuário: na rede compartilhada, ele
    # recebe o nome público como alias (passo manual); em loopback no Podman, o nome aponta para o host (host-gateway),
    # porque o pasta dá ao container o próprio IP do host. No Docker rootless o nome resolve pelo DNS (T038).
    if ($Answers.edge.mode -eq 'existing' -and $Answers.keycloak.mode -eq 'provision') {
        foreach ($service in $api, $bff) {
            if ($sharedNetwork) { & $join $service $null }
            elseif ($Answers.engine -eq 'podman-rootless') { $service['extra_hosts'] = @("$($endpoint.Host):host-gateway") }
        }
    }
    $services['bff'] = $bff
    $services['web'] = $web

    if ($Answers.edge.mode -eq 'provision') {
        $edgeDepends = [ordered]@{ bff = [ordered]@{ condition = 'service_started' }; web = [ordered]@{ condition = 'service_started' } }
        if ($Answers.keycloak.mode -eq 'provision') { $edgeDepends['keycloak'] = [ordered]@{ condition = 'service_started' } }
        $services['edge'] = [ordered]@{
            image      = $Infrastructure.nginx
            restart    = 'unless-stopped'
            ports      = @([ordered]@{ target = 8443; published = [string] $Answers.edge.httpsPort; protocol = 'tcp' })
            secrets    = @((& $mount 'edge-certificate' 'pki/edge.crt' '/etc/nginx/certs/tls.crt'), (& $mount 'edge-key' 'pki/edge.key' '/etc/nginx/certs/tls.key'))
            volumes    = @("$(Join-Path $installDir 'edge/dokpod.conf'):/etc/nginx/conf.d/dokpod.conf:ro", "$(Join-Path $installDir 'edge/dokpod.locations'):/etc/nginx/conf.d/dokpod.locations:ro")
            # O nome público resolve para a borda dentro da rede: API e BFF alcançam a Authority pelo mesmo nome do navegador (D-008).
            networks   = [ordered]@{ 'dokpod-edge' = [ordered]@{ aliases = @($endpoint.Host) } }
            depends_on = $edgeDepends
            # A imagem não declara healthcheck; o nginx.conf dela serve /nginx-healthz só no loopback.
            healthcheck = [ordered]@{ test = @('CMD', 'curl', '--fail', '--silent', 'http://127.0.0.1:8081/nginx-healthz'); interval = '5s'; timeout = '3s'; retries = 6 }
        }
    }

    $services['agent'] = [ordered]@{
        image       = $Images.agent
        restart     = 'unless-stopped'
        # Engine rootless: o root do container é o usuário do host, sem privilégio no host (D-015).
        user        = '0:0'
        read_only   = $true
        tmpfs       = @('/tmp:rw,noexec,nosuid,size=16m')
        environment = [ordered]@{
            DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT     = 'https://api:7443'
            DOKPOD_AGENT_ENVIRONMENT_ID             = $LocalEnvironmentId
            DOKPOD_AGENT_CLIENT_CERTIFICATE_PATH    = '/certs/agent.pfx'
            DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH = '/certs/ca.crt'
            DOKPOD_AGENT_DATA_DIRECTORY             = '/var/lib/dokpod-agent'
            DOKPOD_AGENT_DOCKER_SOCKET              = '/run/engine.sock'
        }
        secrets     = @((& $mount 'agent-pfx' "pki/agent-$LocalEnvironmentId.pfx" '/certs/agent.pfx'),
            (& $mount 'agent-pfx-password' "pki/agent-$LocalEnvironmentId.pass" '/run/secrets/DOKPOD_AGENT_CLIENT_CERTIFICATE_PASSWORD'))
        volumes     = @("$($EngineSocket):/run/engine.sock", 'dokpod-agent-data:/var/lib/dokpod-agent', "$(Join-Path $installDir 'certs/ca.crt'):/certs/ca.crt:ro")
        networks    = @('dokpod-internal')
        depends_on  = [ordered]@{ api = [ordered]@{ condition = 'service_started' } }
    }

    $networks = [ordered]@{
        'dokpod-internal' = [ordered]@{ internal = $true }
        # Com PostgreSQL existente, a API sai por esta rede até o servidor; só o provisionado fica isolado.
        'dokpod-data'     = [ordered]@{ internal = $Answers.postgres.mode -eq 'provision' }
        'dokpod-edge'     = [ordered]@{ ipam = [ordered]@{ config = @([ordered]@{ subnet = $script:EdgeSubnet }) } }
    }
    if ($Answers.keycloak.mode -eq 'provision') { $networks['keycloak-data'] = [ordered]@{ internal = $true } }
    if ($sharedNetwork) { $networks['dokpod-shared'] = [ordered]@{ external = $true; name = $sharedNetwork } }

    $volumes = [ordered]@{ 'dokpod-api-keys' = [ordered]@{}; 'dokpod-bff-keys' = [ordered]@{}; 'dokpod-agent-data' = [ordered]@{} }
    if ($Answers.postgres.mode -eq 'provision') { $volumes['dokpod-postgres-data'] = [ordered]@{} }
    if ($Answers.keycloak.mode -eq 'provision') { $volumes['keycloak-postgres-data'] = [ordered]@{} }

    return [ordered]@{ name = 'dokpod'; services = $services; networks = $networks; volumes = $volumes; secrets = $secrets }
}

function Test-CaBundleNeeded {
    param([System.Collections.IDictionary] $Answers)

    # A API com a CA local também precisa do bundle: o BFF a valida em https://api:8443.
    $localCa = $Answers.certificates.edge.source -eq 'local-ca' -or $Answers.certificates.api.source -eq 'local-ca' -or $Answers.keycloak.mode -eq 'provision'
    $keycloakCa = $Answers.keycloak.mode -eq 'existing' -and $Answers.keycloak.Contains('caCertificate')
    $postgresCa = $Answers.postgres.mode -ne 'provision' -and $Answers.postgres.Contains('caCertificate')
    return $localCa -or $keycloakCa -or $postgresCa
}

function ConvertTo-ComposeYaml {
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Model)

    Import-InstallerLibraries
    $serializer = [YamlDotNet.Serialization.SerializerBuilder]::new().WithQuotingNecessaryStrings().Build()
    return "# Gerado pelo instalador do Dokpod (spec 033); sem segredos. Atualizações sem migration: deploy/hooks/compose-deploy-hook.sh.`n" +
        $serializer.Serialize((ConvertTo-PlainGraph $Model))
}

function ConvertTo-PlainGraph {
    # Dicionários e listas do .NET que o YamlDotNet serializa na ordem de inserção.
    param($Node)

    if ($Node -is [psobject]) { $Node = $Node.PSObject.BaseObject }
    if ($Node -is [System.Collections.IDictionary]) {
        $map = [System.Collections.Generic.Dictionary[string, object]]::new()
        foreach ($key in $Node.Keys) { $map[[string] $key] = ConvertTo-PlainGraph $Node[$key] }
        return $map
    }

    if ($Node -is [System.Collections.IEnumerable] -and $Node -isnot [string]) {
        $list = [System.Collections.Generic.List[object]]::new()
        foreach ($item in $Node) { $list.Add((ConvertTo-PlainGraph $item)) }
        return , $list
    }

    return $Node
}

function Get-ComposeServiceHashes {
    <#
    .SYNOPSIS
    Hash da configuração desejada de cada serviço (sem segredos), para o plano decidir keep/update (D-007).
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Model)

    $hashes = [ordered]@{}
    foreach ($name in $Model.services.Keys) {
        $service = [ordered]@{}
        foreach ($key in $Model.services[$name].Keys) { if ($key -ne 'labels') { $service[$key] = $Model.services[$name][$key] } }
        $hashes[$name] = Get-Sha256Hex -Text ($service | ConvertTo-Json -Depth 16 -Compress)
    }

    return $hashes
}

function Set-ComposeConfigHashLabels {
    <#
    .SYNOPSIS
    Grava em cada serviço o rótulo io.dokpod.installer.config-hash, que o plano lê do engine (D-007), e devolve os hashes.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Model)

    $hashes = Get-ComposeServiceHashes -Model $Model
    foreach ($name in $hashes.Keys) {
        $Model.services[$name]['labels'] = [ordered]@{ 'io.dokpod.installer.config-hash' = $hashes[$name] }
    }

    return $hashes
}

function New-EdgeConfiguration {
    <#
    .SYNOPSIS
    dokpod.conf (servidor da borda provisionada ou o map para o nginx existente) e dokpod.locations.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $endpoint = Get-PublicEndpoint $Answers
    $existing = $Answers.edge.mode -eq 'existing'
    $upstreamHost = if ($existing -and $Answers.edge.upstreamHost -isnot [System.Collections.IDictionary]) { [string] $Answers.edge.upstreamHost } else { $null }
    $format = { param($hostName, $port) if ($hostName -match ':') { "[$hostName]:$port" } else { "$($hostName):$port" } }
    $values = [ordered]@{
        VERSION            = $Answers.version
        SERVER_NAME        = $endpoint.Host
        PUBLIC_PORT        = $endpoint.Port
        INSTALL_DIR        = $Answers.installDir
        BFF_UPSTREAM       = $(if ($upstreamHost) { & $format $upstreamHost $Answers.edge.bffPort } elseif ($existing) { "$($script:SharedAliases.bff):8080" } else { 'bff:8080' })
        WEB_UPSTREAM       = $(if ($upstreamHost) { & $format $upstreamHost $Answers.edge.webPort } elseif ($existing) { "$($script:SharedAliases.web):8080" } else { 'web:8080' })
        KEYCLOAK_LOCATIONS = ''
        INTERNAL_LISTEN    = ''
    }
    # Na rede dokpod-edge, o nome público resolve para a borda (D-008): API e BFF chegam pela porta pública, que pode
    # não ser a 8443 publicada no host.
    if ($endpoint.Port -ne 8443) {
        $values.INTERNAL_LISTEN = "    # Tráfego interno pela porta pública (API e BFF até a Authority, D-008).`n    listen $($endpoint.Port) ssl;"
    }
    if ($Answers.keycloak.mode -eq 'provision') {
        $keycloakUpstream = if ($upstreamHost) { & $format $upstreamHost $Answers.edge.keycloakPort } elseif ($existing) { "$($script:SharedAliases.keycloak):8080" } else { 'keycloak:8080' }
        $values.KEYCLOAK_LOCATIONS = Expand-InstallerTemplate -Name 'edge/keycloak.locations.template' -Values @{ PUBLIC_PORT = $endpoint.Port; KEYCLOAK_UPSTREAM = $keycloakUpstream }
    }

    return [ordered]@{
        'dokpod.conf'      = Expand-InstallerTemplate -Name $(if ($existing) { 'edge/existing.conf.template' } else { 'edge/server.conf.template' }) -Values $values
        'dokpod.locations' = Expand-InstallerTemplate -Name 'edge/locations.conf.template' -Values $values
    }
}

function New-DeployEnvironment {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Model
    )

    $api = if ($Answers.edition -eq 'business') { 'dokpod-api-business' } else { 'dokpod-ce-api' }
    $services = "$api=api dokpod-ce-bff=bff dokpod-ce-web=web dokpod-ce-agent=agent"
    $order = (@('edge', 'bff', 'web', 'agent', 'api') | Where-Object { $Model.services.Contains($_) } | Where-Object { $_ -ne 'edge' }) -join ' '
    return Expand-InstallerTemplate -Name 'deploy.env.template' -Values @{ INSTALL_DIR = $Answers.installDir; SERVICES = $services; ORDER = $order }
}

function New-DokpodEnvironmentFile {
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    return @(
        '# Valores não secretos da instalação (spec 033); o compose.yaml já os traz resolvidos.'
        "DOKPOD_VERSION=$($Answers.version)"
        "DOKPOD_EDITION=$($Answers.edition)"
        "DOKPOD_PUBLIC_URL=$($Answers.publicUrl)"
        "DOKPOD_ENGINE=$($Answers.engine)"
        ''
    ) -join "`n"
}

function New-CaBundle {
    <#
    .SYNOPSIS
    Raízes do sistema mais as CAs da instalação (local e as do Keycloak e do PostgreSQL existentes), para SSL_CERT_FILE da API e do BFF.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([string[]] $ExtraPem = @())

    $parts = [System.Collections.Generic.List[string]]::new()
    if (Test-Path -LiteralPath $script:SystemCaBundle) { $parts.Add([System.IO.File]::ReadAllText($script:SystemCaBundle).TrimEnd()) }
    foreach ($pem in $ExtraPem) { if ($pem) { $parts.Add($pem.TrimEnd()) } }
    return ($parts -join "`n") + "`n"
}

function Write-InstallerFiles {
    <#
    .SYNOPSIS
    Grava os arquivos não secretos em installDir e devolve os caminhos gravados.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Model,
        [string] $CaBundle,
        [string] $LocalCaPem,
        [string] $AgentsCaPem
    )

    $root = $Answers.installDir
    $files = [ordered]@{
        'compose.yaml'          = ConvertTo-ComposeYaml -Model $Model
        'dokpod.env'            = New-DokpodEnvironmentFile -Answers $Answers
        'deploy.env'            = New-DeployEnvironment -Answers $Answers -Model $Model
    }
    $edge = New-EdgeConfiguration -Answers $Answers
    foreach ($name in $edge.Keys) { $files["edge/$name"] = $edge[$name] }
    if ($CaBundle) { $files['certs/ca-bundle.crt'] = $CaBundle }
    if ($LocalCaPem) { $files['certs/ca.crt'] = $LocalCaPem }
    if ($AgentsCaPem) { $files['certs/agents-ca.crt'] = $AgentsCaPem }

    $written = [System.Collections.Generic.List[string]]::new()
    foreach ($name in $files.Keys) {
        $path = Join-Path $root $name
        $content = $files[$name]
        if ((Protect-InstallerText -Text $content) -ne $content) {
            Stop-Installer -Code Error -Message "O arquivo $name teria um valor sensível; nada foi gravado."
        }

        if ($PSCmdlet.ShouldProcess($path, 'gravar')) {
            [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $path))
            [System.IO.File]::WriteAllText($path, $content)
            $written.Add($path)
        }
    }

    return , $written.ToArray()
}

Export-ModuleMember -Function Expand-InstallerTemplate, Get-KeycloakAuthority, Get-BffTrustedNetworks, New-InstallerComposeModel, ConvertTo-ComposeYaml,
Get-ComposeServiceHashes, Set-ComposeConfigHashLabels, Test-CaBundleNeeded, New-EdgeConfiguration, New-DeployEnvironment, New-DokpodEnvironmentFile, New-CaBundle, Write-InstallerFiles
