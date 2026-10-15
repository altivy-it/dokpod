import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { activeNavId, COMING_SOON, NAV_ITEMS } from '../../shell/navigation';
import { normalizeRegistry, RegistriesApiService } from './registries-api.service';
import { registrySituation } from './registries.page';
import { parseRegistries, RegistryPolicyDrawer } from './registry-policy.drawer';

describe('Registros (spec 020)', () => {
  it('replaces the coming-soon entry with the Registries route', () => {
    expect(NAV_ITEMS.find(i => i.id === 'registries')!.route).toBe('/registros');
    expect(COMING_SOON['registros']).toBeUndefined();
    expect(activeNavId('/registros?vista=politica')).toBe('registries');
  });

  it('normalizes registries like the server and refuses scheme, path and credential', () => {
    expect(normalizeRegistry(' Registry.Empresa.com:5000 ')).toBe('registry.empresa.com:5000');
    expect(normalizeRegistry('index.docker.io')).toBe('docker.io');
    expect(normalizeRegistry('https://quay.io')).toBeNull();
    expect(normalizeRegistry('quay.io/org')).toBeNull();
    expect(normalizeRegistry('user:pass@quay.io')).toBeNull();
  });

  it('parses one registry per line or comma, deduplicated and sorted, keeping the invalid ones apart', () => {
    expect(parseRegistries('quay.io\nRegistry.Empresa.com, quay.io\n\nhttps://x.io')).toEqual({
      registries: ['quay.io', 'registry.empresa.com'],
      invalid: ['https://x.io'],
    });
    expect(parseRegistries('  ')).toEqual({ registries: [], invalid: [] });
  });

  it('shows the most serious situation', () => {
    expect(registrySituation({ blockedHosts: 1, insecureHosts: 1, outsidePolicyHosts: 1 })).toBe('Bloqueado');
    expect(registrySituation({ blockedHosts: 0, insecureHosts: 2, outsidePolicyHosts: 1 })).toBe('Inseguro');
    expect(registrySituation({ blockedHosts: 0, insecureHosts: 0, outsidePolicyHosts: 1 })).toBe('Fora da política');
    expect(registrySituation({ blockedHosts: 0, insecureHosts: 0, outsidePolicyHosts: 0 })).toBe('Permitido');
  });
});

@Component({
  imports: [RegistryPolicyDrawer],
  template: `<app-registry-policy-drawer [open]="true" stage="staging" [current]="current()" (saved)="saved.push($event)" />`,
})
class Host {
  readonly current = signal<string[]>(['docker.io']);
  readonly saved: string[][] = [];
}

describe('registry-policy drawer (FR-007)', () => {
  const impact = vi.fn();

  beforeEach(() => {
    vi.useFakeTimers();
    impact.mockReset();
    impact.mockResolvedValue({ images: 3, hosts: 2 });
    TestBed.configureTestingModule({ providers: [{ provide: RegistriesApiService, useValue: { impact } }] });
  });

  afterEach(() => vi.useRealTimers());

  const primary = () => Array.from(document.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent?.includes('Salvar política'))!;
  const textarea = () => document.querySelector<HTMLTextAreaElement>('#rp-registries')!;

  it('shows the images left outside, blocks invalid entries and saves the normalized list', async () => {
    const fixture = TestBed.createComponent(Host);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(textarea().value).toBe('docker.io');

    textarea().value = 'Quay.io\nhttps://bad.io';
    textarea().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(document.body.textContent).toContain('Registros inválidos: https://bad.io');
    expect(primary().disabled).toBe(true);

    textarea().value = 'Quay.io';
    textarea().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(450);
    fixture.detectChanges();
    expect(impact).toHaveBeenCalledWith('staging', ['quay.io']);
    expect(document.body.textContent).toContain('3 imagens em 2 hosts ficam fora da lista (nada é removido).');
    primary().click();
    expect(fixture.componentInstance.saved).toEqual([['quay.io']]);

    textarea().value = '';
    textarea().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(document.body.textContent).toContain('Qualquer registro será aceito em Homologação.');
    fixture.nativeElement.remove();
  });
});
