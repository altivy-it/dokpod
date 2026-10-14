import { describe, expect, it } from 'vitest';
import { parseImageReference, pullFailureLabel } from './image-reference';

describe('parseImageReference', () => {
  it('normaliza como a API', () => {
    expect(parseImageReference('nginx')?.canonical).toBe('docker.io/library/nginx:latest');
    expect(parseImageReference('registry.empresa.com:5000/pedidos/api:3.14.2')?.canonical).toBe('registry.empresa.com:5000/pedidos/api:3.14.2');
    expect(parseImageReference('nginx:1.27')?.mutableTag).toBe(true);
    expect(parseImageReference('nginx@sha256:' + 'a'.repeat(64))?.mutableTag).toBe(false);
  });

  it.each(['', 'user:senha@registry.empresa.com/app', 'nginx latest', 'Nginx', 'app?x=1', 'nginx:', 'a/'])('rejeita %s', value => {
    expect(parseImageReference(value)).toBeNull();
  });

  it('traduz códigos de falha', () => {
    expect(pullFailureLabel('image_not_found')).toContain('não encontrada');
    expect(pullFailureLabel('xyz')).toBe('Falha: xyz');
  });
});
