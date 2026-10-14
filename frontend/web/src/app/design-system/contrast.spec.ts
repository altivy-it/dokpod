// O runner do Vitest roda em Node; o projeto não declara @types/node, então tipamos só o necessário.
// @ts-expect-error módulo Node disponível no runner de testes
import { readFileSync } from 'node:fs';

declare const process: { cwd(): string };

/**
 * Verificação automatizada de contraste (spec 007 FR-009/T026): lê `src/styles/tokens.css` e confere
 * os pares de token usados pelos componentes. Superfícies de vidro translúcidas são compostas sobre a
 * base escura (pior caso: a camada mais clara do gradiente).
 */
type Rgba = [number, number, number, number];

const css: string = readFileSync(`${process.cwd()}/src/styles/tokens.css`, 'utf-8');
const tokens = new Map<string, string>(Array.from(css.matchAll(/--([\w-]+):\s*([^;]+);/g), (m: RegExpMatchArray) => [m[1], m[2].trim()]));

function color(name: string): Rgba {
  const value = tokens.get(name);
  if (!value) throw new Error(`token --${name} ausente`);
  const hex = /^#([0-9a-f]{6})$/i.exec(value);
  if (hex) return [0, 2, 4].map(i => parseInt(hex[1].slice(i, i + 2), 16)).concat(1) as Rgba;
  const rgba = /^rgba?\(([^)]+)\)$/.exec(value);
  if (rgba) {
    const [r, g, b, a = '1'] = rgba[1].split(',').map(p => p.trim());
    return [Number(r), Number(g), Number(b), Number(a)];
  }
  throw new Error(`cor não suportada em --${name}: ${value}`);
}

function over(top: Rgba, bottom: Rgba): Rgba {
  const a = top[3];
  return [0, 1, 2].map(i => top[i] * a + bottom[i] * (1 - a)).concat(1) as Rgba;
}

function surface(...layers: string[]): Rgba {
  return layers.reduceRight<Rgba>((acc, name) => over(color(name), acc), color('surface-base'));
}

function luminance([r, g, b]: Rgba): number {
  const lin = (c: number) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
}

function ratio(fg: Rgba, bg: Rgba): number {
  const [a, b] = [luminance(fg), luminance(bg)].sort((x, y) => y - x);
  return (a + 0.05) / (b + 0.05);
}

const TEXT = 4.5;
const UI = 3;

const BACKGROUNDS: Record<string, Rgba> = {
  base: surface(),
  panel: surface('surface-panel'),
  glass: surface('glass-fill-top'),
  overlay: surface('overlay-fill-top', 'scrim'),
  row: surface('row-tint', 'surface-panel'),
  input: surface('input-fill', 'surface-panel'),
};

// [texto, fundo, mínimo] — pares usados em `app-text.css`, `altivy-kit.css` e componentes `dok-*`.
const PAIRS: [string, keyof typeof BACKGROUNDS, number][] = [
  ['foreground', 'base', TEXT],
  ['foreground', 'panel', TEXT],
  ['foreground', 'glass', TEXT],
  ['foreground', 'overlay', TEXT],
  ['foreground', 'row', TEXT],
  ['foreground', 'input', TEXT],
  ['text-strong', 'glass', TEXT],
  ['muted', 'base', TEXT],
  ['muted', 'panel', TEXT],
  ['muted', 'glass', TEXT],
  ['muted', 'overlay', TEXT],
  ['teal-light', 'base', TEXT],
  ['teal-light', 'panel', TEXT],
  ['teal-light', 'glass', TEXT],
  ['signal-success', 'panel', TEXT],
  ['signal-warning', 'panel', TEXT],
  ['signal-error', 'panel', TEXT],
  ['signal-info', 'panel', TEXT],
  ['signal-error', 'glass', TEXT],
  ['slate', 'panel', UI],
  ['chart-1', 'panel', UI],
  ['chart-2', 'panel', UI],
  ['chart-3', 'panel', UI],
  ['chart-5', 'panel', UI],
];

describe('contraste dos tokens (WCAG 2.2 AA)', () => {
  it.each(PAIRS)('%s sobre %s atinge %s:1', (fg, bg, min) => {
    expect(ratio(color(fg), BACKGROUNDS[bg])).toBeGreaterThanOrEqual(min);
  });

  it('texto escuro do botão primário sobre o gradiente teal atinge 4,5:1', () => {
    expect(ratio(color('ink'), color('cta-bottom'))).toBeGreaterThanOrEqual(TEXT);
    expect(ratio(color('ink'), color('cta-top'))).toBeGreaterThanOrEqual(TEXT);
  });

  it('foco teal-light é distinguível da base (3:1)', () => {
    expect(ratio(color('teal-light'), BACKGROUNDS['base'])).toBeGreaterThanOrEqual(UI);
  });
});
