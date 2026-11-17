import React from 'react';
import clsx from 'clsx';

// A CSP do portal (style-src 'self') bloqueia style="". O tema Prism é vazio e
// as cores vêm de custom.css; o único estilo restante (token vazio) vira classe.
export default function CodeBlockLineToken({line, token, style, className, ...props}) {
  return <span {...props} className={clsx(className, token.empty && 'token--empty')} />;
}
