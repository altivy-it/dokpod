import { inject, Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, Subject } from '@microsoft/signalr';
import { SessionService } from './session.service';
import { controlPlaneHubUrl, type ControlPlaneRealtimeStatus } from './control-plane-realtime.service';

export interface TerminalInput {
  data?: string;
  resize?: { cols: number; rows: number };
}

export interface TerminalOutput {
  data?: string;
  status?: { state: 'connecting' | 'connected' | 'closed' | 'error'; reason?: string | null; shell?: string | null; containerUserIsRoot?: boolean; warning?: string | null; sessionId?: string | null };
}

export type InvalidationKind = 'inventory' | 'host' | 'events' | 'images';

export interface LiveLogLine {
  timestamp: string | null;
  stream: 'stdout' | 'stderr';
  level: string | null;
  text: string;
}

export interface LiveLogChunk {
  sessionId: string;
  lines: LiveLogLine[];
  omittedLines: number;
  ended: boolean;
  endReason: string | null;
}

export interface Invalidation {
  environmentId: string;
  kind: InvalidationKind;
  sequence: number;
}

/**
 * Uma conexão SignalR para o console: entra nos grupos dos ambientes observados e publica
 * invalidações por tipo (spec 008 FR-013). As páginas buscam o estado pela API ao receber.
 */
@Injectable({ providedIn: 'root' })
export class ConsoleRealtimeService {
  private readonly session = inject(SessionService);
  readonly status = signal<ControlPlaneRealtimeStatus>('disconnected');
  readonly lastInvalidation = signal<Invalidation | null>(null);

  private connection: HubConnection | null = null;
  private starting: Promise<HubConnection | null> | null = null;
  private readonly joined = new Set<string>();
  private sequence = 0;

  /** Garante a assinatura dos ambientes informados (adiciona os novos; mantém os anteriores). */
  async watch(environmentIds: readonly string[]): Promise<void> {
    const connection = await this.ensureConnected();
    if (!connection) return;
    for (const environmentId of environmentIds) {
      if (this.joined.has(environmentId)) continue;
      try {
        await connection.invoke('JoinEnvironmentAsync', environmentId);
        this.joined.add(environmentId);
      } catch {
        // Ambiente sem permissão de leitura: ignorado sem revelar existência.
      }
    }
  }

  /** Logs ao vivo (spec 011): stream do hub; cancelar a assinatura encerra a sessão no agente. */
  async streamLogs(
    environmentId: string,
    containerId: string,
    contains: string | null,
    handlers: { next: (chunk: LiveLogChunk) => void; error: (error: unknown) => void; complete: () => void },
  ): Promise<{ dispose: () => void } | null> {
    const connection = await this.ensureConnected();
    if (!connection) return null;
    const subscription = connection.stream<LiveLogChunk>('StreamContainerLogs', environmentId, containerId, contains).subscribe(handlers);
    return subscription;
  }

  /**
   * Terminal (spec 014): stream bidirecional no hub. `send` envia entrada (base64) ou redimensionamento; `dispose`
   * completa a entrada (o servidor encerra a sessão com `user_closed`) e cancela a assinatura.
   */
  async openTerminal(
    request: { environmentId: string; containerId: string; cols: number; rows: number; justification: string | null },
    handlers: { next: (frame: TerminalOutput) => void; error: (error: unknown) => void; complete: () => void },
  ): Promise<{ send: (input: TerminalInput) => void; dispose: () => void } | null> {
    const connection = await this.ensureConnected();
    if (!connection) return null;
    const input = new Subject<TerminalInput>();
    const subscription = connection.stream<TerminalOutput>('OpenTerminal', request, input).subscribe(handlers);
    let open = true;
    return {
      send: value => {
        if (open) input.next(value);
      },
      dispose: () => {
        if (!open) return;
        open = false;
        input.complete();
        setTimeout(() => subscription.dispose(), 3000);
      },
    };
  }

  /** Reabre a conexão (o BFF renova o token na negociação); assinaturas de ambiente são refeitas. */
  async reconnect(): Promise<void> {
    const ids = [...this.joined];
    await this.stop();
    await this.watch(ids);
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.joined.clear();
    if (connection && connection.state !== HubConnectionState.Disconnected) await connection.stop();
    this.status.set('disconnected');
  }

  private ensureConnected(): Promise<HubConnection | null> {
    if (this.connection?.state === HubConnectionState.Connected) return Promise.resolve(this.connection);
    this.starting ??= this.start().finally(() => (this.starting = null));
    return this.starting;
  }

  private async start(): Promise<HubConnection | null> {
    try {
      const requestToken = await this.session.antiforgeryToken();
      const connection = new HubConnectionBuilder()
        .withUrl(controlPlaneHubUrl(document.baseURI), {
          headers: { 'X-Dokpod-Antiforgery': requestToken },
          withCredentials: true,
        })
        .withAutomaticReconnect()
        .build();
      connection.on('invalidated', (n: { environmentId: string; kind: InvalidationKind }) => this.publish(n.environmentId, n.kind));
      connection.on('inventoryChanged', (n: { environmentId: string }) => this.publish(n.environmentId, 'inventory'));
      connection.onreconnecting(() => this.status.set('connecting'));
      connection.onreconnected(() => {
        const ids = [...this.joined];
        this.joined.clear();
        this.status.set('connected');
        void this.watch(ids);
      });
      connection.onclose(() => this.status.set('disconnected'));
      this.status.set('connecting');
      await connection.start();
      this.connection = connection;
      this.status.set('connected');
      return connection;
    } catch {
      this.status.set('error');
      return null;
    }
  }

  private publish(environmentId: string, kind: InvalidationKind): void {
    this.lastInvalidation.set({ environmentId, kind, sequence: ++this.sequence });
  }
}
