import { computed, inject, Injectable, signal } from '@angular/core';
import { ControlPlaneApiService } from '../data-access/control-plane-api.service';
import type { EnvironmentState } from '../data-access/generated/control-plane';

/** Ambientes autorizados ao usuário (todas as páginas), usados para scopes do menu e seletores de host. */
@Injectable({ providedIn: 'root' })
export class EnvironmentDirectoryService {
  private readonly api = inject(ControlPlaneApiService);
  private loadingPromise: Promise<void> | null = null;

  readonly environments = signal<EnvironmentState[]>([]);
  readonly loaded = signal(false);
  readonly scopes = computed(() => new Set(this.environments().flatMap(e => e.scopes as string[])));

  load(force = false): Promise<void> {
    if (this.loadingPromise && !force) return this.loadingPromise;
    this.loadingPromise = this.fetchAll().finally(() => this.loaded.set(true));
    return this.loadingPromise;
  }

  byId(environmentId: string): EnvironmentState | undefined {
    return this.environments().find(e => e.environmentId === environmentId);
  }

  private async fetchAll(): Promise<void> {
    const all: EnvironmentState[] = [];
    let cursor: string | undefined;
    try {
      for (let guard = 0; guard < 50; guard++) {
        const page = await this.api.listEnvironments(cursor);
        all.push(...page.environments);
        if (!page.nextCursor) break;
        cursor = page.nextCursor;
      }
      this.environments.set(all);
    } catch {
      this.environments.set(all);
    }
  }
}
