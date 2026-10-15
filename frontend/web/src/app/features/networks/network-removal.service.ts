import { computed, inject, Injectable, signal } from '@angular/core';
import { newUuid } from '../../core/uuid';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { ContainerCommandStatus, NetworkItem } from '../../data-access/generated/control-plane';
import { NetworksApiService } from './networks-api.service';

export const NETWORK_BATCH_LIMIT = 50;
const CONCURRENCY = 5;
const STORAGE_KEY = 'dokpod.network-batch.v1';
const TERMINAL = new Set<string>(['succeeded', 'failed', 'indeterminate']);

export type NetworkBatchState = 'queued' | 'submitting' | ContainerCommandStatus['state'];

export interface NetworkBatchItem {
  environmentId: string;
  environmentName: string;
  networkId: string;
  name: string;
  idempotencyKey: string;
  commandId: string | null;
  state: NetworkBatchState;
  failureCode: string | null;
}

export interface NetworkBatch {
  id: string;
  justification: string | null;
  startedAt: string;
  items: NetworkBatchItem[];
}

export interface NetworkEligibility {
  eligible: NetworkItem[];
  skipped: { item: NetworkItem; reason: string }[];
}

/** Motivo pelo qual a rede fica fora da remoção (FR-006: listar afetadas e ignoradas). */
export function networkIneligibleReason(item: NetworkItem): string | null {
  if (item.system) return 'rede do sistema';
  if (item.scope !== 'local') return 'rede de cluster';
  if (item.inUse) return item.containers.length === 1 ? 'em uso por 1 container' : `em uso por ${item.containers.length} containers`;
  if (!item.hostConnected) return 'host desconectado';
  if (!item.canRemove) return 'sem permissão ou agente sem suporte';
  return null;
}

export function evaluateNetworkEligibility(items: NetworkItem[]): NetworkEligibility {
  const eligible: NetworkItem[] = [];
  const skipped: NetworkEligibility['skipped'] = [];
  for (const item of items) {
    const reason = networkIneligibleReason(item);
    if (reason) skipped.push({ item, reason });
    else eligible.push(item);
  }
  return { eligible, skipped };
}

/** Texto dos códigos padronizados de resultado (FR-008). */
export function networkFailureText(code: string | null): string {
  switch (code) {
    case 'network_in_use':
      return 'em uso';
    case 'network_not_found':
      return 'não encontrada';
    case 'network_system':
      return 'rede do sistema';
    case 'network_not_local':
      return 'rede de cluster';
    case 'network_remove_failed':
      return 'recusada pelo engine ou driver';
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
 * Remoção de redes (spec 019 US3): um comando por rede, até 50, concorrência 5. Chaves e IDs ficam no
 * `sessionStorage` da aba; após recarregar, o acompanhamento retoma pelo servidor sem reenviar automaticamente.
 */
@Injectable({ providedIn: 'root' })
export class NetworkRemovalService {
  private readonly api = inject(NetworksApiService);
  private readonly commands = inject(ControlPlaneApiService);
  private tracking = false;

  readonly batch = signal<NetworkBatch | null>(this.restore());
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

  async run(items: NetworkItem[], justification: string | null): Promise<NetworkBatch> {
    if (items.length === 0) throw new Error('Nenhuma rede elegível.');
    if (items.length > NETWORK_BATCH_LIMIT) throw new Error(`O lote aceita no máximo ${NETWORK_BATCH_LIMIT} redes.`);
    if (this.running()) throw new Error('Aguarde a remoção em andamento terminar.');
    this.save({
      id: newUuid(),
      justification,
      startedAt: new Date().toISOString(),
      items: items.map(item => ({
        environmentId: item.environmentId,
        environmentName: item.environmentName,
        networkId: item.networkId,
        name: item.name,
        idempotencyKey: newUuid(),
        commandId: null,
        state: 'queued',
        failureCode: null,
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
          const commandId = await this.api.removeNetwork(item.environmentId, item.networkId, item.idempotencyKey, batch.justification);
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
              this.patch(item, { state: status.state, failureCode: status.state === 'succeeded' ? null : (status.failureCode ?? null) });
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

  private patch(target: Pick<NetworkBatchItem, 'environmentId' | 'networkId'>, changes: Partial<NetworkBatchItem>): void {
    const batch = this.batch();
    if (!batch) return;
    this.save({
      ...batch,
      items: batch.items.map(i => (i.environmentId === target.environmentId && i.networkId === target.networkId ? { ...i, ...changes } : i)),
    });
  }

  private save(batch: NetworkBatch | null): void {
    this.batch.set(batch);
    try {
      if (batch) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(batch));
      else sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Sem sessionStorage: o lote continua apenas em memória.
    }
  }

  private restore(): NetworkBatch | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const batch = JSON.parse(raw) as NetworkBatch;
      // Itens interrompidos no envio voltam a "na fila": não são reenviados sozinhos (FR-007).
      return { ...batch, items: batch.items.map(i => (i.state === 'submitting' ? { ...i, state: 'queued' } : i)) };
    } catch {
      return null;
    }
  }
}
