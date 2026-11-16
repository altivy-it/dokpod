#Requires -Version 7.4
<#
.SYNOPSIS
Atualização de versão (spec 033 T048, FR-011, FR-032, FR-036): `upgrade --version <X.Y.Z>` só para uma versão maior, com
as imagens verificadas, as migrations e o backup do fluxo do install, o plano de controle antes dos agentes e a versão
de cada serviço conferida no fim.

.DESCRIPTION
O resto é o install com a versão nova: as etapas já sobem o plano de controle (dados, identidade, aplicação e borda)
antes do agente, e a etapa migrations exige o planHash e o backup (pg_dump no dokpod-postgres provisionado, confirmação
nomeada nos demais) quando há migration nova.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')

$script:Runbooks = 'docs/runbooks/deploy-e-rollback.md e docs/runbooks/backup-restore-e-atualizacao.md'
# Serviços com imagem do Dokpod, cuja versão vem no rótulo OCI (os de infraestrutura têm a versão deles).
$script:DokpodServices = 'api', 'bff', 'web', 'agent'

function ConvertTo-InstallerVersion {
    # X.Y.Z, com pré-release (-rc.N) ordenada antes da versão final.
    param([string] $Value)
    if ($Value -notmatch '^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$') {
        Stop-Installer -Code Usage -Message "Versão inválida: $Value. Use X.Y.Z de uma release publicada."
    }

    return [pscustomobject]@{ Core = [version] "$($Matches[1]).$($Matches[2]).$($Matches[3])"; Pre = $Matches[4] }
}

function Compare-InstallerVersion {
    <#
    .SYNOPSIS
    -1, 0 ou 1, como X.Y.Z do semver: a pré-release vem antes da final, e entre pré-releases vale a ordem do texto.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param([Parameter(Mandatory)][string] $Left, [Parameter(Mandatory)][string] $Right)

    $a = ConvertTo-InstallerVersion $Left
    $b = ConvertTo-InstallerVersion $Right
    $core = $a.Core.CompareTo($b.Core)
    if ($core -ne 0) { return [math]::Sign($core) }
    if ($a.Pre -eq $b.Pre) { return 0 }
    if (-not $a.Pre) { return 1 }
    if (-not $b.Pre) { return -1 }
    return [math]::Sign([string]::CompareOrdinal($a.Pre, $b.Pre))
}

function Assert-UpgradeVersion {
    <#
    .SYNOPSIS
    `upgrade` só vai para uma versão maior que a instalada. Igual ou menor dá código 2, apontando o rollback.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $Installed, [Parameter(Mandatory)][string] $Target)

    $order = Compare-InstallerVersion -Left $Target -Right $Installed
    if ($order -eq 0) {
        Stop-Installer -Code Usage -Message "O Dokpod já está na $Installed. Para reaplicar a mesma versão, use 'install'."
    }
    if ($order -lt 0) {
        Stop-Installer -Code Usage -Message ("A versão instalada é $Installed; $Target é anterior. O instalador não volta versão (as migrations são forward-only, FR-010): " +
            "siga o rollback de $($script:Runbooks).")
    }
}

function Assert-ServiceImageVersion {
    <#
    .SYNOPSIS
    Depois da atualização, cada serviço do Dokpod roda a imagem com `org.opencontainers.image.version` igual à versão
    pedida (FR-032). Diferença dá código 1 com o serviço e a versão encontrada.
    .OUTPUTS
    Serviço → versão encontrada.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param(
        [Parameter(Mandatory)][string] $Version,
        [Parameter(Mandatory)][string] $Project,
        [string[]] $Services = $script:DokpodServices,
        [string] $Cli = 'docker'
    )

    $found = [ordered]@{}
    $problems = @()
    foreach ($service in $Services) {
        $ids = Invoke-InstallerProcess -FilePath $Cli -ArgumentList @('ps', '--quiet', '--filter', "label=com.docker.compose.project=$Project", '--filter', "label=com.docker.compose.service=$service")
        $id = @($ids.Output -split "`n" | Where-Object { $_.Trim() }) | Select-Object -First 1
        if (-not $id) { $problems += "$service sem container"; continue }
        $image = (Invoke-InstallerProcess -FilePath $Cli -ArgumentList @('inspect', '--format', '{{.Image}}', $id.Trim())).Output.Trim()
        $label = (Invoke-InstallerProcess -FilePath $Cli -ArgumentList @('image', 'inspect', '--format', '{{index .Config.Labels "org.opencontainers.image.version"}}', $image)).Output.Trim()
        $found[$service] = $label
        if ($label -ne $Version -and $label -ne "v$Version") { $problems += "$service na $(if ($label) { $label } else { 'versão desconhecida' })" }
    }

    if ($problems) {
        Stop-Installer -Code Error -Message "A atualização para a $Version não ficou completa: $($problems -join '; '). Reexecute 'upgrade --version $Version'."
    }

    return $found
}

Export-ModuleMember -Function Compare-InstallerVersion, Assert-UpgradeVersion, Assert-ServiceImageVersion
