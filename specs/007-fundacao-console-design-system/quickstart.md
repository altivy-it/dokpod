# Quickstart de Validação: Fundação do Console

Pré-requisito: stack E2E conforme [deploy/e2e/README.md](../../deploy/e2e/README.md); acesso somente pela borda nginx.

1. Build e testes do frontend em container (alias `ng` de `.github/copilot-instructions.md`): lint, typecheck, `ng test`, `ng build`.
2. Subir a stack E2E e abrir o console pela URL do gateway.
3. Sem sessão: verificar tela de entrada no visual novo e "Entrar com Keycloak".
4. Autenticado: percorrer todos os itens do menu por teclado (Tab/Enter), conferir item ativo, breadcrumbs e páginas "Em breve".
5. Antes da spec 008: atalho de estágio desabilitado com explicação; abrir uma rota com `?stage=production`, navegar e recarregar e confirmar o parâmetro preservado.
6. Redimensionar para 1920, 1440, 1100, 390 e 320px: menu recolhível abaixo de 1100px, sem rolagem horizontal; repetir com zoom de 200%.
7. Abrir `/dev/componentes` (somente build de desenvolvimento) e validar cada componente em todos os estados.
8. Executar Playwright legado (ambientes, inventário, lifecycle) e a suíte de acessibilidade.
9. Loop de verificação visual: screenshots 1440px e 390px da moldura, entrada, 404, "Em breve" e catálogo; comparar com os boards do canvas.
