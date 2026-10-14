import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  getContainer,
  getContainerCommand,
  getContainerLogs,
  getContainerSummary,
  inspectContainer,
  getEnvironment,
  getOverview,
  listContainers,
  listEnvironmentContainers,
  listEnvironments,
  listEvents,
  registerEnvironment,
  revokeAgentIdentity,
  setEnvironmentStage,
  submitContainerCommand,
  type ContainerConfigSummary,
  type ContainerDetail,
  type ContainerInspection,
  type ContainerListPage,
  type ContainerLogPage,
  type GetContainerLogsData,
  type ListContainersData,
  type ListEnvironmentsData,
  type ListEventsData,
  type OperationalEventPage,
  type OverviewSummary,
  type Stage,
  type ContainerCommandStatus,
  type ContainerInventory,
  type EnvironmentState,
  type EnvironmentPage,
  type InventoryPage,
  type RegisterEnvironmentRequest,
  type SubmitContainerCommandRequest,
} from './generated/control-plane';
import { client } from './generated/control-plane/client.gen';
import { SessionService } from '../core/session.service';

export type ContainerAction = SubmitContainerCommandRequest['action'];

interface AntiforgeryResponse {
  requestToken: string;
}

@Injectable({ providedIn: 'root' })
export class ControlPlaneApiService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);

  constructor() {
    client.setConfig({
      baseUrl: document.baseURI.replace(/\/$/, ''),
      credentials: 'same-origin',
    });
    client.interceptors.error.use((error, response) => {
      if (!response) {
        return error;
      }
      if (response.status === 401) this.session.expire();

      return typeof error === 'object' && error !== null
        ? { ...error, status: response.status }
        : { status: response.status };
    });
  }

  async listContainers(environmentId: string, cursor?: string): Promise<InventoryPage> {
    const result = await listEnvironmentContainers({
      path: { environmentId },
      query: { cursor, limit: 50 },
      throwOnError: true,
    });

    return result.data;
  }

  async listEnvironments(cursor?: string, filters: Omit<NonNullable<ListEnvironmentsData['query']>, 'cursor'> = {}): Promise<EnvironmentPage> {
    const result = await listEnvironments({
      query: { cursor, ...{ limit: 20, ...filters } },
      throwOnError: true,
    });

    return result.data;
  }

  async listAllContainers(query: NonNullable<ListContainersData['query']>): Promise<ContainerListPage> {
    const result = await listContainers({ query, throwOnError: true });
    return result.data;
  }

  /** Comando individual com chave de idempotência explícita (lote retomável, spec 010 FR-008). */
  async submitContainerAction(
    environmentId: string,
    containerId: string,
    expectedContainerRevision: string,
    action: ContainerAction,
    idempotencyKey: string,
  ): Promise<string> {
    const requestToken = await this.antiforgery();
    const accepted = await submitContainerCommand({
      path: { environmentId, containerId },
      headers: { 'Idempotency-Key': idempotencyKey, 'X-Dokpod-Antiforgery': requestToken },
      body: { action, expectedContainerRevision, deadlineUtc: new Date(Date.now() + 2 * 60_000).toISOString() },
      throwOnError: true,
    });
    return accepted.data.commandId;
  }

  async getContainer(environmentId: string, containerId: string): Promise<ContainerDetail> {
    const result = await getContainer({ path: { environmentId, containerId }, throwOnError: true });
    return result.data;
  }

  async getContainerSummary(environmentId: string, containerId: string): Promise<ContainerConfigSummary> {
    const result = await getContainerSummary({ path: { environmentId, containerId }, throwOnError: true });
    return result.data;
  }

  async inspectContainer(environmentId: string, containerId: string): Promise<ContainerInspection> {
    const result = await inspectContainer({ path: { environmentId, containerId }, throwOnError: true });
    return result.data;
  }

  async getContainerLogs(
    environmentId: string,
    containerId: string,
    query: NonNullable<GetContainerLogsData['query']>,
  ): Promise<ContainerLogPage> {
    const result = await getContainerLogs({ path: { environmentId, containerId }, query, throwOnError: true });
    return result.data;
  }

  async getOverview(stage?: Stage | null): Promise<OverviewSummary> {
    const result = await getOverview({ query: stage ? { stage } : {}, throwOnError: true });
    return result.data;
  }

  async listEvents(query: NonNullable<ListEventsData['query']>): Promise<OperationalEventPage> {
    const result = await listEvents({ query, throwOnError: true });
    return result.data;
  }

  async setEnvironmentStage(environmentId: string, stage: Stage | null): Promise<boolean> {
    const requestToken = await this.antiforgery();
    const result = await setEnvironmentStage({
      path: { environmentId },
      headers: { 'X-Dokpod-Antiforgery': requestToken },
      body: { stage },
      throwOnError: true,
    });
    return result.data.changed;
  }

  async revokeAgentIdentity(environmentId: string): Promise<void> {
    const requestToken = await this.antiforgery();
    await revokeAgentIdentity({
      path: { environmentId },
      headers: { 'X-Dokpod-Antiforgery': requestToken },
      throwOnError: true,
    });
  }

  private async antiforgery(): Promise<string> {
    const { requestToken } = await firstValueFrom(this.http.get<AntiforgeryResponse>('bff/antiforgery'));
    return requestToken;
  }

  async registerEnvironment(request: RegisterEnvironmentRequest): Promise<string> {
    const { requestToken } = await firstValueFrom(
      this.http.get<AntiforgeryResponse>('bff/antiforgery'),
    );
    const result = await registerEnvironment({
      headers: { 'X-Dokpod-Antiforgery': requestToken },
      body: request,
      throwOnError: true,
    });

    return result.data.environmentId;
  }

  async getEnvironment(environmentId: string): Promise<EnvironmentState> {
    const result = await getEnvironment({
      path: { environmentId },
      throwOnError: true,
    });

    return result.data;
  }

  async getCommand(environmentId: string, commandId: string): Promise<ContainerCommandStatus> {
    const result = await getContainerCommand({
      path: { environmentId, commandId },
      throwOnError: true,
    });

    return result.data;
  }

  async submitCommand(
    environmentId: string,
    container: ContainerInventory,
    action: ContainerAction,
  ): Promise<string> {
    const { requestToken } = await firstValueFrom(
      this.http.get<AntiforgeryResponse>('bff/antiforgery'),
    );
    const accepted = await submitContainerCommand({
      path: { environmentId, containerId: container.containerId },
      headers: {
        'Idempotency-Key': crypto.randomUUID(),
        'X-Dokpod-Antiforgery': requestToken,
      },
      body: {
        action,
        expectedContainerRevision: container.revision,
        deadlineUtc: new Date(Date.now() + 2 * 60_000).toISOString(),
      },
      throwOnError: true,
    });

    return accepted.data.commandId;
  }
}