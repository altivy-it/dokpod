# Especificação da Feature: Qualificação Windows e Podman

**Branch da migração**: `chore/adotar-spec-kit`; branch de execução a definir após autorização.

**Criada em**: 2026-10-06

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: MVP P-07 e recortes P-01/P-02/P-05/P-06 do [MVP](../../docs/plan/mvp.md), P-05 do [desbloqueio](../../docs/plan/desbloqueio-release.md).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Instalar agente Windows sem runtime (Prioridade: P1)

Como operador Windows, quero um Worker Service self-contained com identidade/journal protegidos e preservados.

**Valor desta prioridade**: Execução em console e DryRun não qualificam o serviço.

**Teste independente**: Instalar em host Windows limpo sem runtime .NET, exercitar serviço, atualização, rollback e remoção com dados sintéticos.

**Cenários de aceite**:

1. **Dado** RID/Windows Server/Docker/conta aprovados na matriz, **Quando** instalado, **Então** inicia como Windows Service com conta dedicada, named pipe permitido e ACL mínima de pipe/dados.
2. **Dado** principal não autorizado ou pipe alternativo, **Quando** tenta acesso/startup, **Então** é rejeitado sem ampliar permissões.
3. **Dado** atualização/rollback/remoção, **Quando** executados, **Então** identidade, certificados e journal persistem fora do binário e remoção preserva dados por padrão.

### Jornada do Usuário 2 - Comprovar durabilidade Windows (Prioridade: P1)

Como operador, quero que crash e perda de resposta não quebrem journal-before-ack e deduplicação.

**Valor desta prioridade**: Garantia de filesystem Linux não pode ser atribuída automaticamente ao Windows.

**Teste independente**: Injetar interrupções antes/depois de ACK/resultado e retomar serviço/host comparando journal e efeito no engine.

**Cenários de aceite**:

1. **Dado** comando aceito, **Quando** há crash após ACK, **Então** o registro durável já existe e replay não perde intenção silenciosamente.
2. **Dado** resultado persistido, **Quando** serviço reconecta com novo fencing, **Então** dedup reproduz resultado sem novo efeito, preservando at-least-once e reconciliação, não exactly-once.

### Jornada do Usuário 3 - Qualificar capabilities Podman (Prioridade: P1)

Como operador Linux, quero suporte Podman somente para modos e capabilities realmente exercitados.

**Valor desta prioridade**: Docker e Libpod têm diferenças de API, privilégio e lifecycle.

**Teste independente**: Provas em engines Podman rootless/rootful reais com versão/mount/capabilities identificados.

**Cenários de aceite**:

1. **Dado** capability ausente ou divergência Libpod, **Quando** o comando é solicitado, **Então** ele é rejeitado ou tratado pelo adapter específico, nunca presumido equivalente ao Docker.
2. **Dado** mount/socket de usuário rootless, **Quando** o agente opera, **Então** acessa só a engine local permitida e principals não autorizados não a acessam.
3. **Dado** matriz não exercitada, **Quando** suporte é documentado, **Então** permanece experimental/adiado por decisão humana, sem estabilidade implícita.

## Casos de Borda

- RID/versão Docker/Windows Server não suportados, conta/ACL inadequadas e instalação com elevação não autorizada.
- Falha de disco/flush, crash, journal corrompido, perda de resposta, startup atrasado e recuperação limitada.
- Update interrompido, rollback com protocolo N-1, artefato inválido/revogado e remoção acidental de dados.
- Rootless/rootful, Libpod, mount, capabilities distintas; Podman Windows é engine Linux em VM quando aplicável, não suporte nativo a Windows containers.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: Agente Windows DEVE ser Worker Service .NET 10 self-contained por RID aprovado, executável assinado, sem runtime .NET pré-instalado; build/test runners permanecem containerizados e instalação/smoke do serviço ocorrem em host Windows.
- **FR-002**: Matriz DEVE identificar Windows Server/RID/Docker/conta/pipe/ACL; startup recusa configuração fora da política, com chaves/certificados/journal em diretório protegido separado do binário.
- **FR-003**: Instalação, startup atrasado, recuperação automática limitada, update atômico, rollback e remoção DEVEM preservar identidade/journal; DryRun/console não comprovam serviço real.
- **FR-004**: Windows DEVE comprovar garantia equivalente de journal-before-ack e persistência-before-report sob crash/restart, dedup ID/hash, fencing, deadline, replay e reconciliação em engine real; não copiar promessa de fsync Linux sem prova de plataforma.
- **FR-005**: Ambos DEVEM manter engine local como fonte, gRPC mTLS/identidade individual, negociação N/N-1 e capabilities, API sem socket remoto, BFF/Keycloak inalterados.
- **FR-006**: Podman Linux DEVE qualificar rootless/rootful, versões/APIs, mounts/permissões e diferenças Libpod; capability não anunciada é recusada, sem proxy genérico ou equivalência Docker presumida.
- **FR-007**: Principals não autorizados NÃO DEVEM acessar pipe/socket/dados; delete não remove volume implicitamente e nenhuma prova usa workloads/secrets reais.
- **FR-008**: Provas 2 e 3 de viabilidade e gates de distribuição/segurança por capability DEVEM possuir evidência sanitizada e decisão humana stable/experimental/adiada; Podman Windows nativo fica fora do escopo.
- **FR-009**: Supply chain e gate HIGH/CRITICAL de 005 também se aplicam aos artefatos anunciados. Docker Linux não recebe Windows/Podman estáveis por transitividade; adiamento precisa ser formal na matriz inicial.

### Entidades Principais

- **Capability/matriz**: engine, modo, versão, SO, RID, conta, permissões e classificação de suporte.
- **Pacote/journal**: binário verificável e dados persistentes com política de preservação.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Ciclo real de Windows Service passa em host limpo sem runtime e preserva identidade/journal em update, rollback e remoção.
- **SC-002**: Testes de crash/replay/ACL demonstram durabilidade equivalente, dedup e rejeição de acesso não autorizado.
- **SC-003**: Cada combinação Podman anunciada passa provas reais e possui diferenças/capabilities documentadas.
- **SC-004**: Nenhuma capability não exercitada é declarada estável; decisão humana por matriz é referenciada.

## Premissas e Proveniência

MVP registra publish `win-x64`, console/named pipe/mTLS e script DryRun; P-07 permanece `not-started`. Não refazer pacote/script já existente. Instalação real, conta/ACL, crash durability Windows, update/rollback e Podman seguem sem qualificação. Este conjunto preserva a exceção Windows e não autoriza elevação, instalação ou alteração de hosts nesta migração.
