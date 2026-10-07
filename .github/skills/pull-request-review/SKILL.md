---
name: pull-request-review
description: "Executa revisão read-only rastreável ao Spec Kit de branch, diff ou PR do Dokpod, com gates de segurança, contratos, engines e testes."
argument-hint: "PR, branch, diff e tarefa/remediação Spec Kit relacionada"
---

# Pull Request Review

Esta skill é um gate auxiliar do Spec Kit, não um processo de implementação ou
correção. Trabalhe somente com leitura, busca e evidências sanitizadas existentes;
não edite arquivos nem execute comandos, testes, builds, migrações ou operações
mutantes. Validações ausentes são recomendações ao responsável autorizado.

## 1. Preparar contexto

1. Leia spec, plan e tarefa Spec Kit, ou assessment/remediação oficial Bug Fixing, autorização humana, Issue auxiliar e ADR citados. Sinalize ausência de rastreabilidade ou escopo excedido; a Issue não autoriza execução.
2. Determine base/head e leia o diff completo, inclusive contratos, migrations e arquivos gerados.
3. Liste áreas afetadas: frontend, backend, protocolo, engines, dados, segurança, deployment e docs.
4. Carregue as instruções correspondentes.

## 2. Entender comportamento

1. Trace call sites e consumidores alterados entre browser, BFF, API, agente e engine.
2. Compare comportamento anterior e proposto.
3. Identifique invariantes de autorização, identidade, comandos, reconciliação e auditoria.
4. Verifique compatibilidade de API, Protobuf N/N-1, schema, configuração, imagem e plataforma.

## 3. Revisar por dimensão

- **Correção:** critérios completos; erros, cancelamento, concorrência, idempotência e bordas tratados.
- **Arquitetura:** dependências respeitam limites; regras estão no módulo proprietário; complexidade nova é justificada.
- **Segurança:** autorização precede revelação; mTLS, allowlists, sockets, secrets e operações destrutivas estão protegidos.
- **Dados/operação:** migrations são compatíveis; projeções reconciliam; auditoria, health, rollout e rollback existem.
- **Engines/protocolo:** capabilities não presumem equivalência; N/N-1 e tags reservadas são verificados; journal-before-ack, fencing, replay durável e reconexão são seguros.
- **Frontend:** contrato, estado, UX de erro, acessibilidade e responsividade permanecem alinhados.
- **Testes:** cada risco tem evidência no nível correto; engines e PostgreSQL reais são usados quando necessário; a matriz Docker/Podman x Linux/Windows distingue suporte, execução e lacunas, incluindo agente Linux containerizado e Windows Service sem runtime .NET.

## 4. Confirmar e relatar

Um achado deve ter localização, cenário válido, impacto e correção plausível. Inspecione evidências de validação estreita quando puderem confirmar o risco, sem executar checks. Não relate estilo coberto por formatter/linter nem preferência sem violação concreta.

Liste achados por `Crítica`, `Alta`, `Média`, `Baixa`, seguidos de dúvidas, lacunas de teste, risco residual e resumo curto. Vincule achados às tarefas/remediações de origem; correções retornam ao processo oficial, sem criar tarefas ou remediar por conta própria. Se não houver achados, declare explicitamente e indique o que não foi validado.