import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { JUSTIFICATION_MAX, JUSTIFICATION_MIN } from '../../core/justification-policy.service';
import { ALERT_RESOLUTION, ALERT_SEVERITY, ALERT_STATE, describeRule, describeScope, TIMELINE_LABEL } from '../../data-access/alert-labels';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { AlertDetail } from '../../data-access/generated/control-plane';
import { DokButton, DokDrawer, DokIcon, formatDateTime } from '../../design-system';
import { actionError } from './alerts.facade';

type AlertAction = 'acknowledge' | 'resolve' | 'silence';

const SILENCE_OPTIONS = [
  { hours: 1, label: '1 hora' },
  { hours: 4, label: '4 horas' },
  { hours: 24, label: '24 horas' },
  { hours: 72, label: '3 dias' },
  { hours: 168, label: '7 dias' },
];

/** Hora local `yyyy-MM-ddTHH:mm` para o campo de data e hora. */
export function toLocalInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/**
 * Detalhe do alerta (spec 016 US2): condição, alvo, regra e linha do tempo; reconhecer e resolver pedem
 * comentário e silenciar pede justificativa e fim da janela (até 7 dias), todos com 10 a 500 caracteres.
 */
@Component({
  selector: 'app-alert-detail-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokDrawer, DokIcon, FormsModule, RouterLink],
  styles: `
    .ad-facts { display: grid; grid-template-columns: 140px minmax(0, 1fr); gap: 6px 12px; margin: 0; font-size: 13px; }
    .ad-facts dt { color: var(--muted); }
    .ad-facts dd { margin: 0; color: var(--foreground); overflow-wrap: anywhere; }
    .ad-condition { margin: 0; font-size: 15px; color: var(--text-strong); }
    h3 { margin: 0; font-size: 13px; font-weight: 600; color: var(--muted); }
    .ad-timeline { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; border-left: 2px solid var(--hairline); }
    .ad-timeline li { position: relative; padding: 0 0 12px 14px; font-size: 13px; }
    .ad-timeline li::before { content: ''; position: absolute; left: -5px; top: 5px; width: 8px; height: 8px; border-radius: 50%; background: var(--teal-light); }
    .ad-when { color: var(--muted); }
    .ad-comment { margin: 2px 0 0; color: var(--foreground); overflow-wrap: anywhere; }
    .ad-actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .ad-form { display: flex; flex-direction: column; gap: 8px; padding: 12px; border: 1px solid var(--hairline); border-radius: var(--radius-md); }
    .ad-textarea { width: 100%; box-sizing: border-box; resize: vertical; padding: 10px 12px; border-radius: var(--radius-md); border: 1px solid var(--glass-border-strong); background: var(--input-fill); color: var(--foreground); font: inherit; }
    .ad-textarea:focus-visible, .ad-select:focus-visible { outline: 2px solid var(--teal-light); outline-offset: 1px; }
    .ad-select { padding: 8px 10px; border-radius: var(--radius-md); border: 1px solid var(--glass-border-strong); background: var(--input-fill); color: var(--foreground); font: inherit; }
    .ad-help { display: flex; justify-content: space-between; gap: 12px; color: var(--muted); }
    .ad-error { margin: 0; color: var(--signal-error); font-size: 13px; }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      eyebrow="Alerta"
      [title]="detail()?.alert?.ruleName ?? 'Carregando…'"
      [subtitle]="subtitle()"
      [chips]="chips()"
      [primaryLabel]="pendingAction() ? confirmLabel() : null"
      [primaryIcon]="pendingAction() === 'resolve' ? 'check' : pendingAction() === 'silence' ? 'bell' : 'eye'"
      [primaryDisabled]="!canSubmit()"
      [primaryLoading]="busy()"
      [cancelLabel]="pendingAction() ? 'Voltar' : 'Fechar'"
      width="560px"
      (closed)="pendingAction() ? cancelAction() : closed.emit()"
      (primary)="submit()"
    >
      @if (loadFailed()) {
        <p class="ad-error" role="alert">Não foi possível carregar o alerta. Ele pode ter sido removido pela retenção ou você não tem mais acesso.</p>
      }
      @if (detail(); as d) {
        <p class="ad-condition">{{ d.alert.condition }}</p>
        <dl class="ad-facts">
          <dt>Host</dt>
          <dd>{{ hostName(d.alert.environmentId) }}</dd>
          @if (d.alert.containerName || d.alert.containerId) {
            <dt>Container</dt>
            <dd>
              @if (d.alert.containerId && d.alert.resolution !== 'container_removed') {
                <a [routerLink]="['/containers', d.alert.environmentId, d.alert.containerId]" queryParamsHandling="merge">{{ d.alert.containerName ?? d.alert.containerId.slice(0, 12) }}</a>
              } @else {
                {{ d.alert.containerName ?? d.alert.containerId?.slice(0, 12) }}
              }
            </dd>
          }
          <dt>Aberto em</dt>
          <dd class="alt-num">{{ format(d.alert.openedAt) }}</dd>
          <dt>Última ocorrência</dt>
          <dd class="alt-num">{{ format(d.alert.lastOccurredAt) }} · {{ d.alert.occurrences }} {{ d.alert.occurrences === 1 ? 'ocorrência' : 'ocorrências' }}</dd>
          @if (d.alert.assignee) {
            <dt>Responsável</dt>
            <dd>{{ d.alert.assignee }}</dd>
          }
          @if (d.alert.resolvedAt) {
            <dt>Resolvido em</dt>
            <dd class="alt-num">{{ format(d.alert.resolvedAt) }} · {{ resolution(d.alert.resolution) }}</dd>
          }
          @if (d.rule; as rule) {
            <dt>Regra</dt>
            <dd>{{ describe(rule) }} · {{ scope(rule) }}</dd>
          }
        </dl>

        @if (d.alert.canManage && d.alert.state !== 'resolved' && !pendingAction()) {
          <div class="ad-actions">
            @if (d.alert.state === 'open') {
              <button dok-button variant="outline" size="sm" startIcon="eye" type="button" (click)="startAction('acknowledge')">Reconhecer</button>
            }
            @if (d.alert.state !== 'silenced') {
              <button dok-button variant="outline" size="sm" startIcon="bell" type="button" (click)="startAction('silence')">Silenciar</button>
            }
            <button dok-button variant="outline" size="sm" startIcon="check" type="button" (click)="startAction('resolve')">Resolver</button>
          </div>
        } @else if (!d.alert.canManage && d.alert.state !== 'resolved') {
          <p class="app-small" style="margin: 0; color: var(--muted)"><dok-icon name="lock" [size]="13" /> Ações exigem alert:manage neste host.</p>
        }

        @if (pendingAction(); as action) {
          <form class="ad-form" (submit)="$event.preventDefault(); submit()">
            @if (action === 'silence') {
              <label for="ad-until" class="alt-field__label">Silenciar por</label>
              <select id="ad-until" class="ad-select" [ngModel]="silenceHours()" (ngModelChange)="silenceHours.set(+$event)" name="until">
                @for (option of silenceOptions; track option.hours) {
                  <option [value]="option.hours">{{ option.label }}</option>
                }
              </select>
            }
            <label for="ad-text" class="alt-field__label">{{ action === 'silence' ? 'Justificativa' : 'Comentário' }}</label>
            <textarea
              id="ad-text"
              class="ad-textarea"
              rows="3"
              [attr.maxlength]="max"
              aria-required="true"
              aria-describedby="ad-text-help"
              [ngModel]="text()"
              (ngModelChange)="text.set($event)"
              name="text"
            ></textarea>
            <span id="ad-text-help" class="app-caption ad-help">
              <span>De {{ min }} a {{ max }} caracteres; registrado na linha do tempo e na auditoria.</span>
              <span class="alt-num" aria-hidden="true">{{ text().trim().length }}/{{ max }}</span>
            </span>
            @if (actionFailed(); as message) {
              <p class="ad-error" role="alert">{{ message }}</p>
            }
          </form>
        }

        <h3>Linha do tempo</h3>
        <ol class="ad-timeline">
          @for (entry of d.timeline; track $index) {
            <li>
              <b>{{ timelineLabel(entry.kind) }}</b>
              <span class="ad-when alt-num"> · {{ format(entry.at) }}{{ entry.actor ? ' · ' + entry.actor : '' }}</span>
              @if (entry.comment) {
                <p class="ad-comment">{{ entry.comment }}</p>
              }
            </li>
          }
        </ol>
      }
    </dok-drawer>
  `,
})
export class AlertDetailDrawer {
  readonly open = input(false);
  readonly alertId = input.required<string>();
  /** Muda a cada invalidação de alertas, para recarregar o detalhe aberto. */
  readonly refreshKey = input(0);
  readonly closed = output<void>();
  readonly changed = output<void>();

  private readonly api = inject(ControlPlaneApiService);
  private readonly directory = inject(EnvironmentDirectoryService);
  protected readonly detail = signal<AlertDetail | null>(null);
  protected readonly loadFailed = signal(false);
  protected readonly pendingAction = signal<AlertAction | null>(null);
  protected readonly text = signal('');
  protected readonly silenceHours = signal(4);
  protected readonly busy = signal(false);
  protected readonly actionFailed = signal<string | null>(null);
  protected readonly min = JUSTIFICATION_MIN;
  protected readonly max = JUSTIFICATION_MAX;
  protected readonly silenceOptions = SILENCE_OPTIONS;
  protected readonly format = formatDateTime;

  protected readonly subtitle = computed(() => {
    const alert = this.detail()?.alert;
    return alert ? `${this.hostName(alert.environmentId)}${alert.containerName ? ' · ' + alert.containerName : ''}` : null;
  });
  protected readonly chips = computed(() => {
    const alert = this.detail()?.alert;
    if (!alert) return [];
    const severity = ALERT_SEVERITY[alert.severity];
    const state = ALERT_STATE[alert.state];
    return [
      { tone: severity.tone, icon: 'alert', label: `Severidade ${severity.label.toLowerCase()}` },
      { tone: state.tone, icon: 'info', label: state.label },
    ];
  });
  protected readonly confirmLabel = computed(() =>
    this.pendingAction() === 'acknowledge' ? 'Reconhecer' : this.pendingAction() === 'resolve' ? 'Resolver' : 'Silenciar',
  );
  protected readonly canSubmit = computed(() => {
    const length = this.text().trim().length;
    return length >= JUSTIFICATION_MIN && length <= JUSTIFICATION_MAX && !this.busy();
  });

  constructor() {
    effect(() => {
      const id = this.alertId();
      this.refreshKey();
      if (!this.open()) return;
      untracked(() => void this.load(id));
    });
  }

  protected hostName(environmentId: string): string {
    return this.directory.byId(environmentId)?.name ?? 'host';
  }

  protected describe = describeRule;
  protected scope(rule: NonNullable<AlertDetail['rule']>): string {
    return describeScope(rule.scope, id => this.directory.byId(id)?.name);
  }

  protected resolution(reason: string | null | undefined): string {
    return reason ? (ALERT_RESOLUTION[reason] ?? reason) : '';
  }

  protected timelineLabel(kind: string): string {
    return TIMELINE_LABEL[kind] ?? kind;
  }

  protected startAction(action: AlertAction): void {
    this.pendingAction.set(action);
    this.text.set('');
    this.actionFailed.set(null);
  }

  protected cancelAction(): void {
    this.pendingAction.set(null);
    this.actionFailed.set(null);
  }

  protected async submit(): Promise<void> {
    const action = this.pendingAction();
    const alert = this.detail()?.alert;
    if (!action || !alert || !this.canSubmit()) return;
    this.busy.set(true);
    this.actionFailed.set(null);
    try {
      const text = this.text().trim();
      if (action === 'acknowledge') await this.api.acknowledgeAlert(alert.id, text);
      else if (action === 'resolve') await this.api.resolveAlert(alert.id, text);
      else await this.api.silenceAlert(alert.id, new Date(Date.now() + this.silenceHours() * 3600_000).toISOString(), text);
      this.pendingAction.set(null);
      this.changed.emit();
      await this.load(alert.id);
    } catch (error) {
      this.actionFailed.set(actionError(error));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(alertId: string): Promise<void> {
    try {
      const detail = await this.api.getAlert(alertId);
      if (alertId !== this.alertId()) return;
      this.detail.set(detail);
      this.loadFailed.set(false);
    } catch {
      if (alertId !== this.alertId()) return;
      this.detail.set(null);
      this.loadFailed.set(true);
    }
  }
}
