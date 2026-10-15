import { computed, inject, Injectable, signal } from '@angular/core';
import { newUuid } from '../../core/uuid';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { ContainerCommandStatus, VolumeItem } from '../../data-access/generated/control-plane';
import { VolumesApiService } from './volumes-api.service';

export const VOLUME_BATCH_LIMIT = 50;
const CONCURRENCY = 5;
const STORAGE_KEY = 'dokpod.volume-batch.v1';
const TERMINAL = new Set<string>(['succeeded', 'failed', 'indeterminate']);

export type VolumeBatchState = 'queued' | 'submitting' | ContainerCommandStatus['state'];

export interface VolumeBatchItem {
  environmentId: string;
  environmentName: string;
  name: string;
  idempotencyKey: string;
  commandId: string | null;
  state: VolumeBatchState;
  failureCode: string | null;
}

export interface VolumeBatch {
  id: string;
  justification: string | null;
  startedAt: string;
  items: VolumeBatchItem[];
}

export interface VolumeEligibility {
  eligible: VolumeItem[];
  skipped: { item: VolumeItem; reason: string }[];
}

/** Motivo pelo qual o volume fica fora da remoção (FR-006: listar afetados e ignorados). */
export function volumeIneligibleReason(item: VolumeItem): string | null {
  if (item.inUse) return item.mounts.length === 1 ? 'em uso por 1 container' : `em uso por ${item.mounts.length} containers`;
  if (!item.hostConnected) return 'host desconectado';
  if (!item.canRemove) return 'sem permissão ou agente sem suporte';
  return null;
}

export function evaluateVolumeEligibility(items: VolumeItem[]): VolumeEligibility {
  const eligible: VolumeItem[] = [];
  const skipped: VolumeEligibility['skipped'] = [];
  for (const item of items) {
    const reason = volumeIneligibleReason(item);
    if (reason) skipped.push({ item, reason });
    else eligible.push(item);
  }
  return { eligible, skipped };
}

/** Texto dos códigos padronizados de resultado (FR-008). */
export function volumeFailureText(code: string | null): string {
  switch (code) {
    case 'volume_in_use':
      return 'em uso';
    case 'volume_not_found':
      return 'não encontrado';
    case 'volume_remove_failed':
      return 'recusado pelo engine ou driver';
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
 * Remoção de volumes (spec 018 US2): um comando por volume, até 50, concorrência 5. Chaves e IDs ficam no
 * `sessionStorage` da aba; após recarregar, o acompanhamento retoma pelo servidor sem reenviar automaticamente.
 */
@Injectable({ providedIn: 'root' })
export class VolumeRemovalService {
  private readonly api = inject(VolumesApiService);
  private readonly commands = inject(ControlPlaneApiService);
  private tracking = false;

  readonly batch = signal<VolumeBatch | null>(this.restore());
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

  async run(items: VolumeItem[], justification: string | null): Promise<VolumeBatch> {
    if (items.length === 0) throw new Error('Nenhum volume elegível.');
    if (items.length > VOLUME_BATCH_LIMIT) throw new Error(`O lote aceita no máximo ${VOLUME_BATCH_LIMIT} volumes.`);
    if (this.running()) throw new Error('Aguarde a remoção em andamento terminar.');
    this.save({
      id: newUuid(),
      justification,
      startedAt: new Date().toISOString(),
      items: items.map(item => ({
        environmentId: item.environmentId,
        environmentName: item.environmentName,
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
          const commandId = await this.api.removeVolume(item.environmentId, item.name, item.idempotencyKey, batch.justification);
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

  private patch(target: Pick<VolumeBatchItem, 'environmentId' | 'name'>, changes: Partial<VolumeBatchItem>): void {
    const batch = this.batch();
    if (!batch) return;
    this.save({
      ...batch,
      items: batch.items.map(i => (i.environmentId === target.environmentId && i.name === target.name ? { ...i, ...changes } : i)),
    });
  }

  private save(batch: VolumeBatch | null): void {
    this.batch.set(batch);
    try {
      if (batch) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(batch));
      else sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Sem sessionStorage: o lote continua apenas em memória.
    }
  }

  private restore(): VolumeBatch | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const batch = JSON.parse(raw) as VolumeBatch;
      // Itens interrompidos no envio voltam a "na fila": não são reenviados sozinhos (FR-007).
      return { ...batch, items: batch.items.map(i => (i.state === 'submitting' ? { ...i, state: 'queued' } : i)) };
    } catch {
      return null;
    }
  }
}
