import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  forwardRef,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { DokIcon } from '../icon/dok-icon';

export interface SelectOption {
  value: string;
  label: string;
  caption?: string;
  dot?: string;
  disabled?: boolean;
}

let nextId = 0;

/**
 * Seleção (board `Selecao.dc.html`): botão com `aria-haspopup="listbox"`, setas, Enter/Espaço,
 * Esc, Home/End; perder o foco fecha a lista.
 */
@Component({
  selector: 'dok-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DokSelect), multi: true }],
  host: { style: 'display: block; min-width: 0' },
  styles: `
    .dok-select { position: relative; display: flex; flex-direction: column; gap: 6px; min-width: 0; }
    .dok-select__trigger { width: 100%; cursor: pointer; padding: 0 10px 0 12px; text-align: left; }
    .dok-select__trigger:focus-visible { outline: none; border-color: var(--teal-light); box-shadow: 0 0 0 4px rgba(94, 234, 212, 0.16); }
    .dok-select__value { flex: 1; min-width: 0; display: flex; align-items: center; gap: 8px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .dok-select__chev { display: flex; color: var(--muted); transition: transform 0.15s; }
    .dok-select__chev.is-open { transform: rotate(180deg); }
    .dok-select__list { position: absolute; left: 0; right: 0; top: 100%; margin-top: 6px; z-index: 40; display: flex; flex-direction: column; gap: 2px; max-height: 260px; overflow: auto; background: var(--surface-panel); }
    .dok-select__option { display: flex; align-items: center; gap: 10px; min-height: 36px; padding: 6px 10px; border: 1px solid transparent; border-radius: 8px; color: var(--foreground); cursor: pointer; }
    .dok-select__option.is-active { background: var(--hover-wash); }
    .dok-select__option[aria-selected='true'] { background: var(--accent-tint); border-color: var(--accent-border); color: var(--text-strong); font-weight: 600; }
    .dok-select__option[aria-disabled='true'] { opacity: 0.45; cursor: not-allowed; }
    .dok-select__dot { width: 8px; height: 8px; border-radius: 999px; flex: none; }
    .dok-select__caption { color: var(--muted); font-size: 12px; font-weight: 400; }
    .dok-select__check { margin-left: auto; color: var(--teal-light); display: flex; }
    @media (prefers-reduced-motion: reduce) { .dok-select__chev { transition: none; } }
  `,
  template: `
    <div class="dok-select" (focusout)="onFocusOut($event)">
      @if (label()) {
        <span class="alt-field__label" [id]="labelId">{{ label() }}</span>
      } @else if (ariaLabel()) {
        <span class="sr-only" [id]="labelId">{{ ariaLabel() }}</span>
      }
      <button
        #trigger
        type="button"
        class="alt-ctl dok-select__trigger"
        [class.alt-ctl--sm]="size() === 'sm'"
        [class.alt-ctl--disabled]="isDisabled()"
        aria-haspopup="listbox"
        [attr.aria-expanded]="open()"
        [attr.aria-controls]="listId"
        [attr.aria-labelledby]="label() || ariaLabel() ? labelId + ' ' + valueId : null"
        [attr.aria-activedescendant]="open() && activeIndex() >= 0 ? optionId(activeIndex()) : null"
        [disabled]="isDisabled()"
        (click)="toggle()"
        (keydown)="onKeydown($event)"
      >
        <span class="dok-select__value" [id]="valueId">
          @if (current(); as option) {
            @if (option.dot) {
              <span class="dok-select__dot" [style.background]="option.dot" aria-hidden="true"></span>
            }
            <span>{{ option.label }}</span>
            @if (option.caption) {
              <span class="dok-select__caption">{{ option.caption }}</span>
            }
          } @else {
            <span style="color: var(--muted)">{{ placeholder() }}</span>
          }
        </span>
        <span class="dok-select__chev" [class.is-open]="open()"><dok-icon name="chevron-down" [size]="16" /></span>
      </button>
      @if (open()) {
        <div class="dok-select__list alt-popover" role="listbox" [id]="listId" [attr.aria-labelledby]="label() ? labelId : null" [attr.aria-label]="label() ? null : ariaLabel()">
          @for (option of options(); track option.value; let i = $index) {
            <div
              class="dok-select__option"
              role="option"
              [id]="optionId(i)"
              [class.is-active]="i === activeIndex()"
              [attr.aria-selected]="option.value === value()"
              [attr.aria-disabled]="option.disabled ? 'true' : null"
              (mousedown)="$event.preventDefault(); choose(i)"
              (mouseenter)="activeIndex.set(i)"
            >
              @if (option.dot) {
                <span class="dok-select__dot" [style.background]="option.dot" aria-hidden="true"></span>
              }
              <span style="display: flex; flex-direction: column; gap: 1px; min-width: 0">
                <span>{{ option.label }}</span>
                @if (option.caption) {
                  <span class="dok-select__caption">{{ option.caption }}</span>
                }
              </span>
              @if (option.value === value()) {
                <span class="dok-select__check"><dok-icon name="check" [size]="16" [strokeWidth]="2.4" /></span>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class DokSelect implements ControlValueAccessor {
  readonly label = input<string | null>(null);
  readonly ariaLabel = input<string | null>(null);
  readonly options = input.required<SelectOption[]>();
  readonly size = input<'md' | 'sm'>('md');
  readonly placeholder = input('Selecione');

  private readonly uid = ++nextId;
  protected readonly labelId = `dok-select-l-${this.uid}`;
  protected readonly valueId = `dok-select-v-${this.uid}`;
  protected readonly listId = `dok-select-list-${this.uid}`;
  protected readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  protected readonly value = signal<string | null>(null);
  protected readonly open = signal(false);
  protected readonly activeIndex = signal(-1);
  protected readonly isDisabled = signal(false);
  protected readonly current = computed(() => this.options().find(o => o.value === this.value()) ?? null);

  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  protected optionId(index: number): string {
    return `dok-select-o-${this.uid}-${index}`;
  }

  writeValue(value: unknown): void {
    this.value.set(value === null || value === undefined ? null : String(value));
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

  protected toggle(): void {
    if (this.open()) {
      this.open.set(false);
      return;
    }
    this.openList();
  }

  protected choose(index: number): void {
    const option = this.options()[index];
    if (!option || option.disabled) return;
    this.value.set(option.value);
    this.onChange(option.value);
    this.open.set(false);
    this.activeIndex.set(index);
    this.trigger().nativeElement.focus();
  }

  protected onKeydown(event: KeyboardEvent): void {
    const count = this.options().length;
    if (!count) return;
    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowUp': {
        event.preventDefault();
        if (!this.open()) {
          this.openList();
          return;
        }
        const delta = event.key === 'ArrowDown' ? 1 : -1;
        this.activeIndex.set((this.activeIndex() + delta + count) % count);
        return;
      }
      case 'Enter':
      case ' ':
        event.preventDefault();
        if (this.open()) this.choose(this.activeIndex());
        else this.openList();
        return;
      case 'Escape':
        if (this.open()) {
          event.preventDefault();
          this.open.set(false);
        }
        return;
      case 'Home':
        if (this.open()) {
          event.preventDefault();
          this.activeIndex.set(0);
        }
        return;
      case 'End':
        if (this.open()) {
          event.preventDefault();
          this.activeIndex.set(count - 1);
        }
        return;
      case 'Tab':
        this.open.set(false);
        return;
    }
  }

  protected onFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget as Node | null;
    if (!next || !(event.currentTarget as HTMLElement).contains(next)) {
      this.open.set(false);
      this.onTouched();
    }
  }

  private openList(): void {
    const selected = this.options().findIndex(o => o.value === this.value());
    this.activeIndex.set(selected >= 0 ? selected : 0);
    this.open.set(true);
  }
}
