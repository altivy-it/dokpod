import { computed, inject, Injectable, signal } from '@angular/core';
import { newUuid } from '../../core/uuid';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { ContainerCommandStatus, PodAction, PodItem } from '../../data-access/generated/control-plane';
import { PodsApiService } from './pods-api.service';

export const POD_BATCH_LIMIT = 50;
const CONCURRENCY = 5;
const STORAGE_KEY = 'dokpod.pod-batch.v1';
const TERMINAL = new Set<string>(['succeeded', 'failed', 'indeterminate']);

export type PodBatchState = 'queued' | 'submitting' | ContainerCommandStatus['state'];

export interface PodBatchItem {
  environmentId: string;
  environmentName: string;
  podId: string;
  name: string;
  idempotencyKey: string;
  commandId: string | null;
  state: PodBatchState;
  failureCode: string | null;
  /** Membros fora do estado esperado (falha parcial, FR-008). */
  failedContainers: string[];
}

export interface PodBatch {
  id: string;
  action: PodAction;
  justification: string | null;
  startedAt: string;
  items: PodBatchItem[];
}

export interface PodEligibility {
  eligible: PodItem[];
  skipped: { item: PodItem; reason: string }[];
}

/** Motivo pelo qual o pod fica fora da ação (lista de afetados e ignorados). */
export function podIneligibleReason(item: PodItem, action: PodAction): string | null {
  if (item.allowedActions.includes(action)) return null;
  if (item.agentPod && action !== 'start' && action !== 'unpause') return 'pod do agente (protegido)';
  if (!item.hostConnected) return 'host desconectado';
  if (action === 'start' && item.status === 'running') return 'já está em execução';
  if (action === 'stop' && (item.status === 'exited' || item.status === 'created')) return 'já está parado';
  if (action === 'pause' && item.status !== 'running' && item.status !== 'degraded') return 'não está em execução';
  if (action === 'unpause' && item.status !== 'paused') return 'não está pausado';
  return 'sem permissão ou agente sem suporte';
}

export function evaluatePodEligibility(items: PodItem[], action: PodAction): PodEligibility {
  const eligible: PodItem[] = [];
  const skipped: PodEligibility['skipped'] = [];
  for (const item of items) {
    const reason = podIneligibleReason(item, action);
    if (reason) skipped.push({ item, reason });
    else eligible.push(item);
  }
  return { eligible, skipped };
}

/** Texto dos códigos de resultado (FR-008). */
export function podFailureText(code: string | null): string {
  switch (code) {
    case 'pod_partial_failure':
      return 'parte dos containers não mudou de estado';
    case 'pod_not_found':
      return 'não encontrado';
    case 'agent_pod':
      return 'pod do agente (protegido)';
    case 'pod_action_failed':
      return 'recusado pelo engine';
    case 'justification_required':
      return 'justificativa obrigatória neste estágio';
    case 'justification_invalid':
      return 'justificativa inválida';
    case 'authorization_denied':
      return 'sem permissão';
    case 'agent_disconnected':
      return 'agente desconectado';
    case 'deadline_exceeded':
      return 'prazo esgotado';
    case null:
    case '':
      return 'falha';
    default:
      return code;
  }
}

/**
 * Ações em pods (spec 021 US3): um comando por pod, até 50, concorrência 5. Chaves e IDs ficam no
 * `sessionStorage` da aba; após recarregar, o acompanhamento retoma pelo servidor sem reenviar automaticamente.
 */
@Injectable({ providedIn: 'root' })
export class PodActionsService {
  private readonly api = inject(PodsApiService);
  private readonly commands = inject(ControlPlaneApiService);
  private tracking = false;

  readonly batch = signal<PodBatch | null>(this.restore());
  /** Há comando enviado sem resultado terminal (ou envio em curso); itens nunca enviados não prendem o lote. */
  readonly running = computed(() => this.batch()?.items.some(i => i.state === 'submitting' || (i.commandId !== null && !TERMINAL.has(i.state))) ?? false);
  readonly summary = computed(() => {
    const items = this.batch()?.items ?? [];
    return {
      total: items.length,
      succeeded: items.filter(i => i.state === 'succeeded').length,
      failed: items.filter(i => i.state === 'failed').length,
      indeterminate: items.filter(i => i.state === 'indeterminate').length,
      pending: items.filter(i => !TERMINAL.has(i.state)).length,
      unsubmitted: items.filter(i => i.commandId === null && i.state === 'queued').length,
    };
  });

  constructor() {
    if (this.running()) void this.track();
  }

  async run(action: PodAction, items: PodItem[], justification: string | null): Promise<PodBatch> {
    if (items.length === 0) throw new Error('Nenhum pod elegível.');
    if (items.length > POD_BATCH_LIMIT) throw new Error(`O lote aceita no máximo ${POD_BATCH_LIMIT} pods.`);
    if (this.running()) throw new Error('Aguarde a remoção em andamento terminar.');
    this.save({
      id: newUuid(),
      action,
      justification,
      startedAt: new Date().toISOString(),
      items: items.map(item => ({
        environmentId: item.environmentId,
        environmentName: item.environmentName,
        podId: item.podId,
        name: item.name,
        idempotencyKey: newUuid(),
        commandId: null,
        state: 'queued',
        failureCode: null,
        failedContainers: [],
      })),
    });
    await this.submitQueued();
    void this.track();
    return this.batch()!;
  }

  /** Reenvia itens sem `commandId` com a mesma chave de idempotência (não duplica comandos). */
  async retryUnsubmitted(): Promise<void> {
    await this.submitQueued();
    void this.track();
  }

  dismiss(): void {
    if (this.running()) return;
    this.save(null);
  }

  private async submitQueued(): Promise<void> {
    const batch = this.batch();
    if (!batch) return;
    const queue = batch.items.filter(i => i.commandId === null && i.state === 'queued');
    const worker = async () => {
      for (let item = queue.shift(); item; item = queue.shift()) {
        if (this.batch()?.id !== batch.id) return;
        this.patch(item, { state: 'submitting' });
        try {
          const commandId = await this.api.submitAction(item.environmentId, item.podId, batch.action, item.idempotencyKey, batch.justification);
          this.patch(item, { commandId, state: 'pending' });
        } catch (error: unknown) {
          const problem = error as { code?: string; status?: number } | null;
          // Sem resposta do servidor (rede): volta para a fila, reenviável com a mesma chave.
          if (!problem?.status) this.patch(item, { state: 'queued' });
          else this.patch(item, { state: 'failed', failureCode: problem.code ?? (problem.status === 403 ? 'authorization_denied' : 'command_submission_failed') });
        }
      }
    };
    await Promise.all(Array.from({ length: CONCURRENCY }, worker));
  }

  private async track(): Promise<void> {
    if (this.tracking) return;
    this.tracking = true;
    try {
      while (this.running()) {
        const open = this.batch()!.items.filter(i => i.commandId && !TERMINAL.has(i.state));
        if (open.length === 0) break;
        await Promise.all(
          open.map(async item => {
            try {
              const status = await this.commands.getCommand(item.environmentId, item.commandId!);
              this.patch(item, {
                state: status.state,
                failureCode: status.state === 'succeeded' ? null : (status.failureCode ?? null),
                failedContainers: status.result?.failedContainers ?? [],
              });
            } catch {
              // Falha transitória de consulta: tenta de novo no próximo ciclo.
            }
          }),
        );
        if (!this.running()) break;
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
    } finally {
      this.tracking = false;
    }
  }

  private patch(target: Pick<PodBatchItem, 'environmentId' | 'podId'>, changes: Partial<PodBatchItem>): void {
    const batch = this.batch();
    if (!batch) return;
    this.save({
      ...batch,
      items: batch.items.map(i => (i.environmentId === target.environmentId && i.podId === target.podId ? { ...i, ...changes } : i)),
    });
  }

  private save(batch: PodBatch | null): void {
    this.batch.set(batch);
    try {
      if (batch) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(batch));
      else sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Sem sessionStorage: o lote continua apenas em memória.
    }
  }

  private restore(): PodBatch | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const batch = JSON.parse(raw) as PodBatch;
      // Itens interrompidos no envio voltam a "na fila": não são reenviados sozinhos (FR-007).
      return { ...batch, items: batch.items.map(i => (i.state === 'submitting' ? { ...i, state: 'queued' } : i)) };
    } catch {
      return null;
    }
  }
}
