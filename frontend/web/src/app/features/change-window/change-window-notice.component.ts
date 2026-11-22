import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { changeWindowOpening, isChangeWindowClosed } from '../../core/change-window';
import type { ChangeWindowStatus } from '../../data-access/generated/control-plane';
import { DokStatusChip } from '../../design-system';

/**
 * Aviso "Fora da janela de mudança" com a próxima abertura no fuso do estágio (spec 026 T027, FR-006): texto visível,
 * não só cor. Some sozinho quando a janela abre, pelo relógio de `core/change-window`.
 */
@Component({
  selector: 'app-change-window-notice',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokStatusChip],
  template: `
    @if (closed()) {
      <p class="cw-notice" role="status">
        <dok-status-chip size="sm" color="warning" label="Fora da janela de mudança" />
        <span class="app-small">
          @if (opening(); as next) {
            Ações que mudam o host ficam bloqueadas até {{ next }}.
          } @else {
            Ações que mudam o host ficam bloqueadas até a próxima janela.
          }
        </span>
      </p>
    }
  `,
  styles: `.cw-notice { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin: 0; }`,
})
export class ChangeWindowNotice {
  readonly status = input<ChangeWindowStatus | null | undefined>(null);

  protected readonly closed = computed(() => isChangeWindowClosed(this.status()));
  protected readonly opening = computed(() => changeWindowOpening(this.status()));
}
