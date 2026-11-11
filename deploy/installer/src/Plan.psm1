#Requires -Version 7.4
<#
.SYNOPSIS
Motor de plano do instalador (spec 033 T020, FR-004, FR-026, FR-027, FR-029, D-007): compara o desejado com o observado
e decide create, update, keep, recreate ou remove para cada componente, com motivo e efeito colateral. O plano é sempre
recalculado do que está rodando; o estado só ajuda a lembrar o que foi aplicado.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')

$script:Services = 'dokpod-postgres', 'keycloak-postgres', 'keycloak', 'edge', 'web', 'bff', 'api', 'agent'
$script:Logical = 'secrets', 'pki', 'realm', 'migrations'
$script:WithData = 'dokpod-postgres', 'keycloak-postgres'
$script:SideEffects = @{
    'dokpod-postgres'   = 'o console e a API ficam indisponíveis durante a troca'
    'keycloak-postgres' = 'o login fica indisponível durante a troca'
    'keycloak'          = 'o login fica indisponível por alguns segundos'
    'edge'              = 'o console fica indisponível por alguns segundos'
    'bff'               = 'as sessões abertas no console precisam entrar de novo'
    'api'               = 'os agentes reconectam'
    'agent'             = 'o host local fica sem agente por alguns segundos'
    'pki'               = 'os serviços que usam o certificado são recriados; trocar a CA exige reemitir os agentes'
    'secrets'           = 'os serviços que usam o segredo são recriados'
    'realm'             = 'as mudanças no realm valem no próximo login'
    'migrations'        = 'as migrations exigem backup e confirmação própria do planHash'
}

function Get-DesiredComponents {
    <#
    .SYNOPSIS
    Componentes desejados pelas respostas: origem (provisionado ou do usuário) e hash da configuração dos serviços.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][System.Collections.IDictionary] $ServiceHashes
    )

    $existing = @()
    if ($Answers.postgres.mode -ne 'provision') { $existing += 'dokpod-postgres' }
    if ($Answers.keycloak.mode -eq 'existing') { $existing += 'keycloak', 'keycloak-postgres' }
    if ($Answers.edge.mode -eq 'existing') { $existing += 'edge' }

    $desired = [System.Collections.Generic.List[object]]::new()
    foreach ($name in $script:Logical + $script:Services) {
        if ($name -eq 'keycloak-postgres' -and $Answers.keycloak.mode -eq 'existing') { continue }
        $origin = if ($name -in $existing) { 'existing' } else { 'provisioned' }
        $hash = if ($ServiceHashes.Contains($name)) { $ServiceHashes[$name] } else { $null }
        if ($origin -eq 'provisioned' -and $name -in $script:Services -and -not $hash) {
            Stop-Installer -Code Error -Message "O serviço $name não está no compose.yaml gerado."
        }

        $desired.Add([pscustomobject]@{ Name = $name; Origin = $origin; Hash = $hash })
    }

    return , $desired.ToArray()
}

function New-InstallerPlan {
    <#
    .SYNOPSIS
    Plano por componente. Observed: Name -> @{ Exists; Hash; Running; Pending } (Pending para realm e migrations).
    .PARAMETER Recreate
    Componentes cuja recriação foi pedida.
    .PARAMETER RecreateAll
    Recriação total. Componentes com dados só saem com o nome em -ConfirmDataRemoval.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]] $Desired,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Observed,
        [string[]] $Recreate = @(),
        [switch] $RecreateAll,
        [string[]] $ConfirmDataRemoval = @()
    )

    $Recreate = @($Recreate | Where-Object { $_ })
    $unknown = @($Recreate | Where-Object { $_ -notin $Desired.Name })
    if ($unknown.Count -gt 0) {
        Stop-Installer -Code Usage -Message "Componente desconhecido para recriar: $($unknown -join ', ')."
    }

    $actions = [System.Collections.Generic.List[object]]::new()
    foreach ($component in $Desired) {
        $seen = if ($Observed.Contains($component.Name)) { $Observed[$component.Name] } else { @{ Exists = $false } }
        $wantsRecreate = $RecreateAll -or $component.Name -in $Recreate
        $action, $reason = Resolve-ComponentAction -Component $component -Seen $seen -WantsRecreate:$wantsRecreate -RecreateAll:$RecreateAll -ConfirmDataRemoval $ConfirmDataRemoval
        $actions.Add([pscustomobject]@{
                Component  = $component.Name
                Origin     = $component.Origin
                Action     = $action
                Reason     = $reason
                SideEffect = $(if ($action -in 'keep') { $null } else { $script:SideEffects[$component.Name] })
                Hash       = $component.Hash
            })
    }

    return , $actions.ToArray()
}

function Resolve-ComponentAction {
    param($Component, $Seen, [switch] $WantsRecreate, [switch] $RecreateAll, [string[]] $ConfirmDataRemoval)

    if ($Component.Origin -eq 'existing') {
        if ($WantsRecreate -and -not $RecreateAll) {
            Stop-Installer -Code Usage -Message "$($Component.Name) é do usuário; o instalador nunca o recria nem remove."
        }

        return 'keep', 'componente do usuário (existing): nunca recriado nem removido'
    }

    if ($Component.Name -in $script:WithData -and $RecreateAll) {
        if ($Component.Name -in $ConfirmDataRemoval) {
            return 'remove', 'recriação total com remoção dos dados confirmada pelo nome'
        }

        return 'keep', 'dados preservados: a remoção exige a segunda confirmação com o nome'
    }

    if (-not $Seen.Exists) {
        return 'create', 'ainda não existe'
    }

    if ($WantsRecreate) {
        return 'recreate', $(if ($RecreateAll) { 'recriação total pedida' } else { 'recriação pedida' })
    }

    if ($Component.Name -in $script:Logical) {
        if ($Seen.Contains('Pending') -and $Seen.Pending) { return 'update', [string] $Seen.Pending }
        return 'keep', 'convergido'
    }

    if ($Seen.Hash -ne $Component.Hash) {
        return 'update', 'a configuração ou a imagem mudou'
    }

    if ($Seen.Contains('Running') -and -not $Seen.Running) {
        return 'update', 'o container existe, mas não está rodando'
    }

    return 'keep', 'configuração e imagem iguais'
}

function Test-InstallerPlanConverged {
    [CmdletBinding()]
    [OutputType([bool])]
    param([Parameter(Mandatory)][object[]] $Plan)

    return -not ($Plan | Where-Object Action -ne 'keep')
}

function Format-InstallerPlan {
    <#
    .SYNOPSIS
    Tabela do plano para o operador, com o resumo dos efeitos colaterais.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][object[]] $Plan)

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add(('{0,-18} {1,-9} {2}' -f 'Componente', 'Ação', 'Motivo'))
    foreach ($item in $Plan) {
        $lines.Add(('{0,-18} {1,-9} {2}' -f $item.Component, $item.Action, $item.Reason))
    }

    $effects = @($Plan | Where-Object SideEffect | ForEach-Object { "  - $($_.Component): $($_.SideEffect)" })
    if ($effects.Count -gt 0) {
        $lines.Add('')
        $lines.Add('Efeitos colaterais:')
        $effects | ForEach-Object { $lines.Add($_) }
    }

    return Protect-InstallerText -Text ($lines -join "`n")
}

function Get-ObservedServices {
    <#
    .SYNOPSIS
    Serviços do projeto compose que estão no engine, com o hash de configuração gravado no rótulo pelo instalador.
    Container do projeto sem esse rótulo é de outra stack com o mesmo nome de projeto (a de laboratório, por exemplo):
    nunca é tratado como da instalação e bloqueia com código 3, sem ser tocado.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param([scriptblock] $Cli = { param([string[]] $arguments) $output = & docker @arguments 2>$null; if ($LASTEXITCODE -eq 0) { $output -join "`n" } })

    $observed = [ordered]@{}
    $foreign = [System.Collections.Generic.List[string]]::new()
    $json = & $Cli @('ps', '--all', '--filter', 'label=com.docker.compose.project=dokpod', '--format', '{{json .}}')
    foreach ($line in @(($json ?? '') -split "`n" | Where-Object { $_.Trim() })) {
        $container = $line | ConvertFrom-Json -AsHashtable
        $labels = @{}
        foreach ($pair in ([string] $container.Labels -split ',')) {
            $key, $value = $pair -split '=', 2
            if ($key) { $labels[$key] = $value }
        }

        $service = $labels['com.docker.compose.service']
        if (-not $service) { continue }
        if (-not $labels.Contains('io.dokpod.installer.config-hash')) {
            $foreign.Add([string] $container.Names)
            continue
        }

        $observed[$service] = @{
            Exists  = $true
            Hash    = $labels['io.dokpod.installer.config-hash']
            Running = [string] $container.State -eq 'running'
        }
    }

    if ($foreign.Count -gt 0) {
        Stop-Installer -Code Prerequisite -Message ("O projeto compose 'dokpod' já tem containers que não são deste instalador: $($foreign -join ', '). " +
            'Remova ou renomeie essa stack (por exemplo, a de laboratório em deploy/e2e) antes de instalar; nada foi alterado.')
    }

    return $observed
}

Export-ModuleMember -Function Get-DesiredComponents, New-InstallerPlan, Test-InstallerPlanConverged, Format-InstallerPlan, Get-ObservedServices
