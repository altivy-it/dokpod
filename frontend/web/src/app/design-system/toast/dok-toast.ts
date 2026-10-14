import { ChangeDetectionStrategy, Component, inject, Injectable, signal } from '@angular/core';
import { DokIcon } from '../icon/dok-icon';
import { tone } from '../tone';

export type ToastType = 'success' | 'error' | 'warning' | 'info';

export interface ToastMessage {
  id: number;
  type: ToastType;
  title?: string;
  message?: string;
  items?: string[];
  autoClose: number | false;
}

const AUTO: Record<ToastType, number | false> = { success: 4000, info: 4000, warning: 6000, error: false };
const ICON: Record<ToastType, string> = { success: 'check-circle', error: 'alert', warning: 'info', info: 'info' };
const WORD: Record<ToastType, string> = { success: 'Sucesso', error: 'Erro', warning: 'Atenção', info: 'Informação' };

/** Notificações (Toast do kit). Erros não fecham sozinhos. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private sequence = 0;
  readonly toasts = signal<ToastMessage[]>([]);

  notify(type: ToastType, options: { title?: string; message?: string; items?: string[]; autoClose?: number | false }): number {
    const toast: ToastMessage = {
      id: ++this.sequence,
      type,
      title: options.title,
      message: options.message,
      items: options.items,
      autoClose: options.autoClose ?? AUTO[type],
    };
    this.toasts.update(list => [toast, ...list].slice(0, 5));
    return toast.id;
  }

  success(message: string, title?: string): number {
    return this.notify('success', { message, title });
  }

  error(message: string, title?: string): number {
    return this.notify('error', { message, title });
  }

  dismiss(id: number): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }
}

@Component({
  selector: 'dok-toast-host',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  template: `
    <section class="alt-toast alt-toast--top-right" aria-label="Notificações">
      @for (toast of service.toasts().slice(0, 3); track toast.id) {
        <div
          class="alt-glass alt-glass--overlay alt-toast__item"
          [attr.role]="toast.type === 'error' ? 'alert' : 'status'"
          [attr.aria-live]="toast.type === 'error' ? 'assertive' : 'polite'"
        >
          <span class="alt-toast__icon" [style.background]="spec(toast).background" [style.color]="spec(toast).color">
            <dok-icon [name]="icon(toast)" [size]="16" [label]="word(toast)" />
          </span>
          <div class="alt-toast__body">
            @if (toast.title) {
              <div class="alt-toast__title">{{ toast.title }}</div>
            }
            @if (toast.message) {
              <p class="alt-toast__msg">{{ toast.message }}</p>
            }
            @if (toast.items?.length) {
              <ul class="alt-toast__list">
                @for (item of toast.items; track $index) {
                  <li>{{ item }}</li>
                }
              </ul>
            }
          </div>
          <button type="button" class="alt-toast__close" aria-label="Fechar notificação" (click)="service.dismiss(toast.id)">
            <dok-icon name="x" [size]="15" />
          </button>
          @if (toast.autoClose) {
            <span
              class="alt-toast__progress"
              aria-hidden="true"
              [style.background]="spec(toast).color"
              [style.animation-duration.ms]="toast.autoClose"
              (animationend)="service.dismiss(toast.id)"
            ></span>
          }
        </div>
      }
    </section>
  `,
})
export class DokToastHost {
  protected readonly service = inject(ToastService);

  protected spec(toast: ToastMessage) {
    return tone(toast.type);
  }

  protected icon(toast: ToastMessage): string {
    return ICON[toast.type];
  }

  protected word(toast: ToastMessage): string {
    return WORD[toast.type];
  }
}
