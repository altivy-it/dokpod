import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  acknowledgeAlert,
  createAlertRule,
  createAlertSilence,
  deleteAlertRule,
  endAlertSilence,
  getAlert,
  listAlertRules,
  listAlerts,
  listAlertSilences,
  resolveAlert,
  silenceAlert,
  updateAlertRule,
  type AlertDetail,
  type AlertPage,
  type AlertRulePage,
  type AlertRuleRequest,
  type AlertSilencePage,
  type CreateAlertSilenceRequest,
  type ListAlertsData,
} from '../../data-access/generated/control-plane';

/** Gestão de alertas, regras e silenciamentos (spec 016), fora do bundle inicial: só a página Alertas usa. */
@Injectable({ providedIn: 'root' })
export class AlertsApiService {
  private readonly http = inject(HttpClient);

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

  private async antiforgery(): Promise<string> {
    const { requestToken } = await firstValueFrom(this.http.get<{ requestToken: string }>('bff/antiforgery'));
    return requestToken;
  }
}
