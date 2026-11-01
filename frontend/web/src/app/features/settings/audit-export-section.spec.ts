import { TestBed } from '@angular/core/testing';
import type {
  AuditExportDestination,
  LicensingStatus,
} from '../../data-access/generated/control-plane';
import { ToastService } from '../../design-system';
import { AuditExportApiService } from './audit-export-api.service';
import { AuditExportSection } from './audit-export-section.component';
import { LicensingApiService } from './licensing-api.service';
import { auditExportFailureMessage } from './settings-labels';

function license(
  entitlement: LicensingStatus['entitlement'],
  capabilities = ['audit-siem-export'],
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

const destination: AuditExportDestination = {
  destinationId: '7a1c2d3e-0000-4000-8000-000000000001',
  name: 'siem-soc',
  protocol: 'syslog',
  host: 'siem.exemplo.interno',
  port: 6514,
  path: null,
  secretRef: 'siem-soc',
  secretFiles: { ca: true, clientCertificate: false, token: false },
  categories: ['audit_export', 'containers'],
  active: true,
  state: 'failing',
  lastSentAt: '2026-11-01T12:00:00Z',
  lastError: { code: 'tls_error', at: '2026-11-01T12:05:00Z' },
  pending: { count: 12, capped: false, oldestAt: '2026-11-01T12:00:01Z' },
  lastGapAt: null,
  version: 3,
};

describe('Seção Exportação para SIEM (spec 027 T022)', () => {
  const api = { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), test: vi.fn() };
  const licensing = { status: vi.fn() };
  const toast = { success: vi.fn(), error: vi.fn() };

  beforeEach(() => {
    [...Object.values(api), licensing.status, toast.success, toast.error].forEach((fn) =>
      fn.mockReset(),
    );
    licensing.status.mockResolvedValue({ status: license('granted'), etag: '"1"' });
    api.list.mockResolvedValue([destination]);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuditExportApiService, useValue: api },
        { provide: LicensingApiService, useValue: licensing },
        { provide: ToastService, useValue: toast },
      ],
    });
  });

  async function render() {
    const fixture = TestBed.createComponent(AuditExportSection);
    await settle(fixture);
    return fixture;
  }

  /** A seção lê a licença e depois a lista: espera as duas promessas antes de conferir a tela. */
  async function settle(fixture: {
    detectChanges: () => void;
    whenStable: () => Promise<unknown>;
  }) {
    for (let i = 0; i < 5; i++) {
      fixture.detectChanges();
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve));
    }
    fixture.detectChanges();
  }

  function button(fixture: Awaited<ReturnType<typeof render>>, text: string): HTMLButtonElement {
    return [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === text,
    ) as HTMLButtonElement;
  }

  it('lista o destino com situação, secret só como presença, fila e último erro', async () => {
    const fixture = await render();
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('siem-soc');
    expect(text).toContain('siem.exemplo.interno:6514');
    expect(text).toContain('Falhando');
    expect(text).toContain('siem-soc: CA');
    expect(text).toContain('pendentes: 12');
    expect(text).toContain('Falha de TLS');
  });

  it('não aparece sem a capability audit-siem-export na licença', async () => {
    licensing.status.mockResolvedValue({
      status: license('granted', ['change-windows']),
      etag: '"1"',
    });
    const fixture = await render();

    expect(fixture.nativeElement.textContent.trim()).toBe('');
    expect(api.list).not.toHaveBeenCalled();
  });

  it('cria com syslog e a categoria audit_export marcada e travada', async () => {
    api.create.mockResolvedValue(destination);
    const fixture = await render();
    button(fixture, 'Novo destino').click();
    await settle(fixture);

    const mandatory = [...fixture.nativeElement.querySelectorAll('dok-checkbox')].find(
      (c: HTMLElement) => c.textContent?.includes('Exportação para SIEM'),
    ) as HTMLElement;
    expect((mandatory.querySelector('input') as HTMLInputElement).checked).toBe(true);
    expect((mandatory.querySelector('input') as HTMLInputElement).disabled).toBe(true);

    const component = fixture.componentInstance as unknown as {
      form: () => { name: string; host: string; categories: Record<string, boolean> };
      save: () => Promise<void>;
    };
    Object.assign(component.form(), { name: 'novo', host: 'siem2.interno' });
    component.form().categories['images'] = true;
    await component.save();

    expect(api.create).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'novo',
        protocol: 'syslog',
        host: 'siem2.interno',
        port: 6514,
        categories: ['images', 'audit_export'],
      }),
      expect.any(String),
    );
    expect(toast.success).toHaveBeenCalled();
  });

  it('com a licença restrita, criar, editar e testar ficam desabilitados; desativar e excluir não', async () => {
    licensing.status.mockResolvedValue({ status: license('restricted'), etag: '"1"' });
    const fixture = await render();

    expect(fixture.nativeElement.textContent).toContain('Licença restrita');
    expect(button(fixture, 'Novo destino').disabled).toBe(true);
    expect(button(fixture, 'Editar').disabled).toBe(true);
    expect(button(fixture, 'Enviar evento de teste').disabled).toBe(true);
    expect(button(fixture, 'Desativar').disabled).toBe(false);
    expect(button(fixture, 'Excluir').disabled).toBe(false);
  });

  it('mostra o resultado do evento de teste', async () => {
    api.test
      .mockResolvedValueOnce({ delivered: true, errorCode: null })
      .mockResolvedValueOnce({ delivered: false, errorCode: 'connection_refused' });
    const fixture = await render();

    button(fixture, 'Enviar evento de teste').click();
    await settle(fixture);
    expect(toast.success).toHaveBeenCalledWith('Evento de teste entregue a siem-soc.');

    button(fixture, 'Enviar evento de teste').click();
    await settle(fixture);
    expect(toast.error).toHaveBeenCalledWith(
      'Evento de teste não entregue: Conexão recusada ou host não encontrado.',
    );
  });

  it('desativa com a versão lida e traduz as recusas da API', async () => {
    api.update.mockResolvedValue({ ...destination, active: false });
    const fixture = await render();
    button(fixture, 'Desativar').click();
    await settle(fixture);

    expect(api.update).toHaveBeenCalledWith(
      destination,
      expect.objectContaining({ active: false, categories: destination.categories }),
    );
    expect(auditExportFailureMessage('audit_export_limit_reached', 409)).toContain('5 destinos');
    expect(auditExportFailureMessage('license_expired', 403)).toContain('licença');
    expect(auditExportFailureMessage('rate_limited', 429)).toContain('5 testes');
  });
});
