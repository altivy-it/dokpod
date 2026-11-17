import React, {useCallback, useEffect, useId, useRef, useState} from 'react';
import clsx from 'clsx';
import {translate} from '@docusaurus/Translate';
import styles from './styles.module.css';

const CHOICE_KEY = 'altivy.docs.theme';

// Ícones no estilo Lucide: traço 2, pontas arredondadas, sem preenchimento.
function Icon({name}) {
  const paths = {
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
  return (
    <svg className={styles.icon} viewBox="0 0 24 24" width="18" height="18" fill="none"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
      aria-hidden="true" focusable="false">
      {paths[name]}
    </svg>
  );
}

function useOptions() {
  return [
    {choice: 'light', value: 'light', label: translate({id: 'portal.theme.light', message: 'Claro'})},
    {choice: 'dark', value: 'dark', label: translate({id: 'portal.theme.dark', message: 'Escuro'})},
    {choice: 'system', value: null, label: translate({id: 'portal.theme.system', message: 'Sistema'})},
  ];
}

function rememberChoice(choice) {
  try {
    window.localStorage.setItem(CHOICE_KEY, choice);
  } catch (error) {
    // Sem armazenamento a troca vale só para esta página.
  }
}

// Substitui o botão de ciclo do Docusaurus por um menu Claro/Escuro/Sistema.
// value é 'light', 'dark' ou null (Sistema), como em useColorMode().
export default function ColorModeToggle({className, buttonClassName, value, onChange}) {
  const options = useOptions();
  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  const buttonRef = useRef(null);
  const itemRefs = useRef([]);
  const menuId = useId();
  const current = options.find((option) => option.value === (value ?? null)) ?? options[2];

  const close = useCallback((restoreFocus) => {
    setOpen(false);
    if (restoreFocus) {
      buttonRef.current?.focus();
    }
  }, []);

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    const activeIndex = options.indexOf(current);
    itemRefs.current[activeIndex]?.focus();
    const onPointer = (event) => {
      if (!rootRef.current?.contains(event.target)) {
        close(false);
      }
    };
    document.addEventListener('pointerdown', onPointer);
    return () => document.removeEventListener('pointerdown', onPointer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const select = (option) => {
    rememberChoice(option.choice);
    onChange(option.value);
    close(true);
  };

  const onMenuKeyDown = (event) => {
    const items = itemRefs.current;
    const index = items.indexOf(document.activeElement);
    if (event.key === 'Escape') {
      event.preventDefault();
      close(true);
    } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const step = event.key === 'ArrowDown' ? 1 : -1;
      items[(index + step + items.length) % items.length]?.focus();
    } else if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault();
      items[event.key === 'Home' ? 0 : items.length - 1]?.focus();
    } else if (event.key === 'Tab') {
      close(false);
    }
  };

  const buttonLabel = translate(
    {id: 'portal.theme.button', message: 'Tema: {theme}'},
    {theme: current.label},
  );

  return (
    <div ref={rootRef} className={clsx(styles.root, className)}>
      <button
        ref={buttonRef}
        type="button"
        className={clsx('clean-btn', styles.button, buttonClassName)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={menuId}
        aria-label={buttonLabel}
        title={buttonLabel}
        onClick={() => setOpen((state) => !state)}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown') {
            event.preventDefault();
            setOpen(true);
          }
        }}>
        <Icon name={current.choice} />
      </button>
      {open && (
        <ul
          id={menuId}
          role="menu"
          aria-label={translate({id: 'portal.theme.menu', message: 'Tema'})}
          className={styles.menu}
          onKeyDown={onMenuKeyDown}>
          {options.map((option, index) => (
            <li key={option.choice} role="none">
              <button
                ref={(element) => {
                  itemRefs.current[index] = element;
                }}
                type="button"
                role="menuitemradio"
                aria-checked={option === current}
                data-theme-choice={option.choice}
                className={clsx('clean-btn', styles.item, option === current && styles.itemActive)}
                onClick={() => select(option)}>
                <Icon name={option.choice} />
                <span>{option.label}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
