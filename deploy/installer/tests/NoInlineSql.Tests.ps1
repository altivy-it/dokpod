#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
<#
.SYNOPSIS
Spec 033 T009 (FR-039, SC-011): o código do instalador não tem SQL escrito; todo SQL vem de arquivos .sql.

.DESCRIPTION
Varre os literais de string de deploy/installer/src (*.ps1 e *.psm1) pelo parser do PowerShell e recusa os que parecem
um comando SQL. O SQL do banco do Dokpod vem do catálogo de backend/libs/Dokpod.ControlPlane.Infrastructure/Sql e o do
estado SQLite de deploy/installer/sql/state (D-014). As exceções ficam em $allowed, com o motivo.
#>

BeforeDiscovery {
    $script:sources = @(Get-ChildItem -Path (Join-Path $PSScriptRoot '../src') -Recurse -File -Include '*.ps1', '*.psm1' |
            ForEach-Object { @{ Path = $_.FullName; Name = $_.Name } })
}

BeforeAll {
    $script:sqlCommand = [regex]::new(
        '\b(?:SELECT\s+[\s\S]+?\s+FROM|INSERT\s+INTO|UPDATE\s+[\w".{}$]+\s+SET|DELETE\s+FROM|CREATE\s+(?:OR\s+REPLACE\s+)?(?:TABLE|ROLE|DATABASE|INDEX|SCHEMA|FUNCTION|VIEW|TRIGGER|EXTENSION)|ALTER\s+(?:TABLE|ROLE|DATABASE|SCHEMA|FUNCTION|DEFAULT\s+PRIVILEGES)|DROP\s+(?:TABLE|ROLE|DATABASE|INDEX|SCHEMA|FUNCTION|VIEW|TRIGGER)|GRANT\s+[\s\S]+?\s+(?:ON|TO)\s|REVOKE\s+[\s\S]+?\s+(?:ON|FROM)\s|TRUNCATE\s|PRAGMA\s+\w)',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    # Literais aceitos: @{ File = 'Nome.psm1'; Literal = '...'; Reason = '...' }. Vazio hoje.
    $script:allowed = @()

    function Get-InlineSql {
        param([Parameter(Mandatory)][string] $Source)

        $tokens = $null
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseInput($Source, [ref] $tokens, [ref] $errors)
        $kinds = @('StringLiteral', 'StringExpandable', 'HereStringLiteral', 'HereStringExpandable')
        foreach ($token in $tokens) {
            if ($token.Kind.ToString() -in $kinds -and $script:sqlCommand.IsMatch($token.Text)) {
                $token.Text
            }
        }
    }
}

Describe 'Detector de SQL no código' {
    It 'encontra <_>' -ForEach @(
        '$q = "SELECT id FROM components"'
        "`$q = 'insert into runs (run_id) values (`$run_id)'"
        "`$q = @'`nUPDATE run_lock SET run_id = `$run_id`n'@"
        '$q = "CREATE TABLE IF NOT EXISTS installation (id text)"'
        '$q = "PRAGMA user_version"'
        '$q = "GRANT USAGE ON SCHEMA dokpod TO runtime"'
    ) {
        Get-InlineSql -Source $_ | Should -Not -BeNullOrEmpty
    }

    It 'ignora texto comum: <_>' -ForEach @(
        '$m = "Selecione o engine e atualize a lista"'
        '$p = "Installation/checks/server-version.sql"'
        'Write-Host "update available"'
    ) {
        Get-InlineSql -Source $_ | Should -BeNullOrEmpty
    }
}

Describe 'Código do instalador' {
    It '<Name> não tem SQL escrito' -ForEach $sources {
        $found = @(Get-InlineSql -Source (Get-Content -LiteralPath $Path -Raw) |
                Where-Object { $literal = $_; -not ($script:allowed | Where-Object { $_.File -eq $Name -and $_.Literal -eq $literal }) })
        $found | Should -BeNullOrEmpty -Because 'todo SQL do instalador vem de arquivos .sql (FR-039)'
    }
}
