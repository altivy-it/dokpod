import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ENGINE_LABEL } from '../../data-access/console-labels';
import type { NetworkItem } from '../../data-access/generated/control-plane';
import { DokKeyValue, formatDateTime, type KeyValueItem } from '../../design-system';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { stageLabel } from '../../shell/stage-filter.service';
import { networkIneligibleReason } from './network-removal.service';

const yesNo = (value: boolean) => (value ? 'Sim' : 'Não');

/** Detalhe da rede (spec 019 FR-004): endereçamento, flags, labels e containers conectados com link e endereços. */
@Component({
  selector: 'app-network-detail-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokDrawer, DokKeyValue, RouterLink],
  styles: `
    h3 { margin: 0; font-size: 13px; font-weight: 600; color: var(--muted); }
    .nd-section { display: flex; flex-direction: column; gap: 8px; }
    .nd-list { margin: 0; padding: 0; list-style: none; border: 1px solid var(--hairline); border-radius: var(--radius-sm); }
    .nd-list li { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 7px 10px; border-top: 1px solid var(--hairline); font-size: 13px; }
    .nd-list li:first-child { border-top: 0; }
    .nd-list a { color: var(--teal-light); font-weight: 500; text-decoration: none; }
    .nd-list a:hover, .nd-list a:focus-visible { text-decoration: underline; }
    .nd-mono { font-family: var(--font-mono, monospace); color: var(--muted); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .nd-muted { color: var(--muted); margin: 0; }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      eyebrow="Rede"
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
        <p class="app-caption nd-muted">Remoção indisponível: {{ removalReason() }}.</p>
      }
      <dok-key-value [items]="facts()" [labelWidth]="110" />

      <section class="nd-section" aria-labelledby="nd-subnets">
        <h3 id="nd-subnets">Endereçamento</h3>
        @if (item().subnets.length) {
          <ul class="nd-list">
            @for (subnet of item().subnets; track subnet.subnet) {
              <li><span class="nd-mono">{{ subnet.subnet }}</span><span class="nd-mono">gateway {{ subnet.gateway || '—' }}</span></li>
            }
          </ul>
        } @else {
          <p class="app-caption nd-muted">Sem sub-rede configurada.</p>
        }
      </section>

      <section class="nd-section" aria-labelledby="nd-containers">
        <h3 id="nd-containers">{{ item().containers.length === 0 ? 'Nenhum container conectado' : item().containers.length === 1 ? '1 container conectado' : item().containers.length + ' containers conectados' }}</h3>
        @if (item().containers.length) {
          <ul class="nd-list">
            @for (container of item().containers; track container.containerId) {
              <li>
                <a [routerLink]="['/containers', item().environmentId, container.containerId]">{{ container.containerName || container.containerId.slice(0, 12) }}</a>
                <span class="nd-mono">{{ address(container.ipv4, container.ipv6) }}</span>
              </li>
            }
          </ul>
          <p class="app-caption nd-muted">Inclui containers parados (sem endereço): a rede continua em uso enquanto o container existir.</p>
        }
        @if (item().truncated) {
          <p class="app-caption nd-muted">Lista truncada pelo agente: a rede tem mais sub-redes, labels ou conexões do que o limite publicado.</p>
        }
      </section>

      <section class="nd-section" aria-labelledby="nd-labels">
        <h3 id="nd-labels">Labels</h3>
        @if (labels().length) {
          <dok-key-value [items]="labels()" [labelWidth]="200" />
        } @else {
          <p class="app-caption nd-muted">Sem labels.</p>
        }
      </section>
    </dok-drawer>
  `,
})
export class NetworkDetailDrawer {
  readonly open = input(false);
  readonly item = input.required<NetworkItem>();
  readonly closed = output<void>();
  readonly remove = output<NetworkItem>();

  protected readonly engineLabel = computed(() => (this.item().engine ? (ENGINE_LABEL[this.item().engine!] ?? this.item().engine) : ''));
  protected readonly removalReason = computed(() => networkIneligibleReason(this.item()) ?? 'indisponível');
  protected readonly chips = computed(() => {
    const item = this.item();
    const chips = [
      item.system
        ? { tone: 'secondary', icon: 'shield', label: 'Do sistema' }
        : { tone: item.inUse ? 'success' : 'warning', icon: item.inUse ? 'link' : 'info', label: item.inUse ? 'Em uso' : 'Sem uso' },
    ];
    if (item.stage === 'production') chips.push({ tone: 'warning', icon: 'shield', label: 'Produção' });
    return chips;
  });
  protected readonly facts = computed<KeyValueItem[]>(() => {
    const item = this.item();
    return [
      { label: 'Host', value: item.environmentName },
      { label: 'Ambiente', value: item.stage ? stageLabel(item.stage) : '—' },
      { label: 'Identificador', value: item.networkId.slice(0, 12), mono: true },
      { label: 'Driver', value: item.driver || '—', mono: true },
      { label: 'Escopo', value: item.scope || '—', mono: true },
      { label: 'Interna', value: yesNo(item.internal) },
      { label: 'IPv6', value: yesNo(item.ipv6) },
      { label: 'Anexável', value: yesNo(item.attachable) },
      { label: 'Criada', value: item.createdAt ? formatDateTime(item.createdAt) : '—' },
      { label: 'Observada', value: formatDateTime(item.observedAt) },
    ];
  });
  protected readonly labels = computed<KeyValueItem[]>(() =>
    Object.entries(this.item().labels)
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([label, value]) => ({ label, value: value || '—', mono: true })),
  );

  protected address(ipv4: string | null | undefined, ipv6: string | null | undefined): string {
    return [ipv4, ipv6].filter(Boolean).join(' · ') || 'sem endereço (parado)';
  }
}
