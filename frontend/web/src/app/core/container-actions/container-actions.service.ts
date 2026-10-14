import { computed, inject, Injectable, signal } from '@angular/core';
import { ControlPlaneApiService, type ContainerAction } from '../../data-access/control-plane-api.service';
import type { ContainerCommandStatus, ContainerListItem } from '../../data-access/generated/control-plane';
import { newUuid } from '../uuid';

export const ACTION_LABEL: Record<ContainerAction, { verb: string; done: string; icon: string }> = {
  start: { verb: 'Iniciar', done: 'iniciado', icon: 'play' },
  stop: { verb: 'Parar', done: 'parado', icon: 'pause' },
  restart: { verb: 'Reiniciar', done: 'reiniciado', icon: 'refresh' },
  pause: { verb: 'Pausar', done: 'pausado', icon: 'pause' },
  unpause: { verb: 'Retomar', done: 'retomado', icon: 'play' },
  delete: { verb: 'Remover', done: 'removido', icon: 'trash' },
};

export const BATCH_LIMIT = 50;
const CONCURRENCY = 5;
const STORAGE_KEY = 'dokpod.batch.v1';
const TERMINAL = new Set<ContainerCommandStatus['state']>(['succeeded', 'failed', 'indeterminate']);

export type BatchItemState = 'queued' | 'submitting' | ContainerCommandStatus['state'];

export interface BatchItem {
  environmentId: string;
  environmentName: string;
  containerId: string;
  name: string;
  revision: string;
  idempotencyKey: string;
  commandId: string | null;
  state: BatchItemState;
  failureCode: string | null;
}

export interface Batch {
  id: string;
  action: ContainerAction;
  startedAt: string;
  items: BatchItem[];
}

export interface Eligibility {
  eligible: ContainerListItem[];
  skipped: { item: ContainerListItem; reason: string }[];
}

/** Motivo pelo qual um container não entra na ação (spec 010 FR-006). */
export function ineligibleReason(item: ContainerListItem, action: ContainerAction): string | null {
  if (!item.hostConnected) return 'host desconectado';
  if (item.allowedActions.includes(action)) return null;
  const state = (item.state ?? '').toLowerCase();
  if (action === 'pause' && state !== 'running') return 'não está em execução';
  if (action === 'unpause' && state !== 'paused') return 'não está pausado';
  if (action === 'start' && state === 'running') return 'já está em execução';
  if (action === 'stop' && (state === 'exited' || state === 'created')) return 'já está parado';
  if (action === 'delete' && (state === 'running' || state === 'paused' || state === 'restarting')) return 'pare o container antes de remover';
  return 'sem permissão ou não suportado pelo agente';
}

export function evaluateEligibility(items: ContainerListItem[], action: ContainerAction): Eligibility {
  const eligible: ContainerListItem[] = [];
  const skipped: Eligibility['skipped'] = [];
  for (const item of items) {
    const reason = ineligibleReason(item, action);
    if (reason) skipped.push({ item, reason });
    else eligible.push(item);
  }
  return { eligible, skipped };
}

/**
 * Lotes de ações por container (spec 010 US2): um comando individual por container, concorrência 5,
 * chaves de idempotência e IDs persistidos em `sessionStorage` (escopo da aba); o acompanhamento
 * sempre consulta o servidor, e uma recarga retoma sem reenviar automaticamente.
 */
@Injectable({ providedIn: 'root' })
export class ContainerActionsService {
  private readonly api = inject(ControlPlaneApiService);
  readonly batch = signal<Batch | null>(this.restore());
  readonly running = computed(() => this.batch()?.items.some(i => !TERMINAL.has(i.state as ContainerCommandStatus['state'])) ?? false);
  readonly summary = computed(() => {
    const items = this.batch()?.items ?? [];
    return {
      total: items.length,
      succeeded: items.filter(i => i.state === 'succeeded').length,
      failed: items.filter(i => i.state === 'failed').length,
      indeterminate: items.filter(i => i.state === 'indeterminate').length,
      pending: items.filter(i => !TERMINAL.has(i.state as ContainerCommandStatus['state'])).length,
    };
  });

  constructor() {
    if (this.batch() && this.running()) void this.track();
  }

  async run(action: ContainerAction, items: ContainerListItem[]): Promise<Batch> {
    if (items.length === 0) throw new Error('Nenhum container elegível.');
    if (items.length > BATCH_LIMIT) throw new Error(`O lote aceita no máximo ${BATCH_LIMIT} containers.`);
    const batch: Batch = {
      id: newUuid(),
      action,
      startedAt: new Date().toISOString(),
      items: items.map(item => ({
        environmentId: item.environmentId,
        environmentName: item.environmentName,
        containerId: item.containerId,
        name: item.name,
        revision: item.revision,
        idempotencyKey: newUuid(),
        commandId: null,
        state: 'queued',
        failureCode: null,
      })),
    };
    this.save(batch);
    await this.submitAll(batch.id);
    void this.track();
    return this.batch()!;
  }

  /** Reenvia itens sem `commandId` com a mesma chave de idempotência (não duplica comandos). */
  async retryUnsubmitted(): Promise<void> {
    const batch = this.batch();
    if (!batch) return;
    await this.submitAll(batch.id);
    void this.track();
  }

  dismiss(): void {
    if (this.running()) return;
    this.save(null);
  }

  private async submitAll(batchId: string): Promise<void> {
    const queue = (this.batch()?.items ?? []).filter(i => i.commandId === null && i.state !== 'failed');
    const worker = async () => {
      for (let item = queue.shift(); item; item = queue.shift()) {
        if (this.batch()?.id !== batchId) return;
        this.patch(item.containerId, item.environmentId, { state: 'submitting' });
        try {
          const commandId = await this.api.submitContainerAction(
            item.environmentId,
            item.containerId,
            item.revision,
            this.batch()!.action,
            item.idempotencyKey,
          );
          this.patch(item.containerId, item.environmentId, { commandId, state: 'pending' });
        } catch (error: unknown) {
          const problem = error as { code?: string; status?: number };
          this.patch(item.containerId, item.environmentId, {
            state: 'failed',
            failureCode: problem.code ?? (problem.status === 403 ? 'authorization_denied' : 'command_submission_failed'),
          });
        }
      }
    };
    await Promise.all(Array.from({ length: CONCURRENCY }, worker));
  }

  private tracking = false;

  private async track(): Promise<void> {
    if (this.tracking) return;
    this.tracking = true;
    try {
      while (this.running()) {
        const batch = this.batch()!;
        const open = batch.items.filter(i => i.commandId && !TERMINAL.has(i.state as ContainerCommandStatus['state']));
        await Promise.all(
          open.map(async item => {
            try {
              const status = await this.api.getCommand(item.environmentId, item.commandId!);
              this.patch(item.containerId, item.environmentId, { state: status.state, failureCode: status.failureCode });
            } catch {
              // Falha transitória de consulta: tenta de novo no próximo ciclo.
            }
          }),
        );
        if (!this.running()) break;
        if (batch.items.some(i => i.commandId === null && (i.state === 'queued' || i.state === 'submitting'))) break;
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
    } finally {
      this.tracking = false;
    }
  }

  private patch(containerId: string, environmentId: string, changes: Partial<BatchItem>): void {
    const batch = this.batch();
    if (!batch) return;
    this.save({
      ...batch,
      items: batch.items.map(i => (i.containerId === containerId && i.environmentId === environmentId ? { ...i, ...changes } : i)),
    });
  }

  private save(batch: Batch | null): void {
    this.batch.set(batch);
    try {
      if (batch) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(batch));
      else sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Sem sessionStorage: o lote continua apenas em memória.
    }
  }

  private restore(): Batch | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const batch = JSON.parse(raw) as Batch;
      // Itens que estavam sendo enviados voltam a "na fila": a mesma chave evita duplicidade se forem reenviados.
      return { ...batch, items: batch.items.map(i => (i.state === 'submitting' ? { ...i, state: 'queued' } : i)) };
    } catch {
      return null;
    }
  }
}
