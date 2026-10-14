import { Injectable, signal } from '@angular/core';

/** Título e trilha do cabeçalho definidos pela página ativa; contador do sino vem da visão geral. */
@Injectable({ providedIn: 'root' })
export class ShellContextService {
  readonly title = signal('Visão geral');
  readonly breadcrumbs = signal<string[]>(['Operação', 'Visão geral']);
  readonly notifications = signal<number | null>(null);
  readonly alertCount = signal<number | null>(null);

  set(title: string, breadcrumbs: string[]): void {
    this.title.set(title);
    this.breadcrumbs.set(breadcrumbs);
  }
}
