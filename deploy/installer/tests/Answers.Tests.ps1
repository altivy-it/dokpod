#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '6.2.0' }
# Spec 033 T018 (FR-001, FR-002): arquivo de respostas YAML/JSON pelo esquema, sem segredo em texto, caminhos seguros e
# modo guiado.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../src/Common.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../src/Answers.psm1') -Force

    function New-Answers {
        param([hashtable] $Override = @{})
        $answers = [ordered]@{
            schemaVersion = 1
            version       = '0.8.0'
            edition       = 'ce'
            publicUrl     = 'https://dokpod.lab.test'
            engine        = 'podman-rootless'
            operator      = 'Ana Operadora'
            installDir    = '/tmp/dokpod-033/install'
            secretsDir    = '/tmp/dokpod-033/secrets'
            postgres      = @{ mode = 'provision' }
            keycloak      = @{ mode = 'provision'; initialAdmin = @{ username = 'dokpod-admin'; email = 'ana@lab.test' } }
            edge          = @{ mode = 'provision' }
            certificates  = @{ edge = @{ source = 'local-ca' }; api = @{ source = 'local-ca' }; agents = @{ source = 'local-ca' } }
        }
        foreach ($key in $Override.Keys) { $answers[$key] = $Override[$key] }
        return $answers
    }

    function Write-Answers {
        param($Answers, [string] $Extension = '.json')
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-answers-$([guid]::NewGuid().ToString('N'))$Extension"
        [System.IO.File]::WriteAllText($path, ($Answers | ConvertTo-Json -Depth 20))
        return $path
    }
}

Describe 'Arquivo de respostas' {
    It 'aceita JSON válido e aplica os padrões' {
        $answers = Read-InstallerAnswers -Path (Write-Answers (New-Answers))

        $answers.engine | Should -Be 'podman-rootless'
        $answers.edge.httpsPort | Should -Be 443
        $answers.keycloak.realm | Should -Be 'dokpod'
        $answers.localEnvironment.stage | Should -Be 'production'
        $answers.certificates.renewWarningDays | Should -Be 30
    }

    It 'lê YAML com números, booleanos e texto entre aspas' {
        $yaml = @"
schemaVersion: 1
version: "0.8.0"
edition: business
publicUrl: https://dokpod.lab.test:8443
engine: docker-rootless
operator: Ana Operadora
installDir: /tmp/dokpod-033/install
secretsDir: /tmp/dokpod-033/secrets
manualMigrations: true
agentGrpcPort: 17443
postgres:
  mode: provision
keycloak:
  mode: provision
  initialAdmin: { username: dokpod-admin, email: ana@lab.test }
edge:
  mode: provision
  httpsPort: 8443
certificates:
  edge: { source: local-ca }
  api: { source: local-ca, keyAlgorithm: rsa-3072 }
  agents: { source: local-ca }
"@
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-$([guid]::NewGuid().ToString('N')).yaml"
        [System.IO.File]::WriteAllText($path, $yaml)

        $answers = Read-InstallerAnswers -Path $path

        $answers.version | Should -Be '0.8.0'
        $answers.edge.httpsPort | Should -Be 8443
        $answers.agentGrpcPort | Should -Be 17443
        $answers.manualMigrations | Should -BeTrue
        $answers.certificates.api.keyAlgorithm | Should -Be 'rsa-3072'
    }

    It 'recusa segredo escrito em texto e diz onde' {
        # Chave por índice: o par chave=valor de senha não aparece no texto (T056).
        $admin = @{ username = 'postgres' }
        $admin['password'] = 'canario-033'
        $answers = New-Answers @{ postgres = @{ mode = 'existing-create'; host = 'db'; port = 5432; admin = $admin } }

        { Read-InstallerAnswers -Path (Write-Answers $answers) } | Should -Throw -ExpectedMessage '*$.postgres.admin.password*{ file*'
    }

    It 'aceita segredo por { file } ou { env }' {
        $admin = @{ username = 'postgres' }
        $admin['password'] = @{ env = 'PG_ADMIN' }
        $answers = New-Answers @{ postgres = @{ mode = 'existing-create'; host = 'db'; port = 5432; admin = $admin } }

        (Read-InstallerAnswers -Path (Write-Answers $answers)).postgres.admin.password.env | Should -Be 'PG_ADMIN'
    }

    It 'recusa o que o esquema não aceita: <Name>' -ForEach @(
        @{ Name = 'engine rootful'; Override = @{ engine = 'podman-rootful' } }
        @{ Name = 'URL sem https'; Override = @{ publicUrl = 'http://dokpod.lab.test' } }
        @{ Name = 'campo desconhecido'; Override = @{ inventado = 1 } }
        @{ Name = 'upstreamHost fora de loopback'; Override = @{ edge = @{ mode = 'existing'; upstreamHost = '10.0.0.5' } } }
    ) {
        { Read-InstallerAnswers -Path (Write-Answers (New-Answers $Override)) } | Should -Throw
    }

    It 'recusa caminhos iguais, aninhados ou relativos: <Name>' -ForEach @(
        @{ Name = 'iguais'; Install = '/tmp/x'; Secrets = '/tmp/x' }
        @{ Name = 'segredos dentro da instalação'; Install = '/tmp/x'; Secrets = '/tmp/x/secrets' }
        @{ Name = 'instalação dentro dos segredos'; Install = '/tmp/s/i'; Secrets = '/tmp/s' }
        @{ Name = 'relativo'; Install = 'install'; Secrets = '/tmp/s' }
    ) {
        { Assert-InstallPaths -InstallDirectory $Install -SecretsDirectory $Secrets } | Should -Throw
    }

    It 'recusa caminho dentro de um checkout Git' {
        $repo = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-repo-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path (Join-Path $repo '.git') -Force | Out-Null

        { Assert-InstallPaths -InstallDirectory (Join-Path $repo 'install') -SecretsDirectory '/tmp/dokpod-033-s' } | Should -Throw -ExpectedMessage '*Git*'
    }

    It 'grava as respostas sem segredos e relê igual' {
        $answers = Read-InstallerAnswers -Path (Write-Answers (New-Answers))
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "dokpod-033-$([guid]::NewGuid().ToString('N'))/answers.yaml"

        Save-InstallerAnswers -Answers $answers -Path $path

        (Read-InstallerAnswers -Path $path).publicUrl | Should -Be 'https://dokpod.lab.test'
    }

    It 'sem terminal, resposta obrigatória faltando dá código 2 com a lista' {
        $env:DOKPOD_NON_INTERACTIVE = '1'
        try {
            $failure = $null
            try { Request-InstallerAnswers -Partial ([ordered]@{ edition = 'ce' }) | Out-Null } catch { $failure = $_ }
            $failure.Exception.ExitCode | Should -Be 2
            $failure.Exception.Message | Should -Match 'version.*publicUrl.*engine.*operator'
        }
        finally {
            Remove-Item Env:DOKPOD_NON_INTERACTIVE
        }
    }

    It 'o esquema da imagem é o do contrato da spec' -Skip:(-not (Test-Path '/src/specs/033-instalador-guiado/contracts/answers.schema.json')) {
        Get-FileHash (Get-AnswersSchemaPath) | Select-Object -ExpandProperty Hash |
            Should -Be (Get-FileHash '/src/specs/033-instalador-guiado/contracts/answers.schema.json').Hash
    }
}
