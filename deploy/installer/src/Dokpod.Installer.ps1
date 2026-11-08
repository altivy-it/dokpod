<#
.SYNOPSIS
Instala, atualiza, recria e consulta uma instalação self-hosted do Dokpod.

.DESCRIPTION
Ponto de entrada do instalador guiado da spec 033. Executa dentro da imagem
dokpod-installer, iniciada pelo bootstrap install.sh com o socket do engine
rootless (Docker ou Podman), o diretório de instalação e o destino de segredos
montados. O host precisa somente do engine rootless com Compose v2.

Esta versão é o esqueleto da T005: reconhece os comandos do contrato e
oferece a ajuda, mas nenhum comando executa ainda. Cada comando passa a
funcionar nas tarefas indicadas em specs/033-instalador-guiado/tasks.md.

Comandos do contrato (specs/033-instalador-guiado/contracts/installer-cli.md):
install (padrão), plan, upgrade, migrations, sql, recreate, host,
certificates, status e help.

Códigos de saída: 0 sucesso ou convergido; 1 erro na aplicação; 2 uso
inválido; 3 pré-requisito não atendido; 4 confirmação recusada ou ausente;
5 assinatura ou manifesto inválido; 6 divergência de migrations; 7 outra
execução em andamento; 10 pausado para migration manual.

.PARAMETER Command
Comando do instalador. Padrão: install.

.PARAMETER RemainingArguments
Subcomandos e opções do comando, conforme o contrato. Aceita --help em
qualquer posição.

.EXAMPLE
pwsh -File deploy/installer/src/Dokpod.Installer.ps1 --help

.EXAMPLE
sh install.sh plan --answers ~/.local/share/dokpod/answers.yaml

.INPUTS
Nenhum.

.OUTPUTS
Ajuda e mensagens de uso. Nenhum segredo é exibido.

.NOTES
Requer PowerShell 7.6 ou superior. Os segredos só entram por arquivo ou
variável de ambiente referenciados no arquivo de respostas, nunca por
argumento de linha de comando.

.LINK
../README.md
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Position = 0)]
    [string] $Command = 'install',

    [Parameter(ValueFromRemainingArguments)]
    [string[]] $RemainingArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RemainingArguments = @($RemainingArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

if ($Command -in @('--help', 'help') -or $RemainingArguments -contains '--help') {
    Get-Help $PSCommandPath -Full
    exit 0
}

# Tarefa que entrega cada comando; até lá o comando é reconhecido e recusado sem efeito.
$commandTasks = [ordered]@{
    install      = 'T027 a T034'
    plan         = 'T027'
    upgrade      = 'T048'
    migrations   = 'T030'
    sql          = 'T027'
    recreate     = 'T042 a T044'
    host         = 'T046 e T047'
    certificates = 'T049'
    status       = 'T027'
}

if (-not $commandTasks.Contains($Command)) {
    [Console]::Error.WriteLine("Comando desconhecido: $Command. Consulte --help.")
    exit 2
}

[Console]::Error.WriteLine("O comando '$Command' ainda não está disponível nesta versão (spec 033, $($commandTasks[$Command])). Consulte --help.")
exit 2
