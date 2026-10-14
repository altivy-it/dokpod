import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DokButton } from '../../design-system/button/dok-button';
import { DokEmptyState } from '../../design-system/empty-state/dok-empty-state';
import { COMING_SOON } from '../navigation';
import { ShellContextService } from '../shell-context.service';

@Component({
  selector: 'app-coming-soon-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokEmptyState, RouterLink],
  template: `
    <div class="dok-stack">
      <dok-empty-state kind="soon" [title]="entry().label + ' em breve'" [detail]="entry().description">
        <a dok-button variant="outline" size="sm" startIcon="home" routerLink="/" queryParamsHandling="merge">Voltar à visão geral</a>
      </dok-empty-state>
    </div>
  `,
})
export class ComingSoonPage {
  readonly item = input('');
  private readonly context = inject(ShellContextService);
  protected readonly entry = computed(
    () => COMING_SOON[this.item()] ?? { label: 'Funcionalidade', description: 'Esta área ainda não foi entregue.' },
  );

  constructor() {
    effect(() => this.context.set(this.entry().label, ['Dokpod', this.entry().label]));
  }
}
