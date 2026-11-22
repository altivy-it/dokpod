import { signal } from '@angular/core';
import type { ChangeWindowStatus } from '../data-access/generated/control-plane';

/**
 * Janela de mudança por host (spec 026 D-008). O diretório de ambientes publica o `changeWindow` de cada host, e o
 * relógio troca o estado sozinho no próximo `closesAt` ou `nextOpensAt`, sem polling. A tela só avisa antes do
 * clique; a API decide de novo em toda tentativa.
 */
const clock = signal(Date.now());
const byEnvironment = signal<ReadonlyMap<string, ChangeWindowStatus>>(new Map());
let timer: ReturnType<typeof setTimeout> | null = null;
/** `setTimeout` aceita no máximo ~24,8 dias; a próxima abertura está sempre a menos de 8 dias. */
const MAX_DELAY = 2_147_483_647;

export function publishChangeWindows(entries: Iterable<[string, ChangeWindowStatus | null | undefined]>): void {
  const map = new Map<string, ChangeWindowStatus>();
  for (const [environmentId, status] of entries) if (status) map.set(environmentId, status);
  byEnvironment.set(map);
  clock.set(Date.now());
  schedule();
}

export function changeWindowOf(environmentId: string): ChangeWindowStatus | undefined {
  return byEnvironment().get(environmentId);
}

/** Fechada agora: fora da janela até `nextOpensAt`, ou aberta mas já depois de `closesAt`. */
export function isChangeWindowClosed(status: ChangeWindowStatus | null | undefined): boolean {
  if (!status) return false;
  const now = clock();
  if (status.state === 'closed') return !status.nextOpensAt || now < Date.parse(status.nextOpensAt);
  if (status.state === 'open') return !!status.closesAt && now >= Date.parse(status.closesAt);
  return false;
}

/** Próxima abertura no fuso do estágio, para o aviso; nulo se a janela não está fechada ou a abertura não é conhecida. */
export function changeWindowOpening(status: ChangeWindowStatus | null | undefined): string | null {
  if (!isChangeWindowClosed(status) || status?.state !== 'closed' || !status.nextOpensAt) return null;
  const timeZone = status.timeZone ?? undefined;
  try {
    const text = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone }).format(new Date(status.nextOpensAt));
    return timeZone ? `${text} (${timeZone})` : text;
  } catch {
    return new Date(status.nextOpensAt).toLocaleString('pt-BR');
  }
}

/** Motivo para a lista de afetados e ignorados (mesmo estilo de "host desconectado"). */
export function changeWindowReason(status: ChangeWindowStatus | null | undefined): string | null {
  if (!isChangeWindowClosed(status)) return null;
  const opening = changeWindowOpening(status);
  return opening ? `fora da janela de mudança (abre em ${opening})` : 'fora da janela de mudança';
}

export function changeWindowReasonFor(environmentId: string): string | null {
  return changeWindowReason(changeWindowOf(environmentId));
}

function schedule(): void {
  if (timer !== null) clearTimeout(timer);
  timer = null;
  const now = Date.now();
  const next = [...byEnvironment().values()]
    .flatMap(status => [status.closesAt, status.nextOpensAt])
    .map(instant => (instant ? Date.parse(instant) : Number.NaN))
    .filter(instant => Number.isFinite(instant) && instant > now)
    .reduce((earliest, instant) => Math.min(earliest, instant), Number.POSITIVE_INFINITY);
  if (!Number.isFinite(next)) return;
  timer = setTimeout(() => {
    clock.set(Date.now());
    schedule();
  }, Math.min(next - now + 250, MAX_DELAY));
}
