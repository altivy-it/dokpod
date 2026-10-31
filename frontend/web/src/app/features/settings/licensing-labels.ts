import type { LicensingStatus } from '../../data-access/generated/control-plane';
import type { Tone } from '../../design-system';

export const ENTITLEMENT: Record<LicensingStatus['entitlement'], { label: string; tone: Tone }> = {
  ce_only: { label: 'Sem licença', tone: 'neutral' },
  granted: { label: 'Licença válida', tone: 'success' },
  granted_in_grace: { label: 'Em tolerância', tone: 'warning' },
  restricted: { label: 'Restrita', tone: 'error' },
  indeterminate: { label: 'Indeterminada', tone: 'warning' },
};

/** Mensagens de recusa da importação; nunca repetem o conteúdo do arquivo (FR-027). */
export function importFailureMessage(code: string | null, status: number): string {
  switch (code) {
    case 'license_invalid':
      return 'O arquivo foi recusado na verificação. A licença atual continua em vigor.';
    case 'license_obsolete':
      return 'A licença tem revisão anterior à ativa. A licença atual continua em vigor.';
    case 'license_slot_unknown':
      return 'O slot informado não existe nesta licença.';
    case 'license_slot_disabled':
      return 'O slot informado está desabilitado nesta licença.';
    case 'license_revision_conflict':
      return 'O estado da licença mudou desde a leitura. Atualize e tente de novo.';
    case 'payload_too_large':
      return 'O arquivo de licença tem mais de 64 KiB.';
    case 'rate_limited':
      return 'Muitas importações em sequência. Aguarde um minuto e tente de novo.';
    case 'authorization_denied':
      return 'Importar a licença exige a permissão licensing:manage.';
    default:
      return status === 503 ? 'A autorização está indisponível. Tente de novo em instantes.' : 'Não foi possível importar a licença.';
  }
}
