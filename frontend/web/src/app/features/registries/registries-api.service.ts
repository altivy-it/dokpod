import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  getRegistry,
  getRegistryPolicyImpact,
  listRegistries,
  listRegistryPolicies,
  setRegistryPolicy,
  type ListRegistriesData,
  type RegistryDetail,
  type RegistryListPage,
  type RegistryPolicy,
  type RegistryPolicyImpact,
} from '../../data-access/generated/control-plane';

export type PolicyStage = RegistryPolicy['stage'];

/** API de registros e da política por estágio (spec 020), fora do bundle inicial. */
@Injectable({ providedIn: 'root' })
export class RegistriesApiService {
  private readonly http = inject(HttpClient);

  async listRegistries(query: NonNullable<ListRegistriesData['query']>): Promise<RegistryListPage> {
    return (await listRegistries({ query, throwOnError: true })).data;
  }

  async getRegistry(registry: string): Promise<RegistryDetail> {
    return (await getRegistry({ path: { registry }, throwOnError: true })).data;
  }

  async listPolicies(): Promise<RegistryPolicy[]> {
    return (await listRegistryPolicies({ throwOnError: true })).data.policies;
  }

  async impact(stage: PolicyStage, registries: string[]): Promise<RegistryPolicyImpact> {
    return (await getRegistryPolicyImpact({ path: { stage }, query: registries.length ? { registries } : {}, throwOnError: true })).data;
  }

  async savePolicy(stage: PolicyStage, registries: string[]): Promise<void> {
    const { requestToken } = await firstValueFrom(this.http.get<{ requestToken: string }>('bff/antiforgery'));
    await setRegistryPolicy({ path: { stage }, headers: { 'X-Dokpod-Antiforgery': requestToken }, body: { registries }, throwOnError: true });
  }
}

/** Mesma forma canônica do servidor: só host[:porta], minúsculo; esquema, caminho e credencial são recusados. */
export function normalizeRegistry(value: string): string | null {
  const text = value.trim().toLowerCase();
  if (!text || text.length > 255 || text.includes('://') || text.includes('/') || text.includes('@')) return null;
  const name = text === 'index.docker.io' || text === 'registry-1.docker.io' ? 'docker.io' : text;
  return /^(?:[a-z0-9]|[a-z0-9][a-z0-9-]*[a-z0-9])(?:\.(?:[a-z0-9]|[a-z0-9][a-z0-9-]*[a-z0-9]))*(?::[0-9]{1,5})?$/.test(name) ? name : null;
}
