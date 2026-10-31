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
  consumption: { vcpuBand: 32, current: null, windowPeak: null, windowPeakAt: null, unknownHosts: 0 },
  grace: null,
};

describe('Seção Licença (spec 031 T028)', () => {
  const api = { status: vi.fn(), importLicense: vi.fn() };
  const toast = { success: vi.fn(), error: vi.fn() };

  beforeEach(() => {
    Object.values(api).forEach(fn => fn.mockReset());
    Object.values(toast).forEach(fn => fn.mockReset());
    api.status.mockResolvedValue({ status: granted, etag: '"3"' });
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
    (fixture.nativeElement.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
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

  it('imports with the ETag as If-Match and the chosen slot', async () => {
    api.importLicense.mockResolvedValue({ status: { ...granted, license: { ...granted.license!, revision: 3 } }, etag: '"4"' });
    const fixture = await render();

    await chooseAndImport(fixture, new File(['eyJhbGciOiJFUzI1NiJ9.e30.c2ln'], 'licenca.jws'));

    expect(api.importLicense).toHaveBeenCalledWith(expect.any(File), 'prd', '"3"');
    expect(toast.success).toHaveBeenCalledWith('Licença importada.');
    expect(fixture.nativeElement.textContent).toContain('revisão 3');
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
