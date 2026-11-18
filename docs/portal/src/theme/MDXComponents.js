// Tabelas largas rolam na horizontal; a região rolável precisa de foco e nome (WCAG 2.1.1).
import React from 'react';
import MDXComponents from '@theme-original/MDXComponents';
import {translate} from '@docusaurus/Translate';

function Table(props) {
  return (
    <div
      className="portal-table"
      role="region"
      tabIndex={0}
      aria-label={translate({id: 'portal.table.label', message: 'Tabela; role na horizontal para ver todas as colunas'})}>
      <table {...props} />
    </div>
  );
}

export default {...MDXComponents, table: Table};
