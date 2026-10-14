import { ChangeDetectionStrategy, Component, computed, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { DokIcon } from '../icon/dok-icon';

let nextId = 0;

/** Campo de texto (board `Campo.dc.html`), com rótulo, ajuda, erro, sufixo, mono e segredo. */
@Component({
  selector: 'dok-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DokField), multi: true }],
  host: { style: 'display: block; min-width: 0' },
  template: `
    <div class="alt-field">
      @if (label()) {
        <label class="alt-field__label" [for]="inputId">{{ label() }}</label>
      }
      <div
        class="alt-ctl"
        [class.alt-ctl--sm]="size() === 'sm'"
        [class.alt-ctl--error]="!!error()"
        [class.alt-ctl--disabled]="isDisabled()"
      >
        <input
          [id]="inputId"
          [type]="inputType()"
          [value]="value()"
          [attr.inputmode]="inputMode()"
          [attr.placeholder]="placeholder() || null"
          [attr.aria-label]="label() ? null : ariaLabel()"
          [attr.aria-describedby]="describedBy()"
          [attr.aria-invalid]="error() ? 'true' : null"
          [attr.required]="required() ? '' : null"
          [attr.autocomplete]="secret() ? 'off' : autocomplete()"
          [disabled]="isDisabled()"
          [style.font-family]="mono() ? 'var(--font-mono)' : null"
          [style.font-size.px]="mono() ? (size() === 'sm' ? 12.5 : 13) : null"
          spellcheck="false"
          (input)="onInput($event)"
          (blur)="onTouched()"
        />
        @if (suffix()) {
          <span class="app-caption" style="color: var(--muted); white-space: nowrap">{{ suffix() }}</span>
        }
        @if (secret()) {
          <button
            type="button"
            class="alt-campo-busca__clear"
            [attr.aria-label]="revealed() ? 'Ocultar valor' : 'Mostrar valor'"
            [attr.aria-pressed]="revealed()"
            [title]="revealed() ? 'Ocultar valor' : 'Mostrar valor'"
            (click)="revealed.set(!revealed())"
          >
            <dok-icon name="eye" [size]="15" />
          </button>
        }
      </div>
      @if (error()) {
        <span class="alt-field__error" [id]="inputId + '-error'" role="alert"><dok-icon name="alert" [size]="13" />{{ error() }}</span>
      } @else if (help()) {
        <span class="alt-field__help" [id]="inputId + '-help'">{{ help() }}</span>
      }
    </div>
  `,
})
export class DokField implements ControlValueAccessor {
  readonly label = input<string | null>(null);
  readonly ariaLabel = input<string | null>(null);
  readonly placeholder = input<string | null>(null);
  readonly help = input<string | null>(null);
  readonly error = input<string | null>(null);
  readonly suffix = input<string | null>(null);
  readonly mono = input(false);
  readonly secret = input(false);
  readonly size = input<'md' | 'sm'>('md');
  readonly type = input<'text' | 'number' | 'search' | 'url'>('text');
  readonly required = input(false);
  readonly autocomplete = input('off');

  protected readonly inputId = `dok-field-${++nextId}`;
  protected readonly value = signal('');
  protected readonly revealed = signal(false);
  protected readonly isDisabled = signal(false);
  protected readonly inputType = computed(() => (this.secret() && !this.revealed() ? 'password' : 'text'));
  protected readonly inputMode = computed(() => (this.type() === 'number' ? 'decimal' : null));
  protected readonly describedBy = computed(() =>
    this.error() ? `${this.inputId}-error` : this.help() ? `${this.inputId}-help` : null,
  );

  private onChange: (value: string) => void = () => undefined;
  protected onTouched: () => void = () => undefined;

  writeValue(value: unknown): void {
    this.value.set(value === null || value === undefined ? '' : String(value));
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.isDisabled.set(isDisabled);
  }

  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.value.set(value);
    this.onChange(value);
  }
}
