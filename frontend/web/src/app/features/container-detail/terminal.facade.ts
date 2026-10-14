import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { ConsoleRealtimeService, type TerminalOutput } from '../../core/console-realtime.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { TerminalPolicy } from '../../data-access/generated/control-plane';

export type TerminalState = 'idle' | 'loading' | 'blocked' | 'justification' | 'connecting' | 'connected' | 'closed' | 'error';

/** Textos dos motivos de recusa e término (data-model da spec 014). */
export const TERMINAL_REASON: Record<string, string> = {
  scope: 'Sem permissão para abrir terminal neste host.',
  not_supported: 'Terminal não suportado pelo agente deste host.',
  not_running: 'O container não está em execução.',
  agent_disconnected: 'O agente do host está desconectado.',
  agent_timeout: 'O agente não respondeu a tempo.',
  label_disabled: 'Terminal bloqueado por este container (label dokpod.exec=disabled).',
  limit: 'Limite de sessões de terminal atingido (por usuário, container ou host).',
  justification_missing: 'Informe uma justificativa de 10 a 500 caracteres.',
  mfa_required: 'É necessária autenticação multifator recente.',
  websocket_required: 'O terminal exige conexão WebSocket.',
  shell_not_available: 'Shell não disponível nesta imagem.',
  user_closed: 'Sessão encerrada.',
  exit: 'O shell foi encerrado.',
  idle_timeout: 'Sessão encerrada por inatividade.',
  max_duration: 'Sessão encerrada pelo limite de duração.',
  permission_revoked: 'Permissão revogada: sessão encerrada.',
  authorization_unavailable: 'Não foi possível revalidar a permissão: sessão encerrada.',
  session_refresh_required: 'A sessão de login expirou: abra o terminal de novo.',
  container_stopped: 'O container parou.',
  client_disconnected: 'Conexão perdida.',
  expired: 'O pedido de terminal expirou.',
  not_found: 'Container não encontrado.',
  error: 'Erro no terminal.',
  realtime_unavailable: 'Tempo real indisponível: não foi possível abrir o terminal.',
};

export function terminalReason(code: string | null | undefined): string {
  return (code && TERMINAL_REASON[code]) ?? (code ? `Encerrado: ${code}` : 'Sessão encerrada.');
}

export function encodeBase64(text: string): string {
  const bytes = new TextEncoder().encode(text);
  let binary = '';
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary);
}

export function decodeBase64(data: string): Uint8Array {
  const binary = atob(data);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

/**
 * Sessão de terminal (spec 014 T011): política, justificativa em Produção, stream do hub, renovação periódica da
 * autorização (D-008) e estados. Nenhum conteúdo da sessão é guardado fora do emulador.
 */
@Injectable()
export class TerminalFacade {
  private readonly api = inject(ControlPlaneApiService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private handle: { send: (input: { data?: string; resize?: { cols: number; rows: number } }) => void; dispose: () => void } | null = null;
  private renewTimer: ReturnType<typeof setInterval> | null = null;
  private writer: ((bytes: Uint8Array) => void) | null = null;

  readonly state = signal<TerminalState>('idle');
  readonly policy = signal<TerminalPolicy | null>(null);
  readonly reason = signal<string | null>(null);
  readonly shell = signal<string | null>(null);
  readonly root = signal(false);
  readonly warning = signal<string | null>(null);
  readonly sessionId = signal<string | null>(null);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.close());
  }

  /** Carrega a política; devolve se a sessão pode abrir já (sem justificativa). */
  async prepare(environmentId: string, containerId: string): Promise<boolean> {
    this.state.set('loading');
    this.reason.set(null);
    try {
      const policy = await this.api.getTerminalPolicy(environmentId, containerId);
      this.policy.set(policy);
      if (!policy.allowed) {
        this.reason.set(policy.reason ?? 'scope');
        this.state.set('blocked');
        return false;
      }
      if (policy.requiresJustification) {
        this.state.set('justification');
        return false;
      }
      return true;
    } catch (error: unknown) {
      const status = (error as { status?: number } | null)?.status;
      this.reason.set(status === 403 ? 'scope' : 'error');
      this.state.set('blocked');
      return false;
    }
  }

  /** Abre a sessão; `write` recebe os bytes do TTY para o emulador. */
  async open(environmentId: string, containerId: string, cols: number, rows: number, justification: string | null, write: (bytes: Uint8Array) => void): Promise<void> {
    this.close();
    this.writer = write;
    this.state.set('connecting');
    this.reason.set(null);
    this.warning.set(null);
    const handle = await this.realtime.openTerminal(
      { environmentId, containerId, cols, rows, justification },
      {
        next: frame => this.onFrame(frame),
        error: () => this.finish(this.state() === 'connected' ? 'client_disconnected' : 'realtime_unavailable', 'error'),
        complete: () => {
          if (this.state() === 'connecting' || this.state() === 'connected') this.finish('user_closed', 'closed');
        },
      },
    );
    if (!handle) {
      this.finish('realtime_unavailable', 'error');
      return;
    }
    this.handle = handle;
  }

  input(text: string): void {
    if (this.state() === 'connected' && text) this.handle?.send({ data: encodeBase64(text) });
  }

  resize(cols: number, rows: number): void {
    if (this.state() === 'connected') this.handle?.send({ resize: { cols, rows } });
  }

  close(): void {
    this.stopRenew();
    this.handle?.dispose();
    this.handle = null;
    if (this.state() === 'connecting' || this.state() === 'connected') this.finish('user_closed', 'closed');
  }

  private onFrame(frame: TerminalOutput): void {
    if (frame.data) {
      this.writer?.(decodeBase64(frame.data));
      return;
    }
    const status = frame.status;
    if (!status) return;
    if (status.sessionId) this.sessionId.set(status.sessionId);
    switch (status.state) {
      case 'connected':
        if (status.warning) {
          this.warning.set(status.warning);
          return;
        }
        this.shell.set(status.shell ?? null);
        this.root.set(!!status.containerUserIsRoot);
        this.state.set('connected');
        this.startRenew();
        return;
      case 'closed':
      case 'error':
        this.finish(status.reason ?? 'error', status.state);
        return;
    }
  }

  private finish(reason: string, state: 'closed' | 'error'): void {
    this.stopRenew();
    this.reason.set(reason);
    this.warning.set(null);
    this.state.set(state);
    this.handle?.dispose();
    this.handle = null;
  }

  private startRenew(): void {
    this.stopRenew();
    // A cada 2 min: o BFF entrega token novo e a API reavalia `container:exec` (D-008).
    this.renewTimer = setInterval(() => {
      const id = this.sessionId();
      if (id) this.api.renewTerminalSession(id).catch(() => undefined);
    }, 120_000);
  }

  private stopRenew(): void {
    if (this.renewTimer) clearInterval(this.renewTimer);
    this.renewTimer = null;
  }
}
