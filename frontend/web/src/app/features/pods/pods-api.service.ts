import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { getPod, listPods, submitPodAction, type ListPodsData, type PodAction, type PodItem, type PodListPage } from '../../data-access/generated/control-plane';

/** API de pods (spec 021), fora do bundle inicial. */
@Injectable({ providedIn: 'root' })
export class PodsApiService {
  private readonly http = inject(HttpClient);

  async listPods(query: NonNullable<ListPodsData['query']>): Promise<PodListPage> {
    return (await listPods({ query, throwOnError: true })).data;
  }

  async getPod(environmentId: string, pod: string): Promise<PodItem> {
    return (await getPod({ path: { environmentId, pod }, throwOnError: true })).data;
  }

  /** Pod que contém o container (link do detalhe do container), ou nulo. */
  async podOfContainer(environmentId: string, containerId: string): Promise<PodItem | null> {
    const page = await this.listPods({ environmentId: [environmentId], containerId, limit: 1 });
    return page.pods[0] ?? null;
  }

  /** Um comando por pod; a mesma chave de idempotência torna o reenvio seguro. */
  async submitAction(environmentId: string, podId: string, action: PodAction, idempotencyKey: string, justification: string | null): Promise<string> {
    const { requestToken } = await firstValueFrom(this.http.get<{ requestToken: string }>('bff/antiforgery'));
    const accepted = await submitPodAction({
      path: { environmentId, podId },
      headers: { 'Idempotency-Key': idempotencyKey, 'X-Dokpod-Antiforgery': requestToken },
      body: { action, deadlineUtc: new Date(Date.now() + 2 * 60_000).toISOString(), ...(justification ? { justification } : {}) },
      throwOnError: true,
    });
    return accepted.data.commandId;
  }
}
