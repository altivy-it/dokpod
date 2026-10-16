import type { AuditEventView } from '../../data-access/generated/control-plane';
import { AUDIT_ACTION, auditActionLabel, auditActorLabel } from '../../data-access/audit-labels';
import { targetLink } from './audit-event.drawer';
import { auditQuery, periodRange } from './audit.facade';

const event = (overrides: Partial<AuditEventView>): AuditEventView => ({
  id: 'e1',
  occurredAt: '2026-10-15T12:00:00Z',
  correlationId: 'c1',
  actorKind: 'user',
  actorId: 'sub-1',
  action: 'container.stop',
  environmentId: 'env-1',
  outcome: 'succeeded',
  ...overrides,
});

describe('audit period (spec 017)', () => {
  const now = new Date('2026-10-15T12:00:00Z');

  it('turns presets into ranges ending now', () => {
    expect(periodRange('7d', undefined, undefined, now)).toEqual({ since: '2026-10-08T12:00:00.000Z', until: '2026-10-15T12:00:00.000Z' });
  });

  it('accepts custom days up to 90 days and rejects longer or inverted ranges', () => {
    const range = periodRange('personalizado', '2026-10-01', '2026-10-02', now)!;
    expect(new Date(range.since).getTime()).toBe(new Date('2026-10-01T00:00:00').getTime());
    expect(new Date(range.until).getTime()).toBe(new Date('2026-10-02T23:59:59.999').getTime());
    expect(periodRange('personalizado', '2026-01-01', '2026-10-01', now)).toBeNull();
    expect(periodRange('personalizado', '2026-10-05', '2026-10-01', now)).toBeNull();
    expect(periodRange('personalizado', 'ontem', undefined, now)).toBeNull();
  });

  it('builds the API query only with the filters in use', () => {
    expect(
      auditQuery({ stage: null, since: 's', until: 'u', environmentIds: [], actor: null, categories: ['terminal'], outcomes: ['denied'], text: null }),
    ).toEqual({ since: 's', until: 'u', category: ['terminal'], outcome: ['denied'] });
  });
});

describe('audit labels and targets', () => {
  it('labels every action supported by the backend list and keeps unknown actions verbatim', () => {
    expect(Object.keys(AUDIT_ACTION)).toHaveLength(40);
    expect(auditActionLabel('container.stop')).toBe('Parar container');
    expect(auditActionLabel('futuro.acao')).toBe('futuro.acao');
  });

  it('shows the user name, the identifier when there is none, and the kind for agents', () => {
    expect(auditActorLabel(event({ actorName: 'ana' }))).toBe('ana');
    expect(auditActorLabel(event({ actorName: null }))).toBe('sub-1');
    expect(auditActorLabel(event({ actorKind: 'agent', actorName: null }))).toBe('Agente');
  });

  it('links containers and alerts that can still be opened', () => {
    expect(targetLink(event({ subjectId: 'a'.repeat(64) }))?.route).toEqual(['/containers', 'env-1', 'a'.repeat(64)]);
    expect(targetLink(event({ action: 'alert.acknowledged', subjectId: 'x', details: { alertId: 'al-1' } }))).toEqual({ route: ['/alertas'], query: { alerta: 'al-1' } });
    expect(targetLink(event({ action: 'audit.exported', subjectId: 'audit' }))).toBeNull();
    expect(targetLink(event({ subjectId: null }))).toBeNull();
  });
});
