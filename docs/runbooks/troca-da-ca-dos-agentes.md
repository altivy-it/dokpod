# Runbook: troca da CA dos agentes

Procedimento para trocar a autoridade que emite os certificados mTLS entre agentes e API (threat model consolidado da spec 005, lacuna L1). Use na expiração planejada da CA ou quando a chave dela pode ter sido exposta.

## Como a confiança funciona

- **API → agente**: a API aceita o certificado do agente se a cadeia fecha numa das raízes de `DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH`, se ele tem EKU `clientAuth` e se o fingerprint SHA-256 está cadastrado em `dokpod.agent_identities` sem `revoked_at_utc`. Não há CRL nem OCSP: **revogar um agente é marcar a identidade como revogada** (`POST /api/v1/environments/{id}/agent-identity/revoke`), o que encerra a sessão ativa.
- **Agente → API**: o agente aceita o servidor se a cadeia fecha numa das raízes de `DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH` **e** se o nome do endpoint (`DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT`) está no certificado da API.
- Os dois arquivos aceitam **PEM com mais de uma raiz** (bundle). Um arquivo DER continua valendo com uma raiz só.

Como a revogação é por fingerprint, a CA só importa para emitir identidades novas. Uma CA comprometida permite emitir certificados com cadeia válida, mas eles ainda precisam de um fingerprint cadastrado. O risco real é o cadastro: quem tem a chave da CA **e** acesso de escrita a `dokpod.agent_identities`, ou a um enrollment, passa a se apresentar como agente.

## Troca planejada (com sobreposição, sem queda)

1. Gere a CA nova fora do repositório e guarde a chave offline. Não reutilize a chave da antiga.
2. Monte o bundle `antiga + nova` (concatenação dos dois PEM) e aponte para ele:
   - na API, `DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH`;
   - em cada agente, `DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH`.
   Recrie a API e reinicie os agentes. Confira em `dokpod_agents_connected` ([observabilidade](observabilidade.md)) que todos voltaram.
3. Emita o certificado novo da API pela CA nova, com o nome que os agentes usam no endpoint, e troque o PFX de `DOKPOD_API_CERTIFICATE_PATH`. Os agentes aceitam, porque o bundle tem as duas raízes.
4. Para cada host: emita o certificado novo do agente pela CA nova, atualize o fingerprint da identidade e troque o PFX do agente. A sessão reabre com o certificado novo.
5. Quando nenhum agente usar mais certificado da CA antiga (todos os fingerprints cadastrados são da nova), troque o bundle pela CA nova sozinha nos dois lados e recrie a API e os agentes.
6. Destrua a chave da CA antiga e registre a troca na operação.

## Comprometimento da CA

1. **Primeiro, contenha.** Revogue toda identidade de agente que não reconheça (fingerprint diferente do instalado no host) e confira a auditoria (`agent.*`, comandos recentes) desde a data provável da exposição.
2. Gere a CA nova e siga os passos 2 a 4 da troca planejada, **pulando a sobreposição longa**: o bundle `antiga + nova` vale só enquanto os agentes são reemitidos.
3. Ao fim, deixe só a CA nova nos dois lados (passo 5). Um agente que não foi reemitido para de conectar, e isso é o esperado.
4. Troque também o certificado da API se ele foi emitido pela CA comprometida (passo 3), porque um servidor falso poderia se apresentar aos agentes durante a sobreposição.

## Verificação

- Agente com certificado da CA removida: a sessão é recusada no handshake TLS.
- Agente apontado para um nome que não está no certificado da API: recusado com `The remote certificate was rejected by the provided RemoteCertificateValidationCallback` (comprovado no laboratório em 2026-10-24).
- Testes: `AgentControlClientTrustTests` (nome divergente, bundle, raiz desconhecida, DER) e `ClientCertificateTrustTests` (bundle, raiz removida, EKU).
