#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T053 (SC-005, cenário 7 do quickstart, L7): nenhum segredo da instalação aparece fora de secretsDir. Uma
# instalação completa com engine real; durante a execução, uma amostragem guarda `docker inspect` e `docker top` de
# todo container (inclusive os avulsos); no fim, cada segredo de secretsDir é procurado na saída, em reports/*.json, em
# installDir, nas amostras e nos logs dos containers. O journal do host (journalctl --user) fica fora do alcance do
# container dos testes: a garantia é o --log-driver none dos containers avulsos, conferido aqui.

BeforeAll {
    . (Join-Path $PSScriptRoot 'TestCleanup.ps1')
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Secrets.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Install.psm1') -Force
    $script:enabled = $env:DOKPOD_TEST_FULL_INSTALL -eq '1' -and $env:DOCKER_HOST -and $env:DOKPOD_TEST_SHARED
}

Describe 'Canário de segredos (SC-005)' {
    BeforeAll {
        if (-not $script:enabled) { return }
        $script:project = 'dokpod-t053'
        $script:root = Join-Path $env:DOKPOD_TEST_SHARED "t053-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $script:answers = [ordered]@{
            schemaVersion = 1; version = '0.8.0'; edition = 'ce'; publicUrl = 'https://dokpod.lab.test:18459'; engine = 'podman-rootless'; operator = 'Ana'
            installDir = (Join-Path $script:root 'install'); secretsDir = (Join-Path $script:root 'secrets'); manualMigrations = $false
            postgres = @{ mode = 'provision' }
            keycloak = @{ mode = 'provision'; realm = 'dokpod'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge = @{ mode = 'provision'; httpsPort = 18459 }
            certificates = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' }; renewWarningDays = 30 }
            localEnvironment = @{ name = 'host-local'; stage = 'production' }
        }
        $images = [ordered]@{ api = $env:DOKPOD_TEST_API_IMAGE; bff = $env:DOKPOD_TEST_BFF_IMAGE; web = $env:DOKPOD_TEST_WEB_IMAGE; agent = $env:DOKPOD_TEST_AGENT_IMAGE }
        $infra = (Get-Content -Raw (Join-Path $PSScriptRoot '../templates/infrastructure-images.json') | ConvertFrom-Json -AsHashtable).images
        $confirmations = @{ Plan = $true }

        # Amostragem de todo container em execução: inspect (argumentos, env, mounts, log driver) e processos.
        $script:samples = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
        $script:logDrivers = [System.Collections.Concurrent.ConcurrentDictionary[string, string]]::new()
        $stop = [System.Threading.ManualResetEventSlim]::new($false)
        $poller = Start-ThreadJob -ArgumentList $script:samples, $script:logDrivers, $stop -ScriptBlock {
            param($bag, $drivers, $stopSignal)
            while (-not $stopSignal.IsSet) {
                foreach ($id in @(& docker ps --quiet 2>$null)) {
                    $inspect = (& docker inspect $id 2>$null) -join "`n"
                    if ($inspect) {
                        $bag.Add($inspect)
                        $parsed = $inspect | ConvertFrom-Json -ErrorAction SilentlyContinue
                        if ($parsed -and $parsed[0].Config.Labels.PSObject.Properties['io.dokpod.installer.task']) {
                            [void] $drivers.TryAdd($parsed[0].Name, [string] $parsed[0].HostConfig.LogConfig.Type)
                        }
                    }
                    $top = (& docker top $id 2>$null) -join "`n"
                    if ($top) { $bag.Add($top) }
                }
                Start-Sleep -Milliseconds 250
            }
        }

        $writer = [System.IO.StringWriter]::new()
        $previous = [Console]::Out
        [Console]::SetOut($writer)
        try {
            $run = { Invoke-InstallerInstall -Answers $script:answers -Images $images -Infrastructure $infra -InstallerImage $env:DOKPOD_INSTALLER_IMAGE_REF `
                    -Confirmations $confirmations -Interactive $false -Project $script:project -WaitTimeoutSeconds 420 }
            $declined = try { & $run } catch { $_.Exception }
            $confirmations['MigrationsPlanHash'] = [regex]::Match($declined.Message, '--confirm-migrations ([0-9a-f]{64})').Groups[1].Value
            $script:code = & $run
            # A reexecução também passa pelos containers avulsos (Keycloak com o provisioner, migrations).
            $script:again = & $run
        }
        finally {
            [Console]::SetOut($previous)
            $stop.Set()
            [void] (Wait-Job $poller -Timeout 15)
            Remove-Job $poller -Force -ErrorAction SilentlyContinue
        }
        $script:output = $writer.ToString() + "`n" + $declined.Message

        # Os canários: o conteúdo de cada segredo de secretsDir. Chaves PEM entram por uma linha do corpo; conexões, pela
        # senha; o PFX (binário) pela senha dele, que está no .pass.
        $script:canaries = [ordered]@{}
        foreach ($file in Get-ChildItem -LiteralPath $script:answers.secretsDir -Recurse -File) {
            if ($file.Extension -in '.crt', '.pfx') { continue }
            $text = [System.IO.File]::ReadAllText($file.FullName).Trim()
            if (-not $text) { continue }
            $value = if ($text -match 'PRIVATE KEY-----') { ($text -split "`n" | Where-Object { $_ -notmatch '-----' })[1].Trim() }
            # A senha da connection string (o campo Password do Npgsql).
            elseif ($text -match '(?i)(?<=passw' + 'ord\s*=)([^;]+)') { $Matches[1] }
            else { $text }
            if ($value.Length -ge 12) { $script:canaries[$file.FullName.Substring($script:answers.secretsDir.Length + 1)] = $value }
        }
    }

    AfterAll {
        if (-not $script:enabled) { return }
        Remove-TestComposeProject -Project $script:project
        Remove-Item -LiteralPath $script:root -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'a instalação e a reexecução terminam, e há segredos para procurar' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $script:code | Should -Be 0
        $script:again | Should -Be 0
        $script:canaries.Count | Should -BeGreaterOrEqual 8
        $script:samples.Count | Should -BeGreaterThan 0
    }

    It 'nenhum segredo na saída, nos relatórios nem nos arquivos de installDir' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $texts = [ordered]@{ saída = $script:output }
        foreach ($file in Get-ChildItem -LiteralPath $script:answers.installDir -Recurse -File) {
            $texts[$file.FullName] = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($file.FullName))
        }
        foreach ($name in $script:canaries.Keys) {
            foreach ($where in $texts.Keys) { $texts[$where].Contains($script:canaries[$name]) | Should -BeFalse -Because "$name em $where" }
        }
    }

    It 'nenhum segredo no docker inspect nem nos processos de container algum, durante a execução' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $all = @($script:samples) -join "`n"
        foreach ($name in $script:canaries.Keys) { $all.Contains($script:canaries[$name]) | Should -BeFalse -Because $name }
    }

    It 'nenhum segredo nos logs dos containers, e os avulsos não gravam log (L7)' {
        if (-not $script:enabled) { Set-ItResult -Skipped -Because 'DOKPOD_TEST_FULL_INSTALL não está ativo'; return }
        $logs = foreach ($id in @(& docker ps -aq --filter "label=com.docker.compose.project=$($script:project)")) { (& docker logs $id 2>&1) -join "`n" }
        $joined = @($logs) -join "`n"
        foreach ($name in $script:canaries.Keys) { $joined.Contains($script:canaries[$name]) | Should -BeFalse -Because $name }
        $script:logDrivers.Count | Should -BeGreaterThan 0 -Because 'a amostragem precisa ter visto algum container avulso'
        foreach ($entry in $script:logDrivers.GetEnumerator()) { $entry.Value | Should -Be 'none' -Because $entry.Key }
    }
}
