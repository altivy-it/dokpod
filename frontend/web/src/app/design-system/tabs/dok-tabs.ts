import { ChangeDetectionStrategy, Component, ElementRef, input, model, viewChildren } from '@angular/core';
import { DokIcon } from '../icon/dok-icon';

export interface TabItem {
  id: string;
  label: string;
  icon?: string;
  count?: number | null;
  disabled?: boolean;
}

let nextId = 0;

/** Abas (Tabs do kit): `tablist` com setas, Home/End e foco itinerante. O painel fica no slot. */
@Component({
  selector: 'dok-tabs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  host: { style: 'display: block; min-width: 0' },
  template: `
    <div class="alt-tabs" [class.alt-tabs--plain]="variant() === 'plain'" [class.alt-tabs--card]="variant() === 'card'" [class.alt-glass]="variant() === 'card'">
      <div role="tablist" class="alt-tabs__list" [attr.aria-label]="ariaLabel()">
        @for (tab of tabs(); track tab.id; let i = $index) {
          <button
            #tabButton
            type="button"
            role="tab"
            class="alt-tabs__tab"
            [class.alt-tabs__tab--active]="tab.id === value()"
            [id]="tabId(tab.id)"
            [attr.aria-selected]="tab.id === value()"
            [attr.aria-controls]="panelId()"
            [attr.tabindex]="tab.id === value() ? 0 : -1"
            [disabled]="tab.disabled"
            (click)="select(tab)"
            (keydown)="onKeydown($event, i)"
          >
            @if (tab.icon) {
              <dok-icon [name]="tab.icon" [size]="15" />
            }
            <span>{{ tab.label }}</span>
            @if (tab.count !== undefined && tab.count !== null) {
              <span class="alt-tabs__count alt-num">{{ tab.count }}</span>
            }
          </button>
        }
      </div>
    </div>
  `,
})
export class DokTabs {
  readonly tabs = input.required<TabItem[]>();
  readonly value = model<string>('');
  readonly variant = input<'plain' | 'card'>('plain');
  readonly ariaLabel = input<string | null>(null);

  private readonly uid = ++nextId;
  private readonly buttons = viewChildren<ElementRef<HTMLButtonElement>>('tabButton');

  tabId(id: string): string {
    return `dok-tabs-${this.uid}-tab-${id}`;
  }

  panelId(): string {
    return `dok-tabs-${this.uid}-panel`;
  }

  protected select(tab: TabItem): void {
    if (tab.disabled) return;
    this.value.set(tab.id);
  }

  protected onKeydown(event: KeyboardEvent, index: number): void {
    const tabs = this.tabs();
    const count = tabs.length;
    let target: number | null = null;
    if (event.key === 'ArrowRight') target = this.next(index, 1);
    else if (event.key === 'ArrowLeft') target = this.next(index, -1);
    else if (event.key === 'Home') target = this.next(-1, 1);
    else if (event.key === 'End') target = this.next(count, -1);
    if (target === null) return;
    event.preventDefault();
    this.select(tabs[target]);
    this.buttons()[target]?.nativeElement.focus();
  }

  private next(from: number, direction: 1 | -1): number | null {
    const tabs = this.tabs();
    const count = tabs.length;
    for (let step = 1; step <= count; step++) {
      const index = (from + direction * step + count * 2) % count;
      if (!tabs[index].disabled) return index;
    }
    return null;
  }
}
