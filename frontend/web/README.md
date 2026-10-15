# Console web do Dokpod

Console operacional em Angular 22 (componentes standalone, Signals, design system `dok-*`). Arquitetura, convenções e regras de sessão estão em [docs/frontend.md](../../docs/frontend.md); os mockups em `docs/design/app` são a referência visual.

Requisitos: Node `>=24.15.0 <25` e npm `12.0.2` (ver `package.json`).

## Servidor de desenvolvimento

```bash
npm ci
npm start
```

Abra `http://localhost:4200/`. A aplicação recarrega a cada alteração nos arquivos-fonte. A sessão é mediada pelo BFF: sem a stack (nginx, BFF, API e Keycloak) as chamadas autenticadas falham; para validar a interface sem a stack use a suíte E2E simulada abaixo.

## Cliente da API

O cliente HTTP é gerado a partir de `contracts/openapi`:

```bash
npm run generate:api        # regenera o cliente
npm run check:api-contract  # regenera o cliente e falha se ele diferir do versionado (mesma verificação da CI)
```

## Build

```bash
npm run build
```

O resultado fica em `dist/`. O build de produção é o padrão e aplica os orçamentos de tamanho definidos em `angular.json`.

## Testes unitários

Os testes usam [Vitest](https://vitest.dev/):

```bash
npx ng test --watch=false
```

## Testes ponta a ponta

Os testes E2E ficam em `frontend/tests/e2e` (Playwright com pytest e verificação de acessibilidade com axe) e acessam a aplicação sempre pela borda nginx:

```bash
frontend/tests/e2e/mock/run-mock-e2e.sh            # suíte com respostas simuladas da API
frontend/tests/e2e/mock/run-mock-e2e.sh -- -m visual  # capturas para o loop visual
```

A suíte simulada não substitui a E2E autenticada com a stack real; resultados sem a stack real são registrados como `NOT RUN` nas tarefas das specs. Detalhes em [frontend/tests/e2e/README.md](../tests/e2e/README.md).
