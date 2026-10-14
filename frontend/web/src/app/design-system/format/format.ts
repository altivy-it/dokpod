import { Pipe, PipeTransform } from '@angular/core';

const LOCALE = 'pt-BR';

export function formatNumber(value: number | null | undefined, decimals = 0): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return value.toLocaleString(LOCALE, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
}

export function formatPercent(value: number | null | undefined, decimals = 1): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return `${value.toLocaleString(LOCALE, { minimumFractionDigits: 0, maximumFractionDigits: decimals })}%`;
}

/** Bytes em unidades binárias com sufixos usados no mockup (MiB, GiB). */
export function formatBytes(value: number | null | undefined, decimals = 0): string {
  if (value === null || value === undefined || Number.isNaN(value) || value < 0) return '—';
  const units = ['B', 'KiB', 'MiB', 'GiB', 'TiB'];
  let amount = value;
  let unit = 0;
  while (amount >= 1024 && unit < units.length - 1) {
    amount /= 1024;
    unit++;
  }
  const digits = unit === 0 ? 0 : decimals;
  return `${amount.toLocaleString(LOCALE, { minimumFractionDigits: 0, maximumFractionDigits: digits })} ${units[unit]}`;
}

/** Megabytes decimais (ex.: tamanho de imagem "184 MB"). */
export function formatMegabytes(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return `${Math.round(value / 1_000_000).toLocaleString(LOCALE)} MB`;
}

function pad(value: number): string {
  return value < 10 ? `0${value}` : String(value);
}

function toDate(value: string | number | Date | null | undefined): Date | null {
  if (value === null || value === undefined || value === '') return null;
  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

/** `dd/MM/yyyy HH:mm` no fuso do navegador. */
export function formatDateTime(value: string | number | Date | null | undefined): string {
  const date = toDate(value);
  if (!date) return '—';
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** `HH:mm` no fuso do navegador. */
export function formatTime(value: string | number | Date | null | undefined, withSeconds = false): string {
  const date = toDate(value);
  if (!date) return '—';
  const base = `${pad(date.getHours())}:${pad(date.getMinutes())}`;
  return withSeconds ? `${base}:${pad(date.getSeconds())}` : base;
}

/** Duração relativa curta: "há 4 min", "há 2 h", "há 3 dias". */
export function formatRelative(value: string | number | Date | null | undefined, now: Date = new Date()): string {
  const date = toDate(value);
  if (!date) return '—';
  const seconds = Math.max(0, Math.round((now.getTime() - date.getTime()) / 1000));
  if (seconds < 60) return 'agora';
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `há ${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `há ${hours} h`;
  const days = Math.floor(hours / 24);
  return days === 1 ? 'há 1 dia' : `há ${days} dias`;
}

/** Duração sem prefixo: "4 min", "2 h", "3 dias". */
export function formatDuration(value: string | number | Date | null | undefined, now: Date = new Date()): string {
  const relative = formatRelative(value, now);
  return relative.startsWith('há ') ? relative.slice(3) : relative;
}

@Pipe({ name: 'dokNumber' })
export class DokNumberPipe implements PipeTransform {
  transform(value: number | null | undefined, decimals = 0): string {
    return formatNumber(value, decimals);
  }
}

@Pipe({ name: 'dokPercent' })
export class DokPercentPipe implements PipeTransform {
  transform(value: number | null | undefined, decimals = 1): string {
    return formatPercent(value, decimals);
  }
}

@Pipe({ name: 'dokBytes' })
export class DokBytesPipe implements PipeTransform {
  transform(value: number | null | undefined, decimals = 0): string {
    return formatBytes(value, decimals);
  }
}

@Pipe({ name: 'dokDateTime' })
export class DokDateTimePipe implements PipeTransform {
  transform(value: string | number | Date | null | undefined): string {
    return formatDateTime(value);
  }
}

@Pipe({ name: 'dokTime' })
export class DokTimePipe implements PipeTransform {
  transform(value: string | number | Date | null | undefined, withSeconds = false): string {
    return formatTime(value, withSeconds);
  }
}

@Pipe({ name: 'dokRelative' })
export class DokRelativePipe implements PipeTransform {
  transform(value: string | number | Date | null | undefined): string {
    return formatRelative(value);
  }
}
