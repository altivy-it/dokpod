# Pesquisa: Licenciamento Business offline

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Data**: 2026-10-29

Origem: plano privado de licenciamento (2026-10-28), aprovado como entrada pelo solicitante em 2026-10-29, e leitura do repositório no commit `204849a`. Cada decisão abaixo resolve uma dúvida do contexto técnico; nenhuma autoriza implementação (FR-030, FR-031).

## D-001: Formato da licença

- **Decisão**: JWS Compact (RFC 7515) com perfil restrito: assinatura `ES256` (ECDSA P-256 com SHA-256, RFC 7518 §3.4), assinatura no formato IEEE P1363 de 64 bytes, header protegido só com `alg`, `kid` e `typ: dokpod-license+jws`. O verificador aceita apenas `ES256` e só procura `kid` no trust store embarcado; recusa `none`, HMAC, `jwk`, `jku`, `x5u`, `x5c`, `crit` e qualquer header extra.
- **Justificativa**: formato padronizado e auditável; ECDSA P-256 está no `System.Security.Cryptography.ECDsa` do .NET 10 sem dependência nova; assinatura curta; o perfil fechado elimina as confusões clássicas de JWS (algoritmo escolhido pelo arquivo, chave embutida).
- **Alternativas**: Ed25519 (sem API estável na BCL do .NET 10 para todas as plataformas-alvo, exigiria dependência); RSA-PSS (assinatura e chave maiores, sem ganho); XML assinado ou formato binário próprio (mais superfície de parser, sem ferramentas); bibliotecas de licenciamento prontas (licenças source-available ou servidor online, conflitam com a política de dependências da constituição).

## D-002: Verificador como biblioteca independente

- **Decisão**: `backend/libs/Dokpod.Licensing/`, biblioteca .NET 10 que depende só da BCL, com licença MIT proposta. Recebe bytes limitados, trust store e instante; devolve resultado imutável com identidade, direitos, vigência e falhas tipadas. Não lê arquivo, não grava banco, não usa rede, não decide autorização de usuário.
- **Justificativa**: a mesma biblioteca serve ao produto e à CLI do fornecedor (conformidade do formato num só lugar); MIT permite consumo pela CLI separada sem arrastar AGPL; sem I/O, os testes de vetores são determinísticos.
- **Alternativas**: verificador dentro de `Dokpod.ControlPlane.Infrastructure` (CLI teria de copiar código AGPL); duas implementações, uma por lado (formatos divergem).
- **Gate**: licença MIT e titularidade dependem da revisão jurídica (FR-030).

## D-003: Limites defensivos do parser

- **Decisão**: arquivo até 64 KiB; até 64 capabilities; até 2 slots; strings de identificador até 128 caracteres; profundidade JSON até 8; UTF-8 estrito; Base64URL sem padding; JSON sem propriedades duplicadas nem desconhecidas; inteiros dentro de `int32`. A assinatura é verificada sobre os segmentos recebidos, antes de interpretar os claims; nada é reserializado.
- **Justificativa**: cumpre FR-008 com limites que cobrem o payload mínimo (FR-006) com folga e impedem negação de serviço por upload.
- **Alternativas**: sem limites explícitos (risco de DoS); validação só por schema (não pega duplicidade nem verifica antes de interpretar).

## D-004: Identidade do host e contagem de CPU

- **Decisão**: entidade local `Host` com ID opaco, associada administrativamente (e com auditoria) a um ou mais ambientes. A capacidade do host é a maior CPU confiável entre os ambientes associados. Novo campo aditivo no `AgentHello` indica a origem da CPU (`ENGINE` ou `PROCESS_FALLBACK`); `cpu_count` (tag 10) não muda. Agentes N-1 sem o campo são tratados como origem desconhecida: o valor entra como medição desconhecida, nunca como confiável. A tag do campo novo é reservada no registro de alocação de contratos de `specs/README.md` na fase de tarefas.
- **Justificativa**: hoje `AgentControlWorker` usa `Environment.ProcessorCount` quando o engine não informa CPU; esse valor pode refletir o container do agente e não pode virar dado comercial silencioso (FR-013). `EnvironmentId` identifica um engine, não um host (FR-012).
- **Alternativas**: inferir host por hostname (falsificável e instável); contar por ambiente (duplica hosts com dois engines); reutilizar `cpu_count` com valor sentinela (quebra N/N-1).

## D-005: Apuração e janela

- **Decisão**: observações brutas de capacidade por host (instante, vCPUs, origem) em tabela particionada por mês, com retenção técnica mínima de 35 dias mais uma âncora por host (última observação anterior à janela). O pico é recalculado a partir das observações brutas, para a faixa da licença vigente: `U(t) = Σ teto(vCPU_h(t) / B)`, `P = max U(s)` na janela de 30 dias, avaliando `U` em cada instante de mudança. Hosts desconectados mantêm a última capacidade conhecida; remoção fecha o intervalo sem apagar o histórico.
- **Justificativa**: cumpre FR-011, FR-014 e FR-015 (troca de faixa reapura dos dados brutos); avaliar só nos instantes de mudança mantém o custo proporcional às mudanças, não ao tempo.
- **Alternativas**: guardar só o pico convertido (impossível reapurar com outra faixa); usar a série de métricas operacionais da spec 009 (retenção e semântica diferentes, não é registro comercial).

## D-006: Estados, tolerância e relógio

- **Decisão**: estado efetivo com eixos separados (autenticidade, vigência, apuração, direitos) e prazos persistidos: tolerância de expiração em `expiresAt + graceDays`; tolerância de excesso com início e prazo gravados na primeira detecção; prazos não se somam (vale o mais cedo). Último instante confiável persistido; relógio abaixo dele deixa o estado `indeterminate` para direitos Business, sem conceder tolerância. Direitos são reavaliados em cada nova operação exclusiva e em cada execução de job; cache de verificação não estende prazo.
- **Justificativa**: cumpre FR-018 a FR-021 e os casos de borda de relógio e restauração.
- **Alternativas**: booleano `valid` (não expressa tolerância nem motivo); prazo contado do boot ou do upload (reinício estenderia o prazo).

## D-007: Persistência e auditoria global

- **Decisão**: schema `dokpod` no PostgreSQL existente, migrations SQL no padrão de `Migrations/Script/` (expand-contract): revisões importadas (bytes, hash, autor, resultado), estado efetivo (linha única com versão para concorrência otimista), hosts e associações, observações de capacidade (particionadas) e eventos de licenciamento (particionados, append-only). Os eventos de licenciamento formam trilha própria, porque `AuditEvent` exige `EnvironmentId` e uma importação é global.
- **Justificativa**: sem datastore novo (constituição V); partições seguem a restrição técnica de tabelas temporais; trilha própria evita ambiente fictício (`Guid.Empty`).
- **Alternativas**: estender `AuditEvent` com ambiente opcional (muda contrato de auditoria da spec 017 e suas consultas); arquivo local (sem transação nem concorrência).

## D-008: Autorização e API

- **Decisão**: recurso Keycloak da instalação `urn:dokpod:installation` com scopes `licensing:read` e `licensing:manage`, criados por `tools/scripts/configure-keycloak.ps1`. Endpoints `GET /api/v1/licensing/status`, `PUT /api/v1/licensing/license` (`If-Match`, antiforgery, 64 KiB) e `POST /api/v1/licensing/revocations` no OpenAPI existente. Recusa por licença usa `403` com `application/problem+json` e código tipado (`license_expired`, `license_capacity_exceeded`, `license_revoked`, `license_state_indeterminate`); upload inválido `422`; revisão em conflito `412`; nunca `401`.
- **Justificativa**: cumpre FR-022 a FR-024 seguindo o padrão de recurso e scope da spec 023 e o `problem+json` já usado na API.
- **Alternativas**: roles locais (proibido pela constituição III); middleware global de licença (bloquearia a CE, viola FR-003).

## D-009: Separação de edições

- **Decisão**: a edição é fixada no build: a imagem Business compõe o módulo de licenciamento e o trust store; a imagem CE não registra o módulo e não expõe a tela nem os endpoints de licença. `IBusinessCapabilityGate` só existe na composição Business; casos de uso CE nunca dependem dele. Não há variável de ambiente que transforme CE em Business.
- **Justificativa**: cumpre FR-001 e a regra do plano; o teste da História 1 roda sobre a imagem CE sem licença.
- **Alternativas**: um binário só com flag de edição (CE passaria a carregar o verificador e um interruptor trivial); repositório separado para a Business inteira (fora do escopo de 031; o local do código Business proprietário das specs 025 a 029 depende da revisão jurídica).

## D-010: CLI de emissão

- **Decisão**: repositório separado `dokpod-license-admin` (nome proposto), C#/.NET 10, consumindo `Dokpod.Licensing` por pacote de versão exata. Comandos `license inspect|verify|issue|renew|amend`, `revocation create`, `key list`, `audit export`; toda operação mutável com `--dry-run`. Registro de emissões transacional local da própria CLI; segredo nunca por argumento. Criação do repositório, nome, licença, visibilidade e provedor de chave são decisões da fase F2.
- **Justificativa**: cumpre FR-028 e FR-029 e o requisito de nenhum emissor nas imagens do cliente (SC-008).
- **Alternativas**: emissor em `backend/apps/` (proibido pelo plano: entraria no build do produto); serviço web de emissão (servidor 24x7 desnecessário e superfície de ataque).
- **Pendente (F2)**: o armazenamento do registro (SQLite proposto) exige seleção de versão e licença; provedor de chave (HSM ou equivalente) exige seleção própria.

## D-011: Revogação e rotação

- **Decisão**: pacote de revogação como JWS separado (`typ: dokpod-revocation+jws`), com escopo mínimo (IDs de licença do próprio cliente), sequência monotônica e propósito próprio. Trust store embarcado aceita mais de uma chave por `kid`, permitindo sobreposição na rotação. Documentos de um tipo nunca são aceitos como outro.
- **Justificativa**: cumpre a História 6 sem lista global de clientes e sem conexão.
- **Alternativas**: CRL ou OCSP (exigem rede); lista global de revogação (expõe outros clientes).

## D-012: Comprovação de ausência de comunicação externa

- **Decisão**: teste de integração com a stack em rede sem egress (só PostgreSQL e Keycloak locais), captura de DNS/TCP/UDP do processo da API durante início, importação, erro, renovação, expiração e revogação, e busca de canários de licença nos logs, métricas e traces exportados.
- **Justificativa**: SC-002 exige zero tentativas, não ausência de resposta.
- **Alternativas**: revisão de código apenas (não prova comportamento de dependências).
