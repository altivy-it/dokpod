import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { DokButton } from './button/dok-button';
import { DokDataGrid, type GridColumn } from './data-grid/dok-data-grid';
import { DokDrawer } from './drawer/dok-drawer';
import { DokInfoKpi } from './kpi/dok-info-kpi';
import { DokSelect } from './select/dok-select';
import { DokEngineBadge, DokStatusChip } from './status/dok-status';
import { DokTabs } from './tabs/dok-tabs';

function key(el: Element, k: string): void {
  el.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true }));
}

@Component({
  imports: [DokSelect, FormsModule],
  template: `<dok-select label="Engine" [options]="options" [(ngModel)]="value" />`,
})
class SelectHost {
  value = 'docker';
  options = [
    { value: 'docker', label: 'Docker 27.3.1' },
    { value: 'podman', label: 'Podman 5.2.3' },
    { value: 'other', label: 'Outro' },
  ];
}

@Component({
  imports: [DokTabs],
  template: `<dok-tabs [tabs]="tabs" [(value)]="value" ariaLabel="Seções" />`,
})
class TabsHost {
  value = signal('a');
  tabs = [
    { id: 'a', label: 'Resumo' },
    { id: 'b', label: 'Logs', disabled: true },
    { id: 'c', label: 'Inspecionar' },
  ];
}

@Component({
  imports: [DokDrawer],
  template: `
    <button id="opener" type="button" (click)="open.set(true)">abrir</button>
    <dok-drawer [open]="open()" title="Criar container" primaryLabel="Criar" (closed)="open.set(false)">
      <input id="first" />
    </dok-drawer>
  `,
})
class DrawerHost {
  open = signal(false);
}

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
      [entityLabel]="['container', 'containers']"
      [rows]="rows"
      [columns]="columns"
      [rowActions]="[{ id: 'restart', label: 'Reiniciar', icon: 'refresh' }]"
      [(selection)]="selection"
      (rowAction)="last = $event.action + ':' + $event.row.name"
    />
  `,
})
class GridHost {
  selection = signal<string[]>([]);
  last = '';
  rows: Row[] = [
    { id: '1', name: 'gateway', status: 'Em execução', cpu: 18.4 },
    { id: '2', name: 'api', status: 'Parado', cpu: 2 },
    { id: '3', name: 'worker', status: 'Com falha', cpu: 41.5 },
  ];
  columns: GridColumn<Row>[] = [
    { field: 'name', header: 'Nome' },
    { field: 'status', header: 'Situação', type: 'status', filterable: true },
    { field: 'cpu', header: 'CPU', type: 'number', decimals: 1 },
  ];
}

describe('design system', () => {
  it('button reflects loading as aria-busy and disables the host', () => {
    @Component({ imports: [DokButton], template: `<button dok-button variant="primary" [loading]="true">Salvar</button>` })
    class Host {}
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(button.getAttribute('aria-busy')).toBe('true');
    expect(button.hasAttribute('disabled')).toBe(true);
    expect(button.className).toContain('alt-btn--primary');
  });

  it('status chip filter exposes a named remove button', () => {
    @Component({ imports: [DokStatusChip], template: `<dok-status-chip variant="filter" field="Engine" label="Docker" [removable]="true" />` })
    class Host {}
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('button').getAttribute('aria-label')).toBe('Remover filtro Engine Docker');
  });

  it('engine badge always shows engine and version as text', () => {
    @Component({ imports: [DokEngineBadge], template: `<dok-engine-badge engine="podman" version="5.2.3" />` })
    class Host {}
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent.trim()).toBe('Podman 5.2.3');
  });

  it('kpi announces unavailable values', () => {
    @Component({ imports: [DokInfoKpi], template: `<dok-info-kpi description="Imagens sem uso" [value]="null" />` })
    class Host {}
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=group]').getAttribute('aria-label')).toBe('Imagens sem uso: indisponível');
  });

  it('select supports arrows, Enter and Escape', async () => {
    const fixture = TestBed.createComponent(SelectHost);
    fixture.detectChanges();
    await fixture.whenStable();
    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-haspopup=listbox]');
    key(trigger, 'ArrowDown');
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelectorAll('[role=option]').length).toBe(3);
    key(trigger, 'ArrowDown');
    key(trigger, 'Enter');
    fixture.detectChanges();
    expect(fixture.componentInstance.value).toBe('podman');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    key(trigger, 'Enter');
    fixture.detectChanges();
    // Esc com a lista aberta não chega ao contêiner (ex.: o drawer da criação de container).
    const outer = vi.fn();
    fixture.nativeElement.addEventListener('keydown', outer);
    key(trigger, 'Escape');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=listbox]')).toBeNull();
    expect(outer).not.toHaveBeenCalled();
    key(trigger, 'Escape');
    expect(outer).toHaveBeenCalledTimes(1);
  });

  it('tabs move with arrows skipping disabled tabs and use roving tabindex', () => {
    const fixture = TestBed.createComponent(TabsHost);
    fixture.detectChanges();
    const tabs: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('[role=tab]'));
    key(tabs[0], 'ArrowRight');
    fixture.detectChanges();
    expect(fixture.componentInstance.value()).toBe('c');
    expect(tabs[2].getAttribute('aria-selected')).toBe('true');
    expect(tabs[0].getAttribute('tabindex')).toBe('-1');
    key(tabs[2], 'Home');
    fixture.detectChanges();
    expect(fixture.componentInstance.value()).toBe('a');
  });

  it('drawer is a modal dialog, closes on Escape and returns focus', async () => {
    const fixture = TestBed.createComponent(DrawerHost);
    fixture.detectChanges();
    const opener: HTMLButtonElement = fixture.nativeElement.querySelector('#opener');
    opener.focus();
    opener.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const dialog = fixture.nativeElement.querySelector('[role=dialog]');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.getAttribute('aria-labelledby')).toBeTruthy();
    key(dialog, 'Escape');
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    expect(fixture.nativeElement.querySelector('[role=dialog]')).toBeNull();
    expect(document.activeElement).toBe(opener);
  });

  it('data grid selects rows, sorts with aria-sort and runs row actions', () => {
    const fixture = TestBed.createComponent(GridHost);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    const rowBoxes = el.querySelectorAll<HTMLButtonElement>('tbody [role=checkbox]');
    expect(rowBoxes[0].getAttribute('aria-label')).toBe('Selecionar gateway');
    rowBoxes[0].click();
    fixture.detectChanges();
    expect(fixture.componentInstance.selection()).toEqual(['1']);
    expect(el.textContent).toContain('1 container selecionado');
    expect(el.querySelector('thead [role=checkbox]')!.getAttribute('aria-checked')).toBe('mixed');

    const cpuHeader = Array.from(el.querySelectorAll('th')).find(th => th.textContent?.includes('CPU'))!;
    cpuHeader.querySelector('button')!.click();
    fixture.detectChanges();
    expect(cpuHeader.getAttribute('aria-sort')).toBe('ascending');
    const firstName = el.querySelector('tbody tr td:nth-child(2)')!.textContent!.trim();
    expect(firstName).toBe('api');

    el.querySelector<HTMLButtonElement>('button[aria-label="Ações de worker"]')!.click();
    fixture.detectChanges();
    el.querySelector<HTMLButtonElement>('[role=menuitem]')!.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.last).toBe('restart:worker');
    fixture.nativeElement.remove();
  });
});
