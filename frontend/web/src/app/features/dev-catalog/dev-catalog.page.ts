import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DokButton,
  DokCard,
  DokCheckbox,
  DokDataGrid,
  DokDonutChart,
  DokDrawer,
  DokEmptyState,
  DokEngineBadge,
  DokField,
  DokInfoKpi,
  DokKeyValue,
  DokLineChart,
  DokProgressBar,
  DokSearchField,
  DokSelect,
  DokStatusChip,
  DokStatusCircle,
  DokTabs,
  formatPercent,
  ToastService,
  type GridColumn,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';

interface DemoRow {
  id: string;
  name: string;
  status: string;
  engine: string;
  host: string;
  cpu: number;
  mem: number;
}

/** Catálogo de componentes (somente desenvolvimento), com dados sintéticos do board. */
@Component({
  selector: 'app-dev-catalog-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DokButton,
    DokCard,
    DokCheckbox,
    DokDataGrid,
    DokDonutChart,
    DokDrawer,
    DokEmptyState,
    DokEngineBadge,
    DokField,
    DokInfoKpi,
    DokKeyValue,
    DokLineChart,
    DokProgressBar,
    DokSearchField,
    DokSelect,
    DokStatusChip,
    DokStatusCircle,
    DokTabs,
    FormsModule,
  ],
  templateUrl: './dev-catalog.page.html',
})
export class DevCatalogPage {
  protected readonly toast = inject(ToastService);
  protected readonly drawerOpen = signal(false);
  protected readonly tab = signal('logs');
  protected readonly selection = signal<string[]>(['c09']);
  protected engine = 'podman';
  protected name = 'api-pedidos-b21e';
  protected secret = 'segredo-do-cofre';
  protected healthcheck = true;
  protected readonly pct = (v: number) => formatPercent(v);

  protected readonly tabs = [
    { id: 'summary', label: 'Resumo', icon: 'info' },
    { id: 'logs', label: 'Logs', icon: 'list' },
    { id: 'inspect', label: 'Inspecionar', icon: 'eye' },
    { id: 'stats', label: 'Estatísticas', icon: 'chart-line' },
    { id: 'terminal', label: 'Terminal', icon: 'command', disabled: true },
  ];
  protected readonly engineOptions = [
    { value: 'podman', label: 'Podman 5.2.3', caption: 'rootless', dot: 'var(--chart-5)' },
    { value: 'docker', label: 'Docker 27.3.1', caption: 'daemon', dot: 'var(--chart-2)' },
  ];
  protected readonly hours = ['04h', '05h', '06h', '07h', '08h', '09h', '10h', '11h', '12h', '13h', '14h', '15h'];
  protected readonly series = [
    { name: 'CPU', data: [31, 29, 30, 36, 44, 52, 58, 55, 49, 51, 47, 46.8], area: true },
    { name: 'Memória', data: [54, 54, 55, 57, 59, 62, 64, 63, 61, 62, 61, 61.2] },
  ];
  protected readonly engines = [
    { label: 'Docker', value: 84, color: 'var(--chart-2)' },
    { label: 'Podman', value: 70, color: 'var(--chart-5)' },
  ];
  protected readonly rows: DemoRow[] = [
    { id: 'c01', name: 'api-pedidos-7f9c', status: 'Reiniciando', engine: 'Docker', host: 'srv-prod-02', cpu: 0, mem: 412 },
    { id: 'c02', name: 'gateway-01', status: 'Em execução', engine: 'Docker', host: 'srv-prod-01', cpu: 18.4, mem: 286 },
    { id: 'c09', name: 'worker-relatorios', status: 'Com falha', engine: 'Podman', host: 'srv-batch-01', cpu: 0, mem: 0 },
    { id: 'c15', name: 'migracao-catalogo', status: 'Parado', engine: 'Docker', host: 'srv-prod-02', cpu: 0, mem: 0 },
    { id: 'c16', name: 'debug-rede', status: 'Pausado', engine: 'Podman', host: 'srv-prod-03', cpu: 0, mem: 18 },
  ];
  protected readonly columns: GridColumn<DemoRow>[] = [
    { field: 'name', header: 'Nome', width: 190, key: true },
    { field: 'status', header: 'Situação', type: 'status', width: 140, filterable: true },
    { field: 'engine', header: 'Engine', width: 100, filterable: true },
    { field: 'host', header: 'Host', width: 130, filterable: true },
    { field: 'cpu', header: 'CPU %', type: 'number', decimals: 1, width: 80 },
    { field: 'mem', header: 'Memória MiB', type: 'number', width: 110 },
  ];
  protected readonly tones = { 'Em execução': 'success', Parado: 'neutral', Pausado: 'info', Reiniciando: 'warning', 'Com falha': 'error' };
  protected readonly rowActions = [
    { id: 'logs', label: 'Ver logs', icon: 'list' },
    { id: 'restart', label: 'Reiniciar', icon: 'refresh' },
    { id: 'remove', label: 'Remover', icon: 'trash', danger: true },
  ];
  protected readonly kv = [
    { label: 'Comando', value: 'node dist/server.js --port 80', mono: true },
    { label: 'Usuário', value: 'app (uid 1001)', mono: true },
    { label: 'Health check', value: 'GET /healthz a cada 15 s · 3 tentativas' },
  ];

  constructor() {
    inject(ShellContextService).set('Componentes', ['Desenvolvimento', 'Catálogo de componentes']);
  }
}
