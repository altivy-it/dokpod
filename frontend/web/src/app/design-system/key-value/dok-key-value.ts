import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface KeyValueItem {
  label: string;
  value: string;
  mono?: boolean;
}

/** Lista de definições (`ct-kv`). */
@Component({
  selector: 'dok-key-value',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: block; min-width: 0' },
  template: `
    <dl class="ct-kv" [style.grid-template-columns]="labelWidth() ? labelWidth() + 'px minmax(0, 1fr)' : null">
      @for (item of items(); track item.label) {
        <dt>{{ item.label }}</dt>
        <dd [class.ct-mono]="item.mono">{{ item.value }}</dd>
      }
    </dl>
  `,
})
export class DokKeyValue {
  readonly items = input.required<KeyValueItem[]>();
  readonly labelWidth = input<number | null>(null);
}
