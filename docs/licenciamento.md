# Licenciamento e comercialização

**Status da política:** decisão aceita (2026-09-06); revisão de preços, métrica e planos aceita em 2026-10-30 ([ADR 2026-0007](adr/2026-0007-precificacao-por-node-e-faixa-de-vcpu.md)); formato da licença no [ADR 2026-0008](adr/2026-0008-licenca-assinada-e-verificacao-offline.md), aceito em 2026-10-30  
**Data:** 2026-09-06, revisada em 2026-10-27 e 2026-10-30  
**Implementação atual:** nenhuma edição publicada; CE é a primeira entrega planejada

Esta é a política operacional derivada do [ADR 2026-0003](adr/2026-0003-licenciamento-e-edicoes.md). O ADR registra contexto e decisão; este documento é a referência para escopo das edições, comercialização e gates de release.

## Modelo

O Dokpod terá duas edições: a Community Edition gratuita e a edição comercial (Business), vendida em três planos anuais. Os planos abaixo seguem o [ADR 2026-0007](adr/2026-0007-precificacao-por-node-e-faixa-de-vcpu.md), aceito em 2026-10-30, incluindo nodes, faixas de vCPU, contagem, pacote adicional, tolerância e cobrança.

| Aspecto | Community Edition | Teams | Business | Enterprise |
| --- | --- | --- | --- | --- |
| Licença | `AGPL-3.0-only` | comercial proprietária | comercial proprietária | comercial proprietária |
| Preço | gratuito | US$ 900/ano | US$ 1.500/ano | sob medida |
| Nodes | sem limite artificial | até 5 | 5, mais pacotes de 5 a US$ 1.200/ano com prorrata, até 25 | sem teto |
| vCPUs por node | sem limite | até 16 | até 32 | acima de 32 |
| Usuários | sem limite artificial | sem cobrança ou limite | sem cobrança ou limite | sem cobrança ou limite |
| Instalações | sem limite imposto | uma de produção e uma de homologação | uma de produção e uma de homologação | conforme contrato |
| Chave de licença | não exige | arquivo assinado, verificável offline | arquivo assinado, verificável offline | arquivo assinado, verificável offline |
| Suporte | comunidade | comunidade e documentação | e-mail em horário comercial, próximo dia útil | SLA contratual |
| Recursos comerciais | não | governança e conformidade | governança, conformidade e escala de frota | todos, mais builds de suporte estendido |
| Cobrança | gratuita | USD, online por merchant of record | fatura em USD (exterior) ou BRL com NFS-e (Brasil) | contrato |

Tolerância de 30 dias para expiração e excesso, avisos 60, 30 e 7 dias antes de vencer, regularização por pacote com prorrata e sem cobrança retroativa; detalhes e pendências tributárias no [ADR 2026-0007](adr/2026-0007-precificacao-por-node-e-faixa-de-vcpu.md#decisões-do-solicitante-sobre-as-pendências-2026-10-30).

A política anterior (US$ 99/mês ou US$ 990/ano por organização, até 100 nodes) permanece registrada no [ADR 2026-0003](adr/2026-0003-licenciamento-e-edicoes.md) e foi substituída pelo ADR 2026-0007 em 2026-10-30.

A CE será implementada e estabilizada antes do início da Business. Os preços são uma decisão de produto; impostos, moeda de cobrança, meios de pagamento, SLA e contrato comercial precisam de validação jurídica e financeira antes da primeira venda.

## Community Edition

A CE será um produto self-hosted funcional, sem chave, telemetria comercial obrigatória ou dependência da infraestrutura do fornecedor. Seu baseline inclui:

- múltiplos ambientes e usuários;
- Docker Linux e demais engines promovidos pela matriz pública de suporte;
- inventário, saúde e sincronização de containers;
- iniciar, parar, reiniciar e excluir containers;
- autenticação pelo Keycloak;
- autorização por ambiente e por ação no Keycloak (scopes de `docs/configuracao-keycloak.md`), com papéis de administração, operação e leitura;
- identidade mTLS dos agentes, com revogação;
- auditoria persistente de comandos e resultados, filtrável e exportável em CSV;
- API e protocolos públicos necessários à interoperabilidade;
- backup, restauração e atualização manuais;
- correções de segurança para versões suportadas.

A CE não terá limites artificiais de nodes, usuários ou containers. Limites técnicos publicados continuam valendo como matriz testada, não como mecanismo de licenciamento.

## Business

**A Business é uma edição futura; das funcionalidades exclusivas abaixo, só a exportação para SIEM foi implementada
([capabilities](#capabilities-implementadas)).** O mecanismo de licenciamento offline que as habilita já existe
([spec 031](../specs/031-licenciamento-business-offline/spec.md)): a imagem Business verifica a licença, apura nodes e
aplica tolerância e restrição; a CE não contém nada disso ativo.
Quando implementada, incluirá todo o baseline da CE e concentrará valor em governança, automação e suporte:

- permissões por equipe e por recurso individual, geridas no console (a CE já autoriza por ambiente e por ação);
- retenção configurável de auditoria e exportação para SIEM por syslog e OpenTelemetry ([spec 027](../specs/027-exportacao-auditoria-siem/spec.md));
- políticas centralizadas e detecção de divergência;
- janelas de mudança por estágio ([spec 026](../specs/026-janelas-de-mudanca/spec.md));
- aviso de nova versão das imagens em uso ([spec 025](../specs/025-aviso-nova-versao-imagem/spec.md));
- implantação somente por código em produção ([spec 028](../specs/028-implantacao-somente-por-codigo/spec.md));
- modelos de SSO prontos para Microsoft Entra ID, Google e GitHub ([spec 029](../specs/029-modelos-sso/spec.md));
- operações agendadas (as ações em lote já fazem parte da CE, [spec 010](../specs/010-lista-containers-acoes-lote/spec.md));
- integrações externas de alertas (a governança de alertas e a parada justificada já fazem parte da CE, [spec 016](../specs/016-governanca-alertas/spec.md));
- GitOps, stacks e Compose quando essas capacidades forem implementadas;
- backup automático e políticas de retenção;
- atualização coordenada de agentes com rollout e rollback;
- relatórios operacionais e de conformidade;
- builds com ciclo de suporte estendido;
- suporte comercial por e-mail.

Esta matriz descreve intenção de produto, não funcionalidades já implementadas. A reconciliação com o que já foi entregue na CE está na [spec 031](../specs/031-licenciamento-business-offline/spec.md#matriz-cebusiness-reconciliação-de-2026-10-29): nada já entregue na CE migra para a Business. Cada capability ainda depende de contrato, testes, segurança, documentação e gate de release próprios.

### Capabilities implementadas

A licença lista as capabilities em `capabilities`; a imagem Business só executa um recurso exclusivo se a capability
estiver nela. Com a licença restrita, o que já está configurado continua funcionando e só operações novas são recusadas.

| Capability | Recurso | Com a licença restrita ou sem a capability | Spec e operação |
| --- | --- | --- | --- |
| `audit-siem-export` | Exportação da trilha de auditoria para SIEM por syslog e OpenTelemetry | Destinos ativos continuam enviando; criar, editar, reativar e testar são recusados; desativar e excluir sempre funcionam | [spec 027](../specs/027-exportacao-auditoria-siem/spec.md), [runbook](runbooks/auditoria-exportacao-siem.md) |

## Métricas comerciais

### Organização

Uma organização é a pessoa jurídica ou unidade contratante identificada no contrato e na licença. Empresas relacionadas, uso por prestador de serviço e ambientes de clientes precisam ser tratados expressamente pelo contrato comercial; não se presume direito de sublicenciamento ou operação como serviço gerenciado.

### Node

Um node é um host físico, máquina virtual ou dispositivo com engine Docker ou Podman gerenciado pelo Dokpod. A quantidade de containers e usuários não altera a contagem. Um host com mais de um engine conta uma vez enquanto representar o mesmo ambiente computacional gerenciado.

Pelo ADR 2026-0007 (aceito em 2026-10-30), cada node conta como `teto(vCPUs / faixa do plano)` nodes equivalentes, com os vCPUs lógicos informados pelo agente (`cpu_count` no `Hello`). A apuração usa o maior total dos últimos 30 dias, calculado localmente.

A CE mede nodes apenas para capacidade e observabilidade local. A edição comercial usa a contagem para validar o plano contratado, sem transmitir inventário ao fornecedor.

## Licença da CE

O código original publicado como Dokpod Community Edition será disponibilizado sob **GNU Affero General Public License v3.0 only**, identificador SPDX `AGPL-3.0-only`.

Em termos operacionais, a licença permite usar, estudar, modificar e redistribuir a CE, inclusive comercialmente, desde que suas condições sejam cumpridas. Quando uma versão modificada oferecer interação remota pela rede, seus usuários devem receber uma forma de obter o código-fonte correspondente dessa versão, conforme a seção 13 da AGPL.

O texto integral em [LICENSE](../LICENSE) prevalece sobre este resumo. Este documento não é aconselhamento jurídico e não substitui a análise da licença para um caso concreto.

## Escopo e marcas

A AGPL cobre somente arquivos identificados como parte da CE e não altera licenças de componentes de terceiros. Imagens, dependências, fontes e assets preservam seus próprios avisos e termos.

A licença de copyright não concede direito de usar nomes, logotipos, domínios ou identidade visual do Dokpod para sugerir endosso ou origem oficial. Uma política de marcas deve ser publicada antes da primeira release pública.

Código e artefatos exclusivos da Business não serão distribuídos sob AGPL, salvo indicação explícita no próprio arquivo. A simples proximidade em um repositório, imagem ou pacote não deve ser usada para contornar obrigações da AGPL; a composição final precisa de revisão jurídica.

## Duplo licenciamento e contribuições

O titular dos direitos pode oferecer seu próprio código simultaneamente sob AGPL e sob licença comercial. Essa possibilidade depende de controlar os direitos necessários sobre todo código incorporado à distribuição proprietária.

Antes de aceitar contribuições externas, o projeto deverá publicar um CLA aprovado juridicamente ou adotar outro mecanismo explícito que permita usar a contribuição tanto na CE quanto na Business. Até essa definição:

- contribuições externas não serão incorporadas ao produto;
- dependências continuam exigindo licença compatível e verificada;
- código AGPL de terceiros sem direito adicional não será incluído na Business;
- proveniência e autoria serão preservadas em cada contribuição.

## Licença Business

A Business usará um arquivo de licença assinado digitalmente. O payload mínimo será versionado e conterá:

- identificador da licença e da organização;
- edição e versão do schema;
- datas de emissão, início e expiração;
- limite de nodes e faixa de vCPUs por node do plano;
- uma instalação de produção e uma de homologação;
- capacidades licenciadas;
- assinatura digital.

Somente a chave pública de verificação será distribuída com o produto. A chave privada permanecerá fora dos repositórios, imagens e ambientes de cliente. A validação será local e não exigirá conexão periódica com o fornecedor.

Expiração, arquivo inválido ou excesso de nodes nunca poderá parar containers, excluir dados ou bloquear backup e exportação. A política de enforcement deverá:

1. avisar administradores de forma auditável;
2. conceder período de tolerância definido no contrato;
3. impedir novas execuções de funcionalidades exclusivas após a tolerância;
4. preservar leitura, exportação, renovação e todas as funcionalidades da CE;
5. manter workloads existentes sem interferência.

O período de tolerância e o SLA serão definidos no contrato antes da implementação; não devem ser codificados sem essa decisão.

A [spec 031](../specs/031-licenciamento-business-offline/spec.md) implementou essa política com os valores aprovados
nela: tolerância vem da própria licença, avisos em 60, 30 e 7 dias, e restrição só de novas execuções Business. A
contratação e cada renovação exigem a exportação do relatório de apuração com o desafio do fornecedor; o arquivo é
encadeado por hashes e o fornecedor o verifica sem rede (D-013). Operação: [runbook de importação](runbooks/licenciamento-importacao.md)
e [runbook de rotação e revogação](runbooks/licenciamento-rotacao-e-revogacao.md).

## Suporte e atualizações

- A CE recebe suporte comunitário e documentação pública.
- A Business recebe suporte por e-mail conforme SLA contratual.
- Vulnerabilidades suportadas recebem correção nas duas edições sem atraso deliberado para criar vantagem comercial.
- Recursos Business podem ter ciclo de suporte estendido, desde que isso não retire correções de segurança da CE suportada.
- O cliente mantém acesso aos próprios dados mesmo após o fim da assinatura.

## Checklist antes da primeira release CE

- preservar o texto oficial da `AGPL-3.0-only` em [LICENSE](../LICENSE);
- incluir avisos legais visíveis e link para o código-fonte na interface web;
- publicar código-fonte correspondente, scripts de build e instruções de instalação;
- inventariar licenças de dependências, imagens e assets;
- publicar política de marcas e de contribuições;
- revisar o modelo com assessoria jurídica na jurisdição de operação.

## Checklist antes da primeira venda Business

- aprovar CLA ou mecanismo equivalente;
- aprovar licença comercial, contrato, SLA, privacidade e tributação;
- definir tolerância, renovação, cancelamento e reembolso;
- validar assinatura, adulteração, expiração, relógio incorreto e recuperação;
- testar que enforcement não afeta workloads, dados ou funcionalidades CE;
- demonstrar suporte para 100 nodes na matriz publicada;
- separar artefatos, SBOMs, avisos e pipelines das duas edições.

## Componentes de terceiros no console web

Registro exigido pela spec 007 (T006/T027) e pelo ADR 2026-0005. Toda inclusão nova exige licença permissiva compatível com `AGPL-3.0-only`, versão fixada e necessidade demonstrada.

| Componente | Uso | Licença | Versão | Local | Distribuído |
| --- | --- | --- | --- | --- | --- |
| Outfit (400/500/600, latin) | fonte da interface | SIL OFL 1.1 | `@fontsource/outfit` 5.3.0 (woff2 versionado) | `frontend/web/src/styles/fonts/` + `OFL-Outfit.txt` | sim |
| JetBrains Mono (400/500, latin) | fonte de dados técnicos | SIL OFL 1.1 | `@fontsource/jetbrains-mono` 5.3.0 (woff2 versionado) | `frontend/web/src/styles/fonts/` + `OFL-JetBrainsMono.txt` | sim |
| Geometria de ícones | ícones `dok-icon` | própria do kit Altivy (`docs/design/app/project/ds/altivy`), traço no estilo Lucide redesenhado no kit | — | `frontend/web/src/app/design-system/icon/icon-paths.ts` | sim |
| `axe-core` | auditoria de acessibilidade no E2E | MPL-2.0 | 4.14.0 (devDependency exata em `frontend/web/package.json`) | `axe.min.js` injetado pelo runner `frontend/tests/e2e/` | não |
| `@xterm/xterm` | emulador do terminal interativo (spec 014, ADR 2026-0006) | MIT | 6.0.0 (dependency exata em `frontend/web/package.json`) | chunk lazy do detalhe do container | sim (aviso de copyright MIT no bundle) |
| `@xterm/addon-fit` | ajuste do terminal ao tamanho do painel | MIT | 0.11.0 (dependency exata) | chunk lazy do detalhe do container | sim |

Fontes OFL podem ser empacotadas com software sob qualquer licença desde que acompanhadas do texto da licença e sem venda isolada das fontes; os arquivos `OFL-*.txt` acompanham os `woff2`. `axe-core` é usado apenas no runner de testes e não integra artefatos distribuídos.

## Referências

- [ADR 2026-0003](adr/2026-0003-licenciamento-e-edicoes.md)
- [GNU Affero General Public License v3.0](https://www.gnu.org/licenses/agpl-3.0.html)
- [SPDX: AGPL-3.0-only](https://spdx.org/licenses/AGPL-3.0-only.html)
