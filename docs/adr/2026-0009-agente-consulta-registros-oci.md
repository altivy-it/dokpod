# ADR 2026-0009: Agente consulta registros OCI diretamente

**Status:** proposed

**Data:** 2026-11-01  
**Responsáveis:** zocatel (responsável pelo produto)  
**Origem:** IA assistida, a pedido do solicitante (`/dokpod-create-adr` em 2026-11-01, depois da decisão "executar suas recomendações" registrada no [threat model da spec 025](../../specs/025-aviso-nova-versao-imagem/threat-model.md#decisão-do-revisor))  
**Revisor humano:** pendente (zocatel)  
**Relacionado:** [spec 025](../../specs/025-aviso-nova-versao-imagem/spec.md), [plan 025](../../specs/025-aviso-nova-versao-imagem/plan.md) (decisões D-001, D-003, D-005 e D-012 de [research.md](../../specs/025-aviso-nova-versao-imagem/research.md)), [threat model 025](../../specs/025-aviso-nova-versao-imagem/threat-model.md), specs [012](../../specs/012-imagens-pull/spec.md) e [020](../../specs/020-registros/spec.md), [ADR 2026-0008](2026-0008-licenca-assinada-e-verificacao-offline.md)

## Contexto

Até a spec 024, o agente falava só com o engine local (socket Docker ou Podman) e com a API (gRPC com mTLS). Quem falava com registros de imagens era o engine, no pull da spec 012. O agente apenas repassava, no cabeçalho `X-Registry-Auth`, a credencial lida do `config.json` do host (`RegistryCredentialFile`).

A spec 025 precisa saber se a tag usada por um container aponta, no registro, para um conteúdo diferente do que está no host, sem baixar a imagem e sem levar a credencial ao plano de controle (FR-002, FR-007). Dois fatos de 2026-11-01 limitam as opções:
- o Podman 5.7.0 responde `404` a `GET /distribution/{nome}/json` na API compatível, o único endpoint de engine que consulta o registro sem pull;
- o `HEAD` de manifesto na API OCI Distribution não consome a cota de pulls do Docker Hub (`ratelimit-remaining` inalterado antes e depois).

A escolha é duradoura porque muda o papel do agente descrito em [arquitetura](../arquitetura.md) ("comunicação com a engine local e com a API"), cria a primeira saída de rede do agente além do engine e da API, e serve de precedente para recursos futuros que precisem de rede a partir do host.

## Forças de decisão

- um único comportamento para Docker e Podman, com capability anunciada e testada em cada engine (constituição, princípio IV);
- credencial de registro só no host, nunca no plano de controle, em log ou em telemetria (constituição, princípio III);
- TLS sempre verificado, sem HTTP para registros inseguros;
- nenhuma dependência nova (BCL `HttpClient`);
- nada que altere containers ou imagens (FR-004);
- reversibilidade: desligar a capability sem afetar o pull nem o inventário.

## Opções consideradas

### Opção A: agente consulta o registro pela API OCI Distribution

`HEAD /v2/<repositório>/manifests/<tag>` (e `GET` do índice só quando ele mudou), com o desafio `Bearer`/`Basic` padrão e a credencial do `config.json` do host.

- **Vantagens**: o mesmo código para os dois engines; não consome a cota de pull; a credencial não sai do host; o protocolo é padrão (OCI Distribution Spec).
- **Desvantagens**: o agente passa a ter saída HTTPS própria. Em host com proxy configurado só no daemon do engine, a verificação falha até o agente ter `HTTPS_PROXY`. O agente implementa o desafio de token, com cuidados próprios: o `realm` pode apontar para outro host (lacuna L1 do threat model).

### Opção B: engine consulta o registro

Pelo `GET /distribution/{nome}/json` no Docker e por outro caminho no Podman.

- **Vantagens**: o agente continua sem rede própria; o proxy do daemon vale.
- **Desvantagens**: não existe no Podman compatível (evidência acima). Exigiria dois caminhos e um sem endpoint equivalente na API, e a capability só no Docker, o que deixaria o Podman sem o recurso.

### Opção C: plano de controle consulta o registro

- **Vantagens**: agente inalterado.
- **Desvantagens**: a credencial do host iria ao servidor, contra FR-002 e FR-007. O servidor também precisaria alcançar registros privados nas redes dos hosts e passaria a ter saída de rede, contra a premissa de sem egress da API de licenciamento (spec 031, D-012).

## Decisão

Recomendada a **Opção A**, com estes limites:

1. **Escopo**: só leitura de manifesto (`HEAD` e `GET`), só para referências que vêm do inventário dos containers do próprio host, filtradas pela política de registros do estágio antes do pedido. Nenhum usuário informa host, registro ou URL.
2. **TLS**: verificado com o trust store do sistema, sem opção de desligar. Registro marcado como inseguro na configuração do engine fica "Não verificável", sem HTTP.
3. **Credencial**: lida por registro a cada pedido. Vai ao `realm` do desafio só quando ele é `https` e do mesmo host do registro, ou no par `docker.io` → `auth.docker.io`; nos demais casos, token anônimo. O token fica só em memória, durante o pedido. `Authorization` nunca segue redirecionamento para outro host.
4. **Limites**: 10 s por requisição, 5 min por pedido, 4 MiB de manifesto, 64 KiB de resposta de token e 4 consultas simultâneas.
5. **Proxy**: o agente respeita `HTTPS_PROXY` e `NO_PROXY` do próprio container e não contorna o proxy.
6. **Logs e telemetria**: só o tipo da exceção e o motivo mapeado; nunca URL, referência, cabeçalho, token ou corpo.
7. **Capability**: `CAPABILITY_IMAGE_UPDATE_CHECK`, anunciada por engine só depois do teste contra um registro real. O agente Windows não anuncia até passar no mesmo teste.

**Revisar a decisão** se:
- um engine passar a oferecer consulta de manifesto remoto nos dois casos (Docker e Podman);
- surgir outro recurso pedindo saída de rede do agente: ele exige ADR próprio, porque este ADR não autoriza saída genérica;
- clientes relatarem com frequência hosts que alcançam registros só pelo proxy do daemon.

## Consequências

### Positivas

- aviso de nova versão igual nos dois engines, sem pull e sem cota;
- a credencial continua só no host;
- a falha do registro é visível ("Falha na verificação" com motivo) e nunca vira falso "Atualizada".

### Negativas e trade-offs

- o agente ganha uma superfície de rede própria e código de autenticação de registro para manter;
- em host com proxy só no daemon, é preciso configurar `HTTPS_PROXY` no agente (risco R4 do threat model);
- [arquitetura](../arquitetura.md) e [segurança](../seguranca.md) precisam registrar a saída nova do agente (T027 da spec 025).

## Segurança e privacidade

O [threat model da spec 025](../../specs/025-aviso-nova-versao-imagem/threat-model.md) cobre as fronteiras B1–B5. As lacunas L1–L4 entram nas tarefas T009 e T011, e os riscos R1–R5 foram aceitos pelo solicitante em 2026-11-01.

A premissa central: o host já confia nos registros que suas imagens usam, então a verificação não alcança nada que o engine não alcançaria. Não há porta nova de entrada.

## Dados, protocolo e compatibilidade

- Protocolo agente-servidor: capability 23 e mensagens 17 e 27, só campos novos (N/N-1, [contrato](../../specs/025-aviso-nova-versao-imagem/contracts/agent-protocol.md)). Agente antigo não anuncia e não recebe; servidor antigo ignora o campo novo.
- Nenhum dado de registro novo no plano de controle além do estado, do motivo e do digest remoto ([modelo de dados](../../specs/025-aviso-nova-versao-imagem/data-model.md)). A API não devolve o digest remoto nem o registro resolvido.

## Observabilidade e operação

- Métricas por estado, motivo e host, sem referência nem registro (D-012).
- O runbook da 025 documenta proxy, credencial, registros inseguros e a cota do Docker Hub.

## Validação

A decisão se confirma, ou se falsifica, com:
- teste contra `registry:2` real com TLS e autenticação: digest igual dá `up_to_date`, tag republicada dá `update_available`, índice com só outra arquitetura alterada continua `up_to_date`;
- agentes Docker e Podman reais na stack E2E, com a capability anunciada e o mesmo resultado nos dois;
- `realm` em outro host não recebe credencial; `realm` `http` é recusado; redirecionamento para outro host não leva `Authorization`;
- canário no `config.json` ausente de respostas, banco, logs e telemetria;
- cota: `ratelimit-remaining` do Docker Hub inalterado depois de uma verificação sem mudança.

Qualquer falha nesses pontos invalida a Opção A ou os seus limites.

## Rollout e rollback

- **Rollout**: entra com a spec 025, só na composição Business com a capability `image-update-notice`. A CE nunca envia o pedido ao agente.
- **Rollback**: o servidor deixa de enviar a mensagem 17 (módulo não registrado ou capability fora da licença), ou o agente deixa de anunciar a capability. Pull, inventário e protocolo existentes não mudam. As tabelas da 025 têm migration `Down`.

## Referências

- [OCI Distribution Specification](https://github.com/opencontainers/distribution-spec/blob/main/spec.md) (`HEAD` e `GET` de manifesto)
- Docker Registry token authentication (desafio `WWW-Authenticate: Bearer`)
- [research.md da spec 025](../../specs/025-aviso-nova-versao-imagem/research.md), D-001 a D-005 e D-012
