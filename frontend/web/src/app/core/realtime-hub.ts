/** Endereço e estados do hub em tempo real, sem depender do pacote SignalR (que é carregado sob demanda). */
export type ControlPlaneRealtimeStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

export function controlPlaneHubUrl(baseUri: string): string {
  return `${baseUri.replace(/\/$/, '')}/hubs/control-plane`;
}
