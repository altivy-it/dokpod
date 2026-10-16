import { ChangeDetectionStrategy, Component, ElementRef, forwardRef, input, signal, viewChild } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { DokIcon } from '../icon/dok-icon';

let nextId = 0;

/** Marcador (board `Marcador.dc.html`) sobre `input type="checkbox"` nativo. */
@Component({
  selector: 'dok-checkbox',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DokCheckbox), multi: true }],
  host: { style: 'display: block' },
  styles: `
    .dok-check { position: relative; display: flex; align-items: flex-start; gap: 10px; padding: 4px 0; cursor: pointer; color: var(--foreground); }
    .dok-check input { position: absolute; opacity: 0; width: 18px; height: 18px; margin: 1px 0 0; cursor: pointer; }
    .dok-check__box { flex: none; width: 18px; height: 18px; margin-top: 1px; display: flex; align-items: center; justify-content: center; border-radius: 5px; border: 1px solid var(--glass-border-strong); background: var(--input-fill); color: var(--ink); transition: background 0.15s, border-color 0.15s; }
    .dok-check input:checked + .dok-check__box { background: var(--teal-light); border-color: var(--teal-light); }
    .dok-check input:focus-visible + .dok-check__box { box-shadow: 0 0 0 2px var(--surface-base), 0 0 0 4px var(--teal-light); }
    .dok-check input:disabled ~ * { opacity: 0.5; }
    .dok-check__label { font-size: 13px; line-height: 20px; }
    .dok-check__help { font-size: 12px; color: var(--muted); }
    @media (prefers-reduced-motion: reduce) { .dok-check__box { transition: none; } }
  `,
  template: `
    <label class="dok-check">
      <input
        #native
        type="checkbox"
        [id]="inputId"
        [checked]="checked()"
        [disabled]="isDisabled()"
        [attr.aria-describedby]="help() ? inputId + '-help' : null"
        (change)="onToggle($event)"
        (blur)="onTouched()"
      />
      <span class="dok-check__box" aria-hidden="true">
        @if (checked()) {
          <dok-icon name="check" [size]="13" [strokeWidth]="3" />
        }
      </span>
      <span style="display: flex; flex-direction: column; gap: 2px; min-width: 0">
        <span class="dok-check__label">{{ label() }}</span>
        @if (help()) {
          <span class="dok-check__help" [id]="inputId + '-help'">{{ help() }}</span>
        }
      </span>
    </label>
  `,
})
export class DokCheckbox implements ControlValueAccessor {
  readonly label = input.required<string>();
  readonly help = input<string | null>(null);

  protected readonly inputId = `dok-check-${++nextId}`;
  protected readonly checked = signal(false);
  protected readonly isDisabled = signal(false);
  private onChange: (value: boolean) => void = () => undefined;
  protected onTouched: () => void = () => undefined;

  private readonly native = viewChild<ElementRef<HTMLInputElement>>('native');

  /** Também acerta o elemento nativo: o clique já o alterou e o valor devolvido pode ser igual ao último renderizado. */
  writeValue(value: unknown): void {
    this.checked.set(!!value);
    const element = this.native()?.nativeElement;
    if (element) element.checked = !!value;
  }

  registerOnChange(fn: (value: boolean) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.isDisabled.set(isDisabled);
  }

  protected onToggle(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.checked.set(checked);
    this.onChange(checked);
  }
}
