import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DokButton,
  DokEmptyState,
  DokField,
  DokKeyValue,
  DokStatusChip,
  formatDateTime,
  type KeyValueItem,
  ToastService,
} from '../../design-system';
import { ENTITLEMENT, expiryWarningDays, GRACE_CAUSE, importFailureMessage, REASON, revocationFailureMessage } from './licensing-labels';
import type { LicensingHost } from '../../data-access/generated/control-plane';
import { LicensingApiService, MAX_LICENSE_BYTES, toLicensingFailure, type LicensingSnapshot } from './licensing-api.service';

/**
 * Seção Licença de Configurações → Instalação (spec 031 T028), só na edição Business: estado, organização, plano,
 * vigência, capacidade, cota da instalação, capabilities e importação com `If-Match`. O conteúdo do arquivo nunca
 * aparece na tela, em notificação ou em erro (FR-027).
 */
@Component({
  selector: 'app-licensing-section',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokEmptyState, DokField, DokKeyValue, DokStatusChip, FormsModule],
  templateUrl: './licensing-section.component.html',
})
export class LicensingSection implements OnInit {
  private readonly api = inject(LicensingApiService);
  private readonly toast = inject(ToastService);

  protected readonly snapshot = signal<LicensingSnapshot | null>(null);
  protected readonly loadStatus = signal<number | null>(null);
  protected readonly file = signal<File | null>(null);
  protected readonly importing = signal(false);
  protected readonly revocationFile = signal<File | null>(null);
  protected readonly hosts = signal<LicensingHost[]>([]);

  /** Motivos (`reasons`), tolerância e aviso de vencimento (T048); a mesma informação fica na trilha de licenciamento. */
  protected readonly reasons = computed(() => (this.snapshot()?.status.reasons ?? []).map(reason => REASON[reason] ?? reason));
  protected readonly grace = computed(() => {
    const grace = this.snapshot()?.status.grace;
    return grace ? `Tolerância por ${GRACE_CAUSE[grace.cause] ?? grace.cause} até ${formatDateTime(grace.until)}` : null;
  });
  protected readonly expiryWarning = computed(() => {
    const status = this.snapshot()?.status;
    const days = status?.license ? expiryWarningDays(status.license.expiresAt, status.validity) : null;
    return days === null ? null : `Vence em ${days} dia(s), em ${formatDateTime(status!.license!.expiresAt)}`;
  });
  protected readonly unknownHosts = computed(() => this.hosts().filter(host => !host.removed && host.source !== 'engine'));

  /** Consumo apurado (US3): atual, pico da janela de 30 dias com horário, faixa e cota da instalação. */
  protected readonly consumption = computed<KeyValueItem[]>(() => {
    const status = this.snapshot()?.status;
    if (!status?.license) return [];
    const usage = status.consumption;
    const quota = status.installation.quota;
    const peakAt = usage.windowPeakAt ? ` em ${formatDateTime(usage.windowPeakAt)}` : '';
    return [
      { label: 'Consumo atual', value: usage.current === null ? '—' : `${usage.current} nodes equivalentes` },
      { label: 'Pico em 30 dias', value: usage.windowPeak === null ? '—' : `${usage.windowPeak} nodes equivalentes${peakAt}` },
      { label: 'Faixa', value: usage.vcpuBand === null ? '—' : `${usage.vcpuBand} vCPU por node equivalente` },
      { label: 'Cota da instalação', value: quota === null ? 'sem limite próprio' : `${quota} nodes equivalentes` },
    ];
  });
  protected slotId = '';

  protected readonly entitlement = computed(() => {
    const status = this.snapshot()?.status;
    return status ? ENTITLEMENT[status.entitlement] : null;
  });

  protected readonly details = computed<KeyValueItem[]>(() => {
    const status = this.snapshot()?.status;
    if (!status?.license) return [];
    const license = status.license;
    const capacity = license.capacity?.mode === 'unlimited' ? 'Ilimitada' : `${license.capacity?.nodeEquivalents ?? '—'} nodes equivalentes`;
    const quota = status.installation.quota === null || status.installation.quota === undefined ? 'sem limite próprio' : `${status.installation.quota} nodes equivalentes`;
    return [
      { label: 'Organização', value: license.organizationId ?? '—', mono: true },
      { label: 'Licença', value: `${license.licenseId ?? '—'} · revisão ${license.revision ?? '—'}`, mono: true },
      { label: 'Plano', value: license.plan ?? '—' },
      { label: 'Vigência', value: `${formatDateTime(license.notBefore ?? '')} até ${formatDateTime(license.expiresAt ?? '')}` },
      { label: 'Capacidade', value: `${capacity} · faixa de ${status.consumption?.vcpuBand ?? '—'} vCPU` },
      { label: 'Instalação', value: `${status.installation.slotId ?? '—'} (${status.installation.kind === 'staging' ? 'homologação' : 'produção'}) · ${quota}` },
      { label: 'Recursos', value: license.capabilities?.length ? license.capabilities.join(', ') : 'nenhum' },
    ];
  });

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    try {
      const snapshot = await this.api.status();
      this.snapshot.set(snapshot);
      this.slotId ||= snapshot.status.installation.slotId ?? '';
      this.loadStatus.set(null);
      this.hosts.set(snapshot.status.license ? await this.api.hosts().catch(() => []) : []);
    } catch (error: unknown) {
      this.loadStatus.set(toLicensingFailure(error).status || 0);
    }
  }

  protected chooseRevocation(event: Event): void {
    this.revocationFile.set((event.target as HTMLInputElement).files?.[0] ?? null);
  }

  protected async importRevocation(): Promise<void> {
    const file = this.revocationFile();
    if (!file) return;
    if (file.size > MAX_LICENSE_BYTES) {
      this.toast.error(importFailureMessage('payload_too_large', 413));
      return;
    }
    this.importing.set(true);
    try {
      this.snapshot.set(await this.api.importRevocation(file));
      this.toast.success('Pacote de revogação importado.');
    } catch (error: unknown) {
      const failure = toLicensingFailure(error);
      this.toast.error(revocationFailureMessage(failure.code, failure.status));
    } finally {
      this.importing.set(false);
    }
  }

  protected chooseFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
  }

  protected async importLicense(): Promise<void> {
    const file = this.file();
    const snapshot = this.snapshot();
    if (!file || !snapshot || !this.slotId.trim()) return;
    if (file.size > MAX_LICENSE_BYTES) {
      this.toast.error(importFailureMessage('payload_too_large', 413));
      return;
    }
    this.importing.set(true);
    try {
      this.snapshot.set(await this.api.importLicense(file, this.slotId.trim(), snapshot.etag));
      this.toast.success('Licença importada.');
      this.hosts.set(await this.api.hosts().catch(() => []));
    } catch (error: unknown) {
      const failure = toLicensingFailure(error);
      this.toast.error(importFailureMessage(failure.code, failure.status));
      if (failure.code === 'license_revision_conflict') await this.load();
    } finally {
      this.importing.set(false);
    }
  }
}
