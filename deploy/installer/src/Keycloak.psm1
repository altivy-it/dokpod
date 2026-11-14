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
$script:NoAdminAccess = 21
$script:VersionTooOld = 22
# Keycloak homologado: a série 26 (a imagem provisionada é a 26.7, infrastructure-images.json).
$script:MinimumMajor = 26
# Containers avulsos com Keycloak existente: a rede padrão do engine, que tem saída.
$script:ExistingNetwork = 'bridge'

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
        ExistingNetwork       = $script:ExistingNetwork
    }
}

function Initialize-KeycloakSecret {
    <#
    .SYNOPSIS
    Gera os segredos do Keycloak que faltam: client secret do BFF e do dokpod-provisioner e, só no provisionado, a senha
    do administrador de bootstrap (FR-028). Um keycloak-bootstrap-admin vazio quer dizer bootstrap concluído e continua
    vazio. No existente, a credencial do master nunca é gerada nem gravada (FR-015).
    .OUTPUTS
    Os nomes dos segredos criados nesta execução.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $names = @($script:BffSecret, $script:ProvisionerSecret)
    if ($Answers.keycloak.mode -eq 'provision') { $names = @($script:BootstrapSecret) + $names }
    $created = @()
    foreach ($name in $names) {
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
        # CA privada do Keycloak existente (keycloak.caCertificate): entra no tar e soma-se às raízes do sistema.
        [string] $CaPem,
        [string] $Cli = 'docker'
    )

    $root = $script:SidecarRoot
    $trust = ''
    if ($CaPem) {
        $Files = [ordered]@{} + $Files
        $Files['ca.crt'] = $CaPem
        $trust = "cat /etc/ssl/certs/ca-certificates.crt $root/ca.crt > $root/ca-bundle.crt && export SSL_CERT_FILE=$root/ca-bundle.crt && "
    }
    $arguments = @('run', '--rm') + (Get-ContainerLimitArguments) + @('--interactive', '--log-driver', 'none', '--read-only', '--network', $Network,
        '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
        '--tmpfs', "$($root):rw,noexec,nosuid,nodev,size=16m,mode=1777", '--tmpfs', '/tmp:rw,noexec,nosuid,nodev,size=64m,mode=1777',
        '--env', "HOME=$root", '--env', 'POWERSHELL_TELEMETRY_OPTOUT=1', '--env', 'DOTNET_CLI_TELEMETRY_OPTOUT=1',
        '--label', "io.dokpod.installer.task=$Task", '--name', "dokpod-installer-$Task-$([guid]::NewGuid().ToString('N').Substring(0, 12))",
        '--entrypoint', 'sh', $Image, '-c',
        "umask 077 && tar -x -f - -C $root && $($trust)exec pwsh -NoLogo -NoProfile -NonInteractive -File $root/sidecar.ps1 $root/input.json")

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

    $settings = Get-KeycloakSidecarSettings -Answers $Answers -BaseUrl $BaseUrl
    $settings['initialUsername'] = $Answers.keycloak.initialAdmin.username
    $settings['initialEmail'] = $Answers.keycloak.initialAdmin.email
    $settings['bootstrapUsername'] = $script:BootstrapUsername
    $settings.secrets['bootstrapPassword'] = $bootstrapPassword
    $settings.secrets['initialPassword'] = $initialPassword

    $result = Invoke-KeycloakSidecar -Image $Image -Network $Network -Task 'keycloak' -Files (Get-KeycloakSidecarFiles -Settings $settings -ScriptsRoot $ScriptsRoot) -Cli $Cli
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

function Get-ExistingKeycloakCaPem {
    <#
    .SYNOPSIS
    O PEM de keycloak.caCertificate (público: vai para o bundle de SSL_CERT_FILE da API e do BFF e para o container da
    reconciliação), ou $null sem ele (A3).
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $keycloak = $Answers.keycloak
    if ($keycloak.mode -ne 'existing' -or -not $keycloak.Contains('caCertificate')) { return $null }
    $path = [string] $keycloak.caCertificate
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Stop-Installer -Code Usage -Message "O arquivo de keycloak.caCertificate não existe: $path."
    }

    $pem = [System.IO.File]::ReadAllText($path)
    if ($pem -notmatch '-----BEGIN CERTIFICATE-----' -or $pem -match 'PRIVATE KEY-----') {
        Stop-Installer -Code Usage -Message "keycloak.caCertificate ($path) precisa ter só certificados PEM."
    }

    return $pem.TrimEnd() + "`n"
}

function Get-ExistingKeycloakAdminCredential {
    <#
    .SYNOPSIS
    Senha do administrador do master do Keycloak existente: de { file }/{ env } ou do prompt, só em memória (FR-015).
    Sem ela e sem terminal, devolve $null quando a reexecução pode usar o dokpod-provisioner; senão, código 2.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [bool] $Interactive = (Test-InteractiveSession),
        [scriptblock] $AskCredential = { param($message) Read-Host -Prompt $message -MaskInput },
        [bool] $ProvisionerAvailable = $false
    )

    $admin = $Answers.keycloak.admin
    $credential = if ($admin.Contains('password')) {
        try { Read-SecretReference -Reference $admin.password -Purpose "keycloak.admin ($($admin.username))" }
        catch {
            # O operador pode apagar o arquivo depois da instalação, como com o PostgreSQL existente.
            if (-not $ProvisionerAvailable) { throw }
            Write-InstallerLine -Message "A credencial de keycloak.admin não está disponível; o realm é reconciliado com o dokpod-provisioner, e a política do realm fica como está."
            return $null
        }
    }
    elseif ($ProvisionerAvailable) {
        return $null
    }
    elseif ($Interactive) {
        [string] (& $AskCredential "Senha de $($admin.username) no realm master de $($Answers.keycloak.url) (usada só nesta execução, não é gravada)")
    }
    else {
        Stop-Installer -Code Usage -Message 'Sem terminal, keycloak.admin.password precisa vir como { file } ou { env } no arquivo de respostas.'
    }

    if ([string]::IsNullOrEmpty($credential)) {
        Stop-Installer -Code Usage -Message "A senha de $($admin.username) no Keycloak está vazia."
    }

    Register-InstallerSecret -Value $credential
    return $credential
}

function Get-KeycloakSidecarSettings {
    # input.json comum: URL, realm, redirect do BFF e os segredos dos clients; o resto vem de cada modo.
    param([System.Collections.IDictionary] $Answers, [string] $BaseUrl)

    $secretsDir = $Answers.secretsDir
    return [ordered]@{
        baseUrl      = $BaseUrl
        realm        = $Answers.keycloak.realm
        # O BFF responde sob /dokpod na borda: o redirect_uri do client é <publicUrl>/dokpod/signin-oidc.
        bffBaseUrl   = "$($Answers.publicUrl.TrimEnd('/'))/dokpod"
        minimumMajor = $script:MinimumMajor
        secrets      = [ordered]@{
            bffSecret         = Read-InstallerSecret -SecretsDirectory $secretsDir -Name $script:BffSecret
            provisionerSecret = Read-InstallerSecret -SecretsDirectory $secretsDir -Name $script:ProvisionerSecret
        }
    }
}

function Get-KeycloakSidecarFiles {
    param([System.Collections.IDictionary] $Settings, [string] $ScriptsRoot)

    return [ordered]@{
        'input.json'             = $Settings | ConvertTo-Json -Depth 4
        'sidecar.ps1'            = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Keycloak.Sidecar.ps1'))
        'configure-keycloak.ps1' = [System.IO.File]::ReadAllText((Join-Path $ScriptsRoot 'configure-keycloak.ps1'))
    }
}

function Stop-KeycloakSidecarFailure {
    param($Result, [string] $Url)

    if ($Result.ExitCode -eq $script:NoAdminAccess -or $Result.ExitCode -eq $script:VersionTooOld) {
        Stop-Installer -Code Prerequisite -Message "Keycloak em $($Url): $($Result.Error.Trim()) Nada foi alterado."
    }

    Stop-Installer -Code Error -Message "A configuração do Keycloak falhou (código $($Result.ExitCode)): $($Result.Error.Trim())"
}

function Invoke-ExistingKeycloakRealm {
    <#
    .SYNOPSIS
    keycloak.mode existing (T039, FR-013 a FR-015, A3): confere acesso e versão antes de alterar; reconcilia só o realm
    do Dokpod com -Bootstrap -SkipUser; põe administratorUser em /dokpod/administrators.
    .DESCRIPTION
    A credencial do master é transitória: nunca vai para secretsDir, o estado ou o relatório. Na reexecução sem ela, a
    reconciliação usa o dokpod-provisioner (clients, roles e grupos; a política do realm fica como está). Quando
    administratorUser não existe no realm, ele é criado com a senha temporária de initial-admin-password e troca
    obrigatória, como o primeiro administrador do Keycloak provisionado.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Image,
        [string] $Network = $script:ExistingNetwork,
        [bool] $Interactive = (Test-InteractiveSession),
        [scriptblock] $AskCredential = { param($message) Read-Host -Prompt $message -MaskInput },
        # O realm já foi reconciliado numa execução anterior: sem a credencial do master, basta o dokpod-provisioner.
        [bool] $RealmConfigured = $false,
        [string] $ScriptsRoot = (Join-Path (Get-InstallerHome) 'keycloak'),
        [string] $Cli = 'docker'
    )

    $keycloak = $Answers.keycloak
    if ($keycloak.mode -ne 'existing') {
        Stop-Installer -Code Error -Message "keycloak.mode '$($keycloak.mode)' não é o existente."
    }

    $secretsDir = $Answers.secretsDir
    $provisionerAvailable = $RealmConfigured -and (Test-InstallerSecret -SecretsDirectory $secretsDir -Name $script:ProvisionerSecret)
    foreach ($name in $script:BffSecret, $script:ProvisionerSecret) {
        if (-not (Test-InstallerSecret -SecretsDirectory $secretsDir -Name $name)) {
            Write-InstallerSecret -SecretsDirectory $secretsDir -Name $name -Value (New-InstallerSecret)
        }
    }

    $adminCredential = Get-ExistingKeycloakAdminCredential -Answers $Answers -Interactive $Interactive -AskCredential $AskCredential -ProvisionerAvailable $provisionerAvailable
    $initialPassword = Read-OptionalSecret -SecretsDirectory $secretsDir -Name $script:InitialPasswordSecret
    $initialCreatedNow = $false
    if (-not $initialPassword) {
        $initialPassword = New-InitialAdminPassword
        Write-InstallerSecret -SecretsDirectory $secretsDir -Name $script:InitialPasswordSecret -Value $initialPassword
        $initialCreatedNow = $true
    }

    $settings = Get-KeycloakSidecarSettings -Answers $Answers -BaseUrl $keycloak.url.TrimEnd('/')
    $settings['mode'] = 'existing'
    $settings['adminUsername'] = [string] $keycloak.admin.username
    $settings['administratorUser'] = [string] $keycloak.administratorUser
    $settings.secrets['adminPassword'] = $adminCredential
    $settings.secrets['initialPassword'] = $initialPassword
    try {
        $result = Invoke-KeycloakSidecar -Image $Image -Network $Network -Task 'keycloak' -Files (Get-KeycloakSidecarFiles -Settings $settings -ScriptsRoot $ScriptsRoot) `
            -CaPem (Get-ExistingKeycloakCaPem -Answers $Answers) -Cli $Cli
    }
    finally {
        $settings.secrets['adminPassword'] = $null
        $adminCredential = $null
    }

    $passwordFile = Resolve-SecretPath -SecretsDirectory $secretsDir -Name $script:InitialPasswordSecret
    if ($result.ExitCode -ne 0 -or $null -eq $result.Result) {
        if ($initialCreatedNow) { Remove-Item -LiteralPath $passwordFile -Force -ErrorAction SilentlyContinue }
        Stop-KeycloakSidecarFailure -Result $result -Url $keycloak.url
    }

    $state = $result.Result
    # A senha temporária só fica enquanto o usuário criado agora não a trocou (FR-014).
    if (-not $state.updatePasswordPending -or ($initialCreatedNow -and -not $state.passwordSet)) {
        Remove-Item -LiteralPath $passwordFile -Force -ErrorAction SilentlyContinue
    }

    return [pscustomobject]@{
        UserStateBefore       = $state.userStateBefore
        PasswordSet           = [bool] $state.passwordSet
        UpdatePasswordPending = [bool] $state.updatePasswordPending
        BootstrapMode         = [bool] $state.bootstrap
        BootstrapRemoved      = $false
        Version               = if ($state.PSObject.Properties['version']) { $state.version } else { $null }
        InitialPasswordFile   = if (Test-Path -LiteralPath $passwordFile) { $script:InitialPasswordSecret } else { $null }
    }
}

function Get-KeycloakRealmDrift {
    <#
    .SYNOPSIS
    Divergências do realm em relação ao contrato do Dokpod (T040), pelo -Drift do configure-keycloak.ps1, sem alterar
    nada: a lista vai para o plano (realm: update, com cada item) e só é corrigida depois da confirmação.
    .OUTPUTS
    Os itens divergentes (vazio quando o realm está conforme), ou $null quando não dá para comparar: Keycloak
    provisionado ainda não rodando, ou o existente sem credencial nesta execução.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Image,
        [Parameter(Mandatory)][string] $Network,
        [string] $BaseUrl = $script:InternalUrl,
        [string] $ScriptsRoot = (Join-Path (Get-InstallerHome) 'keycloak'),
        [string] $Cli = 'docker'
    )

    $secretsDir = $Answers.secretsDir
    foreach ($name in $script:BffSecret, $script:ProvisionerSecret) {
        if (-not (Test-InstallerSecret -SecretsDirectory $secretsDir -Name $name)) { return $null }
    }

    $existing = $Answers.keycloak.mode -eq 'existing'
    $settings = Get-KeycloakSidecarSettings -Answers $Answers -BaseUrl $(if ($existing) { $Answers.keycloak.url.TrimEnd('/') } else { $BaseUrl })
    $settings['drift'] = $true
    if ($existing) {
        $settings['mode'] = 'existing'
        $settings['adminUsername'] = [string] $Answers.keycloak.admin.username
    }
    else {
        $settings['bootstrapUsername'] = $script:BootstrapUsername
        $settings.secrets['bootstrapPassword'] = Read-OptionalSecret -SecretsDirectory $secretsDir -Name $script:BootstrapSecret
    }

    $result = Invoke-KeycloakSidecar -Image $Image -Network $Network -Task 'keycloak-drift' -Files (Get-KeycloakSidecarFiles -Settings $settings -ScriptsRoot $ScriptsRoot) `
        -CaPem (Get-ExistingKeycloakCaPem -Answers $Answers) -Cli $Cli
    if ($result.ExitCode -ne 0 -or $null -eq $result.Result) {
        # Sem a comparação, o plano não lista divergências; a reconciliação da etapa realm continua corrigindo tudo.
        Write-InstallerLine -ToStandardError -Message "Não foi possível comparar o realm $($Answers.keycloak.realm) com o contrato (código $($result.ExitCode)): $($result.Error.Trim())"
        return $null
    }

    return , [string[]] @($result.Result.drift | Where-Object { $_ })
}

Export-ModuleMember -Function Get-KeycloakDefaults, Initialize-KeycloakSecret, New-InitialAdminPassword, Invoke-KeycloakSidecar, Invoke-KeycloakRealm,
Get-ExistingKeycloakCaPem, Get-ExistingKeycloakAdminCredential, Invoke-ExistingKeycloakRealm, Get-KeycloakRealmDrift
