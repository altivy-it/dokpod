import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom, type Observable } from 'rxjs';
import type {
  AuditExportDestination,
  AuditExportDestinationInput,
  AuditExportTestResult,
} from '../../data-access/generated/control-plane';
import { newUuid } from '../../core/uuid';

/** Recusa tipada da API (contracts/api.md); nunca traz host interno, resposta do servidor nem segredo. */
export interface AuditExportFailure {
  status: number;
  code: string | null;
}

const ROUTE = 'api/v1/settings/audit-export/destinations';

/**
 * Destinos de exportação da auditoria (spec 027 T022), só na edição Business; a CE nunca chama estas rotas. Usa o
 * `HttpClient` direto, como as mutações do licenciamento, para o código ficar no chunk lazy de Configurações.
 */
@Injectable({ providedIn: 'root' })
export class AuditExportApiService {
  private readonly http = inject(HttpClient);

  async list(): Promise<AuditExportDestination[]> {
    return this.call(() => this.http.get<AuditExportDestination[]>(ROUTE));
  }

  /** A mesma intenção reusa a chave: um clique repetido devolve o mesmo destino por 24 h. */
  async create(
    input: AuditExportDestinationInput,
    idempotencyKey = newUuid(),
  ): Promise<AuditExportDestination> {
    const token = await this.antiforgery();
    return this.call(() =>
      this.http.post<AuditExportDestination>(ROUTE, input, {
        headers: { 'Idempotency-Key': idempotencyKey, 'X-Dokpod-Antiforgery': token },
      }),
    );
  }

  async update(
    destination: AuditExportDestination,
    input: AuditExportDestinationInput,
  ): Promise<AuditExportDestination> {
    const token = await this.antiforgery();
    return this.call(() =>
      this.http.put<AuditExportDestination>(`${ROUTE}/${destination.destinationId}`, input, {
        headers: { 'If-Match': `"${destination.version}"`, 'X-Dokpod-Antiforgery': token },
      }),
    );
  }

  async remove(destination: AuditExportDestination): Promise<void> {
    const token = await this.antiforgery();
    await this.call(() =>
      this.http.delete<void>(`${ROUTE}/${destination.destinationId}`, {
        headers: { 'If-Match': `"${destination.version}"`, 'X-Dokpod-Antiforgery': token },
      }),
    );
  }

  async test(destination: AuditExportDestination): Promise<AuditExportTestResult> {
    const token = await this.antiforgery();
    return this.call(() =>
      this.http.post<AuditExportTestResult>(`${ROUTE}/${destination.destinationId}/test`, null, {
        headers: { 'X-Dokpod-Antiforgery': token },
      }),
    );
  }

  private async call<T>(request: () => Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(request());
    } catch (error: unknown) {
      throw toAuditExportFailure(error);
    }
  }

  private async antiforgery(): Promise<string> {
    return (await firstValueFrom(this.http.get<{ requestToken: string }>('bff/antiforgery')))
      .requestToken;
  }
}

export function toAuditExportFailure(error: unknown): AuditExportFailure {
  if (error instanceof HttpErrorResponse) {
    const code =
      typeof error.error === 'object' && error.error !== null
        ? (error.error as { code?: unknown }).code
        : null;
    return { status: error.status, code: typeof code === 'string' ? code : null };
  }
  const problem = error as { status?: number; code?: unknown } | null;
  return {
    status: problem?.status ?? 0,
    code: typeof problem?.code === 'string' ? problem.code : null,
  };
}
