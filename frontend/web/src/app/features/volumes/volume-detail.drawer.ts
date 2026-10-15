import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ENGINE_LABEL } from '../../data-access/console-labels';
import type { VolumeItem, VolumeUsage } from '../../data-access/generated/control-plane';
import { DokButton, DokKeyValue, DokStatusChip, formatBytes, formatDateTime, type KeyValueItem } from '../../design-system';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { stageLabel } from '../../shell/stage-filter.service';
import { volumeIneligibleReason } from './volume-removal.service';
import { VolumesApiService } from './volumes-api.service';

const USAGE_FAILURE: Record<string, string> = {
  agent_disconnected: 'O agente do host está desconectado.',
  not_supported: 'O agente deste host não informa o tamanho de volumes.',
  too_many_requests: 'Muitas consultas ao agente; tente de novo em instantes.',
};

/** Detalhe do volume (spec 018 FR-004): montagens com link ao container e tamanho consultado sob demanda. */
@Component({
  selector: 'app-volume-detail-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokDrawer, DokKeyValue, DokStatusChip, RouterLink],
  styles: `
    h3 { margin: 0; font-size: 13px; font-weight: 600; color: var(--muted); }
    .vd-section { display: flex; flex-direction: column; gap: 8px; }
    .vd-list { margin: 0; padding: 0; list-style: none; border: 1px solid var(--hairline); border-radius: var(--radius-sm); }
    .vd-list li { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 7px 10px; border-top: 1px solid var(--hairline); font-size: 13px; }
    .vd-list li:first-child { border-top: 0; }
    .vd-list a { color: var(--teal-light); font-weight: 500; text-decoration: none; }
    .vd-list a:hover, .vd-list a:focus-visible { text-decoration: underline; }
    .vd-mono { font-family: var(--font-mono, monospace); color: var(--muted); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .vd-muted { color: var(--muted); margin: 0; }
    .vd-usage { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      eyebrow="Volume"
      [title]="item().name"
      [subtitle]="item().environmentName + (item().engine ? ' · ' + engineLabel() : '')"
      [chips]="chips()"
      [primaryLabel]="item().canRemove ? 'Remover' : null"
      primaryIcon="trash"
      primaryVariant="danger"
      cancelLabel="Fechar"
      width="560px"
      (closed)="closed.emit()"
      (primary)="remove.emit(item())"
    >
      @if (!item().canRemove) {
        <p class="app-caption vd-muted">Remoção indisponível: {{ removalReason() }}.</p>
      }
      <dok-key-value [items]="facts()" [labelWidth]="110" />

      <section class="vd-section" aria-labelledby="vd-mounts">
        <h3 id="vd-mounts">{{ item().mounts.length === 0 ? 'Nenhum container monta este volume' : item().mounts.length === 1 ? '1 container monta este volume' : item().mounts.length + ' containers montam este volume' }}</h3>
        @if (item().mounts.length) {
          <ul class="vd-list">
            @for (mount of item().mounts; track mount.containerId + mount.destination) {
              <li>
                <a [routerLink]="['/containers', item().environmentId, mount.containerId]">{{ mount.containerName || mount.containerId.slice(0, 12) }}</a>
                <span class="vd-mono" [title]="mount.destination">{{ mount.destination }}{{ mount.readOnly ? ' (somente leitura)' : '' }}</span>
              </li>
            }
          </ul>
          <p class="app-caption vd-muted">Inclui containers parados: o volume continua em uso enquanto o container existir.</p>
        }
        @if (item().truncated) {
          <p class="app-caption vd-muted">Lista truncada pelo agente: o volume tem mais labels ou montagens do que o limite publicado.</p>
        }
      </section>

      <section class="vd-section" aria-labelledby="vd-labels">
        <h3 id="vd-labels">Labels</h3>
        @if (labels().length) {
          <dok-key-value [items]="labels()" [labelWidth]="200" />
        } @else {
          <p class="app-caption vd-muted">Sem labels.</p>
        }
      </section>

      <section class="vd-section" aria-labelledby="vd-size">
        <h3 id="vd-size">Tamanho</h3>
        <div class="vd-usage" aria-live="polite">
          @if (usage(); as u) {
            <span class="alt-num">{{ u.sizeBytes != null ? formatBytes(u.sizeBytes, 1) : 'não informado pelo engine' }}</span>
            @if (u.refCount != null) {
              <dok-status-chip size="sm" variant="outlined" color="secondary" [label]="u.refCount + (u.refCount === 1 ? ' referência' : ' referências')" />
            }
          } @else if (usageError(); as e) {
            <span class="app-small vd-muted">{{ e }}</span>
          } @else {
            <span class="app-small vd-muted">Consultado ao agente só quando solicitado.</span>
          }
          <button dok-button variant="outline" size="xs" startIcon="refresh" type="button" [loading]="usageLoading()" [disabled]="!item().hostConnected" (click)="loadUsage()">
            {{ usage() ? 'Consultar de novo' : 'Consultar tamanho' }}
          </button>
        </div>
      </section>
    </dok-drawer>
  `,
})
export class VolumeDetailDrawer {
  readonly open = input(false);
  readonly item = input.required<VolumeItem>();
  readonly closed = output<void>();
  readonly remove = output<VolumeItem>();

  private readonly api = inject(VolumesApiService);
  protected readonly formatBytes = formatBytes;
  protected readonly usage = signal<VolumeUsage | null>(null);
  protected readonly usageError = signal<string | null>(null);
  protected readonly usageLoading = signal(false);

  protected readonly engineLabel = computed(() => (this.item().engine ? (ENGINE_LABEL[this.item().engine!] ?? this.item().engine) : ''));
  protected readonly removalReason = computed(() => volumeIneligibleReason(this.item()) ?? 'indisponível');
  protected readonly chips = computed(() => {
    const item = this.item();
    const chips = [{ tone: item.inUse ? 'success' : 'warning', icon: item.inUse ? 'link' : 'info', label: item.inUse ? 'Em uso' : 'Sem uso' }];
    if (item.stage === 'production') chips.push({ tone: 'warning', icon: 'shield', label: 'Produção' });
    return chips;
  });
  protected readonly facts = computed<KeyValueItem[]>(() => {
    const item = this.item();
    return [
      { label: 'Host', value: item.environmentName },
      { label: 'Ambiente', value: item.stage ? stageLabel(item.stage) : '—' },
      { label: 'Tipo', value: item.anonymous ? 'Anônimo' : 'Nomeado' },
      { label: 'Driver', value: item.driver || '—', mono: true },
      { label: 'Escopo', value: item.scope || '—', mono: true },
      { label: 'Criado', value: item.createdAt ? formatDateTime(item.createdAt) : '—' },
      { label: 'Observado', value: formatDateTime(item.observedAt) },
    ];
  });
  protected readonly labels = computed<KeyValueItem[]>(() =>
    Object.entries(this.item().labels)
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([label, value]) => ({ label, value: value || '—', mono: true })),
  );

  constructor() {
    effect(() => {
      this.item().environmentId;
      this.item().name;
      untracked(() => {
        this.usage.set(null);
        this.usageError.set(null);
      });
    });
  }

  protected async loadUsage(): Promise<void> {
    const item = this.item();
    this.usageLoading.set(true);
    this.usageError.set(null);
    try {
      this.usage.set(await this.api.getVolumeUsage(item.environmentId, item.name));
    } catch (error: unknown) {
      const code = (error as { code?: string } | null)?.code;
      this.usage.set(null);
      this.usageError.set((code && USAGE_FAILURE[code]) ?? 'O agente não respondeu a tempo. Tente de novo.');
    } finally {
      this.usageLoading.set(false);
    }
  }
}
