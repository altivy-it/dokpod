#Requires -Version 7.4
<#
.SYNOPSIS
Recriação por item (spec 033 T042, FR-029): traduz `recreate <item>[,<item>...]` em componentes do plano, serviços a
recriar e arquivos a invalidar, e invalida só depois da confirmação do plano.

.DESCRIPTION
Itens aceitos:
- pki:edge, pki:api: o certificado (e a chave) do papel; o serviço que o usa é recriado.
- pki:agent:<id>: o certificado do agente do ambiente local; a identidade antiga é revogada e a nova registrada.
- secret:<nome>: os segredos que o instalador sabe regenerar e propagar (Get-RecreatableSecret).
- config: os arquivos de installDir são regravados, e os serviços que os leem, recriados.
- service:<nome>: só o container do serviço.

Componente do usuário (existing) é recusado com código 2: o instalador nunca o recria nem remove (FR-029). Nada é
alterado antes da confirmação; depois dela, os arquivos invalidados são reemitidos pelas etapas normais do install.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module (Join-Path $PSScriptRoot 'Secrets.psm1')
Import-Module (Join-Path $PSScriptRoot 'State.psm1')
Import-Module (Join-Path $PSScriptRoot 'Pki.psm1')

# Serviços que leem arquivos de installDir (config) ou os certificados de cada papel.
$script:ConfigServices = 'edge', 'web', 'bff', 'api', 'agent', 'keycloak'
$script:PkiServices = @{ edge = @('edge'); api = @('api') }
$script:PkiSecrets = @{ edge = @('pki/edge.crt', 'pki/edge.key'); api = @('pki/api.pfx', 'pki/api.pass') }
# Troca da CA (T044): a raiz antiga fica aqui enquanto os agentes são reemitidos.
$script:PreviousCa = 'pki/ca-previous.crt'

function Get-RecreatableSecret {
    <#
    .SYNOPSIS
    Segredos que `recreate secret:<nome>` regenera, com o que mais muda: a etapa que o propaga e os serviços recriados.
    .DESCRIPTION
    As senhas administrativas dos bancos provisionados (dokpod-db-password, keycloak-db-password) e o client secret do
    dokpod-provisioner ficam de fora: trocá-los exige a credencial atual no próprio servidor, fora do que a reexecução
    faz. As conexões do PostgreSQL preparado pelo usuário (existing-prepared) também: elas vêm das respostas.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $secrets = [ordered]@{
        'Authentication__Keycloak__ClientSecret' = [pscustomobject]@{ Services = @('bff'); Reason = 'o client secret do dokpod-bff muda no realm' }
    }
    if ($Answers.postgres.mode -ne 'existing-prepared') {
        $secrets['ConnectionStrings__ControlPlane'] = [pscustomobject]@{ Services = @('api'); Reason = 'a senha da role de runtime muda no PostgreSQL' }
        $secrets['dokpod-owner-connection'] = [pscustomobject]@{ Services = @(); Reason = 'a senha da role dona muda no PostgreSQL' }
    }

    return $secrets
}

function Resolve-RecreateTarget {
    <#
    .SYNOPSIS
    O que cada item de `recreate` muda: componentes do plano, serviços a recriar, segredos a apagar e agentes a reemitir.
    Item desconhecido, malformado ou de componente do usuário dá código 2, antes de qualquer efeito.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Items,
        # Os serviços do compose.yaml gerado e a origem de cada componente (Get-DesiredComponents).
        [Parameter(Mandatory)][string[]] $Services,
        [Parameter(Mandatory)][object[]] $Desired,
        [string] $LocalEnvironmentId
    )

    $components = [System.Collections.Generic.List[string]]::new()
    $force = [System.Collections.Generic.List[string]]::new()
    $files = [System.Collections.Generic.List[string]]::new()
    $agents = [System.Collections.Generic.List[string]]::new()
    $add = { param($list, [string[]] $values) foreach ($value in $values) { if ($value -and -not $list.Contains($value)) { $list.Add($value) } } }
    $existing = @($Desired | Where-Object Origin -eq 'existing' | ForEach-Object Name)
    $recreatable = Get-RecreatableSecret -Answers $Answers

    $Items = @($Items | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($Items.Count -eq 0) {
        Stop-Installer -Code Usage -Message "Diga o que recriar: recreate pki:edge|pki:api|pki:agent:<id>|secret:<nome>|config|service:<nome>, ou recreate --all."
    }

    foreach ($item in $Items) {
        $kind, $name = $item -split ':', 2
        switch ($kind) {
            'pki' {
                if ($name -in 'edge', 'api') {
                    & $add $components @('pki')
                    & $add $files $script:PkiSecrets[$name]
                    & $add $force @($script:PkiServices[$name] | Where-Object { $_ -in $Services })
                }
                elseif ($name -like 'agent:*') {
                    $id = $name.Substring('agent:'.Length)
                    if ($id -ne $LocalEnvironmentId) {
                        Stop-Installer -Code Usage -Message ("pki:agent:$id não é o ambiente local ($LocalEnvironmentId). O certificado de um host remoto é " +
                            "reemitido com o pacote dele, por 'host add' (FR-025).")
                    }

                    & $add $components @('pki')
                    & $add $files @("pki/agent-$id.pfx", "pki/agent-$id.pass")
                    & $add $force @('agent')
                    & $add $agents @($id)
                }
                elseif ($name -eq 'ca') {
                    Stop-Installer -Code Usage -Message "pki:ca segue o procedimento de troca da CA, com os agentes reemitidos: use 'recreate pki:ca' sozinho (FR-030)."
                }
                else {
                    Stop-Installer -Code Usage -Message "Item desconhecido: $item. Use pki:edge, pki:api ou pki:agent:<id>."
                }
            }
            'secret' {
                if (-not $recreatable.Contains($name)) {
                    Stop-Installer -Code Usage -Message ("secret:$name não é regenerável pelo instalador. Os regeneráveis nesta instalação são: " +
                        "$(@($recreatable.Keys | ForEach-Object { "secret:$_" }) -join ', '). Certificados: pki:<papel>.")
                }

                & $add $components @('secrets')
                & $add $files @($name)
                & $add $force @($recreatable[$name].Services | Where-Object { $_ -in $Services })
            }
            'config' {
                & $add $force @($script:ConfigServices | Where-Object { $_ -in $Services })
            }
            'service' {
                if ($name -in $existing) {
                    Stop-Installer -Code Usage -Message "$name é do usuário (existing); o instalador nunca o recria nem remove (FR-029)."
                }

                if ($name -notin $Services) {
                    Stop-Installer -Code Usage -Message "O serviço $name não existe nesta instalação. Serviços: $($Services -join ', ')."
                }

                & $add $force @($name)
            }
            default {
                Stop-Installer -Code Usage -Message "Item desconhecido: $item. Use pki:<papel>, secret:<nome>, config ou service:<nome>."
            }
        }
    }

    & $add $components $force.ToArray()
    return [pscustomobject]@{
        Items      = $Items
        Components = $components.ToArray()
        Services   = $force.ToArray()
        Files      = $files.ToArray()
        Agents     = $agents.ToArray()
    }
}

function Invoke-RecreateInvalidation {
    <#
    .SYNOPSIS
    Depois da confirmação: apaga os segredos e certificados pedidos, para as etapas do install os reemitirem.
    .DESCRIPTION
    Só remove arquivos dentro de secretsDir, pelos nomes já validados. A identidade de um agente reemitido é revogada
    pela etapa environment, antes do registro da nova (Invoke-EnvironmentStep -RevokeIdentity).
    .OUTPUTS
    Os nomes removidos.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][pscustomobject] $Target
    )

    $removed = @()
    foreach ($name in $Target.Files) {
        $path = Resolve-SecretPath -SecretsDirectory $Answers.secretsDir -Name $name
        if ((Test-Path -LiteralPath $path -PathType Leaf) -and $PSCmdlet.ShouldProcess($name, 'remover para reemitir')) {
            Remove-Item -LiteralPath $path -Force
            $removed += $name
        }
    }

    return , [string[]] $removed
}

function Get-DataRemovalItem {
    <#
    .SYNOPSIS
    O que a segunda confirmação de `recreate --all` precisa nomear (T043, SC-008): os bancos provisionados, os key rings
    da API e do BFF e as identidades dos agentes. Componentes do usuário nunca entram.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $items = @()
    if ($Answers.postgres.mode -eq 'provision') { $items += 'dokpod-postgres' }
    if ($Answers.keycloak.mode -eq 'provision') { $items += 'keycloak-postgres' }
    return , [string[]] ($items + @('dokpod-api-keys', 'dokpod-bff-keys', 'agent-identities'))
}

function Resolve-DataRemoval {
    <#
    .SYNOPSIS
    A segunda confirmação de `recreate --all`: a lista exata de Get-DataRemovalItem, por --confirm-data-removal ou
    digitada no terminal. Sem ela, os dados ficam. Uma lista diferente da exata dá código 2, sem efeito (FR-029).
    .OUTPUTS
    $true quando os dados também saem.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [string[]] $Confirmed = @(),
        [bool] $Interactive = (Test-InteractiveSession),
        [scriptblock] $Prompt = { param($message) Read-Host -Prompt $message }
    )

    $items = Get-DataRemovalItem -Answers $Answers
    $expected = ($items | Sort-Object) -join ','
    $Confirmed = @($Confirmed | Where-Object { $_ })
    if ($Confirmed.Count -gt 0) {
        if ((($Confirmed | Sort-Object -Unique) -join ',') -ne $expected) {
            Stop-Installer -Code Usage -Message ("--confirm-data-removal precisa nomear exatamente os dados desta instalação: $($items -join ','). " +
                'Sem a lista exata, nada é removido.')
        }

        return $true
    }

    if (-not $Interactive) { return $false }
    $answer = & $Prompt ("Para remover também os dados (o banco, o login, os key rings e as identidades dos agentes), digite exatamente " +
        "$($items -join ','); Enter preserva os dados")
    $typed = @(([string] $answer) -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Sort-Object -Unique) -join ','
    return $typed -eq $expected
}

function Remove-InstallationData {
    <#
    .SYNOPSIS
    Depois das duas confirmações: para os containers do projeto e remove os volumes dos dados nomeados. O resto (os
    segredos, os certificados e os arquivos de installDir) fica, e as etapas do install recriam tudo do zero.
    .DESCRIPTION
    Com o Keycloak provisionado, o administrador de bootstrap volta a existir (keycloak-bootstrap-admin novo), porque o
    realm sai com o banco; a senha temporária antiga também sai, e o primeiro administrador ganha outra.
    .OUTPUTS
    Os volumes removidos.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Model,
        [Parameter(Mandatory)][string] $Project,
        [string] $Cli = 'docker'
    )

    $volumes = @{ 'dokpod-postgres' = 'dokpod-postgres-data'; 'keycloak-postgres' = 'keycloak-postgres-data'; 'dokpod-api-keys' = 'dokpod-api-keys'; 'dokpod-bff-keys' = 'dokpod-bff-keys' }
    $items = Get-DataRemovalItem -Answers $Answers
    if (-not $PSCmdlet.ShouldProcess($Project, "remover os dados: $($items -join ', ')")) { return , [string[]] @() }

    $compose = @('compose', '--file', (Join-Path $Answers.installDir 'compose.yaml'), '--project-name', $Project)
    $down = Invoke-InstallerProcess -FilePath $Cli -ArgumentList ($compose + @('down', '--remove-orphans'))
    if ($down.ExitCode -ne 0) {
        Stop-Installer -Code Error -Message "Não foi possível parar o projeto $Project (código $($down.ExitCode)): $($down.Error.Trim()) Nenhum dado foi removido."
    }

    $removed = @()
    foreach ($item in $items) {
        if (-not $volumes.ContainsKey($item) -or -not $Model.volumes.Contains($volumes[$item])) { continue }
        $name = "$($Project)_$($volumes[$item])"
        $result = Invoke-InstallerProcess -FilePath $Cli -ArgumentList @('volume', 'rm', '--force', $name)
        if ($result.ExitCode -ne 0) {
            Stop-Installer -Code Error -Message "O volume $name não foi removido (código $($result.ExitCode)): $($result.Error.Trim())"
        }

        $removed += $name
    }

    if ('keycloak-postgres' -in $items) {
        Write-InstallerSecret -SecretsDirectory $Answers.secretsDir -Name 'keycloak-bootstrap-admin' -Value (New-InstallerSecret) -Recreate
        $temporary = Resolve-SecretPath -SecretsDirectory $Answers.secretsDir -Name 'initial-admin-password'
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }

    return , [string[]] $removed
}

function Get-PreviousCaPem {
    <#
    .SYNOPSIS
    A raiz antiga durante a troca da CA (pki/ca-previous.crt), que entra junto com a nova na confiança da API (agentes) e
    dos agentes (API); $null fora da troca.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][string] $SecretsDirectory)

    if (-not (Test-InstallerSecret -SecretsDirectory $SecretsDirectory -Name $script:PreviousCa)) { return $null }
    return [System.IO.File]::ReadAllText((Resolve-SecretPath -SecretsDirectory $SecretsDirectory -Name $script:PreviousCa)).TrimEnd() + "`n"
}

function Get-CaRotationState {
    # A linha 'ca' do estado: ca_state e o início da troca (updated_at, gravado quando ela começa).
    param($State)
    $rows = Invoke-StateSql -State $State -Name 'certificates-list' -Rows
    $row = $rows | Where-Object { $_['role'] -eq 'ca' } | Select-Object -First 1
    if (-not $row) { return [pscustomobject]@{ State = 'active'; Since = $null } }
    return [pscustomobject]@{ State = $(if ($row['ca_state']) { $row['ca_state'] } else { 'active' }); Since = [DateTimeOffset]::Parse($row['updated_at']) }
}

function Set-CaRecord {
    param($State, $Certificate, [string] $Role, [string] $SecretRef, [string] $CaState)
    Invoke-StateSql -State $State -Name 'certificate-upsert' -Parameters @{
        role = $Role; origin = 'local-ca'; subject_alt_names_json = '[]'; not_after = $Certificate.NotAfter.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
        fingerprint_sha256 = Get-CertificateFingerprint -Certificate $Certificate; secret_ref = $SecretRef; ca_state = $(if ($CaState) { $CaState } else { $null })
        now = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
    }
}

function Start-CaRotation {
    <#
    .SYNOPSIS
    Primeira fase de `recreate pki:ca` (T044, FR-030, runbook troca-da-ca-dos-agentes): CA nova em pki/ca.*, a antiga
    em pki/ca-previous.crt, estado `rotating`. A confiança passa a ter as duas raízes (Get-TrustMaterial), e os
    certificados da CA local (API, borda e o agente local) são reemitidos pela nova.
    .DESCRIPTION
    Numa retomada com o estado já em `rotating`, não gera outra CA: só devolve o que reemitir de novo.
    .OUTPUTS
    O alvo da recriação: Files a reemitir, Services a recriar e Agents (o ambiente local).
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $LocalEnvironmentId
    )

    $secretsDir = $Answers.secretsDir
    $files = @("pki/agent-$LocalEnvironmentId.pfx", "pki/agent-$LocalEnvironmentId.pass")
    $services = @('agent', 'api', 'bff')
    foreach ($role in 'api', 'edge') {
        if ($Answers.certificates[$role].source -eq 'local-ca') { $files += $script:PkiSecrets[$role]; $services += $script:PkiServices[$role] }
    }

    # Retomada ou segunda rodada (hosts remotos já reemitidos): a CA nova e o agente local já estão; só falta concluir.
    if ((Get-CaRotationState -State $State).State -eq 'rotating') {
        return [pscustomobject]@{ Items = @('pki:ca'); Components = @('pki'); Services = @(); Files = @(); Agents = @() }
    }

    if ($PSCmdlet.ShouldProcess('pki/ca', 'trocar a CA local')) {
        $oldPem = [System.IO.File]::ReadAllText((Resolve-SecretPath -SecretsDirectory $secretsDir -Name 'pki/ca.crt'))
        $old = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem($oldPem)
        Write-InstallerSecret -SecretsDirectory $secretsDir -Name $script:PreviousCa -Bytes ([System.Text.Encoding]::UTF8.GetBytes($oldPem)) `
            -Recreate:(Test-InstallerSecret -SecretsDirectory $secretsDir -Name $script:PreviousCa)
        $algorithm = if ($Answers.certificates.agents.Contains('keyAlgorithm')) { $Answers.certificates.agents.keyAlgorithm } else { 'ecdsa-p256' }
        $ca = New-LocalCertificateAuthority -Algorithm $algorithm
        Write-InstallerSecret -SecretsDirectory $secretsDir -Name 'pki/ca.key' -Value (ConvertTo-PrivateKeyPem -Certificate $ca) -Recreate
        Write-InstallerSecret -SecretsDirectory $secretsDir -Name 'pki/ca.crt' -Bytes ([System.Text.Encoding]::UTF8.GetBytes((ConvertTo-CertificatePem -Certificate $ca))) -Recreate
        Set-CaRecord -State $State -Certificate $old -Role 'ca-previous' -SecretRef $script:PreviousCa
        Set-CaRecord -State $State -Certificate $ca -Role 'ca' -SecretRef 'pki/ca.key' -CaState 'rotating'
    }

    return [pscustomobject]@{ Items = @('pki:ca'); Components = @('pki') + $services; Services = $services; Files = $files; Agents = @($LocalEnvironmentId) }
}

function Get-CaRotationPending {
    <#
    .SYNOPSIS
    Hosts remotos ainda com certificado da CA antiga: ambientes não revogados, além do local, cujo pacote é anterior ao
    início da troca. Enquanto houver algum, a raiz antiga fica.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)] $State, [Parameter(Mandatory)][string] $LocalEnvironmentId)

    $since = (Get-CaRotationState -State $State).Since
    $pending = foreach ($row in Invoke-StateSql -State $State -Name 'environments-list' -Rows) {
        if ($row['environment_id'] -eq $LocalEnvironmentId -or $row['revoked_at']) { continue }
        $generated = if ($row['package_generated_at']) { [DateTimeOffset]::Parse($row['package_generated_at']) } else { $null }
        if (-not $generated -or ($since -and $generated -lt $since)) { $row['name'] }
    }

    return , [string[]] @($pending)
}

function Complete-CaRotation {
    <#
    .SYNOPSIS
    Segunda fase: com o agente local reconectado pelo certificado novo e nenhum host remoto pendente, tira a raiz
    antiga (só a nova fica na confiança dos dois lados) e volta o estado para `active`.
    .OUTPUTS
    $true quando concluiu; $false quando ainda há hosts remotos pendentes (a troca fica em `rotating`).
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $LocalEnvironmentId,
        # Devolve a conexão do agente local (Get-AgentConnection); a espera vai até TimeoutSeconds.
        [Parameter(Mandatory)][scriptblock] $Connection,
        [int] $TimeoutSeconds = 180
    )

    $rotation = Get-CaRotationState -State $State
    if ($rotation.State -ne 'rotating') { return $true }
    $pending = Get-CaRotationPending -State $State -LocalEnvironmentId $LocalEnvironmentId
    if ($pending.Count -gt 0) {
        Write-InstallerLine -Message ("Troca da CA em andamento: os hosts $($pending -join ', ') ainda usam certificado da CA antiga. Gere o pacote novo de " +
            "cada um ('host add --name <nome>'), instale-o e rode 'recreate pki:ca' de novo para tirar a raiz antiga.")
        return $false
    }

    $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ($true) {
        $session = & $Connection
        if ($session -and $session.Connected -and $session.SessionStartedAt -ge $rotation.Since.AddSeconds(-1)) { break }
        if ([datetime]::UtcNow -gt $deadline) {
            Stop-Installer -Code Error -Message ("O agente local não reconectou com o certificado da CA nova em $TimeoutSeconds s; a raiz antiga continua. " +
                "Veja 'docker compose logs agent' e rode 'recreate pki:ca' de novo.")
        }
        Start-Sleep -Seconds 3
    }

    if ($PSCmdlet.ShouldProcess($script:PreviousCa, 'tirar a raiz antiga')) {
        $ca = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem(
            [System.IO.File]::ReadAllText((Resolve-SecretPath -SecretsDirectory $Answers.secretsDir -Name 'pki/ca.crt')))
        Remove-Item -LiteralPath (Resolve-SecretPath -SecretsDirectory $Answers.secretsDir -Name $script:PreviousCa) -Force
        Set-CaRecord -State $State -Certificate $ca -Role 'ca' -SecretRef 'pki/ca.key' -CaState 'active'
    }

    return $true
}

function Get-InstallerCertificateStatus {
    <#
    .SYNOPSIS
    Validade de cada certificado da instalação (T049, FR-019): a CA local, a borda, a API e o agente de cada ambiente não
    revogado. Perto do vencimento (renewWarningDays, padrão 30) ou vencido, diz o que fazer: `recreate pki:<item>` com a
    CA local, `host add` para um host remoto e só o aviso para o certificado da organização.
    .OUTPUTS
    Role, Origin, NotAfter, DaysLeft, Status (ok, warning ou expired) e Action.
    #>
    [CmdletBinding()]
    [OutputType([pscustomobject[]])]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)] $State,
        [DateTimeOffset] $Now = [DateTimeOffset]::UtcNow
    )

    $secretsDir = $Answers.secretsDir
    $warning = if ($Answers.certificates.Contains('renewWarningDays')) { [int] $Answers.certificates.renewWarningDays } else { 30 }
    $read = {
        param([string] $name)
        $path = Resolve-SecretPath -SecretsDirectory $secretsDir -Name $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
        if ($name.EndsWith('.pfx')) {
            $pfxCredential = Read-InstallerSecret -SecretsDirectory $secretsDir -Name ($name -replace '\.pfx$', '.pass')
            return [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12([System.IO.File]::ReadAllBytes($path), $pfxCredential)
        }
        return [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([System.IO.File]::ReadAllText($path))
    }

    $items = [System.Collections.Generic.List[object]]::new()
    $items.Add(@{ Role = 'ca'; Name = 'pki/ca.crt'; Origin = 'local-ca'; Action = "recreate pki:ca" })
    foreach ($role in 'edge', 'api') {
        $origin = [string] $Answers.certificates[$role].source
        $action = if ($origin -eq 'local-ca') { "recreate pki:$role" } else { "troque o certificado da organização em certificates.$role e use recreate pki:$role" }
        $items.Add(@{ Role = $role; Name = $(if ($role -eq 'edge') { 'pki/edge.crt' } else { 'pki/api.pfx' }); Origin = $origin; Action = $action })
    }
    $environments = Invoke-StateSql -State $State -Name 'environments-list' -Rows
    foreach ($row in $environments) {
        if ($row['revoked_at'] -or $row['engine'] -notin 'docker', 'podman', 'windows') { continue }
        $id = $row['environment_id']
        $action = if ($row['package_generated_at']) { "host add --name $($row['name']) (pacote novo)" } else { "recreate pki:agent:$id" }
        $items.Add(@{ Role = "agent:$id"; Name = "pki/agent-$id.pfx"; Origin = 'local-ca'; Action = $action })
    }

    $result = foreach ($item in $items) {
        $certificate = & $read $item.Name
        if (-not $certificate) { continue }
        $days = [int] [math]::Floor(($certificate.NotAfter.ToUniversalTime() - $Now.UtcDateTime).TotalDays)
        $status = if ($days -lt 0) { 'expired' } elseif ($days -lt $warning) { 'warning' } else { 'ok' }
        [pscustomobject]@{
            Role = $item.Role; Origin = $item.Origin; NotAfter = [DateTimeOffset] $certificate.NotAfter; DaysLeft = $days; Status = $status
            Action = $(if ($status -eq 'ok') { $null } else { $item.Action })
        }
    }

    return , @($result)
}

function Write-CertificateWarning {
    <#
    .SYNOPSIS
    O aviso da reexecução (FR-019): uma linha por certificado vencido ou perto de vencer, com a ação sugerida.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]] $Status)

    foreach ($item in $Status | Where-Object Status -ne 'ok') {
        $when = if ($item.Status -eq 'expired') { "venceu há $(-$item.DaysLeft) dias" } else { "vence em $($item.DaysLeft) dias" }
        Write-InstallerLine -ToStandardError -Message "Aviso: o certificado $($item.Role) ($($item.Origin)) $when; $($item.Action)."
    }
}

Export-ModuleMember -Function Get-RecreatableSecret, Resolve-RecreateTarget, Invoke-RecreateInvalidation, Get-DataRemovalItem, Resolve-DataRemoval,
Remove-InstallationData, Get-PreviousCaPem, Get-CaRotationState, Start-CaRotation, Get-CaRotationPending, Complete-CaRotation,
Get-InstallerCertificateStatus, Write-CertificateWarning
