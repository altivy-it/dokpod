import React, {useEffect, useId, useRef, useState} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import {translate} from '@docusaurus/Translate';
import {useActiveDocContext, useDocsVersionCandidates} from '@docusaurus/plugin-content-docs/client';
import styles from './styles.module.css';

// Busca local com Pagefind (índice gerado em /pagefind no build, sem serviço
// externo). O índice já é separado por <html lang>; o filtro "version" limita
// os resultados à versão aberta.
let pagefindPromise;
function loadPagefind() {
  if (!pagefindPromise) {
    const load = window.dokpodLoadPagefind;
    pagefindPromise = (load ? load() : Promise.reject(new Error('search-loader.js ausente'))).then(async (pagefind) => {
      await pagefind.options({excerptLength: 18});
      return pagefind;
    });
  }
  return pagefindPromise;
}

function useActiveVersionName() {
  const {activeVersion} = useActiveDocContext(undefined);
  const candidates = useDocsVersionCandidates(undefined);
  return (activeVersion ?? candidates[0])?.name;
}

export default function SearchBar({mobile, className}) {
  const version = useActiveVersionName();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState([]);
  const [state, setState] = useState('idle');
  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  const inputId = useId();
  const listId = useId();

  useEffect(() => {
    const term = query.trim();
    if (term.length < 2) {
      setResults([]);
      setState('idle');
      return undefined;
    }
    let cancelled = false;
    setState('loading');
    loadPagefind()
      .then((pagefind) => pagefind.debouncedSearch(term, {filters: {version}}, 250))
      .then(async (search) => {
        if (cancelled || search === null) {
          return;
        }
        const data = await Promise.all(search.results.slice(0, 8).map((result) => result.data()));
        if (!cancelled) {
          setResults(data);
          setState('done');
        }
      })
      .catch(() => {
        if (!cancelled) {
          setResults([]);
          setState('error');
        }
      });
    return () => {
      cancelled = true;
    };
  }, [query, version]);

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    const onPointer = (event) => {
      if (!rootRef.current?.contains(event.target)) {
        setOpen(false);
      }
    };
    document.addEventListener('pointerdown', onPointer);
    return () => document.removeEventListener('pointerdown', onPointer);
  }, [open]);

  const status = {
    idle: '',
    loading: translate({id: 'portal.search.loading', message: 'Buscando…'}),
    error: translate({id: 'portal.search.error', message: 'A busca não está disponível nesta página.'}),
    done: results.length
      ? translate({id: 'portal.search.count', message: '{count} resultado(s) nesta versão'}, {count: results.length})
      : translate({id: 'portal.search.empty', message: 'Nenhum resultado nesta versão.'}),
  }[state];
  const showPanel = open && query.trim().length >= 2;

  return (
    <div ref={rootRef} className={clsx('navbar__search', styles.root, mobile && styles.mobile, className)}
      onKeyDown={(event) => {
        if (event.key === 'Escape') {
          setOpen(false);
        }
      }}>
      <form role="search" onSubmit={(event) => event.preventDefault()}>
        <label htmlFor={inputId} className={styles.visuallyHidden}>
          {translate({id: 'portal.search.label', message: 'Buscar no manual'})}
        </label>
        <input
          id={inputId}
          type="search"
          className={styles.input}
          placeholder={translate({id: 'portal.search.placeholder', message: 'Buscar'})}
          autoComplete="off"
          value={query}
          onFocus={() => setOpen(true)}
          onChange={(event) => {
            setQuery(event.target.value);
            setOpen(true);
          }}
        />
      </form>
      <p className={styles.visuallyHidden} role="status" aria-live="polite">{status}</p>
      {showPanel && (
        <div className={styles.panel} id={listId}>
          <p className={styles.status}>{status}</p>
          {results.length > 0 && (
            <ul className={styles.list}>
              {results.map((result) => (
                <li key={result.url}>
                  <Link className={styles.result} to={result.url} autoAddBaseUrl={false} onClick={() => setOpen(false)}>
                    <span className={styles.title}>{result.meta?.title ?? result.url}</span>
                    {/* O Pagefind entrega o trecho já escapado, só com <mark>. */}
                    <span className={styles.excerpt} dangerouslySetInnerHTML={{__html: result.excerpt}} />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
