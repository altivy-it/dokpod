# ADR 2026-0008: Licença assinada e verificação offline da edição Business

**Status:** proposed

**Data:** 2026-10-29  
**Responsáveis:** zocatel (responsável pelo produto)  
**Origem:** IA assistida, a pedido do solicitante (fase F0 da spec 031)  
**Revisor humano:** zocatel; revisão pendente  
**Relacionado:** [spec 031](../../specs/031-licenciamento-business-offline/spec.md), [plan 031](../../specs/031-licenciamento-business-offline/plan.md) (decisões D-001, D-002 e D-009 de [research.md](../../specs/031-licenciamento-business-offline/research.md)), [ADR 2026-0003](2026-0003-licenciamento-e-edicoes.md), [ADR 2026-0007](2026-0007-precificacao-por-node-e-faixa-de-vcpu.md)

## Contexto

O ADR 2026-0003 decidiu que a Business valida um arquivo de licença assinado, localmente e offline, com só a chave pública no produto, e que a CE não usa chave. Ele não define formato, algoritmo, onde mora o verificador nem como CE e Business se separam. Em 2026-10-29 o solicitante tomou a `v0.1.0` como marco de estabilização da CE e abriu a fase F0 do licenciamento. Essas escolhas são duradouras: mudar o formato depois exige compatibilidade com licenças já emitidas e com versões do produto já instaladas.

## Forças de decisão

- verificação sem rede, sem servidor do fornecedor e sem algoritmo próprio;
- resistência a adulteração e às confusões conhecidas de tokens assinados (algoritmo ou chave escolhidos pelo próprio arquivo);
- uma única implementação do formato para o produto e para a CLI de emissão, que fica em repositório separado;
- CE sem dependência do mecanismo em runtime e sem interruptor trivial para virar Business;
- nenhuma dependência nova sem licença permissiva verificada (constituição);
- reversibilidade: poder introduzir outro perfil no futuro sem invalidar licenças vigentes.

## Opções consideradas

### Opção A: JWS Compact com perfil fechado `ES256`, verificador BCL-only e edição fixada no build

JWS (RFC 7515) com ECDSA P-256 e SHA-256 (RFC 7518 §3.4), header limitado a `alg`, `kid` e `typ`, trust store embarcado. Verificador em `backend/libs/Dokpod.Licensing`, só com a BCL do .NET 10, MIT proposta, sem I/O. A imagem Business compõe o módulo; a CE não o registra.

Vantagens: padrão aberto e auditável; criptografia da BCL, sem dependência; assinatura de 64 bytes; o perfil fechado elimina `alg: none`, HMAC com chave pública e chave embutida; a CLI consome a mesma biblioteca. Desvantagens: JWS genérico é amplo e exige disciplina para fechar o perfil; ECDSA exige gerador de números aleatórios correto no emissor (responsabilidade do provedor de chave).

### Opção B: biblioteca ou serviço de licenciamento de terceiros

Vantagens: menos código próprio. Desvantagens: as opções avaliadas no plano de origem eram source-available ou dependiam de servidor online, conflitando com a política de dependências e com a operação offline; formato fora do controle do projeto.

### Opção C: formato binário ou XML assinado próprio

Vantagens: controle total. Desvantagens: mais superfície de parser, sem ferramentas de inspeção, sem ganho de segurança sobre a opção A.

## Decisão

Adotar a **opção A**, com estes limites:

1. Licença e pacote de revogação são JWS Compact com perfil fechado: só `ES256`, assinatura IEEE P1363, header só com `alg`, `kid` e `typ` (`dokpod-license+jws` ou `dokpod-revocation+jws`), `kid` procurado apenas no trust store embarcado; qualquer outro header recusa o documento. A assinatura é verificada sobre os segmentos recebidos antes de interpretar o payload.
2. O payload segue [contracts/license-format.md](../../specs/031-licenciamento-business-offline/contracts/license-format.md): IDs opacos, sem dados pessoais, inventário ou preço; limites de tamanho e estrutura definidos.
3. O verificador é a biblioteca `Dokpod.Licensing`, só BCL, sem I/O nem rede, consumida pelo produto e pela CLI por versão exata, com vetores de conformidade públicos.
4. A edição é fixada no build: só a imagem Business registra o módulo e o trust store; nenhuma configuração transforma CE em Business.
5. Um perfil novo (outro algoritmo, inclusive pós-quântico) entra como perfil adicional, com novo `typ` ou `schemaVersion`, sem remover o anterior enquanto houver licenças vigentes.

Condições de revisão: parecer jurídico contrário à licença MIT do verificador ou à composição CE/Business; vulnerabilidade relevante em ECDSA P-256; exigência de cliente que torne `ES256` inaceitável.

## Consequências

### Positivas

- Verificação offline com código auditável e sem dependência nova.
- Formato único entre emissor e produto, testado por vetores compartilhados.
- CE sem o mecanismo em runtime, como exige o ADR 2026-0003.

### Negativas e trade-offs

- Duas imagens por versão (CE e Business), com custo de build, teste e documentação.
- O perfil fechado precisa de testes negativos extensos; um erro no parser é um risco de segurança direto.
- A segurança depende inteiramente da guarda da chave privada pelo fornecedor (provedor de chave a decidir na fase F2).

## Segurança e privacidade

O arquivo só tem integridade e autenticidade, não sigilo; não contém dados pessoais, hostnames, inventário ou preço. Chaves de licença são separadas de mTLS, TLS, Keycloak e Data Protection. Chaves sintéticas de teste nunca entram no trust store de produção. O licenciamento não faz comunicação externa. Threat model da importação e do trust store é entrega da fase F1, antes de código.

## Dados, protocolo e compatibilidade

Perfil 1: `schemaVersion: 1`, `meteringVersion: 1`. Licenças de um perfil continuam aceitas enquanto a versão do produto suportar esse perfil; a rotação de chave usa sobreposição de `kid` no trust store. Não há mudança no protocolo agente-servidor por esta decisão (a origem da CPU é tratada na spec 031, D-004).

## Observabilidade e operação

Resultado da verificação em logs só com códigos tipados e IDs opacos, nunca o arquivo. Eventos de importação na trilha global de licenciamento da spec 031.

## Validação

A decisão é falsificada se qualquer um falhar:

- vetores independentes: licença válida aceita; bit alterado, `alg: none`, HS256 com a chave pública, `jwk`/`jku`/`x5u` no header, `kid` desconhecido, propriedade duplicada, 65 KiB e profundidade excessiva recusados com o código esperado;
- interoperabilidade: licença assinada por implementação independente de ES256 aceita pelo verificador;
- teste de arquitetura: `Dokpod.Licensing` sem referência a projetos do produto ou pacotes externos; imagem CE sem o módulo registrado;
- captura de rede: zero tentativas externas durante verificação, importação e erro;
- verificação de uma licença de 64 KiB abaixo de 50 ms.

## Rollout e rollback

Implementação só depois do aceite deste ADR, da revisão jurídica (spec 031, FR-030) e do aceite do ADR 2026-0007 para as regras de capacidade (FR-031). Rollback: a imagem CE continua sem o módulo; uma instalação Business volta à CE trocando a imagem, sem perda de dados. Trocar de formato depois exige ADR novo e período de convivência dos perfis.

## Referências

- [RFC 7515: JSON Web Signature](https://www.rfc-editor.org/rfc/rfc7515)
- [RFC 7518, seção 3.4: ES256](https://www.rfc-editor.org/rfc/rfc7518#section-3.4)
- [Pesquisa da spec 031](../../specs/031-licenciamento-business-offline/research.md)
