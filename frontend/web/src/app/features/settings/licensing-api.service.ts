import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { getLicensingStatus, type LicensingStatus } from '../../data-access/generated/control-plane';

/** Estado da licença e a versão (`ETag`) exigida no `If-Match` da importação (spec 031 T028). */
export interface LicensingSnapshot {
  status: LicensingStatus;
  etag: string;
}

/** Recusa tipada da API, sem nenhum trecho do arquivo (FR-027). */
export interface LicensingFailure {
  status: number;
  code: string | null;
}

export const MAX_LICENSE_BYTES = 65536;

/** API de licenciamento da edição Business (spec 031), fora do bundle inicial; a CE nunca a chama. */
@Injectable({ providedIn: 'root' })
export class LicensingApiService {
  private readonly http = inject(HttpClient);

  async status(): Promise<LicensingSnapshot> {
    const result = await getLicensingStatus({ throwOnError: true });
    return { status: result.data, etag: result.response.headers.get('ETag') ?? '"0"' };
  }

  /** Envia o arquivo como `application/jose`, com `If-Match` e antiforgery do BFF. */
  async importLicense(file: Blob, slotId: string, etag: string): Promise<LicensingSnapshot> {
    const [{ requestToken }, body] = await Promise.all([
      firstValueFrom(this.http.get<{ requestToken: string }>('bff/antiforgery')),
      file.text(),
    ]);
    try {
      const response = await firstValueFrom(
        this.http.put<LicensingStatus>('api/v1/licensing/license', body, {
          headers: { 'Content-Type': 'application/jose', 'If-Match': etag, 'X-Dokpod-Antiforgery': requestToken },
          params: { slotId },
          observe: 'response',
        }),
      );
      return { status: response.body!, etag: response.headers.get('ETag') ?? etag };
    } catch (error: unknown) {
      throw toLicensingFailure(error);
    }
  }
}

export function toLicensingFailure(error: unknown): LicensingFailure {
  if (error instanceof HttpErrorResponse) {
    const code = typeof error.error === 'object' && error.error !== null ? ((error.error as { code?: unknown }).code ?? null) : null;
    return { status: error.status, code: typeof code === 'string' ? code : null };
  }
  const problem = error as { status?: number; code?: string } | null;
  return { status: problem?.status ?? 0, code: problem?.code ?? null };
}
