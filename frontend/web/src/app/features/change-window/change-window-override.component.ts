import { ChangeDetectionStrategy, Component, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DokCheckbox } from '../../design-system/checkbox/dok-checkbox';
import { DokIcon } from '../../design-system/icon/dok-icon';

/**
 * Opção "Executar fora da janela (exceção de emergência)" dos drawers de ação (spec 026 T033). O drawer só a mostra
 * quando algum host fora da janela tem `change:override`; marcada, a justificativa passa a ser obrigatória.
 */
@Component({
  selector: 'app-change-window-override',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokCheckbox, DokIcon, FormsModule],
  template: `
    <div class="cwo-note">
      <dok-icon name="alert" [size]="16" />
      <dok-checkbox
        label="Executar fora da janela (exceção de emergência)"
        help="Só nos hosts em que você tem change:override. Vale para esta ação, exige justificativa e fica destacada na auditoria."
        [ngModel]="checked()"
        (ngModelChange)="checked.set($event)"
      />
    </div>
  `,
  styles: `
    .cwo-note { display: flex; gap: 10px; align-items: flex-start; padding: 12px 14px; border-radius: var(--radius-md); font-size: 13px; color: var(--foreground); background: var(--warning-tint); }
    .cwo-note dok-icon { display: flex; margin-top: 1px; }
  `,
})
export class ChangeWindowOverrideOption {
  readonly checked = model(false);
}
