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

## Threat model: inspeção e logs sob demanda (spec 011)

**Fronteira nova**: navegador → BFF → API → stream mTLS do agente → API do engine (`/containers/{id}/json` e `/logs`). Consultas são mensagens tipadas (`SUMMARY`, `INSPECT`, `LOGS`), nunca repasse genérico de rota ou payload.

**Atores**: operador autorizado (`container:inspect`, `container:logs`), usuário sem o scope, usuário de outro ambiente, agente comprometido, administrador do host.

| Ameaça | Mitigação | Teste negativo |
| --- | --- | --- |
| Leitura cruzada de ambiente (IDOR) | Autorização Keycloak por ambiente em toda consulta; 404 indistinguível de "não autorizado" para container | container de ambiente não autorizado responde 404 |
| Scope ausente | `container:inspect`/`container:logs` avaliados por requisição e a cada 60 s no streaming | 403 em `summary`, `inspect`, `logs` e no hub sem o scope |
| Vazamento de segredo por campo do engine | Allowlist no resumo; denylist na inspeção (`Config.Env`, `Config.CreateCommand`, binds fora da allowlist, chaves com `password\|secret\|token\|key\|credential\|auth`); `SecretMasker` sobre todo valor de texto (labels, opções de log, health check, argumentos e campos desconhecidos) | canários por campo ausentes em respostas, logs de nginx/BFF/API/agente, auditoria e PostgreSQL |
| Vazamento de segredo em logs | Mascaramento no agente antes do envio (Bearer, `password=`, `secret=`, `token=`, `user:pass@`, `-u user:pass`, chaves privadas, JWT); aviso de best-effort na interface | linhas com canários chegam mascaradas |
| Termo de busca em logs de acesso | `contains` literal, ≤ 128, removido dos logs da API e do BFF | termo canário ausente nos logs |
| Persistência indevida | Conteúdo nunca vai ao PostgreSQL; auditoria grava só metadados (ator, ambiente, container, volume) | varredura do banco por canário |
| DoS no agente/canal gRPC | ≤ 4 consultas e ≤ 10 sessões ao vivo por agente, ≤ 3 por usuário; mensagens ≤ 256 KiB (limite gRPC 1 MiB); 200 linhas/s e 256 KiB/s por sessão; prioridade de comandos sobre consultas | resposta máxima não derruba a sessão; 429 acima dos limites |
| Revogação durante sessão ao vivo | Reavaliação a cada 60 s; logout e expiração da sessão do BFF encerram; `STOP` enviado ao agente | sessão encerrada em ≤ 60 s após revogar |
| Conteúdo malicioso no log (XSS, ANSI) | Sequências ANSI e controles removidos no agente; renderização somente como texto | linha com `<script>` e ANSI exibida como texto |
| Agente comprometido | Resposta tratada como dado não confiável, com limites de tamanho e schema; nunca executada | resposta acima do limite é truncada |

**Risco residual**: segredos sem padrão reconhecível em campos fora da denylist podem aparecer na inspeção; a interface avisa que o mascaramento é best-effort. Aprovação: decisão do solicitante registrada em `specs/README.md` (2026-10-13), com revisão humana de segurança pendente antes de release.

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