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
  getContainerMetrics,
  getStageMetrics,
  listHostMetrics,
  listContainers,
  listEnvironmentContainers,
  listEnvironments,
  listEvents,
  listImages,
  createContainer,
  getContainerCreateOptions,
  getTerminalPolicy,
  renewTerminalSession,
  lookupImage,
  pullImage,
  registerEnvironment,
  revokeAgentIdentity,
  setEnvironmentStage,
  submitContainerCommand,
  getAuditSummary,
  type AuditSummary,
  acknowledgeAlert,
  createAlertRule,
  createAlertSilence,
  deleteAlertRule,
  endAlertSilence,
  getAlert,
  getAlertSummary,
  listActionJustificationPolicies,
  listAlertRules,
  listAlerts,
  listAlertSilences,
  listContainerJustifications,
  resolveAlert,
  setActionJustificationPolicy,
  silenceAlert,
  updateAlertRule,
  type AlertDetail,
  type AlertPage,
  type AlertRulePage,
  type AlertRuleRequest,
  type AlertSilencePage,
  type AlertSummary,
  type ContainerJustifications,
  type CreateAlertSilenceRequest,
  type JustificationPolicyPage,
  type ListAlertsData,
  type ContainerConfigSummary,
  type ContainerMetrics,
  type HostMetricsPage,
  type MetricsStep,
  type MetricsWindow,
  type StageMetrics,
  type ContainerDetail,
  type ContainerInspection,
  type ContainerListPage,
  type ContainerLogPage,
  type GetContainerLogsData,
  type ListContainersData,
  type ListEnvironmentsData,
  type ListEventsData,
  type ListImagesData,
  type ImageListPage,
  type ImageLookup,
  type ContainerCreationRequest,
  type ContainerCreateOptions,
  type TerminalPolicy,
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
import { newUuid } from '../core/uuid';

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

  /** Métricas (spec 009): agregado do estágio, último valor por host e série de container (até 30 min). */
  async getStageMetrics(stage: Stage | null, window: MetricsWindow = '12h', step: MetricsStep = '1h'): Promise<StageMetrics> {
    const result = await getStageMetrics({ query: { ...(stage ? { stage } : {}), window, step }, throwOnError: true });
    return result.data;
  }

  async listHostMetrics(stage: Stage | null): Promise<HostMetricsPage> {
    const result = await listHostMetrics({ query: stage ? { stage } : {}, throwOnError: true });
    return result.data;
  }

  async getContainerMetrics(environmentId: string, containerId: string, window: MetricsWindow = '30m', step: MetricsStep = '1m'): Promise<ContainerMetrics> {
    const result = await getContainerMetrics({ path: { environmentId, containerId }, query: { window, step }, throwOnError: true });
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
    justification: string | null = null,
  ): Promise<string> {
    const requestToken = await this.antiforgery();
    const accepted = await submitContainerCommand({
      path: { environmentId, containerId },
      headers: { 'Idempotency-Key': idempotencyKey, 'X-Dokpod-Antiforgery': requestToken },
      body: {
        action,
        expectedContainerRevision,
        deadlineUtc: new Date(Date.now() + 2 * 60_000).toISOString(),
        ...(justification ? { justification } : {}),
      },
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

  /** Inventário de imagens dos hosts autorizados (spec 012). */
  async listImages(query: NonNullable<ListImagesData['query']>): Promise<ImageListPage> {
    const result = await listImages({ query, throwOnError: true });
    return result.data;
  }

  /** Imagem presente no host pela referência normalizada, sem consultar o registro (spec 012 T012). */
  async lookupImage(environmentId: string, reference: string): Promise<ImageLookup> {
    const result = await lookupImage({ path: { environmentId }, query: { reference }, throwOnError: true });
    return result.data;
  }

  /** Pull por host; uma chave de idempotência por host torna o retry seguro (spec 012 D-002). */
  async pullImage(environmentId: string, reference: string, idempotencyKey: string): Promise<string> {
    const requestToken = await this.antiforgery();
    const accepted = await pullImage({
      path: { environmentId },
      headers: { 'Idempotency-Key': idempotencyKey, 'X-Dokpod-Antiforgery': requestToken },
      body: { reference, deadlineUtc: new Date(Date.now() + 10 * 60_000).toISOString() },
      throwOnError: true,
    });
    return accepted.data.commandId;
  }

  /** Dados do formulário de criação (spec 013): segredos, redes e volumes do host, somente nomes. */
  async getContainerCreateOptions(environmentId: string): Promise<ContainerCreateOptions> {
    const result = await getContainerCreateOptions({ path: { environmentId }, throwOnError: true });
    return result.data;
  }

  /** Criação idempotente: a chave é gerada por envio lógico e reutilizada em retry (spec 013 SC-004). */
  async createContainer(environmentId: string, request: ContainerCreationRequest, idempotencyKey: string): Promise<{ commandId: string; equivalentCommand?: string | null }> {
    const requestToken = await this.antiforgery();
    const accepted = await createContainer({
      path: { environmentId },
      headers: { 'Idempotency-Key': idempotencyKey, 'X-Dokpod-Antiforgery': requestToken },
      body: request,
      throwOnError: true,
    });
    return accepted.data;
  }

  /** Política do terminal (spec 014): permitido, motivo, justificativa e limites. */
  async getTerminalPolicy(environmentId: string, containerId: string): Promise<TerminalPolicy> {
    const result = await getTerminalPolicy({ path: { environmentId, containerId }, throwOnError: true });
    return result.data;
  }

  /** Reavalia a autorização da sessão com o token atual (o BFF renova o token; spec 014 D-008). */
  async renewTerminalSession(sessionId: string): Promise<void> {
    const requestToken = await this.antiforgery();
    await renewTerminalSession({ path: { sessionId }, headers: { 'X-Dokpod-Antiforgery': requestToken }, throwOnError: true });
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

  /** Governança de alertas (spec 016). */
  async getAlertSummary(stage: Stage | null): Promise<AlertSummary> {
    const result = await getAlertSummary({ query: stage ? { stage } : {}, throwOnError: true });
    return result.data;
  }

  async listAlerts(query: NonNullable<ListAlertsData['query']>): Promise<AlertPage> {
    const result = await listAlerts({ query, throwOnError: true });
    return result.data;
  }

  async getAlert(alertId: string): Promise<AlertDetail> {
    const result = await getAlert({ path: { alertId }, throwOnError: true });
    return result.data;
  }

  async acknowledgeAlert(alertId: string, comment: string): Promise<void> {
    const headers = { 'X-Dokpod-Antiforgery': await this.antiforgery() };
    await acknowledgeAlert({ path: { alertId }, headers, body: { comment }, throwOnError: true });
  }

  async resolveAlert(alertId: string, comment: string): Promise<void> {
    const headers = { 'X-Dokpod-Antiforgery': await this.antiforgery() };
    await resolveAlert({ path: { alertId }, headers, body: { comment }, throwOnError: true });
  }

  async silenceAlert(alertId: string, until: string, justification: string): Promise<void> {
    const headers = { 'X-Dokpod-Antiforgery': await this.antiforgery() };
    await silenceAlert({ path: { alertId }, headers, body: { until, justification }, throwOnError: true });
  }

  async listAlertRules(): Promise<AlertRulePage> {
    const result = await listAlertRules({ throwOnError: true });
    return result.data;
  }

  async saveAlertRule(ruleId: string | null, request: AlertRuleRequest): Promise<string> {
    const headers = { 'X-Dokpod-Antiforgery': await this.antiforgery() };
    const result = ruleId
      ? await updateAlertRule({ path: { ruleId }, headers, body: request, throwOnError: true })
      : await createAlertRule({ headers, body: request, throwOnError: true });
    return result.data.id;
  }

  async deleteAlertRule(ruleId: string): Promise<void> {
    const headers = { 'X-Dokpod-Antiforgery': await this.antiforgery() };
    await deleteAlertRule({ path: { ruleId }, headers, throwOnError: true });
  }

  async listAlertSilences(active: boolean): Promise<AlertSilencePage> {
    const result = await listAlertSilences({ query: { active }, throwOnError: true });
    return result.data;
  }

  async createAlertSilence(request: CreateAlertSilenceRequest): Promise<string> {
    const headers = { 'X-Dokpod-Antiforgery': await this.antiforgery() };
    const result = await createAlertSilence({ headers, body: request, throwOnError: true });
    return result.data.id;
  }

  async endAlertSilence(silenceId: string): Promise<void> {
    const headers = { 'X-Dokpod-Antiforgery': await this.antiforgery() };
    await endAlertSilence({ path: { silenceId }, headers, throwOnError: true });
  }

  async listJustificationPolicies(): Promise<JustificationPolicyPage> {
    const result = await listActionJustificationPolicies({ throwOnError: true });
    return result.data;
  }

  async setJustificationPolicy(stage: Stage, required: boolean): Promise<void> {
    const headers = { 'X-Dokpod-Antiforgery': await this.antiforgery() };
    await setActionJustificationPolicy({ path: { stage }, headers, body: { required }, throwOnError: true });
  }

  async listContainerJustifications(environmentId: string, containerId: string): Promise<ContainerJustifications> {
    const result = await listContainerJustifications({ path: { environmentId, containerId }, throwOnError: true });
    return result.data;
  }

  /** Auditoria (spec 017): só o resumo de acesso, usado pelo menu; a consulta fica em features/audit (lazy). */
  async getAuditSummary(): Promise<AuditSummary> {
    const result = await getAuditSummary({ throwOnError: true });
    return result.data;
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
        'Idempotency-Key': newUuid(),
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