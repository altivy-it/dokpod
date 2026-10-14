import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { pollWhileVisible } from './metrics-poller';

function setVisibility(state: 'visible' | 'hidden'): void {
  Object.defineProperty(document, 'visibilityState', { value: state, configurable: true });
  document.dispatchEvent(new Event('visibilitychange'));
}

describe('pollWhileVisible', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
  });

  afterEach(() => {
    vi.useRealTimers();
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
  });

  it('polls every 30 s only while the tab is visible and reloads when it comes back', async () => {
    const load = vi.fn().mockResolvedValue(undefined);
    TestBed.runInInjectionContext(() => pollWhileVisible(load));

    await vi.advanceTimersByTimeAsync(30_000);
    expect(load).toHaveBeenCalledTimes(1);

    setVisibility('hidden');
    await vi.advanceTimersByTimeAsync(120_000);
    expect(load).toHaveBeenCalledTimes(1);

    setVisibility('visible');
    await vi.advanceTimersByTimeAsync(0);
    expect(load).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(30_000);
    expect(load).toHaveBeenCalledTimes(3);
  });

  it('queues a reload requested while a load is running', async () => {
    let release!: () => void;
    const load = vi.fn().mockImplementationOnce(() => new Promise<void>(resolve => (release = resolve))).mockResolvedValue(undefined);
    const poller = TestBed.runInInjectionContext(() => pollWhileVisible(load));

    poller.reload();
    poller.reload();
    expect(load).toHaveBeenCalledTimes(1);
    release();
    await vi.advanceTimersByTimeAsync(0);
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('stops when the injector is destroyed', async () => {
    const load = vi.fn().mockResolvedValue(undefined);
    TestBed.runInInjectionContext(() => pollWhileVisible(load));
    TestBed.resetTestingModule();
    await vi.advanceTimersByTimeAsync(90_000);
    expect(load).not.toHaveBeenCalled();
  });
});
