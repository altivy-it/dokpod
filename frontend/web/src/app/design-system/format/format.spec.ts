import {
  formatBytes,
  formatDateTime,
  formatDuration,
  formatNumber,
  formatPercent,
  formatRelative,
} from './format';

describe('format pt-BR', () => {
  it('formats numbers with pt-BR separators', () => {
    expect(formatNumber(1234.56, 2)).toBe('1.234,56');
    expect(formatNumber(154)).toBe('154');
    expect(formatNumber(null)).toBe('—');
  });

  it('formats percentages without trailing zeros', () => {
    expect(formatPercent(46.8)).toBe('46,8%');
    expect(formatPercent(80)).toBe('80%');
  });

  it('formats bytes in binary units', () => {
    expect(formatBytes(412 * 1024 * 1024)).toBe('412 MiB');
    expect(formatBytes(512)).toBe('512 B');
    expect(formatBytes(-1)).toBe('—');
  });

  it('formats date and time as dd/MM/yyyy HH:mm', () => {
    expect(formatDateTime(new Date(2026, 9, 10, 15, 12))).toBe('10/10/2026 15:12');
    expect(formatDateTime('invalid')).toBe('—');
  });

  it('formats relative durations', () => {
    const now = new Date(2026, 9, 10, 15, 12);
    expect(formatRelative(new Date(2026, 9, 10, 15, 8), now)).toBe('há 4 min');
    expect(formatRelative(new Date(2026, 9, 10, 13, 12), now)).toBe('há 2 h');
    expect(formatRelative(new Date(2026, 9, 9, 15, 12), now)).toBe('há 1 dia');
    expect(formatDuration(new Date(2026, 9, 10, 15, 8), now)).toBe('4 min');
  });
});
