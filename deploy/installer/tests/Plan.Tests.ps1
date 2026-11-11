#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T020 (FR-026, FR-027, FR-029, D-007): plano a partir do observado, com motivo e efeito colateral.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Plan.psm1') -Force

    function New-Answers {
        param([hashtable] $Override = @{})
        $answers = @{ postgres = @{ mode = 'provision' }; keycloak = @{ mode = 'provision' }; edge = @{ mode = 'provision' } }
        foreach ($key in $Override.Keys) { $answers[$key] = $Override[$key] }
        return $answers
    }

    function New-Hashes {
        param([string[]] $Services = @('dokpod-postgres', 'keycloak-postgres', 'keycloak', 'edge', 'web', 'bff', 'api', 'agent'))
        $hashes = [ordered]@{}
        foreach ($name in $Services) { $hashes[$name] = "hash-$name" }
        return $hashes
    }

    function New-Converged {
        param($Desired)
        $observed = [ordered]@{}
        foreach ($component in $Desired) { $observed[$component.Name] = @{ Exists = $true; Hash = $component.Hash; Running = $true; Pending = $null } }
        return $observed
    }

    function Get-Action {
        param($Plan, [string] $Component)
        return ($Plan | Where-Object Component -eq $Component).Action
    }
}

Describe 'Plano' {
    It 'tudo novo: create em todos os componentes' {
        $desired = Get-DesiredComponents -Answers (New-Answers) -ServiceHashes (New-Hashes)
        $plan = New-InstallerPlan -Desired $desired -Observed @{}

        @($plan | Where-Object Action -ne 'create').Count | Should -Be 0
        $plan.Component | Should -Contain 'migrations'
        $plan.Component | Should -Contain 'realm'
    }

    It 'reexecução sem mudança: keep em tudo e convergido (FR-026)' {
        $desired = Get-DesiredComponents -Answers (New-Answers) -ServiceHashes (New-Hashes)
        $plan = New-InstallerPlan -Desired $desired -Observed (New-Converged $desired)

        Test-InstallerPlanConverged -Plan $plan | Should -BeTrue
        @($plan | Where-Object SideEffect).Count | Should -Be 0
    }

    It 'hash diferente vira update com efeito colateral; container parado também' {
        $desired = Get-DesiredComponents -Answers (New-Answers) -ServiceHashes (New-Hashes)
        $observed = New-Converged $desired
        $observed['api'].Hash = 'hash-antigo'
        $observed['bff'].Running = $false

        $plan = New-InstallerPlan -Desired $desired -Observed $observed

        Get-Action $plan 'api' | Should -Be 'update'
        ($plan | Where-Object Component -eq 'api').SideEffect | Should -Match 'agentes reconectam'
        Get-Action $plan 'bff' | Should -Be 'update'
        Get-Action $plan 'web' | Should -Be 'keep'
    }

    It 'migrations e realm pendentes viram update com o motivo observado' {
        $desired = Get-DesiredComponents -Answers (New-Answers) -ServiceHashes (New-Hashes)
        $observed = New-Converged $desired
        $observed['migrations'].Pending = '2 migrations pendentes'
        $observed['realm'].Pending = 'redirect URI do dokpod-bff divergente'

        $plan = New-InstallerPlan -Desired $desired -Observed $observed

        ($plan | Where-Object Component -eq 'migrations').Reason | Should -Be '2 migrations pendentes'
        Get-Action $plan 'realm' | Should -Be 'update'
    }

    It 'componente do usuário nunca é recriado nem removido' {
        $answers = New-Answers @{ postgres = @{ mode = 'existing-create' }; keycloak = @{ mode = 'existing' }; edge = @{ mode = 'existing' } }
        $desired = Get-DesiredComponents -Answers $answers -ServiceHashes (New-Hashes @('web', 'bff', 'api', 'agent'))

        ($desired | Where-Object Origin -eq 'existing').Name | Sort-Object | Should -Be @('dokpod-postgres', 'edge', 'keycloak')
        $desired.Name | Should -Not -Contain 'keycloak-postgres'
        $all = New-InstallerPlan -Desired $desired -Observed @{} -RecreateAll -ConfirmDataRemoval 'dokpod-postgres'
        Get-Action $all 'dokpod-postgres' | Should -Be 'keep'
        Get-Action $all 'edge' | Should -Be 'keep'
        { New-InstallerPlan -Desired $desired -Observed @{} -Recreate 'keycloak' } | Should -Throw -ExpectedMessage '*nunca o recria*'
    }

    It 'recriação de um componente muda só ele' {
        $desired = Get-DesiredComponents -Answers (New-Answers) -ServiceHashes (New-Hashes)
        $plan = New-InstallerPlan -Desired $desired -Observed (New-Converged $desired) -Recreate 'edge'

        @($plan | Where-Object Action -ne 'keep').Component | Should -Be @('edge')
        Get-Action $plan 'edge' | Should -Be 'recreate'
    }

    It 'recriação total preserva dados sem a segunda confirmação nomeada (FR-029)' {
        $desired = Get-DesiredComponents -Answers (New-Answers) -ServiceHashes (New-Hashes)
        $observed = New-Converged $desired

        $refused = New-InstallerPlan -Desired $desired -Observed $observed -RecreateAll
        Get-Action $refused 'dokpod-postgres' | Should -Be 'keep'
        Get-Action $refused 'keycloak-postgres' | Should -Be 'keep'
        Get-Action $refused 'api' | Should -Be 'recreate'

        $confirmed = New-InstallerPlan -Desired $desired -Observed $observed -RecreateAll -ConfirmDataRemoval 'dokpod-postgres'
        Get-Action $confirmed 'dokpod-postgres' | Should -Be 'remove'
        Get-Action $confirmed 'keycloak-postgres' | Should -Be 'keep'
    }

    It 'lista de recriação nula ou vazia não recria nada' {
        $desired = Get-DesiredComponents -Answers (New-Answers) -ServiceHashes (New-Hashes)
        $plan = New-InstallerPlan -Desired $desired -Observed (New-Converged $desired) -Recreate $null

        Test-InstallerPlanConverged -Plan $plan | Should -BeTrue
    }

    It 'recriação de componente desconhecido é uso inválido' {
        $desired = Get-DesiredComponents -Answers (New-Answers) -ServiceHashes (New-Hashes)
        { New-InstallerPlan -Desired $desired -Observed @{} -Recreate 'inventado' } | Should -Throw -ExpectedMessage '*desconhecido*'
    }

    It 'retomada depois de interrupção: o que já existe com o hash certo fica, o resto é criado (FR-027)' {
        $desired = Get-DesiredComponents -Answers (New-Answers) -ServiceHashes (New-Hashes)
        $observed = [ordered]@{
            'secrets' = @{ Exists = $true; Pending = $null }; 'pki' = @{ Exists = $true; Pending = $null }
            'dokpod-postgres' = @{ Exists = $true; Hash = 'hash-dokpod-postgres'; Running = $true }
        }

        $plan = New-InstallerPlan -Desired $desired -Observed $observed

        Get-Action $plan 'pki' | Should -Be 'keep'
        Get-Action $plan 'dokpod-postgres' | Should -Be 'keep'
        Get-Action $plan 'api' | Should -Be 'create'
        Get-Action $plan 'migrations' | Should -Be 'create'
    }

    It 'a tabela do plano sai redigida' {
        Register-InstallerSecret -Value 'canario-033-plano'
        $plan = @([pscustomobject]@{ Component = 'api'; Origin = 'provisioned'; Action = 'update'; Reason = 'canario-033-plano'; SideEffect = 'x'; Hash = 'h' })

        Format-InstallerPlan -Plan $plan | Should -Not -Match 'canario-033-plano'
    }

    It 'lê do engine o hash gravado no rótulo de cada serviço' {
        $cli = {
            param([string[]] $arguments)
            '{"Names":"dokpod-api-1","State":"running","Labels":"com.docker.compose.project=dokpod,com.docker.compose.service=api,io.dokpod.installer.config-hash=abc"}'
            '{"Names":"dokpod-bff-1","State":"exited","Labels":"com.docker.compose.project=dokpod,com.docker.compose.service=bff,io.dokpod.installer.config-hash=def"}'
        }

        $observed = Get-ObservedServices -Cli { param($a) (& $cli $a) -join "`n" }.GetNewClosure()

        $observed.api.Hash | Should -Be 'abc'
        $observed.api.Running | Should -BeTrue
        $observed.bff.Running | Should -BeFalse
    }

    It 'container do projeto dokpod sem o rótulo do instalador bloqueia com código 3' {
        $cli = { param($a) '{"Names":"dokpod_web_1","State":"running","Labels":"com.docker.compose.project=dokpod,com.docker.compose.service=web"}' }

        { Get-ObservedServices -Cli $cli } | Should -Throw -ExpectedMessage '*dokpod_web_1*nada foi alterado*'
        try { Get-ObservedServices -Cli $cli } catch { $_.Exception.ExitCode | Should -Be 3 }
    }
}
