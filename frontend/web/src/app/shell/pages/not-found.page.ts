import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DokButton } from '../../design-system/button/dok-button';
import { DokEmptyState } from '../../design-system/empty-state/dok-empty-state';
import { ShellContextService } from '../shell-context.service';

@Component({
  selector: 'app-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokEmptyState, RouterLink],
  template: `
    <div class="dok-stack">
      <dok-empty-state kind="empty" icon="help" code="Erro 404" title="Página não encontrada" detail="O endereço não existe ou foi movido.">
        <a dok-button variant="outline" size="sm" startIcon="home" routerLink="/">Voltar à visão geral</a>
      </dok-empty-state>
    </div>
  `,
})
export class NotFoundPage {
  constructor() {
    inject(ShellContextService).set('Página não encontrada', ['Dokpod', 'Erro 404']);
  }
}
