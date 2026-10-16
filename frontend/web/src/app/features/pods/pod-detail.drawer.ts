import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { PodAction, PodItem } from '../../data-access/generated/control-plane';
import { DokButton, DokKeyValue, DokStatusChip, formatDateTime, type KeyValueItem } from '../../design-system';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { stageLabel } from '../../shell/stage-filter.service';
import { POD_ACTION, POD_STATUS } from './pod-labels';

/** Detalhe do pod (spec 021 FR-004): containers com situação, reinícios e link; ações permitidas. */
@Component({
  selector: 'app-pod-detail-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokDrawer, DokKeyValue, DokStatusChip, RouterLink],
  styles: `
    h3 { margin: 0; font-size: 13px; font-weight: 600; color: var(--muted); }
    .pd-section { display: flex; flex-direction: column; gap: 8px; }
    .pd-list { margin: 0; padding: 0; list-style: none; border: 1px solid var(--hairline); border-radius: var(--radius-sm); }
    .pd-list li { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 7px 10px; border-top: 1px solid var(--hairline); font-size: 13px; }
    .pd-list li:first-child { border-top: 0; }
    .pd-list a { color: var(--teal-light); font-weight: 500; text-decoration: none; }
    .pd-list a:hover, .pd-list a:focus-visible { text-decoration: underline; }
    .pd-meta { color: var(--muted); display: flex; gap: 8px; align-items: center; }
    .pd-muted { color: var(--muted); margin: 0; }
    .pd-actions { display: flex; flex-wrap: wrap; gap: 8px; }
  `,
  template: `
    <dok-drawer [open]="open()" eyebrow="Pod" [title]="item().name" [subtitle]="item().environmentName" [chips]="chips()" cancelLabel="Fechar" width="560px" (closed)="closed.emit()">
      @if (item().agentPod) {
        <p class="app-caption pd-muted">Pod do agente: o console não para, pausa nem reinicia este pod, porque o host ficaria sem agente.</p>
      }
      @if (item().allowedActions.length) {
        <div class="pd-actions" role="group" aria-label="Ações no pod">
          @for (action of item().allowedActions; track action) {
            <button dok-button variant="outline" size="sm" [startIcon]="actions[action].icon" type="button" (click)="act.emit(action)">{{ actions[action].verb }}</button>
          }
        </div>
      }
      <dok-key-value [items]="facts()" [labelWidth]="110" />
      <section class="pd-section" aria-labelledby="pd-containers">
        <h3 id="pd-containers">{{ item().runningContainers }} de {{ members().length }} containers em execução</h3>
        <ul class="pd-list">
          @for (member of item().containers; track member.containerId) {
            <li>
              <a [routerLink]="['/containers', item().environmentId, member.containerId]">{{ member.name || member.containerId.slice(0, 12) }}</a>
              <span class="pd-meta">
                @if (member.infra) {
                  <span>infraestrutura</span>
                }
                @if (member.restarts) {
                  <span>{{ member.restarts }} reinícios</span>
                }
                <dok-status-chip size="sm" [color]="member.status.startsWith('running') ? 'success' : member.status.startsWith('paused') ? 'info' : 'neutral'" [label]="memberStatus(member.status)" />
              </span>
            </li>
          }
        </ul>
      </section>
    </dok-drawer>
  `,
})
export class PodDetailDrawer {
  readonly open = input(false);
  readonly item = input.required<PodItem>();
  readonly closed = output<void>();
  readonly act = output<PodAction>();

  protected readonly actions = POD_ACTION;
  protected readonly members = computed(() => this.item().containers.filter(c => !c.infra));
  protected readonly chips = computed(() => {
    const item = this.item();
    const chips = [{ tone: POD_STATUS[item.status].tone, icon: 'grid', label: POD_STATUS[item.status].label }];
    if (item.agentPod) chips.push({ tone: 'secondary', icon: 'shield', label: 'Pod do agente' });
    if (item.stage === 'production') chips.push({ tone: 'warning', icon: 'shield', label: 'Produção' });
    return chips;
  });
  protected readonly facts = computed<KeyValueItem[]>(() => {
    const item = this.item();
    const labels = Object.entries(item.labels).map(([k, v]) => `${k}=${v}`);
    return [
      { label: 'Host', value: item.environmentName },
      { label: 'Ambiente', value: item.stage ? stageLabel(item.stage) : '—' },
      { label: 'Identificador', value: item.podId.slice(0, 12), mono: true },
      { label: 'Redes', value: item.networks.join(', ') || '—', mono: true },
      { label: 'Infraestrutura', value: item.infraId ? item.infraId.slice(0, 12) : 'sem container de infraestrutura', mono: !!item.infraId },
      { label: 'Criado', value: item.createdAt ? formatDateTime(item.createdAt) : '—' },
      { label: 'Labels', value: labels.join(', ') || '—', mono: labels.length > 0 },
    ];
  });

  protected memberStatus(status: string): string {
    if (status.startsWith('running')) return 'Em execução';
    if (status.startsWith('paused')) return 'Pausado';
    if (status.startsWith('created')) return 'Criado';
    return 'Parado';
  }
}
