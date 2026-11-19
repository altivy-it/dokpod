#Requires -Version 7.4
<#
.SYNOPSIS
Arquivo de respostas do instalador (spec 033 T018, FR-001, FR-002): leitura YAML ou JSON, validação pelo esquema,
caminhos de instalação, modo guiado e gravação sem segredos.
#>
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')

function Get-AnswersSchemaPath {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return Join-Path (Get-InstallerHome) 'templates/answers.schema.json'
}

function ConvertFrom-YamlText {
    <#
    .SYNOPSIS
    Converte YAML em JSON com tipos: escalar sem aspas vira número, booleano ou nulo; com aspas, sempre texto.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Text)

    Import-InstallerLibraries
    $stream = [YamlDotNet.RepresentationModel.YamlStream]::new()
    try {
        $stream.Load([System.IO.StringReader]::new($Text))
    }
    catch {
        Stop-Installer -Code Usage -Message "O arquivo de respostas não é YAML válido: $($_.Exception.InnerException?.Message ?? $_.Exception.Message)"
    }

    if ($stream.Documents.Count -ne 1) {
        Stop-Installer -Code Usage -Message 'O arquivo de respostas precisa ter exatamente um documento YAML.'
    }

    return (ConvertFrom-YamlNode -Node $stream.Documents[0].RootNode) | ConvertTo-Json -Depth 32 -Compress
}

function ConvertFrom-YamlNode {
    [CmdletBinding()]
    param([Parameter(Mandatory)] $Node)

    switch ($Node.NodeType) {
        'Mapping' {
            $map = [ordered]@{}
            foreach ($entry in $Node.Children.GetEnumerator()) {
                $map[[string] $entry.Key.Value] = ConvertFrom-YamlNode -Node $entry.Value
            }

            return $map
        }
        'Sequence' {
            return , @($Node.Children | ForEach-Object { ConvertFrom-YamlNode -Node $_ })
        }
        default {
            $value = $Node.Value
            if ($Node.Style -ne [YamlDotNet.Core.ScalarStyle]::Plain) {
                return [string] $value
            }

            if ($null -eq $value -or $value -in '', '~', 'null', 'Null', 'NULL') { return $null }
            if ($value -cin 'true', 'True', 'TRUE') { return $true }
            if ($value -cin 'false', 'False', 'FALSE') { return $false }
            $number = 0L
            if ($value -match '^-?[0-9]+$' -and [long]::TryParse($value, [ref] $number)) { return $number }
            return [string] $value
        }
    }
}

function Read-InstallerAnswers {
    <#
    .SYNOPSIS
    Lê o arquivo de respostas (.yaml, .yml ou .json), valida pelo esquema e devolve um dicionário com os padrões aplicados.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Stop-Installer -Code Usage -Message "Arquivo de respostas não encontrado: $Path."
    }

    $text = [System.IO.File]::ReadAllText($Path)
    $json = if ([System.IO.Path]::GetExtension($Path) -eq '.json') { $text } else { ConvertFrom-YamlText -Text $text }
    return ConvertFrom-InstallerAnswersJson -Json $json
}

function ConvertFrom-InstallerAnswersJson {
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    param([Parameter(Mandatory)][string] $Json)

    $problems = Test-InstallerAnswersJson -Json $Json
    if ($problems.Count -gt 0) {
        Stop-Installer -Code Usage -Message ("Arquivo de respostas inválido:`n  - " + ($problems -join "`n  - "))
    }

    $answers = $Json | ConvertFrom-Json -AsHashtable
    Set-InstallerAnswerDefaults -Answers $answers
    Assert-InstallPaths -InstallDirectory $answers.installDir -SecretsDirectory $answers.secretsDir
    return $answers
}

function Test-InstallerAnswersJson {
    <#
    .SYNOPSIS
    Problemas do arquivo de respostas: esquema e, com mensagem própria, segredo escrito em texto (FR-002).
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param([Parameter(Mandatory)][string] $Json)

    $problems = [System.Collections.Generic.List[string]]::new()
    try {
        $document = $Json | ConvertFrom-Json -AsHashtable -Depth 64
    }
    catch {
        return , [string[]] @('O conteúdo não é JSON nem YAML válido.')
    }

    foreach ($path in (Find-InlineSecret -Node $document -Path '$')) {
        $problems.Add("$path tem um segredo escrito no arquivo; use { file: <caminho> } ou { env: <NOME> }.")
    }

    $schemaErrors = $null
    $valid = Test-Json -Json $Json -SchemaFile (Get-AnswersSchemaPath) -ErrorVariable schemaErrors -ErrorAction SilentlyContinue
    if (-not $valid -and $problems.Count -eq 0) {
        foreach ($schemaError in $schemaErrors) {
            $problems.Add(($schemaError.Exception.Message -replace '\s+', ' ').Trim())
        }

        if ($problems.Count -eq 0) {
            $problems.Add('O arquivo não segue o esquema answers.schema.json.')
        }
    }

    return , [string[]] $problems
}

function Find-InlineSecret {
    # Campo de senha, chave ou credencial com texto em vez de { file } / { env }.
    param($Node, [string] $Path)

    if ($Node -is [System.Collections.IDictionary]) {
        foreach ($key in $Node.Keys) {
            $value = $Node[$key]
            if ($key -match '(?i)^(password|secret|token|key|credential|privateKey)$' -and $value -is [string]) {
                "$Path.$key"
            }
            elseif ($null -ne $value) {
                Find-InlineSecret -Node $value -Path "$Path.$key"
            }
        }
    }
    elseif ($Node -is [System.Collections.IList] -and $Node -isnot [string]) {
        for ($index = 0; $index -lt $Node.Count; $index++) {
            Find-InlineSecret -Node $Node[$index] -Path "$Path[$index]"
        }
    }
}

function Set-InstallerAnswerDefaults {
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Answers)

    $userHome = [System.Environment]::GetFolderPath('UserProfile')
    if (-not $Answers.Contains('installDir')) { $Answers['installDir'] = Join-Path $userHome '.local/share/dokpod' }
    if (-not $Answers.Contains('secretsDir')) { $Answers['secretsDir'] = Join-Path $userHome '.config/dokpod/secrets' }
    if (-not $Answers.Contains('manualMigrations')) { $Answers['manualMigrations'] = $false }
    if (-not $Answers.Contains('localEnvironment')) { $Answers['localEnvironment'] = @{} }
    if (-not $Answers.localEnvironment.Contains('name')) { $Answers.localEnvironment['name'] = 'host-local' }
    if (-not $Answers.localEnvironment.Contains('stage')) { $Answers.localEnvironment['stage'] = 'production' }
    if (-not $Answers.keycloak.Contains('realm')) { $Answers.keycloak['realm'] = 'dokpod' }
    if ($Answers.edge.mode -eq 'provision' -and -not $Answers.edge.Contains('httpsPort')) {
        # A porta da publicUrl, quando ela tem uma: é a que os navegadores usam (o modo guiado não pergunta outra).
        $url = [uri] $Answers.publicUrl
        $Answers.edge['httpsPort'] = if ($url.IsDefaultPort) { 443 } else { $url.Port }
    }
    if ($Answers.edge.mode -eq 'existing') {
        if (-not $Answers.edge.Contains('upstreamHost')) { $Answers.edge['upstreamHost'] = '127.0.0.1' }
        foreach ($default in @(@('webPort', 18080), @('bffPort', 18081), @('keycloakPort', 18082))) {
            if (-not $Answers.edge.Contains($default[0])) { $Answers.edge[$default[0]] = $default[1] }
        }
    }
    if (-not $Answers.certificates.Contains('renewWarningDays')) { $Answers.certificates['renewWarningDays'] = 30 }
}

function Assert-InstallPaths {
    <#
    .SYNOPSIS
    installDir e secretsDir absolutos, diferentes, um fora do outro e fora de checkout Git.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $InstallDirectory,
        [Parameter(Mandatory)][string] $SecretsDirectory
    )

    foreach ($path in $InstallDirectory, $SecretsDirectory) {
        if (-not [System.IO.Path]::IsPathFullyQualified($path)) {
            Stop-Installer -Code Usage -Message "O caminho $path precisa ser absoluto."
        }
    }

    $install = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($InstallDirectory))
    $secrets = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($SecretsDirectory))
    $separator = [System.IO.Path]::DirectorySeparatorChar
    if ($install -eq $secrets) {
        Stop-Installer -Code Usage -Message 'installDir e secretsDir precisam ser diferentes.'
    }

    if ($secrets.StartsWith($install + $separator, [System.StringComparison]::Ordinal) -or
        $install.StartsWith($secrets + $separator, [System.StringComparison]::Ordinal)) {
        Stop-Installer -Code Usage -Message 'installDir e secretsDir não podem ficar um dentro do outro.'
    }

    foreach ($path in $install, $secrets) {
        $current = $path
        while ($current) {
            if (Test-Path -LiteralPath (Join-Path $current '.git')) {
                Stop-Installer -Code Usage -Message "O caminho $path fica dentro de um checkout Git ($current); escolha uma pasta fora do repositório."
            }

            $parent = Split-Path -Parent $current
            if ($parent -eq $current) { break }
            $current = $parent
        }
    }
}

function Save-InstallerAnswers {
    <#
    .SYNOPSIS
    Grava as respostas da execução guiada em installDir/answers.yaml (JSON, que também é YAML válido), sem segredos.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Answers,
        [Parameter(Mandatory)][string] $Path
    )

    $json = $Answers | ConvertTo-Json -Depth 32
    $problems = Test-InstallerAnswersJson -Json $json
    if ($problems.Count -gt 0) {
        Stop-Installer -Code Error -Message ("As respostas montadas não seguem o esquema: " + ($problems -join '; '))
    }

    if ($PSCmdlet.ShouldProcess($Path, 'gravar respostas')) {
        [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
        [System.IO.File]::WriteAllText($Path, "# Respostas do instalador do Dokpod (spec 033); sem segredos.`n$json`n")
    }
}

function Request-InstallerAnswers {
    <#
    .SYNOPSIS
    Modo guiado: completa as respostas obrigatórias perguntando ao operador. Sem terminal, resposta faltante dá código 2.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.IDictionary])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'Prompt', Justification = 'Chamado dentro do scriptblock $ask.')]
    param(
        [System.Collections.IDictionary] $Partial = [ordered]@{},
        [scriptblock] $Prompt = { param($question, $default) Read-Host -Prompt $(if ($default) { "$question [$default]" } else { $question }) }
    )

    $answers = [ordered]@{ schemaVersion = 1 }
    foreach ($key in $Partial.Keys) { $answers[$key] = $Partial[$key] }
    $interactive = Test-InteractiveSession
    $missing = [System.Collections.Generic.List[string]]::new()

    $ask = {
        param([string] $name, [string] $question, [string] $default, [string[]] $choices)
        $value = $null
        if ($interactive) {
            while ($true) {
                $value = & $Prompt $question $default
                if ([string]::IsNullOrWhiteSpace($value)) { $value = $default }
                if ($value -and (-not $choices -or $value -in $choices)) { break }
                Write-InstallerLine -ToStandardError -Message "Resposta inválida para $name$(if ($choices) { ' (' + ($choices -join ', ') + ')' })."
            }
        }
        elseif ($default) {
            $value = $default
        }
        else {
            $missing.Add($name)
        }

        return $value
    }

    if (-not $answers.Contains('version')) { $answers['version'] = & $ask 'version' 'Versão do Dokpod a instalar' $null $null }
    if (-not $answers.Contains('edition')) { $answers['edition'] = & $ask 'edition' 'Edição (ce ou business)' 'ce' @('ce', 'business') }
    if (-not $answers.Contains('publicUrl')) { $answers['publicUrl'] = & $ask 'publicUrl' 'URL pública (https://nome[:porta])' $null $null }
    if (-not $answers.Contains('engine')) { $answers['engine'] = & $ask 'engine' 'Engine (docker-rootless ou podman-rootless)' $null @('docker-rootless', 'podman-rootless') }
    if (-not $answers.Contains('operator')) { $answers['operator'] = & $ask 'operator' 'Seu nome, para a auditoria' $null $null }
    if (-not $answers.Contains('postgres')) {
        $answers['postgres'] = @{ mode = (& $ask 'postgres.mode' 'PostgreSQL (provision, existing-create ou existing-prepared)' 'provision' @('provision', 'existing-create', 'existing-prepared')) }
        if ($answers.postgres.mode -eq 'existing-create') {
            # A senha administrativa não entra nas respostas: é pedida na execução e descartada (FR-008).
            $answers.postgres['host'] = & $ask 'postgres.host' 'Nome ou IP do PostgreSQL, como os containers o alcançam' $null $null
            $answers.postgres['port'] = [int] (& $ask 'postgres.port' 'Porta do PostgreSQL' '5432' $null)
            $answers.postgres['sslMode'] = & $ask 'postgres.sslMode' 'TLS do PostgreSQL (require, verify-ca ou verify-full)' 'verify-full' @('require', 'verify-ca', 'verify-full')
            $answers.postgres['admin'] = @{ username = (& $ask 'postgres.admin.username' 'Usuário administrativo do PostgreSQL (usado só para criar o banco e as roles)' 'postgres' $null) }
        }
        elseif ($answers.postgres.mode -eq 'existing-prepared') {
            # Banco e roles preparados pelo usuário: as senhas entram como arquivo, nunca no arquivo de respostas.
            $answers.postgres['host'] = & $ask 'postgres.host' 'Nome ou IP do PostgreSQL, como os containers o alcançam' $null $null
            $answers.postgres['port'] = [int] (& $ask 'postgres.port' 'Porta do PostgreSQL' '5432' $null)
            $answers.postgres['sslMode'] = & $ask 'postgres.sslMode' 'TLS do PostgreSQL (require, verify-ca ou verify-full)' 'verify-full' @('require', 'verify-ca', 'verify-full')
            $answers.postgres['database'] = & $ask 'postgres.database' 'Banco do Dokpod, já criado' 'dokpod' $null
            foreach ($key in 'owner', 'runtime') {
                $label = if ($key -eq 'owner') { 'Role dona do banco (aplica as migrations)' } else { 'Role de runtime (membro de dokpod_runtime)' }
                $username = & $ask "postgres.$key.username" $label $null $null
                $file = & $ask "postgres.$key.password" "Caminho absoluto do arquivo com a senha de $username" $null $null
                $answers.postgres[$key] = [ordered]@{ username = $username }
                $answers.postgres[$key]['password'] = @{ file = $file }
            }
        }
    }
    if (-not $answers.Contains('keycloak')) {
        $mode = & $ask 'keycloak.mode' 'Keycloak (provision ou existing)' 'provision' @('provision', 'existing')
        $answers['keycloak'] = @{ mode = $mode }
        if ($mode -eq 'provision') {
            $answers.keycloak['initialAdmin'] = @{
                username = (& $ask 'keycloak.initialAdmin.username' 'Usuário do primeiro administrador' 'dokpod-admin' $null)
                email    = (& $ask 'keycloak.initialAdmin.email' 'E-mail do primeiro administrador' $null $null)
            }
        }
        else {
            # A senha administrativa não entra nas respostas: é pedida na execução e descartada (FR-015).
            $answers.keycloak['url'] = & $ask 'keycloak.url' 'URL do Keycloak existente (https://...)' $null $null
            $answers.keycloak['admin'] = @{ username = (& $ask 'keycloak.admin.username' 'Administrador do realm master (usado só nesta execução)' 'admin' $null) }
            $answers.keycloak['administratorUser'] = & $ask 'keycloak.administratorUser' 'Usuário que recebe as roles do Dokpod' $null $null
        }
    }

    if (-not $answers.Contains('edge')) {
        $answers['edge'] = @{ mode = (& $ask 'edge.mode' 'Borda (provision ou existing)' 'provision' @('provision', 'existing')) }
        if ($answers.edge.mode -eq 'existing') {
            # Só loopback ou uma rede compartilhada com o nginx do usuário (FR-043); o resto o esquema recusa.
            $upstream = & $ask 'edge.upstreamHost' 'Onde web e BFF escutam para o seu nginx: 127.0.0.1, ::1 ou o nome de uma rede compartilhada com ele' '127.0.0.1' $null
            $answers.edge['upstreamHost'] = if ($upstream -in '127.0.0.1', '::1') { $upstream } else { @{ network = $upstream } }
        }
    }
    if (-not $answers.Contains('certificates')) {
        $answers['certificates'] = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' } }
    }

    if ($missing.Count -gt 0) {
        Stop-Installer -Code Usage -Message ("Sem terminal, estas respostas são obrigatórias no arquivo de respostas: " + ($missing -join ', ') + '.')
    }

    return ConvertFrom-InstallerAnswersJson -Json ($answers | ConvertTo-Json -Depth 32)
}

Export-ModuleMember -Function Get-AnswersSchemaPath, ConvertFrom-YamlText, Read-InstallerAnswers, ConvertFrom-InstallerAnswersJson,
Test-InstallerAnswersJson, Set-InstallerAnswerDefaults, Assert-InstallPaths, Save-InstallerAnswers, Request-InstallerAnswers
