import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';

export type Stage = 'production' | 'staging' | 'development';

export interface StageOption {
  id: Stage;
  label: string;
  icon: string;
}

export const STAGES: StageOption[] = [
  { id: 'production', label: 'Produção', icon: 'shield' },
  { id: 'staging', label: 'Homologação', icon: 'flag' },
  { id: 'development', label: 'Desenvolvimento', icon: 'tool' },
];

export function isStage(value: unknown): value is Stage {
  return value === 'production' || value === 'staging' || value === 'development';
}

export function stageLabel(stage: string | null | undefined): string {
  return STAGES.find(s => s.id === stage)?.label ?? 'Todos os estágios';
}

/** Estágio global (atalhos do cabeçalho), persistido no query param `stage`. */
@Injectable({ providedIn: 'root' })
export class StageFilterService {
  private readonly router = inject(Router);

  readonly stage = toSignal(
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      map(() => this.read()),
    ),
    { initialValue: this.read() },
  );

  select(stage: Stage | null): void {
    void this.router.navigate([], {
      relativeTo: this.deepest(),
      queryParams: { stage: stage ?? null },
      queryParamsHandling: 'merge',
    });
  }

  private read(): Stage | null {
    const value = this.router.parseUrl(this.router.url).queryParams['stage'];
    return isStage(value) ? value : null;
  }

  private deepest(): ActivatedRoute {
    let route = this.router.routerState.root;
    while (route.firstChild) route = route.firstChild;
    return route;
  }
}
