import { TestBed } from '@angular/core/testing';
import type { LicensingStatus } from '../../data-access/generated/control-plane';
import { ToastService } from '../../design-system';
import { LicensingApiService, MAX_LICENSE_BYTES } from './licensing-api.service';
import { LicensingSection } from './licensing-section.component';
import { importFailureMessage } from './licensing-labels';

const granted: LicensingStatus = {
  entitlement: 'granted',
  authenticity: 'valid',
  validity: 'active',
  metering: 'regular',
  reasons: [],
  license: {
    licenseId: 'lic_7f3c9a',
    organizationId: 'org_51d2e0',
    revision: 2,
    plan: 'business',
    notBefore: '2026-11-01T00:00:00Z',
    expiresAt: '2027-11-01T00:00:00Z',
    capabilities: ['change-windows'],
    capacity: { mode: 'limited', nodeEquivalents: 10 },
  },
  installation: { slotId: 'prd', kind: 'production', quota: 8 },
  consumption: { vcpuBand: 32, current: 5, windowPeak: 7, windowPeakAt: '2026-11-20T15:00:00Z', unknownHosts: 1 },
  grace: null,
};

describe('Seção Licença (spec 031 T028)', () => {
  const api = { status: vi.fn(), importLicense: vi.fn(), importRevocation: vi.fn(), exportReport: vi.fn(), hosts: vi.fn() };
  const toast = { success: vi.fn(), error: vi.fn() };

  beforeEach(() => {
    Object.values(api).forEach(fn => fn.mockReset());
    Object.values(toast).forEach(fn => fn.mockReset());
    api.status.mockResolvedValue({ status: granted, etag: '"3"' });
    api.hosts.mockResolvedValue([
      { hostId: 'h1', label: 'rack-1', removed: false, environmentIds: ['e1'], vcpus: 32, source: 'engine', nodeEquivalents: 1 },
      { hostId: 'h2', label: 'legado-n-1', removed: false, environmentIds: ['e2'], vcpus: null, source: 'unknown', nodeEquivalents: null },
    ]);
    TestBed.configureTestingModule({
      providers: [
        { provide: LicensingApiService, useValue: api },
        { provide: ToastService, useValue: toast },
      ],
    });
  });

  async function render() {
    const fixture = TestBed.createComponent(LicensingSection);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  async function chooseAndImport(fixture: Awaited<ReturnType<typeof render>>, file: File) {
    const input = fixture.nativeElement.querySelector('#lic-file') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('form[aria-label="Importar licença"]') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows state, organization, plan, validity, capacity, installation quota and capabilities', async () => {
    const text = (await render()).nativeElement.textContent as string;
    expect(text).toContain('Licença válida');
    expect(text).toContain('org_51d2e0');
    expect(text).toContain('lic_7f3c9a · revisão 2');
    expect(text).toContain('10 nodes equivalentes · faixa de 32 vCPU');
    expect(text).toContain('prd (produção) · 8 nodes equivalentes');
    expect(text).toContain('change-windows');
  });

  it('shows the metering and highlights hosts with unknown measurement (T040)', async () => {
    const fixture = await render();
    await fixture.whenStable();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('5 nodes equivalentes');
    expect(text).toContain('7 nodes equivalentes em');
    expect(text).toContain('32 vCPU por node equivalente');
    expect(text).toContain('1 host(s) com medição desconhecida');
    expect(text).toContain('legado-n-1');
    expect(text).not.toContain('rack-1:');
  });

  it('imports with the ETag as If-Match and the chosen slot', async () => {
    api.importLicense.mockResolvedValue({ status: { ...granted, license: { ...granted.license!, revision: 3 } }, etag: '"4"' });
    const fixture = await render();

    await chooseAndImport(fixture, new File(['eyJhbGciOiJFUzI1NiJ9.e30.c2ln'], 'licenca.jws'));

    expect(api.importLicense).toHaveBeenCalledWith(expect.any(File), 'prd', '"3"');
    expect(toast.success).toHaveBeenCalledWith('Licença importada.');
    expect(fixture.nativeElement.textContent).toContain('revisão 3');
  });

  it('shows reasons, the grace deadline and the expiry warning (T048)', async () => {
    const soon = new Date(Date.now() + 20 * 86_400_000).toISOString();
    api.status.mockResolvedValue({
      status: {
        ...granted,
        entitlement: 'granted_in_grace',
        metering: 'excess_in_grace',
        reasons: ['license_capacity_exceeded', 'metering_unknown'],
        license: { ...granted.license!, expiresAt: soon },
        grace: { cause: 'excess', startedAt: '2026-11-01T00:00:00Z', until: '2026-12-01T00:00:00Z' },
      },
      etag: '"3"',
    });

    const text = (await render()).nativeElement.textContent as string;

    expect(text).toContain('Em tolerância');
    expect(text).toContain('Consumo acima da cota da instalação');
    expect(text).toContain('Há hosts com medição desconhecida');
    expect(text).toContain('Tolerância por excesso de consumo até');
    expect(text).toMatch(/Vence em (19|20) dia\(s\)/);
  });

  it('imports a revocation package and shows the revoked state (T052)', async () => {
    api.importRevocation.mockResolvedValue({ status: { ...granted, entitlement: 'restricted', authenticity: 'revoked', reasons: ['license_revoked'] }, etag: '"4"' });
    const fixture = await render();
    const input = fixture.nativeElement.querySelector('#rev-file') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [new File(['pacote'], 'revogacao.jws')], configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('form[aria-label="Importar pacote de revogação"]') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.importRevocation).toHaveBeenCalledWith(expect.any(File));
    expect(toast.success).toHaveBeenCalledWith('Pacote de revogação importado.');
    expect(fixture.nativeElement.textContent).toContain('Licença revogada pelo fornecedor');

    api.importRevocation.mockRejectedValue({ status: 422, code: 'license_obsolete' });
    (fixture.nativeElement.querySelector('form[aria-label="Importar pacote de revogação"]') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(toast.error).toHaveBeenCalledWith('A sequência do pacote não é maior que a já importada.');
  });

  it('exports the report only with a valid challenge and saves it locally (T060)', async () => {
    api.exportReport.mockResolvedValue(new Blob(['{}'], { type: 'application/json' }));
    const create = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:relatorio');
    const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const fixture = await render();
    const form = () => fixture.nativeElement.querySelector('form[aria-label="Exportar relatório de apuração"]') as HTMLFormElement;
    const input = form().querySelector('input') as HTMLInputElement;

    input.value = 'curto';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    form().dispatchEvent(new Event('submit'));
    expect(api.exportReport).not.toHaveBeenCalled();

    input.value = 'rnw-2027-org-a91f';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    form().dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(api.exportReport).toHaveBeenCalledWith('rnw-2027-org-a91f');
    expect(create).toHaveBeenCalled();
    expect(revoke).toHaveBeenCalledWith('blob:relatorio');
    expect(toast.success).toHaveBeenCalledWith('Relatório exportado. Envie o arquivo ao fornecedor pelo canal combinado.');
  });

  it('shows a typed refusal without echoing the file and keeps the current license', async () => {
    api.importLicense.mockRejectedValue({ status: 422, code: 'license_invalid' });
    const fixture = await render();

    await chooseAndImport(fixture, new File(['eyJhbGciOiJFUzI1NiJ9.SEGREDO.c2ln'], 'licenca.jws'));

    expect(toast.error).toHaveBeenCalledWith(importFailureMessage('license_invalid', 422));
    expect(JSON.stringify(toast.error.mock.calls)).not.toContain('SEGREDO');
    expect(fixture.nativeElement.textContent).not.toContain('SEGREDO');
    expect(fixture.nativeElement.textContent).toContain('lic_7f3c9a · revisão 2');
  });

  it('refuses files above 64 KiB before uploading', async () => {
    const fixture = await render();

    await chooseAndImport(fixture, new File([new Uint8Array(MAX_LICENSE_BYTES + 1)], 'grande.jws'));

    expect(api.importLicense).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith(importFailureMessage('payload_too_large', 413));
  });

  it('shows the forbidden state without licensing:read', async () => {
    api.status.mockRejectedValue({ status: 403, code: 'authorization_denied' });

    expect((await render()).nativeElement.textContent).toContain('Ver a licença exige a permissão licensing:read.');
  });
});
