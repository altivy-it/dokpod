import { DestroyRef, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';

/**
 * Consulta periódica de métricas (spec 009 FR-012, D-007): a cada 30 s, somente com a aba visível;
 * ao voltar a ficar visível consulta imediatamente. Métricas não usam o canal em tempo real.
 */
export function pollWhileVisible(load: () => Promise<void>, intervalMs = 30_000): { reload: () => void } {
  const document = inject(DOCUMENT);
  let timer: ReturnType<typeof setInterval> | null = null;
  let running = false;
  let pending = false;

  const run = async (): Promise<void> => {
    if (document.visibilityState === 'hidden') return;
    if (running) {
      pending = true;
      return;
    }
    running = true;
    try {
      await load();
    } finally {
      running = false;
    }
    if (pending) {
      pending = false;
      await run();
    }
  };
  const start = () => {
    if (timer === null) timer = setInterval(() => void run(), intervalMs);
  };
  const stop = () => {
    if (timer !== null) clearInterval(timer);
    timer = null;
  };
  const onVisibility = () => {
    if (document.visibilityState === 'hidden') {
      stop();
    } else {
      void run();
      start();
    }
  };

  document.addEventListener('visibilitychange', onVisibility);
  inject(DestroyRef).onDestroy(() => {
    stop();
    document.removeEventListener('visibilitychange', onVisibility);
  });
  if (document.visibilityState !== 'hidden') start();
  return { reload: () => void run() };
}
