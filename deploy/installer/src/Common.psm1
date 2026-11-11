#Requires -Version 7.4
<#
.SYNOPSIS
Base do instalador do Dokpod (spec 033): códigos de saída, erro do instalador, redação de segredos e bibliotecas .NET.
#>
Set-StrictMode -Version Latest

# Contrato: specs/033-instalador-guiado/contracts/installer-cli.md (FR-035).
$script:ExitCodes = [ordered]@{
    Success            = 0
    Error              = 1
    Usage              = 2
    Prerequisite       = 3
    Declined           = 4
    Signature          = 5
    MigrationDivergence = 6
    Locked             = 7
    PausedForMigration = 10
}

# Valores sensíveis carregados nesta execução; toda saída passa por Protect-InstallerText (FR-033).
$script:SensitiveValues = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

class InstallerException : System.Exception {
    [int] $ExitCode
    InstallerException([int] $exitCode, [string] $message) : base($message) {
        $this.ExitCode = $exitCode
    }
}

function Get-InstallerExitCode {
    [CmdletBinding()]
    [OutputType([int])]
    param([Parameter(Mandatory)][ValidateScript({ $script:ExitCodes.Contains($_) })][string] $Name)

    return $script:ExitCodes[$Name]
}

function Stop-Installer {
    <#
    .SYNOPSIS
    Interrompe com um código do contrato e uma mensagem sem segredos.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateScript({ $script:ExitCodes.Contains($_) })][string] $Code,
        [Parameter(Mandatory)][string] $Message
    )

    throw [InstallerException]::new($script:ExitCodes[$Code], (Protect-InstallerText -Text $Message))
}

function Register-InstallerSecret {
    <#
    .SYNOPSIS
    Registra um valor sensível para que nunca apareça na saída, no relatório ou no estado.
    #>
    [CmdletBinding()]
    param([AllowEmptyString()][string] $Value)

    if (-not [string]::IsNullOrEmpty($Value) -and $Value.Length -ge 4) {
        [void] $script:SensitiveValues.Add($Value)
    }
}

function Clear-InstallerSecrets {
    [CmdletBinding()]
    param()

    $script:SensitiveValues.Clear()
}

function Protect-InstallerText {
    <#
    .SYNOPSIS
    Substitui por *** cada valor sensível registrado, do mais longo para o mais curto.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([AllowEmptyString()][AllowNull()][string] $Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return $Text
    }

    $result = $Text
    foreach ($value in ($script:SensitiveValues | Sort-Object -Property Length -Descending)) {
        $result = $result.Replace($value, '***', [System.StringComparison]::Ordinal)
    }

    return $result
}

function Write-InstallerLine {
    <#
    .SYNOPSIS
    Escreve uma linha para o operador, sempre redigida. -ToStandardError vai para stderr.
    #>
    [CmdletBinding()]
    param(
        [AllowEmptyString()][string] $Message = '',
        [switch] $ToStandardError
    )

    $text = Protect-InstallerText -Text $Message
    if ($ToStandardError) {
        [Console]::Error.WriteLine($text)
    }
    else {
        [Console]::Out.WriteLine($text)
    }
}

function Get-InstallerHome {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    if ($env:DOKPOD_INSTALLER_HOME) {
        return $env:DOKPOD_INSTALLER_HOME
    }

    return (Split-Path -Parent $PSScriptRoot)
}

function Import-InstallerLibraries {
    <#
    .SYNOPSIS
    Carrega as DLLs publicadas pelo estágio de build (Microsoft.Data.Sqlite, SQLitePCLRaw, YamlDotNet), uma vez.
    #>
    [CmdletBinding()]
    param([string] $Directory = (Join-Path (Get-InstallerHome) 'lib'))

    if ('Microsoft.Data.Sqlite.SqliteConnection' -as [type]) {
        return
    }

    if (-not (Test-Path -LiteralPath $Directory)) {
        Stop-Installer -Code Error -Message "Bibliotecas do instalador ausentes em $Directory; use a imagem dokpod-installer."
    }

    foreach ($name in 'SQLitePCLRaw.core', 'SQLitePCLRaw.provider.e_sqlite3', 'SQLitePCLRaw.batteries_v2', 'Microsoft.Data.Sqlite', 'YamlDotNet') {
        $path = Join-Path $Directory "$name.dll"
        if (Test-Path -LiteralPath $path) {
            Add-Type -LiteralPath $path
        }
    }

    [SQLitePCL.Batteries_V2]::Init()
}

function Get-Sha256Hex {
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Text)

    $bytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($Text))
    return [System.Convert]::ToHexStringLower($bytes)
}

function Test-InteractiveSession {
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    return -not [Console]::IsInputRedirected -and -not [Console]::IsOutputRedirected -and -not $env:DOKPOD_NON_INTERACTIVE
}

Export-ModuleMember -Function Get-InstallerExitCode, Stop-Installer, Register-InstallerSecret, Clear-InstallerSecrets,
Protect-InstallerText, Write-InstallerLine, Get-InstallerHome, Import-InstallerLibraries, Get-Sha256Hex, Test-InteractiveSession
