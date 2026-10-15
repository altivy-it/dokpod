import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { ALERT_SEVERITIES, ALERT_SEVERITY, RULE_EVENT_TYPES, RULE_METRICS } from '../../data-access/alert-labels';
import { AlertsApiService } from './alerts-api.service';
import type { AlertRule, AlertRuleRequest, AlertSeverity, Stage } from '../../data-access/generated/control-plane';
import { DokCheckbox, DokDrawer, DokField } from '../../design-system';
import { STAGES } from '../../shell/stage-filter.service';
import { actionError } from './alerts.facade';

interface RuleForm {
  name: string;
  description: string;
  conditionKind: 'event' | 'metric';
  eventType: string;
  metric: NonNullable<AlertRule['metric']>;
  thresholdPercent: number;
  durationMinutes: number;
  minOccurrences: number;
  severity: AlertSeverity;
  stage: Stage | '';
  environmentId: string;
  containerPattern: string;
  enabled: boolean;
}

const CONTAINER_EVENTS = new Set(['container.died', 'container.oom_killed', 'container.unhealthy', 'container.auto_restarted', 'command.failed', 'command.indeterminate']);

export function emptyRuleForm(): RuleForm {
  return {
    name: '',
    description: '',
    conditionKind: 'event',
    eventType: 'container.died',
    metric: 'host_cpu',
    thresholdPercent: 80,
    durationMinutes: 0,
    minOccurrences: 1,
    severity: 'medium',
    stage: '',
    environmentId: '',
    containerPattern: '',
    enabled: true,
  };
}

export function ruleToForm(rule: AlertRule): RuleForm {
  return {
    name: rule.name,
    description: rule.description ?? '',
    conditionKind: rule.conditionKind,
    eventType: rule.eventType ?? 'container.died',
    metric: rule.metric ?? 'host_cpu',
    thresholdPercent: rule.thresholdPercent ?? 80,
    durationMinutes: Math.round(rule.durationSeconds / 60),
    minOccurrences: rule.minOccurrences,
    severity: rule.severity,
    stage: rule.scope.stage ?? '',
    environmentId: rule.scope.environmentId ?? '',
    containerPattern: rule.scope.containerPattern ?? '',
    enabled: rule.enabled,
  };
}

/** Corpo da requisição a partir do formulário; null com a mensagem do primeiro campo inválido. */
export function ruleRequest(form: RuleForm, version: number | null): { request: AlertRuleRequest | null; error: string | null } {
  const name = form.name.trim();
  if (!name || name.length > 120) return { request: null, error: 'Informe um nome de até 120 caracteres.' };
  if (form.description.length > 500) return { request: null, error: 'A descrição aceita até 500 caracteres.' };
  const duration = Math.round(Number(form.durationMinutes) * 60);
  if (!Number.isFinite(duration) || duration < 0 || duration > 86_400) return { request: null, error: 'A duração vai de 0 a 1.440 minutos.' };
  const metric = form.conditionKind === 'metric';
  const threshold = Number(form.thresholdPercent);
  if (metric && (!Number.isFinite(threshold) || threshold < 1 || threshold > 100)) return { request: null, error: 'O limite vai de 1% a 100%.' };
  const occurrences = Math.round(Number(form.minOccurrences));
  if (!metric && (!Number.isFinite(occurrences) || occurrences < 1 || occurrences > 100)) return { request: null, error: 'O mínimo de ocorrências vai de 1 a 100.' };
  if (!metric && occurrences > 1 && duration === 0) return { request: null, error: 'Com mais de uma ocorrência, informe a janela em minutos.' };
  const pattern = form.containerPattern.trim();
  if (pattern.length > 128) return { request: null, error: 'O padrão de nome aceita até 128 caracteres.' };
  return {
    error: null,
    request: {
      name,
      description: form.description.trim() || null,
      conditionKind: form.conditionKind,
      eventType: metric ? null : form.eventType,
      metric: metric ? form.metric : null,
      thresholdPercent: metric ? threshold : null,
      durationSeconds: duration,
      minOccurrences: metric ? 1 : occurrences,
      severity: form.severity,
      scope: { stage: form.stage || null, environmentId: form.environmentId || null, containerPattern: pattern || null },
      enabled: form.enabled,
      version,
    },
  };
}

/** Criar e editar regra (spec 016 US4); em regra padrão só limite, duração, severidade, abrangência e ativação mudam. */
@Component({
  selector: 'app-rule-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokCheckbox, DokDrawer, DokField, FormsModule],
  styles: `
    .rd-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .rd-full { grid-column: 1 / -1; }
    .rd-select { width: 100%; padding: 8px 10px; border-radius: var(--radius-md); border: 1px solid var(--glass-border-strong); background: var(--input-fill); color: var(--foreground); font: inherit; }
    .rd-select:focus-visible { outline: 2px solid var(--teal-light); outline-offset: 1px; }
    .rd-select:disabled { opacity: 0.6; }
    .rd-label { display: flex; flex-direction: column; gap: 6px; }
    .rd-error { margin: 0; color: var(--signal-error); font-size: 13px; }
    .rd-note { margin: 0; color: var(--muted); font-size: 13px; }
    fieldset { border: 0; margin: 0; padding: 0; display: flex; gap: 16px; }
    legend { padding: 0; margin-bottom: 6px; }
    @media (max-width: 560px) { .rd-grid { grid-template-columns: 1fr; } }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      eyebrow="Regra de alerta"
      [title]="rule() ? 'Editar ' + rule()!.name : 'Nova regra'"
      [subtitle]="rule()?.builtIn ? 'Regra padrão: a condição é fixa; ajuste limite, severidade, abrangência e ativação.' : 'Vale para as próximas avaliações; a alteração fica na auditoria.'"
      [primaryLabel]="rule() ? 'Salvar regra' : 'Criar regra'"
      [primaryDisabled]="busy()"
      [primaryLoading]="busy()"
      width="560px"
      (closed)="closed.emit()"
      (primary)="save()"
    >
      <form class="rd-grid" (submit)="$event.preventDefault(); save()">
        <div class="rd-full">
          <dok-field label="Nome" [ngModel]="form().name" (ngModelChange)="patch({ name: $event })" name="name" [required]="true" />
        </div>
        <div class="rd-full">
          <dok-field label="Descrição (opcional)" [ngModel]="form().description" (ngModelChange)="patch({ description: $event })" name="description" />
        </div>
        <fieldset class="rd-full" [disabled]="builtIn()">
          <legend class="alt-field__label">Condição</legend>
          <label><input type="radio" name="kind" value="event" [checked]="form().conditionKind === 'event'" (change)="patch({ conditionKind: 'event' })" /> Evento operacional</label>
          <label><input type="radio" name="kind" value="metric" [checked]="form().conditionKind === 'metric'" (change)="patch({ conditionKind: 'metric', durationMinutes: form().durationMinutes || 5 })" /> Limite de CPU ou memória</label>
        </fieldset>
        @if (form().conditionKind === 'event') {
          <label class="rd-label rd-full">
            <span class="alt-field__label">Tipo de evento</span>
            <select class="rd-select" [disabled]="builtIn()" [ngModel]="form().eventType" (ngModelChange)="patch({ eventType: $event })" name="eventType">
              @for (type of eventTypes; track type.id) {
                <option [value]="type.id">{{ type.label }}</option>
              }
            </select>
          </label>
          <dok-field label="Mínimo de ocorrências" type="number" [ngModel]="form().minOccurrences" (ngModelChange)="patch({ minOccurrences: $event })" name="minOccurrences" help="1 abre na primeira ocorrência" />
          <dok-field label="Janela ou duração (min)" type="number" [ngModel]="form().durationMinutes" (ngModelChange)="patch({ durationMinutes: $event })" name="duration" help="Janela das ocorrências; para agente desconectado, tempo mínimo" />
        } @else {
          <label class="rd-label rd-full">
            <span class="alt-field__label">Métrica</span>
            <select class="rd-select" [disabled]="builtIn()" [ngModel]="form().metric" (ngModelChange)="patch({ metric: $event })" name="metric">
              @for (metric of metrics; track metric.id) {
                <option [value]="metric.id">{{ metric.label }}</option>
              }
            </select>
          </label>
          <dok-field label="Limite" type="number" suffix="%" [ngModel]="form().thresholdPercent" (ngModelChange)="patch({ thresholdPercent: $event })" name="threshold" />
          <dok-field label="Acima do limite por (min)" type="number" [ngModel]="form().durationMinutes" (ngModelChange)="patch({ durationMinutes: $event })" name="duration" />
        }
        <label class="rd-label">
          <span class="alt-field__label">Severidade</span>
          <select class="rd-select" [ngModel]="form().severity" (ngModelChange)="patch({ severity: $event })" name="severity">
            @for (severity of severities; track severity) {
              <option [value]="severity">{{ severityLabel(severity) }}</option>
            }
          </select>
        </label>
        <label class="rd-label">
          <span class="alt-field__label">Estágio</span>
          <select class="rd-select" [ngModel]="form().stage" (ngModelChange)="patch({ stage: $event })" name="stage">
            <option value="">Todos os estágios</option>
            @for (stage of stages; track stage.id) {
              <option [value]="stage.id">{{ stage.label }}</option>
            }
          </select>
        </label>
        <label class="rd-label">
          <span class="alt-field__label">Host</span>
          <select class="rd-select" [ngModel]="form().environmentId" (ngModelChange)="patch({ environmentId: $event })" name="host">
            <option value="">Todos os hosts</option>
            @for (host of hosts(); track host.environmentId) {
              <option [value]="host.environmentId">{{ host.name }}</option>
            }
          </select>
        </label>
        @if (containerTarget()) {
          <dok-field label="Containers (padrão de nome)" [ngModel]="form().containerPattern" (ngModelChange)="patch({ containerPattern: $event })" name="pattern" placeholder="ex.: api-*" [mono]="true" />
        }
        <div class="rd-full">
          <dok-checkbox label="Regra ativa" help="Desativada, a regra não abre alertas novos." [ngModel]="form().enabled" (ngModelChange)="patch({ enabled: $event })" name="enabled" />
        </div>
        @if (error(); as message) {
          <p class="rd-error rd-full" role="alert">{{ message }}</p>
        }
        <p class="rd-note rd-full">Salvar exige alert:manage em todos os hosts abrangidos.</p>
      </form>
    </dok-drawer>
  `,
})
export class RuleDrawer {
  readonly open = input(false);
  readonly rule = input<AlertRule | null>(null);
  readonly closed = output<void>();
  readonly saved = output<void>();

  private readonly api = inject(AlertsApiService);
  private readonly directory = inject(EnvironmentDirectoryService);
  protected readonly form = signal<RuleForm>(emptyRuleForm());
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly eventTypes = RULE_EVENT_TYPES;
  protected readonly metrics = RULE_METRICS;
  protected readonly severities = ALERT_SEVERITIES;
  protected readonly stages = STAGES;
  protected readonly builtIn = computed(() => this.rule()?.builtIn ?? false);
  protected readonly hosts = computed(() => [...this.directory.environments()].sort((a, b) => a.name.localeCompare(b.name, 'pt-BR')));
  protected readonly containerTarget = computed(() => {
    const form = this.form();
    return form.conditionKind === 'metric' ? form.metric.startsWith('container_') : CONTAINER_EVENTS.has(form.eventType);
  });

  constructor() {
    effect(() => {
      const rule = this.rule();
      if (!this.open()) return;
      untracked(() => {
        this.form.set(rule ? ruleToForm(rule) : emptyRuleForm());
        this.error.set(null);
      });
    });
  }

  protected patch(changes: Partial<RuleForm>): void {
    this.form.set({ ...this.form(), ...changes });
  }

  protected severityLabel(severity: AlertSeverity): string {
    return ALERT_SEVERITY[severity].label;
  }

  protected async save(): Promise<void> {
    const rule = this.rule();
    const { request, error } = ruleRequest(this.form(), rule?.version ?? null);
    if (!request) {
      this.error.set(error);
      return;
    }
    if (!this.containerTarget()) request.scope = { ...request.scope, containerPattern: null };
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.saveAlertRule(rule?.id ?? null, request);
      this.saved.emit();
    } catch (failure) {
      this.error.set(actionError(failure));
    } finally {
      this.busy.set(false);
    }
  }
}
