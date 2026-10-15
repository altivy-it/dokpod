import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { getNetwork, listNetworks, removeNetwork, type ListNetworksData, type NetworkItem, type NetworkListPage } from '../../data-access/generated/control-plane';

/** API de redes (spec 019), fora do bundle inicial: só a página Redes usa. */
@Injectable({ providedIn: 'root' })
export class NetworksApiService {
  private readonly http = inject(HttpClient);

  async listNetworks(query: NonNullable<ListNetworksData['query']>): Promise<NetworkListPage> {
    const result = await listNetworks({ query, throwOnError: true });
    return result.data;
  }

  /** Detalhe pelo identificador ou, no link do detalhe do container, pelo nome. */
  async getNetwork(environmentId: string, network: string): Promise<NetworkItem> {
    const result = await getNetwork({ path: { environmentId, network }, throwOnError: true });
    return result.data;
  }

  /** Um comando por rede; a mesma chave de idempotência torna o reenvio seguro (FR-007). */
  async removeNetwork(environmentId: string, networkId: string, idempotencyKey: string, justification: string | null): Promise<string> {
    const { requestToken } = await firstValueFrom(this.http.get<{ requestToken: string }>('bff/antiforgery'));
    const accepted = await removeNetwork({
      path: { environmentId, networkId },
      headers: { 'Idempotency-Key': idempotencyKey, 'X-Dokpod-Antiforgery': requestToken },
      body: { deadlineUtc: new Date(Date.now() + 2 * 60_000).toISOString(), ...(justification ? { justification } : {}) },
      throwOnError: true,
    });
    return accepted.data.commandId;
  }
}
