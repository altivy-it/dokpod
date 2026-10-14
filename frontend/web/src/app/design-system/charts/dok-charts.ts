import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { formatNumber } from '../format/format';
import { DokIcon } from '../icon/dok-icon';

export interface ChartSeries {
  name: string;
  data: (number | null)[];
  area?: boolean;
  color?: string;
}

export interface ChartReference {
  value: number;
  label: string;
}

export interface DonutItem {
  label: string;
  value: number;
  color?: string;
}

const SERIES_COLORS = ['var(--chart-1)', 'var(--chart-2)', 'var(--chart-3)', 'var(--chart-4)', 'var(--chart-5)', 'var(--chart-6)'];

function ticks(min: number, max: number, count = 4): number[] {
  const raw = (max - min) / count || 1;
  const magnitude = Math.pow(10, Math.floor(Math.log10(raw)));
  const ratio = raw / magnitude;
  const step = (ratio <= 1 ? 1 : ratio <= 2 ? 2 : ratio <= 2.5 ? 2.5 : ratio <= 5 ? 5 : 10) * magnitude;
  const out: number[] = [];
  const start = Math.floor(min / step) * step;
  const end = Math.ceil(max / step - 1e-9) * step;
  for (let v = start; v <= end + step / 2; v += step) out.push(+v.toFixed(6));
  return out;
}

const defaultFormat = (value: number) => formatNumber(value, 1);

/** Gráfico de linha/área (Graphics line do kit). Setas exploram os pontos; tabela alternativa no botão. */
@Component({
  selector: 'dok-line-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  host: { style: 'display: flex; flex-direction: column; min-width: 0' },
  template: `
    <section class="alt-graphics alt-graphics--line alt-graphics__card alt-glass" style="flex: 1 1 auto">
      <header class="alt-graphics__hd">
        <div class="alt-graphics__ttw">
          <h3 class="alt-graphics__tt">{{ title() }}</h3>
          @if (subtitle()) {
            <span class="alt-graphics__sub">{{ subtitle() }}</span>
          }
        </div>
        <button
          type="button"
          class="alt-tbtn alt-tbtn--icon alt-tbtn--ghost"
          [attr.aria-pressed]="asTable()"
          aria-label="Ver como tabela"
          title="Ver como tabela"
          (click)="asTable.set(!asTable())"
        >
          <dok-icon name="table" [size]="16" />
        </button>
      </header>
      @if (!asTable() && !unavailable()) {
        <div class="alt-graphics__legend">
          @for (s of series(); track s.name; let i = $index) {
            <span><i class="alt-graphics__key alt-graphics__key--line" [style.--c]="color(s, i)"></i>{{ s.name }}</span>
          }
          @if (reference()) {
            <span><i class="alt-graphics__key alt-graphics__key--ref" style="--c: var(--muted)"></i>{{ reference()!.label }}</span>
          }
        </div>
      }
      <div class="alt-graphics__body">
        @if (unavailable()) {
          <div class="app-small" style="display: grid; place-items: center; color: var(--muted)" [style.height.px]="height()">{{ unavailable() }}</div>
        } @else if (asTable()) {
          <div class="alt-graphics__tablewrap alt-scroll">
            <table class="alt-graphics__table alt-num">
              <caption class="sr-only">{{ title() }}</caption>
              <thead>
                <tr>
                  <th scope="col">{{ xLabel() }}</th>
                  @for (s of series(); track s.name) {
                    <th scope="col">{{ s.name }}</th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (x of x(); track $index; let i = $index) {
                  <tr>
                    <td>{{ x }}</td>
                    @for (s of series(); track s.name) {
                      <td>{{ s.data[i] === null || s.data[i] === undefined ? '—' : format()(s.data[i]!) }}</td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <div #plot class="alt-graphics__plot" [style.height.px]="height()">
            @if (width() > 0) {
              <svg class="alt-graphics__svg alt-num" [attr.width]="width()" [attr.height]="height()" role="img" [attr.aria-label]="title()">
                @for (t of geometry().ticks; track $index) {
                  <g>
                    <line [attr.x1]="m.l" [attr.x2]="width() - m.r" [attr.y1]="t.y" [attr.y2]="t.y" class="alt-graphics__grid" />
                    <text [attr.x]="m.l - 8" [attr.y]="t.y + 4" text-anchor="end" class="alt-graphics__ax">{{ t.label }}</text>
                  </g>
                }
                @for (t of geometry().xTicks; track $index) {
                  <text [attr.x]="t.x" [attr.y]="height() - 6" [attr.text-anchor]="t.anchor" class="alt-graphics__ax">{{ t.label }}</text>
                }
                @if (geometry().reference; as r) {
                  <line [attr.x1]="m.l" [attr.x2]="width() - m.r" [attr.y1]="r.y" [attr.y2]="r.y" class="alt-graphics__ref" />
                  <text [attr.x]="width() - m.r" [attr.y]="r.y - 6" text-anchor="end" class="alt-graphics__ax">{{ r.label }}</text>
                }
                @for (p of geometry().paths; track p.key) {
                  @if (p.area) {
                    <path [attr.d]="p.area" [attr.fill]="p.color" fill-opacity="0.1" />
                  }
                  <path [attr.d]="p.line" fill="none" [attr.stroke]="p.color" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" />
                }
                @if (geometry().end; as e) {
                  <circle [attr.cx]="e.x" [attr.cy]="e.y" r="4" [attr.fill]="e.color" class="alt-graphics__ring" />
                  <text [attr.x]="e.x - 8" [attr.y]="e.y - 10" text-anchor="end" class="alt-graphics__val">{{ e.label }}</text>
                }
                @if (hover() !== null) {
                  <line [attr.x1]="xAt(hover()!)" [attr.x2]="xAt(hover()!)" [attr.y1]="m.t" [attr.y2]="height() - m.b" class="alt-graphics__xh" />
                  @for (dot of hoverDots(); track $index) {
                    <circle [attr.cx]="dot.x" [attr.cy]="dot.y" r="4" [attr.fill]="dot.color" class="alt-graphics__ring" />
                  }
                }
                <rect
                  [attr.x]="m.l"
                  [attr.y]="m.t"
                  [attr.width]="Math.max(0, width() - m.l - m.r)"
                  [attr.height]="Math.max(0, height() - m.t - m.b)"
                  fill="transparent"
                  tabindex="0"
                  class="alt-graphics__mark"
                  aria-label="Explorar valores; use as setas"
                  (pointermove)="onPointer($event)"
                  (pointerleave)="hover.set(null)"
                  (blur)="hover.set(null)"
                  (focus)="hover.set(hover() ?? x().length - 1)"
                  (keydown)="onKey($event)"
                />
              </svg>
            }
            @if (hover() !== null && tip(); as t) {
              <div class="alt-graphics__tip alt-num" role="status" [style.left.px]="t.left" [style.top.px]="t.top" [style.transform]="t.transform">
                <div class="alt-graphics__tip-h">{{ t.title }}</div>
                @for (row of t.rows; track row.name) {
                  <div class="alt-graphics__tip-r">
                    <i class="alt-graphics__key alt-graphics__key--line" [style.--c]="row.color"></i><b>{{ row.value }}</b><span>{{ row.name }}</span>
                  </div>
                }
              </div>
            }
          </div>
        }
      </div>
      @if (note()) {
        <p class="alt-graphics__note">{{ note() }}</p>
      }
    </section>
  `,
})
export class DokLineChart {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly x = input<string[]>([]);
  readonly xLabel = input('Horário');
  readonly series = input<ChartSeries[]>([]);
  readonly reference = input<ChartReference | null>(null);
  readonly yMin = input<number | null>(null);
  readonly yMax = input<number | null>(null);
  readonly height = input(200);
  readonly format = input<(value: number) => string>(defaultFormat);
  readonly note = input<string | null>(null);
  readonly unavailable = input<string | null>(null);

  protected readonly Math = Math;
  protected readonly m = { l: 40, r: 18, t: 16, b: 26 };
  protected readonly asTable = signal(false);
  protected readonly width = signal(0);
  protected readonly hover = signal<number | null>(null);
  private readonly plot = viewChild<ElementRef<HTMLElement>>('plot');
  private observer: ResizeObserver | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.observer?.disconnect());
    effect(() => this.observe(this.plot()?.nativeElement ?? null));
  }

  protected color(series: ChartSeries, index: number): string {
    return series.color ?? SERIES_COLORS[index % SERIES_COLORS.length];
  }

  protected readonly scale = computed(() => {
    const values: number[] = [0];
    for (const s of this.series()) for (const v of s.data) if (v !== null && v !== undefined) values.push(v);
    if (this.reference()) values.push(this.reference()!.value);
    const t = ticks(this.yMin() ?? 0, this.yMax() ?? Math.max(...values), 4);
    return { ticks: t, min: t[0], max: t[t.length - 1] };
  });

  xAt(index: number): number {
    const n = this.x().length;
    return this.m.l + (n <= 1 ? 0 : (index * (this.width() - this.m.l - this.m.r)) / (n - 1));
  }

  private yAt(value: number): number {
    const { min, max } = this.scale();
    return this.m.t + (1 - (value - min) / (max - min || 1)) * (this.height() - this.m.t - this.m.b);
  }

  protected readonly geometry = computed(() => {
    const n = this.x().length;
    const fmt = this.format();
    const tickList = this.scale().ticks.map(v => ({ y: this.yAt(v), label: fmt(v) }));
    const idx = n ? [0, Math.round((n - 1) / 4), Math.round((n - 1) / 2), Math.round((3 * (n - 1)) / 4), n - 1] : [];
    const unique = [...new Set(idx)];
    const xTicks = unique.map((i, k) => ({
      x: this.xAt(i),
      label: this.x()[i],
      anchor: k === 0 ? 'start' : k === unique.length - 1 ? 'end' : 'middle',
    }));
    const paths: { key: string; line: string; area: string | null; color: string }[] = [];
    type EndLabel = { x: number; y: number; label: string; color: string };
    let end = null as EndLabel | null;
    this.series().forEach((s, si) => {
      const points: [number, number][] = [];
      s.data.forEach((v, i) => {
        if (v !== null && v !== undefined) points.push([this.xAt(i), this.yAt(v)]);
      });
      if (!points.length) return;
      const color = this.color(s, si);
      const line = 'M' + points.map(p => `${p[0].toFixed(1)} ${p[1].toFixed(1)}`).join('L');
      const base = this.yAt(this.scale().min);
      const area = s.area ? `${line}L${points[points.length - 1][0]} ${base}L${points[0][0]} ${base}Z` : null;
      paths.push({ key: s.name, line, area, color });
      if (si === 0) {
        const lastIndex = s.data.map((v, i) => (v === null || v === undefined ? -1 : i)).filter(i => i >= 0).pop()!;
        const last = points[points.length - 1];
        end = { x: last[0], y: last[1], label: fmt(s.data[lastIndex]!), color };
      }
    });
    const ref = this.reference();
    return {
      ticks: tickList,
      xTicks,
      paths: paths.reverse(),
      end,
      reference: ref ? { y: this.yAt(ref.value), label: ref.label } : null,
    };
  });

  protected readonly hoverDots = computed(() => {
    const k = this.hover();
    if (k === null) return [];
    return this.series()
      .map((s, si) => ({ s, si }))
      .filter(({ s }) => s.data[k] !== null && s.data[k] !== undefined)
      .map(({ s, si }) => ({ x: this.xAt(k), y: this.yAt(s.data[k]!), color: this.color(s, si) }));
  });

  protected readonly tip = computed(() => {
    const k = this.hover();
    if (k === null) return null;
    const x = this.xAt(k);
    const flip = x > this.width() * 0.62;
    return {
      title: this.x()[k],
      left: flip ? x - 12 : x + 12,
      top: this.m.t + 30,
      transform: flip ? 'translateX(-100%)' : 'none',
      rows: this.series()
        .map((s, si) => ({ s, si }))
        .filter(({ s }) => s.data[k] !== null && s.data[k] !== undefined)
        .map(({ s, si }) => ({ name: s.name, value: this.format()(s.data[k]!), color: this.color(s, si) })),
    };
  });

  protected onPointer(event: PointerEvent): void {
    const rect = this.plot()?.nativeElement.getBoundingClientRect();
    if (!rect) return;
    const n = this.x().length;
    const step = (this.width() - this.m.l - this.m.r) / Math.max(1, n - 1);
    const index = Math.round((event.clientX - rect.left - this.m.l) / step);
    this.hover.set(Math.max(0, Math.min(n - 1, index)));
  }

  protected onKey(event: KeyboardEvent): void {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    event.preventDefault();
    const n = this.x().length;
    const current = this.hover() ?? n - 1;
    this.hover.set(Math.max(0, Math.min(n - 1, current + (event.key === 'ArrowRight' ? 1 : -1))));
  }

  private observe(el: HTMLElement | null): void {
    this.observer?.disconnect();
    this.observer = null;
    if (!el) return;
    const measure = () => this.width.set(el.clientWidth);
    measure();
    if (typeof ResizeObserver === 'undefined') return;
    this.observer = new ResizeObserver(measure);
    this.observer.observe(el);
  }
}

/** Gráfico de rosca (Graphics donut do kit), com legenda textual e percentuais. */
@Component({
  selector: 'dok-donut-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: flex; flex-direction: column; min-width: 0' },
  template: `
    <section class="alt-graphics alt-graphics--donut alt-graphics__card alt-glass" style="flex: 1 1 auto">
      <header class="alt-graphics__hd">
        <div class="alt-graphics__ttw">
          <h3 class="alt-graphics__tt">{{ title() }}</h3>
          @if (subtitle()) {
            <span class="alt-graphics__sub">{{ subtitle() }}</span>
          }
        </div>
      </header>
      <div class="alt-graphics__body" style="flex: 1 1 auto; display: flex; align-items: center; justify-content: center">
        @if (total() === 0) {
          <div class="app-small" style="color: var(--muted)">{{ emptyLabel() }}</div>
        } @else {
          <div class="alt-graphics__plot alt-graphics__donut">
            <svg class="alt-graphics__svg alt-num" [attr.width]="size()" [attr.height]="size()" role="img" [attr.aria-label]="title()">
              @for (arc of arcs(); track arc.label; let i = $index) {
                <path
                  [attr.d]="arc.d"
                  [attr.fill]="arc.color"
                  [attr.fill-opacity]="active() === null || active() === i ? 1 : 0.35"
                  class="alt-graphics__mark"
                  tabindex="0"
                  [attr.aria-label]="arc.aria"
                  (pointerenter)="active.set(i)"
                  (pointerleave)="active.set(null)"
                  (focus)="active.set(i)"
                  (blur)="active.set(null)"
                />
              }
              <text [attr.x]="size() / 2" [attr.y]="size() / 2 + 4" text-anchor="middle" class="alt-graphics__center">{{ center() ?? totalLabel() }}</text>
              <text [attr.x]="size() / 2" [attr.y]="size() / 2 + 22" text-anchor="middle" class="alt-graphics__ax">{{ centerLabel() }}</text>
            </svg>
            <ul class="alt-graphics__dlg alt-num">
              @for (arc of arcs(); track arc.label; let i = $index) {
                <li [attr.data-dim]="active() !== null && active() !== i ? 'true' : null">
                  <i class="alt-graphics__key alt-graphics__key--sq" [style.--c]="arc.color"></i>
                  <span>{{ arc.label }}</span>
                  <b>{{ arc.percent }}</b>
                </li>
              }
            </ul>
          </div>
        }
      </div>
    </section>
  `,
})
export class DokDonutChart {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly items = input<DonutItem[]>([]);
  readonly center = input<string | null>(null);
  readonly centerLabel = input<string | null>(null);
  readonly size = input(168);
  readonly thickness = input(18);
  readonly emptyLabel = input('Sem dados');

  protected readonly active = signal<number | null>(null);
  protected readonly total = computed(() => this.items().reduce((sum, item) => sum + item.value, 0));
  protected readonly totalLabel = computed(() => formatNumber(this.total()));

  protected readonly arcs = computed(() => {
    const size = this.size();
    const thickness = this.thickness();
    const r = (size - thickness) / 2 - 2;
    const c = size / 2;
    const total = this.total() || 1;
    const gap = this.items().length > 1 ? 0.035 : 0;
    let angle = -Math.PI / 2;
    const point = (a: number, radius: number) => `${(c + radius * Math.cos(a)).toFixed(2)} ${(c + radius * Math.sin(a)).toFixed(2)}`;
    return this.items().map((item, i) => {
      const sweep = Math.min((item.value / total) * Math.PI * 2, Math.PI * 2 - 0.0001);
      const start = angle + gap / 2;
      const end = angle + sweep - gap / 2;
      angle += sweep;
      const r1 = r + thickness / 2;
      const r0 = r - thickness / 2;
      const large = end - start > Math.PI ? 1 : 0;
      const d = `M${point(start, r1)}A${r1} ${r1} 0 ${large} 1 ${point(end, r1)}L${point(end, r0)}A${r0} ${r0} 0 ${large} 0 ${point(start, r0)}Z`;
      const percent = `${formatNumber((item.value / total) * 100)}%`;
      return {
        label: item.label,
        color: item.color ?? SERIES_COLORS[i % SERIES_COLORS.length],
        d,
        percent,
        valueLabel: formatNumber(item.value),
        aria: `${item.label}: ${formatNumber(item.value)} (${percent})`,
      };
    });
  });
}
