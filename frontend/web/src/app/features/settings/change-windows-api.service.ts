import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom, type Observable } from 'rxjs';
import type {
  ChangeWindowItem,
  ChangeWindowList,
  ChangeWindowRequest,
  Stage,
} from '../../data-access/generated/control-plane';
import { toAuditExportFailure } from './audit-export-api.service';

const ROUTE = 'api/v1/change-windows';

/**
 * Janelas de mudança por estágio (spec 026 T019), só na edição Business; a CE nunca chama estas rotas. Usa o
 * `HttpClient` direto, como a exportação da 027, para o código ficar no chunk lazy de Configurações.
 */
@Injectable({ providedIn: 'root' })
export class ChangeWindowsApiService {
  private readonly http = inject(HttpClient);

  async list(): Promise<ChangeWindowItem[]> {
    return (await this.call(() => this.http.get<ChangeWindowList>(ROUTE))).stages;
  }

  /** Sem versão, cria (`If-None-Match: *`); com versão, edita aquela versão (`If-Match`). */
  async save(stage: Stage, version: number | null, input: ChangeWindowRequest): Promise<ChangeWindowItem> {
    const token = await this.antiforgery();
    const headers: Record<string, string> = { 'X-Dokpod-Antiforgery': token };
    if (version === null) headers['If-None-Match'] = '*';
    else headers['If-Match'] = `"${version}"`;
    return this.call(() => this.http.put<ChangeWindowItem>(`${ROUTE}/${stage}`, input, { headers }));
  }

  async remove(stage: Stage, version: number): Promise<void> {
    const token = await this.antiforgery();
    await this.call(() =>
      this.http.delete<void>(`${ROUTE}/${stage}`, {
        headers: { 'If-Match': `"${version}"`, 'X-Dokpod-Antiforgery': token },
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
