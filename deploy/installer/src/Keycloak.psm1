#Requires -Version 7.4
<#
.SYNOPSIS
Keycloak provisionado (spec 033 T029, FR-012, FR-013, FR-014, FR-033, L3): segredos, reconciliação do realm pelo
configure-keycloak.ps1 e remoção do administrador de bootstrap.

.DESCRIPTION
A reconciliação roda num container avulso da imagem do instalador, só na rede interna do Keycloak, com raiz somente
leitura e sem socket do engine (Keycloak.Sidecar.ps1). Os segredos e os scripts entram por stdin num tar extraído para
um tmpfs; nada sensível passa por argumento ou variável de ambiente do container.

O administrador de bootstrap (`dokpod-bootstrap` no master) só existe enquanto `keycloak-bootstrap-admin` tem conteúdo:
o compose só o repassa ao Keycloak com o arquivo não vazio. Depois que o administrador nominal existe, a conta é
removida e o arquivo fica vazio. Se a remoção falhar, a execução falha e a reexecução tenta de novo (L3).
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module (Join-Path $PSScriptRoot 'Secrets.psm1')

$script:BootstrapUsername = 'dokpod-bootstrap'
$script:BootstrapSecret = 'keycloak-bootstrap-admin'
$script:BffSecret = 'Authentication__Keycloak__ClientSecret'
$script:ProvisionerSecret = 'keycloak-provisioner-client-secret'
$script:InitialPasswordSecret = 'initial-admin-password'
$script:Network = 'dokpod_keycloak-data'
$script:InternalUrl = 'http://keycloak:8080'
$script:SidecarRoot = '/run/dokpod-kc'
$script:ResultMarker = '@@dokpod-result@@'
$script:BootstrapRemovalFailed = 20

function Get-KeycloakDefaults {
    <#
    .SYNOPSIS
    Nomes fixos do Keycloak provisionado: segredos, rede interna, URL interna e o administrador de bootstrap.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param()

    return [ordered]@{
        BootstrapUsername     = $script:BootstrapUsername
        BootstrapSecret       = $script:BootstrapSecret
        BffSecret             = $script:BffSecret
        ProvisionerSecret     = $script:ProvisionerSecret
        InitialPasswordSecret = $script:InitialPasswordSecret
        Network               = $script:Network
        InternalUrl           = $script:InternalUrl
    }
}

function Initialize-KeycloakSecret {
    <#
    .SYNOPSIS
    Gera os segredos do Keycloak provisionado que faltam: senha do administrador de bootstrap, client secret do BFF e do
    dokpod-provisioner (FR-028). Um keycloak-bootstrap-admin vazio quer dizer bootstrap concluído e continua vazio.
    .OUTPUTS
    Os nomes dos segredos criados nesta execução.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    if ($Answers.keycloak.mode -ne 'provision') {
        Stop-Installer -Code Error -Message "keycloak.mode '$($Answers.keycloak.mode)' não é o provisionado."
    }

    $created = @()
    foreach ($name in $script:BootstrapSecret, $script:BffSecret, $script:ProvisionerSecret) {
        if (-not (Test-InstallerSecret -SecretsDirectory $Answers.secretsDir -Name $name)) {
            Write-InstallerSecret -SecretsDirectory $Answers.secretsDir -Name $name -Value (New-InstallerSecret)
            $created += $name
        }
    }

    return , $created
}

function Read-OptionalSecret {
    param([string] $SecretsDirectory, [string] $Name)

    if (-not (Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $Name)) { return $null }
    $value = [System.IO.File]::ReadAllText((Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name $Name))
    if ($value) { Register-InstallerSecret -Value $value }
    return $value
}

function New-InitialAdminPassword {
    <#
    .SYNOPSIS
    Senha temporária do primeiro administrador: 24 caracteres com maiúscula, minúscula, dígito e símbolo, acima da
    política do realm (length(14)).
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $characters = [char[]]::new(20)
    for ($i = 0; $i -lt $characters.Length; $i++) {
        $characters[$i] = $alphabet[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($alphabet.Length)]
    }

    $value = (-join $characters) + 'Aa7#'
    Register-InstallerSecret -Value $value
    return $value
}

function Invoke-KeycloakSidecar {
    <#
    .SYNOPSIS
    Roda sidecar.ps1 num container avulso da imagem do instalador, só na rede do Keycloak, com raiz somente leitura, sem
    capabilities, sem log e sem socket do engine. Os arquivos (scripts e input.json com os segredos) entram por stdin num
    tar extraído para um tmpfs; nada sensível passa por argumento ou variável de ambiente do container (FR-033).
    .OUTPUTS
    ExitCode, Error e Result: o JSON da última linha `@@dokpod-result@@`, ou nulo sem ela.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string] $Image,
        [Parameter(Mandatory)][string] $Network,
        [Parameter(Mandatory)][string] $Task,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Files,
        [string] $Cli = 'docker'
    )

    $root = $script:SidecarRoot
    $arguments = @('run', '--rm', '--interactive', '--log-driver', 'none', '--read-only', '--network', $Network,
        '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
        '--tmpfs', "$($root):rw,noexec,nosuid,nodev,size=16m,mode=1777", '--tmpfs', '/tmp:rw,noexec,nosuid,nodev,size=64m,mode=1777',
        '--env', "HOME=$root", '--env', 'POWERSHELL_TELEMETRY_OPTOUT=1', '--env', 'DOTNET_CLI_TELEMETRY_OPTOUT=1',
        '--label', "io.dokpod.installer.task=$Task", '--name', "dokpod-installer-$Task-$([guid]::NewGuid().ToString('N').Substring(0, 12))",
        '--entrypoint', 'sh', $Image, '-c',
        "umask 077 && tar -x -f - -C $root && exec pwsh -NoLogo -NoProfile -NonInteractive -File $root/sidecar.ps1 $root/input.json")

    $process = Invoke-InstallerProcess -FilePath $Cli -ArgumentList $arguments -InputBytes (New-InstallerTarBundle -Files $Files)
    $line = @($process.Output -split "`n" | Where-Object { $_.StartsWith($script:ResultMarker) }) | Select-Object -Last 1
    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        Error    = $process.Error
        Result   = if ($line) { $line.Substring($script:ResultMarker.Length) | ConvertFrom-Json } else { $null }
    }
}

function Invoke-KeycloakRealm {
    <#
    .SYNOPSIS
    Reconcilia o realm, os clients, os grupos e o usuário inicial no Keycloak provisionado e remove o administrador de
    bootstrap (FR-013, FR-014, L3).
    .DESCRIPTION
    A senha temporária do usuário inicial é gravada em secretsDir/initial-admin-password antes de ser aplicada, para não
    se perder se a execução cair. O arquivo é apagado quando o Keycloak mostra a senha já trocada (sem UPDATE_PASSWORD),
    conforme data-model.md. A exibição única em TTY fica com o resumo final (T032).
    .PARAMETER Image
    Imagem do instalador (a mesma em execução), usada no container avulso.
    .OUTPUTS
    Estado sem segredos: usuário antes, senha aplicada, troca pendente e se o bootstrap foi removido.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Image,
        [string] $Network = $script:Network,
        [string] $BaseUrl = $script:InternalUrl,
        [string] $ScriptsRoot = (Join-Path (Get-InstallerHome) 'keycloak'),
        [string] $Cli = 'docker'
    )

    if ($Answers.keycloak.mode -ne 'provision') {
        Stop-Installer -Code Error -Message "keycloak.mode '$($Answers.keycloak.mode)' não é o provisionado."
    }

    $secretsDir = $Answers.secretsDir
    $bootstrapPassword = Read-OptionalSecret -SecretsDirectory $secretsDir -Name $script:BootstrapSecret
    $initialPassword = Read-OptionalSecret -SecretsDirectory $secretsDir -Name $script:InitialPasswordSecret
    $initialCreatedNow = $false
    if (-not $initialPassword) {
        $initialPassword = New-InitialAdminPassword
        Write-InstallerSecret -SecretsDirectory $secretsDir -Name $script:InitialPasswordSecret -Value $initialPassword
        $initialCreatedNow = $true
    }

    $settings = [ordered]@{
        baseUrl           = $BaseUrl
        realm             = $Answers.keycloak.realm
        # O BFF responde sob /dokpod na borda: o redirect_uri do client é <publicUrl>/dokpod/signin-oidc.
        bffBaseUrl        = "$($Answers.publicUrl.TrimEnd('/'))/dokpod"
        initialUsername   = $Answers.keycloak.initialAdmin.username
        initialEmail      = $Answers.keycloak.initialAdmin.email
        bootstrapUsername = $script:BootstrapUsername
        secrets           = [ordered]@{
            bootstrapPassword = $bootstrapPassword
            bffSecret         = Read-InstallerSecret -SecretsDirectory $secretsDir -Name $script:BffSecret
            provisionerSecret = Read-InstallerSecret -SecretsDirectory $secretsDir -Name $script:ProvisionerSecret
            initialPassword   = $initialPassword
        }
    }

    $files = [ordered]@{
        'input.json'              = $settings | ConvertTo-Json -Depth 4
        'sidecar.ps1'             = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Keycloak.Sidecar.ps1'))
        'configure-keycloak.ps1'  = [System.IO.File]::ReadAllText((Join-Path $ScriptsRoot 'configure-keycloak.ps1'))
    }
    $result = Invoke-KeycloakSidecar -Image $Image -Network $Network -Task 'keycloak' -Files $files -Cli $Cli
    if ($result.ExitCode -eq $script:BootstrapRemovalFailed) {
        Stop-Installer -Code Error -Message ("O realm foi configurado, mas o administrador de bootstrap '$($script:BootstrapUsername)' continua no master " +
            "do Keycloak: $($result.Error.Trim()) Ação: reexecute o instalador, que tenta a remoção de novo; enquanto isso, a instalação não converge (L3).")
    }

    if ($result.ExitCode -ne 0 -or $null -eq $result.Result) {
        if ($initialCreatedNow) { Remove-Item -LiteralPath (Resolve-SecretPath -SecretsDirectory $secretsDir -Name $script:InitialPasswordSecret) -Force }
        Stop-Installer -Code Error -Message "A configuração do Keycloak falhou (código $($result.ExitCode)): $($result.Error.Trim())"
    }

    $state = $result.Result
    if ($state.bootstrapRemoved) {
        # Vazio, e não apagado: o compose referencia o arquivo, e o entrypoint só cria a conta com ele não vazio.
        [System.IO.File]::WriteAllBytes((Resolve-SecretPath -SecretsDirectory $secretsDir -Name $script:BootstrapSecret), [byte[]] @())
    }

    $passwordFile = Resolve-SecretPath -SecretsDirectory $secretsDir -Name $script:InitialPasswordSecret
    if (-not $state.updatePasswordPending -or ($initialCreatedNow -and -not $state.passwordSet)) {
        Remove-Item -LiteralPath $passwordFile -Force -ErrorAction SilentlyContinue
    }

    return [pscustomobject]@{
        UserStateBefore       = $state.userStateBefore
        PasswordSet           = [bool] $state.passwordSet
        UpdatePasswordPending = [bool] $state.updatePasswordPending
        BootstrapMode         = [bool] $state.bootstrap
        BootstrapRemoved      = [bool] $state.bootstrapRemoved
        InitialPasswordFile   = if (Test-Path -LiteralPath $passwordFile) { $script:InitialPasswordSecret } else { $null }
    }
}

Export-ModuleMember -Function Get-KeycloakDefaults, Initialize-KeycloakSecret, New-InitialAdminPassword, Invoke-KeycloakSidecar, Invoke-KeycloakRealm
