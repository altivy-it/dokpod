import { inject, Injectable, signal } from '@angular/core';
import { ControlPlaneApiService, type ContainerAction } from '../data-access/control-plane-api.service';
import type { Stage } from '../data-access/generated/control-plane';

export type JustificationMode = 'required' | 'optional' | 'none';

const JUSTIFIED: ReadonlySet<ContainerAction> = new Set<ContainerAction>(['stop', 'pause', 'delete']);
export const JUSTIFICATION_MIN = 10;
export const JUSTIFICATION_MAX = 500;

/** Exigência para a ação: parar, pausar e remover pedem justificativa; obrigatória onde a política do estágio manda. */
export function justificationMode(action: ContainerAction, stages: readonly (Stage | null | undefined)[], required: ReadonlySet<Stage>): JustificationMode {
  if (!JUSTIFIED.has(action)) return 'none';
  return stages.some(stage => stage && required.has(stage)) ? 'required' : 'optional';
}

export function justificationValid(text: string, mode: JustificationMode): boolean {
  const length = text.trim().length;
  if (mode === 'none') return true;
  if (length === 0) return mode === 'optional';
  return length >= JUSTIFICATION_MIN && length <= JUSTIFICATION_MAX;
}

/** Política de justificativa por estágio (spec 016 FR-014); carregada uma vez e recarregada após mudança. */
@Injectable({ providedIn: 'root' })
export class JustificationPolicyService {
  private readonly api = inject(ControlPlaneApiService);
  private loading: Promise<void> | null = null;
  /** Estágios com justificativa obrigatória; Produção por padrão até a primeira leitura. */
  readonly required = signal<ReadonlySet<Stage>>(new Set<Stage>(['production']));
  readonly loaded = signal(false);

  load(force = false): Promise<void> {
    if (this.loading && !force) return this.loading;
    this.loading = this.api
      .listJustificationPolicies()
      .then(page => {
        this.required.set(new Set(page.policies.filter(p => p.required).map(p => p.stage)));
        this.loaded.set(true);
      })
      .catch(() => {
        // Sem a política: mantém o padrão; a API recusa com justification_required se preciso.
        this.loading = null;
      });
    return this.loading;
  }

  async set(stage: Stage, required: boolean): Promise<void> {
    await this.api.setJustificationPolicy(stage, required);
    await this.load(true);
  }
}
