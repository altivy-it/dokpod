import fixtures from '../../testing/equivalent-command.fixtures.json';
import type { ContainerCreationRequest } from './generated/control-plane';
import { renderEquivalentCommand, shellQuote } from './equivalent-command';

describe('renderEquivalentCommand', () => {
  it.each(fixtures.map(f => [f.name, f] as const))('paridade com o servidor: %s', (_name, fixture) => {
    const command = renderEquivalentCommand(fixture.request as unknown as ContainerCreationRequest, fixture.engine as 'docker' | 'podman');
    expect(command).toBe(fixture.expected);
  });

  it('nunca inclui valores literais', () => {
    const command = renderEquivalentCommand(
      { image: 'nginx', name: 'x', environment: [{ name: 'SENHA', value: 'canario-ui', sensitive: true }], autoRemove: false },
      'docker',
    );
    expect(command).not.toContain('canario-ui');
    expect(command).toContain('SENHA=***');
  });

  it('aplica aspas POSIX', () => {
    expect(shellQuote("it's")).toBe("'it'\\''s'");
    expect(shellQuote('a b')).toBe("'a b'");
    expect(shellQuote('ok')).toBe('ok');
  });
});
