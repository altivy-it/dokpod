#Requires -Version 7.4
<#
.SYNOPSIS
Nginx existente do usuário (spec 033 T037, FR-021, FR-043, D-008, L1): onde web e BFF escutam, em quem o BFF confia,
validação do dokpod.conf com `nginx -t` em container e os passos manuais que restam.

.DESCRIPTION
O instalador só escreve em installDir/edge. Não toca em arquivo do nginx do usuário, não o recarrega e não o reinicia:
lista os passos para o usuário fazer.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module (Join-Path $PSScriptRoot 'Render.psm1')

$script:Loopback = '127.0.0.1', '::1'
$script:NetworkPattern = '^[a-zA-Z0-9][a-zA-Z0-9_.-]*$'
# Container avulso do nginx -t.
$script:CheckLabel = 'io.dokpod.installer.task=edge-check'

function Assert-ExistingEdgeAnswers {
    <#
    .SYNOPSIS
    Com nginx existente, web e BFF só escutam em loopback ou numa rede compartilhada com o nginx (FR-043): outro valor
    de upstreamHost dá código 2 antes de qualquer efeito, e as portas do loopback precisam ser distintas.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $edge = $Answers.edge
    if ($edge.mode -ne 'existing') { return }

    $upstream = $edge.upstreamHost
    if ($upstream -is [System.Collections.IDictionary]) {
        $network = [string] $upstream['network']
        if ($network -notmatch $script:NetworkPattern) {
            Stop-Installer -Code Usage -Message "edge.upstreamHost.network '$network' não é um nome de rede válido."
        }

        return
    }

    if ([string] $upstream -notin $script:Loopback) {
        Stop-Installer -Code Usage -Message ("edge.upstreamHost '$upstream' não é loopback: web e BFF só escutam em 127.0.0.1, ::1 ou numa rede " +
            'compartilhada com o seu nginx ({ network: <nome> }), para que nenhum tráfego externo chegue ao BFF sem passar pela borda (FR-043).')
    }

    $ports = @('webPort', 'bffPort')
    if ($Answers.keycloak.mode -eq 'provision') { $ports += 'keycloakPort' }
    $values = @($ports | ForEach-Object { [int] $edge[$_] })
    if (@($values | Select-Object -Unique).Count -ne $values.Count) {
        Stop-Installer -Code Usage -Message "As portas de edge ($($ports -join ', ')) precisam ser diferentes: $($values -join ', ')."
    }
}

function Get-SharedEdgeNetworkSubnet {
    <#
    .SYNOPSIS
    As sub-redes da rede compartilhada com o nginx do usuário. A rede precisa existir (código 3) e não ser maior que /24
    (/120 no IPv6), para o BFF não confiar em X-Forwarded-* de fora dela (L1).
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)][string] $Network,
        [string] $Cli = 'docker'
    )

    $result = Invoke-InstallerProcess -FilePath $Cli -ArgumentList @('network', 'inspect', '--format', '{{json .IPAM.Config}}', $Network)
    if ($result.ExitCode -ne 0) {
        Stop-Installer -Code Prerequisite -Message ("A rede $Network de edge.upstreamHost não existe. Crie-a com sub-rede /24 e conecte o seu nginx a ela, por exemplo: " +
            "docker network create --subnet 10.253.34.0/24 $Network && docker network connect $Network <container do nginx>.")
    }

    $config = @($result.Output.Trim() | ConvertFrom-Json)
    $subnets = @($config | ForEach-Object { if ($_ -and $_.PSObject.Properties['Subnet'] -and $_.Subnet) { [string] $_.Subnet } })
    if ($subnets.Count -eq 0) {
        Stop-Installer -Code Prerequisite -Message "A rede $Network não tem sub-rede declarada; o BFF não teria em quem confiar."
    }

    foreach ($subnet in $subnets) {
        $parsed = [System.Net.IPNetwork]::Parse($subnet)
        $minimum = if ($parsed.BaseAddress.AddressFamily -eq 'InterNetworkV6') { 120 } else { 24 }
        if ($parsed.PrefixLength -lt $minimum) {
            Stop-Installer -Code Prerequisite -Message ("A rede $Network tem a sub-rede $subnet, maior que /$($minimum): o BFF confiaria em X-Forwarded-* de containers " +
                'fora da borda (L1). Use uma rede só do nginx e do Dokpod, com sub-rede /24.')
        }
    }

    return , [string[]] $subnets
}

function Get-EdgeTrustedNetworks {
    <#
    .SYNOPSIS
    As sub-redes que o BFF confia além das fixas (Get-BffTrustedNetworks): só as da rede compartilhada com o nginx do
    usuário. Em loopback e na borda provisionada, as fixas bastam.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [string] $Cli = 'docker'
    )

    if ($Answers.edge.mode -ne 'existing' -or $Answers.edge.upstreamHost -isnot [System.Collections.IDictionary]) { return , [string[]] @() }
    return Get-SharedEdgeNetworkSubnet -Network ([string] $Answers.edge.upstreamHost.network) -Cli $Cli
}

function Test-EdgeConfiguration {
    <#
    .SYNOPSIS
    `nginx -t` em container com o dokpod.conf e o dokpod.locations gerados, incluídos como o README do instalador
    manda: o primeiro no contexto http, o segundo num server {}. Erro de sintaxe dá código 1 com a saída do nginx.
    .DESCRIPTION
    O container não tem rede: os nomes da rede compartilhada resolvem para o loopback só para a validação.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Image,
        [string] $Cli = 'docker'
    )

    $edgeDirectory = Join-Path $Answers.installDir 'edge'
    $serverName = ([uri] $Answers.publicUrl).Host
    $config = "pid /tmp/nginx.pid; error_log stderr; events {} http { access_log off; client_body_temp_path /tmp/b; proxy_temp_path /tmp/p; " +
        "fastcgi_temp_path /tmp/f; uwsgi_temp_path /tmp/u; scgi_temp_path /tmp/s; include /dokpod/dokpod.conf; " +
        "server { listen 8443; server_name $serverName; include /dokpod/dokpod.locations; } }"
    $hosts = @()
    foreach ($alias in 'dokpod-web', 'dokpod-bff', 'dokpod-keycloak') { $hosts += '--add-host', "$($alias):127.0.0.1" }
    $arguments = @('run', '--rm', '--log-driver', 'none', '--network', 'none', '--label', $script:CheckLabel) + (Get-ContainerLimitArguments) + $hosts + @(
        '--read-only', '--tmpfs', '/tmp:rw,size=4m', '--volume', "$($edgeDirectory):/dokpod:ro", '--entrypoint', 'sh', $Image,
        '-c', "printf '%s' '$config' > /tmp/check.conf && exec nginx -t -c /tmp/check.conf")
    $result = Invoke-InstallerProcess -FilePath $Cli -ArgumentList $arguments
    $output = ($result.Error + $result.Output).Trim()
    if ($result.ExitCode -ne 0) {
        Stop-Installer -Code Error -Message "O nginx -t recusou a configuração gerada em $($edgeDirectory): $output"
    }

    return $output
}

function Get-ExistingEdgeSteps {
    <#
    .SYNOPSIS
    Os passos manuais no nginx do usuário (FR-021): certificado, os dois include, o alias do nome público na rede
    compartilhada (com o Keycloak provisionado) e o reload.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    if ($Answers.edge.mode -ne 'existing') { return , [string[]] @() }
    $endpoint = [uri] $Answers.publicUrl
    $port = if ($endpoint.IsDefaultPort) { 443 } else { $endpoint.Port }
    $edgeDirectory = Join-Path $Answers.installDir 'edge'
    $steps = @()
    $steps += if ($Answers.certificates.edge.source -eq 'local-ca') {
        "No nginx, use para $($endpoint.Host) o certificado $(Join-Path $Answers.secretsDir 'pki/edge.crt') e a chave $(Join-Path $Answers.secretsDir 'pki/edge.key') " +
        "(copie-os para onde o nginx lê, com acesso restrito); a CA local fica em $(Join-Path $Answers.installDir 'certs/ca.crt')."
    }
    else { "No nginx, use para $($endpoint.Host) o certificado da organização informado em certificates.edge." }
    $steps += "No contexto http do nginx: include $(Join-Path $edgeDirectory 'dokpod.conf');"
    $steps += "No server {} com TLS que atende $($endpoint.Host):$($port): include $(Join-Path $edgeDirectory 'dokpod.locations');"
    $upstream = $Answers.edge.upstreamHost
    if ($upstream -is [System.Collections.IDictionary] -and $Answers.keycloak.mode -eq 'provision') {
        $steps += ("Dê ao container do nginx o alias $($endpoint.Host) na rede $($upstream.network), para API e BFF chegarem ao Keycloak pelo nome público: " +
            "docker network disconnect $($upstream.network) <nginx> && docker network connect --alias $($endpoint.Host) $($upstream.network) <nginx>.")
    }
    elseif ($upstream -isnot [System.Collections.IDictionary] -and $Answers.keycloak.mode -eq 'provision') {
        $steps += "O nginx precisa escutar $($port) também no endereço que os containers usam para o host (host.containers.internal no Podman), não só no IP público."
    }
    $steps += 'Teste e recarregue o nginx: nginx -t && nginx -s reload (ou systemctl reload nginx). O instalador não toca nos arquivos do nginx.'
    return , [string[]] $steps
}

Export-ModuleMember -Function Assert-ExistingEdgeAnswers, Get-SharedEdgeNetworkSubnet, Get-EdgeTrustedNetworks,
Test-EdgeConfiguration, Get-ExistingEdgeSteps
