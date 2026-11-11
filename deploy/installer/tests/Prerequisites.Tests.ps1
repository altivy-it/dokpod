#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T021 (FR-003, D-015): engine rootless e versões, portas, disco, DNS, serviços existentes e avisos.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Prerequisites.psm1') -Force

    function New-Cli {
        param([string] $Kind = 'podman', [string] $Version = '5.7.0', [bool] $Rootless = $true, [string] $Compose = '5.6.1')
        $versionJson = if ($Kind -eq 'podman') {
            @{ Server = @{ Version = $Version; Components = @(@{ Name = 'Podman Engine'; Version = $Version }) } } | ConvertTo-Json -Depth 6
        }
        else {
            @{ Server = @{ Version = $Version; Components = @(@{ Name = 'Engine'; Version = $Version }) } } | ConvertTo-Json -Depth 6
        }
        $security = if ($Rootless) { @('name=seccomp,profile=builtin', 'name=rootless', 'name=cgroupns') } else { @('name=seccomp,profile=builtin') }
        $infoJson = @{ ServerVersion = $Version; SecurityOptions = $security } | ConvertTo-Json
        return {
            param([string[]] $arguments)
            switch ($arguments[0]) {
                'version' { $versionJson }
                'info' { $infoJson }
                'compose' { $Compose }
            }
        }.GetNewClosure()
    }

    function New-Answers {
        param([hashtable] $Override = @{})
        $answers = @{
            engine    = 'podman-rootless'
            publicUrl = 'https://dokpod.lab.test'
            postgres  = @{ mode = 'provision' }
            keycloak  = @{ mode = 'provision' }
            edge      = @{ mode = 'provision'; httpsPort = 8443 }
        }
        foreach ($key in $Override.Keys) { $answers[$key] = $Override[$key] }
        return $answers
    }

    $script:goodHost = [pscustomobject]@{ UnprivilegedPortStart = 1024; Linger = 'yes'; ListeningPorts = @(22, 5432); FreeBytes = 50GB; PortDriver = $null }
    $script:resolve = { param($name) @('192.0.2.10') }
    $script:reachable = { param($hostName, $port) $true }

    function Get-Status {
        param($Results, [string] $Check)
        return ($Results | Where-Object Check -eq $Check).Status
    }
}

Describe 'Engine' {
    It 'aceita Podman 5 rootless' {
        $engine = Get-EngineFacts -Cli (New-Cli)
        $results = Test-InstallerPrerequisites -Answers (New-Answers) -Engine $engine -HostFacts $script:goodHost -Resolve $script:resolve

        $engine.Kind | Should -Be 'podman'
        $engine.Rootless | Should -BeTrue
        Get-Status $results 'engine' | Should -Be 'ok'
        Get-Status $results 'compose' | Should -Be 'ok'
        @($results | Where-Object Status -eq 'failed').Count | Should -Be 0
    }

    It 'recusa engine rootful pela API, com a orientação do rootless' {
        $results = Test-InstallerPrerequisites -Answers (New-Answers) -Engine (Get-EngineFacts -Cli (New-Cli -Rootless $false)) -HostFacts $script:goodHost -Resolve $script:resolve

        ($results | Where-Object Check -eq 'engine').Message | Should -Match 'rootful.*D-015'
        { Assert-InstallerPrerequisites -Results $results } | Should -Throw
    }

    It 'recusa versão abaixo do mínimo e engine diferente do pedido: <Name>' -ForEach @(
        @{ Name = 'Podman 4'; Kind = 'podman'; Version = '4.9.3'; Engine = 'podman-rootless' }
        @{ Name = 'Docker 26'; Kind = 'docker'; Version = '26.1.0'; Engine = 'docker-rootless' }
        @{ Name = 'Docker no lugar de Podman'; Kind = 'docker'; Version = '28.5.2'; Engine = 'podman-rootless' }
    ) {
        $results = Test-InstallerPrerequisites -Answers (New-Answers @{ engine = $Engine }) -Engine (Get-EngineFacts -Cli (New-Cli -Kind $Kind -Version $Version)) -HostFacts $script:goodHost -Resolve $script:resolve

        Get-Status $results 'engine' | Should -Be 'failed'
    }

    It 'socket sem resposta falha antes do resto' {
        $engine = Get-EngineFacts -Cli { param($arguments) $null }
        $results = Test-InstallerPrerequisites -Answers (New-Answers) -Engine $engine -HostFacts $script:goodHost

        $results.Count | Should -Be 1
        Get-Status $results 'engine' | Should -Be 'failed'
    }
}

Describe 'Host' {
    BeforeAll { $script:engine = Get-EngineFacts -Cli (New-Cli) }

    It 'porta abaixo do limite sem privilégio falha com as três saídas e sem alterar o sysctl' {
        $results = Test-InstallerPrerequisites -Answers (New-Answers @{ edge = @{ mode = 'provision'; httpsPort = 443 } }) -Engine $script:engine -HostFacts $script:goodHost -Resolve $script:resolve
        $message = ($results | Where-Object Check -eq 'port:edge.httpsPort').Message

        Get-Status $results 'port:edge.httpsPort' | Should -Be 'failed'
        $message | Should -Match '8443'
        $message | Should -Match 'edge.mode: existing'
        $message | Should -Match 'não altera o sysctl'
    }

    It 'porta em uso falha, mas a já publicada por esta instalação não' {
        $busy = [pscustomobject]@{ UnprivilegedPortStart = 80; Linger = 'yes'; ListeningPorts = @(8443); FreeBytes = 50GB; PortDriver = $null }

        Get-Status (Test-InstallerPrerequisites -Answers (New-Answers) -Engine $script:engine -HostFacts $busy -Resolve $script:resolve) 'port:edge.httpsPort' | Should -Be 'failed'
        Get-Status (Test-InstallerPrerequisites -Answers (New-Answers) -Engine $script:engine -HostFacts $busy -Resolve $script:resolve -OwnedPorts 8443) 'port:edge.httpsPort' | Should -Be 'ok'
    }

    It 'fato ausente vira aviso, nunca aprovação' {
        $unknown = [pscustomobject]@{ UnprivilegedPortStart = $null; Linger = $null; ListeningPorts = $null; FreeBytes = $null; PortDriver = $null }
        $results = Test-InstallerPrerequisites -Answers (New-Answers) -Engine $script:engine -HostFacts $unknown -Resolve $script:resolve

        Get-Status $results 'port:edge.httpsPort' | Should -Be 'warning'
        Get-Status $results 'disk' | Should -Be 'warning'
        Get-Status $results 'linger' | Should -Be 'warning'
    }

    It 'pouco disco, DNS sem resposta e serviço existente inalcançável falham' {
        $smallDisk = [pscustomobject]@{ UnprivilegedPortStart = 1024; Linger = 'no'; ListeningPorts = @(); FreeBytes = 1GB; PortDriver = $null }
        $answers = New-Answers @{
            postgres = @{ mode = 'existing-create'; host = 'db.lab.test'; port = 5432 }
            keycloak = @{ mode = 'existing'; url = 'https://sso.lab.test' }
        }
        $results = Test-InstallerPrerequisites -Answers $answers -Engine $script:engine -HostFacts $smallDisk -Resolve { param($name) throw 'nx' } -Probe { param($h, $p) $false }

        Get-Status $results 'disk' | Should -Be 'failed'
        Get-Status $results 'dns' | Should -Be 'failed'
        Get-Status $results 'postgres' | Should -Be 'failed'
        Get-Status $results 'keycloak' | Should -Be 'failed'
        ($results | Where-Object Check -eq 'linger').Message | Should -Match 'loginctl enable-linger'
    }

    It 'IP de origem: Podman ok; Docker rootless com builtin avisa; com slirp4netns ok' {
        $docker = Get-EngineFacts -Cli (New-Cli -Kind docker -Version '28.5.2')
        $answers = New-Answers @{ engine = 'docker-rootless' }

        Get-Status (Test-InstallerPrerequisites -Answers (New-Answers) -Engine $script:engine -HostFacts $script:goodHost -Resolve $script:resolve) 'source-ip' | Should -Be 'ok'
        Get-Status (Test-InstallerPrerequisites -Answers $answers -Engine $docker -HostFacts $script:goodHost -Resolve $script:resolve) 'source-ip' | Should -Be 'warning'
        $slirp = [pscustomobject]@{ UnprivilegedPortStart = 1024; Linger = 'yes'; ListeningPorts = @(); FreeBytes = 50GB; PortDriver = 'slirp4netns' }
        Get-Status (Test-InstallerPrerequisites -Answers $answers -Engine $docker -HostFacts $slirp -Resolve $script:resolve) 'source-ip' | Should -Be 'ok'
    }

    It 'lê os fatos do host das variáveis do install.sh' {
        $env:DOKPOD_HOST_UNPRIVILEGED_PORT_START = '1024'; $env:DOKPOD_HOST_LINGER = 'no'; $env:DOKPOD_HOST_LISTENING_PORTS = '22,443'; $env:DOKPOD_HOST_FREE_BYTES = '123'
        try {
            $facts = Get-HostFacts
            $facts.UnprivilegedPortStart | Should -Be 1024
            $facts.ListeningPorts | Should -Be @(22, 443)
            $facts.FreeBytes | Should -Be 123
        }
        finally {
            Remove-Item Env:DOKPOD_HOST_UNPRIVILEGED_PORT_START, Env:DOKPOD_HOST_LINGER, Env:DOKPOD_HOST_LISTENING_PORTS, Env:DOKPOD_HOST_FREE_BYTES
        }
    }
}
