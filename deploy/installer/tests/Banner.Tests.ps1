#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 036 T002 (FR-001, FR-002, FR-005; contracts/installer-cli.md): logo do instalador, modos de cor e versões sem
# UTF-8 e em terminal estreito, sempre com o ambiente injetado.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Banner.psm1') -Force
    $script:esc = [char] 27
    $script:versionLine = 'Instalador do Dokpod 0.0.0-logo · Altivy'
}

Describe 'Modo de cor' {
    It '<Name>' -ForEach @(
        @{ Name = 'NO_COLOR vence o truecolor'; Environment = @{ NO_COLOR = '1'; COLORTERM = 'truecolor'; TERM = 'xterm-256color' }; Expected = 'None' }
        @{ Name = 'NO_COLOR vazia não tira a cor'; Environment = @{ NO_COLOR = ''; COLORTERM = 'truecolor' }; Expected = 'TrueColor' }
        @{ Name = 'TERM=dumb'; Environment = @{ TERM = 'dumb'; COLORTERM = 'truecolor' }; Expected = 'None' }
        @{ Name = 'COLORTERM truecolor'; Environment = @{ TERM = 'xterm'; COLORTERM = 'truecolor' }; Expected = 'TrueColor' }
        @{ Name = 'COLORTERM 24bit'; Environment = @{ COLORTERM = '24bit' }; Expected = 'TrueColor' }
        @{ Name = '256 cores'; Environment = @{ TERM = 'xterm-256color' }; Expected = 'Ansi256' }
        @{ Name = '16 cores'; Environment = @{ TERM = 'xterm' }; Expected = 'Ansi16' }
    ) {
        Get-InstallerColorMode -Environment $Environment | Should -Be $Expected
    }
}

Describe 'UTF-8 do terminal' {
    It '<Name>' -ForEach @(
        @{ Name = 'LANG pt_BR.UTF-8'; Environment = @{ LANG = 'pt_BR.UTF-8' }; Expected = $true }
        @{ Name = 'C.utf8'; Environment = @{ LANG = 'C.utf8' }; Expected = $true }
        @{ Name = 'LANG C'; Environment = @{ LANG = 'C' }; Expected = $false }
        @{ Name = 'POSIX'; Environment = @{ LANG = 'POSIX' }; Expected = $false }
        @{ Name = 'LC_ALL vence LC_CTYPE e LANG'; Environment = @{ LC_ALL = 'C'; LC_CTYPE = 'pt_BR.UTF-8'; LANG = 'pt_BR.UTF-8' }; Expected = $false }
        @{ Name = 'LC_CTYPE vence LANG'; Environment = @{ LC_CTYPE = 'pt_BR.UTF-8'; LANG = 'C' }; Expected = $true }
        @{ Name = 'nada definido'; Environment = @{}; Expected = $false }
    ) {
        Test-InstallerUtf8 -Environment $Environment | Should -Be $Expected
    }
}

Describe 'Logo' {
    It 'truecolor: seis linhas no degradê Y1 e a versão apagada' {
        $text = Format-InstallerBanner -Version '0.0.0-logo' -ColorMode TrueColor -Utf8 $true -Width 120
        $lines = $text -split "`n"
        $colors = '255;242;122', '238;244;99', '217;244;86', '198;243;78', '183;243;74', '159;217;60'
        for ($i = 0; $i -lt 6; $i++) {
            $lines[$i].StartsWith("$($script:esc)[38;2;$($colors[$i])m", [StringComparison]::Ordinal) | Should -BeTrue
            $lines[$i].EndsWith("$($script:esc)[0m", [StringComparison]::Ordinal) | Should -BeTrue
            # A última linha é só a sombra.
            if ($i -lt 5) { $lines[$i] | Should -Match '█' }
        }
        $text | Should -Match ([regex]::Escape("$($script:esc)[2m$($script:versionLine)$($script:esc)[0m"))
    }

    It '256 cores: índice do cubo mais próximo, sem truecolor' {
        $text = Format-InstallerBanner -Version '0.0.0-logo' -ColorMode Ansi256 -Utf8 $true -Width 120
        $text | Should -Not -Match '38;2;'
        ($text -split "`n")[0].StartsWith("$($script:esc)[38;5;228m", [StringComparison]::Ordinal) | Should -BeTrue
    }

    It '16 cores: amarelo nas três primeiras linhas e verde nas três últimas' {
        $lines = (Format-InstallerBanner -Version '0.0.0-logo' -ColorMode Ansi16 -Utf8 $true -Width 120) -split "`n"
        0..2 | ForEach-Object { $lines[$_].StartsWith("$($script:esc)[93m", [StringComparison]::Ordinal) | Should -BeTrue }
        3..5 | ForEach-Object { $lines[$_].StartsWith("$($script:esc)[92m", [StringComparison]::Ordinal) | Should -BeTrue }
    }

    It 'sem cor: nenhum caractere de controle' {
        $text = Format-InstallerBanner -Version '0.0.0-logo' -ColorMode None -Utf8 $true -Width 120
        $text | Should -Not -Match "$($script:esc)"
        $text | Should -Match ([regex]::Escape($script:versionLine))
    }

    It 'sem UTF-8: o mesmo desenho com #, sem blocos nem a linha da sombra' {
        $text = Format-InstallerBanner -Version '0.0.0-logo' -ColorMode None -Utf8 $false -Width 120
        $lines = $text -split "`n"
        $text | Should -Not -Match '[█╗╔╝╚═║]'
        $lines.Count | Should -Be 7
        0..4 | ForEach-Object { $lines[$_] | Should -Match '#' }
        $lines[5] | Should -BeExactly ''
        $lines[0] | Should -BeExactly '######   ######  ##   ## ######   ######  ######'
    }

    It 'terminal com <Width> colunas: só a linha da versão' -ForEach @(@{ Width = 51 }, @{ Width = $null }) {
        $text = Format-InstallerBanner -Version '0.0.0-logo' -ColorMode None -Utf8 $true -Width $Width
        $text | Should -BeExactly $script:versionLine
    }

    It 'com 52 colunas o desenho cabe' {
        Format-InstallerBanner -Version '0.0.0-logo' -ColorMode None -Utf8 $true -Width 52 | Should -Match '█'
    }

    It 'versão vazia vira dev' {
        Format-InstallerBanner -Version '' -ColorMode None -Utf8 $true -Width 120 | Should -Match ([regex]::Escape('Instalador do Dokpod dev · Altivy'))
    }
}
