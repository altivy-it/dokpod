import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ControlPlaneApiService } from '../data-access/control-plane-api.service';
import type { AuditSummary } from '../data-access/generated/control-plane';
import { SessionService } from './session.service';

/**
 * Acesso à Auditoria (spec 017 D-006): hosts com `audit:read` segundo o Keycloak, não a lista de scopes do
 * registro do ambiente. Carregado uma vez por sessão; decide o item de menu e o atalho do detalhe do container.
 */
@Injectable({ providedIn: 'root' })
export class AuditAccessService {
  private readonly api = inject(ControlPlaneApiService);
  private readonly session = inject(SessionService);
  readonly summary = signal<AuditSummary | null>(null);

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
    try {
      this.summary.set(await this.api.getAuditSummary());
    } catch {
      this.summary.set({ readableHosts: 0, oldestEventAt: null });
    }
  }
}
