#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T024 (FR-002, FR-005, D-010, D-015, L7): install.sh com engines falsos que gravam os argumentos.

BeforeAll {
    $script:script = Join-Path $PSScriptRoot '../install.sh'

    function New-Sandbox {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-sh-$([guid]::NewGuid().ToString('N'))"
        $bin = Join-Path $root 'bin'
        $xdg = Join-Path $root 'run'
        New-Item -ItemType Directory -Path $bin, (Join-Path $xdg 'podman'), (Join-Path $root 'home') -Force | Out-Null
        foreach ($cli in 'podman', 'docker') {
            $fake = Join-Path $bin $cli
            # Grava cada argumento numa linha; "run" do cosign responde conforme FAKE_COSIGN_EXIT.
            [System.IO.File]::WriteAllText($fake, "#!/bin/sh`nfor a in `"`$@`"; do printf '%s\n' `"`$a`"; done >> `"`$FAKE_LOG`"`necho '@@FIM@@' >> `"`$FAKE_LOG`"`ncase `"`$*`" in *cosign*) exit `"`${FAKE_COSIGN_EXIT:-0}`" ;; esac`nexit 0`n")
            [System.IO.File]::SetUnixFileMode($fake, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        }

        $socketPath = Join-Path $xdg 'podman/podman.sock'
        $socket = [System.Net.Sockets.Socket]::new([System.Net.Sockets.AddressFamily]::Unix, [System.Net.Sockets.SocketType]::Stream, [System.Net.Sockets.ProtocolType]::Unspecified)
        $socket.Bind([System.Net.Sockets.UnixDomainSocketEndPoint]::new($socketPath))
        return [pscustomobject]@{ Root = $root; Bin = $bin; Xdg = $xdg; Socket = $socketPath; Handle = $socket; Log = (Join-Path $root 'calls.log'); Home = (Join-Path $root 'home') }
    }

    function Invoke-InstallSh {
        param($Sandbox, [string[]] $Arguments, [hashtable] $Environment = @{})
        $start = [System.Diagnostics.ProcessStartInfo]::new('/bin/sh')
        $start.ArgumentList.Add($script:script)
        foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.Environment['PATH'] = "$($Sandbox.Bin):/usr/bin:/bin"
        $start.Environment['HOME'] = $Sandbox.Home
        $start.Environment['FAKE_LOG'] = $Sandbox.Log
        $start.Environment['XDG_RUNTIME_DIR'] = $Sandbox.Xdg
        foreach ($key in $Environment.Keys) {
            if ($null -eq $Environment[$key]) { [void] $start.Environment.Remove($key) } else { $start.Environment[$key] = $Environment[$key] }
        }

        $process = [System.Diagnostics.Process]::Start($start)
        $stderr = $process.StandardError.ReadToEnd()
        [void] $process.StandardOutput.ReadToEnd()
        $process.WaitForExit()
        $calls = [System.Collections.Generic.List[string[]]]::new()
        if (Test-Path $Sandbox.Log) {
            foreach ($block in ([System.IO.File]::ReadAllText($Sandbox.Log) -split "@@FIM@@`n")) {
                if ($block.Trim()) { $calls.Add([string[]] ($block.TrimEnd("`n") -split "`n")) }
            }
        }

        return [pscustomobject]@{ ExitCode = $process.ExitCode; Error = $stderr; Calls = $calls }
    }
}

Describe 'install.sh' {
    BeforeEach { $script:box = New-Sandbox }
    AfterEach {
        $script:box.Handle.Dispose()
        Remove-Item -LiteralPath $script:box.Root -Recurse -Force -ErrorAction SilentlyContinue
    }

    It '--help sai com 0 e não chama o engine' {
        $result = Invoke-InstallSh $script:box @('--help')
        $result.ExitCode | Should -Be 0
        $result.Calls.Count | Should -Be 0
    }

    It 'sem XDG_RUNTIME_DIR recusa com código 3' {
        (Invoke-InstallSh $script:box @('status') @{ XDG_RUNTIME_DIR = $null }).ExitCode | Should -Be 3
    }

    It 'socket fora de XDG_RUNTIME_DIR é recusado (D-015)' {
        $outside = Join-Path $script:box.Root 'fora.sock'
        $socket = [System.Net.Sockets.Socket]::new([System.Net.Sockets.AddressFamily]::Unix, [System.Net.Sockets.SocketType]::Stream, [System.Net.Sockets.ProtocolType]::Unspecified)
        $socket.Bind([System.Net.Sockets.UnixDomainSocketEndPoint]::new($outside))
        try {
            $script:box.Handle.Dispose()
            Remove-Item -LiteralPath $script:box.Socket
            New-Item -ItemType SymbolicLink -Path $script:box.Socket -Target $outside | Out-Null
            $result = Invoke-InstallSh $script:box @('status')
            $result.ExitCode | Should -Be 3
            $result.Error | Should -Match 'fora de'
        }
        finally { $socket.Dispose() }
    }

    It 'sem o digest da release recusa com código 5' {
        $result = Invoke-InstallSh $script:box @('status')
        $result.ExitCode | Should -Be 5
        $result.Error | Should -Match 'não veio de uma release'
    }

    It 'imagem alternativa só local' {
        (Invoke-InstallSh $script:box @('status') @{ DOKPOD_INSTALLER_IMAGE = 'ghcr.io/outro/instalador:latest' }).ExitCode | Should -Be 5
    }

    It 'monta só o necessário, sem log do engine, e preserva os argumentos do instalador' {
        $answers = Join-Path $script:box.Root 'respostas com espaço.yaml'
        $secretFile = Join-Path $script:box.Root 'pg-admin.txt'
        Set-Content -LiteralPath $secretFile -Value 'x'
        Set-Content -LiteralPath $answers -Value "postgres:`n  admin:`n    password: { file: $secretFile }`nkeycloak:`n  admin:`n    password: { env: KC_ADMIN_SECRET }`n"
        $canary = 'canario-033-install-sh'

        $result = Invoke-InstallSh $script:box @('plan', '--answers', $answers, '--dry-run') @{ DOKPOD_INSTALLER_IMAGE = 'localhost/dokpod-installer:dev'; KC_ADMIN_SECRET = $canary }

        $result.ExitCode | Should -Be 0
        $run = $result.Calls[-1]
        $run[0] | Should -Be 'run'
        $run | Should -Contain '--log-driver'
        $run[[array]::IndexOf($run, '--log-driver') + 1] | Should -Be 'none'
        $run | Should -Contain "--volume=$($script:box.Socket):/run/engine.sock"
        $run | Should -Contain "--volume=$($answers):$($answers):ro"
        $run | Should -Contain "--volume=$($secretFile):$($secretFile):ro"
        $run | Should -Contain '--env=KC_ADMIN_SECRET'
        $run | Should -Contain '--security-opt=label=disable'
        $imageIndex = [array]::IndexOf($run, 'localhost/dokpod-installer:dev')
        $run[($imageIndex + 1)..($run.Count - 1)] | Should -Be @('plan', '--answers', $answers, '--dry-run')
        ($run -join "`n") | Should -Not -Match $canary
        @($run | Where-Object { $_ -match '^--volume=.*\.sock' }).Count | Should -Be 1
    }

    It 'repassa o nome público das respostas resolvido no host (o Docker não copia o /etc/hosts)' {
        $answers = Join-Path $script:box.Root 'publica.yaml'
        Set-Content -LiteralPath $answers -Value "publicUrl: https://localhost:18444`n"
        $result = Invoke-InstallSh $script:box @('plan', '--answers', $answers) @{ DOKPOD_INSTALLER_IMAGE = 'localhost/dokpod-installer:dev' }
        $result.ExitCode | Should -Be 0
        $run = $result.Calls[-1]
        $run | Should -Contain '--env=DOKPOD_HOST_PUBLIC_NAME=localhost'
        ($run | Where-Object { $_ -like '--env=DOKPOD_HOST_PUBLIC_ADDRESSES=*' }) | Should -Match '127\.0\.0\.1|::1'
    }

    It 'respostas com caminho relativo dão código 2' {
        (Invoke-InstallSh $script:box @('plan', '--answers', 'respostas.yaml') @{ DOKPOD_INSTALLER_IMAGE = 'localhost/x:dev' }).ExitCode | Should -Be 2
    }

    It 'com o digest da release, verifica a assinatura antes de executar, e falha com 5 se não conferir' {
        $released = Join-Path $script:box.Root 'install.sh'
        [System.IO.File]::WriteAllText($released, ([System.IO.File]::ReadAllText($script:script).Replace('__DOKPOD_INSTALLER_DIGEST__', ('a' * 64))))
        $original = $script:script
        $script:script = $released
        try {
            $ok = Invoke-InstallSh $script:box @('status')
            $ok.ExitCode | Should -Be 0
            $ok.Calls[0] | Should -Contain 'verify'
            ($ok.Calls[0] -join ' ') | Should -Match 'cosign:v3.1.4@sha256:'
            $ok.Calls[1][-2..-1] | Should -Be @("ghcr.io/altivy-it/dokpod-installer@sha256:$('a' * 64)", 'status')

            Remove-Item $script:box.Log
            $bad = Invoke-InstallSh $script:box @('status') @{ FAKE_COSIGN_EXIT = '1' }
            $bad.ExitCode | Should -Be 5
            $bad.Calls.Count | Should -Be 1
        }
        finally { $script:script = $original }
    }
}
