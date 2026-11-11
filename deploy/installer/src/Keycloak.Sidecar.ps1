<#
.SYNOPSIS
Reconcilia o realm do Dokpod num Keycloak provisionado, de dentro de um container avulso do instalador (spec 033 T029).

.DESCRIPTION
Não é chamado pelo operador. O Keycloak.psm1 entrega este script, o configure-keycloak.ps1 e um input.json com os
segredos por stdin, num tar extraído para um tmpfs do container, que fica só na rede interna do Keycloak. Assim nenhum
segredo aparece em argumento, variável de ambiente do container ou `docker inspect`.

Passos:
1. espera o Keycloak responder;
2. descobre o estado do usuário inicial (ausente, sem senha ou pronto), para não reaplicar a senha temporária nem
   limpar a troca obrigatória de senha numa reexecução;
3. executa configure-keycloak.ps1, com -Bootstrap enquanto o administrador de bootstrap existir;
4. com -Bootstrap, remove o administrador de bootstrap do master depois que o administrador nominal existe (L3).

A última linha da saída é `@@dokpod-result@@` seguida de um JSON sem segredos. Códigos de saída: 0 sucesso, 1 erro,
20 a remoção do administrador de bootstrap falhou.

.PARAMETER InputPath
Caminho do input.json no tmpfs. É apagado logo depois de lido.

.EXAMPLE
pwsh -File Keycloak.Sidecar.ps1 /run/dokpod-kc/input.json

.NOTES
Requer PowerShell 7.4 ou superior, o da imagem do instalador.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($args -contains '--help') {
    Get-Help $PSCommandPath -Full
    exit 0
}

$InputPath = [string] $args[0]
$settings = Get-Content -LiteralPath $InputPath -Raw | ConvertFrom-Json -AsHashtable
Remove-Item -LiteralPath $InputPath -Force
$secrets = $settings.secrets
$baseUrl = $settings.baseUrl.TrimEnd('/')
$realm = $settings.realm
$bootstrap = -not [string]::IsNullOrEmpty($secrets.bootstrapPassword)

function Get-AccessToken {
    $body = if ($bootstrap) {
        @{ grant_type = 'password'; client_id = 'admin-cli'; username = $settings.bootstrapUsername; password = $secrets.bootstrapPassword }
    }
    else {
        @{ grant_type = 'client_credentials'; client_id = 'dokpod-provisioner'; client_secret = $secrets.provisionerSecret }
    }

    $tokenRealm = if ($bootstrap) { 'master' } else { $realm }
    return (Invoke-RestMethod -Method POST -Uri "$baseUrl/realms/$tokenRealm/protocol/openid-connect/token" -Body $body `
            -ContentType 'application/x-www-form-urlencoded').access_token
}

function Invoke-AdminApi {
    # O Invoke-RestMethod devolve uma lista JSON como um objeto só; o ForEach-Object a desdobra em itens.
    param([string] $Method, [string] $Path)
    return Invoke-RestMethod -Method $Method -Uri "$baseUrl/admin/realms/$Path" -Headers @{ Authorization = "Bearer $(Get-AccessToken)" } |
        ForEach-Object { $_ }
}

function Get-InitialUserState {
    # absent | no-password | ready, e se a troca obrigatória de senha ainda está pendente.
    try {
        $users = @(Invoke-AdminApi GET "$realm/users?username=$([Uri]::EscapeDataString($settings.initialUsername))&exact=true")
    }
    catch {
        $response = $_.Exception.PSObject.Properties['Response'] ? $_.Exception.Response : $null
        # Realm ainda inexistente, na primeira execução.
        if ($response -and [int] $response.StatusCode -eq 404) { return @{ state = 'absent'; updatePasswordPending = $false } }
        throw
    }

    $user = $users | Where-Object username -eq $settings.initialUsername | Select-Object -First 1
    if (-not $user) { return @{ state = 'absent'; updatePasswordPending = $false } }

    $credentials = @(Invoke-AdminApi GET "$realm/users/$($user.id)/credentials" | Where-Object type -eq 'password')
    $pending = @($user.requiredActions) -contains 'UPDATE_PASSWORD'
    return @{ state = $(if ($credentials.Count -gt 0) { 'ready' } else { 'no-password' }); updatePasswordPending = $pending }
}

# 1. Keycloak no ar (até 3 minutos no primeiro start, que cria o schema do banco).
$deadline = [datetime]::UtcNow.AddMinutes(3)
while ($true) {
    try {
        Invoke-RestMethod -Uri "$baseUrl/realms/master/.well-known/openid-configuration" | Out-Null
        break
    }
    catch {
        if ([datetime]::UtcNow -gt $deadline) { throw "O Keycloak não respondeu em $baseUrl em 3 minutos." }
        Start-Sleep -Seconds 2
    }
}

# 2. Estado do usuário inicial antes de reconciliar.
$before = Get-InitialUserState

# 3. Reconciliação do realm pelo script de tools/scripts (fonte única, D-001).
$environment = @{
    DOKPOD_BFF_CLIENT_SECRET         = $secrets.bffSecret
    DOKPOD_PROVISIONER_CLIENT_SECRET = $secrets.provisionerSecret
}
$parameters = @{
    BaseUrl          = $baseUrl
    Realm            = $realm
    BffBaseUrl       = $settings.bffBaseUrl
    InternalHttp     = $true
    InitialUsername  = $settings.initialUsername
    InitialUserEmail = $settings.initialEmail
}
$passwordSet = $false
switch ($before.state) {
    'absent' { $passwordSet = $true }
    'no-password' { $parameters.ResetInitialUserPassword = $true; $parameters.RequiredActions = @('UPDATE_PASSWORD'); $passwordSet = $true }
    # Usuário pronto: o script reaplicaria requiredActions vazio e limparia a troca obrigatória de senha pendente.
    'ready' { $parameters.SkipUser = $true }
}
if ($passwordSet) { $environment.DOKPOD_ADMIN_TEMPORARY_PASSWORD = $secrets.initialPassword }
if ($bootstrap) {
    $parameters.Bootstrap = $true
    $parameters.AdminUsername = $settings.bootstrapUsername
    $parameters.AdminPassword = ConvertTo-SecureString -String $secrets.bootstrapPassword -AsPlainText -Force
}

foreach ($name in $environment.Keys) { [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process') }
& (Join-Path $PSScriptRoot 'configure-keycloak.ps1') @parameters
$after = Get-InitialUserState
if ($after.state -ne 'ready') {
    throw "O usuário inicial $($settings.initialUsername) não ficou pronto no realm $realm (estado $($after.state))."
}

# 4. Remoção do administrador de bootstrap (L3): só depois que o administrador nominal existe.
$bootstrapRemoved = $false
if ($bootstrap) {
    try {
        $master = @(Invoke-AdminApi GET "master/users?username=$([Uri]::EscapeDataString($settings.bootstrapUsername))&exact=true") |
            Where-Object username -eq $settings.bootstrapUsername | Select-Object -First 1
        if ($master) {
            $token = Get-AccessToken
            Invoke-RestMethod -Method DELETE -Uri "$baseUrl/admin/realms/master/users/$($master.id)" -Headers @{ Authorization = "Bearer $token" } | Out-Null
        }
        $bootstrapRemoved = $true
    }
    catch {
        [Console]::Error.WriteLine("Falha ao remover o administrador de bootstrap $($settings.bootstrapUsername) do master: $($_.Exception.Message)")
        exit 20
    }
}

$result = @{
    userStateBefore       = $before.state
    passwordSet           = $passwordSet
    updatePasswordPending = $after.updatePasswordPending
    bootstrap             = $bootstrap
    bootstrapRemoved      = $bootstrapRemoved
}
[Console]::Out.WriteLine('@@dokpod-result@@' + ($result | ConvertTo-Json -Compress))
