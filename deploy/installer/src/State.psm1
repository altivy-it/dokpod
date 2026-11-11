#Requires -Version 7.4
<#
.SYNOPSIS
Estado da instalação em SQLite (spec 033 T019, FR-034, FR-044, D-014): schema forward-only, consultas em arquivos .sql,
trava de execução, etapas, backup, retenção e relatório. Nenhum valor sensível é gravado.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')

$script:BackupsToKeep = 10
$script:RunsToKeep = 200
$script:Outcomes = 'converged', 'changed', 'prerequisite_failed', 'declined', 'failed', 'paused_for_manual_migration'

function Get-StateSqlDirectory {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return Join-Path (Get-InstallerHome) 'sql/state'
}

function Get-StateSql {
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string] $Name)

    return [System.IO.File]::ReadAllText((Join-Path (Get-StateSqlDirectory) "queries/$Name.sql"))
}

function Get-UtcNow {
    return [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ', [System.Globalization.CultureInfo]::InvariantCulture)
}

function Invoke-StateSql {
    <#
    .SYNOPSIS
    Executa uma consulta de sql/state/queries com parâmetros $nome; -Rows devolve dicionários, -Scalar o primeiro valor.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $Name,
        [System.Collections.IDictionary] $Parameters = @{},
        [switch] $Rows,
        [switch] $Scalar,
        [string] $Text
    )

    $command = $State.Connection.CreateCommand()
    try {
        $command.CommandText = if ($Text) { $Text } else { Get-StateSql -Name $Name }
        if ($State.Transaction) { $command.Transaction = $State.Transaction }
        foreach ($key in $Parameters.Keys) {
            $value = $Parameters[$key]
            Assert-NoSecretValue -Value $value -Name $key
            [void] $command.Parameters.AddWithValue("`$$key", $(if ($null -eq $value) { [DBNull]::Value } else { $value }))
        }

        if ($Rows) {
            $reader = $command.ExecuteReader()
            try {
                $result = [System.Collections.Generic.List[object]]::new()
                while ($reader.Read()) {
                    $row = [ordered]@{}
                    for ($index = 0; $index -lt $reader.FieldCount; $index++) {
                        $row[$reader.GetName($index)] = if ($reader.IsDBNull($index)) { $null } else { $reader.GetValue($index) }
                    }

                    $result.Add($row)
                }

                return , $result.ToArray()
            }
            finally {
                $reader.Dispose()
            }
        }

        if ($Scalar) {
            $value = $command.ExecuteScalar()
            return $(if ($value -is [DBNull]) { $null } else { $value })
        }

        [void] $command.ExecuteNonQuery()
    }
    finally {
        $command.Dispose()
    }
}

function Assert-NoSecretValue {
    # Nenhuma coluna recebe valor registrado como sensível (FR-044).
    param($Value, [string] $Name)

    if ($Value -is [string] -and $Value.Length -ge 4 -and (Protect-InstallerText -Text $Value) -ne $Value) {
        Stop-Installer -Code Error -Message "O estado recusou um valor sensível em '$Name'."
    }
}

function Open-InstallerState {
    <#
    .SYNOPSIS
    Abre (ou cria) installDir/state/installation.db, confere integridade e aplica o schema só para a frente.
    .PARAMETER ExistingInstallationDetected
    Há sinais de uma instalação (containers, banco, realm). Com banco ausente ou corrompido, só reconstrói com -ConfirmRebuild.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $InstallDirectory,
        [switch] $ExistingInstallationDetected,
        [switch] $ConfirmRebuild
    )

    Import-InstallerLibraries
    $directory = Join-Path $InstallDirectory 'state'
    [void] [System.IO.Directory]::CreateDirectory($directory)
    $path = Join-Path $directory 'installation.db'
    $exists = Test-Path -LiteralPath $path
    if (-not $exists -and $ExistingInstallationDetected -and -not $ConfirmRebuild) {
        Stop-Installer -Code Declined -Message 'O estado da instalação não existe, mas há uma instalação neste host; confirme a reconstrução a partir do que está rodando.'
    }

    $state = $null
    try {
        $state = Connect-InstallerState -Path $path
        $integrity = Invoke-StateSql -State $state -Name 'integrity-check' -Scalar
    }
    catch [System.Management.Automation.MethodInvocationException] {
        $integrity = $_.Exception.InnerException?.Message ?? $_.Exception.Message
    }

    if ($integrity -ne 'ok') {
        if ($state) { Close-InstallerState -State $state }
        if (-not $ConfirmRebuild) {
            Stop-Installer -Code Declined -Message "O estado da instalação está corrompido ($integrity); confirme a reconstrução a partir do que está rodando."
        }

        $aside = "$path.corrompido-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
        [System.IO.File]::Move($path, $aside)
        $state = Connect-InstallerState -Path $path
    }

    if (-not $IsWindows) {
        [System.IO.File]::SetUnixFileMode($path, [System.IO.UnixFileMode]'UserRead, UserWrite, GroupRead')
    }

    Update-InstallerStateSchema -State $state
    $state.Rebuilt = (-not $exists -and $ExistingInstallationDetected) -or $integrity -ne 'ok'
    return $state
}

function Connect-InstallerState {
    param([string] $Path)

    $builder = [Microsoft.Data.Sqlite.SqliteConnectionStringBuilder]::new()
    $builder.DataSource = $Path
    $builder.Mode = 'ReadWriteCreate'
    $builder.Pooling = $false
    $connection = [Microsoft.Data.Sqlite.SqliteConnection]::new($builder.ToString())
    $connection.Open()
    $state = [pscustomobject]@{ Connection = $connection; Path = $Path; Transaction = $null; Rebuilt = $false }
    Invoke-StateSql -State $state -Name 'connection-open' -Rows | Out-Null
    return $state
}

function Update-InstallerStateSchema {
    param($State)

    $current = [int] (Invoke-StateSql -State $State -Name 'user-version-get' -Scalar)
    $files = Get-ChildItem -LiteralPath (Join-Path (Get-StateSqlDirectory) 'schema') -Filter '*.sql' | Sort-Object Name
    $known = if ($files) { [int] ($files[-1].Name.Split('-')[0]) } else { 0 }
    if ($current -gt $known) {
        Stop-Installer -Code Usage -Message "O estado é de uma versão mais nova do instalador (schema $current; este conhece até $known). Use o instalador dessa versão."
    }

    foreach ($file in $files) {
        if ([int] ($file.Name.Split('-')[0]) -le $current) { continue }
        $transaction = $State.Connection.BeginTransaction()
        $State.Transaction = $transaction
        try {
            Invoke-StateSql -State $State -Name 'schema' -Text ([System.IO.File]::ReadAllText($file.FullName))
            $transaction.Commit()
        }
        catch {
            $transaction.Rollback()
            throw
        }
        finally {
            $State.Transaction = $null
            $transaction.Dispose()
        }
    }
}

function Close-InstallerState {
    [CmdletBinding()]
    param([Parameter(Mandatory)] $State)

    $State.Connection.Dispose()
}

function Enter-InstallerRunLock {
    <#
    .SYNOPSIS
    Adquire a trava única em BEGIN IMMEDIATE. Ocupada dá código 7; trava velha (container inexistente) só sai com confirmação.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $RunId,
        [Parameter(Mandatory)][string] $ContainerName,
        [scriptblock] $ContainerExists = { param($name) $false },
        [switch] $ConfirmStaleRelease
    )

    $transaction = $State.Connection.BeginTransaction([System.Data.IsolationLevel]::Serializable, $false)
    $State.Transaction = $transaction
    try {
        $holder = Invoke-StateSql -State $State -Name 'lock-get' -Rows
        if ($holder.Count -gt 0) {
            $existing = $holder[0]
            if (& $ContainerExists $existing.container_name) {
                Stop-Installer -Code Locked -Message "Outra execução do instalador está em andamento ($($existing.run_id), desde $($existing.acquired_at))."
            }

            if (-not $ConfirmStaleRelease) {
                Stop-Installer -Code Locked -Message "Há uma trava de uma execução que não está mais rodando ($($existing.run_id)); confirme a liberação."
            }

            Invoke-StateSql -State $State -Name 'lock-delete' -Parameters @{ run_id = $null }
        }

        Invoke-StateSql -State $State -Name 'lock-insert' -Parameters @{ run_id = $RunId; container_name = $ContainerName; now = (Get-UtcNow) }
        $transaction.Commit()
    }
    catch {
        $transaction.Rollback()
        throw
    }
    finally {
        $State.Transaction = $null
        $transaction.Dispose()
    }
}

function Exit-InstallerRunLock {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $RunId
    )

    Invoke-StateSql -State $State -Name 'lock-delete' -Parameters @{ run_id = $RunId }
}

function Start-InstallerRun {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $Command,
        [string] $RunId = [guid]::NewGuid().ToString()
    )

    Invoke-StateSql -State $State -Name 'run-insert' -Parameters @{ run_id = $RunId; command = $Command; now = (Get-UtcNow) }
    return $RunId
}

function Add-InstallerRunStep {
    <#
    .SYNOPSIS
    Registra uma etapa numa transação, só depois que ela terminou (FR-027).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $RunId,
        [Parameter(Mandatory)][string] $Step,
        [ValidateSet('done', 'failed', 'skipped')][string] $Status = 'done'
    )

    $transaction = $State.Connection.BeginTransaction()
    $State.Transaction = $transaction
    try {
        Invoke-StateSql -State $State -Name 'run-step-insert' -Parameters @{ run_id = $RunId; step = $Step; status = $Status; now = (Get-UtcNow) }
        $transaction.Commit()
    }
    catch {
        $transaction.Rollback()
        throw
    }
    finally {
        $State.Transaction = $null
        $transaction.Dispose()
    }
}

function Complete-InstallerRun {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $RunId,
        [Parameter(Mandatory)][ValidateScript({ $_ -in $script:Outcomes })][string] $Outcome,
        [Parameter(Mandatory)][int] $ExitCode,
        [string] $ReportPath
    )

    Invoke-StateSql -State $State -Name 'run-finish' -Parameters @{
        run_id = $RunId; outcome = $Outcome; exit_code = $ExitCode; report_path = $ReportPath; now = (Get-UtcNow)
    }
}

function Get-InstallationRecord {
    [CmdletBinding()]
    param([Parameter(Mandatory)] $State)

    $rows = Invoke-StateSql -State $State -Name 'installation-get' -Rows
    return $(if ($rows.Count -gt 0) { $rows[0] } else { $null })
}

function Set-InstallationRecord {
    <#
    .SYNOPSIS
    Grava a instalação; a versão só aumenta (FR-032).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $Version,
        [Parameter(Mandatory)][string] $Edition,
        [Parameter(Mandatory)][string] $Engine,
        [Parameter(Mandatory)][string] $PublicUrl
    )

    $existing = Get-InstallationRecord -State $State
    if ($existing -and [version] $Version -lt [version] $existing.version) {
        Stop-Installer -Code Usage -Message "A versão instalada é $($existing.version); $Version é anterior. Use o procedimento de rollback (docs/runbooks/deploy-e-rollback.md)."
    }

    $id = if ($existing) { $existing.installation_id } else { [guid]::NewGuid().ToString() }
    Invoke-StateSql -State $State -Name 'installation-upsert' -Parameters @{
        installation_id = $id; version = $Version; edition = $Edition; engine = $Engine; public_url = $PublicUrl; now = (Get-UtcNow)
    }
}

function Backup-InstallerState {
    <#
    .SYNOPSIS
    VACUUM INTO state/backups/<runId>.db, mantendo as 10 cópias mais recentes.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $RunId
    )

    $directory = Join-Path (Split-Path -Parent $State.Path) 'backups'
    [void] [System.IO.Directory]::CreateDirectory($directory)
    $target = Join-Path $directory "$RunId.db"
    Invoke-StateSql -State $State -Name 'backup' -Parameters @{ path = $target }
    Get-ChildItem -LiteralPath $directory -Filter '*.db' | Where-Object FullName -ne $target | Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -Skip ($script:BackupsToKeep - 1) | Remove-Item -Force
    return $target
}

function Invoke-InstallerStateRetention {
    <#
    .SYNOPSIS
    Mantém as 200 execuções mais recentes e seus relatórios; as que têm sql_overrides ficam sempre.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $State,
        [int] $Keep = $script:RunsToKeep
    )

    foreach ($run in (Invoke-StateSql -State $State -Name 'runs-expired' -Parameters @{ keep = $Keep } -Rows)) {
        if ($run.report_path -and (Test-Path -LiteralPath $run.report_path)) {
            Remove-Item -LiteralPath $run.report_path -Force
        }

        Invoke-StateSql -State $State -Name 'run-delete' -Parameters @{ run_id = $run.run_id }
    }
}

function Export-InstallerRunReport {
    <#
    .SYNOPSIS
    Gera installDir/reports/<runId>.json a partir do banco, redigido (FR-034).
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)][string] $RunId,
        [Parameter(Mandatory)][string] $ReportsDirectory,
        [System.Collections.IDictionary] $Extra = @{}
    )

    $run = (Invoke-StateSql -State $State -Name 'run-get' -Parameters @{ run_id = $RunId } -Rows)[0]
    $overrides = Invoke-StateSql -State $State -Name 'sql-overrides-list' -Rows
    $report = [ordered]@{
        schemaVersion = 1
        run           = $run
        installation  = Get-InstallationRecord -State $State
        steps         = Invoke-StateSql -State $State -Name 'run-steps-list' -Parameters @{ run_id = $RunId } -Rows
        components    = Invoke-StateSql -State $State -Name 'components-list' -Rows
        certificates  = Invoke-StateSql -State $State -Name 'certificates-list' -Rows
        sqlOverrides  = @($overrides | Where-Object { $_.run_id -eq $RunId })
    }
    foreach ($key in $Extra.Keys) { $report[$key] = $Extra[$key] }

    [void] [System.IO.Directory]::CreateDirectory($ReportsDirectory)
    $path = Join-Path $ReportsDirectory "$RunId.json"
    [System.IO.File]::WriteAllText($path, (Protect-InstallerText -Text ($report | ConvertTo-Json -Depth 16)))
    return $path
}

Export-ModuleMember -Function Get-StateSqlDirectory, Invoke-StateSql, Open-InstallerState, Close-InstallerState, Enter-InstallerRunLock,
Exit-InstallerRunLock, Start-InstallerRun, Add-InstallerRunStep, Complete-InstallerRun, Get-InstallationRecord, Set-InstallationRecord,
Backup-InstallerState, Invoke-InstallerStateRetention, Export-InstallerRunReport
