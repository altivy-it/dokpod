import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConsoleRealtimeService, type LiveLogLine } from '../../core/console-realtime.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { LogLineView } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokEmptyState,
  DokSearchField,
  DokSelect,
  DokStatusCircle,
  formatTime,
} from '../../design-system';

const MAX_LINES = 10_000;
const MAX_BYTES = 5 * 1024 * 1024;
const LEVEL_COLOR: Record<string, string> = {
  ERROR: 'var(--signal-error)',
  WARN: 'var(--signal-warning)',
  INFO: 'var(--signal-info)',
  DEBUG: 'var(--muted)',
  TRACE: 'var(--muted)',
};

interface ViewLine {
  id: number;
  time: string;
  level: string;
  text: string;
  stderr: boolean;
  bytes: number;
}

type LogsState = 'loading' | 'ready' | 'forbidden' | 'disconnected' | 'unsupported' | 'error';

/** Aba Logs (spec 011 US4): últimas linhas, filtros, ao vivo com taxa limitada e download do buffer. */
@Component({
  selector: 'app-logs-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokEmptyState, DokSearchField, DokSelect, DokStatusCircle, FormsModule],
  styleUrl: './logs.panel.css',
  template: `
    @switch (state()) {
      @case ('forbidden') {
        <dok-empty-state kind="forbidden" title="Sem permissão" detail="Você não tem o scope container:logs neste host." [compact]="true" />
      }
      @case ('disconnected') {
        <dok-empty-state kind="offline" title="Logs indisponíveis: agente desconectado" detail="Nenhum log é servido de cache." [compact]="true">
          <button dok-button variant="outline" size="xs" startIcon="refresh" type="button" (click)="load()">Tentar novamente</button>
        </dok-empty-state>
      }
      @case ('unsupported') {
        <dok-empty-state kind="empty" icon="info" title="Driver não permite leitura" [detail]="unsupportedDetail()" [compact]="true" />
      }
      @case ('error') {
        <dok-empty-state kind="error" [title]="errorText()" [compact]="true">
          <button dok-button variant="outline" size="xs" startIcon="refresh" type="button" (click)="load()">Tentar novamente</button>
        </dok-empty-state>
      }
      @default {
        <div class="alt-panel lg-panel">
          <div class="lg-toolbar">
            <dok-search-field placeholder="Filtrar logs" ariaLabel="Filtrar logs" width="240px" [(value)]="filter" [debounceMs]="150" />
            <dok-select class="lg-level" ariaLabel="Nível" size="sm" [options]="levelOptions" [(ngModel)]="level" />
            @if (filter().trim().length >= 2 && !live()) {
              <button dok-button variant="text" size="xs" startIcon="search" type="button" (click)="searchHistory()">Buscar no histórico</button>
            }
            <span style="flex: 1"></span>
            @if (omitted()) {
              <span class="app-caption lg-muted" aria-live="polite">Linhas omitidas: {{ omitted() }}</span>
            }
            <button type="button" class="lg-live" [attr.aria-pressed]="live()" (click)="toggleLive()">
              <dok-status-circle [color]="live() ? 'accent' : 'neutral'" [text]="live() ? 'Ao vivo' : 'Pausado'" size="small" [pulse]="live()" />
            </button>
            <button dok-button variant="ghost" size="xs" startIcon="download" type="button" [disabled]="!lines().length" (click)="download()">Baixar</button>
          </div>
          <div #scroller class="ct-mono alt-scroll lg-lines" role="log" aria-label="Linhas de log" [attr.aria-busy]="state() === 'loading'" (scroll)="onScroll()">
            @if (state() === 'loading') {
              @for (i of skeleton; track i) {
                <div class="alt-skel" style="height: 14px; margin: 4px 0" [style.width.%]="40 + ((i * 17) % 55)"></div>
              }
            } @else {
              @for (line of visible(); track line.id) {
                <div class="lg-line">
                  <span class="lg-time">{{ line.time }}</span>
                  <span class="lg-level-tag" [style.color]="levelColor(line.level)">{{ line.level || '—' }}</span>
                  <span class="lg-text" [class.lg-stderr]="line.stderr">{{ line.text }}</span>
                </div>
              } @empty {
                <p class="lg-muted">{{ lines().length ? 'Nenhuma linha corresponde ao filtro.' : 'O container ainda não registrou logs.' }}</p>
              }
            }
          </div>
          <div class="lg-foot app-caption lg-muted">
            <span>{{ lines().length }} linhas{{ truncated() ? ' · trecho truncado no limite' : '' }}{{ dropped() ? ' · buffer limitado a 10.000 linhas' : '' }}</span>
            <span>Segredos conhecidos são mascarados no agente; o mascaramento é best-effort.</span>
          </div>
        </div>
      }
    }
  `,
})
export class LogsPanel {
  readonly environmentId = input.required<string>();
  readonly containerId = input.required<string>();
  readonly containerName = input<string>('container');

  private readonly api = inject(ControlPlaneApiService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');

  protected readonly skeleton = [1, 2, 3, 4, 5, 6, 7, 8];
  protected readonly state = signal<LogsState>('loading');
  protected readonly lines = signal<ViewLine[]>([]);
  protected readonly filter = signal('');
  protected readonly level = signal('');
  protected readonly live = signal(false);
  protected readonly omitted = signal(0);
  protected readonly truncated = signal(false);
  protected readonly dropped = signal(false);
  protected readonly errorText = signal('Não foi possível ler os logs');
  protected readonly unsupportedDetail = signal('O driver de log configurado não permite leitura pelo engine.');
  protected readonly levelOptions = [
    { value: '', label: 'Todos os níveis' },
    { value: 'ERROR', label: 'Erro', dot: 'var(--signal-error)' },
    { value: 'WARN', label: 'Aviso', dot: 'var(--signal-warning)' },
    { value: 'INFO', label: 'Info', dot: 'var(--signal-info)' },
    { value: 'DEBUG', label: 'Debug', dot: 'var(--muted)' },
  ];

  private sequence = 0;
  private bytes = 0;
  private followBottom = true;
  private subscription: { dispose: () => void } | null = null;

  protected readonly visible = computed(() => {
    const query = this.filter().trim().toLowerCase();
    const level = this.level();
    return this.lines().filter(line => (!level || line.level === level) && (!query || line.text.toLowerCase().includes(query)));
  });

  constructor() {
    effect(() => {
      this.environmentId();
      this.containerId();
      untracked(() => void this.load());
    });
    inject(DestroyRef).onDestroy(() => this.stopLive());
  }

  async load(contains?: string): Promise<void> {
    this.stopLive();
    this.state.set('loading');
    try {
      const page = await this.api.getContainerLogs(this.environmentId(), this.containerId(), {
        tail: 500,
        ...(contains ? { contains } : {}),
      });
      this.bytes = 0;
      this.lines.set([]);
      this.append(page.lines);
      this.truncated.set(page.truncated);
      this.omitted.set(page.omittedLines);
      this.state.set('ready');
      this.followBottom = true;
      queueMicrotask(() => this.scrollToEnd());
    } catch (error: unknown) {
      this.setError(error);
    }
  }

  protected searchHistory(): void {
    void this.load(this.filter().trim());
  }

  protected async toggleLive(): Promise<void> {
    if (this.live()) {
      this.stopLive();
      return;
    }

    this.live.set(true);
    this.omitted.set(0);
    this.subscription = await this.realtime.streamLogs(this.environmentId(), this.containerId(), null, {
      next: chunk => {
        this.append(chunk.lines);
        if (chunk.omittedLines) this.omitted.update(value => value + chunk.omittedLines);
        if (chunk.ended) this.live.set(false);
        if (this.followBottom) queueMicrotask(() => this.scrollToEnd());
      },
      error: () => this.live.set(false),
      complete: () => this.live.set(false),
    });
    if (!this.subscription) this.live.set(false);
  }

  protected onScroll(): void {
    const el = this.scroller()?.nativeElement;
    if (el) this.followBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 24;
  }

  protected download(): void {
    const content = this.lines().map(line => `${line.time}\t${line.level || '-'}\t${line.text}`).join('\n');
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-');
    link.href = url;
    link.download = `${this.containerName()}-${stamp}.log`;
    link.click();
    URL.revokeObjectURL(url);
  }

  protected levelColor(level: string): string {
    return LEVEL_COLOR[level] ?? 'var(--muted)';
  }

  private append(incoming: (LogLineView | LiveLogLine)[]): void {
    if (!incoming.length) return;
    const mapped = incoming.map<ViewLine>(line => {
      const text = line.text ?? '';
      return {
        id: ++this.sequence,
        time: line.timestamp ? formatTime(line.timestamp, true) : '—',
        level: line.level ?? '',
        text,
        stderr: line.stream === 'stderr',
        bytes: text.length + 24,
      };
    });
    this.bytes += mapped.reduce((sum, line) => sum + line.bytes, 0);
    let next = [...this.lines(), ...mapped];
    while (next.length > MAX_LINES || this.bytes > MAX_BYTES) {
      const removed = next.shift();
      if (!removed) break;
      this.bytes -= removed.bytes;
      this.dropped.set(true);
    }
    this.lines.set(next);
  }

  private stopLive(): void {
    this.subscription?.dispose();
    this.subscription = null;
    this.live.set(false);
  }

  private scrollToEnd(): void {
    const el = this.scroller()?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  }

  private setError(error: unknown): void {
    const status = (error as { status?: number }).status;
    const code = (error as { code?: string }).code;
    if (status === 403) this.state.set('forbidden');
    else if (status === 409) this.state.set('disconnected');
    else if (status === 422) {
      if (code === 'not_supported') this.unsupportedDetail.set('O agente deste host não suporta leitura de logs.');
      this.state.set('unsupported');
    } else {
      this.errorText.set(status === 504 ? 'O agente não respondeu no prazo' : status === 429 ? 'Muitas consultas neste host; tente novamente' : 'Não foi possível ler os logs');
      this.state.set('error');
    }
  }
}
