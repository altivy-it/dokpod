import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  model,
  output,
  viewChild,
} from '@angular/core';
import { DokIcon } from '../icon/dok-icon';

let nextId = 0;

/** Campo de busca (CampoBusca do kit), com atalho de foco opcional e debounce. */
@Component({
  selector: 'dok-search-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  host: { style: 'display: block; min-width: 0; max-width: 100%', '(window:keydown)': 'onWindowKey($event)' },
  template: `
    <div class="alt-campo-busca" [style.width]="width()">
      @if (label()) {
        <label class="alt-field__label" [for]="inputId">{{ label() }}</label>
      }
      <div class="alt-ctl alt-campo-busca__ctl" [class.alt-campo-busca__ctl--md]="size() === 'md'">
        <dok-icon name="search" [size]="16" />
        <input
          #field
          [id]="inputId"
          type="search"
          [value]="value()"
          [attr.placeholder]="placeholder()"
          [attr.aria-label]="label() ? null : ariaLabel() || placeholder()"
          [attr.aria-keyshortcuts]="shortcut() === '/' ? '/' : null"
          (input)="onInput($event)"
          (keydown.escape)="clear($event)"
        />
        @if (value()) {
          <button type="button" class="alt-campo-busca__clear" aria-label="Limpar busca" (click)="clear()">
            <dok-icon name="x" [size]="14" />
          </button>
        } @else if (shortcut()) {
          <kbd class="alt-campo-busca__kbd" aria-hidden="true">{{ shortcut() }}</kbd>
        }
      </div>
    </div>
  `,
})
export class DokSearchField {
  readonly label = input<string | null>(null);
  readonly ariaLabel = input<string | null>(null);
  readonly placeholder = input('Buscar');
  readonly size = input<'sm' | 'md'>('sm');
  readonly width = input<string>('260px');
  readonly shortcut = input<string | null>(null);
  readonly debounceMs = input(250);
  readonly value = model('');
  readonly search = output<string>();

  protected readonly inputId = `dok-search-${++nextId}`;
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.timer && clearTimeout(this.timer));
  }

  protected onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
    this.schedule();
  }

  protected clear(event?: Event): void {
    if (!this.value()) return;
    event?.preventDefault();
    this.value.set('');
    this.emitNow();
    this.field().nativeElement.focus();
  }

  protected onWindowKey(event: KeyboardEvent): void {
    if (this.shortcut() !== '/' || event.key !== '/') return;
    const target = event.target as HTMLElement | null;
    const tag = target?.tagName?.toLowerCase();
    if (tag === 'input' || tag === 'textarea' || tag === 'select' || target?.isContentEditable) return;
    event.preventDefault();
    this.field().nativeElement.focus();
  }

  private schedule(): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => this.emitNow(), this.debounceMs());
  }

  private emitNow(): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = null;
    this.search.emit(this.value());
  }
}
