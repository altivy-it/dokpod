import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DokIcon } from '../icon/dok-icon';
import { tone } from '../tone';

export type ChipVariant = 'soft' | 'outlined' | 'filter';

/** Chip de status (StatusChip do kit). Variante `filter` é removível. */
@Component({
  selector: 'dok-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  host: { style: 'display: contents' },
  template: `
    <span [class]="classes()" [style]="chipStyle()" [attr.title]="title()">
      @if (variant() === 'outlined' && !iconName()) {
        <span class="alt-dot" [style.color]="spec().color" aria-hidden="true"></span>
      }
      @if (iconName()) {
        <dok-icon [name]="iconName()!" [size]="13" [strokeWidth]="2.2" />
      }
      @if (variant() === 'filter' && field()) {
        <span class="alt-status-chip__field">{{ field() }}:</span>
      }
      <span class="alt-status-chip__label">{{ label() }}</span>
      @if (removable()) {
        <button type="button" class="alt-status-chip__del" [attr.aria-label]="removeLabel()" (click)="removed.emit()">
          <dok-icon name="x" [size]="13" [strokeWidth]="2.2" />
        </button>
      }
    </span>
  `,
})
export class DokStatusChip {
  readonly label = input.required<string>();
  readonly color = input<string>('neutral');
  readonly variant = input<ChipVariant>('soft');
  readonly size = input<'sm' | 'md'>('md');
  readonly icon = input<string | boolean | null>(null);
  readonly field = input<string | null>(null);
  readonly removable = input(false);
  readonly title = input<string | null>(null);
  readonly removed = output<void>();

  protected readonly spec = computed(() => tone(this.color()));
  protected readonly iconName = computed(() => {
    const icon = this.icon();
    return icon === true ? this.spec().icon : typeof icon === 'string' ? icon : null;
  });
  protected readonly classes = computed(() =>
    [
      'alt-chip',
      'alt-status-chip',
      `alt-status-chip--${this.variant()}`,
      this.size() === 'sm' ? 'alt-status-chip--sm' : '',
      this.removable() ? 'alt-status-chip--deletable' : '',
    ]
      .filter(Boolean)
      .join(' '),
  );
  protected readonly chipStyle = computed(() => {
    const spec = this.spec();
    if (this.variant() === 'soft') return { background: spec.background, color: spec.color };
    if (this.variant() === 'outlined') return { '--alt-status-chip-c': spec.color };
    return {};
  });
  protected readonly removeLabel = computed(() =>
    `Remover ${this.field() ? `filtro ${this.field()} ` : ''}${this.label()}`.trim(),
  );
}

const DOT = { small: 8, medium: 10, large: 12 } as const;
const FONT = { small: 12, medium: 14, large: 16 } as const;

/** Círculo de status (StatusCircle do kit), sempre acompanhado de texto. */
@Component({
  selector: 'dok-status-circle',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: contents' },
  template: `
    <span class="alt-status-circle" [style.font-size.px]="font()">
      <span
        class="alt-status-circle__dot alt-status-circle__dot--halo"
        [class.alt-status-circle__dot--pulse]="pulse()"
        [style]="dotStyle()"
        aria-hidden="true"
      ></span>
      <span class="alt-status-circle__text">{{ text() }}</span>
    </span>
  `,
})
export class DokStatusCircle {
  readonly text = input.required<string>();
  readonly color = input<string>('success');
  readonly size = input<'small' | 'medium' | 'large'>('small');
  readonly pulse = input(false);

  protected readonly font = computed(() => FONT[this.size()]);
  protected readonly dotStyle = computed(() => {
    const z = DOT[this.size()];
    const color = this.color() === 'neutral' ? 'var(--slate)' : tone(this.color()).color;
    const halo = Math.round(z * 0.35);
    return {
      width: `${z}px`,
      height: `${z}px`,
      margin: `${halo}px`,
      background: color,
      '--alt-status-circle-c': color,
      '--alt-status-circle-halo': `${halo}px`,
    };
  });
}

/** Badge de engine (`ct-engine`): Docker azul, Podman laranja. */
@Component({
  selector: 'dok-engine-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: contents' },
  template: `<span class="ct-engine" [class.ct-engine--podman]="isPodman()"><i aria-hidden="true"></i>{{ label() }}</span>`,
})
export class DokEngineBadge {
  readonly engine = input.required<string>();
  readonly version = input<string | null>(null);

  protected readonly isPodman = computed(() => this.engine().toLowerCase() === 'podman');
  protected readonly label = computed(() => {
    const engine = this.engine().toLowerCase();
    const name = engine === 'podman' ? 'Podman' : engine === 'docker' ? 'Docker' : this.engine();
    return this.version() ? `${name} ${this.version()}` : name;
  });
}
