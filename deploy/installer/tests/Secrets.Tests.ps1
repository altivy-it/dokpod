#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T023 (FR-002, FR-028, FR-033): segredos com entropia do sistema, 0600 em 0700, falha fechada e redação.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Secrets.psm1') -Force
}

Describe 'Segredos da instalação' {
    BeforeEach {
        Clear-InstallerSecrets
        $script:root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-secrets-$([guid]::NewGuid().ToString('N'))"
    }

    AfterEach {
        Remove-Item -LiteralPath $script:root -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'gera valores distintos, longos e redigidos' {
        $first = New-InstallerSecret
        $second = New-InstallerSecret

        $first | Should -Not -Be $second
        $first.Length | Should -BeGreaterOrEqual 43
        Protect-InstallerText -Text "valor=$first" | Should -Be 'valor=***'
    }

    It 'grava com modo 0600 numa pasta 0700' {
        Write-InstallerSecret -SecretsDirectory $script:root -Name 'pki/api.pass' -Value 'canario-033-pass'

        $file = Join-Path $script:root 'pki/api.pass'
        [System.IO.File]::ReadAllText($file) | Should -Be 'canario-033-pass'
        [System.IO.File]::GetUnixFileMode($file) | Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite')
        [System.IO.File]::GetUnixFileMode((Join-Path $script:root 'pki')) | Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
    }

    It 'falha fechada quando o destino existe, e só substitui com -Recreate' {
        Write-InstallerSecret -SecretsDirectory $script:root -Name 'dokpod-db-password' -Value 'original-033'

        { Write-InstallerSecret -SecretsDirectory $script:root -Name 'dokpod-db-password' -Value 'novo-033' } |
            Should -Throw -ExpectedMessage '*recreate secret:dokpod-db-password*'
        Read-InstallerSecret -SecretsDirectory $script:root -Name 'dokpod-db-password' | Should -Be 'original-033'

        Write-InstallerSecret -SecretsDirectory $script:root -Name 'dokpod-db-password' -Value 'novo-033' -Recreate
        Read-InstallerSecret -SecretsDirectory $script:root -Name 'dokpod-db-password' | Should -Be 'novo-033'
    }

    It 'recusa nome que sairia de secretsDir: <_>' -ForEach '../fora', '/etc/passwd', 'a/../../b', '.oculto' {
        { Write-InstallerSecret -SecretsDirectory $script:root -Name $_ -Value 'x-033-x' } | Should -Throw
    }

    It 'lê { file } e { env } sem ecoar e registra para redação' {
        New-Item -ItemType Directory -Path $script:root | Out-Null
        $file = Join-Path $script:root 'pg-admin'
        [System.IO.File]::WriteAllText($file, "canario-033-arquivo`n")
        $env:DOKPOD_T033_SEGREDO = 'canario-033-ambiente'
        try {
            Read-SecretReference -Reference @{ file = $file } -Purpose 'PostgreSQL' | Should -Be 'canario-033-arquivo'
            Read-SecretReference -Reference @{ env = 'DOKPOD_T033_SEGREDO' } -Purpose 'Keycloak' | Should -Be 'canario-033-ambiente'
        }
        finally {
            Remove-Item Env:DOKPOD_T033_SEGREDO
        }

        Protect-InstallerText -Text 'canario-033-arquivo e canario-033-ambiente' | Should -Be '*** e ***'
    }

    It 'recusa referência ausente ou vazia' {
        { Read-SecretReference -Reference @{ env = 'DOKPOD_T033_NAO_EXISTE' } -Purpose 'x' } | Should -Throw -ExpectedMessage '*install.sh*'
        { Read-SecretReference -Reference @{ file = '/nao/existe' } -Purpose 'x' } | Should -Throw
        { Read-SecretReference -Reference @{ value = 'texto' } -Purpose 'x' } | Should -Throw
    }

    It 'a saída do instalador nunca leva o canário' {
        $canary = 'canario-033-saida-' + [guid]::NewGuid().ToString('N')
        Write-InstallerSecret -SecretsDirectory $script:root -Name 'teste' -Value $canary

        $output = & { Write-InstallerLine -Message "conexão com $canary" } 6>&1 2>&1 | Out-String
        $captured = [System.IO.StringWriter]::new()
        $original = [Console]::Out
        [Console]::SetOut($captured)
        try {
            Write-InstallerLine -Message "conexão com $canary"
        }
        finally {
            [Console]::SetOut($original)
        }

        $captured.ToString() | Should -Not -Match ([regex]::Escape($canary))
        $captured.ToString() | Should -Match 'conexão com \*\*\*'
        $output | Should -Not -Match ([regex]::Escape($canary))
    }
}
