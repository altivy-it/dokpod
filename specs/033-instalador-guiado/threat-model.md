# Threat model: instalador guiado do Dokpod

**Spec**: 033, tarefa T001 (gate antes de código) · **Data**: 2026-11-08 · **Origem**: IA assistida, a partir de
[spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md) e
[contratos](contracts/)
**Estado**: **revisado e aceito** pelo solicitante em 2026-11-08 ([decisão](#decisão-do-revisor)).

Este documento cobre só as fronteiras novas da 033. As que já existem continuam regidas por
[segurança](../../docs/seguranca.md), pelo [threat model da 031](../031-licenciamento-business-offline/threat-model.md) e
pelos das specs que definiram cada superfície:
- sessão do navegador e BFF;
- PEP com Keycloak;
- canal mTLS agente-servidor;
- release assinada e deploy hook (spec 024).

## Como revisar

1. Leia o [modelo do sistema](#modelo-do-sistema) e as [premissas](#premissas-de-confiança). A principal: o instalador é
   executado pelo operador do host, com o mesmo poder que esse operador já tem sobre o engine.
2. Percorra as [fronteiras](#fronteiras-novas). Cada mitigação aponta o requisito (FR), a decisão (D) e a tarefa (T).
3. Decida cada [lacuna](#lacunas-encontradas): corrigir na tarefa indicada ou aceitar.
4. Aceite ou recuse cada [risco residual](#registro-de-riscos-residuais).
5. Preencha a [decisão](#decisão-do-revisor). A T001 só é marcada com a decisão registrada.

## Modelo do sistema

```text
OPERADOR (shell no host)
  install.sh ──verifica digest + cosign──▶ imagem dokpod-installer (efêmera, --rm)
                                              │ monta: socket do engine, installDir, secretsDir
                                              │ lê: answers.yaml, {file}/{env} de segredos, credenciais administrativas (transitórias)
                                              ├─▶ engine (Compose v2) ──▶ cria/atualiza: bancos, Keycloak, borda, web, BFF, API, agente
                                              ├─▶ container avulso psql (PGPASSFILE em tmpfs) ──▶ PostgreSQL: banco, roles, concessões
                                              ├─▶ container avulso da API em modo admin (conexão dokpod_owner por arquivo)
                                              │        ├─▶ migrations (catálogo SQL, sobreposição opcional em /run/sql-override)
                                              │        └─▶ ambiente, identidade do agente, licença (auditados)
                                              ├─▶ Keycloak Admin API (credencial do master, transitória) ──▶ só o realm dokpod
                                              ├─▶ openssl ──▶ CA local, certificados de borda, API e agentes (secretsDir/pki)
                                              └─▶ installDir/state/installation.db (SQLite, sem segredos), reports/*.json

RUNTIME (depois da instalação)
  navegador ──HTTPS──▶ borda (única porta HTTP pública) ──▶ web, BFF ──▶ API (dokpod_runtime, /run/secrets)
  agente remoto ──gRPC mTLS──▶ API (porta publicada só com agentGrpcPort)
  agente local ──rede interna──▶ API; socket do engine montado só no agente
```

### Premissas de confiança

- Quem executa o instalador já controla o engine do host. O engine é sempre rootless (D-015), Docker ou Podman, então
  esse poder é o do usuário dono do socket, não o de root. O instalador não concede poder além desse e recusa engine
  rootful.
- `installDir` e `secretsDir` pertencem ao operador (`0750` e `0700`). Quem pode gravar neles controla a instalação,
  como quem pode gravar no compose de qualquer stack.
- As imagens só valem pelo digest do manifesto da release, verificado com cosign antes do uso (D-010).
- As credenciais administrativas do PostgreSQL e do Keycloak existentes são do cliente. O instalador as usa só durante a
  execução, e o runtime do Dokpod nunca as recebe.
- O SQL sobreposto é decisão consciente do operador (FR-041). O Dokpod garante que ele seja visto, confirmado e
  registrado, não que esteja certo.

## Fronteiras novas

### B1. Bootstrap e cadeia de confiança das imagens

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| `install.sh` adulterado no download | Asset da release com SHA-256 e atestação; o quickstart manda conferir com `gh attestation verify` antes de executar | D-001, T051 |
| Imagem do instalador trocada no registro | Digest gravado no próprio `install.sh` na publicação e verificado com cosign (container por digest) antes de executar | D-001, T024, T051 |
| Imagem de serviço trocada ou tag reapontada | Só `referência@digest` do `release-manifest.json`, com `cosign verify` da identidade do `publish.yml`; falha dá código 5 sem alteração | FR-005, D-010, T025 |
| Pacote offline adulterado | Mesma verificação sobre `docker load` e assinaturas exportadas | FR-006, T025, T050 |
| Dependência da imagem do instalador comprometida | Versões fixadas na matriz, SBOM, scan e assinatura da imagem como as demais | T004, T051, T052 |

### B2. Container do instalador com o socket do engine

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Engine rootful dando ao instalador poder de root no host | Recusado nas verificações iniciais pela API do engine (`name=rootless`, `host.security.rootless`), não pelo caminho do socket; socket fora de `$XDG_RUNTIME_DIR` recusado pelo `install.sh` | D-015, T021, T024 |
| Auditoria com o IP do encaminhador de portas rootless em vez do cliente | Aviso nas verificações iniciais com a configuração que preserva o IP (`pasta`, `slirp4netns`) ou nginx existente do host | D-015, T021 |
| Instalador usado como porta de entrada permanente para o engine | Container efêmero (`--rm`), sem porta exposta, só iniciado pelo operador; nada fica rodando com o socket depois da execução | D-001, T024 |
| Mounts além do necessário | Só o socket, `installDir` e `secretsDir`; containers avulsos (`psql`, modo administrativo) não recebem o socket | T024, [L5](#lacunas-encontradas) |
| Saída do instalador gravada pelo driver de log do engine (journald, json-file), incluindo a senha temporária e prompts | Ver [L7](#lacunas-encontradas) | T024 |
| Execuções concorrentes corrompendo o estado | `run_lock` em `BEGIN IMMEDIATE`, código 7; *lock* velho só liberado com confirmação | FR-044, D-014, T019 |

### B3. Credenciais administrativas transitórias

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Senha administrativa do PostgreSQL no `docker inspect` ou no `ps` | Só por `PGPASSFILE` em tmpfs montado no container avulso do `psql` e apagado ao fim; nunca `PGPASSWORD` nem `-v` | FR-008, FR-033, T028, T035 |
| Credencial administrativa persistida | Lida de `{file}`, `{env}` ou prompt e descartada no fim; nunca em `secretsDir`, estado ou relatório; canário confere | FR-008, FR-015, T035, T053 |
| Credencial do `master` do Keycloak alterando outro realm | Scripts reconciliam só o realm `dokpod`; plano mostrado antes por `-DryRun`; teste compara exports de `master` e `outro` antes e depois | FR-013, SC-006, T039–T041, [R3](#registro-de-riscos-residuais) |
| Token administrativo do Keycloak vazando | `SecureString` no processo do instalador; token só em memória durante a execução; saída redigida | FR-015, FR-033, T023 |
| Variáveis `{env}` expostas ao repassar ao container | Repasse por nome (`-e NOME`), sem valor na linha de comando | FR-002, T024 |

### B4. Modo administrativo da imagem da API

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Modo administrativo acessível pela rede | Despacho antes de montar o host: sem Kestrel, gRPC, SignalR nem hosted services; termina ao fim do comando | D-003, T010 |
| Runtime com privilégio de dono do schema | A API usa `dokpod_runtime` (DML e `EXECUTE`); só o container avulso do modo administrativo recebe `dokpod-owner-connection` | D-009, T016 |
| Identidade de agente registrada fora do console | Registro auditado (`environment.agent_identity_register`, ator `installer`, operador e justificativa); fingerprint em outro ambiente ou revogado é recusado; quem tem a conexão de *owner* já tem o banco | FR-024, D-004, T013–T015, [R7](#registro-de-riscos-residuais) |
| SQL livre pelo modo administrativo | Nenhum comando aceita SQL ou caminho fora de `/run/secrets`, `/run/sql-override` e da saída montada; todo SQL vem do catálogo | FR-039, T009, T010 |
| Migration aplicada sem o usuário ver | `apply` exige o `planHash` do plano mostrado; banco com migration desconhecida dá código 6 | FR-036, FR-010, T012 |

### B5. Catálogo SQL e sobreposição externa

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| SQL do operador executado como `dokpod_owner` sem ser visto | Cada arquivo alterado aparece no plano com o hash original e o novo; confirmação que nomeia os caminhos; `--yes` não confirma | FR-041, T030 |
| Sobreposição para outra versão ou arquivo inventado | Exige `manifest.json` da mesma versão; caminho sem correspondente é recusado | FR-041, T008 |
| `installDir/sql-override/` alterado entre execuções | Ver [L4](#lacunas-encontradas) | T030 |
| Runtime executando SQL sobreposto | A raiz externa só é habilitada no modo administrativo; o runtime não monta `/run/sql-override` | D-013, T008 |
| Alteração aplicada à mão sem registro | Impressão do schema real comparada com a esperada; diferença não registrada dá código 6, objeto a objeto | FR-042, T006, T012, T059 |
| Segredo em parâmetro de arquivo SQL | Segredos lidos pelo próprio `.sql` com `\set` a partir de `/run/secrets`; nunca `-v` | D-013, T007 |

### B6. Segredos em repouso e em uso

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Segredo em `.env`, `docker inspect` ou variável de ambiente dos serviços | Compose `secrets:` em `/run/secrets` e provedor *key-per-file* na API e no BFF; `dokpod.env` sem segredos | FR-033, D-006, T017, T026 |
| Destino de segredo sobrescrito | Falha fechada se existir sem `recreate` do item | FR-028, T023 |
| Segredo na saída, no relatório ou no estado | Redação de toda saída pelos valores carregados; estado e relatório sem segredos; canário em saída, `reports/`, `installDir`, `inspect`, `ps` e logs | FR-033, FR-034, FR-044, T023, T019, T053 |
| Arquivo de respostas com segredo em texto | Esquema recusa; só `{file}` ou `{env}` | FR-002, T018 |
| Senha temporária do administrador persistida | Mostrada uma vez em TTY; sem TTY, `initial-admin-password` (`0600`) removido quando o Keycloak mostra a senha trocada | FR-014, T032 |

### B7. PKI local

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Chave da CA exposta a um serviço | Só em `secretsDir/pki/`, nunca montada em serviço de runtime; só o container do instalador a lê | FR-018, T022 |
| Certificado errado aceito (SAN, EKU, validade, cadeia) | Validação de todo certificado, gerado ou da organização, antes do uso | FR-017, T022 |
| Certificado vencido derrubando agentes ou a borda | Aviso a partir de `renewWarningDays`; `recreate pki:<item>` | FR-019, T049 |
| Troca da CA deixando agentes sem confiança | Transição `active → rotating → active` com as duas raízes até todos reconectarem | FR-030, T044 |

### B8. Pacote de host

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Pacote com outros segredos da instalação | Só PFX, senha do PFX, CA, `agent.env`, definição do container (ou parâmetros do Windows) e README; teste confere o conteúdo | FR-025, T046 |
| Pacote interceptado em trânsito dá identidade de agente a terceiros | Ver [L2](#lacunas-encontradas); revogação por `host revoke` | FR-025, T046, T047 |
| Identidade registrada para pacote nunca usado | Ver [L6](#lacunas-encontradas) | T046 |

### B9. Keycloak provisionado e primeiro administrador

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Administrador de bootstrap do `master` esquecido com senha conhecida | Removido depois que o administrador nominal existe; ver [L3](#lacunas-encontradas) para falha na remoção | T029 |
| Primeiro administrador com senha fraca ou permanente | Senha gerada, temporária, com troca obrigatória no primeiro login | FR-014, T029 |

### B10. Borda e rede

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| BFF ou API alcançáveis sem a borda | Só redes internas; a borda é a única porta HTTP pública; com nginx existente, web e BFF só em loopback ou rede interna | FR-021, FR-022, FR-043, T026, T037 |
| Porta gRPC dos agentes usada como superfície genérica | Publicada só com `agentGrpcPort`; exige certificado de cliente de ambiente cadastrado; não atende outra rota | FR-022, T046 |
| Cabeçalhos `X-Forwarded-*` forjados por quem alcança o BFF por outro caminho | Ver [L1](#lacunas-encontradas) | T026, T037 |
| Socket do engine exposto | Montado só no agente; nunca publicado por rede | FR-022, T026 |
| Keycloak existente com CA privada aceito sem validação | API e BFF confiam só no bundle montado (sistema, CA local e `keycloak.caCertificate`) | D-008, T026 |

### B11. Estado SQLite e relatórios

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Estado adulterado para pular confirmação (por exemplo, linha falsa em `sql_overrides`) | Quem grava em `installDir` já controla a instalação ([R2](#registro-de-riscos-residuais)); o plano é sempre recalculado do observado, então o estado não substitui a conferência do engine, do banco e do Keycloak | D-007, D-014, T019, T020 |
| Estado corrompido ou apagado levando a recriação destrutiva | `integrity_check` no início; reconstrução a partir do observado só com confirmação; backup `VACUUM INTO` antes de cada execução | FR-044, T019 |
| Segredo gravado no estado | Nenhuma coluna de segredo; teste com canário | FR-044, T019 |

### B12. Recriação e remoção

| Ameaça | Mitigação | Rastro |
| --- | --- | --- |
| Perda de banco, key rings ou identidades por engano | Segunda confirmação nomeada; no modo não interativo, só `--confirm-data-removal` com a lista exata | FR-029, SC-008, T043 |
| Componente do usuário removido ou recriado | Componentes `existing` nunca são removidos nem recriados | FR-029, T042 |
| Volumes de workloads afetados | O instalador só toca recursos com o rótulo da instalação; volumes de workloads nunca entram no plano | Princípio II, T020 |

## Lacunas encontradas

| # | Lacuna | Recomendação | Tarefa |
| --- | --- | --- | --- |
| L1 | O BFF aceita `X-Forwarded-*` das redes em `Security__TrustedNetworks`. Se o instalador gravar uma faixa ampla, quem alcançar o BFF por outro caminho forja origem e esquema | Gravar só a sub-rede interna da borda (borda provisionada) ou `127.0.0.1/32` e `::1/128` (nginx existente em loopback); recusar faixa maior que a rede interna. Testar que o BFF ignora cabeçalhos de fora dela | T026, T037 |
| L2 | O pacote de host leva o PFX e a senha dele juntos, então quem intercepta o arquivo tem a identidade do agente | Cifrar o pacote com uma frase gerada, exibida uma vez em TTY (ou gravada em `secretsDir` sem TTY), e não guardar a frase no pacote. O `README.txt` explica como abrir. Revogar com `host revoke` se o pacote se perder | T046 |
| L3 | Se a remoção do administrador de bootstrap do Keycloak falhar, sobra uma conta do `master` com senha em `secretsDir` | A execução não termina como `converged` enquanto a conta existir: `outcome: failed`, com a ação no relatório; a reexecução tenta de novo | T029 |
| L4 | A cópia em `installDir/sql-override/` é reusada em reexecuções. Alterada depois da confirmação, um arquivo novo poderia entrar sem ser visto | Na reexecução, recalcular os hashes da cópia e compará-los com `sql_overrides.applied_sha256`; qualquer diferença volta ao plano e exige nova confirmação nomeada | T030 |
| L5 | Os containers avulsos (`psql` e modo administrativo) podem herdar redes ou mounts a mais | Executá-los com `--read-only`, sem socket do engine, sem porta publicada, só na rede interna do banco e só com os mounts do comando | T010, T028 |
| L6 | `host add` registra a identidade na hora; um pacote nunca usado mantém uma identidade válida | `status` lista identidades de pacote sem conexão há mais de 7 dias e sugere `host revoke`. Recomendo aceitar o risco com esse aviso, sem revogação automática | T046, T027 |
| L7 | O engine pode gravar a saída do container do instalador no driver de log (journald ou json-file) mesmo com `--rm`, inclusive a senha temporária exibida em TTY | Executar o instalador e os containers avulsos com `--log-driver none` e conferir no canário (T053) que o journal do host não tem os valores | T024, T053 |

## Registro de riscos residuais

| # | Risco | Fronteira | Mitigação atual | Aceite |
| --- | --- | --- | --- | --- |
| R1 | O socket rootless no container do instalador dá o poder do usuário dono do engine: todos os containers e volumes desse usuário | B2 | Só engine rootless (D-015); container efêmero, imagem verificada, só iniciado pelo operador que já tem esse poder | Aceito (2026-11-08) |
| R2 | Quem grava em `installDir` ou `secretsDir` controla a instalação (compose, SQL sobreposto, estado) | B5, B11 | Permissões `0750`/`0700` do operador; plano recalculado do observado | Aceito (2026-11-08) |
| R3 | A credencial do `master` do Keycloak dá, durante a execução, poder sobre todos os realms; um defeito poderia alterar outro | B3 | Só o realm `dokpod` nos scripts; `-DryRun` no plano; teste SC-006 | Aceito (2026-11-08) |
| R4 | A chave da CA local fica protegida só por permissão de arquivo, sem HSM | B7 | `0600` em `secretsDir`, nunca montada em serviço; CA da organização como alternativa | Aceito (2026-11-08) |
| R5 | A sobreposição confirmada executa SQL arbitrário do operador como `dokpod_owner` | B5 | Por decisão do solicitante (FR-041): visível, confirmada, registrada e conferida pela impressão do schema | Aceito (2026-11-08) |
| R6 | O transporte do pacote de host até o outro host fica fora do Dokpod | B8 | Pacote cifrado (L2), revogação | Aceito (2026-11-08) |
| R7 | Quem tem a conexão de `dokpod_owner` pode vincular qualquer fingerprint a um ambiente sem enrollment | B4 | Auditado; mesmo poder de quem tem o banco; enrollment fica para spec própria | Aceito (2026-11-08) |

## Controles transversais

- **Autorização**: o Keycloak continua sendo a autoridade dos usuários. O instalador só configura o realm do Dokpod e
  concede os scopes do primeiro ambiente ao grupo `/dokpod/administrators`.
- **Menor privilégio**: o runtime usa `dokpod_runtime`. As credenciais administrativas são transitórias, e o modo
  administrativo roda num container avulso sem rede pública.
- **Integridade**: imagens por digest assinado, catálogo SQL com hash e impressão do schema, estado transacional.
- **Segredos**: só por arquivo, nunca na saída, no relatório, no estado, no `inspect`, no `ps` ou nos logs; canário no
  SC-005.
- **Auditoria**: cadastro de ambiente, identidade de agente e licença na auditoria do Dokpod; o restante no relatório de
  execução.
- **Recuperação**: backup do banco antes de migration, backup do estado antes de cada execução, remoção de dados só com
  segunda confirmação nomeada.

## Decisão do revisor

| Campo | Valor |
| --- | --- |
| Revisor | Solicitante (Lincoln, `@lzocateli`; Origem: humano) |
| Data | 2026-11-08 |
| Premissa central (instalador com o poder do operador sobre o engine) | Aceita pelo solicitante em 2026-11-08, com a restrição: "a aplicação funciona rootless, o pré-requisito é docker ou podman ambos rootless" (D-015) |
| Lacunas L1–L7 | Recomendações aceitas e aplicadas nas tarefas: L1 em T026 e T037; L2 em T046; L3 em T029; L4 em T030; L5 em T010 e T028; L6 aceita com o aviso de `status` (T027, T046); L7 em T024 e T053 |
| Riscos R1–R7 | Aceitos |
| Declaração | "Premissa central para você aceitar: sim, contudo a aplicação funciona rootless, o pre requisito é docker ou podman ambos rootless" e "Aceito as recomendações L1–L7 e os riscos R1–R7" |
