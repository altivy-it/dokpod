#Requires -Version 7.4
<#
.SYNOPSIS
Segredos da instalação (spec 033 T023, FR-002, FR-028, FR-033, D-006): geração, leitura de referências e gravação em
secretsDir, sempre redigidos na saída.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')

$script:DirectoryMode = [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute'
$script:FileMode = [System.IO.UnixFileMode]'UserRead, UserWrite'

function New-InstallerSecret {
    <#
    .SYNOPSIS
    Gera um segredo com a entropia do sistema, em base64url sem padding, e o registra para redação.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([ValidateRange(16, 128)][int] $Bytes = 32)

    $value = [System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes($Bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    Register-InstallerSecret -Value $value
    return $value
}

function Read-SecretReference {
    <#
    .SYNOPSIS
    Lê um segredo referenciado no arquivo de respostas ({ file } ou { env }), sem ecoar, e o registra para redação.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Reference,
        [Parameter(Mandatory)][string] $Purpose
    )

    if ($Reference.Contains('file')) {
        $path = [string] $Reference['file']
        if (-not [System.IO.Path]::IsPathFullyQualified($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            Stop-Installer -Code Usage -Message "O arquivo do segredo de $Purpose não existe: $path."
        }

        $value = [System.IO.File]::ReadAllText($path).TrimEnd("`r", "`n")
    }
    elseif ($Reference.Contains('env')) {
        $name = [string] $Reference['env']
        $value = [System.Environment]::GetEnvironmentVariable($name)
        if ($null -eq $value) {
            Stop-Installer -Code Usage -Message "A variável $name do segredo de $Purpose não chegou ao instalador; o install.sh repassa só as referenciadas em { env }."
        }
    }
    else {
        Stop-Installer -Code Usage -Message "O segredo de $Purpose precisa ser { file } ou { env }."
    }

    if ([string]::IsNullOrEmpty($value)) {
        Stop-Installer -Code Usage -Message "O segredo de $Purpose está vazio."
    }

    Register-InstallerSecret -Value $value
    return $value
}

function Initialize-SecretsDirectory {
    <#
    .SYNOPSIS
    Cria secretsDir (e subpastas) com modo 0700, ou corrige o modo de uma existente.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        [void] [System.IO.Directory]::CreateDirectory($Path, $script:DirectoryMode)
    }

    if (-not $IsWindows) {
        [System.IO.File]::SetUnixFileMode($Path, $script:DirectoryMode)
    }
}

function Test-InstallerSecret {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][string] $SecretsDirectory,
        [Parameter(Mandatory)][string] $Name
    )

    return Test-Path -LiteralPath (Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name $Name) -PathType Leaf
}

function Write-InstallerSecret {
    <#
    .SYNOPSIS
    Grava um segredo em secretsDir com modo 0600. Destino existente só é substituído com -Recreate (FR-028).
    #>
    [CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'Text')]
    param(
        [Parameter(Mandatory)][string] $SecretsDirectory,
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory, ParameterSetName = 'Text')][string] $Value,
        [Parameter(Mandatory, ParameterSetName = 'Bytes')][byte[]] $Bytes,
        [switch] $Recreate
    )

    $path = Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name $Name
    if ((Test-Path -LiteralPath $path) -and -not $Recreate) {
        Stop-Installer -Code Declined -Message "O segredo '$Name' já existe; ele só é substituído com 'recreate secret:$Name'."
    }

    if (-not $PSCmdlet.ShouldProcess($Name, 'gravar segredo')) {
        return
    }

    Initialize-SecretsDirectory -Path (Split-Path -Parent $path)
    if ($PSCmdlet.ParameterSetName -eq 'Text') {
        Register-InstallerSecret -Value $Value
        $Bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    }

    $temporary = "$path.tmp-$([System.Guid]::NewGuid().ToString('N'))"
    $options = [System.IO.FileStreamOptions]@{ Mode = 'CreateNew'; Access = 'Write'; Share = 'None' }
    if (-not $IsWindows) {
        $options.UnixCreateMode = $script:FileMode
    }

    $stream = [System.IO.FileStream]::new($temporary, $options)
    try {
        $stream.Write($Bytes, 0, $Bytes.Length)
    }
    finally {
        $stream.Dispose()
    }

    [System.IO.File]::Move($temporary, $path, $true)
}

function Read-InstallerSecret {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string] $SecretsDirectory,
        [Parameter(Mandatory)][string] $Name
    )

    $path = Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Stop-Installer -Code Error -Message "O segredo '$Name' não existe em secretsDir."
    }

    $value = [System.IO.File]::ReadAllText($path)
    Register-InstallerSecret -Value $value
    return $value
}

function Resolve-SecretPath {
    <#
    .SYNOPSIS
    Caminho de um segredo dentro de secretsDir; nome com '..', absoluto ou fora da pasta é recusado.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string] $SecretsDirectory,
        [Parameter(Mandatory)][string] $Name
    )

    if ($Name -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*(/[A-Za-z0-9][A-Za-z0-9_.-]*)*$' -or $Name -match '\.\.') {
        Stop-Installer -Code Usage -Message "Nome de segredo inválido: $Name."
    }

    $root = [System.IO.Path]::GetFullPath($SecretsDirectory)
    $path = [System.IO.Path]::GetFullPath((Join-Path $root $Name))
    if (-not $path.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::Ordinal)) {
        Stop-Installer -Code Usage -Message "O segredo $Name sairia de secretsDir."
    }

    return $path
}

Export-ModuleMember -Function New-InstallerSecret, Read-SecretReference, Initialize-SecretsDirectory, Test-InstallerSecret,
Write-InstallerSecret, Read-InstallerSecret, Resolve-SecretPath
