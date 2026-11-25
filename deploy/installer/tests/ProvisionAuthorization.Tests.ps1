#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Bug provisionamento-uma-policy-existente: o provision-environment-authorization.ps1, que o instalador executa uma vez
# por scope, contra um Admin API falso do Keycloak. A segunda concessão ao mesmo grupo reaproveita a policy, a
# reexecução do mesmo scope atualiza a permission e o nome do grupo segue o padrão aceito.

BeforeAll {
    # Os scripts do checkout, quando montados em /src (pester.sh), vencem os copiados na imagem.
    $scripts = if (Test-Path '/src/tools/scripts/provision-environment-authorization.ps1') { '/src/tools/scripts' } else { Join-Path $env:DOKPOD_INSTALLER_HOME 'keycloak' }
    $script:provision = Join-Path $scripts 'provision-environment-authorization.ps1'
    $script:environmentId = '6f1c2a4e-0d3b-4b8a-9c7e-2a5d8e1f0b36'
    $script:savedAppData = $env:APPDATA
    $env:APPDATA = $null
    $env:DOKPOD_PROVISIONER_CLIENT_SECRET = 'canario-provisioner-falso'

    # Estado do Keycloak falso. Nome duplicado no POST da policy responde 403, como na T038 da spec 026.
    $script:state = [hashtable]::Synchronized(@{
            resources = [System.Collections.ArrayList]::Synchronized([System.Collections.ArrayList]::new())
            policies = [System.Collections.ArrayList]::Synchronized([System.Collections.ArrayList]::new())
            permissions = [System.Collections.ArrayList]::Synchronized([System.Collections.ArrayList]::new())
            log = [System.Collections.ArrayList]::Synchronized([System.Collections.ArrayList]::new())
        })
    $script:port = Get-Random -Minimum 20000 -Maximum 40000
    $script:listener = [System.Net.HttpListener]::new()
    $script:listener.Prefixes.Add("http://localhost:$script:port/")
    $script:listener.Start()
    $script:server = Start-ThreadJob -ArgumentList $script:listener, $script:state -ScriptBlock {
        param($listener, $state)
        $authz = '/admin/realms/dokpod/clients/c1/authz/resource-server'
        while ($listener.IsListening) {
            try { $context = $listener.GetContext() } catch { break }
            $request = $context.Request
            $path = $request.Url.AbsolutePath
            $name = $request.QueryString['name']
            $body = if ($request.HasEntityBody) { [System.IO.StreamReader]::new($request.InputStream).ReadToEnd() | ConvertFrom-Json } else { $null }
            [void] $state.log.Add("$($request.HttpMethod) $path")
            $status = 200
            $result = switch -Regex ("$($request.HttpMethod) $path") {
                '^POST /realms/dokpod/protocol/openid-connect/token$' { @{ access_token = 'token-falso' } }
                '^GET /admin/realms/dokpod/clients$' { , @(@{ id = 'c1'; clientId = 'dokpod-api' }) }
                "^GET $authz/scope$" { , @(@{ id = "scope-$name"; name = $name }) }
                "^GET $authz/resource$" { , @($state.resources | Where-Object name -eq $name) }
                "^POST $authz/resource$" {
                    $resource = @{ _id = "resource-$($state.resources.Count + 1)"; name = $body.name; scopes = @($body.scopes) }
                    [void] $state.resources.Add($resource); $resource
                }
                "^GET $authz/resource/([^/]+)$" { $id = $Matches[1]; $state.resources | Where-Object _id -eq $id }
                "^PUT $authz/resource/([^/]+)$" { $id = $Matches[1]; ($state.resources | Where-Object _id -eq $id).scopes = @($body.scopes); $null }
                '^GET /admin/realms/dokpod/groups$' { , @(@{ id = 'g-root'; name = 'dokpod'; path = '/dokpod' }) }
                '^GET /admin/realms/dokpod/groups/g-root/children$' {
                    $search = $request.QueryString['search']
                    if ($search -eq 'ausente') { , @() } else { , @(@{ id = "g-$search"; name = $search; path = "/dokpod/$search" }) }
                }
                "^GET $authz/policy$" { , @($state.policies | Where-Object name -eq $name) }
                "^POST $authz/policy/group$" {
                    if ($state.policies | Where-Object name -eq $body.name) { $status = 403; $null }
                    else { $policy = @{ id = "policy-$($state.policies.Count + 1)"; name = $body.name; type = 'group' }; [void] $state.policies.Add($policy); $policy }
                }
                "^GET $authz/permission$" { , @($state.permissions | Where-Object name -eq $name) }
                "^POST $authz/permission/scope$" {
                    if ($state.permissions | Where-Object name -eq $body.name) { $status = 409; $null }
                    else {
                        $permission = @{ id = "permission-$($state.permissions.Count + 1)"; name = $body.name; type = 'scope'; resources = @($body.resources); scopes = @($body.scopes); policies = @($body.policies) }
                        [void] $state.permissions.Add($permission); $permission
                    }
                }
                "^GET $authz/policy/([^/]+)/associatedPolicies$" { $id = $Matches[1]; , @(($state.permissions | Where-Object id -eq $id).policies | ForEach-Object { @{ id = $_ } }) }
                "^GET $authz/permission/([^/]+)/resources$" { $id = $Matches[1]; , @(($state.permissions | Where-Object id -eq $id).resources | ForEach-Object { @{ _id = $_ } }) }
                "^GET $authz/permission/([^/]+)/scopes$" { $id = $Matches[1]; , @(($state.permissions | Where-Object id -eq $id).scopes | ForEach-Object { @{ id = $_ } }) }
                "^PUT $authz/permission/scope/([^/]+)$" {
                    $id = $Matches[1]; $permission = $state.permissions | Where-Object id -eq $id
                    $permission.resources = @($body.resources); $permission.scopes = @($body.scopes); $permission.policies = @($body.policies); $null
                }
                default { $status = 404; $null }
            }
            $context.Response.StatusCode = $status
            if ($null -ne $result) {
                $json = if ($result -is [array]) { ConvertTo-Json -InputObject $result -Depth 10 -Compress } else { $result | ConvertTo-Json -Depth 10 -Compress }
                $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
                $context.Response.ContentType = 'application/json'
                $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            }
            $context.Response.Close()
        }
    }

    function Invoke-Provision {
        param([string] $Scope, [string] $GroupPath = '/dokpod/plantao')
        @(& $script:provision -EnvironmentId $script:environmentId -GroupPath $GroupPath -Scope $Scope -BaseUrl "http://localhost:$script:port")
    }

    function Get-Requests {
        param([string] $Pattern)
        @($script:state.log | Where-Object { $_ -match $Pattern })
    }
}

AfterAll {
    $script:listener.Stop()
    $script:server | Remove-Job -Force
    Remove-Item Env:DOKPOD_PROVISIONER_CLIENT_SECRET -ErrorAction SilentlyContinue
    $env:APPDATA = $script:savedAppData
}

Describe 'Provisionamento UMA com policy existente' {
    It 'o segundo scope para o mesmo grupo reaproveita a policy e cria só a permission nova' {
        $first = Invoke-Provision -Scope 'environment:read'
        $first | Should -Contain 'Policy UMA criada: environment-6f1c2a4e-0d3b-4b8a-9c7e-2a5d8e1f0b36-group-plantao'

        $second = Invoke-Provision -Scope 'change:override'

        $second | Should -Contain 'Policy UMA já existe: environment-6f1c2a4e-0d3b-4b8a-9c7e-2a5d8e1f0b36-group-plantao'
        $second | Should -Contain 'Permission UMA criada: environment-6f1c2a4e-0d3b-4b8a-9c7e-2a5d8e1f0b36-change-override'
        (Get-Requests '^POST .*/policy/group$').Count | Should -Be 1
        $script:state.policies.Count | Should -Be 1
        @($script:state.permissions.name) | Should -Be @(
            'environment-6f1c2a4e-0d3b-4b8a-9c7e-2a5d8e1f0b36-environment-read',
            'environment-6f1c2a4e-0d3b-4b8a-9c7e-2a5d8e1f0b36-change-override')
        @($script:state.resources[0].scopes.name) | Should -Be @('environment:read', 'change:override')
    }

    It 'a reexecução do mesmo scope atualiza a permission existente sem recriá-la' {
        $before = (Get-Requests '^POST .*/permission/scope$').Count

        $output = Invoke-Provision -Scope 'environment:read'

        $output | Should -Contain 'Permission UMA atualizada: environment-6f1c2a4e-0d3b-4b8a-9c7e-2a5d8e1f0b36-environment-read'
        (Get-Requests '^POST .*/permission/scope$').Count | Should -Be $before
        (Get-Requests '^PUT .*/permission/scope/permission-1$').Count | Should -Be 1
        @($script:state.permissions[0].policies) | Should -Be @('policy-1')
    }

    It 'lista vazia do Keycloak continua falhando como zero resultados' {
        { Invoke-Provision -Scope 'environment:read' -GroupPath '/dokpod/ausente' } |
            Should -Throw -ExpectedMessage 'grupo /dokpod/ausente deve resolver exatamente um resultado; encontrados 0.'
    }
}

Describe 'Nome do grupo' {
    It 'aceita dígitos e hífens entre letras: <_>' -ForEach @('/dokpod/plantao-2', '/dokpod/operador-teste', '/dokpod/n1') {
        $output = Invoke-Provision -Scope 'environment:read' -GroupPath $_

        $output | Should -Contain "Policy UMA criada: environment-6f1c2a4e-0d3b-4b8a-9c7e-2a5d8e1f0b36-group-$($_.Split('/')[-1])"
    }

    It 'recusa <_>' -ForEach @('/dokpod/-plantao', '/dokpod/plantao-', '/dokpod/a--b', '/dokpod/a_b', '/dokpod/a/b', '/outro/plantao') {
        { & $script:provision -EnvironmentId $script:environmentId -GroupPath $_ -DryRun } | Should -Throw
    }
}
