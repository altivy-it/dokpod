import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  getVolume,
  getVolumeUsage,
  listVolumes,
  removeVolume,
  type ListVolumesData,
  type VolumeItem,
  type VolumeListPage,
  type VolumeUsage,
} from '../../data-access/generated/control-plane';

/** API de volumes (spec 018), fora do bundle inicial: só a página Volumes usa. */
@Injectable({ providedIn: 'root' })
export class VolumesApiService {
  private readonly http = inject(HttpClient);

  async listVolumes(query: NonNullable<ListVolumesData['query']>): Promise<VolumeListPage> {
    const result = await listVolumes({ query, throwOnError: true });
    return result.data;
  }

  async getVolume(environmentId: string, name: string): Promise<VolumeItem> {
    const result = await getVolume({ path: { environmentId, name }, throwOnError: true });
    return result.data;
  }

  /** Tamanho segundo o engine, consultado ao agente só quando o operador pede (FR-004). */
  async getVolumeUsage(environmentId: string, name: string): Promise<VolumeUsage> {
    const result = await getVolumeUsage({ path: { environmentId, name }, throwOnError: true });
    return result.data;
  }

  /** Um comando por volume; a mesma chave de idempotência torna o reenvio seguro (FR-007). */
  async removeVolume(environmentId: string, name: string, idempotencyKey: string, justification: string | null): Promise<string> {
    const { requestToken } = await firstValueFrom(this.http.get<{ requestToken: string }>('bff/antiforgery'));
    const accepted = await removeVolume({
      path: { environmentId, name },
      headers: { 'Idempotency-Key': idempotencyKey, 'X-Dokpod-Antiforgery': requestToken },
      body: { deadlineUtc: new Date(Date.now() + 2 * 60_000).toISOString(), ...(justification ? { justification } : {}) },
      throwOnError: true,
    });
    return accepted.data.commandId;
  }
}
