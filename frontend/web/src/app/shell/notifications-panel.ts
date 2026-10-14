import { ChangeDetectionStrategy, Component, ElementRef, inject, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { describeEvent, type EventView } from '../data-access/console-labels';
import { ControlPlaneApiService } from '../data-access/control-plane-api.service';
import type { Stage } from '../data-access/generated/control-plane';
import { formatTime } from '../design-system/format/format';
import { DokIcon } from '../design-system/icon/dok-icon';
import { DokStatusChip } from '../design-system/status/dok-status';

interface NotificationItem extends EventView {
  id: string;
  time: string;
}

/**
 * Sino de notificações (spec 008 T021): abre um painel com os eventos que pedem atenção
 * (aviso/erro) das últimas 24 h, carregados ao abrir; Esc fecha e devolve o foco ao sino.
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
    .dok-notif { width: 360px; padding: 6px; }
    .dok-notif__head { display: flex; align-items: center; justify-content: space-between; padding: 8px 10px; }
    .dok-notif__list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; max-height: 360px; overflow: auto; }
    .dok-notif__item { display: grid; grid-template-columns: 44px minmax(0, 1fr) auto; gap: 10px; align-items: start; padding: 10px; border-top: 1px solid var(--hairline); }
    .dok-notif__text { color: var(--foreground); font-size: 13px; overflow: hidden; text-overflow: ellipsis; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
    .dok-notif__where { color: var(--muted); font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .dok-notif__foot { padding: 8px 10px 4px; border-top: 1px solid var(--hairline); }
  `,
  template: `
    <button
      #trigger
      type="button"
      class="alt-app-header__btn alt-app-header__notif"
      aria-haspopup="dialog"
      [attr.aria-expanded]="open()"
      [attr.aria-label]="count() ? 'Eventos que pedem atenção, ' + count() + ' nas últimas 24 horas' : 'Eventos que pedem atenção'"
      (click)="toggle()"
    >
      <dok-icon name="bell" [size]="20" />
      @if (count()) {
        <span class="alt-app-header__badge alt-num" aria-hidden="true">{{ count()! > 99 ? '99+' : count() }}</span>
      }
    </button>
    @if (open()) {
      <div #panel class="alt-app-header__menu alt-popover dok-notif" role="dialog" aria-label="Eventos que pedem atenção">
        <div class="dok-notif__head">
          <b class="app-small" style="color: var(--text-strong)">Eventos que pedem atenção</b>
          <span class="app-caption" style="color: var(--muted)">últimas 24 h</span>
        </div>
        @switch (state()) {
          @case ('loading') {
            <p class="app-small" role="status" style="padding: 10px; margin: 0; color: var(--muted)">Carregando eventos…</p>
          }
          @case ('error') {
            <p class="app-small" role="alert" style="padding: 10px; margin: 0; color: var(--signal-error)">Não foi possível carregar os eventos.</p>
          }
          @default {
            @if (items().length) {
              <ul class="dok-notif__list">
                @for (item of items(); track item.id) {
                  <li class="dok-notif__item">
                    <span class="app-caption alt-num" style="color: var(--muted)">{{ item.time }}</span>
                    <span style="min-width: 0">
                      <span class="dok-notif__text">{{ item.text }}</span>
                      <span class="dok-notif__where">{{ item.where }}</span>
                    </span>
                    <dok-status-chip [label]="item.label" [color]="item.tone" size="sm" />
                  </li>
                }
              </ul>
            } @else {
              <p class="app-small" style="padding: 10px; margin: 0; color: var(--muted)">Nenhum evento que peça atenção nas últimas 24 h.</p>
            }
          }
        }
        <div class="dok-notif__foot">
          <a routerLink="/" queryParamsHandling="merge" class="app-small" (click)="close(false)">Ver eventos na visão geral</a>
        </div>
      </div>
    }
  `,
})
export class DokNotificationsPanel {
  readonly count = input<number | null>(null);
  readonly stage = input<Stage | null>(null);

  private readonly api = inject(ControlPlaneApiService);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  protected readonly open = signal(false);
  protected readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  protected readonly items = signal<NotificationItem[]>([]);

  protected toggle(): void {
    if (this.open()) {
      this.close(false);
      return;
    }
    this.open.set(true);
    void this.load();
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

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      const since = new Date(Date.now() - 24 * 3600 * 1000).toISOString();
      const page = await this.api.listEvents({ since, limit: 50, ...(this.stage() ? { stage: this.stage()! } : {}) });
      this.items.set(
        page.events
          .filter(event => event.severity === 'warning' || event.severity === 'error')
          .slice(0, 8)
          .map(event => ({ ...describeEvent(event), id: event.eventId, time: formatTime(event.occurredAt) })),
      );
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }
}
