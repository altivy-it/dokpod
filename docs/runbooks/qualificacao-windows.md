# Runbook: qualificação do agente Windows (spec 006)

Roteiro para a máquina Windows de testes da [spec 006](../../specs/006-qualificacao-windows-podman/tasks.md). O agente roda como Windows Service nativo, falando com o Docker local pelo named pipe e com a API do laboratório por gRPC mTLS. O Podman Windows está adiado (decisão de 2026-10-24) e fica fora deste roteiro.

Cada passo diz o que guardar como evidência. Evidência é texto sanitizado (sem certificado, chave, token ou hostname real de cliente) colado na tarefa correspondente da 006.

## O que preparar no laboratório Linux

1. **Pacote** (gerado em container, a partir do commit atual):

   ```bash
   deploy/agent/publish-windows-agent.sh
   ```

   Gera em `artifacts/windows-agent/` o `dokpod-agent-<versão>+<commit>-win-x64.zip`, o `.sha256` e um `.json` com commit e versão. O pacote é self-contained: não exige runtime .NET no Windows e não leva certificado, chave nem endpoint.

2. **Ambiente**: cadastre um ambiente novo no console (por exemplo, "Laboratório Windows") e anote o UUID.

3. **Identidade**:

   ```bash
   tools/windows/provision-windows-agent.sh <uuid-do-ambiente> ~/.microsoft/usersecrets/Dokpod/windows
   ```

   Emite o PFX do agente pela CA de laboratório, registra o fingerprint e copia a CA. Leve `dokpod-agent-windows.pfx` e `dokpod-dev-ca.crt` ao Windows por canal protegido. Não versione.

4. **Rede**: a API do laboratório responde em `0.0.0.0:17443`. O certificado da API vale para `api`, `dokpod-api`, `localhost` e `127.0.0.1`, e o agente valida o nome desde a correção A1. Por isso o Windows usa o nome `dokpod-api` (passo 3 abaixo). Libere a porta 17443 no firewall do host Linux para a máquina Windows.

## Na máquina Windows

Requisitos: Windows 10/11 ou Windows Server com Docker (Docker Desktop ou Docker Engine), PowerShell 7.4 e uma sessão elevada para instalar e remover o serviço.

1. **Verificar o pacote** antes de instalar, comparando com o `.sha256` gerado no laboratório:

   ```powershell
   (Get-FileHash .\dokpod-agent-*.zip -Algorithm SHA256).Hash.ToLower()
   ```

2. **Instalar os arquivos** em diretórios separados, binário somente leitura e dados protegidos:

   ```powershell
   Expand-Archive .\dokpod-agent-*.zip -DestinationPath 'C:\Program Files\Dokpod\Agent\<versão>'
   New-Item -ItemType Directory 'C:\ProgramData\Dokpod\Agent\identity' -Force
   Copy-Item .\dokpod-agent-windows.pfx, .\dokpod-dev-ca.crt 'C:\ProgramData\Dokpod\Agent\identity\'
   ```

3. **Nome da API**: adicione em `C:\Windows\System32\drivers\etc\hosts` a linha `<IP-do-laboratório> dokpod-api` e confira com `Test-NetConnection dokpod-api -Port 17443`.

4. **Registrar o serviço**:

   ```powershell
   ./deploy/agent/manage-windows-service.ps1 -Action Install `
     -BinaryPath 'C:\Program Files\Dokpod\Agent\<versão>\Dokpod.Agent.exe' `
     -ControlPlaneEndpoint https://dokpod-api:17443 `
     -EnvironmentId <uuid-do-ambiente> `
     -ClientCertificatePath C:\ProgramData\Dokpod\Agent\identity\dokpod-agent-windows.pfx `
     -ServerCaCertificatePath C:\ProgramData\Dokpod\Agent\identity\dokpod-dev-ca.crt `
     -DataDirectory C:\ProgramData\Dokpod\Agent
   ./deploy/agent/manage-windows-service.ps1 -Action Start
   ```

   O script registra startup automático atrasado e recuperação limitada (duas reinicializações a cada 60 s, zeradas em 24 h). A conta padrão é `LocalSystem`, que só vale para o primeiro teste.

## Checklist de qualificação

| # | Verificação | Como | Tarefa |
| --- | --- | --- | --- |
| W1 | Serviço sobe sem runtime .NET instalado | `dotnet --list-runtimes` ausente ou vazio; `Get-Service DokpodAgent` em `Running`; host aparece no console com inventário do Docker | T003 |
| W2 | Startup atrasado e recuperação limitada | `sc.exe qc DokpodAgent` (`AUTO_START (DELAYED)`), `sc.exe qfailure DokpodAgent`; matar o processo duas vezes e ver o SCM reiniciar, e na terceira não | T003 |
| W3 | Reinício da máquina | Reiniciar o Windows; o serviço volta sozinho e reconecta, sem nova identidade | T003 |
| W4 | Conta dedicada e ACL | Trocar para conta virtual: `sc.exe config DokpodAgent obj= "NT SERVICE\DokpodAgent"`; dar à conta leitura no PFX e escrita em `C:\ProgramData\Dokpod\Agent` (`icacls`); registrar se o pipe `docker_engine` aceita a conta (Docker Desktop: grupo `docker-users`) | T003 |
| W5 | Acesso negado a quem não deve | Com um usuário comum, tentar ler `C:\ProgramData\Dokpod\Agent\identity\*.pfx` e o diretório de dados: deve ser negado | T003, FR-007 |
| W6 | Ações pelo console | Iniciar, parar, reiniciar e remover um container de teste; conferir na auditoria | T003 |
| W7 | Atualização e rollback | Extrair uma versão nova em outra pasta, `-Action Stop`, apontar o serviço para o novo binário (`sc.exe config DokpodAgent binPath= ...`), `-Action Start`; depois voltar ao binário anterior. A identidade e o diretório de dados não mudam | T003 |
| W8 | Remoção preserva dados | `-Action Remove -Confirm`; o serviço some e `C:\ProgramData\Dokpod\Agent` continua (sem `-RemoveData`) | T003 |
| W9 | Journal sob crash | Com um comando em andamento (por exemplo, `restart` de um container lento), matar o processo do agente; ao voltar, o comando termina uma vez só, sem efeito duplicado, e o resultado chega à API | T004 |
| W10 | Revogação | Revogar a identidade no console; a sessão cai e o agente não reconecta | T003 |

Ao terminar, registre os resultados nas tarefas T003 e T004 da 006 e leve a decisão (stable, experimental ou adiada) para o gate 15 da [005](../../specs/005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).

## Limpeza

1. `./deploy/agent/manage-windows-service.ps1 -Action Remove -Confirm -RemoveData` e apagar `C:\Program Files\Dokpod`.
2. Remover a linha `dokpod-api` do arquivo hosts.
3. No console, revogar a identidade e desabilitar o ambiente de teste.
