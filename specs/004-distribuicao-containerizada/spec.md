# Especificação da Feature: Distribuição Containerizada

**Branch da migração**: `chore/adotar-spec-kit`; branch de implementação a definir após autorização.

**Criada em**: 2026-10-06

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: [P04](../../docs/plan/p04-padronizacao-dockerfiles-compose.md), distribuição do [MVP](../../docs/plan/mvp.md) e borda de P05-07.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Construir a mesma aplicação em dev e runtime (Prioridade: P1)

Como mantenedor, quero builds reproduzíveis dos quatro componentes usando Dockerfiles proprietários reutilizados.

**Valor desta prioridade**: Evitar divergência dev/prod e múltiplas regras de build.

**Teste independente**: Build limpo por target e inspeção de imagem com manifests/toolchains identificados.

**Cenários de aceite**:

1. **Dado** checkout limpo, **Quando** API, BFF, web e agente Linux são construídos, **Então** usam bases fixadas e contratos incluídos, sem dependência acidental de artefatos locais.
2. **Dado** imagem runtime, **Quando** inspecionada, **Então** contém apenas artefatos publicados, sem SDK, secrets ou runtime de Node no servidor estático web.

### Jornada do Usuário 2 - Acessar uma borda segura (Prioridade: P1)

Como operador, quero iniciar a aplicação pela topologia canônica, sem duplicar a plataforma compartilhada de identidade.

**Valor desta prioridade**: A borda impede bypass da sessão BFF.

**Teste independente**: Validar configuração/startup, health, login/relay e tentativa de acesso direto pela URL nginx.

**Cenários de aceite**:

1. **Dado** rede/plataforma central autorizadas, **Quando** o Compose Dokpod inicia, **Então** integra web/BFF/API e agente opcional, sem criar cópias de nginx, Keycloak ou PostgreSQL centrais.
2. **Dado** navegador, **Quando** usa `/bff`, `/api/v1` ou `/hubs`, **Então** a borda encaminha ao BFF, nunca oferece rota alternativa para API.
3. **Dado** recriação de container, **Quando** retomado, **Então** key ring, identidade e journal necessários persistem com permissões mínimas.

## Casos de Borda

- Rede externa ausente, secret/certificado inválido, health/readiness indisponível e reinício parcial.
- Usuário runtime sem permissão no mount, volume Windows com ownership divergente e contexto de build incompleto.
- Socket montado read-only ainda mutável; perfil agente é privilégio administrativo, não isolamento de leitura.
- Forwarded headers não confiáveis, rota direta exposta, WebSocket/base path e dependência central degradada.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: API, BFF e web DEVEM executar em containers; agente Linux em OCI; Windows é exceção Worker Service self-contained, qualificada em 006.
- **FR-002**: Cada aplicação DEVE reutilizar Dockerfile multi-stage proprietário com targets existentes dev/publish/runtime conforme necessidade; nenhuma migração documental cria um segundo build.
- **FR-003**: Toolchains/bases `lzocateli/*` DEVEM seguir a [matriz de distribuição](../../docs/distribuicao.md), com versões fixadas, contexto mínimo, runtime sem SDK e usuário não root quando compatível com socket.
- **FR-004**: Compose canônico `deploy/e2e/docker-compose-dokpod.yaml` DEVE integrar serviços Dokpod à plataforma central pela rede externa `identity-global`, sem duplicar nginx/Keycloak/PostgreSQL. A divergência histórica com `identity-client` requer avaliação, não escolha automática.
- **FR-005**: HTTP de browser/runners DEVE passar exclusivamente por nginx containerizado, inclusive testes simulados; API/BFF e infraestrutura não têm publicação direta alternativa no deployment padrão.
- **FR-006**: Secrets/certificados DEVEM vir de provider/arquivo externo em runtime, sem leitura/exibição nesta migração; key ring/journal/identidade exigem mounts persistentes e permissões mínimas.
- **FR-007**: Configuração DEVE ter redes, portas, forwarded headers, limites, health dependencies e reinício explicitamente verificados; mTLS do agente permanece obrigatório.
- **FR-008**: Perfil agente com socket DEVE ficar restrito ao laboratório autorizado até revisão específica; montar read-only não diminui poderes da API do engine.
- **FR-009**: Builds/testes/smokes DEVEM usar ferramentas em containers; assinatura/provenance, scans e qualificação de release pertencem a 005, sem publicação autorizada por esta spec.

### Entidades Principais

- **Imagem/target**: artefato identificado por versão/digest, estágio e plataforma.
- **Topologia/mount**: serviço, rede, porta, health e dados persistentes, sem valores de secrets.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Os quatro builds limpos e smokes pertinentes passam com bases identificadas e runtime mínimo.
- **SC-002**: Inspeções confirmam usuário, portas, mounts, redes e ausência de secrets/SDK em cada runtime.
- **SC-003**: Jornada pública funciona só pela borda nginx e persiste dados necessários após recriação, sem infraestrutura central duplicada.

## Premissas e Proveniência

P04 já registra Dockerfiles, builds e stack saudáveis, mas lista startup/health pendentes e falhas antigas de testes amplos. Avaliar diferença, não reconstruir os quatro componentes como nova feature. P04-01 a P04-04 ficam aqui; P04-05 a P04-07 em 002; MVP P-01/P-02 e P05-07 têm recortes de distribuição aqui, hardening em 005 e pacote Windows em 006.
