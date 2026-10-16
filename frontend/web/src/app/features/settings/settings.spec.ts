import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { JustificationPolicyService } from '../../core/justification-policy.service';
import type { JustificationPolicy, RegistryPolicy, SettingsInstallation } from '../../data-access/generated/control-plane';
import { activeNavId, COMING_SOON, NAV_ITEMS } from '../../shell/navigation';
import { CONSOLE_VERSION, SettingsApiService } from './settings-api.service';
import { migrationLabel } from './settings-labels';
import { changedBy, SettingsPage, stagePolicies } from './settings.page';

const justification = (stage: JustificationPolicy['stage'], required: boolean, canEdit: boolean, updatedBy: string | null = null): JustificationPolicy => ({
  stage,
  required,
  actions: ['stop', 'pause', 'delete'],
  updatedBy,
  updatedAt: updatedBy ? '2026-10-16T10:00:00Z' : null,
  canEdit,
});

const registry = (stage: RegistryPolicy['stage'], registries: string[], canEdit: boolean): RegistryPolicy => ({
  stage,
  registries,
  source: registries.length ? 'console' : 'configuration',
  updatedBy: registries.length ? 'ana' : null,
  updatedAt: registries.length ? '2026-10-16T11:00:00Z' : null,
  canEdit,
});

const installation: SettingsInstallation = {
  controlPlane: { version: '0.1.0' },
  protocolVersion: '1',
  database: { configured: true, lastMigration: '202610160001_PodInventory', pendingMigrations: 0 },
  dependencies: [
    { name: 'keycloak', status: 'unhealthy', description: 'Keycloak indisponível', durationMs: 120 },
    { name: 'postgresql', status: 'healthy', description: null, durationMs: 4 },
  ],
  checkedAt: '2026-10-16T12:00:00Z',
  agents: {
    total: 3,
    connected: 2,
    updateRequired: 1,
    unknown: 1,
    byAgentVersion: [{ version: '0.1.0.0', count: 2 }],
    byEngine: [{ engine: 'podman', version: '5.4.2', count: 2 }],
  },
};

describe('Configurações (spec 022)', () => {
  it('replaces the coming-soon entry and is shown only with settings access', () => {
    const item = NAV_ITEMS.find(i => i.id === 'settings')!;
    expect(item.route).toBe('/configuracoes');
    expect(item.requiredScope).toBe('settings:read');
    expect(COMING_SOON['configuracoes']).toBeUndefined();
    expect(activeNavId('/configuracoes?vista=limites')).toBe('settings');
  });

  it('joins both policies by stage and never leaves the last change blank', () => {
    const joined = stagePolicies([justification('production', true, false, 'ana')], [registry('production', ['quay.io'], true), registry('none', [], true)]);
    expect(joined.map(p => p.stage)).toEqual(['production', 'staging', 'development']);
    expect(joined[0].justification?.required).toBe(true);
    expect(joined[0].registries?.registries).toEqual(['quay.io']);
    expect(joined[1].justification).toBeNull();
    expect(changedBy(null, null, 'Valor inicial')).toBe('Valor inicial');
    expect(changedBy('ana', '2026-10-16T10:00:00Z', 'Valor inicial')).toMatch(/^Alterada por ana em /);
  });

  it('labels migrations and keeps the console version in the product format', () => {
    expect(migrationLabel('202610160001_PodInventory')).toBe('PodInventory (2026-10-16)');
    expect(migrationLabel(null)).toBe('desconhecido');
    expect(CONSOLE_VERSION).toMatch(/^\d+\.\d+\.\d+$/);
  });
});

describe('settings page', () => {
  const api = {
    justificationPolicies: vi.fn(),
    registryPolicies: vi.fn(),
    installation: vi.fn(),
    bffVersion: vi.fn(),
    parameters: vi.fn(),
  };
  const set = vi.fn();

  beforeEach(() => {
    Object.values(api).forEach(fn => fn.mockReset());
    set.mockReset();
    api.justificationPolicies.mockResolvedValue([justification('production', true, true), justification('staging', false, false), justification('development', false, true)]);
    api.registryPolicies.mockResolvedValue([registry('production', ['quay.io'], true), registry('staging', [], false), registry('development', [], true)]);
    api.installation.mockResolvedValue(installation);
    api.bffVersion.mockResolvedValue(null);
    api.parameters.mockResolvedValue([
      { group: 'retention', key: 'Commands:Retention', label: 'Comandos concluídos e seus parâmetros', value: '30 dias', source: 'installation' },
      { group: 'limits', key: null, label: 'Ações e remoções em lote', value: '50 itens por lote', source: 'default' },
    ]);
    set.mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: SettingsApiService, useValue: api },
        { provide: JustificationPolicyService, useValue: { set } },
      ],
    });
  });

  async function render(vista?: string) {
    const fixture = TestBed.createComponent(SettingsPage);
    if (vista) fixture.componentRef.setInput('vista', vista);
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();
    // ngModel grava o valor nos marcadores na rodada seguinte.
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('shows each stage policy with read-only stages disabled and saves through the shared policy service', async () => {
    const fixture = await render();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Qualquer registro');
    expect(text).toContain('quay.io');
    const boxes = Array.from(fixture.nativeElement.querySelectorAll('input[type="checkbox"]')) as HTMLInputElement[];
    expect(boxes.map(b => [b.checked, b.disabled])).toEqual([[true, false], [false, true], [false, false]]);
    expect(text).toContain('Somente leitura: exige environment:manage');

    boxes[2].click();
    await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve));
    expect(set).toHaveBeenCalledWith('development', true);
    expect(api.justificationPolicies).toHaveBeenCalledTimes(2);
 
    // Falha ao salvar: o marcador volta ao valor salvo.
    set.mockRejectedValueOnce({ status: 403, code: 'authorization_denied' });
    const box = () => (fixture.nativeElement.querySelectorAll('input[type="checkbox"]') as NodeListOf<HTMLInputElement>)[0];
    box().click();
    expect(box().checked).toBe(false);
    for (let i = 0; i < 3; i++) {
      await fixture.whenStable();
      await new Promise(resolve => setTimeout(resolve));
      fixture.detectChanges();
    }
    expect(set).toHaveBeenLastCalledWith('production', false);
    expect(box().checked).toBe(true);
  });

  it('shows versions, an unavailable dependency and the agents needing an update', async () => {
    const fixture = await render('instalacao');
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('PodInventory (2026-10-16) · atualizado');
    expect(text).toContain('indisponível');
    expect(text).toContain('Provedor de identidade (Keycloak)');
    expect(text).toContain('Keycloak indisponível');
    expect(text).toContain('1 · ver em Hosts');
    expect(text).toContain('5.4.2');
  });

  it('shows effective values with their source and the forbidden state', async () => {
    const fixture = await render('limites');
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('30 dias');
    expect(text).toContain('Configurado na instalação');
    expect(text).toContain('Padrão do Dokpod');
    expect(text).toContain('Commands:Retention');

    api.parameters.mockRejectedValue({ status: 403, code: 'authorization_denied' });
    const denied = await render('limites');
    expect(denied.nativeElement.textContent).toContain('Configurações exigem administração de algum host.');
  });
});
