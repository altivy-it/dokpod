# Segurança do Dokpod

## Ativos e fronteiras

Os ativos críticos são controle dos hosts, identidade de agentes e usuários, credenciais de registry, configuração, trilha de auditoria e metadados de workloads. A fronteira de maior risco é o agente com acesso ao socket Docker ou Podman.

## Ameaças prioritárias

| Ameaça | Controle mínimo |
| --- | --- |
| agente falso | inscrição de uso único, aprovação e mTLS por ambiente |
| servidor falso | agente confia apenas na CA configurada e valida hostname |
| replay de comando | ID idempotente, nonce/revisão, expiração e registro durável |
| usuário opera ambiente indevido | autorização por ação e recurso antes de revelar dados |
| proxy vira execução arbitrária | comandos allowlist e DTOs estritos, sem proxy genérico |
| SSRF via configuração de endpoint | agente acessa socket local conhecido; egress do servidor restrito |
| comprometimento do agente | imagem mínima, assinatura, SBOM, patching e escopo reduzido |
| vazamento em logs | redaction, limites e proibição de secrets/corpo por padrão |
| ação destrutiva acidental | confirmação, escopo explícito e auditoria |
| supply chain | versões fixadas, proveniência, scan e licença verificada |

## Identidade

Keycloak é a autoridade externa obrigatória para usuários, credenciais, MFA, federação, SSO, sessões, grupos, roles, recursos, scopes, políticas e decisões de autorização. O Dokpod não implementa cadastro, senha, recuperação de conta, diretório de usuários, membership ou política própria. O realm `dokpod` é separado do realm administrativo `master`.

O BFF usa OIDC/OAuth 2.0 Authorization Code com PKCE como cliente confidencial. O browser recebe somente cookie `Secure`, `HttpOnly` e `SameSite` apropriado; access e refresh tokens ficam no servidor. Operações mutáveis usam proteção CSRF e conexões SignalR validam Origin. A API atua como PEP, valida token e aplica a decisão deny-by-default do Keycloak antes de listar, detalhar, operar, auditar ou assinar notificações de um ambiente.

Cada ambiente usa recurso opaco no Keycloak e scopes explícitos como `environment:read`, `container:start`, `container:stop`, `container:restart`, `container:delete` e `audit:read`. A integração que registra recursos é idempotente, reconciliável, auditável e usa credencial de serviço com menor privilégio. Decisão ausente, inválida, expirada ou indisponível falha fechada; memberships e políticas não são copiadas para o PostgreSQL do Dokpod.

Agentes não usam identidade de usuário. Cada ambiente possui certificado próprio, revogável e rotacionável, persistido em volume protegido. O servidor deriva o ambiente do certificado e valida cadeia, EKU, expiração e revogação com falha fechada. Tokens de bootstrap são aleatórios, armazenados como hash, vinculados ao ambiente e à chave pública, consumidos transacionalmente e nunca aparecem em argumentos ou logs. A aprovação mostra o fingerprint; revogação encerra streams ativos.

## Engine local

A posse do socket Docker costuma equivaler a root no host. A API Podman concede execução arbitrária como o usuário do serviço. Marcar um mount como read-only não transforma uma API mutável em somente leitura.

O agente deve:

- abrir somente o socket configurado;
- validar esquema e path permitidos;
- limitar corpo, headers, duração e concorrência;
- converter comandos para chamadas conhecidas;
- nunca aceitar URL, método ou payload bruto enviados pelo browser;
- rejeitar URI absoluta, path injection, redirects e headers não permitidos antes do socket;
- recusar operação fora das capacidades anunciadas;
- eliminar secrets de erros antes do reporte.

No Windows, o startup valida named pipe, ACL, conta do serviço, RID e versão do engine contra a matriz suportada. Pipe alternativo ou permissões mais amplas que a política são recusados. O executável self-contained é assinado; chaves, certificados e journal ficam em diretório persistente separado e protegido.

## Dados e auditoria

Inventário contém metadados potencialmente sensíveis. Variáveis de ambiente, conteúdo de secrets e arquivos não são coletados. Logs e inspeção não são coletados como telemetria nem persistidos: o console os lê **sob demanda**, para o usuário autorizado, pelo canal do agente (spec 011, threat model abaixo). Labels e nomes são tratados como não confiáveis e escapados na interface.

A telemetria usa allowlist de campos, limites e normalização de caracteres de controle. A auditoria é append-only e registra identidade, ação, recurso, horário, correlation ID e resultado. A role normal da aplicação não recebe `UPDATE` ou `DELETE` sobre auditoria; integridade e retenção serão definidas antes de produção. Acesso à auditoria também exige autorização.

## Eventos operacionais (spec 008)

- Somente tipos da allowlist (`OperationalEventTypes`) são aceitos; tipo desconhecido é descartado. Parâmetros seguem allowlist por nome, com caracteres de controle removidos, valores limitados a 256 caracteres e o conjunto a 2 KiB; labels, variáveis de ambiente e comandos dos containers nunca são copiados.
- Horário informado pelo agente é preservado, mas divergência acima de 5 minutos do recebimento é sinalizada e não altera a ordenação de auditoria.
- `dokpod_apply_temporal_retention` é `SECURITY DEFINER`; desde a migration `202610130004_RestrictTemporalRetention` aceita apenas `environment_events`, `host_resource_samples`, `agent_commands` e `agent_command_parameters` e as colunas temporais previstas. Auditoria nunca é purgada por essa função.
- Gravação de inventário usa transação `SERIALIZABLE` repetida em `40001`/`40P01`, evitando perda de snapshot quando vários agentes publicam ao mesmo tempo.

## Threat model: inspeção e logs sob demanda (spec 011)

**Fronteira nova**: navegador → BFF → API → stream mTLS do agente → API do engine (`/containers/{id}/json` e `/logs`). Consultas são mensagens tipadas (`SUMMARY`, `INSPECT`, `LOGS`), nunca repasse genérico de rota ou payload.

**Atores**: operador autorizado (`container:inspect`, `container:logs`), usuário sem o scope, usuário de outro ambiente, agente comprometido, administrador do host.

| Ameaça | Mitigação | Teste negativo |
| --- | --- | --- |
| Leitura cruzada de ambiente (IDOR) | Autorização Keycloak por ambiente em toda consulta; 404 indistinguível de "não autorizado" para container | container de ambiente não autorizado responde 404 |
| Scope ausente | `container:inspect`/`container:logs` avaliados por requisição e a cada 60 s no streaming | 403 em `summary`, `inspect`, `logs` e no hub sem o scope |
| Vazamento de segredo por campo do engine | Allowlist no resumo; denylist na inspeção (`Config.Env`, `Config.CreateCommand`, binds fora da allowlist, chaves com `password\|secret\|token\|key\|credential\|auth`); `SecretMasker` sobre todo valor de texto (labels, opções de log, health check, argumentos e campos desconhecidos) | canários por campo ausentes em respostas, logs de nginx/BFF/API/agente, auditoria e PostgreSQL |
| Vazamento de segredo em logs | Mascaramento no agente antes do envio (Bearer, `password=`, `secret=`, `token=`, `user:pass@`, `-u user:pass`, chaves privadas, JWT); aviso de best-effort na interface | linhas com canários chegam mascaradas |
| Termo de busca em logs de acesso | `contains` literal, ≤ 128; API e BFF não registram URLs com query (`Microsoft.AspNetCore.Hosting.Diagnostics` e `System.Net.Http.HttpClient` em Warning, `QueryLoggingTests`); o gateway da plataforma de identidade, fora deste repositório, deve registrar o caminho sem query para `/dokpod/api/` | termo canário ausente nos logs |
| Persistência indevida | Conteúdo nunca vai ao PostgreSQL; auditoria grava só metadados (ator, ambiente, container, volume) | varredura do banco por canário |
| DoS no agente/canal gRPC | ≤ 4 consultas e ≤ 10 sessões ao vivo por agente, ≤ 3 por usuário; mensagens ≤ 256 KiB (limite gRPC 1 MiB); 200 linhas/s e 256 KiB/s por sessão; prioridade de comandos sobre consultas | resposta máxima não derruba a sessão; 429 acima dos limites |
| Revogação durante sessão ao vivo | `StreamingAuthorizationMonitor` (compartilhado com a 014): reavaliação a cada 60 s; token da conexão perto do `exp` encerra com `session_refresh_required` e o navegador reabre a conexão com token renovado pelo BFF; Keycloak indisponível encerra (falha fechada); logout e expiração da sessão do BFF encerram; `STOP` enviado ao agente | sessão encerrada em ≤ 60 s após revogar; sessão longa sobrevive à renovação do token |
| Conteúdo malicioso no log (XSS, ANSI) | Sequências ANSI e controles removidos no agente; renderização somente como texto | linha com `<script>` e ANSI exibida como texto |
| Agente comprometido | Resposta tratada como dado não confiável, com limites de tamanho e schema; nunca executada | resposta acima do limite é truncada |

**Risco residual**: segredos sem padrão reconhecível em campos fora da denylist podem aparecer na inspeção; a interface avisa que o mascaramento é best-effort. Aprovação: decisão do solicitante registrada em `specs/README.md` (2026-10-13), com revisão humana de segurança pendente antes de release.

## Threat model: inventário de imagens e pull (spec 012)

**Fronteira nova**: navegador → BFF → API → fila de comandos (PostgreSQL) → stream mTLS do agente → `POST /images/create` do engine → registro de imagens. O agente passa a falar, indiretamente, com registros externos escolhidos pelo usuário.

**Atores**: operador com `image:pull`, usuário sem o scope, usuário de outro ambiente, registro malicioso ou comprometido, agente comprometido, administrador do host (dono das credenciais de registro).

| Ameaça | Mitigação | Teste negativo |
| --- | --- | --- |
| Injeção na referência repassada ao engine | Gramática OCI estrita (`ImageReference`) na API **e** no agente; nome e tag/digest vão como parâmetros codificados (`fromImage`, `tag`), nunca concatenados ao caminho | referências com espaço, `?`, `#`, `\`, maiúsculas no caminho, `;`, quebra de linha ou fora da gramática → `invalid_reference` (`ImageReferenceTests`, `ImagePullServiceTests`, `ImagePullTests`) |
| Credencial embutida na referência (`user:senha@registry/...`) | Recusada pela gramática (o `@` só é aceito antes de digest válido); credenciais de registro ficam no host (`DOKPOD_AGENT_REGISTRY_AUTH_FILE`) e só seguem do agente ao engine local em `X-Registry-Auth` | referência com credencial → 400; arquivo de credenciais nunca lido pela API; varredura gitleaks |
| Uso do host como proxy para registros internos ou não confiáveis | Lista opcional `Images:AllowedRegistriesByStage` (por estágio; vazia = qualquer registro); scope `image:pull` por host | registro fora da lista → `registry_not_allowed` (403) |
| Scope ausente / ambiente não autorizado | Decisão Keycloak `image:pull` por ambiente antes de ler o cadastro; falha do Keycloak = negado (`authorization_unavailable`) | 403 sem o scope; nenhum comando persistido |
| Agente sem suporte | Servidor só envia `PULL_IMAGE` a agentes com capability 13; API responde 422 `not_supported` | agente sem `ImagePull` → 422 |
| Replay e conflito de idempotência | `Idempotency-Key` por host; hash `v2` cobre tipo, alvo e referência canônica (sem prazo) | mesma chave com outra referência → 409 `command_id_conflict` |
| Esgotamento de disco / pull infinito | Prazo do comando (≤ 30 min) aplicado no agente; `insufficient_storage` mapeado; um pull por imagem por host ao mesmo tempo (trava por nome) | prazo curto → `deadline_exceeded` |
| Imagem maliciosa (supply chain) | Fora do escopo: sem verificação de assinatura nesta spec (D-005); aviso de tag mutável e recomendação de digest na interface; auditoria `image.pull` com ator, host e comando | — (risco residual) |
| Agente comprometido falsificando inventário | Snapshot paginado validado (ordem, identidade, ≤ 200 imagens e 256 KiB por página, ≤ 64 tags/digests); dados tratados como não confiáveis e só exibidos como texto | página fora de ordem encerra a sessão (`image_snapshot_invalid`) |

**Retenção de comandos (D-008)**: comandos concluídos ou com falha (e seus parâmetros, por `ON DELETE CASCADE`) são removidos após `Commands:Retention` (padrão 90 dias) em lotes pelo `AgentCommandExpirationWorker`; pendentes e indeterminados nunca são removidos (ver `docs/runbooks/operacao-comandos-postgresql.md`). A trilha de auditoria não é afetada.

**Risco residual**: sem verificação de assinatura, um registro comprometido pode entregar imagem maliciosa a um usuário autorizado; a mitigação é operacional (registros permitidos por estágio, digests, revisão). Aprovação: decisão do solicitante registrada em `specs/README.md`, com revisão humana de segurança pendente antes de release.

## Threat model: criação de container (spec 013)

**Fronteira nova**: navegador → BFF → API (validação por allowlist + cifragem dos valores) → fila de comandos → stream mTLS → agente (validação defensiva) → `POST /containers/create` (Docker/Podman compatível) ou `POST /libpod/containers/create` (Podman, somente para segredos do Podman e health check com reinício) → `start`. Criar container equivale a executar código arbitrário no host com as permissões do engine; é a operação mais poderosa do console.

**Atores**: operador com `container:create`, usuário sem o scope, usuário de outro ambiente, imagem maliciosa, agente comprometido, administrador do host (dono de segredos e da allowlist de bind).

| Ameaça | Mitigação | Teste negativo |
| --- | --- | --- |
| Escalada por opções do engine (privileged, cap-add, devices, namespaces do host, security-opt, sysctls, userns) | Allowlist fechada no contrato (`additionalProperties: false`, campo extra → 400) e no domínio (`ContainerCreationSpec`); o agente monta o corpo do engine só a partir da allowlist e revalida (defesa em profundidade) | corpo com `privileged`, `capAdd`, `devices`, `pidMode`, `networkMode: host`, `securityOpt`, `sysctls`, `userns` → 400 na API; spec inválida no agente → `invalid_spec` |
| Montagem do socket do engine ou de caminhos do host | Volumes nomeados por padrão; bind só de um diretório exato da allowlist do agente (`DOKPOD_AGENT_BIND_MOUNT_ALLOWLIST`, vazia por padrão, sem diretórios aninhados), com caminho canônico e sem subcaminhos: o engine segue links simbólicos na origem do bind, e um container com o diretório montado poderia trocar um subcaminho por um link para `/` entre a validação e a montagem; socket do engine (`docker.sock`, `podman.sock`) e `/`, `/etc`, `/proc`, `/sys`, `/dev`, `/var/run`, `/run` sempre recusados | bind fora da allowlist, de subcaminho de diretório permitido ou para o socket → `mount_not_allowed`; allowlist aninhada impede o agente de iniciar |
| Rede do host / rede arbitrária | Rede só por nome de rede existente; `host`, `none:`-prefixos e `container:` recusados | `network: host` → 400 |
| Vazamento de valores de variáveis | Todos os valores cifrados com Data Protection da API (key ring próprio, propósito `Dokpod.CommandParameters.v1`) enquanto o comando não é terminal; JSON de parâmetros guarda só nomes, flag `sensitive` e referências; valores nunca retornam pela API, auditoria, eventos ou logs; purga (`protected_values = NULL`, `purged_at`) após estado terminal; comando equivalente com `***` | canários em valores ausentes de respostas, auditoria, `agent_command_parameters` após terminal e logs (`ContainerCreationIntegrationTests`, `SensitiveValuesCanaryTests`) |
| Segredos do host | Referência por nome (`podman_secret` resolvido pelo Podman via `secret_env`; `file` lido pelo agente de `<data>/secrets/<nome>`, nome sem `/`, `..` ou caracteres de controle); plano de controle conhece só o nome | nome com `../` → `invalid_spec`; segredo inexistente → `secret_not_found` |
| Perda do key ring da API | Valores não recuperáveis fora da API; comandos não terminais falham com `parameters_unavailable` (auditado); nenhum container é criado | chave removida → `parameters_unavailable` |
| Reinício do agente entre aceite e `create` | Journal sem valores (só hash); reconciliação por label `dokpod.command-id`; reentrega com valores enquanto não terminal; purgado/expirado → `parameters_unavailable` | reentrega após reinício não duplica (`ContainerCreatorTests`, Podman real) |
| Retry e duplicidade | `Idempotency-Key`; hash `v2` sobre a forma canônica (valores entram como SHA-256, nunca em claro); label de reconciliação | mesma chave → mesmo comando; corpo diferente → 409 |
| Volumes anônimos apagados por remoção automática (princípio II) | Agente inspeciona a imagem e recusa `autoRemove` se algum `VOLUME` declarado não tiver volume nomeado no destino; `autoRemove` exige `restartPolicy: no` | imagem com `VOLUME` + `autoRemove` → `anonymous_volume_with_auto_remove` |
| DoS por recursos | Limites fixos (32 portas, 100 variáveis, 10 volumes, valor ≤ 32 KiB, CPU 0,01-64, memória 6 MiB-1 TiB); prazo do comando | limites excedidos → 400 por campo |
| Imagem maliciosa / registro não confiável | Registros permitidos por estágio (012); auditoria com o comando equivalente; sem verificação de assinatura (risco residual da 012) | registro fora da lista → `registry_not_allowed` |
| Capability ausente | Servidor só envia `CREATE_CONTAINER` a agentes com `CAPABILITY_CONTAINER_CREATE` (14), anunciada apenas no Linux | agente sem a capability → 422 `not_supported` |

**Risco residual**: um operador autorizado pode executar qualquer imagem permitida com as permissões não privilegiadas do engine; a mitigação é o scope dedicado, registros por estágio e a auditoria. Aprovação: decisão do solicitante registrada em `specs/README.md` (2026-10-13), com revisão humana de segurança pendente antes de release.

## Threat model: terminal interativo (spec 014)

**Fronteira nova**: navegador → BFF (WebSocket) → hub SignalR da API (`OpenTerminal`) → `ExecOpenRequest` no stream `Connect` → RPC `Exec` aberto pelo agente (mTLS, vínculo a sessão/fencing) → `POST /containers/{id}/exec` + `/exec/{id}/start` (conexão sequestrada) no engine. Decisão em ADR 2026-0006.

**Atores**: operador com `container:exec`, usuário sem o scope, usuário de outro ambiente, sessão do navegador sequestrada, workload malicioso (saída hostil), agente comprometido.

| Ameaça | Mitigação | Teste negativo |
| --- | --- | --- |
| Abuso do shell / escalada | Scope `container:exec` por ambiente; comando fixo (`/bin/bash` → `/bin/sh`), usuário padrão da imagem, sem `Privileged`, `User` ou `Env` no exec; alerta visível quando o usuário do container é root | sem scope → `scope` (403); request com comando/usuário não existe no contrato |
| Bloqueio por workload | Label `dokpod.exec=disabled` verificada pelo agente | label → `label_disabled` |
| Abertura sem justificativa em Produção | Justificativa 10-500 caracteres, mascarada (`SecretMasker`) antes da auditoria | ausente/curta → `justification_missing` |
| Sessão longa ou esquecida | Ociosidade 10 min, duração 30 min, aviso 1 min antes; queda do navegador encerra em ≤ 30 s (keepalive 15/30 s) | `idle_timeout`, `max_duration`, `client_disconnected` |
| Revogação durante a sessão | `StreamingAuthorizationMonitor` a cada 60 s (token renovado pelo BFF), Keycloak indisponível encerra (falha fechada), identidade do agente revogada derruba o canal | `permission_revoked` |
| Sessão órfã no engine | Fechar o TTY (SIGHUP) e confirmar `Running=false` em ≤ 5 s; IDs ativos em `<data>/exec-sessions.json`, verificados após reinício; senão `orphan_suspected` | inspeção do exec após cada motivo de término (Podman real) |
| Processos em segundo plano (`nohup`, `&`, `setsid`) | Fora da garantia: o engine não encerra execs | — (risco residual) |
| Sequências maliciosas na saída (OSC 52, links, títulos) | xterm.js restritivo: sem addon de links, sem clipboard OSC 52, `allowProposedApi: false`; colar multi-linha exige confirmação | saída com OSC 52 não altera a área de transferência |
| DoS do agente/canal | RPC separado do controle; ≤ 4 sessões por agente, 2 por usuário, 1 por usuário por container; saída ≤ 256 KiB/s por sessão; entrada ≤ 64 KiB por mensagem | limite → `limit` |
| Replay/sequestro do canal do agente | Handshake com `exec_session_id` pendente, `session_id` e fencing token ativos; at-most-once; recusa após `expires_at` | handshake com fencing antigo ou id desconhecido é recusado |
| Transporte inseguro do navegador | WebSocket obrigatório (sem long polling/SSE) | outro transporte → `websocket_required` |
| Vazamento de conteúdo | Conteúdo nunca vai a PostgreSQL, logs ou telemetria; auditoria com metadados (bytes, duração, motivo) | canários digitados/exibidos ausentes do banco e dos logs |

**Janela de revogação**: até 60 s mais a vida útil restante do access token (realm com access token ≤ 5 min, `docs/configuracao-keycloak.md`). **Risco residual**: processos em segundo plano criados pelo usuário e ações executadas antes da revogação. Aprovação: decisão do solicitante registrada em `specs/README.md` (2026-10-13); ADR 2026-0006 `accepted` em 2026-10-14; revisão humana de segurança pendente antes de release.

## Threat model: governança de alertas e ações justificadas (spec 016)

**Fronteira nova**: eventos gravados (`OperationalEventService`) → `AlertEvaluator` → `dokpod.alerts`; varredura (`AlertSweeper`, 30 s) sobre inventário, conexão e amostras em memória; API de alertas, regras, silenciamentos, política e justificativas; `justification` opcional em `POST .../commands`, gravada em `dokpod.container_action_justifications` fora do payload assinado do agente (D-005).

**Atores**: operador com `alert:read`/`alert:manage`, administrador com `environment:manage`, usuário sem acesso a um host, cliente da API que ignora a UI.

| Ameaça | Mitigação | Teste negativo |
| --- | --- | --- |
| Ver alertas, regras ou silenciamentos de host sem leitura | Filtro por `alert:read` em cada consulta; detalhe de alerta sem leitura responde 404 (indistinguível); regras e silenciamentos com host restrito não aparecem (FR-023) | usuário somente leitura não recebe ações; alerta de outro host → 404 |
| Silenciar ou desativar regras para esconder incidentes | `alert:manage` em **todos** os hosts abrangidos; janela máxima de 7 dias; justificativa de 10 a 500 caracteres; alertas silenciados ficam registrados (estado `silenced`); toda criação, alteração, encerramento e resolução manual é auditada por host | silenciamento > 7 dias → 400; sem `alert:manage` → 403 |
| Parada "intencional" usada para encobrir falha | Justificativa obrigatória onde a política do estágio manda, recusada pela API (`422 justification_required`) mesmo para clientes que não conhecem a política (FR-017); autor (nome do token) e horário gravados; a marcação termina quando o container volta a executar | comando sem justificativa em estágio com política → 422 (stack real) |
| Mudar a política de justificativa | `environment:manage` em todos os hosts do estágio (D-007), auditado em cada host | sem o scope → 403 |
| Segredo digitado em justificativa ou comentário | Mascaramento (`JustificationMasker`) antes de persistir e auditar (FR-020) | canário de senha no formato chave=valor não aparece no registro (teste de aplicação) |
| Alerta forjado por evento repetido ou reenvio | Avaliação só dos eventos efetivamente gravados (deduplicação por `source_key`); um alerta ativo por regra e alvo (índice único parcial); 100 repetições = 1 alerta (SC-002) | teste em PostgreSQL real |
| Inundação de alertas | Deduplicação por regra e alvo, regras com mínimo de ocorrências e janela, silenciamentos por padrão de nome | — |

**Risco residual**: o estado das condições de duração (limites de CPU e memória, agente desconectado) vive em memória da réplica única, como as amostras da spec 009; reiniciar a API recomeça a contagem. A autoria exibida usa `preferred_username`; a auditoria continua com o `sub` do token.

## Threat model: consulta da auditoria (spec 017)

**Fronteira nova**: navegador → BFF → `GET /api/v1/audit/{summary,events,events/{id},export}` → leitura de `dokpod.audit_events`. Não há escrita na trilha além de `audit.exported`.

| Ameaça | Mitigação | Teste negativo |
| --- | --- | --- |
| Ler eventos de host sem permissão | `audit:read` decidido no Keycloak por host em cada consulta, detalhe, relacionados e exportação; evento de host sem acesso responde 404 (indistinguível) | `AuditConsoleIntegrationTests` (acesso parcial) e E2E sem `audit:read` |
| Varredura cara da trilha (DoS) | período obrigatório de até 90 dias, cursor por chave, página de até 100, exportação limitada a 10.000 eventos | período maior → `period_too_long`; exportação maior → `export_too_large` |
| Exfiltração silenciosa | toda exportação grava `audit.exported` (período e quantidade) em cada host abrangido | evento conferido na stack real |
| Fórmula maliciosa no CSV | células iniciadas por `= + - @`, tabulação ou retorno recebem apóstrofo | E2E de exportação |
| Revelar dado mascarado | a API devolve os detalhes como gravados (já mascarados na origem); não há rota de desmascaramento | — |
| Alterar ou apagar evidência | sem rotas de escrita; gatilho existente rejeita `UPDATE`/`DELETE`; o Down da migration mantém a coluna `actor_name` | testes rodam em transação desfeita porque o próprio teste não consegue apagar |

**Nome do ator**: eventos novos guardam o `preferred_username` do token (o `sub` continua como identificador). Para eventos gravados fora da requisição do usuário (por exemplo, fim do terminal), a API usa o último nome visto para o `sub`, mantido só em memória e limitado. **Risco residual**: a consulta em si não é auditada (decisão da spec), só a exportação.

## Desenvolvimento e release

- threat model atualizado para cada nova fronteira;
- testes negativos de autenticação, autorização, replay, SSRF e elevação;
- análise estática, dependências, secrets, SBOM e imagens em CI;
- imagens executadas como usuário não root quando compatível com o socket;
- instalação verifica assinatura, provenance e identidade do workflow; digest ausente, emissor incorreto ou artefato revogado é rejeitado;
- nenhuma credencial em Git, camada de imagem, variável pública ou exemplo;
- achado crítico ou alto bloqueia release até correção ou aceitação formal.

## Divulgação de vulnerabilidades

Consulte a [política de segurança](../.github/SECURITY.md). Não abra issue pública com exploit, credencial ou detalhe que facilite abuso antes da correção coordenada.

Consulte também a [configuração do Keycloak](configuracao-keycloak.md).