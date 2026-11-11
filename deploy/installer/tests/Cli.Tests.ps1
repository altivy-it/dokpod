#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T027 (FR-001, FR-004, FR-029, FR-036, FR-041; contracts/installer-cli.md): argumentos, confirmações nomeadas
# e comandos sem efeito.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Cli.psm1') -Force
    $script:entry = Join-Path $PSScriptRoot '../src/Dokpod.Installer.ps1'

    function Invoke-Entry {
        param([string[]] $Arguments)
        $output = & pwsh -NoProfile -NonInteractive -File $script:entry @Arguments 2>&1
        return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }
}

Describe 'Argumentos' {
    It 'sem comando é install' {
        (ConvertFrom-InstallerArguments -Arguments @('--dry-run')).Command | Should -Be 'install'
    }

    It '--yes não confirma remoção de dados, migrations nem sobreposição de SQL' {
        $parsed = ConvertFrom-InstallerArguments -Arguments @('recreate', '--all', '--yes')
        $parsed.Confirmations.Plan | Should -BeTrue
        $parsed.Confirmations.DataRemoval.Count | Should -Be 0
        $parsed.Confirmations.MigrationsPlanHash | Should -BeNullOrEmpty
        $parsed.Confirmations.SqlOverrides.Count | Should -Be 0
    }

    It 'confirmações nomeadas viram listas' {
        $hash = 'a' * 64
        $parsed = ConvertFrom-InstallerArguments -Arguments @('upgrade', '--version', '0.9.0', '--confirm-data-removal', 'dokpod-postgres, keycloak-postgres', '--confirm-migrations', $hash, '--confirm-sql-overrides', 'Bootstrap/01.sql')
        $parsed.Confirmations.DataRemoval | Should -Be @('dokpod-postgres', 'keycloak-postgres')
        $parsed.Confirmations.MigrationsPlanHash | Should -Be $hash
        $parsed.Confirmations.SqlOverrides | Should -Be @('Bootstrap/01.sql')
    }

    It 'rejeita comando, opção, valor ausente, repetição e planHash inválido' -ForEach @(
        @{ Arguments = @('instalar') }
        @{ Arguments = @('plan', '--senha', 'x') }
        @{ Arguments = @('plan', '--answers') }
        @{ Arguments = @('plan', '--answers', '/a', '--answers', '/b') }
        @{ Arguments = @('upgrade', '--confirm-migrations', 'abc') }
    ) {
        { ConvertFrom-InstallerArguments -Arguments $Arguments } | Should -Throw
        try { ConvertFrom-InstallerArguments -Arguments $Arguments } catch { $_.Exception.ExitCode | Should -Be 2 }
    }
}

Describe 'Ponto de entrada' {
    It 'help e --help saem com 0 e listam os códigos de saída' {
        foreach ($arguments in @(, @('help')), @(, @('--help')), @('status', '--help')) {
            $result = Invoke-Entry $arguments
            $result.ExitCode | Should -Be 0
            $result.Output | Should -Match '10 pausado para migration manual'
        }
    }

    It 'uso inválido sai com 2' {
        (Invoke-Entry @('plan', '--desconhecida')).ExitCode | Should -Be 2
    }

    It 'comando que aplica mudanças ainda não implementado sai com 2 e aponta --dry-run' {
        $result = Invoke-Entry @('install', '--answers', '/nao/existe.yaml')
        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'dry-run'
    }

    It 'status sem instalação sai com 0 e não cria nada' {
        $dir = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-cli-$([guid]::NewGuid().ToString('N'))"
        $result = Invoke-Entry @('status', '--install-dir', $dir)
        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'Nenhuma instalação'
        Test-Path $dir | Should -BeFalse
    }

    It 'sql export copia o catálogo para pasta vazia e recusa pasta com arquivos' {
        if (-not (Test-Path (Join-Path $env:DOKPOD_INSTALLER_HOME 'catalog/manifest.json'))) {
            Set-ItResult -Skipped -Because 'o catálogo SQL só existe na imagem do instalador'
            return
        }

        $out = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-sql-$([guid]::NewGuid().ToString('N'))"
        (Invoke-Entry @('sql', 'export', '--out', $out)).ExitCode | Should -Be 0
        Test-Path (Join-Path $out 'manifest.json') | Should -BeTrue
        (Invoke-Entry @('sql', 'export', '--out', $out)).ExitCode | Should -Be 2
        (Invoke-Entry @('sql', 'importar', '--out', $out)).ExitCode | Should -Be 2
    }
}

Describe 'plan --dry-run contra o engine real' {
    # Checkpoint da fase 2: plano completo sem efeitos. Precisa do socket do engine montado (DOCKER_HOST).
    It 'gera o plano de uma instalação nova, sai com 0 e não grava nada' {
        if (-not $env:DOCKER_HOST) {
            Set-ItResult -Skipped -Because 'o socket do engine não foi montado'
            return
        }

        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-plan-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $root | Out-Null
        $manifest = Join-Path $root 'release-manifest.json'
        $artifacts = foreach ($name in 'dokpod-api', 'dokpod-api-business', 'dokpod-bff', 'dokpod-web', 'dokpod-agent') {
            @{ kind = 'container'; name = $name; reference = "ghcr.io/altivy-it/$name"; digest = "sha256:$('c' * 64)" }
        }
        @{ version = '0.8.0'; artifacts = @($artifacts) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifest
        $answers = Join-Path $root 'answers.json'
        [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://localhost:18443'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $root 'install'); secretsDir = (Join-Path $root 'secrets')
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 18443 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' } }
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $answers
        $env:DOKPOD_RELEASE_MANIFEST = $manifest
        $env:DOKPOD_HOST_UNPRIVILEGED_PORT_START = '1024'
        $env:DOKPOD_HOST_LINGER = 'yes'
        $env:DOKPOD_HOST_LISTENING_PORTS = '22'
        $env:DOKPOD_HOST_FREE_BYTES = [string] 50GB
        # Este host pode ter a stack de laboratório no mesmo projeto compose; aí o plano deve bloquear com código 3.
        $foreign = @(& docker ps --all --filter label=com.docker.compose.project=dokpod --format '{{.Label "io.dokpod.installer.config-hash"}}' |
                Where-Object { -not $_ }).Count -gt 0
        try {
            $result = Invoke-Entry @('plan', '--answers', $answers)

            if ($foreign) {
                $result.ExitCode | Should -Be 3
                $result.Output | Should -Match 'não são deste instalador'
                Test-Path (Join-Path $root 'install') | Should -BeFalse
                return
            }

            $result.Output | Should -Match 'Plano para o Dokpod 0.8.0'
            $result.ExitCode | Should -Be 0
            $result.Output | Should -Match '(?m)^api\s+create'
            $result.Output | Should -Match '(?m)^migrations\s+create'
            $result.Output | Should -Match 'Nada foi alterado'
            Test-Path (Join-Path $root 'install') | Should -BeFalse
            Test-Path (Join-Path $root 'secrets') | Should -BeFalse
        }
        finally {
            'DOKPOD_RELEASE_MANIFEST', 'DOKPOD_HOST_UNPRIVILEGED_PORT_START', 'DOKPOD_HOST_LINGER', 'DOKPOD_HOST_LISTENING_PORTS', 'DOKPOD_HOST_FREE_BYTES' |
                ForEach-Object { Remove-Item "env:$_" -ErrorAction SilentlyContinue }
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
