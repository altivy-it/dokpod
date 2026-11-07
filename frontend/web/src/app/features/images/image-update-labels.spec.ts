import { describe, expect, it } from 'vitest';
import type { ImageUpdateCheck } from '../../data-access/generated/control-plane';
import { canPullNewVersion, hasImageUpdateCheck, IMAGE_UPDATE_REASON_LABEL, IMAGE_UPDATE_STATE_TONE, imageUpdateDescription, imageUpdateStateLabel } from './image-update-labels';

const check = (patch: Partial<ImageUpdateCheck>): ImageUpdateCheck => ({
  state: 'update_available',
  reason: null,
  checkedAt: '2026-11-01T12:00:00Z',
  lastSuccessAt: '2026-11-01T12:00:00Z',
  outdatedSince: null,
  stale: false,
  ...patch,
});

describe('image update labels (spec 025 T017)', () => {
  it('describes every state in text, with a tone for each label', () => {
    for (const state of ['update_available', 'up_to_date', 'not_checkable', 'failed', 'container_behind'] as const) {
      const label = imageUpdateStateLabel(check({ state }));
      expect(label.length).toBeGreaterThan(0);
      expect(IMAGE_UPDATE_STATE_TONE[label]).toBeDefined();
    }
    expect(imageUpdateStateLabel(check({ state: 'container_behind' }))).toBe('Versão nova no host; o container usa a anterior');
  });

  it('adds the reason, the date and the stale mark', () => {
    const text = imageUpdateDescription(check({ state: 'not_checkable', reason: 'digest_reference', stale: true }));

    expect(text).toContain('Não verificável');
    expect(text).toContain('Referência fixada por digest');
    expect(text).toContain('verificada em');
    expect(text).toContain('host desconectado');
  });

  it('has a pt-BR text for every reason of the contract', () => {
    expect(Object.keys(IMAGE_UPDATE_REASON_LABEL)).toHaveLength(11);
    expect(IMAGE_UPDATE_REASON_LABEL.unauthorized).toBe('Credencial recusada pelo registro');
  });

  it('offers "Baixar nova versão" only with image:pull and only in update_available (T020)', () => {
    expect(canPullNewVersion(check({ state: 'update_available' }), true)).toBe(true);
    expect(canPullNewVersion(check({ state: 'update_available' }), false)).toBe(false);
    for (const state of ['up_to_date', 'not_checkable', 'failed', 'container_behind'] as const) {
      expect(canPullNewVersion(check({ state }), true)).toBe(false);
    }
    expect(canPullNewVersion(null, true)).toBe(false);
    expect(canPullNewVersion(undefined, true)).toBe(false);
  });

  it('shows nothing when the API does not send the field (CE)', () => {
    expect(hasImageUpdateCheck({})).toBe(false);
    expect(hasImageUpdateCheck({ updateCheck: null })).toBe(true);
    expect(imageUpdateDescription(null)).toBe('Ainda não verificada');
    expect(imageUpdateStateLabel(null)).toBe('Ainda não verificada');
    expect(IMAGE_UPDATE_STATE_TONE['Ainda não verificada']).toBe('neutral');
  });
});
