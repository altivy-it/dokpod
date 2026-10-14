# Contrato: Componentes do Design System Dokpod

Contrato público dos componentes standalone (`dok-*`). Inputs usam `input()`/`model()` do Angular; eventos usam `output()`. Referência visual entre parênteses.

| Componente | Inputs principais | Outputs | Acessibilidade |
| --- | --- | --- | --- |
| `dok-icon` (Altivy.Icon) | `name`, `size` (14-24), `strokeWidth` | — | `aria-hidden` por padrão; `label` torna-o `role="img"`. |
| `dok-button` (Button) | `variant` (`primary`, `secondary`, `outline`, `ghost`, `text`, `danger`), `size` (`xs`, `sm`, `md`), `startIcon`, `icon` (somente ícone), `loading`, `disabled`, `routerLink`/`href` | `pressed` | Botão só-ícone exige `ariaLabel`; `aria-busy` em loading. |
| `dok-status-chip` (StatusChip) | `label`, `color` (`success`, `warning`, `error`, `info`, `neutral`, `accent`), `variant` (`soft`, `outlined`, `filter`), `field`, `icon`, `size` | `removed` (variante filter) | Remoção com nome "Remover filtro {field}". |
| `dok-status-circle` (StatusCircle) | `color`, `text`, `size`, `pulse` | — | Texto sempre presente; pulso respeita movimento reduzido. |
| `dok-info-kpi` (InfoKPI) | `value`, `decimals`, `unit`, `description`, `icon`, `color`, `loading` | — | Valor + descrição lidos juntos; skeleton com `aria-busy`. |
| `dok-card` (CardBase) | `title`, `color`, `statusLabel`; slot `actions` | — | `section` com `aria-labelledby`. |
| `dok-line-chart` (Graphics line) | `title`, `subtitle`, `x`, `series[]` (`name`, `data`, `area`), `reference` (`value`, `label`), `yMin`, `yMax`, `format`, `height`, `note` | — | Tabela alternativa oculta; tooltip acessível por teclado. |
| `dok-donut-chart` (Graphics donut) | `title`, `subtitle`, `items[]` (`label`, `value`, `color`), `center`, `centerLabel`, `size` | — | Legenda textual com valores. |
| `dok-data-grid` (EnhancedDataGrid) | `title`, `entityLabel` (singular/plural), `rows`, `columns[]`, `statusTones`, `rowActions[]`, `selection`, `sort`, `density`, `searchPlaceholder`, `updatedAt`, `exportName`, `loading`, `error`, `hasMore` | `selectionChange`, `sortChange`, `searchChange`, `filterChange`, `rowAction`, `rowOpen`, `loadMore` | `table` nativa, `aria-sort`, checkbox com rótulo da linha, anúncio "N containers selecionados". |
| `dok-tabs` (Tabs) | `tabs[]` (`label`, `icon`, `id`), `value`, `variant` | `valueChange` | `tablist`/`tab`/`tabpanel`, setas e Home/End. |
| `dok-search-field` (CampoBusca) | `label`, `placeholder`, `size`, `width`, `debounceMs` | `valueChange` | `type="search"`, rótulo visível ou `aria-label`. |
| `dok-field` (Campo) | `label`, `ariaLabel`, `value`, `mono`, `secret`, `suffix`, `help`, `error`, `size`, `required`, `type` | ControlValueAccessor | `aria-describedby` para ajuda/erro; revelar segredo anunciado. |
| `dok-select` (Seleção) | `label`, `ariaLabel`, `options[]` (`value`, `label`, `caption`, `dot`), `size` | ControlValueAccessor | Padrão listbox do board `Selecao.dc.html`. |
| `dok-checkbox` (Marcador) | `label`, `help`, `disabled` | ControlValueAccessor | `input type="checkbox"` nativo estilizado. |
| `dok-drawer` (CorporateDrawer) | `open`, `width`, `eyebrow`, `title`, `subtitle`, `chips[]`, `primaryLabel`, `cancelLabel`, `primaryLoading`, `primaryDisabled` | `closed`, `primary` | `role="dialog"`, `aria-modal`, foco preso, Esc fecha, foco devolvido ao gatilho. |
| `dok-empty-state` (EmptyStates) | `kind` (`empty`, `error`, `forbidden`, `offline`, `stale`, `loading`), `title`, `detail`; slot de ação | — | `role="status"` ou `alert` conforme gravidade. |
| `dok-toast` + `ToastService` (Toast) | `notify({ tone, title, detail })` | — | Região viva `polite`/`assertive`. |
| `dok-progress-bar` (`ct-bar`) | `value` (0-100), `warnAt`, `label` | — | `role="progressbar"` com `aria-valuenow`. |
| `dok-key-value` (`ct-kv`) | `items[]` (`label`, `value`, `mono`) | — | `dl`/`dt`/`dd`. |
| `dok-engine-badge` (`ct-engine`) | `engine` (`docker`, `podman`), `version` | — | Texto com engine e versão. |
| `dok-app-shell` (AppShell) | `menu`, `title`, `breadcrumbs`, `user`, `stages`, `notifications` | `stageChange`, `notificationsOpen`, `signOut` | `nav` com `aria-current="page"`, link "Pular para o conteúdo". |
