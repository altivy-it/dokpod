import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { formatNumber } from '../format/format';
import { DokIcon } from '../icon/dok-icon';
import { tone } from '../tone';

/** KPI de vidro (InfoKPI do kit). `value` nulo exibe "—" com motivo em `unavailableLabel`. */
@Component({
  selector: 'dok-info-kpi',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  host: { style: 'display: block; min-width: 0' },
  template: `
    <div class="alt-info-kpi alt-glass" role="group" [attr.aria-label]="ariaLabel()" [attr.aria-busy]="loading() ? 'true' : null" style="width: 100%">
      <span class="alt-info-kpi__icon" [style.color]="spec().color" [style.background]="spec().background">
        <dok-icon [name]="icon()" [size]="20" />
      </span>
      <div>
        @if (loading()) {
          <div class="alt-skel" style="height: 28px; width: 70%"></div>
        } @else {
          <div class="alt-info-kpi__value alt-num">
            {{ display() }}
            @if (unit() && value() !== null) {
              <small>{{ unit() }}</small>
            }
          </div>
        }
        <div class="alt-info-kpi__desc">
          {{ description() }}
          @if (!loading() && value() === null && unavailableLabel()) {
            <span style="color: var(--slate)"> · {{ unavailableLabel() }}</span>
          }
        </div>
      </div>
    </div>
  `,
})
export class DokInfoKpi {
  readonly value = input<number | null>(null);
  readonly decimals = input(0);
  readonly unit = input<string | null>(null);
  readonly description = input.required<string>();
  readonly icon = input('activity');
  readonly color = input('primary');
  readonly loading = input(false);
  readonly unavailableLabel = input('indisponível');

  protected readonly spec = computed(() => tone(this.color()));
  protected readonly display = computed(() =>
    this.value() === null ? '—' : formatNumber(this.value(), this.decimals()),
  );
  protected readonly ariaLabel = computed(() =>
    this.value() === null
      ? `${this.description()}: ${this.unavailableLabel()}`
      : `${this.description()}: ${this.display()}${this.unit() ?? ''}`,
  );
}
