#Requires -Version 7.4
<#
.SYNOPSIS
Logo do instalador (spec 036, contracts/installer-cli.md): desenho, modos de cor e versões sem UTF-8 e para terminal
estreito. As funções de formatação recebem o ambiente como parâmetro; só Write-InstallerBanner lê o terminal real.
#>
Set-StrictMode -Version Latest

# Desenho aprovado na prévia (opção D, 6 linhas de 50 colunas), original do Dokpod.
$script:Logo = @(
    '██████╗  ██████╗ ██╗  ██╗██████╗  ██████╗ ██████╗ '
    '██╔══██╗██╔═══██╗██║ ██╔╝██╔══██╗██╔═══██╗██╔══██╗'
    '██║  ██║██║   ██║█████╔╝ ██████╔╝██║   ██║██║  ██║'
    '██║  ██║██║   ██║██╔═██╗ ██╔═══╝ ██║   ██║██║  ██║'
    '██████╔╝╚██████╔╝██║  ██╗██║     ╚██████╔╝██████╔╝'
    '╚═════╝  ╚═════╝ ╚═╝  ╚═╝╚═╝      ╚═════╝ ╚═════╝ '
)
# Prefixo de cor de cada linha por modo (D-004), em tabela fixa: o logo roda uma vez por processo, e montar as cores
# na hora custava mais do que a meta de SC-003. TrueColor é o degradê Y1, do amarelo ao lima da marca (#b7f34a); Ansi256
# é o índice do cubo 6x6x6 mais próximo de cada cor do Y1 (16 + 36r + 6g + b); Ansi16 é amarelo nas três primeiras
# linhas e verde nas três últimas.
$script:Esc = [char] 27
$script:Prefixes = @{
    TrueColor = @('255;242;122', '238;244;99', '217;244;86', '198;243;78', '183;243;74', '159;217;60') | ForEach-Object { "$($script:Esc)[38;2;$($_)m" }
    Ansi256   = @(228, 227, 191, 191, 155, 149) | ForEach-Object { "$($script:Esc)[38;5;$($_)m" }
    Ansi16    = @('93', '93', '93', '92', '92', '92') | ForEach-Object { "$($script:Esc)[$($_)m" }
}
# Sem UTF-8: o mesmo desenho, com # no lugar dos blocos e sem a sombra; a última linha, só de sombra, some (D-005).
$script:AsciiLogo = @($script:Logo | ForEach-Object { ($_ -replace '█', '#' -replace '[^#]', ' ').TrimEnd() } | Where-Object { $_ })
# Largura do desenho mais a margem (FR-005).
$script:MinimumWidth = 52

function Get-InstallerColorMode {
    <#
    .SYNOPSIS
    None, Ansi16, Ansi256 ou TrueColor pelas convenções de terminal: NO_COLOR não vazia, TERM=dumb, COLORTERM e TERM.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Environment)

    $term = [string] $Environment['TERM']
    $colorTerm = [string] $Environment['COLORTERM']
    if ($Environment['NO_COLOR'] -or $term -eq 'dumb') { return 'None' }
    if ($colorTerm -eq 'truecolor' -or $colorTerm -eq '24bit') { return 'TrueColor' }
    if ($term.Contains('256color')) { return 'Ansi256' }
    return 'Ansi16'
}

function Test-InstallerUtf8 {
    <#
    .SYNOPSIS
    Verdadeiro se o locale efetivo (LC_ALL, depois LC_CTYPE, depois LANG, como no POSIX) for UTF-8.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Environment)

    foreach ($name in 'LC_ALL', 'LC_CTYPE', 'LANG') {
        $value = ([string] $Environment[$name]).ToLowerInvariant()
        if ($value) {
            $at = $value.IndexOf('@')
            if ($at -ge 0) { $value = $value.Substring(0, $at) }
            return $value.EndsWith('.utf-8') -or $value.EndsWith('.utf8')
        }
    }
    return $false
}

function Format-InstallerBanner {
    <#
    .SYNOPSIS
    Texto do logo, sem quebra de linha final. Com largura nula ou abaixo de 52 colunas, só a linha da versão.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [AllowEmptyString()][string] $Version,
        [ValidateSet('None', 'Ansi16', 'Ansi256', 'TrueColor')][string] $ColorMode = 'None',
        [bool] $Utf8 = $true,
        [Nullable[int]] $Width
    )

    $reset = "$($script:Esc)[0m"
    $versionText = "Instalador do Dokpod $(if ($Version) { $Version } else { 'dev' }) · Altivy"
    $versionLine = if ($ColorMode -eq 'None') { $versionText } else { "$($script:Esc)[2m$versionText$reset" }
    if ($null -eq $Width -or $Width -lt $script:MinimumWidth) { return $versionLine }

    $art = if ($Utf8) { $script:Logo } else { $script:AsciiLogo }
    $prefixes = $script:Prefixes[$ColorMode]
    $builder = [System.Text.StringBuilder]::new()
    for ($i = 0; $i -lt $art.Count; $i++) {
        if ($prefixes) { [void] $builder.Append($prefixes[$i]).Append($art[$i]).Append($reset).Append("`n") }
        else { [void] $builder.Append($art[$i].TrimEnd()).Append("`n") }
    }
    return $builder.Append("`n").Append($versionLine).ToString()
}

function Write-InstallerBanner {
    <#
    .SYNOPSIS
    Escreve o logo na saída padrão, numa única escrita, conforme o terminal real (variáveis do processo e largura).
    #>
    [CmdletBinding()]
    param([AllowEmptyString()][string] $Version)

    $environment = @{}
    foreach ($name in 'NO_COLOR', 'TERM', 'COLORTERM', 'LANG', 'LC_ALL', 'LC_CTYPE') {
        $environment[$name] = [Environment]::GetEnvironmentVariable($name)
    }
    $width = $null
    try { $width = [Console]::WindowWidth } catch { $width = $null }
    if ($width -le 0) { $width = $null }

    $utf8 = Test-InstallerUtf8 -Environment $environment
    $text = Format-InstallerBanner -Version $Version -ColorMode (Get-InstallerColorMode -Environment $environment) -Utf8 $utf8 -Width $width
    $stream = [Console]::OpenStandardOutput()
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes("$text`n`n")
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush()
}

Export-ModuleMember -Function Get-InstallerColorMode, Test-InstallerUtf8, Format-InstallerBanner, Write-InstallerBanner
