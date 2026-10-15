import { ChangeDetectionStrategy, Component, computed, ElementRef, inject, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AlertSummaryService } from '../core/alert-summary.service';
import { EnvironmentDirectoryService } from '../core/environment-directory.service';
import { ALERT_SEVERITY } from '../data-access/alert-severity';
import type { Stage } from '../data-access/generated/control-plane';
import { formatTime } from '../design-system/format/format';
import { DokIcon } from '../design-system/icon/dok-icon';
import { DokStatusChip } from '../design-system/status/dok-status';

/**
 * Sino (spec 016 FR-006): alertas abertos e reconhecidos, não silenciados, do estágio selecionado; até 10,
 * cada um levando ao alerta na tela Alertas. Esc fecha e devolve o foco ao sino.
 */
@Component({
  selector: 'dok-notifications-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon, DokStatusChip, RouterLink],
  host: {
    style: 'position: relative; display: flex',
    '(document:mousedown)': 'onDocumentDown($event)',
    '(document:keydown.escape)': 'onEscape()',
  },
  styles: `
    .dok-notif { width: 380px; padding: 6px; }
    .dok-notif__head { display: flex; align-items: center; justify-content: space-between; padding: 8px 10px; }
    .dok-notif__list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; max-height: 380px; overflow: auto; }
    .dok-notif__item { display: grid; grid-template-columns: 44px minmax(0, 1fr) auto; gap: 10px; align-items: start; padding: 10px; border-top: 1px solid var(--hairline); color: inherit; text-decoration: none; border-radius: var(--radius-sm); }
    .dok-notif__item:hover, .dok-notif__item:focus-visible { background: var(--surface-hover, var(--hairline)); }
    .dok-notif__text { color: var(--foreground); font-size: 13px; overflow: hidden; text-overflow: ellipsis; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
    .dok-notif__where { color: var(--muted); font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: block; }
    .dok-notif__foot { padding: 8px 10px 4px; border-top: 1px solid var(--hairline); }
  `,
  template: `
    <button
      #trigger
      type="button"
      class="alt-app-header__btn alt-app-header__notif"
      aria-haspopup="dialog"
      [attr.aria-expanded]="open()"
      [attr.aria-label]="count() ? 'Alertas abertos, ' + count() : 'Alertas abertos'"
      (click)="toggle()"
    >
      <dok-icon name="bell" [size]="20" />
      @if (count()) {
        <span class="alt-app-header__badge alt-num" aria-hidden="true">{{ count()! > 99 ? '99+' : count() }}</span>
      }
    </button>
    @if (open()) {
      <div #panel class="alt-app-header__menu alt-popover dok-notif" role="dialog" aria-label="Alertas abertos">
        <div class="dok-notif__head">
          <b class="app-small" style="color: var(--text-strong)">Alertas abertos</b>
          <span class="app-caption" style="color: var(--muted)">{{ summary()?.open ?? 0 }} no total</span>
        </div>
        @if (alerts.error() && !summary()) {
          <p class="app-small" role="alert" style="padding: 10px; margin: 0; color: var(--signal-error)">Não foi possível carregar os alertas.</p>
        } @else if (!summary()) {
          <p class="app-small" role="status" style="padding: 10px; margin: 0; color: var(--muted)">Carregando alertas…</p>
        } @else if (items().length) {
          <ul class="dok-notif__list">
            @for (item of items(); track item.id) {
              <li>
                <a class="dok-notif__item" routerLink="/alertas" [queryParams]="{ alerta: item.id }" queryParamsHandling="merge" (click)="close(false)">
                  <span class="app-caption alt-num" style="color: var(--muted)">{{ item.time }}</span>
                  <span style="min-width: 0">
                    <span class="dok-notif__text">{{ item.text }}</span>
                    <span class="dok-notif__where">{{ item.where }}</span>
                  </span>
                  <dok-status-chip [label]="item.label" [color]="item.tone" size="sm" />
                </a>
              </li>
            }
          </ul>
        } @else {
          <p class="app-small" style="padding: 10px; margin: 0; color: var(--muted)">Nenhum alerta aberto.</p>
        }
        <div class="dok-notif__foot">
          <a routerLink="/alertas" queryParamsHandling="merge" class="app-small" (click)="close(false)">Ver todos os alertas</a>
        </div>
      </div>
    }
  `,
})
export class DokNotificationsPanel {
  readonly count = input<number | null>(null);
  readonly stage = input<Stage | null>(null);

  protected readonly alerts = inject(AlertSummaryService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  protected readonly open = signal(false);
  protected readonly summary = this.alerts.summary;
  protected readonly items = computed(() =>
    (this.summary()?.recent ?? []).map(alert => ({
      id: alert.id,
      time: formatTime(alert.lastOccurredAt),
      text: alert.condition,
      where: [this.directory.byId(alert.environmentId)?.name, alert.ruleName].filter(Boolean).join(' · '),
      ...ALERT_SEVERITY[alert.severity],
    })),
  );

  protected toggle(): void {
    if (this.open()) {
      this.close(false);
      return;
    }
    this.open.set(true);
    void this.alerts.refresh(this.stage());
  }

  protected close(restoreFocus: boolean): void {
    this.open.set(false);
    if (restoreFocus) this.trigger().nativeElement.focus();
  }

  protected onEscape(): void {
    if (this.open()) this.close(true);
  }

  protected onDocumentDown(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.open.set(false);
  }
}
