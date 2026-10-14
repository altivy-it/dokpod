import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  viewChild,
} from '@angular/core';
import { DokButton } from '../button/dok-button';
import { DokIcon } from '../icon/dok-icon';
import { tone } from '../tone';

export interface DrawerChip {
  label: string;
  icon?: string;
  tone?: string;
}

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
let nextId = 0;

/**
 * Drawer lateral (CorporateDrawer do kit): `role="dialog"`, `aria-modal`, foco preso,
 * Esc fecha e o foco volta ao elemento que abriu.
 */
@Component({
  selector: 'dok-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokIcon],
  host: { style: 'display: contents' },
  template: `
    @if (open()) {
      <div class="alt-corporate-drawer" (keydown)="onKeydown($event)">
        <div class="alt-corporate-drawer__scrim" aria-hidden="true" (click)="close()"></div>
        <aside
          #paper
          class="alt-corporate-drawer__paper alt-glass alt-glass--overlay"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          [style.width]="width()"
        >
          <header class="alt-corporate-drawer__head">
            <div class="alt-corporate-drawer__head-text">
              @if (eyebrow()) {
                <div class="alt-corporate-drawer__eyebrow">{{ eyebrow() }}</div>
              }
              <h2 class="alt-corporate-drawer__title" [id]="titleId">{{ title() }}</h2>
              @if (subtitle()) {
                <div class="alt-corporate-drawer__subtitle alt-num">{{ subtitle() }}</div>
              }
              @if (chips().length) {
                <div class="alt-corporate-drawer__chips">
                  @for (chip of chips(); track chip.label) {
                    <span class="alt-corporate-drawer__chip alt-num" [style]="chipStyle(chip)">
                      @if (chipIcon(chip); as icon) {
                        <dok-icon [name]="icon" [size]="13" />
                      }
                      {{ chip.label }}
                    </span>
                  }
                </div>
              }
            </div>
            <button type="button" class="alt-corporate-drawer__close" aria-label="Fechar" (click)="close()">
              <dok-icon name="x" [size]="18" />
            </button>
          </header>
          <div class="alt-corporate-drawer__body alt-scroll">
            <ng-content />
          </div>
          @if (primaryLabel() || cancelLabel()) {
            <footer class="alt-corporate-drawer__foot">
              <ng-content select="[drawerFooterStart]" />
              @if (cancelLabel()) {
                <button dok-button variant="secondary" size="sm" type="button" (click)="close()">{{ cancelLabel() }}</button>
              }
              @if (primaryLabel()) {
                <button
                  dok-button
                  [variant]="primaryVariant()"
                  size="sm"
                  type="button"
                  [startIcon]="primaryIcon()"
                  [loading]="primaryLoading()"
                  [disabled]="primaryDisabled()"
                  (click)="primary.emit()"
                >
                  {{ primaryLabel() }}
                </button>
              }
            </footer>
          }
        </aside>
      </div>
    }
  `,
})
export class DokDrawer {
  readonly open = input(false);
  readonly width = input('520px');
  readonly eyebrow = input<string | null>(null);
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly chips = input<DrawerChip[]>([]);
  readonly primaryLabel = input<string | null>(null);
  readonly primaryIcon = input<string | null>('check');
  readonly primaryVariant = input<'primary' | 'danger'>('primary');
  readonly cancelLabel = input<string | null>('Cancelar');
  readonly primaryLoading = input(false);
  readonly primaryDisabled = input(false);
  readonly closed = output<void>();
  readonly primary = output<void>();

  protected readonly titleId = `dok-drawer-${++nextId}`;
  private readonly paper = viewChild<ElementRef<HTMLElement>>('paper');
  private readonly injector = inject(Injector);
  private returnFocus: HTMLElement | null = null;

  constructor() {
    effect(() => {
      if (this.open()) {
        this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        afterNextRender(() => this.focusFirst(), { injector: this.injector });
      } else if (this.returnFocus) {
        const target = this.returnFocus;
        this.returnFocus = null;
        queueMicrotask(() => target.isConnected && target.focus());
      }
    });
  }

  close(): void {
    this.closed.emit();
  }

  protected chipStyle(chip: DrawerChip): Record<string, string> {
    if (!chip.tone) return {};
    const spec = tone(chip.tone);
    return { color: spec.color, background: spec.background, 'border-color': 'transparent' };
  }

  protected chipIcon(chip: DrawerChip): string | null {
    return chip.icon ?? (chip.tone ? tone(chip.tone).icon : null);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.stopPropagation();
      this.close();
      return;
    }
    if (event.key !== 'Tab') return;
    const items = this.focusable();
    if (!items.length) return;
    const first = items[0];
    const last = items[items.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private focusable(): HTMLElement[] {
    const paper = this.paper()?.nativeElement;
    return paper ? Array.from(paper.querySelectorAll<HTMLElement>(FOCUSABLE)) : [];
  }

  private focusFirst(): void {
    const items = this.focusable();
    const body = items.find(el => !el.classList.contains('alt-corporate-drawer__close'));
    (body ?? items[0])?.focus({ preventScroll: true });
  }
}
