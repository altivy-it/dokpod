import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AUDIT_ACTOR_KIND, AUDIT_CATEGORIES, AUDIT_DETAIL, AUDIT_OUTCOME, auditActionLabel, auditActorLabel } from '../../data-access/audit-labels';
import { AuditApiService } from './audit-api.service';
import type { AuditEventDetail, AuditEventView } from '../../data-access/generated/control-plane';
import { DokDrawer, formatDateTime } from '../../design-system';

const CONTAINER_TARGET = /^container\.(start|stop|restart|delete|pause|unpause|inspected|logs\.|exec\.)/;

/** Link para o alvo quando ainda faz sentido navegar (container, alerta); senão o identificador registrado. */
export function targetLink(event: AuditEventView): { route: string[]; query: Record<string, string> } | null {
  if (!event.subjectId) return null;
  if (CONTAINER_TARGET.test(event.action) && /^[0-9a-f]{12,64}$/i.test(event.subjectId)) {
    return { route: ['/containers', event.environmentId, event.subjectId], query: {} };
  }
  if (event.action.startsWith('alert.') && event.details?.['alertId']) {
    return { route: ['/alertas'], query: { alerta: event.details['alertId'] } };
  }
  return null;
}

/**
 * Detalhe do evento de auditoria (spec 017 US3): todos os campos como gravados (detalhes já mascarados na origem)
 * e os eventos relacionados pela mesma correlação ou comando, navegáveis.
 */
@Component({
  selector: 'app-audit-event-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokDrawer, RouterLink],
  styles: `
    .ae-facts { display: grid; grid-template-columns: 150px minmax(0, 1fr); gap: 6px 12px; margin: 0; font-size: 13px; }
    .ae-facts dt { color: var(--muted); }
    .ae-facts dd { margin: 0; color: var(--foreground); overflow-wrap: anywhere; }
    .ae-mono { font-family: var(--font-mono, monospace); font-size: 12px; }
    h3 { margin: 0; font-size: 13px; font-weight: 600; color: var(--muted); }
    .ae-related { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; border: 1px solid var(--hairline); border-radius: var(--radius-sm); }
    .ae-related li { border-top: 1px solid var(--hairline); }
    .ae-related li:first-child { border-top: 0; }
    .ae-related button { all: unset; box-sizing: border-box; width: 100%; display: grid; grid-template-columns: 130px minmax(0, 1fr) auto; gap: 10px; padding: 8px 10px; cursor: pointer; font-size: 13px; }
    .ae-related button:hover, .ae-related button:focus-visible { background: var(--surface-hover, var(--hairline)); }
    .ae-related button:focus-visible { outline: 2px solid var(--teal-light); outline-offset: -2px; }
    .ae-muted { color: var(--muted); }
    .ae-error { margin: 0; color: var(--signal-error); font-size: 13px; }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      eyebrow="Evento de auditoria"
      [title]="event() ? actionLabel(event()!.action) : 'Carregando…'"
      [subtitle]="subtitle()"
      [chips]="chips()"
      cancelLabel="Fechar"
      width="600px"
      (closed)="closed.emit()"
    >
      @if (failed()) {
        <p class="ae-error" role="alert">Evento não encontrado. Ele pode estar fora dos últimos 90 dias ou de um host que você não pode auditar.</p>
      }
      @if (event(); as e) {
        <dl class="ae-facts">
          <dt>Horário</dt>
          <dd class="alt-num">{{ format(e.occurredAt) }} <span class="ae-muted">({{ e.occurredAt }} UTC)</span></dd>
          <dt>Ator</dt>
          <dd>{{ actorLabel(e) }}@if (e.actorKind === 'user' && e.actorName) {<span class="ae-muted ae-mono"> · {{ e.actorId }}</span>}</dd>
          <dt>Tipo de ator</dt>
          <dd>{{ actorKind[e.actorKind] }}</dd>
          <dt>Host</dt>
          <dd>{{ e.environmentName ?? e.environmentId }}</dd>
          <dt>Categoria</dt>
          <dd>{{ categoryLabel(e.category) }}</dd>
          <dt>Ação registrada</dt>
          <dd class="ae-mono">{{ e.action }}</dd>
          @if (e.subjectId) {
            <dt>Alvo</dt>
            <dd class="ae-mono">
              @if (target(); as link) {
                <a [routerLink]="link.route" [queryParams]="link.query" queryParamsHandling="merge" (click)="closed.emit()">{{ e.subjectId }}</a>
              } @else {
                {{ e.subjectId }}
              }
            </dd>
          }
          <dt>Resultado</dt>
          <dd>{{ outcome(e).label }}@if (e.failureCode) {<span class="ae-mono"> · {{ e.failureCode }}</span>}</dd>
          <dt>Correlação</dt>
          <dd class="ae-mono">{{ e.correlationId }}</dd>
          @if (e.commandId) {
            <dt>Comando</dt>
            <dd class="ae-mono">{{ e.commandId }}</dd>
          }
          @for (detail of details(); track detail.key) {
            <dt>{{ detail.label }}</dt>
            <dd [class.ae-mono]="detail.key === 'command'">{{ detail.value }}</dd>
          }
        </dl>

        <h3>Eventos relacionados</h3>
        @if (related().length) {
          <ul class="ae-related">
            @for (item of related(); track item.id) {
              <li>
                <button type="button" (click)="selected.emit(item)">
                  <span class="alt-num ae-muted">{{ format(item.occurredAt) }}</span>
                  <span>{{ actionLabel(item.action) }} · {{ actorLabel(item) }}</span>
                  <span>{{ outcome(item).label }}</span>
                </button>
              </li>
            }
          </ul>
        } @else {
          <p class="app-small ae-muted" style="margin: 0">Nenhum outro evento com a mesma correlação ou comando.</p>
        }
      }
    </dok-drawer>
  `,
})
export class AuditEventDrawer {
  readonly open = input(false);
  readonly eventId = input.required<string>();
  readonly at = input<string | null>(null);
  readonly closed = output<void>();
  readonly selected = output<AuditEventView>();

  private readonly api = inject(AuditApiService);
  protected readonly detail = signal<AuditEventDetail | null>(null);
  protected readonly failed = signal(false);
  protected readonly event = computed(() => this.detail()?.event ?? null);
  protected readonly related = computed(() => this.detail()?.related ?? []);
  protected readonly target = computed(() => (this.event() ? targetLink(this.event()!) : null));
  protected readonly actorKind = AUDIT_ACTOR_KIND;
  protected readonly format = formatDateTime;
  protected readonly actionLabel = auditActionLabel;
  protected readonly actorLabel = auditActorLabel;
  protected readonly subtitle = computed(() => {
    const e = this.event();
    return e ? `${auditActorLabel(e)} · ${e.environmentName ?? 'host'} · ${formatDateTime(e.occurredAt)}` : null;
  });
  protected readonly chips = computed(() => {
    const e = this.event();
    if (!e) return [];
    const outcome = AUDIT_OUTCOME[e.outcome];
    return [{ tone: outcome.tone, icon: e.outcome === 'succeeded' ? 'check' : 'alert', label: outcome.label }];
  });
  protected readonly details = computed(() =>
    Object.entries(this.event()?.details ?? {})
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([key, value]) => ({ key, label: AUDIT_DETAIL[key] ?? key, value })),
  );

  constructor() {
    effect(() => {
      const id = this.eventId();
      const at = this.at();
      if (!this.open()) return;
      untracked(() => void this.load(id, at));
    });
  }

  protected outcome(event: AuditEventView) {
    return AUDIT_OUTCOME[event.outcome];
  }

  protected categoryLabel(category: AuditEventView['category']): string {
    return AUDIT_CATEGORIES.find(c => c.id === category)?.label ?? '—';
  }

  private async load(id: string, at: string | null): Promise<void> {
    this.failed.set(false);
    try {
      const detail = await this.api.getAuditEvent(id, at);
      if (id !== this.eventId()) return;
      this.detail.set(detail);
    } catch {
      if (id !== this.eventId()) return;
      this.detail.set(null);
      this.failed.set(true);
    }
  }
}
