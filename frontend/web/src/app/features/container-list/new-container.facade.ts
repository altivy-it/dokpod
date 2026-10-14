import { computed, inject, Injectable, signal } from '@angular/core';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { newUuid } from '../../core/uuid';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { ContainerCreateOptions, ContainerCreationRequest, ContainerListItem, EnvironmentState, ImageLookup } from '../../data-access/generated/control-plane';
import { ImageLookupService } from '../../data-access/image-lookup.service';
import { parseImageReference } from '../../data-access/image-reference';
import { StageFilterService } from '../../shell/stage-filter.service';

export type CreationState = 'idle' | 'submitting' | 'pending' | 'dispatched' | 'accepted' | 'succeeded' | 'failed' | 'indeterminate';

/** Estado do envio: nunca contém valores de variáveis (FR-004); apenas ids, estado e códigos. */
export interface CreationTracking {
  environmentId: string;
  commandId: string | null;
  state: CreationState;
  failureCode: string | null;
  containerId: string | null;
}

export const CREATE_FAILURE: Record<string, string> = {
  name_conflict: 'Já existe um container com esse nome no host.',
  port_conflict: 'A porta do host já está em uso; o container criado foi removido.',
  privileged_port: 'O Podman rootless não publica portas abaixo de 1024; use uma porta maior. O container criado foi removido.',
  mount_not_allowed: 'O caminho do host não está na lista permitida do agente.',
  registry_not_allowed: 'O registro da imagem não é permitido para o estágio deste host.',
  registry_unauthorized: 'O registro recusou as credenciais configuradas no host.',
  secret_not_found: 'O segredo referenciado não existe no host.',
  network_not_found: 'A rede informada não existe no host.',
  image_pull_failed: 'Não foi possível baixar a imagem.',
  image_not_found: 'A imagem não foi encontrada.',
  start_failed: 'O container foi criado, mas não iniciou. Ele permanece parado na lista.',
  invalid_spec: 'A especificação foi recusada pelo agente.',
  anonymous_volume_with_auto_remove: 'A imagem declara VOLUME: monte um volume nomeado no mesmo destino ou desmarque a remoção automática.',
  unsupported_option: 'Opção não suportada por esta engine.',
  parameters_unavailable: 'Os parâmetros protegidos expiraram; envie novamente.',
  deadline_exceeded: 'A criação excedeu o prazo.',
  expired_command: 'O comando expirou antes de ser executado.',
  capability_not_supported: 'O agente deste host não suporta criação de containers.',
  not_supported: 'O agente deste host não suporta criação de containers.',
  agent_disconnected: 'O agente do host está desconectado.',
  authorization_denied: 'Sem permissão para criar containers neste host.',
  command_id_conflict: 'O pedido mudou depois do envio; envie novamente.',
};

export function createFailureLabel(code: string | null | undefined): string {
  return (code && CREATE_FAILURE[code]) ?? (code ? `Falha: ${code}` : 'Falha na criação.');
}

/** Motivo para o host não aceitar criação agora; nulo quando elegível (scope × capability × conexão). */
export function createIneligibility(env: EnvironmentState): string | null {
  if (!env.enabled) return 'host desabilitado';
  if (!env.scopes.includes('container:create')) return 'sem permissão container:create';
  if (!env.agent?.capabilities.includes('ContainerCreate')) return 'agente sem suporte a criação';
  if (!env.agent.connected) return 'agente desconectado';
  return null;
}

const TERMINAL: CreationState[] = ['succeeded', 'failed', 'indeterminate'];

/**
 * Criação de container (spec 013 T015): opções do host, validação contra o inventário, envio idempotente e
 * acompanhamento até o estado terminal. A chave de idempotência é mantida em retry de rede e renovada quando o
 * formulário muda depois de uma resposta do servidor.
 */
@Injectable()
export class NewContainerFacade {
  private readonly api = inject(ControlPlaneApiService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly stageFilter = inject(StageFilterService);
  private readonly lookups = inject(ImageLookupService);
  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private idempotencyKey: string | null = null;
  private lookupSeq = 0;

  readonly hosts = computed(() =>
    this.directory
      .environments()
      .filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage())
      .filter(env => env.agent?.capabilities.includes('ContainerCreate') && env.scopes.includes('container:create')),
  );
  readonly cpu = signal<Record<string, number | null>>({});
  readonly options = signal<ContainerCreateOptions | null>(null);
  readonly optionsError = signal<string | null>(null);
  readonly inventory = signal<ContainerListItem[]>([]);
  readonly lookup = signal<ImageLookup | null>(null);
  readonly lookupError = signal<string | null>(null);
  readonly tracking = signal<CreationTracking | null>(null);
  readonly serverErrors = signal<Record<string, string[]>>({});
  readonly running = computed(() => {
    const state = this.tracking()?.state;
    return !!state && state !== 'idle' && !TERMINAL.includes(state);
  });

  async init(): Promise<void> {
    await this.directory.load();
    try {
      const metrics = await this.api.listHostMetrics(this.stageFilter.stage());
      this.cpu.set(Object.fromEntries(metrics.hosts.map(h => [h.environmentId, h.cpuPercent ?? null])));
    } catch {
      this.cpu.set({});
    }
  }

  /** Carrega segredos/redes/volumes do host e o inventário para validar nome e porta antes do envio. */
  async selectHost(environmentId: string): Promise<void> {
    this.options.set(null);
    this.optionsError.set(null);
    this.inventory.set([]);
    const env = this.directory.byId(environmentId);
    if (!env || createIneligibility(env)) return;
    const [options, inventory] = await Promise.allSettled([
      this.api.getContainerCreateOptions(environmentId),
      this.api.listAllContainers({ environmentId: [environmentId], limit: 100 }),
    ]);
    if (options.status === 'fulfilled') this.options.set(options.value);
    else this.optionsError.set('Não foi possível consultar segredos e volumes do host.');
    if (inventory.status === 'fulfilled') this.inventory.set(inventory.value.containers);
  }

  async lookupImage(environmentId: string | null, reference: string): Promise<void> {
    const seq = ++this.lookupSeq;
    this.lookup.set(null);
    this.lookupError.set(null);
    if (!environmentId || !parseImageReference(reference.trim())) return;
    try {
      const result = await this.lookups.lookup(environmentId, reference);
      if (seq === this.lookupSeq) this.lookup.set(result);
    } catch {
      if (seq === this.lookupSeq) this.lookupError.set('Não foi possível verificar a imagem no host.');
    }
  }

  nameInUse(name: string): boolean {
    return this.inventory().some(c => c.name === name);
  }

  portInUse(hostPort: number, protocol: string): boolean {
    return this.inventory().some(c => (c.ports ?? []).some(p => p.hostPort === hostPort && p.protocol === protocol));
  }

  /** O formulário mudou depois de uma resposta: o próximo envio é uma nova intenção. */
  formChanged(): void {
    if (!this.running()) this.idempotencyKey = null;
    this.serverErrors.set({});
  }

  async submit(environmentId: string, request: ContainerCreationRequest): Promise<void> {
    if (this.running()) return;
    this.idempotencyKey ??= newUuid();
    this.serverErrors.set({});
    this.tracking.set({ environmentId, commandId: null, state: 'submitting', failureCode: null, containerId: null });
    try {
      const accepted = await this.api.createContainer(environmentId, request, this.idempotencyKey);
      this.tracking.set({ environmentId, commandId: accepted.commandId, state: 'pending', failureCode: null, containerId: null });
      this.schedulePoll();
    } catch (error: unknown) {
      const problem = error as { status?: number; code?: string; errors?: Record<string, string[]> } | null;
      if (!problem?.status) {
        // Falha de rede: mantém a chave para o retry não criar um segundo container.
        this.tracking.set({ environmentId, commandId: null, state: 'idle', failureCode: 'network', containerId: null });
        return;
      }
      this.idempotencyKey = null;
      if (problem.errors) this.serverErrors.set(problem.errors);
      this.tracking.set({ environmentId, commandId: null, state: 'failed', failureCode: problem.code ?? (problem.status === 403 ? 'authorization_denied' : 'invalid_spec'), containerId: null });
    }
  }

  reset(): void {
    if (this.pollTimer) clearTimeout(this.pollTimer);
    this.tracking.set(null);
    this.idempotencyKey = null;
  }

  dispose(): void {
    if (this.pollTimer) clearTimeout(this.pollTimer);
  }

  private schedulePoll(): void {
    if (this.pollTimer) clearTimeout(this.pollTimer);
    if (!this.running()) return;
    this.pollTimer = setTimeout(() => void this.poll(), 1200);
  }

  private async poll(): Promise<void> {
    const current = this.tracking();
    if (!current?.commandId) return;
    try {
      const status = await this.api.getCommand(current.environmentId, current.commandId);
      this.tracking.set({
        ...current,
        state: status.state,
        failureCode: status.state === 'succeeded' ? null : (status.failureCode ?? null),
        containerId: status.result?.createdContainerId ?? null,
      });
      if (TERMINAL.includes(status.state)) {
        this.idempotencyKey = null;
        this.lookups.invalidate(current.environmentId);
      }
    } catch {
      // Mantém o estado; tenta de novo.
    }
    this.schedulePoll();
  }
}
