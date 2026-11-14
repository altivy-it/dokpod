#Requires -Version 7.4
<#
.SYNOPSIS
Pré-requisitos do instalador (spec 033 T021, FR-003, D-015): engine rootless e versões mínimas, portas, disco, nome
público e serviços existentes. Nada é alterado; qualquer falha dá código 3.
.DESCRIPTION
Os fatos do engine vêm da API do socket montado (docker version/info). Os fatos do host, que o container não enxerga
por estar em outro namespace de rede, vêm do install.sh em variáveis DOKPOD_HOST_*:
  DOKPOD_HOST_UNPRIVILEGED_PORT_START  valor de net.ipv4.ip_unprivileged_port_start
  DOKPOD_HOST_LINGER                    yes ou no (loginctl show-user)
  DOKPOD_HOST_LISTENING_PORTS           portas TCP em uso, separadas por vírgula
  DOKPOD_HOST_FREE_BYTES                espaço livre na pasta de instalação
  DOKPOD_HOST_PORT_DRIVER               driver de portas do Docker rootless, quando conhecido
  DOKPOD_HOST_PUBLIC_NAME e DOKPOD_HOST_PUBLIC_ADDRESSES
                                        o nome público das respostas e os endereços dele no host (getent),
                                        porque o Docker não copia o /etc/hosts do host para o container
Fato ausente vira aviso, nunca aprovação silenciosa.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')

$script:MinimumDocker = [version] '27.0.0'
$script:MinimumPodman = [version] '5.0.0'
$script:MinimumCompose = [version] '2.0.0'
$script:MinimumFreeBytes = 5GB

function Invoke-EngineCli {
    # Docker CLI contra o socket montado (DOCKER_HOST); devolve a saída ou $null quando falha.
    param([string[]] $Arguments)

    $output = & docker @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return ($output -join "`n")
}

function Get-EngineFacts {
    <#
    .SYNOPSIS
    Tipo, versão e modo do engine pela API do socket, não pelo caminho (D-015).
    #>
    [CmdletBinding()]
    param([scriptblock] $Cli = { param([string[]] $arguments) Invoke-EngineCli -Arguments $arguments })

    $version = & $Cli @('version', '--format', '{{json .}}')
    $info = & $Cli @('info', '--format', '{{json .}}')
    $compose = & $Cli @('compose', 'version', '--short')
    if (-not $version -or -not $info) {
        return [pscustomobject]@{ Reachable = $false; Kind = $null; Version = $null; Rootless = $false; ComposeVersion = $null }
    }

    $versionJson = $version | ConvertFrom-Json -AsHashtable
    $infoJson = $info | ConvertFrom-Json -AsHashtable
    $components = @($versionJson.Server?.Components ?? @())
    $podman = $components | Where-Object { $_.Name -eq 'Podman Engine' } | Select-Object -First 1
    $kind = if ($podman) { 'podman' } else { 'docker' }
    $engineVersion = if ($podman) { $podman.Version } else { $versionJson.Server?.Version ?? $infoJson.ServerVersion }
    $security = @($infoJson.SecurityOptions ?? @())
    return [pscustomobject]@{
        Reachable      = $true
        Kind           = $kind
        Version        = ConvertTo-PlainVersion $engineVersion
        Rootless       = [bool] ($security | Where-Object { $_ -match '(^|,)name=rootless(,|$)' })
        ComposeVersion = ConvertTo-PlainVersion ($compose ?? '')
    }
}

function ConvertTo-PlainVersion {
    param([string] $Text)

    if ($Text -match '(\d+)\.(\d+)(?:\.(\d+))?') {
        return [version] ('{0}.{1}.{2}' -f $Matches[1], $Matches[2], ($Matches[3] ?? '0'))
    }

    return $null
}

function Get-HostFacts {
    [CmdletBinding()]
    param()

    $ports = if ($env:DOKPOD_HOST_LISTENING_PORTS) { @($env:DOKPOD_HOST_LISTENING_PORTS -split ',' | Where-Object { $_ } | ForEach-Object { [int] $_ }) } else { $null }
    return [pscustomobject]@{
        UnprivilegedPortStart = if ($env:DOKPOD_HOST_UNPRIVILEGED_PORT_START) { [int] $env:DOKPOD_HOST_UNPRIVILEGED_PORT_START } else { $null }
        Linger                = $env:DOKPOD_HOST_LINGER
        ListeningPorts        = $ports
        FreeBytes             = if ($env:DOKPOD_HOST_FREE_BYTES) { [long] $env:DOKPOD_HOST_FREE_BYTES } else { $null }
        PortDriver            = $env:DOKPOD_HOST_PORT_DRIVER
        PublicName            = $env:DOKPOD_HOST_PUBLIC_NAME
        PublicAddresses       = if ($env:DOKPOD_HOST_PUBLIC_ADDRESSES) { @($env:DOKPOD_HOST_PUBLIC_ADDRESSES -split ',' | Where-Object { $_ }) } else { $null }
    }
}

function Test-InstallerPrerequisites {
    <#
    .SYNOPSIS
    Resultado de cada verificação: ok, warning ou failed, com a correção sugerida.
    .PARAMETER OwnedPorts
    Portas já publicadas por esta instalação (reexecução), que não contam como ocupadas.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $Engine,
        [Parameter(Mandatory)] $HostFacts,
        [int[]] $OwnedPorts = @(),
        [scriptblock] $Resolve = { param($name) [System.Net.Dns]::GetHostAddresses($name) },
        [scriptblock] $Probe = { param($hostName, $port) Test-TcpPort -HostName $hostName -Port $port }
    )

    $results = [System.Collections.Generic.List[object]]::new()
    $add = { param($check, $status, $message) $results.Add([pscustomobject]@{ Check = $check; Status = $status; Message = $message }) }
    $wantedKind = ($Answers.engine -split '-')[0]

    if (-not $Engine.Reachable) {
        & $add 'engine' 'failed' 'O socket do engine não responde; o install.sh monta o socket rootless do usuário ($XDG_RUNTIME_DIR).'
        return , $results.ToArray()
    }

    if ($Engine.Kind -ne $wantedKind) {
        & $add 'engine' 'failed' "O socket é do $($Engine.Kind), mas as respostas pedem $($Answers.engine)."
    }
    elseif (-not $Engine.Rootless) {
        & $add 'engine' 'failed' "O $($Engine.Kind) está em modo rootful, que o instalador não aceita (D-015). Use o modo rootless: $(if ($Engine.Kind -eq 'docker') { 'dockerd-rootless-setuptool.sh install' } else { 'podman como usuário comum, com systemctl --user enable --now podman.socket' })."
    }
    else {
        $minimum = if ($Engine.Kind -eq 'podman') { $script:MinimumPodman } else { $script:MinimumDocker }
        if (-not $Engine.Version -or $Engine.Version -lt $minimum) {
            & $add 'engine' 'failed' "O $($Engine.Kind) $($Engine.Version) está abaixo do mínimo $minimum."
        }
        else {
            & $add 'engine' 'ok' "$($Engine.Kind) $($Engine.Version) rootless."
        }
    }

    if (-not $Engine.ComposeVersion -or $Engine.ComposeVersion -lt $script:MinimumCompose) {
        & $add 'compose' 'failed' 'Docker Compose v2 não respondeu pelo socket.'
    }
    else {
        & $add 'compose' 'ok' "Compose $($Engine.ComposeVersion)."
    }

    $ports = [System.Collections.Generic.List[object]]::new()
    if ($Answers.edge.mode -eq 'provision') { $ports.Add(@{ Name = 'edge.httpsPort'; Port = [int] $Answers.edge.httpsPort }) }
    if ($Answers.Contains('agentGrpcPort') -and $Answers.agentGrpcPort) { $ports.Add(@{ Name = 'agentGrpcPort'; Port = [int] $Answers.agentGrpcPort }) }
    foreach ($entry in $ports) {
        if ($null -eq $HostFacts.UnprivilegedPortStart) {
            & $add "port:$($entry.Name)" 'warning' "Não foi possível ler net.ipv4.ip_unprivileged_port_start do host; a porta $($entry.Port) pode ser recusada pelo modo rootless."
        }
        elseif ($entry.Port -lt $HostFacts.UnprivilegedPortStart) {
            & $add "port:$($entry.Name)" 'failed' ("A porta $($entry.Port) está abaixo de $($HostFacts.UnprivilegedPortStart), que o modo rootless não publica. Saídas: " +
                "(1) use outra porta, por exemplo 8443; (2) use um nginx do host na 443 (edge.mode: existing); " +
                "(3) o administrador do host baixa o limite (sysctl net.ipv4.ip_unprivileged_port_start=$($entry.Port)). O instalador não altera o sysctl.")
        }
        elseif ($null -ne $HostFacts.ListeningPorts -and $entry.Port -in $HostFacts.ListeningPorts -and $entry.Port -notin $OwnedPorts) {
            & $add "port:$($entry.Name)" 'failed' "A porta $($entry.Port) já está em uso no host; escolha outra."
        }
        elseif ($null -eq $HostFacts.ListeningPorts) {
            & $add "port:$($entry.Name)" 'warning' "Não foi possível conferir se a porta $($entry.Port) está livre no host."
        }
        else {
            & $add "port:$($entry.Name)" 'ok' "Porta $($entry.Port) livre."
        }
    }

    if ($null -eq $HostFacts.FreeBytes) {
        & $add 'disk' 'warning' 'Não foi possível medir o espaço livre da pasta de instalação.'
    }
    elseif ($HostFacts.FreeBytes -lt $script:MinimumFreeBytes) {
        & $add 'disk' 'failed' ("Há {0:N1} GiB livres; são necessários pelo menos {1:N0} GiB." -f ($HostFacts.FreeBytes / 1GB), ($script:MinimumFreeBytes / 1GB))
    }
    else {
        & $add 'disk' 'ok' ("{0:N1} GiB livres." -f ($HostFacts.FreeBytes / 1GB))
    }

    $publicHost = ([uri] $Answers.publicUrl).Host
    $hostView = $HostFacts.PSObject.Properties['PublicName'] -and $HostFacts.PublicName -eq $publicHost -and $HostFacts.PublicAddresses
    try {
        # A resolução do host vale mais que a do container: é o host (e o navegador) que chega à borda.
        $addresses = @(if ($hostView) { $HostFacts.PublicAddresses } else { & $Resolve $publicHost })
        if ($addresses.Count -eq 0) { throw 'sem endereço' }
        & $add 'dns' 'ok' "$publicHost resolve para $($addresses -join ', ')."
    }
    catch {
        & $add 'dns' 'failed' "O nome público $publicHost não resolve; ajuste o DNS ou o /etc/hosts antes de instalar."
    }

    if ($Answers.postgres.mode -ne 'provision') {
        if (& $Probe $Answers.postgres.host ([int] $Answers.postgres.port)) {
            & $add 'postgres' 'ok' "PostgreSQL em $($Answers.postgres.host):$($Answers.postgres.port) alcançável."
        }
        else {
            & $add 'postgres' 'failed' "O PostgreSQL em $($Answers.postgres.host):$($Answers.postgres.port) não responde."
        }
    }

    if ($Answers.keycloak.mode -eq 'existing') {
        $keycloak = [uri] $Answers.keycloak.url
        if (& $Probe $keycloak.Host $keycloak.Port) {
            & $add 'keycloak' 'ok' "Keycloak em $($keycloak.Authority) alcançável."
        }
        else {
            & $add 'keycloak' 'failed' "O Keycloak em $($keycloak.Authority) não responde."
        }
    }

    switch ($HostFacts.Linger) {
        'yes' { & $add 'linger' 'ok' 'Linger ativo: os containers voltam depois do reboot.' }
        'no' { & $add 'linger' 'warning' 'Sem linger, os containers não voltam depois do reboot. O administrador do host executa: loginctl enable-linger <usuário>.' }
        default { & $add 'linger' 'warning' 'Não foi possível conferir o linger do usuário.' }
    }

    if ($Engine.Kind -eq 'podman') {
        & $add 'source-ip' 'ok' 'Podman rootless (pasta) preserva o IP de origem do cliente.'
    }
    elseif ($HostFacts.PortDriver -eq 'slirp4netns') {
        & $add 'source-ip' 'ok' 'Docker rootless com slirp4netns preserva o IP de origem.'
    }
    else {
        & $add 'source-ip' 'warning' 'O driver de portas do Docker rootless (builtin) troca o IP do cliente pelo do encaminhador, e a auditoria registraria esse IP. Use DOCKERD_ROOTLESS_ROOTLESSKIT_PORT_DRIVER=slirp4netns ou um nginx do host.'
    }

    return , $results.ToArray()
}

function Test-TcpPort {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][string] $HostName,
        [Parameter(Mandatory)][int] $Port,
        [int] $TimeoutMilliseconds = 3000
    )

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        return $client.ConnectAsync($HostName, $Port).Wait($TimeoutMilliseconds) -and $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Assert-InstallerPrerequisites {
    <#
    .SYNOPSIS
    Mostra o resultado e interrompe com código 3, sem alterar nada, se alguma verificação falhou.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] $Results)

    foreach ($result in $Results) {
        $mark = switch ($result.Status) { 'ok' { 'ok   ' } 'warning' { 'aviso' } default { 'FALHA' } }
        Write-InstallerLine -Message "  [$mark] $($result.Check): $($result.Message)"
    }

    $failed = @($Results | Where-Object Status -eq 'failed')
    if ($failed.Count -gt 0) {
        Stop-Installer -Code Prerequisite -Message "$($failed.Count) pré-requisito(s) não atendido(s); nada foi alterado."
    }
}

Export-ModuleMember -Function Get-EngineFacts, Get-HostFacts, Test-InstallerPrerequisites, Test-TcpPort, Assert-InstallerPrerequisites
