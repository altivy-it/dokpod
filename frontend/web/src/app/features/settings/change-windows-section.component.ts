import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DokButton,
  DokCheckbox,
  DokEmptyState,
  DokField,
  DokStatusChip,
  formatDateTime,
  ToastService,
} from '../../design-system';
import type {
  ChangeWindowDay,
  ChangeWindowItem,
  ChangeWindowRange,
  ChangeWindowStatus,
  LicensingStatus,
} from '../../data-access/generated/control-plane';
import { toAuditExportFailure } from './audit-export-api.service';
import { ChangeWindowsApiService } from './change-windows-api.service';
import { LicensingApiService } from './licensing-api.service';
import {
  CHANGE_WINDOW_DAYS,
  CHANGE_WINDOW_STATE,
  changeWindowFailureMessage,
  STAGES,
} from './settings-labels';

export const CHANGE_WINDOWS_CAPABILITY = 'change-windows';
const MAX_RANGES = 14;
const TIME = /^([01][0-9]|2[0-3]):[0-5][0-9]$/;

/** Faixa em edição: um marcador por dia e as horas como texto `HH:mm`. */
interface RangeForm {
  days: Record<ChangeWindowDay, boolean>;
  start: string;
  end: string;
}

interface WindowForm {
  item: ChangeWindowItem;
  timeZone: string;
  ranges: RangeForm[];
}

/**
 * Janelas de mudança por estágio na vista Políticas (spec 026 T019), só na edição Business e com a capability
 * `change-windows` na licença. Fora da janela, as ações que mudam o host são recusadas; leituras nunca. Com a licença
 * restrita, editar fica bloqueado com a explicação e remover continua, porque a janela gravada segue valendo (FR-007).
 */
@Component({
  selector: 'app-change-windows-section',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokCheckbox, DokEmptyState, DokField, DokStatusChip, FormsModule],
  templateUrl: './change-windows-section.component.html',
  styleUrl: './settings.page.css',
  host: { style: 'display: contents' },
})
export class ChangeWindowsSection implements OnInit {
  private readonly api = inject(ChangeWindowsApiService);
  private readonly licensing = inject(LicensingApiService);
  private readonly toast = inject(ToastService);

  /** `undefined` até a primeira leitura: a seção só aparece depois de saber se a licença tem a capability. */
  protected readonly license = signal<LicensingStatus | null | undefined>(undefined);
  protected readonly windows = signal<ChangeWindowItem[] | null>(null);
  protected readonly loadStatus = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly form = signal<WindowForm | null>(null);

  protected readonly days = CHANGE_WINDOW_DAYS;
  protected readonly stateLabel = CHANGE_WINDOW_STATE;
  protected readonly formatDateTime = formatDateTime;
  protected readonly maxRanges = MAX_RANGES;

  /** Sem a capability na licença a seção não aparece; sem conseguir ler a licença, o servidor decide. */
  protected readonly available = computed(() => {
    const license = this.license();
    return (
      license === null ||
      (license !== undefined &&
        (license.license?.capabilities ?? []).includes(CHANGE_WINDOWS_CAPABILITY))
    );
  });
  protected readonly restricted = computed(() => {
    const license = this.license();
    return (
      !!license && license.entitlement !== 'granted' && license.entitlement !== 'granted_in_grace'
    );
  });

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.license.set(
      await this.licensing
        .status()
        .then((snapshot) => snapshot.status)
        .catch(() => null),
    );
    if (!this.available()) return;
    try {
      this.windows.set(await this.api.list());
      this.loadStatus.set(null);
    } catch (error: unknown) {
      this.loadStatus.set(toAuditExportFailure(error).status || 0);
    }
  }

  protected stageLabel(item: ChangeWindowItem): string {
    return STAGES.find((stage) => stage.id === item.stage)?.label ?? item.stage;
  }

  /** "seg, ter 22:00–06:00 (cruza a meia-noite)". */
  protected describe(range: ChangeWindowRange): string {
    const days = this.days
      .filter((day) => range.days.includes(day.id))
      .map((day) => day.short)
      .join(', ');
    const overnight = range.end < range.start ? ' (cruza a meia-noite)' : '';
    return `${days} ${range.start}–${range.end}${overnight}`;
  }

  /** Estado agora, com o instante no fuso do estágio. */
  protected statusDetail(status: ChangeWindowStatus): string | null {
    const zone = status.timeZone ?? undefined;
    if (status.state === 'open' && status.closesAt) return `Fecha em ${this.inZone(status.closesAt, zone)}`;
    if (status.state === 'closed' && status.nextOpensAt) return `Abre em ${this.inZone(status.nextOpensAt, zone)}`;
    return null;
  }

  protected startEdit(item: ChangeWindowItem): void {
    const ranges = item.ranges.length
      ? item.ranges.map((range) => this.toRangeForm(range))
      : [this.toRangeForm({ days: ['mon', 'tue', 'wed', 'thu', 'fri'], start: '22:00', end: '06:00' })];
    this.form.set({
      item,
      timeZone: item.timeZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone ?? 'UTC',
      ranges,
    });
  }

  protected addRange(form: WindowForm): void {
    if (form.ranges.length >= MAX_RANGES) return;
    this.form.set({
      ...form,
      ranges: [...form.ranges, this.toRangeForm({ days: ['sat'], start: '08:00', end: '12:00' })],
    });
  }

  protected removeRange(form: WindowForm, index: number): void {
    this.form.set({ ...form, ranges: form.ranges.filter((_, i) => i !== index) });
  }

  protected cancel(): void {
    this.form.set(null);
  }

  protected rangeError(range: RangeForm): string | null {
    if (!this.days.some((day) => range.days[day.id])) return 'Escolha ao menos um dia.';
    if (!TIME.test(range.start) || !TIME.test(range.end)) return 'Use horas no formato HH:mm.';
    if (range.start === range.end) return 'Início e fim precisam ser diferentes.';
    return null;
  }

  protected valid(form: WindowForm): boolean {
    return (
      form.timeZone.trim().length > 0 &&
      form.timeZone.length <= 64 &&
      form.ranges.length > 0 &&
      form.ranges.length <= MAX_RANGES &&
      form.ranges.every((range) => this.rangeError(range) === null)
    );
  }

  protected async save(): Promise<void> {
    const form = this.form();
    if (!form || !this.valid(form) || this.restricted()) return;
    await this.run(async () => {
      await this.api.save(form.item.stage, form.item.version ?? null, {
        timeZone: form.timeZone.trim(),
        ranges: form.ranges.map((range) => ({
          days: this.days.filter((day) => range.days[day.id]).map((day) => day.id),
          start: range.start,
          end: range.end,
        })),
      });
      this.toast.success(`Janela de ${this.stageLabel(form.item)} salva.`);
      this.form.set(null);
    });
  }

  protected async remove(item: ChangeWindowItem): Promise<void> {
    if (item.version === null || item.version === undefined) return;
    const version = item.version;
    await this.run(async () => {
      await this.api.remove(item.stage, version);
      this.toast.success(`Janela de ${this.stageLabel(item)} removida. As ações voltam a ser aceitas a qualquer hora.`);
    });
  }

  private toRangeForm(range: ChangeWindowRange): RangeForm {
    const days = Object.fromEntries(this.days.map((day) => [day.id, range.days.includes(day.id)])) as Record<ChangeWindowDay, boolean>;
    return { days, start: range.start, end: range.end };
  }

  private inZone(instant: string, timeZone: string | undefined): string {
    try {
      return `${new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone }).format(new Date(instant))}${timeZone ? ` (${timeZone})` : ''}`;
    } catch {
      return formatDateTime(instant);
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (error: unknown) {
      const failure = toAuditExportFailure(error);
      this.toast.error(changeWindowFailureMessage(failure.code, failure.status));
    } finally {
      this.busy.set(false);
      await this.load();
    }
  }
}
