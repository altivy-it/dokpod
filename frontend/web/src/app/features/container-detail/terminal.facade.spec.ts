import { TestBed } from '@angular/core/testing';
import { ConsoleRealtimeService, type TerminalOutput } from '../../core/console-realtime.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import { decodeBase64, encodeBase64, TerminalFacade, terminalReason } from './terminal.facade';

describe('TerminalFacade', () => {
  let handlers: { next: (frame: TerminalOutput) => void; error: (error: unknown) => void; complete: () => void } | null = null;
  const sent: unknown[] = [];
  const disposed = vi.fn();
  const realtime = {
    openTerminal: vi.fn(async (_request: unknown, h: typeof handlers) => {
      handlers = h;
      return { send: (input: unknown) => sent.push(input), dispose: disposed };
    }),
  };
  const api = { getTerminalPolicy: vi.fn(), renewTerminalSession: vi.fn(async () => undefined) };
  const policy = (overrides: object = {}) => ({
    allowed: true, requiresJustification: false, requiresRecentMfa: false,
    limits: { idleTimeoutSeconds: 600, maxDurationSeconds: 1800, maxPerUser: 2, maxPerUserPerContainer: 1, maxPerAgent: 4, justificationMinLength: 10, justificationMaxLength: 500 },
    ...overrides,
  });

  beforeEach(() => {
    vi.useFakeTimers();
    handlers = null;
    sent.length = 0;
    disposed.mockReset();
    realtime.openTerminal.mockClear();
    api.getTerminalPolicy.mockReset();
    api.renewTerminalSession.mockClear();
    TestBed.configureTestingModule({
      providers: [TerminalFacade, { provide: ConsoleRealtimeService, useValue: realtime }, { provide: ControlPlaneApiService, useValue: api }],
    });
  });

  afterEach(() => vi.useRealTimers());

  it('abre, mostra a saída, envia entrada e renova a autorização a cada 2 minutos', async () => {
    api.getTerminalPolicy.mockResolvedValue(policy());
    const facade = TestBed.inject(TerminalFacade);
    const written: string[] = [];

    expect(await facade.prepare('env', 'c'.repeat(64))).toBe(true);
    await facade.open('env', 'c'.repeat(64), 80, 24, null, bytes => written.push(new TextDecoder().decode(bytes)));
    expect(facade.state()).toBe('connecting');
    handlers!.next({ status: { state: 'connecting', sessionId: 'sess-1' } });
    handlers!.next({ status: { state: 'connected', shell: '/bin/bash', containerUserIsRoot: true } });
    handlers!.next({ data: encodeBase64('app@x:/$ ') });
    facade.input('ls\r');
    facade.resize(120, 40);

    expect(facade.state()).toBe('connected');
    expect(facade.root()).toBe(true);
    expect(written.join('')).toBe('app@x:/$ ');
    expect(sent).toEqual([{ data: encodeBase64('ls\r') }, { resize: { cols: 120, rows: 40 } }]);
    vi.advanceTimersByTime(120_000);
    expect(api.renewTerminalSession).toHaveBeenCalledWith('sess-1');

    handlers!.next({ status: { state: 'connected', warning: 'idle_timeout' } });
    expect(facade.warning()).toBe('idle_timeout');
    handlers!.next({ status: { state: 'closed', reason: 'idle_timeout' } });
    expect(facade.state()).toBe('closed');
    expect(terminalReason(facade.reason())).toContain('inatividade');
    vi.advanceTimersByTime(240_000);
    expect(api.renewTerminalSession).toHaveBeenCalledTimes(1);
  });

  it('exige justificativa em Produção e mostra o motivo quando não permitido', async () => {
    const facade = TestBed.inject(TerminalFacade);
    api.getTerminalPolicy.mockResolvedValueOnce(policy({ requiresJustification: true }));
    expect(await facade.prepare('env', 'c'.repeat(64))).toBe(false);
    expect(facade.state()).toBe('justification');

    api.getTerminalPolicy.mockResolvedValueOnce(policy({ allowed: false, reason: 'not_supported' }));
    expect(await facade.prepare('env', 'c'.repeat(64))).toBe(false);
    expect(facade.state()).toBe('blocked');
    expect(terminalReason(facade.reason())).toBe('Terminal não suportado pelo agente deste host.');
  });

  it('encerra a sessão ao fechar e trata recusa do servidor', async () => {
    const facade = TestBed.inject(TerminalFacade);
    await facade.open('env', 'c'.repeat(64), 80, 24, 'justificativa ok', () => undefined);
    handlers!.next({ status: { state: 'error', reason: 'label_disabled' } });
    expect(facade.state()).toBe('error');
    expect(terminalReason(facade.reason())).toContain('dokpod.exec=disabled');

    await facade.open('env', 'c'.repeat(64), 80, 24, null, () => undefined);
    handlers!.next({ status: { state: 'connected', shell: '/bin/sh' } });
    facade.close();
    expect(disposed).toHaveBeenCalled();
    expect(facade.state()).toBe('closed');
  });

  it('codifica UTF-8 em base64 nos dois sentidos', () => {
    expect(new TextDecoder().decode(decodeBase64(encodeBase64('ação ✓')))).toBe('ação ✓');
  });
});
