import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { RegistryDetail } from '../../data-access/generated/control-plane';
import { DokStatusChip, formatBytes } from '../../design-system';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { stageLabel } from '../../shell/stage-filter.service';
import { ORIGIN_LABEL } from './registry-labels';

/** Detalhe do registro (spec 020 FR-004): configuração por host, credencial só como sim/não e link para Imagens. */
@Component({
  selector: 'app-registry-detail-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokDrawer, DokStatusChip, RouterLink],
  styles: `
    .rd-hosts { margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; gap: 10px; }
    .rd-host { border: 1px solid var(--hairline); border-radius: var(--radius-md); padding: 10px 12px; display: flex; flex-direction: column; gap: 6px; }
    .rd-head { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .rd-head strong { color: var(--text-strong); }
    .rd-chips { display: flex; flex-wrap: wrap; gap: 6px; }
    .rd-facts { margin: 0; display: grid; grid-template-columns: 130px 1fr; gap: 4px 12px; font-size: 13px; }
    .rd-facts dt { color: var(--muted); }
    .rd-facts dd { margin: 0; overflow-wrap: anywhere; }
    .rd-host a { color: var(--teal-light); font-size: 13px; text-decoration: none; }
    .rd-host a:hover, .rd-host a:focus-visible { text-decoration: underline; }
    .rd-muted { color: var(--muted); margin: 0; }
  `,
  template: `
    <dok-drawer [open]="open()" eyebrow="Registro" [title]="detail().registry" [subtitle]="subtitle()" [chips]="chips()" cancelLabel="Fechar" width="580px" (closed)="closed.emit()">
      <p class="app-caption rd-muted">Credenciais aparecem só como "sim" ou "não": usuário, senha e token ficam somente no host.</p>
      <ul class="rd-hosts" aria-label="Configuração por host">
        @for (host of detail().hosts; track host.environmentId) {
          <li class="rd-host">
            <div class="rd-head">
              <strong>{{ host.environmentName }}</strong>
              <dok-status-chip size="sm" [color]="host.allowed ? 'success' : 'warning'" [label]="host.allowed ? 'Permitido' : 'Fora da política'" />
            </div>
            <div class="rd-chips">
              @for (origin of host.origins; track origin) {
                <dok-status-chip size="sm" variant="outlined" [color]="origin === 'insecure' || origin === 'blocked' ? 'warning' : 'secondary'" [label]="originLabel[origin]" />
              }
            </div>
            <dl class="rd-facts">
              <dt>Estágio</dt><dd>{{ host.stage ? stageName(host.stage) : '—' }}</dd>
              <dt>Credencial no agente</dt><dd>{{ host.credential ? 'Sim' : 'Não' }}</dd>
              <dt>Inseguro</dt><dd>{{ host.insecure ? 'Sim (HTTP ou TLS sem verificação)' : 'Não' }}</dd>
              <dt>Bloqueado</dt><dd>{{ host.blocked ? 'Sim' : 'Não' }}</dd>
              <dt>Espelhos</dt><dd class="alt-num">{{ host.mirrors.length ? host.mirrors.join(', ') : '—' }}</dd>
              @if (host.mirrorOf) {
                <dt>Espelho de</dt><dd>{{ host.mirrorOf }}</dd>
              }
              <dt>Imagens</dt><dd>{{ host.images }} · {{ formatBytes(host.sizeBytes, 1) }}</dd>
            </dl>
            @if (!host.configAvailable) {
              <p class="app-caption rd-muted">Configuração do engine indisponível neste host: registros só pelas imagens.</p>
            }
            @if (host.images > 0) {
              <a routerLink="/imagens" [queryParams]="{ environmentId: host.environmentId, q: detail().registry }">Ver as {{ host.images }} imagens em {{ host.environmentName }}</a>
            }
          </li>
        }
      </ul>
    </dok-drawer>
  `,
})
export class RegistryDetailDrawer {
  readonly open = input(false);
  readonly detail = input.required<RegistryDetail>();
  readonly closed = output<void>();

  protected readonly originLabel = ORIGIN_LABEL;
  protected readonly formatBytes = formatBytes;
  protected readonly stageName = stageLabel;
  protected readonly subtitle = computed(() => {
    const count = this.detail().hosts.length;
    return count === 1 ? 'Em 1 host' : `Em ${count} hosts`;
  });
  protected readonly chips = computed(() => {
    const hosts = this.detail().hosts;
    const chips = [];
    if (hosts.some(h => h.insecure)) chips.push({ tone: 'warning', icon: 'alert', label: 'Inseguro' });
    if (hosts.some(h => !h.allowed)) chips.push({ tone: 'warning', icon: 'shield', label: 'Fora da política' });
    return chips;
  });
}
