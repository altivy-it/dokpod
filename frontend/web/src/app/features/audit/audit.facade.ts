import { inject, Injectable, signal } from '@angular/core';
import { AuditApiService } from './audit-api.service';
import type { AuditCategory, AuditEventView, AuditOutcome, ListAuditEventsData, Stage } from '../../data-access/generated/control-plane';

export type AuditPeriod = '24h' | '7d' | '30d' | '90d' | 'personalizado';

export const AUDIT_PERIODS: { id: AuditPeriod; label: string; hours: number }[] = [
  { id: '24h', label: 'Últimas 24 horas', hours: 24 },
  { id: '7d', label: 'Últimos 7 dias', hours: 24 * 7 },
  { id: '30d', label: 'Últimos 30 dias', hours: 24 * 30 },
  { id: '90d', label: 'Últimos 90 dias', hours: 24 * 90 },
  { id: 'personalizado', label: 'Personalizado', hours: 0 },
];

export interface AuditFilters {
  stage: Stage | null;
  since: string;
  until: string;
  environmentIds: string[];
  actor: string | null;
  categories: AuditCategory[];
  outcomes: AuditOutcome[];
  text: string | null;
}

export interface AuditLoadError {
  status: number | null;
  title: string;
  detail: string;
}

/** Período da URL: preset relativo a agora ou dias `yyyy-MM-dd` (início do primeiro, fim do último). */
export function periodRange(period: AuditPeriod, since: string | undefined, until: string | undefined, now = new Date()): { since: string; until: string } | null {
  if (period !== 'personalizado') {
    const hours = AUDIT_PERIODS.find(p => p.id === period)!.hours;
    return { since: new Date(now.getTime() - hours * 3600_000).toISOString(), until: now.toISOString() };
  }
  const day = /^\d{4}-\d{2}-\d{2}$/;
  if (!since || !day.test(since)) return null;
  const start = new Date(`${since}T00:00:00`);
  const end = until && day.test(until) ? new Date(`${until}T23:59:59.999`) : now;
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end <= start) return null;
  if (end.getTime() - start.getTime() > 90 * 24 * 3600_000) return null;
  return { since: start.toISOString(), until: end.toISOString() };
}

export function auditQuery(filters: AuditFilters): Omit<NonNullable<ListAuditEventsData['query']>, 'cursor' | 'limit'> {
  return {
    since: filters.since,
    until: filters.until,
    ...(filters.stage ? { stage: filters.stage } : {}),
    ...(filters.environmentIds.length ? { environmentId: filters.environmentIds } : {}),
    ...(filters.actor ? { actor: filters.actor } : {}),
    ...(filters.categories.length ? { category: filters.categories } : {}),
    ...(filters.outcomes.length ? { outcome: filters.outcomes } : {}),
    ...(filters.text ? { q: filters.text } : {}),
  };
}

/** Trilha paginada da tela Auditoria (spec 017); sempre por período, com cursor do servidor. */
@Injectable()
export class AuditFacade {
  private readonly api = inject(AuditApiService);

  readonly filters = signal<AuditFilters | null>(null);
  readonly events = signal<AuditEventView[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly error = signal<AuditLoadError | null>(null);
  readonly updatedAt = signal<string | null>(null);
  private generation = 0;

  apply(filters: AuditFilters | null): void {
    if (!filters) {
      this.filters.set(null);
      this.events.set([]);
      this.error.set({ status: 400, title: 'Período inválido', detail: 'Escolha um período de até 90 dias, com o fim depois do início.' });
      return;
    }
    const previous = this.filters();
    // Presets relativos mudam a cada leitura da URL; só recarrega quando o filtro muda além do horário.
    if (previous && JSON.stringify({ ...previous, since: '', until: '' }) === JSON.stringify({ ...filters, since: '', until: '' }) &&
      Math.abs(Date.parse(previous.until) - Date.parse(filters.until)) < 60_000 && Math.abs(Date.parse(previous.since) - Date.parse(filters.since)) < 60_000) {
      return;
    }
    this.filters.set(filters);
    void this.reload();
  }

  async reload(): Promise<void> {
    const filters = this.filters();
    if (!filters) return;
    const generation = ++this.generation;
    this.loading.set(true);
    this.error.set(null);
    try {
      const page = await this.api.listAuditEvents({ ...auditQuery(filters), limit: 50 });
      if (generation !== this.generation) return;
      this.events.set(page.events);
      this.nextCursor.set(page.nextCursor ?? null);
      this.updatedAt.set(new Date().toISOString());
    } catch (error) {
      if (generation !== this.generation) return;
      const status = (error as { status?: number })?.status ?? null;
      this.error.set(
        status === 403
          ? { status, title: 'Sem permissão', detail: 'Você não tem acesso à auditoria destes hosts.' }
          : { status, title: 'Não foi possível carregar a auditoria', detail: 'Tente novamente em instantes.' },
      );
    } finally {
      if (generation === this.generation) this.loading.set(false);
    }
  }

  async loadMore(): Promise<void> {
    const filters = this.filters();
    const cursor = this.nextCursor();
    if (!filters || !cursor || this.loadingMore()) return;
    this.loadingMore.set(true);
    try {
      const page = await this.api.listAuditEvents({ ...auditQuery(filters), cursor, limit: 50 });
      this.events.set([...this.events(), ...page.events]);
      this.nextCursor.set(page.nextCursor ?? null);
    } catch {
      this.error.set({ status: null, title: 'Não foi possível carregar mais eventos', detail: 'Tente novamente em instantes.' });
    } finally {
      this.loadingMore.set(false);
    }
  }
}
