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
  DokSelect,
  DokStatusChip,
  formatDateTime,
  formatRelative,
  type SelectOption,
  ToastService,
} from '../../design-system';
import { AUDIT_CATEGORIES } from '../../data-access/audit-labels';
import type {
  AuditCategory,
  AuditExportDestination,
  AuditExportDestinationInput,
  LicensingStatus,
} from '../../data-access/generated/control-plane';
import { newUuid } from '../../core/uuid';
import { AuditExportApiService, toAuditExportFailure } from './audit-export-api.service';
import { LicensingApiService } from './licensing-api.service';
import {
  AUDIT_EXPORT_ERROR,
  AUDIT_EXPORT_STATE,
  auditExportFailureMessage,
} from './settings-labels';

export const AUDIT_EXPORT_CAPABILITY = 'audit-siem-export';
const MANDATORY: AuditCategory = 'audit_export';
const MAX_DESTINATIONS = 5;
/** Portas padrão: syslog sobre TLS (RFC 5425) e OTLP/HTTP; trocar o protocolo troca a porta se ela não foi editada. */
const DEFAULT_PORT: Record<AuditExportDestination['protocol'], string> = {
  syslog: '6514',
  otlp: '4318',
};
const DEFAULT_PATH = '/v1/logs';

/** Formulário de criação ou edição; a chave de idempotência vale para a mesma intenção (repetir o clique). */
interface DestinationForm {
  destination: AuditExportDestination | null;
  idempotencyKey: string;
  name: string;
  protocol: AuditExportDestination['protocol'];
  host: string;
  port: string;
  path: string;
  secretRef: string;
  categories: Record<string, boolean>;
  active: boolean;
}

/**
 * Seção Exportação para SIEM de Configurações → Instalação (spec 027 T022), só na edição Business e com a capability
 * `audit-siem-export` na licença. Com a licença restrita, criar, editar, reativar e testar ficam desabilitados com a
 * explicação; desativar e excluir continuam (FR-008). Do secret, a tela só mostra quais arquivos existem.
 */
@Component({
  selector: 'app-audit-export-section',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokCheckbox, DokEmptyState, DokField, DokSelect, DokStatusChip, FormsModule],
  templateUrl: './audit-export-section.component.html',
  styleUrl: './settings.page.css',
  // O cartão é item da grade da aba Instalação; sem a capability, nada aparece.
  host: { style: 'display: contents' },
})
export class AuditExportSection implements OnInit {
  private readonly api = inject(AuditExportApiService);
  private readonly licensing = inject(LicensingApiService);
  private readonly toast = inject(ToastService);

  /** `undefined` até a primeira leitura: a seção só aparece depois de saber se a licença tem a capability. */
  protected readonly license = signal<LicensingStatus | null | undefined>(undefined);
  protected readonly destinations = signal<AuditExportDestination[] | null>(null);
  protected readonly loadStatus = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly form = signal<DestinationForm | null>(null);

  protected readonly categories = AUDIT_CATEGORIES;
  protected readonly stateLabel = AUDIT_EXPORT_STATE;
  protected readonly errorLabel = AUDIT_EXPORT_ERROR;
  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;
  protected readonly mandatory = MANDATORY;
  protected readonly protocols: SelectOption[] = [
    { value: 'syslog', label: 'Syslog com TLS (RFC 5425)' },
    { value: 'otlp', label: 'OpenTelemetry (OTLP/HTTP)' },
  ];

  /** Sem a capability na licença a seção não aparece; sem conseguir ler a licença, o servidor decide. */
  protected readonly available = computed(() => {
    const license = this.license();
    return (
      license === null ||
      (license !== undefined &&
        (license.license?.capabilities ?? []).includes(AUDIT_EXPORT_CAPABILITY))
    );
  });
  protected readonly restricted = computed(() => {
    const license = this.license();
    return (
      !!license && license.entitlement !== 'granted' && license.entitlement !== 'granted_in_grace'
    );
  });
  protected readonly full = computed(() => (this.destinations()?.length ?? 0) >= MAX_DESTINATIONS);

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
      this.destinations.set(await this.api.list());
      this.loadStatus.set(null);
    } catch (error: unknown) {
      this.loadStatus.set(toAuditExportFailure(error).status || 0);
    }
  }

  protected startCreate(): void {
    this.form.set({
      destination: null,
      idempotencyKey: newUuid(),
      name: '',
      protocol: 'syslog',
      host: '',
      port: DEFAULT_PORT.syslog,
      path: DEFAULT_PATH,
      secretRef: '',
      categories: { [MANDATORY]: true },
      active: true,
    });
  }

  protected startEdit(destination: AuditExportDestination): void {
    this.form.set({
      destination,
      idempotencyKey: newUuid(),
      name: destination.name,
      protocol: destination.protocol,
      host: destination.host,
      port: String(destination.port),
      path: destination.path ?? DEFAULT_PATH,
      secretRef: destination.secretRef ?? '',
      categories: Object.fromEntries(
        [...destination.categories, MANDATORY].map((category) => [category, true]),
      ),
      active: destination.active,
    });
  }

  /** Novo objeto no signal: a tela (OnPush) atualiza porta, caminho e o texto do secret. */
  protected changeProtocol(form: DestinationForm, protocol: string): void {
    const next = protocol === 'otlp' ? 'otlp' : 'syslog';
    const port = form.port === DEFAULT_PORT[form.protocol] ? DEFAULT_PORT[next] : form.port;
    this.form.set({ ...form, protocol: next, port });
  }

  protected cancel(): void {
    this.form.set(null);
  }

  protected valid(form: DestinationForm): boolean {
    const port = Number(form.port);
    return (
      form.name.trim().length > 0 &&
      form.host.trim().length > 0 &&
      Number.isInteger(port) &&
      port >= 1 &&
      port <= 65535 &&
      (form.secretRef === '' || /^[a-z0-9-]{1,64}$/.test(form.secretRef)) &&
      (form.protocol === 'syslog' || /^\/[^?# ]{0,255}$/.test(form.path))
    );
  }

  protected async save(): Promise<void> {
    const form = this.form();
    if (!form || !this.valid(form) || this.restricted()) return;
    const input: AuditExportDestinationInput = {
      name: form.name.trim(),
      protocol: form.protocol,
      host: form.host.trim(),
      port: Number(form.port),
      path: form.protocol === 'otlp' ? form.path : null,
      secretRef: form.secretRef || null,
      categories: this.categories
        .map((category) => category.id)
        .filter((id) => id === MANDATORY || form.categories[id]),
      active: form.active,
    };
    await this.run(async () => {
      if (form.destination) await this.api.update(form.destination, input);
      else await this.api.create(input, form.idempotencyKey);
      this.toast.success(
        form.destination
          ? 'Destino atualizado.'
          : 'Destino criado. O envio começa pelos próximos eventos da trilha.',
      );
      this.form.set(null);
    });
  }

  /** Desativar nunca é bloqueado pela licença; reativar é (FR-008). */
  protected async toggle(destination: AuditExportDestination): Promise<void> {
    if (!destination.active && this.restricted()) return;
    await this.run(async () => {
      await this.api.update(destination, {
        name: destination.name,
        protocol: destination.protocol,
        host: destination.host,
        port: destination.port,
        path: destination.path,
        secretRef: destination.secretRef,
        categories: destination.categories,
        active: !destination.active,
      });
      this.toast.success(
        destination.active
          ? 'Destino desativado. Nada mais é enviado a ele.'
          : 'Destino reativado.',
      );
    });
  }

  protected async remove(destination: AuditExportDestination): Promise<void> {
    await this.run(async () => {
      await this.api.remove(destination);
      this.toast.success('Destino excluído.');
    });
  }

  protected async test(destination: AuditExportDestination): Promise<void> {
    if (this.restricted()) return;
    await this.run(async () => {
      const result = await this.api.test(destination);
      if (result.delivered) this.toast.success(`Evento de teste entregue a ${destination.name}.`);
      else
        this.toast.error(
          `Evento de teste não entregue: ${this.errorLabel[result.errorCode ?? ''] ?? result.errorCode ?? 'falha desconhecida'}.`,
        );
    }, false);
  }

  protected secretSummary(destination: AuditExportDestination): string {
    if (!destination.secretRef) return 'sem secret (CA do sistema)';
    const files = [
      destination.secretFiles.ca ? 'CA' : null,
      destination.secretFiles.clientCertificate ? 'certificado de cliente' : null,
      destination.secretFiles.token ? 'token' : null,
    ].filter(Boolean);
    return `${destination.secretRef}: ${files.length ? files.join(', ') : 'nenhum arquivo encontrado'}`;
  }

  private async run(action: () => Promise<void>, reload = true): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (error: unknown) {
      const failure = toAuditExportFailure(error);
      this.toast.error(auditExportFailureMessage(failure.code, failure.status));
    } finally {
      this.busy.set(false);
      if (reload) await this.load();
    }
  }
}
