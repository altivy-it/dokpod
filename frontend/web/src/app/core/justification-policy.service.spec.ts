import { TestBed } from '@angular/core/testing';
import { ControlPlaneApiService } from '../data-access/control-plane-api.service';
import { JustificationPolicyService, justificationMode, justificationValid } from './justification-policy.service';

describe('justification policy (spec 016)', () => {
  const required = new Set(['production'] as const);

  it('asks for justification only on stop, pause and delete, mandatory where the stage requires it', () => {
    expect(justificationMode('stop', ['production'], required)).toBe('required');
    expect(justificationMode('pause', ['development', 'production'], required)).toBe('required');
    expect(justificationMode('delete', ['staging'], required)).toBe('optional');
    expect(justificationMode('delete', [null], required)).toBe('optional');
    expect(justificationMode('restart', ['production'], required)).toBe('none');
    expect(justificationMode('start', ['production'], required)).toBe('none');
    expect(justificationMode('unpause', ['production'], required)).toBe('none');
  });

  it('validates 10 to 500 characters, allowing empty only when optional', () => {
    expect(justificationValid('', 'required')).toBe(false);
    expect(justificationValid('curta', 'required')).toBe(false);
    expect(justificationValid('  Manutenção programada  ', 'required')).toBe(true);
    expect(justificationValid('x'.repeat(501), 'required')).toBe(false);
    expect(justificationValid('', 'optional')).toBe(true);
    expect(justificationValid('curta', 'optional')).toBe(false);
    expect(justificationValid('', 'none')).toBe(true);
  });

  it('loads the policies and reloads after a change', async () => {
    const api = {
      listJustificationPolicies: vi
        .fn()
        .mockResolvedValueOnce({ policies: [{ stage: 'production', required: true, actions: [] }, { stage: 'staging', required: false, actions: [] }] })
        .mockResolvedValueOnce({ policies: [{ stage: 'production', required: true, actions: [] }, { stage: 'staging', required: true, actions: [] }] }),
      setJustificationPolicy: vi.fn().mockResolvedValue(undefined),
    };
    TestBed.configureTestingModule({ providers: [{ provide: ControlPlaneApiService, useValue: api }] });
    const service = TestBed.inject(JustificationPolicyService);

    await service.load();
    expect([...service.required()]).toEqual(['production']);
    await service.set('staging', true);
    expect(api.setJustificationPolicy).toHaveBeenCalledWith('staging', true);
    expect([...service.required()].sort()).toEqual(['production', 'staging']);
  });
});
