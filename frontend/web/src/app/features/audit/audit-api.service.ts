import { inject, Injectable } from '@angular/core';
import { SessionService } from '../../core/session.service';
import { getAuditEvent, listAuditEvents, type AuditEventDetail, type AuditEventPage, type ListAuditEventsData } from '../../data-access/generated/control-plane';

/** Consulta da auditoria (spec 017), fora do bundle inicial: só a página Auditoria usa. */
@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly session = inject(SessionService);

  async listAuditEvents(query: NonNullable<ListAuditEventsData['query']>): Promise<AuditEventPage> {
    const result = await listAuditEvents({ query, throwOnError: true });
    return result.data;
  }

  async getAuditEvent(eventId: string, at: string | null): Promise<AuditEventDetail> {
    const result = await getAuditEvent({ path: { eventId }, query: at ? { at } : {}, throwOnError: true });
    return result.data;
  }

  /** CSV de todo o filtro; erros (400 export_too_large, 403) chegam como objeto com status e code. */
  async exportAuditEvents(query: Omit<NonNullable<ListAuditEventsData['query']>, 'cursor' | 'limit'>): Promise<{ blob: Blob; fileName: string }> {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null) continue;
      for (const item of Array.isArray(value) ? value : [value]) params.append(key, String(item));
    }
    const response = await fetch(`${document.baseURI.replace(/\/$/, '')}/api/v1/audit/export?${params}`, { credentials: 'same-origin' });
    if (!response.ok) {
      if (response.status === 401) this.session.expire();
      const problem = (await response.json().catch(() => ({}))) as { code?: string };
      throw { status: response.status, code: problem.code };
    }
    const disposition = response.headers.get('content-disposition') ?? '';
    const fileName = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? 'auditoria.csv';
    return { blob: await response.blob(), fileName };
  }
}
