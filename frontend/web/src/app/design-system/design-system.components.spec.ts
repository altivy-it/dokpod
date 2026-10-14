import { Component, signal, type Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { vi } from 'vitest';
import { DokDonutChart, DokLineChart } from './charts/dok-charts';
import { DokCheckbox } from './checkbox/dok-checkbox';
import { DokDataGrid, type GridColumn } from './data-grid/dok-data-grid';
import { DokDrawer } from './drawer/dok-drawer';
import { DokEmptyState } from './empty-state/dok-empty-state';
import { DokField } from './field/dok-field';
import { DokIcon } from './icon/dok-icon';
import { DokKeyValue } from './key-value/dok-key-value';
import { DokProgressBar } from './progress/dok-progress-bar';
import { DokSearchField } from './search/dok-search-field';
import { DokStatusChip } from './status/dok-status';
import { DokToastHost, ToastService } from './toast/dok-toast';

function key(el: Element, k: string, init: KeyboardEventInit = {}): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true, ...init });
  el.dispatchEvent(event);
  return event;
}

function render<T>(type: Type<T>) {
  const fixture = TestBed.createComponent(type);
  document.body.appendChild(fixture.nativeElement);
  fixture.detectChanges();
  return fixture;
}

afterEach(() => {
  document.body.innerHTML = '';
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('dok-field', () => {
  @Component({
    imports: [DokField, ReactiveFormsModule],
    template: `
      <dok-field label="Nome do container" help="Até 63 caracteres" [formControl]="name" />
      <dok-field ariaLabel="Valor da variável" [secret]="true" [formControl]="secret" [error]="error()" />
    `,
  })
  class Host {
    name = new FormControl('api-pedidos');
    secret = new FormControl('s3nh4');
    error = signal<string | null>(null);
  }

  it('links label and help text and propagates value both ways', () => {
    const fixture = render(Host);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    const label: HTMLLabelElement = fixture.nativeElement.querySelector('label');
    expect(label.htmlFor).toBe(input.id);
    expect(input.value).toBe('api-pedidos');
    const help = fixture.nativeElement.querySelector(`#${input.getAttribute('aria-describedby')}`);
    expect(help.textContent.trim()).toBe('Até 63 caracteres');

    input.value = 'api-novo';
    input.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.name.value).toBe('api-novo');

    fixture.componentInstance.name.disable();
    fixture.detectChanges();
    expect(input.disabled).toBe(true);
  });

  it('masks secrets, toggles visibility with a pressed button and announces errors', () => {
    const fixture = render(Host);
    const input: HTMLInputElement = fixture.nativeElement.querySelectorAll('input')[1];
    expect(input.type).toBe('password');
    expect(input.getAttribute('aria-label')).toBe('Valor da variável');
    expect(input.getAttribute('autocomplete')).toBe('off');
    const toggle: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-pressed]');
    expect(toggle.getAttribute('aria-label')).toBe('Mostrar valor');
    toggle.click();
    fixture.detectChanges();
    expect(input.type).toBe('text');
    expect(toggle.getAttribute('aria-pressed')).toBe('true');

    fixture.componentInstance.error.set('Informe o valor');
    fixture.detectChanges();
    expect(input.getAttribute('aria-invalid')).toBe('true');
    const error = fixture.nativeElement.querySelector('[role=alert]');
    expect(error.textContent).toContain('Informe o valor');
    expect(input.getAttribute('aria-describedby')).toBe(error.id);
  });
});

describe('dok-checkbox', () => {
  @Component({
    imports: [DokCheckbox, ReactiveFormsModule],
    template: `<dok-checkbox label="Remover automaticamente ao parar" help="Logs e estado se perdem" [formControl]="control" />`,
  })
  class Host {
    control = new FormControl(false);
  }

  it('is a native checkbox with label and description', () => {
    const fixture = render(Host);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=checkbox]');
    expect(input.closest('label')!.textContent).toContain('Remover automaticamente ao parar');
    expect(fixture.nativeElement.querySelector(`#${input.getAttribute('aria-describedby')}`).textContent).toContain('Logs e estado');
    input.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.control.value).toBe(true);
    fixture.componentInstance.control.setValue(false);
    fixture.detectChanges();
    expect(input.checked).toBe(false);
  });
});

describe('dok-search-field', () => {
  @Component({
    imports: [DokSearchField],
    template: `<dok-search-field placeholder="Buscar containers" shortcut="/" [debounceMs]="200" (search)="last.set($event)" />
      <input id="other" />`,
  })
  class Host {
    last = signal<string | null>(null);
  }

  it('debounces input, clears with Escape and focuses on the slash shortcut', () => {
    vi.useFakeTimers();
    const fixture = render(Host);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=search]');
    expect(input.getAttribute('aria-label')).toBe('Buscar containers');
    expect(input.getAttribute('aria-keyshortcuts')).toBe('/');

    input.value = 'api';
    input.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.last()).toBeNull();
    vi.advanceTimersByTime(200);
    expect(fixture.componentInstance.last()).toBe('api');

    fixture.detectChanges();
    key(input, 'Escape');
    expect(fixture.componentInstance.last()).toBe('');

    input.blur();
    const typed = key(document.body, '/');
    expect(typed.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(input);

    const other: HTMLInputElement = fixture.nativeElement.querySelector('#other');
    other.focus();
    expect(key(other, '/').defaultPrevented).toBe(false);
  });
});

describe('dok-toast', () => {
  @Component({ imports: [DokToastHost], template: `<dok-toast-host />` })
  class Host {}

  it('announces through persistent live regions and keeps errors open', () => {
    vi.useFakeTimers();
    const fixture = render(Host);
    const service = TestBed.inject(ToastService);
    const polite = fixture.nativeElement.querySelector('[aria-live=polite]');
    const assertive = fixture.nativeElement.querySelector('[aria-live=assertive]');
    expect(polite).not.toBeNull();
    expect(assertive.getAttribute('role')).toBe('alert');

    service.success('Container reiniciado', 'api-pedidos');
    service.error('Host desconectado');
    fixture.detectChanges();
    expect(polite.textContent).toContain('Container reiniciado');
    expect(assertive.textContent).toContain('Host desconectado');

    vi.advanceTimersByTime(4000);
    fixture.detectChanges();
    expect(service.toasts().map(t => t.message)).toEqual(['Host desconectado']);
  });

  it('pauses auto close while hovered without depending on animations', () => {
    vi.useFakeTimers();
    const fixture = render(Host);
    const service = TestBed.inject(ToastService);
    const id = service.success('Imagem baixada');
    fixture.detectChanges();
    vi.advanceTimersByTime(3000);
    service.pause(id);
    vi.advanceTimersByTime(10_000);
    expect(service.toasts().length).toBe(1);
    service.resume(id);
    vi.advanceTimersByTime(1000);
    expect(service.toasts().length).toBe(0);
  });
});

describe('dok-drawer focus trap', () => {
  @Component({
    imports: [DokDrawer],
    template: `<dok-drawer [open]="true" title="Novo container" primaryLabel="Criar e iniciar"><input id="first" /></dok-drawer>`,
  })
  class Host {}

  it('cycles Tab and Shift+Tab inside the dialog', async () => {
    const fixture = render(Host);
    await fixture.whenStable();
    const dialog: HTMLElement = fixture.nativeElement.querySelector('[role=dialog]');
    const focusables = Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled]), input'));
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('#first'));

    last.focus();
    expect(key(last, 'Tab').defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(first);
    expect(key(first, 'Tab', { shiftKey: true }).defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(last);
  });
});

describe('charts', () => {
  @Component({
    imports: [DokLineChart],
    template: `<dok-line-chart title="CPU do host" [x]="['14:00', '14:05', '14:10']" [series]="series" />`,
  })
  class LineHost {
    series = [{ name: 'CPU', data: [10, 20.5, null] }];
  }

  it('line chart explores points with the keyboard and offers a table alternative', () => {
    vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(480);
    const fixture = render(LineHost);
    fixture.detectChanges();
    const slider: SVGElement = fixture.nativeElement.querySelector('[role=slider]');
    expect(fixture.nativeElement.querySelector('svg.alt-graphics__svg').getAttribute('role')).toBe('group');
    slider.dispatchEvent(new FocusEvent('focus'));
    fixture.detectChanges();
    expect(slider.getAttribute('aria-valuenow')).toBe('2');
    key(slider, 'ArrowLeft');
    fixture.detectChanges();
    expect(slider.getAttribute('aria-valuetext')).toBe('14:05, CPU 20,5');
    key(slider, 'Home');
    fixture.detectChanges();
    expect(slider.getAttribute('aria-valuetext')).toBe('14:00, CPU 10,0');
    expect(fixture.nativeElement.querySelector('[role=status]').textContent).toContain('10,0');

    const toggle: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-label="Ver como tabela"]');
    toggle.click();
    fixture.detectChanges();
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
    const rows = Array.from(fixture.nativeElement.querySelectorAll('tbody tr')).map((tr: any) =>
      Array.from(tr.querySelectorAll('td'))
        .map((td: any) => td.textContent.trim())
        .join(' '),
    );
    expect(rows).toEqual(['14:00 10,0', '14:05 20,5', '14:10 —']);
    expect(fixture.nativeElement.querySelector('caption').textContent).toContain('CPU do host');
  });

  @Component({
    imports: [DokDonutChart],
    template: `<dok-donut-chart title="Containers por engine" [items]="items" />`,
  })
  class DonutHost {
    items = [
      { label: 'Docker', value: 3 },
      { label: 'Podman', value: 1 },
    ];
  }

  it('donut chart has a textual legend and named focusable arcs', () => {
    const fixture = render(DonutHost);
    const legend = Array.from(fixture.nativeElement.querySelectorAll('li')).map(
      (li: any) => `${li.querySelector('span').textContent.trim()} ${li.querySelector('b').textContent.trim()}`,
    );
    expect(legend).toEqual(['Docker 75%', 'Podman 25%']);
    const arcs = fixture.nativeElement.querySelectorAll('path[role=img]');
    expect(arcs.length).toBe(2);
    expect(arcs[0].getAttribute('aria-label')).toContain('Docker: 3');
    expect(fixture.nativeElement.querySelector('svg.alt-graphics__svg').getAttribute('role')).toBe('group');
  });
});

describe('small components', () => {
  @Component({
    imports: [DokEmptyState, DokProgressBar, DokKeyValue, DokIcon, DokStatusChip],
    template: `
      <dok-empty-state kind="error" detail="Falha ao consultar" />
      <dok-empty-state kind="loading" />
      <dok-progress-bar [value]="140" label="CPU" />
      <dok-key-value [items]="[{ label: 'Imagem', value: 'nginx:1.27', mono: true }]" />
      <dok-icon name="box" />
      <dok-icon name="alert" label="Erro" />
      <dok-status-chip label="Em execução" color="success" />
    `,
  })
  class Host {}

  it('expose the expected semantics', () => {
    const fixture = render(Host);
    const el: HTMLElement = fixture.nativeElement;
    const states = el.querySelectorAll('dok-empty-state > *');
    expect(states[0].getAttribute('role')).toBe('alert');
    expect(states[1].getAttribute('role')).toBe('status');
    const bar = el.querySelector('[role=progressbar]')!;
    expect(bar.getAttribute('aria-valuenow')).toBe('100');
    expect(bar.getAttribute('aria-label')).toBe('CPU');
    expect(el.querySelector('dt')!.textContent).toBe('Imagem');
    expect(el.querySelector('dd')!.classList).toContain('ct-mono');
    const icons = el.querySelectorAll(':scope > dok-icon > svg');
    expect(icons[0].getAttribute('aria-hidden')).toBe('true');
    expect(icons[1].getAttribute('role')).toBe('img');
    expect(icons[1].getAttribute('aria-label')).toBe('Erro');
    expect(el.querySelector('dok-status-chip')!.textContent!.trim()).toBe('Em execução');
  });
});

describe('dok-data-grid keyboard and export', () => {
  interface Row {
    id: string;
    name: string;
    status: string;
    cpu: number;
  }

  @Component({
    imports: [DokDataGrid],
    template: `
      <dok-data-grid
        title="Containers"
        exportName="containers"
        [entityLabel]="['container', 'containers']"
        [rows]="rows"
        [columns]="columns"
        [rowActions]="[{ id: 'logs', label: 'Ver logs' }, { id: 'stop', label: 'Parar' }]"
        (rowAction)="last = $event.action"
      />
    `,
  })
  class Host {
    last = '';
    rows: Row[] = [
      { id: '1', name: 'gateway', status: 'Em execução', cpu: 18.4 },
      { id: '2', name: '=cmd|calc', status: 'Parado', cpu: 2 },
    ];
    columns: GridColumn<Row>[] = [
      { field: 'name', header: 'Nome' },
      { field: 'status', header: 'Situação', type: 'status', filterable: true },
      { field: 'cpu', header: 'CPU', type: 'number', decimals: 1 },
    ];
  }

  it('row menu is named, moves with arrows and Escape returns focus to the trigger', async () => {
    const fixture = render(Host);
    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-label="Ações de gateway"]');
    trigger.focus();
    trigger.click();
    fixture.detectChanges();
    await Promise.resolve();
    const menu: HTMLElement = fixture.nativeElement.querySelector('[role=menu][aria-label="Ações de gateway"]');
    const items = menu.querySelectorAll<HTMLElement>('[role=menuitem]');
    expect(document.activeElement).toBe(items[0]);
    key(items[0], 'ArrowDown');
    expect(document.activeElement).toBe(items[1]);
    key(items[1], 'Home');
    expect(document.activeElement).toBe(items[0]);
    key(items[0], 'Escape');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=menu][aria-label="Ações de gateway"]')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('cycles sort ascending, descending and none on the header button', () => {
    const fixture = render(Host);
    const th = Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('th')).find(h => h.textContent!.includes('Nome'))!;
    const button = th.querySelector('button')!;
    button.click();
    fixture.detectChanges();
    expect(th.getAttribute('aria-sort')).toBe('ascending');
    button.click();
    fixture.detectChanges();
    expect(th.getAttribute('aria-sort')).toBe('descending');
    button.click();
    fixture.detectChanges();
    expect(th.getAttribute('aria-sort')).toBe('none');
  });

  it('exports the loaded rows as CSV neutralizing spreadsheet formulas', async () => {
    const created: Blob[] = [];
    vi.spyOn(URL, 'createObjectURL').mockImplementation((blob: Blob | MediaSource) => {
      created.push(blob as Blob);
      return 'blob:test';
    });
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const fixture = render(Host);
    const exportButton = Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button')).find(b =>
      (b.getAttribute('aria-label') ?? b.textContent ?? '').toLowerCase().includes('export'),
    )!;
    exportButton.click();
    expect(click).toHaveBeenCalled();
    const text = await created[0].text();
    expect(text).toContain('Nome;Situação;CPU');
    expect(text).toContain("'=cmd|calc;Parado;2,0");
    expect(text).toContain('gateway;Em execução;18,4');
  });
});
