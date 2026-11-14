#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T048 (FR-032): upgrade só para versão maior e a versão de cada serviço conferida pelo rótulo OCI da imagem.
# A atualização entre duas versões com migration fica para a T050, com as candidatas da release.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Upgrade.psm1') -Force

    # Engine falso: um container por serviço, cada imagem com o rótulo de versão pedido.
    function New-FakeEngine {
        param([string] $Root, [hashtable] $Versions)
        $cases = ($Versions.Keys | ForEach-Object { "  *service=$_*) echo id-$_ ;;" }) -join "`n"
        $labels = ($Versions.Keys | ForEach-Object { "  *img-$_*) echo '$($Versions[$_])' ;;" }) -join "`n"
        $fake = Join-Path $Root 'docker'
        [System.IO.File]::WriteAllText($fake, @"
#!/bin/sh
case "`$1" in
  ps) case "`$*" in
$cases
  esac ;;
  inspect) echo "img-`${4#id-}" ;;
  image) case "`$*" in
$labels
  esac ;;
esac
"@)
        [System.IO.File]::SetUnixFileMode($fake, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        return $fake
    }
}

Describe 'upgrade --version' {
    It 'compara X.Y.Z com pré-release antes da final' {
        Compare-InstallerVersion -Left '0.9.0' -Right '0.8.0' | Should -Be 1
        Compare-InstallerVersion -Left '0.8.0' -Right '0.8.0' | Should -Be 0
        Compare-InstallerVersion -Left '0.9.0-rc.1' -Right '0.9.0' | Should -Be -1
        Compare-InstallerVersion -Left '0.9.0-rc.2' -Right '0.9.0-rc.1' | Should -Be 1
        Compare-InstallerVersion -Left '0.10.0' -Right '0.9.9' | Should -Be 1
    }

    It 'igual, menor ou inválida dá código 2, com o rollback indicado na menor' {
        { Assert-UpgradeVersion -Installed '0.8.0' -Target '0.9.0' } | Should -Not -Throw
        foreach ($case in @(@{ Target = '0.8.0'; Expected = '*já está na 0.8.0*' }, @{ Target = '0.7.9'; Expected = '*deploy-e-rollback.md*backup-restore*' }, @{ Target = 'latest'; Expected = '*Versão inválida*' })) {
            $failure = { Assert-UpgradeVersion -Installed '0.8.0' -Target $case.Target } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 2
            $failure.Exception.Message | Should -BeLike $case.Expected
        }
    }

    It 'confere o rótulo org.opencontainers.image.version de cada serviço do Dokpod' {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-upgrade-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $root | Out-Null
        try {
            $ok = New-FakeEngine -Root $root -Versions @{ api = '0.9.0'; bff = '0.9.0'; web = 'v0.9.0'; agent = '0.9.0' }
            (Assert-ServiceImageVersion -Version '0.9.0' -Project 'dokpod' -Cli $ok).api | Should -Be '0.9.0'
            $stale = New-FakeEngine -Root $root -Versions @{ api = '0.9.0'; bff = '0.9.0'; web = '0.9.0'; agent = '0.8.0' }
            $failure = { Assert-ServiceImageVersion -Version '0.9.0' -Project 'dokpod' -Cli $stale } | Should -Throw -PassThru
            $failure.Exception.ExitCode | Should -Be 1
            $failure.Exception.Message | Should -Match 'agent na 0.8.0'
        }
        finally { Remove-Item -LiteralPath $root -Recurse -Force }
    }
}
