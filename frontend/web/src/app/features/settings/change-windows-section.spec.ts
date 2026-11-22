import { TestBed } from '@angular/core/testing';
import type { ChangeWindowItem, LicensingStatus } from '../../data-access/generated/control-plane';
import { ToastService } from '../../design-system';
import { ChangeWindowsApiService } from './change-windows-api.service';
import { ChangeWindowsSection } from './change-windows-section.component';
import { LicensingApiService } from './licensing-api.service';
import { changeWindowFailureMessage } from './settings-labels';

function license(
  entitlement: LicensingStatus['entitlement'],
  capabilities = ['change-windows'],
): LicensingStatus {
  return {
    entitlement,
    authenticity: 'valid',
    validity: entitlement === 'restricted' ? 'expired' : 'active',
    metering: 'regular',
    reasons: [],
    license: {
      licenseId: 'lic_1',
      organizationId: 'org_1',
      revision: 1,
      plan: 'business',
      notBefore: '2026-11-01T00:00:00Z',
      expiresAt: '2027-11-01T00:00:00Z',
      capabilities,
      capacity: { mode: 'limited', nodeEquivalents: 10 },
    },
    installation: { slotId: 'prd', kind: 'production', quota: 8 },
    consumption: { vcpuBand: 32, current: 1, windowPeak: 1, windowPeakAt: null, unknownHosts: 0 },
    grace: null,
  } as LicensingStatus;
}

const production: ChangeWindowItem = {
  stage: 'production',
  timeZone: 'America/Sao_Paulo',
  ranges: [{ days: ['mon', 'tue', 'wed', 'thu', 'fri'], start: '22:00', end: '06:00' }],
  updatedBy: 'Ana',
  updatedAt: '2026-11-20T18:00:00Z',
  version: 3,
  editable: true,
  status: { state: 'closed', closesAt: null, nextOpensAt: '2026-11-25T01:00:00Z', timeZone: 'America/Sao_Paulo' },
};

const empty = (stage: ChangeWindowItem['stage'], editable = true): ChangeWindowItem => ({
  stage,
  timeZone: null,
  ranges: [],
  updatedBy: null,
  updatedAt: null,
  version: null,
  editable,
  status: { state: 'none', closesAt: null, nextOpensAt: null, timeZone: null },
});

describe('Seção Janelas de mudança (spec 026 T019)', () => {
  const api = { list: vi.fn(), save: vi.fn(), remove: vi.fn() };
  const licensing = { status: vi.fn() };
  const toast = { success: vi.fn(), error: vi.fn() };

  beforeEach(() => {
    [...Object.values(api), licensing.status, toast.success, toast.error].forEach((fn) => fn.mockReset());
    licensing.status.mockResolvedValue({ status: license('granted'), etag: '"1"' });
    api.list.mockResolvedValue([production, empty('staging'), empty('development', false)]);
    TestBed.configureTestingModule({
      providers: [
        { provide: ChangeWindowsApiService, useValue: api },
        { provide: LicensingApiService, useValue: licensing },
        { provide: ToastService, useValue: toast },
      ],
    });
  });

  async function render() {
    const fixture = TestBed.createComponent(ChangeWindowsSection);
    await settle(fixture);
    return fixture;
  }

  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<unknown> }) {
    for (let i = 0; i < 5; i++) {
      fixture.detectChanges();
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve));
    }
    fixture.detectChanges();
  }

  function button(fixture: Awaited<ReturnType<typeof render>>, label: string): HTMLButtonElement | undefined {
    return [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.getAttribute('aria-label') === label || b.textContent?.trim() === label,
    ) as HTMLButtonElement | undefined;
  }

  function stage(fixture: Awaited<ReturnType<typeof render>>, id: string): HTMLElement {
    return fixture.nativeElement.querySelector(`[data-stage="${id}"]`) as HTMLElement;
  }

  it('mostra as faixas, o fuso, o estado com a próxima abertura e o autor', async () => {
    const fixture = await render();
    const text = stage(fixture, 'production').textContent as string;

    expect(text).toContain('Produção');
    expect(text).toContain('seg, ter, qua, qui, sex 22:00–06:00 (cruza a meia-noite)');
    expect(text).toContain('America/Sao_Paulo');
    expect(text).toContain('Fora da janela de mudança');
    expect(text).toContain('Abre em');
    expect(text).toContain('Alterada por Ana');
    expect(stage(fixture, 'staging').textContent).toContain('Sem janela: ações aceitas a qualquer hora.');
  });

  it('não aparece sem a capability na licença', async () => {
    licensing.status.mockResolvedValue({ status: license('granted', ['audit-siem-export']), etag: '"1"' });
    const fixture = await render();

    expect(fixture.nativeElement.textContent.trim()).toBe('');
    expect(api.list).not.toHaveBeenCalled();
  });

  it('edita com a versão lida e cria sem versão', async () => {
    api.save.mockResolvedValue(production);
    const fixture = await render();

    button(fixture, 'Editar a janela de Produção')!.click();
    await settle(fixture);
    (fixture.nativeElement.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(api.save).toHaveBeenCalledWith('production', 3, {
      timeZone: 'America/Sao_Paulo',
      ranges: [{ days: ['mon', 'tue', 'wed', 'thu', 'fri'], start: '22:00', end: '06:00' }],
    });

    button(fixture, 'Definir a janela de Homologação')!.click();
    await settle(fixture);
    (fixture.nativeElement.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await settle(fixture);
    expect(api.save).toHaveBeenLastCalledWith('staging', null, expect.objectContaining({ ranges: expect.any(Array) }));
    expect(toast.success).toHaveBeenCalled();
  });

  it('sem permissão no estágio, só leitura e sem botões', async () => {
    const fixture = await render();

    expect(button(fixture, 'Definir a janela de Desenvolvimento')).toBeUndefined();
    expect(stage(fixture, 'development').textContent).toContain('Somente leitura.');
  });

  it('versão divergente (412) avisa e relê a lista', async () => {
    api.remove.mockRejectedValue({ status: 412, code: 'precondition_failed' });
    const fixture = await render();

    button(fixture, 'Remover a janela de Produção')!.click();
    await settle(fixture);

    expect(api.remove).toHaveBeenCalledWith('production', 3);
    expect(toast.error).toHaveBeenCalledWith(changeWindowFailureMessage('precondition_failed', 412));
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('com a licença restrita, editar fica bloqueado e remover continua', async () => {
    licensing.status.mockResolvedValue({ status: license('restricted'), etag: '"1"' });
    api.remove.mockResolvedValue(undefined);
    const fixture = await render();

    expect(fixture.nativeElement.textContent).toContain('Licença restrita');
    expect(button(fixture, 'Editar a janela de Produção')!.disabled).toBe(true);
    button(fixture, 'Remover a janela de Produção')!.click();
    await settle(fixture);
    expect(api.remove).toHaveBeenCalledWith('production', 3);
  });

  it('recusa faixa com início igual ao fim antes de enviar', async () => {
    const fixture = await render();

    button(fixture, 'Editar a janela de Produção')!.click();
    await settle(fixture);
    const end = fixture.nativeElement.querySelectorAll('form input[type="text"]')[2] as HTMLInputElement;
    end.value = '22:00';
    end.dispatchEvent(new Event('input'));
    await settle(fixture);

    expect(fixture.nativeElement.textContent).toContain('Início e fim precisam ser diferentes.');
    expect(button(fixture, 'Salvar')!.disabled).toBe(true);
  });
});
