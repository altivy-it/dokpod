<#
.SYNOPSIS
Provisiona a autorização de um ambiente no Keycloak, de dentro de um container avulso do instalador (spec 033 T031).

.DESCRIPTION
Não é chamado pelo operador. O Environments.psm1 entrega este script, o provision-environment-authorization.ps1 e um
input.json com o client secret do dokpod-provisioner por stdin, num tar extraído para um tmpfs do container, que fica
só na rede interna do Keycloak. Assim o segredo não aparece em argumento, variável de ambiente do container ou
`docker inspect`.

Executa provision-environment-authorization.ps1 uma vez por scope, para o grupo pedido. A lista de scopes é o
ValidateSet do parâmetro -Scope do próprio script (fonte única, D-001).

A última linha da saída é `@@dokpod-result@@` seguida de um JSON sem segredos, com os scopes provisionados e os que
mudaram algo no Keycloak. Códigos de saída: 0 sucesso, 1 erro.

.PARAMETER InputPath
Caminho do input.json no tmpfs. É apagado logo depois de lido.

.EXAMPLE
pwsh -File Environments.Sidecar.ps1 /run/dokpod-kc/input.json

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
[Environment]::SetEnvironmentVariable('DOKPOD_PROVISIONER_CLIENT_SECRET', $settings.secrets.provisionerSecret, 'Process')

$script = Join-Path $PSScriptRoot 'provision-environment-authorization.ps1'
$scopes = @((Get-Command $script).Parameters['Scope'].Attributes |
        Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] } | ForEach-Object { $_.ValidValues })
if ($scopes.Count -eq 0) { throw 'O provision-environment-authorization.ps1 não declara os scopes no ValidateSet de -Scope.' }

$changed = [System.Collections.Generic.List[string]]::new()
foreach ($scope in $scopes) {
    $parameters = @{
        EnvironmentId = $settings.environmentId
        GroupPath     = $settings.groupPath
        Scope         = $scope
        BaseUrl       = $settings.baseUrl
        Realm         = $settings.realm
        InternalHttp  = [bool] $settings.internalHttp
    }
    $lines = @(& $script @parameters)
    # Mudou algo: recurso, policy ou permission criados, scope novo no recurso ou permission convertida. A permission
    # existente é sempre regravada com os mesmos dados ("Permission UMA atualizada"), o que não conta como mudança.
    if (@($lines | Where-Object { "$_" -match 'criad[ao]:|atualizado com scope|convertida para scope' }).Count -gt 0) { $changed.Add($scope) }
}

$result = @{ scopes = $scopes; changed = $changed.ToArray() }
[Console]::Out.WriteLine('@@dokpod-result@@' + ($result | ConvertTo-Json -Compress))
