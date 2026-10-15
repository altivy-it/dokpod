import type { AlertRule } from '../../data-access/generated/control-plane';
import { describeRule, describeScope, intentionalLabel } from '../../data-access/alert-labels';
import { dayBoundary } from './alerts.page';
import { emptyRuleForm, ruleRequest, ruleToForm } from './rule.drawer';
import { silenceRequest } from './silence.drawer';

const rule: AlertRule = {
  id: 'r1',
  name: 'Reinícios em sequência',
  conditionKind: 'event',
  eventType: 'container.auto_restarted',
  durationSeconds: 600,
  minOccurrences: 3,
  severity: 'high',
  scope: { stage: 'production', environmentId: null, containerPattern: 'api-*' },
  enabled: true,
  builtIn: true,
  version: 4,
};

describe('alert rule form (spec 016 US4)', () => {
  it('round-trips a rule into the request with its version', () => {
    const { request, error } = ruleRequest(ruleToForm(rule), rule.version);
    expect(error).toBeNull();
    expect(request).toEqual(
      expect.objectContaining({
        eventType: 'container.auto_restarted',
        metric: null,
        thresholdPercent: null,
        durationSeconds: 600,
        minOccurrences: 3,
        version: 4,
        scope: { stage: 'production', environmentId: null, containerPattern: 'api-*' },
      }),
    );
  });

  it('rejects invalid thresholds, names and occurrence windows', () => {
    expect(ruleRequest({ ...emptyRuleForm(), name: '' }, null).error).toContain('nome');
    expect(ruleRequest({ ...emptyRuleForm(), name: 'CPU', conditionKind: 'metric', thresholdPercent: 120 }, null).error).toContain('limite');
    expect(ruleRequest({ ...emptyRuleForm(), name: 'Loop', minOccurrences: 3, durationMinutes: 0 }, null).error).toContain('janela');
    const metric = ruleRequest({ ...emptyRuleForm(), name: 'CPU alta', conditionKind: 'metric', metric: 'host_cpu', thresholdPercent: 85, durationMinutes: 5 }, null);
    expect(metric.request).toEqual(expect.objectContaining({ eventType: null, metric: 'host_cpu', thresholdPercent: 85, durationSeconds: 300, minOccurrences: 1 }));
  });

  it('describes rules and scopes for the operator', () => {
    expect(describeRule(rule)).toBe('Container reiniciado automaticamente: 3 vezes em 10 min');
    expect(describeRule({ ...rule, conditionKind: 'metric', metric: 'host_memory', thresholdPercent: 80, durationSeconds: 300, minOccurrences: 1 })).toBe('Memória do host acima de 80% por 5 min');
    expect(describeScope(rule.scope, () => undefined)).toBe('Produção · containers "api-*"');
    expect(describeScope({ stage: null, environmentId: 'e1', containerPattern: null }, () => 'srv-01')).toBe('srv-01');
    expect(describeScope(null, () => undefined)).toBe('Todos os hosts');
    expect(intentionalLabel({ action: 'pause', actor: 'ana', at: '', reason: '' })).toBe('Pausado intencionalmente');
    expect(intentionalLabel({ action: 'stop', actor: 'ana', at: '', reason: '' })).toBe('Parado intencionalmente');
  });
});

describe('silence form (spec 016 US5)', () => {
  const now = new Date('2026-10-14T12:00:00');
  const base = { stage: '' as const, environmentId: 'e1', containerPattern: '', startsAt: '2026-10-14T12:00', endsAt: '2026-10-14T14:00', justification: 'Manutenção do cluster' };

  it('builds the request with the scope and window', () => {
    const { request } = silenceRequest(base, now);
    expect(request?.scope).toEqual({ stage: null, environmentId: 'e1', containerPattern: null });
    expect(new Date(request!.endsAt).getTime() - new Date(request!.startsAt!).getTime()).toBe(2 * 3600_000);
  });

  it('limits the window to 7 days and requires a justification', () => {
    expect(silenceRequest({ ...base, endsAt: '2026-10-22T12:00' }, now).error).toContain('7 dias');
    expect(silenceRequest({ ...base, endsAt: '2026-10-14T11:00' }, now).error).toContain('depois do início');
    expect(silenceRequest({ ...base, justification: 'curta' }, now).error).toContain('justificativa');
  });
});

describe('history period', () => {
  it('turns URL days into day boundaries and ignores invalid values', () => {
    expect(dayBoundary('2026-10-01', false)).toBe(new Date('2026-10-01T00:00:00').toISOString());
    expect(dayBoundary('2026-10-01', true)).toBe(new Date('2026-10-01T23:59:59.999').toISOString());
    expect(dayBoundary('ontem', false)).toBeNull();
    expect(dayBoundary(undefined, true)).toBeNull();
  });
});
