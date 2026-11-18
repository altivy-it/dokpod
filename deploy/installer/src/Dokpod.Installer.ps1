<#
.SYNOPSIS
Instala, atualiza, recria e consulta uma instalação self-hosted do Dokpod.

.DESCRIPTION
Ponto de entrada do instalador guiado da spec 033. Executa dentro da imagem dokpod-ce-installer, iniciada pelo bootstrap
install.sh com o socket do engine rootless (Docker ou Podman), o diretório de instalação e o destino de segredos
montados. O host precisa somente do engine rootless com Compose v2.

A lógica fica em Cli.psm1 e nos demais módulos de src/. Comandos: install (padrão), plan, upgrade, migrations, sql,
recreate, host, certificates, status e help. Use "help" para a lista completa de opções e códigos de saída.

Os argumentos (comando, subcomandos e opções) seguem specs/033-instalador-guiado/contracts/installer-cli.md e são lidos
de $args, sem bloco param: com CmdletBinding, opções como --out colidiriam com os parâmetros comuns do PowerShell.

.EXAMPLE
pwsh -File deploy/installer/src/Dokpod.Installer.ps1 --help

.EXAMPLE
sh install.sh plan --answers /home/ana/dokpod/answers.yaml

.NOTES
Requer PowerShell 7.6 ou superior. Os segredos só entram por arquivo ou variável de ambiente referenciados no arquivo
de respostas, nunca por argumento de linha de comando.

.LINK
../README.md
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

[string[]] $Arguments = @($args | Where-Object { $null -ne $_ } | ForEach-Object { [string] $_ })
Import-Module (Join-Path $PSScriptRoot 'Cli.psm1')
exit (Invoke-InstallerCli -Arguments $Arguments)
