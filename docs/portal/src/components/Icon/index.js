import React from 'react';
import clsx from 'clsx';

// Ícones Lucide usados no cabeçalho da landing Altivy: traço 2, pontas
// arredondadas, sem preenchimento. Sempre decorativos (aria-hidden).
const PATHS = {
  check: <path d="M20 6 9 17l-5-5" />,
  chevron: <path d="m6 9 6 6 6-6" />,
  globe: (
    <>
      <circle cx="12" cy="12" r="10" />
      <path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20" />
      <path d="M2 12h20" />
    </>
  ),
  dark: <path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z" />,
  light: (
    <>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41" />
    </>
  ),
  system: (
    <>
      <rect width="20" height="14" x="2" y="3" rx="2" />
      <path d="M8 21h8M12 17v4" />
    </>
  ),
};

export default function Icon({name, size = 16, className}) {
  return (
    <svg className={clsx('altivy-icon', className)} viewBox="0 0 24 24" width={size} height={size}
      fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
      aria-hidden="true" focusable="false">
      {PATHS[name]}
    </svg>
  );
}

// Rótulo de item de menu no padrão da landing: check reservando espaço em todos
// os itens e visível só no atual (CSS em custom.css).
export function MenuItemLabel({children}) {
  return (
    <>
      <Icon name="check" className="dropdown__check" />
      <span>{children}</span>
    </>
  );
}
