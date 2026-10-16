# Tarefas: Configurações

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/settings-api.md), [quickstart.md](quickstart.md).

**Estado**: `done` - implementada e validada em 2026-10-16.

- [x] T001 Versão do produto (D-001) e teste de consistência; `GET /bff/version`. Evidência: `ProductVersion_IsTheSameInBackendAndConsole` (props, `package.json` e `CONSOLE_VERSION`); `/bff/version` autenticado (`BffCompositionTests`); o agente passou a informar a versão do produto (`0.1.0`) em vez da versão de assembly (`0.1.0.0`).
- [x] T002 Acesso, instalação e parâmetros no plano de controle (D-002 a D-004); `canEdit` da política de justificativa (D-005); testes, inclusive canário de segredo. Evidência: `SettingsEndpointsTests` (valores efetivos, origem, canário de segredo, durações) e rotas protegidas em `ControlPlaneHttpSurfaceTests`; `AlertGovernanceServiceTests` (`canEdit`, autor e horário); API: 166 aprovados.
- [x] T003 OpenAPI e cliente. Evidência: OpenAPI (`SettingsAccess`, `SettingsInstallation`, `SettingsParameterPage`, `JustificationPolicy` com `canEdit`) e cliente regenerado.
- [x] T004 Tela Governança → Configurações (Políticas, Instalação, Retenções e limites) e item de menu por acesso; Vitest. Evidência: `settings.spec.ts` (políticas por estágio, somente leitura, falha devolve o marcador ao valor salvo, instalação, parâmetros e 403); Vitest: 177 aprovados; bundle inicial 419,72 kB (dois métodos sem uso removidos do serviço de API).
- [x] T005 E2E simulada (`test_022_*.py`) com axe. Evidência: `test_022_settings.py` (5) e `test_007_shell.py` ajustado; E2E simulada: 104 aprovados, axe sem violações sérias/críticas, sem rolagem horizontal em 1100/390/320 px.
- [x] T006 Stack real: quickstart, SC-001 a SC-004. Evidência: stack real Podman. SC-001: políticas da tela iguais às da API; homologação alterada em Configurações aparece igual em Alertas → Regras, com autor e horário, e gera `container.justification_policy_changed` com o ator; restaurada ao final. SC-002: plano de controle, BFF, console e agente `0.1.0`, última migration `202610160001_PodInventory` igual ao `__EFMigrationsHistory` e 0 pendentes, Keycloak e PostgreSQL saudáveis, 2 hosts (1 sem agente) e Podman 5.7.0 iguais ao banco e ao `podman version`. SC-003: `Commands__Retention=30 dias` e `Terminal__MaxPerUser=3` na API → "30 dias" e "3" como "configurado na instalação"; restaurado → padrão. SC-004: 7 canários (senhas, segredos e chaves dos `.env`) em 5 respostas → 0 ocorrências. Usuário sem administração: não há no laboratório; coberto pela E2E simulada.
- [x] T007 Documentação e evidências. Evidência: `docs/seguranca.md` (threat model) e `docs/frontend.md`.

## Achados da implementação e da validação

- **Marcador fora de sincronia após falha**: quando salvar a justificativa falhava, o marcador continuava com o valor clicado, porque o valor vindo da API era igual ao último renderizado e o Angular não reescrevia a propriedade nativa. `DokCheckbox.writeValue` agora acerta também o elemento nativo, e a tela devolve o valor salvo em caso de erro (teste em `settings.spec.ts`).
- **Versão do agente**: o agente informava a versão de assembly (`0.1.0.0`); passou a informar a versão do produto, como API e BFF. Agentes anteriores continuam informando o formato antigo até serem atualizados.
- **Falso positivo na varredura**: o primeiro filtro de canários pegou `KEYCLOAK_REALM` (nome do realm, não é segredo), que aparece no rótulo do ator; a varredura final usa só senhas, segredos e chaves.
- **Orçamento do bundle inicial**: o acesso a Configurações foi carregado junto com o da Auditoria (sem serviço novo) e dois métodos sem uso saíram do serviço de API do bundle inicial.
