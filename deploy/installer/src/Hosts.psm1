#Requires -Version 7.4
<#
.SYNOPSIS
Hosts remotos (spec 033 T046 e T047, FR-025, FR-022, L2, L6): `host add` cadastra o ambiente, emite o certificado do
agente, registra a identidade e gera um pacote cifrado; `host revoke` revoga a identidade e marca o pacote no estado.

.DESCRIPTION
O pacote `installDir/packages/<environmentId>.tar` (0600) tem só o README.txt, o agent.tar.enc e o SHA-256 dele. O
agent.tar.enc é cifrado no formato de `openssl enc -aes-256-cbc -pbkdf2 -iter 600000 -md sha256`, para o host de
destino abri-lo só com o openssl. Dentro: agent.pfx, agent.pass, ca.crt, agent.env, compose.agent.yaml (ou
windows-service.txt) e README.txt. Nenhum outro arquivo de secretsDir entra (FR-025). A frase da cifra é gerada,
mostrada uma vez no terminal ou gravada em secretsDir/packages/<id>.passphrase sem terminal, e nunca vai no pacote (L2).
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module (Join-Path $PSScriptRoot 'Secrets.psm1')
Import-Module (Join-Path $PSScriptRoot 'State.psm1')
Import-Module (Join-Path $PSScriptRoot 'Pki.psm1')
Import-Module (Join-Path $PSScriptRoot 'Environments.psm1')

$script:Iterations = 600000
$script:NamePattern = '^[a-z0-9][a-z0-9.-]{0,62}$'
$script:Reason = 'instalador: host add'

function Protect-HostPackage {
    <#
    .SYNOPSIS
    Cifra no formato do `openssl enc -aes-256-cbc -pbkdf2 -iter 600000 -md sha256`: "Salted__", 8 bytes de sal e o
    texto cifrado; chave e IV vêm do PBKDF2-SHA256 da frase.
    #>
    [CmdletBinding()]
    [OutputType([byte[]])]
    param([Parameter(Mandatory)][byte[]] $Bytes, [Parameter(Mandatory)][string] $Passphrase)

    $salt = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(8)
    $material = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2([System.Text.Encoding]::UTF8.GetBytes($Passphrase), $salt, $script:Iterations,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256, 48)
    $aes = [System.Security.Cryptography.Aes]::Create()
    try {
        $aes.Key = $material[0..31]
        $cipher = $aes.EncryptCbc($Bytes, [byte[]] $material[32..47], [System.Security.Cryptography.PaddingMode]::PKCS7)
    }
    finally { $aes.Dispose() }

    return , [byte[]] ([System.Text.Encoding]::ASCII.GetBytes('Salted__') + $salt + $cipher)
}

function Unprotect-HostPackage {
    # O inverso de Protect-HostPackage, para os testes conferirem o conteúdo como o openssl do host faria.
    [CmdletBinding()]
    [OutputType([byte[]])]
    param([Parameter(Mandatory)][byte[]] $Bytes, [Parameter(Mandatory)][string] $Passphrase)

    $salt = [byte[]] $Bytes[8..15]
    $material = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2([System.Text.Encoding]::UTF8.GetBytes($Passphrase), $salt, $script:Iterations,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256, 48)
    $aes = [System.Security.Cryptography.Aes]::Create()
    try {
        $aes.Key = $material[0..31]
        return , [byte[]] $aes.DecryptCbc([byte[]] $Bytes[16..($Bytes.Length - 1)], [byte[]] $material[32..47], [System.Security.Cryptography.PaddingMode]::PKCS7)
    }
    finally { $aes.Dispose() }
}

function New-HostPassphrase {
    # 6 grupos de 5 caracteres sem ambiguidade (~150 bits), fáceis de ditar.
    $alphabet = 'abcdefghijkmnpqrstuvwxyz23456789'
    $groups = foreach ($group in 1..6) { -join (1..5 | ForEach-Object { $alphabet[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($alphabet.Length)] }) }
    $value = $groups -join '-'
    Register-InstallerSecret -Value $value
    return $value
}

function New-HostPackageFiles {
    <#
    .SYNOPSIS
    Os arquivos de dentro do pacote: o certificado e a senha do agente, a CA que valida a API e a configuração do
    agente para o engine do host (compose.agent.yaml no Docker e no Podman rootless; windows-service.txt no Windows).
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $EnvironmentId,
        [Parameter(Mandatory)][ValidateSet('docker', 'podman', 'windows')][string] $Engine,
        [Parameter(Mandatory)][string] $AgentImage
    )

    $secretsDir = $Answers.secretsDir
    $endpoint = "https://$(([uri] $Answers.publicUrl).Host):$($Answers.agentGrpcPort)"
    $files = [ordered]@{
        'agent.pfx' = [System.IO.File]::ReadAllBytes((Resolve-SecretPath -SecretsDirectory $secretsDir -Name "pki/agent-$EnvironmentId.pfx"))
        'agent.pass' = Read-InstallerSecret -SecretsDirectory $secretsDir -Name "pki/agent-$EnvironmentId.pass"
        'ca.crt' = [System.IO.File]::ReadAllText((Join-Path $Answers.installDir 'certs/ca.crt'))
    }

    if ($Engine -eq 'windows') {
        $files['windows-service.txt'] = @"
# Parâmetros de deploy/agent/manage-windows-service.ps1 -Action Install (docs/runbooks/qualificacao-windows.md).
-ControlPlaneEndpoint $endpoint
-EnvironmentId $EnvironmentId
-ClientCertificatePath <pasta>\agent.pfx
-ServerCaCertificatePath <pasta>\ca.crt
# A senha do PFX está em agent.pass; o script a lê do arquivo, nunca da linha de comando.
"@
        $setup = "Siga docs/runbooks/qualificacao-windows.md com os parâmetros de windows-service.txt."
    }
    else {
        $files['agent.env'] = @"
DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT=$endpoint
DOKPOD_AGENT_ENVIRONMENT_ID=$EnvironmentId
DOKPOD_AGENT_CLIENT_CERTIFICATE_PATH=/certs/agent.pfx
DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH=/certs/ca.crt
DOKPOD_AGENT_DATA_DIRECTORY=/var/lib/dokpod-agent
DOKPOD_AGENT_DOCKER_SOCKET=/run/engine.sock
"@
        $files['compose.agent.yaml'] = @"
# Agente do Dokpod para o ambiente $EnvironmentId (spec 033, FR-025). Engine $Engine rootless: o root do container é
# o usuário do host. DOKPOD_ENGINE_SOCKET aponta o socket do engine do usuário.
name: dokpod-agent
services:
  agent:
    image: $AgentImage
    restart: unless-stopped
    user: '0:0'
    read_only: true
    tmpfs:
      - /tmp:rw,noexec,nosuid,size=16m
    env_file: agent.env
    secrets:
      - source: agent-pfx
        target: /certs/agent.pfx
      - source: agent-pass
        target: /run/secrets/DOKPOD_AGENT_CLIENT_CERTIFICATE_PASSWORD
    volumes:
      - `${DOKPOD_ENGINE_SOCKET:?defina DOKPOD_ENGINE_SOCKET}:/run/engine.sock
      - ./ca.crt:/certs/ca.crt:ro
      - dokpod-agent-data:/var/lib/dokpod-agent
secrets:
  agent-pfx:
    file: ./agent.pfx
  agent-pass:
    file: ./agent.pass
volumes:
  dokpod-agent-data: {}
"@
        $socket = if ($Engine -eq 'podman') { '$XDG_RUNTIME_DIR/podman/podman.sock' } else { '$XDG_RUNTIME_DIR/docker.sock' }
        $setup = "DOKPOD_ENGINE_SOCKET=$socket docker compose -f compose.agent.yaml up -d"
    }

    $files['README.txt'] = @"
Agente do Dokpod para o ambiente $EnvironmentId ($Engine).

1. Guarde esta pasta só para o usuário do engine (chmod 700); agent.pfx e agent.pass são a identidade do agente.
2. $setup
3. O host aparece conectado no console em até 5 minutos. Ele conecta em $endpoint, com mTLS.
4. Se o pacote vazou ou o host saiu de uso: install.sh host revoke --environment $EnvironmentId --reason <motivo>.
"@
    # O checkout dos .psm1 é CRLF (.gitattributes): os here-strings herdam isso, e o host de destino é Linux.
    foreach ($key in @($files.Keys)) { if ($files[$key] -is [string]) { $files[$key] = $files[$key].Replace("`r`n", "`n") } }
    return $files
}

function Invoke-InstallerHostAdd {
    <#
    .SYNOPSIS
    `host add --name <nome> [--engine docker|podman|windows]` (T046, FR-025): ambiente cadastrado, certificado de
    cliente, identidade registrada, autorização dos administradores e o pacote cifrado em installDir/packages.
    .DESCRIPTION
    Exige agentGrpcPort (FR-022): é a única porta publicada além da borda, só com mTLS. Com o mesmo nome, reaproveita o
    ambiente e o certificado e gera outro pacote, com outra frase.
    .OUTPUTS
    EnvironmentId, PackagePath, Sha256 e PassphraseFile (quando a frase ficou em secretsDir, sem terminal).
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $Name,
        [ValidateSet('docker', 'podman', 'windows')][string] $Engine = 'podman',
        [Parameter(Mandatory)][string] $ApiImage,
        [Parameter(Mandatory)][string] $AgentImage,
        [Parameter(Mandatory)][string] $InstallerImage,
        [Parameter(Mandatory)][string] $Network,
        [string] $TrustBundle,
        [Parameter(Mandatory)][string] $KeycloakNetwork,
        [Parameter(Mandatory)][string] $KeycloakUrl,
        [bool] $Interactive = (Test-InteractiveSession),
        [string] $ScriptsRoot = (Join-Path (Get-InstallerHome) 'keycloak'),
        [string] $Cli = 'docker'
    )

    if (-not ($Answers.Contains('agentGrpcPort') -and $Answers.agentGrpcPort)) {
        Stop-Installer -Code Usage -Message ('host add exige agentGrpcPort nas respostas: é a porta gRPC com mTLS dos agentes remotos, a única publicada além ' +
            'da borda (FR-022). Defina-a e rode install de novo, que publica a porta e põe o nome público no certificado da API.')
    }
    if ($Name -notmatch $script:NamePattern) {
        Stop-Installer -Code Usage -Message "Nome de host inválido: $Name (letras minúsculas, dígitos, ponto e hífen, até 63)."
    }

    $secretsDir = $Answers.secretsDir
    $rows = Invoke-StateSql -State $State -Name 'environments-list' -Rows
    $existing = $rows | Where-Object { $_['name'] -eq $Name } | Select-Object -First 1
    if ($existing -and $existing['revoked_at']) {
        Stop-Installer -Code Usage -Message "O host $Name foi revogado; use outro nome para cadastrar de novo."
    }
    if ($existing -and -not $existing['package_generated_at']) {
        Stop-Installer -Code Usage -Message "$Name é o nome do ambiente local; escolha outro."
    }
    $id = if ($existing) { [string] $existing['environment_id'] } else { [guid]::NewGuid().ToString() }

    $algorithm = if ($Answers.certificates.agents.Contains('keyAlgorithm')) { $Answers.certificates.agents.keyAlgorithm } else { 'ecdsa-p256' }
    $ca = Initialize-LocalCertificateAuthority -SecretsDirectory $secretsDir -Algorithm $algorithm
    $agent = Initialize-AgentCertificate -SecretsDirectory $secretsDir -EnvironmentId $id -CertificateAuthority $ca.Certificate -Algorithm $algorithm `
        -RenewWarningDays ([int] $Answers.certificates.renewWarningDays)
    $operator = [string] $Answers.operator
    [void] (Invoke-EnvironmentAdmin -Image $ApiImage -Network $Network -SecretsDirectory $secretsDir -TrustBundle $TrustBundle -Cli $Cli -Arguments @(
            'environments', 'register', '--id', $id, '--name', $Name, '--host', $Name, '--stage', 'production', '--actor', $operator, '--reason', $script:Reason) `
            -Conflict "O ambiente $id já está no banco com outros dados.")
    [void] (Invoke-EnvironmentAuthorization -Answers $Answers -EnvironmentId $id -Image $InstallerImage -Network $KeycloakNetwork -BaseUrl $KeycloakUrl -ScriptsRoot $ScriptsRoot -Cli $Cli)
    [void] (Invoke-EnvironmentAdmin -Image $ApiImage -Network $Network -SecretsDirectory $secretsDir -TrustBundle $TrustBundle -Cli $Cli -Arguments @(
            'agent-identities', 'register', '--environment', $id, '--fingerprint', $agent.Fingerprint, '--actor', $operator, '--reason', $script:Reason) `
            -Conflict "O fingerprint do agente de $Name já está ligado a outro ambiente ou foi revogado.")

    # Pacote: o tar interno cifrado, mais o README de fora e o SHA-256 do cifrado.
    $inner = New-InstallerTarBundle -Files (New-HostPackageFiles -Answers $Answers -EnvironmentId $id -Engine $Engine -AgentImage $AgentImage)
    $passphrase = New-HostPassphrase
    $encrypted = Protect-HostPackage -Bytes $inner -Passphrase $passphrase
    $hash = [System.Convert]::ToHexStringLower([System.Security.Cryptography.SHA256]::HashData($encrypted))
    $outer = New-InstallerTarBundle -Files ([ordered]@{
            'README.txt'            = @"
Pacote do agente $Name ($id), gerado pelo instalador do Dokpod.

Confira e abra no host de destino (a frase foi entregue à parte, nunca vem no pacote):
  sha256sum -c agent.tar.enc.sha256
  openssl enc -d -aes-256-cbc -pbkdf2 -iter $($script:Iterations) -md sha256 -in agent.tar.enc -out agent.tar
  mkdir dokpod-agent && tar -xf agent.tar -C dokpod-agent && chmod 700 dokpod-agent
Depois siga dokpod-agent/README.txt.
"@.Replace("`r`n", "`n")
            'agent.tar.enc'         = $encrypted
            'agent.tar.enc.sha256'  = "$hash  agent.tar.enc`n"
        })
    $directory = Join-Path $Answers.installDir 'packages'
    [void] [System.IO.Directory]::CreateDirectory($directory)
    $path = Join-Path $directory "$id.tar"
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        if (-not $IsWindows) { [System.IO.File]::SetUnixFileMode($path, [System.IO.UnixFileMode]'UserRead, UserWrite') }
        $stream.Write($outer, 0, $outer.Length)
    }
    finally { $stream.Dispose() }

    $passphraseFile = $null
    if ($Interactive) {
        # Única saída da frase: direto no console, sem passar pela redação (como a senha temporária).
        [Console]::Out.WriteLine('')
        [Console]::Out.WriteLine('---- frase do pacote (mostrada só agora; entregue por outro canal) ----')
        [Console]::Out.WriteLine($passphrase)
        [Console]::Out.WriteLine('----')
    }
    else {
        # Não reaproveite $name: no PowerShell é o mesmo parâmetro $Name, o nome do host.
        $passphraseName = "packages/$id.passphrase"
        Write-InstallerSecret -SecretsDirectory $secretsDir -Name $passphraseName -Value $passphrase -Recreate:(Test-InstallerSecret -SecretsDirectory $secretsDir -Name $passphraseName)
        $passphraseFile = Resolve-SecretPath -SecretsDirectory $secretsDir -Name $passphraseName
    }

    Invoke-StateSql -State $State -Name 'environment-upsert' -Parameters @{
        environment_id = $id; name = $Name; engine = $Engine; agent_fingerprint = $agent.Fingerprint
        package_generated_at = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ'); revoked_at = $null
    }

    Write-InstallerLine -Message "Pacote do host $Name ($id): $path (SHA-256 do agent.tar.enc: $hash)."
    if ($passphraseFile) { Write-InstallerLine -Message "Frase do pacote: em $passphraseFile (0600); entregue por outro canal e apague o arquivo." }
    return [pscustomobject]@{ EnvironmentId = $id; PackagePath = $path; Sha256 = $hash; PassphraseFile = $passphraseFile; Fingerprint = $agent.Fingerprint }
}

function Invoke-InstallerHostRevoke {
    <#
    .SYNOPSIS
    `host revoke --environment <id> --reason <texto>` (T047, FR-025): revoga a identidade do agente pelo modo
    administrativo, com auditoria, e marca o ambiente como revogado no estado. A sessão ativa cai pelo reinício da API,
    feito por Invoke-InstallerHostCommand: o modo administrativo não alcança a memória dela.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $EnvironmentId,
        [Parameter(Mandatory)][string] $Reason,
        [Parameter(Mandatory)][string] $ApiImage,
        [Parameter(Mandatory)][string] $Network,
        [string] $TrustBundle,
        [string] $Cli = 'docker'
    )

    if ([string]::IsNullOrWhiteSpace($Reason)) { Stop-Installer -Code Usage -Message 'host revoke exige --reason.' }
    $rows = Invoke-StateSql -State $State -Name 'environments-list' -Rows
    $row = $rows | Where-Object { $_['environment_id'] -eq $EnvironmentId } | Select-Object -First 1
    if (-not $row) { Stop-Installer -Code Usage -Message "O ambiente $EnvironmentId não é desta instalação." }

    $result = Invoke-EnvironmentAdmin -Image $ApiImage -Network $Network -SecretsDirectory $Answers.secretsDir -TrustBundle $TrustBundle -Cli $Cli -Arguments @(
        'agent-identities', 'revoke', '--environment', $EnvironmentId, '--actor', [string] $Answers.operator, '--reason', $Reason) `
        -Conflict "A identidade do agente $EnvironmentId não foi revogada."
    $now = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
    Invoke-StateSql -State $State -Name 'environment-upsert' -Parameters @{
        environment_id = $EnvironmentId; name = $row['name']; engine = $row['engine']; agent_fingerprint = $row['agent_fingerprint']
        package_generated_at = $row['package_generated_at']; revoked_at = $now
    }

    Write-InstallerLine -Message "Ambiente $EnvironmentId ($($row['name'])) revogado: o agente perde a sessão, e a revogação está na auditoria."
    return [pscustomobject]@{ EnvironmentId = $EnvironmentId; Revoked = [bool] $result['revoked']; RevokedAt = $now }
}

Export-ModuleMember -Function Protect-HostPackage, Unprotect-HostPackage, New-HostPackageFiles, Invoke-InstallerHostAdd, Invoke-InstallerHostRevoke
