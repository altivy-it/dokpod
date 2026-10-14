import { computed, inject, Injectable, signal } from '@angular/core';
import { newUuid } from '../../core/uuid';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import { ImageLookupService } from '../../data-access/image-lookup.service';
import { pullFailureLabel } from '../../data-access/image-reference';

export type PullState = 'queued' | 'pending' | 'dispatched' | 'accepted' | 'succeeded' | 'failed' | 'indeterminate';

export interface PullTarget {
  environmentId: string;
  environmentName: string;
  key: string;
  commandId: string | null;
  state: PullState;
  failure: string | null;
  digest: string | null;
  sizeBytes: number | null;
}

export interface PullRun {
  reference: string;
  startedAt: number;
  targets: PullTarget[];
}

const TERMINAL: PullState[] = ['succeeded', 'failed', 'indeterminate'];

/**
 * Envia um pull por host (chave de idempotência por host, retomável) e acompanha cada comando até um
 * estado terminal (spec 012 T011). Estado em memória da sessão: a tela pode ser reaberta sem perder o lote.
 */
@Injectable({ providedIn: 'root' })
export class ImagePullService {
  private readonly api = inject(ControlPlaneApiService);
  private readonly lookup = inject(ImageLookupService);
  private pollTimer: ReturnType<typeof setTimeout> | null = null;

  readonly run = signal<PullRun | null>(null);
  readonly running = computed(() => this.run()?.targets.some(t => !TERMINAL.includes(t.state)) ?? false);
  readonly summary = computed(() => {
    const targets = this.run()?.targets ?? [];
    return {
      succeeded: targets.filter(t => t.state === 'succeeded').length,
      failed: targets.filter(t => t.state === 'failed').length,
      indeterminate: targets.filter(t => t.state === 'indeterminate').length,
      pending: targets.filter(t => !TERMINAL.includes(t.state)).length,
    };
  });
  /** Tempo do envio até o último host terminar (medição de SC-002). */
  readonly elapsedMs = signal<number | null>(null);

  async start(reference: string, hosts: { environmentId: string; name: string }[]): Promise<void> {
    if (this.running()) throw new Error('Aguarde o download em andamento terminar.');
    this.elapsedMs.set(null);
    this.run.set({
      reference,
      startedAt: Date.now(),
      targets: hosts.map(h => ({
        environmentId: h.environmentId,
        environmentName: h.name,
        key: newUuid(),
        commandId: null,
        state: 'queued',
        failure: null,
        digest: null,
        sizeBytes: null,
      })),
    });
    await this.submitQueued();
    this.schedulePoll();
  }

  /** Reenvia hosts que não chegaram a ser aceitos, com a mesma chave (idempotente). */
  async retryUnsubmitted(): Promise<void> {
    await this.submitQueued();
    this.schedulePoll();
  }

  dismiss(): void {
    if (this.running()) return;
    if (this.pollTimer) clearTimeout(this.pollTimer);
    this.run.set(null);
  }

  private async submitQueued(): Promise<void> {
    const run = this.run();
    if (!run) return;
    await Promise.all(
      run.targets
        .filter(t => t.state === 'queued')
        .map(async target => {
          try {
            const commandId = await this.api.pullImage(target.environmentId, run.reference, target.key);
            this.patch(target.key, { commandId, state: 'pending' });
          } catch (error: unknown) {
            const code = (error as { code?: string; status?: number } | null)?.code;
            const status = (error as { status?: number } | null)?.status;
            // Falha de rede (sem status) mantém "na fila" para reenvio; respostas do servidor são definitivas.
            if (status) this.patch(target.key, { state: 'failed', failure: pullFailureLabel(code ?? (status === 403 ? 'authorization_denied' : null)) });
          }
        }),
    );
  }

  private schedulePoll(): void {
    if (this.pollTimer) clearTimeout(this.pollTimer);
    if (!this.running()) {
      this.finish();
      return;
    }
    this.pollTimer = setTimeout(() => void this.poll(), 1500);
  }

  private async poll(): Promise<void> {
    const run = this.run();
    if (!run) return;
    await Promise.all(
      run.targets
        .filter(t => t.commandId && !TERMINAL.includes(t.state))
        .map(async target => {
          try {
            const status = await this.api.getCommand(target.environmentId, target.commandId!);
            this.patch(target.key, {
              state: status.state,
              failure: status.state === 'succeeded' ? null : status.failureCode ? pullFailureLabel(status.failureCode) : null,
              digest: status.result?.resolvedImageDigest ?? null,
              sizeBytes: status.result?.imageSizeBytes ?? null,
            });
            if (status.state === 'succeeded') this.lookup.invalidate(target.environmentId);
          } catch {
            // Mantém o estado; a próxima consulta tenta de novo.
          }
        }),
    );
    this.schedulePoll();
  }

  private finish(): void {
    const run = this.run();
    if (run && this.elapsedMs() === null) this.elapsedMs.set(Date.now() - run.startedAt);
  }

  private patch(key: string, changes: Partial<PullTarget>): void {
    this.run.update(run => (run ? { ...run, targets: run.targets.map(t => (t.key === key ? { ...t, ...changes } : t)) } : run));
  }
}
