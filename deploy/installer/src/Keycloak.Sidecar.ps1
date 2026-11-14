<#
.SYNOPSIS
Reconcilia o realm do Dokpod num Keycloak provisionado ou existente, de dentro de um container avulso do instalador
(spec 033 T029, T039 e T040).

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

Com `mode = existing` (Keycloak do usuário, T039), antes de qualquer alteração confere o acesso administrativo e a
versão; depois reconcilia o realm com -Bootstrap -SkipUser e põe `administratorUser` no grupo /dokpod/administrators,
criando-o com senha temporária quando não existe. Com `drift = true` (T040), só lista as divergências do realm pelo
-Drift do configure-keycloak.ps1, sem alterar nada.

A última linha da saída é `@@dokpod-result@@` seguida de um JSON sem segredos. Códigos de saída: 0 sucesso, 1 erro,
20 a remoção do administrador de bootstrap falhou, 21 sem acesso administrativo, 22 versão abaixo da homologada.

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
$existing = $settings.Contains('mode') -and $settings.mode -eq 'existing'
$driftOnly = $settings.Contains('drift') -and $settings.drift
# No existente, a credencial do master é a do administrador informado; no provisionado, a do bootstrap.
$adminUsername = if ($existing) { $settings['adminUsername'] } else { $settings['bootstrapUsername'] }
$adminCredential = if ($existing) { $secrets['adminPassword'] } else { $secrets['bootstrapPassword'] }
$bootstrap = -not [string]::IsNullOrEmpty($adminCredential)

function Get-AccessToken {
    $body = if ($bootstrap) {
        @{ grant_type = 'password'; client_id = 'admin-cli'; username = $adminUsername; password = $adminCredential }
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

function Get-HttpStatus {
    param($ErrorRecord)
    $response = $ErrorRecord.Exception.PSObject.Properties['Response'] ? $ErrorRecord.Exception.Response : $null
    if ($response) { return [int] $response.StatusCode }
    return $null
}

function Invoke-Configure {
    # configure-keycloak.ps1 (fonte única, D-001) com os parâmetros comuns; devolve as linhas da saída.
    param([hashtable] $Extra = @{})
    $parameters = @{ BaseUrl = $baseUrl; Realm = $realm; BffBaseUrl = $settings.bffBaseUrl }
    if (-not $existing) { $parameters.InternalHttp = $true }
    if ($settings.Contains('loginTheme')) { $parameters.LoginTheme = [string] $settings.loginTheme }
    if ($bootstrap) {
        $parameters.Bootstrap = $true
        $parameters.AdminUsername = $adminUsername
        $parameters.AdminPassword = ConvertTo-SecureString -String $adminCredential -AsPlainText -Force
    }
    foreach ($key in $Extra.Keys) { $parameters[$key] = $Extra[$key] }
    foreach ($name in $environment.Keys) { [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process') }
    return @(& (Join-Path $PSScriptRoot 'configure-keycloak.ps1') @parameters)
}

$environment = @{
    DOKPOD_BFF_CLIENT_SECRET         = $secrets.bffSecret
    DOKPOD_PROVISIONER_CLIENT_SECRET = $secrets.provisionerSecret
}

# 1. Keycloak no ar (até 3 minutos no primeiro start, que cria o schema do banco).
$deadline = [datetime]::UtcNow.AddMinutes($(if ($existing) { 1 } else { 3 }))
while ($true) {
    try {
        Invoke-RestMethod -Uri "$baseUrl/realms/master/.well-known/openid-configuration" | Out-Null
        break
    }
    catch {
        if ([datetime]::UtcNow -gt $deadline) { throw "O Keycloak não respondeu em $baseUrl ($($_.Exception.Message))." }
        Start-Sleep -Seconds 2
    }
}

# 2. Keycloak existente: acesso administrativo e versão antes de alterar qualquer coisa (T039).
$version = $null
if ($existing) {
    try { $token = Get-AccessToken }
    catch {
        $credential = if ($bootstrap) { "o administrador $adminUsername no realm master" } else { "o client dokpod-provisioner no realm $realm" }
        [Console]::Error.WriteLine("Sem acesso administrativo ao Keycloak com $credential (HTTP $(Get-HttpStatus $_)).")
        exit 21
    }

    try {
        $info = Invoke-RestMethod -Uri "$baseUrl/admin/serverinfo" -Headers @{ Authorization = "Bearer $token" }
        $version = [string] $info.systemInfo.version
        $themes = @($info.themes.login | ForEach-Object { $_.name })
        # Sem o tema do Dokpod no Keycloak do usuário, o realm fica com o tema dele.
        if ($themes -notcontains 'dokpod') { $settings.loginTheme = '' }
    }
    catch {
        if ($bootstrap) {
            [Console]::Error.WriteLine("O administrador $adminUsername não lê /admin/serverinfo (HTTP $(Get-HttpStatus $_)); falta acesso administrativo.")
            exit 21
        }
        # O dokpod-provisioner não lê serverinfo: a versão foi conferida na execução com o administrador.
    }

    $major = if ($version -match '^(\d+)\.') { [int] $Matches[1] } else { 0 }
    if ($version -and $major -lt [int] $settings.minimumMajor) {
        [Console]::Error.WriteLine("O Keycloak $version é anterior ao homologado ($($settings.minimumMajor).x).")
        exit 22
    }
}

# 3. Só as divergências (T040): nada é alterado.
if ($driftOnly) {
    $lines = @(Invoke-Configure -Extra @{ Drift = $true; SkipUser = $true })
    $drift = @($lines | ForEach-Object { [string] $_ } | Where-Object { $_.StartsWith('Divergência: ') } | ForEach-Object { $_.Substring('Divergência: '.Length) })
    [Console]::Out.WriteLine('@@dokpod-result@@' + (@{ drift = $drift; version = $version } | ConvertTo-Json -Compress))
    exit 0
}

if ($existing) {
    # 4. Reconciliação do realm (só o do Dokpod; nunca o master nem outros realms, FR-013).
    Invoke-Configure -Extra @{ SkipUser = $true } | Out-Null

    # 5. administratorUser no grupo /dokpod/administrators (FR-014); criado com senha temporária quando não existe.
    $username = [string] $settings.administratorUser
    $users = @(Invoke-AdminApi GET "$realm/users?username=$([Uri]::EscapeDataString($username))&exact=true")
    $user = $users | Where-Object username -eq $username | Select-Object -First 1
    $stateBefore = if ($user) { 'ready' } else { 'absent' }
    $passwordSet = $false
    if (-not $user) {
        $body = @{ username = $username; enabled = $true; requiredActions = @('UPDATE_PASSWORD') } | ConvertTo-Json
        Invoke-RestMethod -Method POST -Uri "$baseUrl/admin/realms/$realm/users" -Headers @{ Authorization = "Bearer $(Get-AccessToken)" } -ContentType 'application/json' -Body $body | Out-Null
        $user = @(Invoke-AdminApi GET "$realm/users?username=$([Uri]::EscapeDataString($username))&exact=true") | Where-Object username -eq $username | Select-Object -First 1
        $reset = @{ type = 'password'; value = $secrets.initialPassword; temporary = $true } | ConvertTo-Json
        Invoke-RestMethod -Method PUT -Uri "$baseUrl/admin/realms/$realm/users/$($user.id)/reset-password" -Headers @{ Authorization = "Bearer $(Get-AccessToken)" } -ContentType 'application/json' -Body $reset | Out-Null
        $passwordSet = $true
    }

    $group = Invoke-AdminApi GET "$realm/group-by-path/dokpod/administrators"
    Invoke-RestMethod -Method PUT -Uri "$baseUrl/admin/realms/$realm/users/$($user.id)/groups/$($group.id)" -Headers @{ Authorization = "Bearer $(Get-AccessToken)" } | Out-Null
    $pending = @((Invoke-AdminApi GET "$realm/users/$($user.id)").requiredActions) -contains 'UPDATE_PASSWORD'
    $result = @{ userStateBefore = $stateBefore; passwordSet = $passwordSet; updatePasswordPending = $pending; bootstrap = $bootstrap; bootstrapRemoved = $false; version = $version }
    [Console]::Out.WriteLine('@@dokpod-result@@' + ($result | ConvertTo-Json -Compress))
    exit 0
}

# 4. Estado do usuário inicial antes de reconciliar.
$before = Get-InitialUserState

# 5. Reconciliação do realm pelo script de tools/scripts (fonte única, D-001).
$extra = @{ InitialUsername = $settings.initialUsername; InitialUserEmail = $settings.initialEmail }
$passwordSet = $false
switch ($before.state) {
    'absent' { $passwordSet = $true }
    'no-password' { $extra.ResetInitialUserPassword = $true; $extra.RequiredActions = @('UPDATE_PASSWORD'); $passwordSet = $true }
    # Usuário pronto: o script reaplicaria requiredActions vazio e limparia a troca obrigatória de senha pendente.
    'ready' { $extra.SkipUser = $true }
}
if ($passwordSet) { $environment.DOKPOD_ADMIN_TEMPORARY_PASSWORD = $secrets.initialPassword }
Invoke-Configure -Extra $extra | Out-Null
$after = Get-InitialUserState
if ($after.state -ne 'ready') {
    throw "O usuário inicial $($settings.initialUsername) não ficou pronto no realm $realm (estado $($after.state))."
}

# 6. Remoção do administrador de bootstrap (L3): só depois que o administrador nominal existe.
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
