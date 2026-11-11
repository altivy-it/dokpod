#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T019 (FR-034, FR-044, D-014): estado em SQLite, schema forward-only, trava, etapas, backup, retenção,
# relatório e nenhum segredo gravado.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/State.psm1') -Force
    Import-InstallerLibraries
}

Describe 'Estado da instalação' {
    BeforeEach {
        Clear-InstallerSecrets
        $script:install = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-state-$([guid]::NewGuid().ToString('N'))"
        $script:state = Open-InstallerState -InstallDirectory $script:install
    }

    AfterEach {
        Close-InstallerState -State $script:state
        [Microsoft.Data.Sqlite.SqliteConnection]::ClearAllPools()
        Remove-Item -LiteralPath $script:install -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'cria o banco com o schema atual, WAL e chaves estrangeiras' {
        Invoke-StateSql -State $script:state -Name 'user-version-get' -Scalar | Should -Be 1
        Invoke-StateSql -State $script:state -Name 'integrity-check' -Scalar | Should -Be 'ok'
        $script:state.Connection.State | Should -Be 'Open'
        (Get-Item (Join-Path $script:install 'state/installation.db')).Length | Should -BeGreaterThan 0
    }

    It 'a versão instalada só aumenta' {
        Set-InstallationRecord -State $script:state -Version '0.8.0' -Edition ce -Engine podman-rootless -PublicUrl 'https://dokpod.lab.test'
        Set-InstallationRecord -State $script:state -Version '0.9.0' -Edition ce -Engine podman-rootless -PublicUrl 'https://dokpod.lab.test'

        (Get-InstallationRecord -State $script:state).version | Should -Be '0.9.0'
        { Set-InstallationRecord -State $script:state -Version '0.8.1' -Edition ce -Engine podman-rootless -PublicUrl 'https://dokpod.lab.test' } |
            Should -Throw -ExpectedMessage '*rollback*'
    }

    It 'recusa estado de schema mais novo com código 2' {
        Invoke-StateSql -State $script:state -Name 'x' -Text 'PRAGMA user_version = 99;'
        Close-InstallerState -State $script:state

        $failure = $null
        try { $script:state = Open-InstallerState -InstallDirectory $script:install } catch { $failure = $_ }
        $failure.Exception.ExitCode | Should -Be 2
        $script:state = Open-InstallerState -InstallDirectory (Join-Path $script:install 'outro')
    }

    It 'trava ocupada dá código 7 e trava velha só sai com confirmação' {
        Enter-InstallerRunLock -State $script:state -RunId 'run-a' -ContainerName 'dokpod-installer-run-a'

        $busy = $null
        try { Enter-InstallerRunLock -State $script:state -RunId 'run-b' -ContainerName 'b' -ContainerExists { $true } } catch { $busy = $_ }
        $busy.Exception.ExitCode | Should -Be 7

        $stale = $null
        try { Enter-InstallerRunLock -State $script:state -RunId 'run-b' -ContainerName 'b' } catch { $stale = $_ }
        $stale.Exception.ExitCode | Should -Be 7
        $stale.Exception.Message | Should -Match 'confirme a liberação'

        Enter-InstallerRunLock -State $script:state -RunId 'run-b' -ContainerName 'b' -ConfirmStaleRelease
        (Invoke-StateSql -State $script:state -Name 'lock-get' -Rows)[0].run_id | Should -Be 'run-b'
        Exit-InstallerRunLock -State $script:state -RunId 'run-b'
        (Invoke-StateSql -State $script:state -Name 'lock-get' -Rows).Count | Should -Be 0
    }

    It 'registra etapas e o relatório sai do banco, sem segredos' {
        $canary = 'canario-033-estado-' + [guid]::NewGuid().ToString('N')
        Register-InstallerSecret -Value $canary
        Set-InstallationRecord -State $script:state -Version '0.8.0' -Edition business -Engine docker-rootless -PublicUrl 'https://dokpod.lab.test'
        $run = Start-InstallerRun -State $script:state -Command 'install'
        Add-InstallerRunStep -State $script:state -RunId $run -Step 'pki'
        Add-InstallerRunStep -State $script:state -RunId $run -Step 'migrations' -Status failed
        Complete-InstallerRun -State $script:state -RunId $run -Outcome failed -ExitCode 1

        $report = Export-InstallerRunReport -State $script:state -RunId $run -ReportsDirectory (Join-Path $script:install 'reports') -Extra @{ nota = "texto com $canary" }
        $json = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json

        $json.run.outcome | Should -Be 'failed'
        $json.steps.step | Should -Be @('pki', 'migrations')
        $json.installation.edition | Should -Be 'business'
        Get-Content -LiteralPath $report -Raw | Should -Not -Match ([regex]::Escape($canary))
    }

    It 'nenhuma coluna recebe valor sensível' {
        $canary = 'canario-033-coluna-' + [guid]::NewGuid().ToString('N')
        Register-InstallerSecret -Value $canary

        { Set-InstallationRecord -State $script:state -Version '0.8.0' -Edition ce -Engine podman-rootless -PublicUrl "https://$canary" } |
            Should -Throw -ExpectedMessage '*valor sensível*'
        [System.IO.File]::ReadAllText((Join-Path $script:install 'state/installation.db')) | Should -Not -Match ([regex]::Escape($canary))
    }

    It 'outcome fora da lista é recusado' {
        $run = Start-InstallerRun -State $script:state -Command 'install'
        { Complete-InstallerRun -State $script:state -RunId $run -Outcome 'quase' -ExitCode 0 } | Should -Throw
    }

    It 'backup por VACUUM INTO mantém as 10 últimas cópias' {
        Set-InstallationRecord -State $script:state -Version '0.8.0' -Edition ce -Engine podman-rootless -PublicUrl 'https://dokpod.lab.test'
        1..12 | ForEach-Object {
            $copy = Backup-InstallerState -State $script:state -RunId "run-$_"
            (Get-Item $copy).LastWriteTimeUtc = [datetime]::UtcNow.AddMinutes($_)
            Test-Path -LiteralPath $copy | Should -BeTrue
        }

        @(Get-ChildItem (Join-Path $script:install 'state/backups') -Filter '*.db').Count | Should -Be 10
        $copy = [Microsoft.Data.Sqlite.SqliteConnection]::new("Data Source=$(Join-Path $script:install 'state/backups/run-12.db');Pooling=False")
        $copy.Open()
        try {
            $command = $copy.CreateCommand()
            $command.CommandText = (Get-Content -Raw (Join-Path (Get-StateSqlDirectory) 'queries/installation-get.sql'))
            $reader = $command.ExecuteReader(); [void] $reader.Read(); $reader.GetString(1) | Should -Be '0.8.0'; $reader.Dispose()
        }
        finally { $copy.Dispose() }
    }

    It 'retenção mantém as N mais recentes e sempre as que têm desvio de SQL' {
        $reports = Join-Path $script:install 'reports'
        $runs = 1..6 | ForEach-Object {
            $run = Start-InstallerRun -State $script:state -Command 'install' -RunId ("run-{0:D2}" -f $_)
            $report = Export-InstallerRunReport -State $script:state -RunId $run -ReportsDirectory $reports
            Complete-InstallerRun -State $script:state -RunId $run -Outcome converged -ExitCode 0 -ReportPath $report
            Start-Sleep -Milliseconds 5
            $run
        }
        Invoke-StateSql -State $script:state -Name 'sql-override-insert' -Parameters @{
            path = 'Migrations/Script/x/Up/01.sql'; purpose = 'migration'; original_sha256 = ('a' * 64); applied_sha256 = ('b' * 64)
            accepted_schema_fingerprint = ('c' * 64); confirmed_by = 'Ana'; now = '2026-11-09T00:00:00.000Z'; run_id = $runs[0]
        }

        Invoke-InstallerStateRetention -State $script:state -Keep 2

        $left = (Invoke-StateSql -State $script:state -Name 'x' -Text 'SELECT run_id FROM runs ORDER BY run_id' -Rows).run_id
        $left | Should -Be @('run-01', 'run-05', 'run-06')
        @(Get-ChildItem $reports).Name | Sort-Object | Should -Be @('run-01.json', 'run-05.json', 'run-06.json')
    }

    It 'banco ausente numa instalação existente só é reconstruído com confirmação' {
        $other = Join-Path $script:install 'outra'
        $failure = $null
        try { Open-InstallerState -InstallDirectory $other -ExistingInstallationDetected | Out-Null } catch { $failure = $_ }
        $failure.Exception.ExitCode | Should -Be 4

        $rebuilt = Open-InstallerState -InstallDirectory $other -ExistingInstallationDetected -ConfirmRebuild
        $rebuilt.Rebuilt | Should -BeTrue
        Close-InstallerState -State $rebuilt
    }

    It 'banco corrompido só é reconstruído com confirmação' {
        $other = Join-Path $script:install 'corrompida'
        New-Item -ItemType Directory -Path (Join-Path $other 'state') -Force | Out-Null
        [System.IO.File]::WriteAllBytes((Join-Path $other 'state/installation.db'), [byte[]] (1..4096 | ForEach-Object { 7 }))

        { Open-InstallerState -InstallDirectory $other } | Should -Throw
        $rebuilt = Open-InstallerState -InstallDirectory $other -ConfirmRebuild
        Invoke-StateSql -State $rebuilt -Name 'user-version-get' -Scalar | Should -Be 1
        Close-InstallerState -State $rebuilt
    }
}

Describe 'Arquivos SQL do estado' {
    It 'o manifesto lista todo arquivo com hash e parâmetros' {
        $root = Get-StateSqlDirectory
        $manifest = Get-Content -Raw (Join-Path $root 'manifest.json') | ConvertFrom-Json
        $onDisk = Get-ChildItem -LiteralPath $root -Recurse -Filter '*.sql' | ForEach-Object { [System.IO.Path]::GetRelativePath($root, $_.FullName) } | Sort-Object

        @($manifest.files.path | Sort-Object) | Should -Be @($onDisk)
        foreach ($file in $manifest.files) {
            $text = [System.IO.File]::ReadAllText((Join-Path $root $file.path))
            Get-Sha256Hex -Text $text | Should -Be $file.sha256 -Because $file.path
            $code = ($text -split "`n" | ForEach-Object { ($_ -split '--')[0] }) -join "`n"
            $parameters = @([regex]::Matches($code, '\$([a-z_]+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
            @($file.parameters.name | Sort-Object) | Should -Be $parameters -Because $file.path
        }
    }
}
