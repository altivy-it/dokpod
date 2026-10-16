import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ControlPlaneApiService } from '../data-access/control-plane-api.service';
import { getSettingsAccess, type AuditSummary } from '../data-access/generated/control-plane';
import { SessionService } from './session.service';

/**
 * Acesso à Auditoria (spec 017 D-006) e a Configurações (spec 022 D-002) segundo o Keycloak, não a lista de scopes do
 * registro do ambiente. Carregado uma vez por sessão; decide os itens de menu e o atalho do detalhe do container.
 */
@Injectable({ providedIn: 'root' })
export class AuditAccessService {
  private readonly api = inject(ControlPlaneApiService);
  private readonly session = inject(SessionService);
  readonly summary = signal<AuditSummary | null>(null);
  /** `environment:manage` em algum host. */
  readonly settings = signal(false);

  constructor() {
    effect(() => {
      if (this.session.state() !== 'authenticated') return;
      untracked(() => void this.refresh());
    });
  }

  canRead(): boolean {
    return (this.summary()?.readableHosts ?? 0) > 0;
  }

  async refresh(): Promise<void> {
    void getSettingsAccess({ throwOnError: true }).then(
      r => this.settings.set(r.data.allowed),
      () => this.settings.set(false),
    );
    try {
      this.summary.set(await this.api.getAuditSummary());
    } catch {
      this.summary.set({ readableHosts: 0, oldestEventAt: null });
    }
  }
}
