/* @ds-bundle: {"format":4,"namespace":"Altivy","components":[{"name":"Page"},{"name":"Glass"},{"name":"Logo"},{"name":"Button"},{"name":"Eyebrow"},{"name":"SectionHeading"},{"name":"Card"},{"name":"NumberedCard"},{"name":"FAQ"},{"name":"Header"},{"name":"MethodTrack"},{"name":"CTABanner"},{"name":"Footer"},{"name":"Accordion"},{"name":"AdmFilas"},{"name":"AdmPipeline"},{"name":"AppHeader"},{"name":"AppShell"},{"name":"BiGraficos"},{"name":"BiSelecaoMeses"},{"name":"BiSemaforo"},{"name":"BiTotalizadores"},{"name":"BridgeVendas"},{"name":"CampoBusca"},{"name":"CardBase"},{"name":"CcoAcoesLinha"},{"name":"CcoCelulas"},{"name":"CcoEntrega"},{"name":"CcoEstadia"},{"name":"CcoIndicadores"},{"name":"CcoJornada"},{"name":"CcoMacros"},{"name":"CcoTelemetria"},{"name":"CcoTorreTabela"},{"name":"ColumnPicker"},{"name":"CommandPalette"},{"name":"CorporateDrawer"},{"name":"CredentialWarning"},{"name":"CteLeitor"},{"name":"CustomBackdrop"},{"name":"CustomMap"},{"name":"CustomSlider"},{"name":"DashWidgets"},{"name":"DropFiles"},{"name":"EmptyStates"},{"name":"EnhancedDataGrid"},{"name":"EnhancedDataTable"},{"name":"ExportarXlsx"},{"name":"FilterAutocomplete"},{"name":"FormBody"},{"name":"FormCRUD"},{"name":"GradeSLA"},{"name":"GraficoBase"},{"name":"Graphics"},{"name":"HdCaixaEntrada"},{"name":"HdChamado"},{"name":"HdDashboard"},{"name":"HdFiltrosLote"},{"name":"HdHistorico"},{"name":"HdSlaGut"},{"name":"InfoKPI"},{"name":"JsonFormsRenderers"},{"name":"LayoutEditavel"},{"name":"ManBoxes"},{"name":"ManCabecalhoPagina"},{"name":"ManEquipe"},{"name":"ManQuiosque"},{"name":"Notificacoes"},{"name":"PerfilSeguranca"},{"name":"PlanOperacaoCard"},{"name":"PplAgendaSemanal"},{"name":"PplCardColumnDay"},{"name":"PplCenarios"},{"name":"PplDiario"},{"name":"PplDocumentos"},{"name":"PplEventCard"},{"name":"PplFiltrosPainel"},{"name":"PplFrotaTimeline"},{"name":"PplGanttMotorista"},{"name":"PplGridProgramacao"},{"name":"PplHistoricoEntrega"},{"name":"PplInfoBoxAderencia"},{"name":"PplKpiDonuts"},{"name":"PplLane"},{"name":"PplMapaAreas"},{"name":"PplPosicaoFrota"},{"name":"PplStatusBars"},{"name":"PplWizard"},{"name":"RhCampoRepetidor"},{"name":"RhFiltros"},{"name":"RhGraficoCard"},{"name":"SeletorSemanas"},{"name":"ShortcutButton"},{"name":"SideRailAction"},{"name":"SmartReport"},{"name":"SplitButton"},{"name":"StatusChip"},{"name":"StatusCircle"},{"name":"Tabs"},{"name":"ThemeSwitch"},{"name":"Toast"},{"name":"Utilities"},{"name":"UxClientesAdmin"},{"name":"UxEntregaProgresso"},{"name":"UxFiltroPagador"},{"name":"UxWidgetsCliente"},{"name":"ViolCampoDinamico"},{"name":"ViolCatalogoWizard"},{"name":"ViolEtapas"},{"name":"ViolRegrasBoard"},{"name":"ViolSimulador"},{"name":"VirtualizedAutocomplete"}]} */
(function () {
  var R = window.React, h = R.createElement;
  function cx() { return Array.prototype.slice.call(arguments).filter(Boolean).join(' '); }
  function omit(p, keys) { var o = {}; for (var k in p) if (keys.indexOf(k) < 0) o[k] = p[k]; return o; }

  function Arrow(size) {
    return h('svg', { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2.2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true' }, h('path', { d: 'M5 12h14M13 6l6 6-6 6' }));
  }
  function Chevron() {
    return h('svg', { width: 18, height: 18, viewBox: '0 0 24 24', fill: 'none', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true' }, h('path', { d: 'm6 9 6 6 6-6' }));
  }

  function Page(p) {
    return h(p.as || 'div', Object.assign(omit(p, ['as', 'className', 'children']), { className: cx('alt-page', p.className) }), p.children);
  }

  function Glass(p) {
    return h(p.as || 'div', Object.assign(omit(p, ['as', 'className', 'level', 'children']), { className: cx('alt-glass', p.level === 'overlay' && 'alt-glass--overlay', p.className) }), p.children);
  }

  function Logo(p) {
    var size = p.size || 28;
    var mark = h('svg', { width: size, height: size, viewBox: '0 0 96 96', 'aria-hidden': 'true' },
      h('path', { d: 'M18 78 L36 18 H46 L30 78 Z', fill: '#FFFFFF' }),
      h('path', { d: 'M78 78 L60 18 H50 L66 78 Z', fill: '#FFFFFF' }),
      h('path', { d: 'M31 52 L48 72 L65 52 Z M39 52 H57 L48 64 Z', fill: '#5EEAD4', fillRule: 'evenodd' }),
      h('path', { d: 'M28 80 H68 L64 88 H32 Z', fill: '#FFFFFF' }));
    if (p.markOnly) return h('span', { role: 'img', 'aria-label': 'Altivy', style: { display: 'inline-flex' } }, mark);
    return h(p.href ? 'a' : 'span', { href: p.href, className: cx('alt-brand', p.className), style: { fontSize: Math.round(size * 0.68) + 'px' } }, mark, 'Altivy');
  }

  function Button(p) {
    var variant = p.variant || 'primary', size = p.size || 'md';
    var arrow = p.arrow === undefined ? variant === 'primary' && size !== 'sm' : p.arrow;
    return h(p.href ? 'a' : 'button', Object.assign(omit(p, ['variant', 'size', 'arrow', 'className', 'children']), {
      className: cx('alt-btn', 'alt-btn--' + variant, size !== 'md' && 'alt-btn--' + size, p.className),
      type: p.href ? undefined : (p.type || 'button')
    }), p.children, arrow ? Arrow(size === 'lg' ? 17 : 16) : null);
  }

  function Eyebrow(p) {
    return h('div', { className: cx('alt-eyebrow', p.className) }, p.rule === false ? null : h('span', { className: 'alt-eyebrow__rule', 'aria-hidden': 'true' }), h('span', null, p.children));
  }

  function SectionHeading(p) {
    var Tag = p.as || 'h2';
    return h('div', { className: cx('alt-heading', p.className) },
      h('div', { className: 'alt-heading__main' }, p.eyebrow ? h(Eyebrow, null, p.eyebrow) : null, h(Tag, { className: 'alt-heading__title' }, p.title)),
      p.aside ? h('p', { className: 'alt-heading__aside' }, p.aside) : null);
  }

  function Card(p) {
    return h('div', { className: cx('alt-glass', 'alt-card', p.className) },
      h('div', { className: 'alt-card__top' }, h('div', { className: 'alt-card__title' }, p.title), p.index ? h('div', { className: 'alt-card__index' }, p.index) : null),
      p.text ? h('p', { className: 'alt-card__text' }, p.text) : null,
      p.bullets && p.bullets.length ? h(R.Fragment, null, h('hr', { className: 'alt-divider' }), h('ul', { className: 'alt-bullets' }, p.bullets.map(function (b, i) { return h('li', { key: i }, b); }))) : null,
      p.children);
  }

  function NumberedCard(p) {
    return h('div', { className: cx('alt-glass', 'alt-ncard', p.active && 'alt-ncard--active', p.className) },
      h('div', { className: 'alt-ncard__num' }, p.number),
      h('div', { className: 'alt-ncard__body' }, h('div', { className: 'alt-ncard__title' }, p.title), p.text ? h('p', { className: 'alt-ncard__text' }, p.text) : null));
  }

  function FAQ(p) {
    var st = R.useState(p.defaultOpen === undefined ? 0 : p.defaultOpen), open = st[0], setOpen = st[1];
    return h('div', { className: cx('alt-faq', p.className) }, (p.items || []).map(function (it, i) {
      var isOpen = open === i, id = 'alt-faq-' + i;
      return h('div', { key: i, className: cx('alt-faq__item', isOpen && 'alt-glass alt-faq__item--open') },
        h('button', { className: 'alt-faq__q', 'aria-expanded': isOpen, 'aria-controls': id, onClick: function () { setOpen(isOpen ? -1 : i); } }, h('span', null, it.q), Chevron()),
        isOpen ? h('div', { id: id, className: 'alt-faq__a' }, it.a) : null);
    }));
  }

  function Header(p) {
    return h('header', { className: cx('alt-glass', 'alt-header', p.className) },
      h(Logo, { href: p.homeHref || '#', size: 28 }),
      h('nav', { className: 'alt-nav', 'aria-label': 'Principal' },
        (p.links || []).map(function (l, i) { return h('a', { key: i, href: l.href || '#' }, l.label); }),
        p.cta ? h(Button, { size: 'sm', href: p.cta.href || '#' }, p.cta.label) : null));
  }

  function MethodTrack(p) {
    var steps = p.steps || [], n = steps.length, cur = p.current || 0;
    var pos = function (i) { return n < 2 ? 0 : (i / (n - 1)) * 100; };
    return h('div', { className: cx('alt-track', p.className) },
      h('div', { className: 'alt-track__rail', role: 'progressbar', 'aria-valuemin': 1, 'aria-valuemax': n, 'aria-valuenow': cur + 1 },
        h('div', { className: 'alt-track__fill', style: { width: pos(cur) + '%' } }),
        steps.map(function (_, i) {
          return h('div', { key: i, className: cx('alt-track__dot', i === 0 && 'alt-track__dot--current', i > cur && 'alt-track__dot--todo'), style: { left: pos(i) + '%', marginLeft: i === n - 1 ? -14 : i === 0 ? 0 : undefined } });
        })),
      h('div', { className: 'alt-track__steps', style: { gridTemplateColumns: 'repeat(' + n + ', minmax(0, 1fr))' } }, steps.map(function (s, i) {
        return h('div', { key: i, className: cx('alt-track__step', i > cur && 'alt-track__step--todo') },
          h('div', { className: 'alt-track__kicker' }, s.kicker || ('Etapa ' + String(i + 1).padStart(2, '0'))),
          h('div', { className: 'alt-track__title' }, s.title),
          s.text ? h('p', { className: 'alt-track__text' }, s.text) : null);
      })));
  }

  function CTABanner(p) {
    return h('section', { className: cx('alt-glass', 'alt-glass--overlay', 'alt-banner', p.className) },
      h('div', { className: 'alt-banner__halo', 'aria-hidden': 'true' }),
      h('div', { className: 'alt-banner__row' },
        h('div', { className: 'alt-banner__copy' }, h('h2', { className: 'alt-banner__title' }, p.title), p.text ? h('p', { className: 'alt-banner__text' }, p.text) : null),
        p.cta ? h(Button, { size: 'lg', href: p.cta.href }, p.cta.label) : null));
  }

  function Footer(p) {
    var cols = p.columns || [];
    return h('footer', { className: cx('alt-footer', p.className) },
      h('div', { className: 'alt-footer__grid', style: { '--alt-cols': cols.length || 1 } },
        h('div', { className: 'alt-footer__about' }, h(Logo, { size: 26 }), p.about ? h('p', null, p.about) : null),
        cols.map(function (c, i) {
          return h('div', { key: i, className: 'alt-footer__col' }, h('div', { className: 'alt-footer__label' }, c.label), c.links.map(function (l, j) { return h('a', { key: j, href: l.href || '#' }, l.label); }));
        }),
        p.contact ? h('div', { className: 'alt-footer__col' }, h('div', { className: 'alt-footer__label' }, p.contact.label || 'Vamos conversar'), p.contact.text ? h('div', { style: { fontSize: 14, color: 'var(--muted)', lineHeight: 1.6 } }, p.contact.text) : null, h('a', { href: 'mailto:' + p.contact.email, style: { color: 'var(--teal-light)', fontSize: 13 } }, p.contact.email)) : null),
      h('div', { className: 'alt-footer__legal' }, h('span', null, p.legal), p.tagline ? h('span', null, p.tagline) : null));
  }

  window.Altivy = Object.assign(window.Altivy || {}, { Page: Page, Glass: Glass, Logo: Logo, Button: Button, Eyebrow: Eyebrow, SectionHeading: SectionHeading, Card: Card, NumberedCard: NumberedCard, FAQ: FAQ, Header: Header, MethodTrack: MethodTrack, CTABanner: CTABanner, Footer: Footer });
})();

/* Altivy app kit — shared helpers and icons used by every app component. Loaded before the components. */
(function () {
  var R = window.React, h = R.createElement;
  var A = window.Altivy = window.Altivy || {};

  function cx() { return Array.prototype.slice.call(arguments).filter(Boolean).join(' '); }
  function omit(p, keys) { var o = {}; for (var k in p) if (keys.indexOf(k) < 0) o[k] = p[k]; return o; }
  var nf = function (v, d) { return Number(v).toLocaleString('pt-BR', { minimumFractionDigits: d || 0, maximumFractionDigits: d || 0 }); };

  /* Lucide-style stroke icons, viewBox 24. */
  var P = {
    'arrow-right': 'M5 12h14M13 6l6 6-6 6',
    'arrow-left': 'M19 12H5M11 18l-6-6 6-6',
    'arrow-up': 'M12 19V5M6 11l6-6 6 6',
    'arrow-down': 'M12 5v14M18 13l-6 6-6-6',
    'chevron-down': 'm6 9 6 6 6-6',
    'chevron-up': 'm18 15-6-6-6 6',
    'chevron-right': 'm9 18 6-6-6-6',
    'chevron-left': 'm15 18-6-6 6-6',
    'chevrons-left': 'm11 17-5-5 5-5M18 17l-5-5 5-5',
    'chevrons-right': 'm13 17 5-5-5-5M6 17l5-5-5-5',
    'x': 'M18 6 6 18M6 6l12 12',
    'check': 'M20 6 9 17l-5-5',
    'plus': 'M12 5v14M5 12h14',
    'minus': 'M5 12h14',
    'search': 'M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14zM20 20l-3.5-3.5',
    'filter': 'M3 5h18l-7 8v6l-4 2v-8z',
    'menu': 'M4 7h16M4 12h16M4 17h16',
    'more': 'M5 12h.01M12 12h.01M19 12h.01',
    'more-v': 'M12 5h.01M12 12h.01M12 19h.01',
    'home': 'M3 10.5 12 3l9 7.5M5 9.5V21h14V9.5',
    'grid': 'M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z',
    'list': 'M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01',
    'columns': 'M3 4h18v16H3zM9 4v16M15 4v16',
    'table': 'M3 4h18v16H3zM3 10h18M3 15h18M9 4v16',
    'bell': 'M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9M10.3 21a1.9 1.9 0 0 0 3.4 0',
    'user': 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4 21a8 8 0 0 1 16 0',
    'users': 'M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M16 3.1a4 4 0 0 1 0 7.8M22 21a7 7 0 0 0-4-6.3',
    'settings': 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z',
    'calendar': 'M3 5h18v16H3zM16 3v4M8 3v4M3 11h18',
    'clock': 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 7v5l3 2',
    'truck': 'M1 4h14v12H1zM15 9h4l3 3v4h-7M5.5 21a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM18.5 21a2 2 0 1 0 0-4 2 2 0 0 0 0 4z',
    'map': 'M9 4 3 6v14l6-2 6 2 6-2V4l-6 2zM9 4v14M15 6v14',
    'pin': 'M12 21s-7-6.2-7-11a7 7 0 0 1 14 0c0 4.8-7 11-7 11zM12 12.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z',
    'route': 'M6 19a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM18 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM9 16h6.5a3.5 3.5 0 0 0 0-7H12',
    'file': 'M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9zM14 3v6h6M8 13h8M8 17h5',
    'upload': 'M12 16V4M6 10l6-6 6 6M4 20h16',
    'download': 'M12 4v12M6 10l6 6 6-6M4 20h16',
    'trash': 'M3 6h18M8 6V4h8v2M6 6l1 14h10l1-14',
    'edit': 'M4 20h4L19 9l-4-4L4 16zM14 6l4 4',
    'copy': 'M9 9h11v11H9zM5 15H4V4h11v1',
    'eye': 'M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z',
    'lock': 'M4 11h16v10H4zM8 11V7a4 4 0 0 1 8 0v4',
    'shield': 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z',
    'key': 'M15 9a4 4 0 1 0-3.5 4L8 16.5V19h2.5v-2H13v-2l1.1-1.1A4 4 0 0 0 15 9zM16 8h.01',
    'alert': 'M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0zM12 9v4M12 17h.01',
    'info': 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 16v-4M12 8h.01',
    'check-circle': 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM8.5 12.5l2.5 2.5 4.5-5',
    'x-circle': 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM15 9l-6 6M9 9l6 6',
    'refresh': 'M20 11a8 8 0 0 0-14.9-3.9L4 9M4 4v5h5M4 13a8 8 0 0 0 14.9 3.9L20 15M20 20v-5h-5',
    'chart-bar': 'M4 20V10M10 20V4M16 20v-7M22 20H2',
    'chart-line': 'M3 3v18h18M7 15l4-4 3 3 6-6',
    'chart-pie': 'M21 12a9 9 0 1 1-9-9v9z M14 3.2A9 9 0 0 1 20.8 10H14z',
    'layers': 'm12 2 10 5-10 5L2 7zM2 12l10 5 10-5M2 17l10 5 10-5',
    'box': 'M21 8 12 3 3 8v8l9 5 9-5zM3 8l9 5 9-5M12 13v8',
    'tool': 'M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 0 0 5.4-5.4l-2.6 2.6-2.4-.6-.6-2.4z',
    'message': 'M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12z',
    'inbox': 'M22 12h-6l-2 3h-4l-2-3H2M5.5 5h13L22 12v7H2v-7z',
    'flag': 'M4 22V4M4 4h13l-2 4 2 4H4',
    'zap': 'M13 2 3 14h9l-1 8 10-12h-9z',
    'activity': 'M22 12h-4l-3 9L9 3l-3 9H2',
    'gauge': 'M12 14l4-4M3.3 17a10 10 0 1 1 17.4 0',
    'fuel': 'M3 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18M3 22h12M15 12h2a2 2 0 0 1 2 2v3a2 2 0 0 0 4 0V9l-3-3M6 7h6',
    'thermometer': 'M14 14.8V4a2 2 0 0 0-4 0v10.8a4 4 0 1 0 4 0z',
    'signal': 'M2 20h.01M7 20v-4M12 20v-8M17 20V8M22 4v16',
    'wifi-off': 'M2 2l20 20M8.5 16.5a5 5 0 0 1 7 0M5 13a10 10 0 0 1 5.2-2.8M19 13a10 10 0 0 0-2.3-1.7M2 8.8a15 15 0 0 1 4.2-2.6M22 8.8A15 15 0 0 0 10.7 5M12 20h.01',
    'sun': 'M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10zM12 1v2M12 21v2M4.2 4.2l1.4 1.4M18.4 18.4l1.4 1.4M1 12h2M21 12h2M4.2 19.8l1.4-1.4M18.4 5.6l1.4-1.4',
    'moon': 'M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z',
    'monitor': 'M2 4h20v13H2zM8 21h8M12 17v4',
    'command': 'M18 3a3 3 0 0 0-3 3v12a3 3 0 1 0 3-3H6a3 3 0 1 0 3 3V6a3 3 0 1 0-3 3h12a3 3 0 0 0 0-6z',
    'star': 'm12 2 3.1 6.3 6.9 1-5 4.9 1.2 6.8L12 17.8 5.8 21l1.2-6.8-5-4.9 6.9-1z',
    'link': 'M10 13a5 5 0 0 0 7.5.5l3-3a5 5 0 0 0-7-7l-1.7 1.7M14 11a5 5 0 0 0-7.5-.5l-3 3a5 5 0 0 0 7 7l1.7-1.7',
    'paperclip': 'm21 11-8.5 8.5a5.5 5.5 0 0 1-7.8-7.8l9-9a3.7 3.7 0 0 1 5.2 5.2l-9 9a1.8 1.8 0 0 1-2.6-2.6L15.5 6',
    'send': 'M22 2 11 13M22 2l-7 20-4-9-9-4z',
    'drag': 'M9 5h.01M9 12h.01M9 19h.01M15 5h.01M15 12h.01M15 19h.01',
    'external': 'M15 3h6v6M10 14 21 3M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6',
    'dollar': 'M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6',
    'package': 'M16.5 9.4 7.5 4.2M21 16V8a2 2 0 0 0-1-1.7l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.7l7 4a2 2 0 0 0 2 0l7-4a2 2 0 0 0 1-1.7zM3.3 7 12 12l8.7-5M12 22V12',
    'scan': 'M3 7V5a2 2 0 0 1 2-2h2M17 3h2a2 2 0 0 1 2 2v2M21 17v2a2 2 0 0 1-2 2h-2M7 21H5a2 2 0 0 1-2-2v-2M7 12h10',
    'spreadsheet': 'M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9zM14 3v6h6M8 13l4 5M12 13l-4 5',
    'play': 'M6 4l14 8-14 8z',
    'pause': 'M6 4h4v16H6zM14 4h4v16h-4z',
    'sliders': 'M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6',
    'target': 'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20zM12 18a6 6 0 1 0 0-12 6 6 0 0 0 0 12zM12 14a2 2 0 1 0 0-4 2 2 0 0 0 0 4z',
    'trend-up': 'M22 7l-8.5 8.5-5-5L2 17M16 7h6v6',
    'trend-down': 'M22 17l-8.5-8.5-5 5L2 7M16 17h6v-6',
    'log-out': 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9',
    'help': 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3M12 17h.01',
    'circle': 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z',
    'hash': 'M4 9h16M4 15h16M10 3 8 21M16 3l-2 18',
    'wrench': 'M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.8-3.8a6 6 0 0 1-7.9 7.9l-6.9 6.9a2.1 2.1 0 0 1-3-3l6.9-6.9a6 6 0 0 1 7.9-7.9z',
    'building': 'M4 21V3h11v18M15 9h5v12M8 7h3M8 11h3M8 15h3M4 21h17',
    'award': 'M12 15a6 6 0 1 0 0-12 6 6 0 0 0 0 12zM8.2 13.9 7 22l5-3 5 3-1.2-8.1'
  };

  function Icon(p) {
    var size = p.size || 18, d = P[p.name] || P.circle;
    return h('svg', { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: p.color || 'currentColor', strokeWidth: p.strokeWidth || 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': p.title ? undefined : 'true', role: p.title ? 'img' : undefined, className: cx('alt-icon', p.className), style: p.style },
      p.title ? h('title', null, p.title) : null,
      d.split(/(?=M)/).map(function (seg, i) { return h('path', { key: i, d: seg }); }));
  }

  /* Semantic tone → token names. Every status shows a word or icon too. */
  var TONES = {
    success: { c: 'var(--signal-success)', bg: 'var(--success-tint)', icon: 'check-circle' },
    warning: { c: 'var(--signal-warning)', bg: 'var(--warning-tint)', icon: 'alert' },
    error: { c: 'var(--signal-error)', bg: 'var(--error-tint-strong)', icon: 'x-circle' },
    info: { c: 'var(--signal-info)', bg: 'var(--info-tint)', icon: 'info' },
    accent: { c: 'var(--teal-light)', bg: 'var(--accent-tint)', icon: 'zap' },
    neutral: { c: 'var(--muted)', bg: 'var(--hover-wash)', icon: 'circle' }
  };
  function tone(t) { return TONES[t] || TONES.neutral; }

  function useToggle(init) { var s = R.useState(!!init); return [s[0], function (v) { s[1](typeof v === 'boolean' ? v : function (x) { return !x; }); }]; }

  A._ = { h: h, R: R, cx: cx, omit: omit, nf: nf, icons: P, tone: tone, TONES: TONES, useToggle: useToggle };
  A.Icon = Icon;
})();


/* ---- Accordion ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var GROUPED = [
    { title: 'Dados da viagem', subtitle: 'Viagem 88213 · ABC-1D23', icon: 'truck',
      fields: [{ label: 'Motorista', value: 'João Pereira' }, { label: 'Origem', value: 'Campinas' }, { label: 'Destino', value: 'Santos' }, { label: 'Km rodado', value: '1.284,5' }] },
    { title: 'Violações', subtitle: 'Última às 14:12', icon: 'alert', status: { tone: 'error', label: '3 críticas' },
      content: 'Excesso de velocidade · Parada não autorizada · Desvio de rota.' },
    { title: 'Pontos de parada', subtitle: 'Previstos na rota', icon: 'pin', count: 6,
      content: 'Posto Graal km 102 · Base Jundiaí · CD Santos · Pátio Cubatão · Balança km 48 · Cliente final.' },
    { title: 'Custos da viagem', subtitle: 'Sem permissão de leitura', icon: 'lock', disabled: true }
  ];
  var SEPARATE = [
    { title: 'Como justificar uma violação?', content: 'Abra a violação na Central, escolha o motivo e descreva o que aconteceu. A justificativa fica visível para o CCO e para o gestor da frota.',
      actions: [{ label: 'Ver manual', variant: 'outline' }, { label: 'Abrir Central' }] },
    { title: 'Quem aprova a justificativa?', content: 'O supervisor do CCO responsável pela região da viagem.' },
    { title: 'Qual o prazo para justificar?', content: 'Até 48 horas após o fim da viagem.' }
  ];

  function Accordion(p) {
    var variant = p.variant || 'grouped';
    var items = p.items || (variant === 'separate' ? SEPARATE : GROUPED);
    var multiple = p.multiple !== false;
    var s = R.useState(p.defaultOpen || [0]);
    var open = p.open || s[0];
    var idBase = R.useRef('alt-acc-' + Math.random().toString(36).slice(2, 8)).current;
    function toggle(i) {
      var isOpen = open.indexOf(i) >= 0;
      var next = isOpen ? open.filter(function (x) { return x !== i; }) : (multiple ? open.concat([i]) : [i]);
      if (!p.open) s[1](next);
      if (p.onChange) p.onChange(next, i, !isOpen);
    }
    function body(it) {
      var parts = [];
      if (it.fields) parts.push(h('dl', { key: 'f', className: 'alt-accordion__kv' }, it.fields.map(function (f, j) {
        return [h('dt', { key: 'l' + j }, f.label), h('dd', { key: 'v' + j, className: 'alt-num' }, f.value)];
      })));
      if (it.content != null) parts.push(h('div', { key: 'c', className: 'alt-accordion__content' }, it.content));
      if (it.actions && it.actions.length) parts.push(h('div', { key: 'a', className: 'alt-accordion__actions' }, it.actions.map(function (a, j) {
        return R.isValidElement(a) ? R.cloneElement(a, { key: j }) : h(A.Button, { key: j, size: 'xs', variant: a.variant || 'primary', onClick: a.onClick, startIcon: a.icon }, a.label);
      })));
      return parts;
    }
    return h('div', { className: cx('alt-accordion', 'alt-accordion--' + variant, variant === 'grouped' && 'alt-glass', p.className), style: p.style },
      items.map(function (it, i) {
        var isOpen = !it.disabled && open.indexOf(i) >= 0, hid = idBase + '-h' + i, pid = idBase + '-p' + i;
        var tn = it.status ? K.tone(it.status.tone) : null;
        return h('div', { key: i, className: cx('alt-accordion__item', variant === 'separate' && 'alt-glass', isOpen && 'alt-accordion__item--open', it.disabled && 'alt-accordion__item--disabled') },
          h('h3', { className: 'alt-accordion__h' },
            h('button', { type: 'button', id: hid, className: 'alt-accordion__head', 'aria-expanded': isOpen, 'aria-controls': pid, disabled: it.disabled, onClick: function () { toggle(i); } },
              it.icon ? h('span', { className: 'alt-accordion__icon' }, h(A.Icon, { name: it.icon, size: 16 })) : null,
              h('span', { className: 'alt-accordion__titles' },
                h('span', { className: 'alt-accordion__title' }, it.title),
                it.subtitle ? h('span', { className: 'alt-accordion__subtitle' }, it.subtitle) : null),
              it.status ? h('span', { className: 'alt-accordion__status' }, h('span', { className: 'alt-dot', style: { color: tn.c } }), it.status.label)
                : it.count != null ? h('span', { className: 'alt-accordion__count alt-num' }, it.count) : null,
              h(A.Icon, { name: 'chevron-down', size: 16, className: 'alt-accordion__chev' }))),
          h('div', { id: pid, role: 'region', 'aria-labelledby': hid, className: 'alt-accordion__panel', 'aria-hidden': !isOpen, inert: isOpen ? undefined : '' },
            h('div', { className: 'alt-accordion__inner' },
              h('div', { className: cx('alt-accordion__body', it.icon && 'alt-accordion__body--indent') }, it.disabled ? null : body(it)))));
      }));
  }

  A.Accordion = Accordion;
})();


/* ---- AdmFilas ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone, nf = K.nf;

  /* Queue state, as in HorizonDashboard: accumulating when there are pending jobs and wait >= 60s. */
  function estadoFila(f) {
    if (f.pausada) return { id: 'pausada', label: 'Pausada', tone: 'neutral', icon: 'pause' };
    if (f.pendentes > 0 && f.espera >= 60) return { id: 'acumulando', label: 'Acumulando', tone: 'error', icon: 'x-circle' };
    if (f.pendentes > 0) return { id: 'atencao', label: 'Em atenção', tone: 'warning', icon: 'alert' };
    return { id: 'saudavel', label: 'Saudável', tone: 'success', icon: 'check-circle' };
  }

  var DEFAULT_FILAS = [
    { id: 'default', nome: 'default', pendentes: 142, espera: 94, carga: 82 },
    { id: 'integracoes', nome: 'integracoes', pendentes: 18, espera: 22, carga: 40 },
    { id: 'notificacoes', nome: 'notificacoes', pendentes: 0, espera: 0, carga: 6 },
    { id: 'relatorios', nome: 'relatorios', pendentes: 3, espera: 8, carga: 12, pausada: true }
  ];
  var DEFAULT_METRICAS = [
    { id: 'jpm', label: 'Jobs por minuto', value: '1.284' },
    { id: 'pend', label: 'Pendentes', value: '163' },
    { id: 'falhas', label: 'Falhas (24 h)', value: '7', tone: 'error' },
    { id: 'proc', label: 'Processos ativos', value: '12' }
  ];
  var DEFAULT_CONSOLE = ['$ php artisan horizon:status', 'Horizon is running.'];

  function AdmFilas(p) {
    var fs = R.useState(p.filas || DEFAULT_FILAS), filas = fs[0], setFilas = fs[1];
    var ps = R.useState(!!p.defaultPausado), pausado = ps[0], setPausado = ps[1];
    var ms = R.useState(null), menu = ms[0], setMenu = ms[1];
    var cs = R.useState(p.defaultConfirm || null), confirm = cs[0], setConfirm = cs[1];
    var ls = R.useState(p.console || DEFAULT_CONSOLE), linhas = ls[0], setLinhas = ls[1];
    var rootRef = R.useRef(null);
    var metricas = p.metricas || DEFAULT_METRICAS;

    R.useEffect(function () {
      if (!menu) return undefined;
      function onDoc(e) { if (!e.target.closest || !e.target.closest('.alt-adm-filas__menuwrap')) setMenu(null); }
      function onKey(e) { if (e.key === 'Escape') setMenu(null); }
      document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey);
      return function () { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); };
    }, [menu]);

    function log(cmd, out) { setLinhas(function (l) { return l.concat(['$ ' + cmd, out]); }); }
    function update(id, patch) { setFilas(function (fl) { return fl.map(function (f) { return f.id === id ? Object.assign({}, f, patch) : f; }); }); }
    function togglePausa() {
      var n = !pausado; setPausado(n);
      log('php artisan horizon:' + (n ? 'pause' : 'continue'), n ? 'Sending SIGUSR2 signal to processes... Horizon paused.' : 'Sending SIGCONT signal to processes... Horizon resumed.');
      if (n ? p.onPause : p.onResume) (n ? p.onPause : p.onResume)();
    }
    function pauseQueue(f) {
      setMenu(null); update(f.id, { pausada: !f.pausada });
      log('php artisan horizon:' + (f.pausada ? 'continue-supervisor' : 'pause-supervisor') + ' ' + f.nome, 'Supervisor ' + f.nome + (f.pausada ? ' resumed.' : ' paused.'));
      var cb = f.pausada ? p.onResumeQueue : p.onPauseQueue; if (cb) cb(f.id);
    }
    function clearQueue(f) {
      update(f.id, { pendentes: 0, espera: 0, carga: 0 }); setConfirm(null);
      log('php artisan queue:clear redis --queue=' + f.nome, 'Cleared ' + f.pendentes + ' jobs from the [' + f.nome + '] queue.');
      if (p.onClearQueue) p.onClearQueue(f.id);
    }
    var conf = confirm && filas.filter(function (f) { return f.id === confirm; })[0];

    return h('section', { ref: rootRef, className: cx('alt-adm-filas alt-glass', p.className), style: p.style, 'aria-label': p.title || 'Filas' },
      h('header', { className: 'alt-adm-filas__hd' },
        h('div', { className: 'alt-adm-filas__ht' }, h('div', { className: 'alt-adm-filas__eyebrow' }, p.eyebrow || 'Operação'), h('h3', null, p.title || 'Filas')),
        pausado ? h('span', { className: 'alt-chip', style: { background: tone('warning').bg, color: tone('warning').c } }, h(A.Icon, { name: 'pause', size: 12 }), 'Horizon pausado')
          : h('span', { className: 'alt-chip alt-adm-filas__refresh' }, h(A.Icon, { name: 'clock', size: 13 }), p.refreshLabel || 'Atualiza a cada 10 s'),
        h('button', { type: 'button', className: 'alt-tbtn', 'aria-pressed': pausado, onClick: togglePausa }, h(A.Icon, { name: pausado ? 'play' : 'pause', size: 14 }), pausado ? 'Retomar' : 'Pausar')),
      h('div', { className: 'alt-adm-filas__kpis alt-num', role: 'list' }, metricas.map(function (m) {
        var t = m.tone ? tone(m.tone) : null;
        return h('div', { key: m.id, role: 'listitem', className: 'alt-adm-filas__kpi' },
          h('span', null, t ? h(A.Icon, { name: t.icon, size: 13, style: { color: t.c } }) : null, m.label),
          h('b', { style: t ? { color: t.c } : null }, m.value));
      })),
      h('div', { className: 'alt-adm-filas__list', role: 'table', 'aria-label': 'Situação das filas' },
        h('div', { className: 'alt-adm-filas__q alt-adm-filas__q--head', role: 'row' },
          ['Fila', 'Estado', 'Carga', 'Pendentes · espera', ''].map(function (c, i) { return h('span', { key: i, role: 'columnheader' }, c); })),
        filas.map(function (f) {
          var e = estadoFila(f), t = tone(e.tone);
          return h('div', { key: f.id, role: 'row', className: cx('alt-adm-filas__q alt-num', f.pausada && 'alt-adm-filas__q--paused') },
            h('b', { role: 'cell', className: 'alt-adm-filas__nome' }, f.nome),
            h('span', { role: 'cell' }, h('span', { className: 'alt-chip', style: { background: t.bg, color: e.tone === 'neutral' ? 'var(--foreground)' : t.c } }, h(A.Icon, { name: e.icon, size: 12 }), e.label)),
            h('span', { role: 'cell', className: 'alt-adm-filas__bar', 'aria-label': 'Carga ' + f.carga + '%' }, h('i', { style: { width: Math.max(2, f.carga) + '%', background: e.tone === 'neutral' ? 'var(--slate)' : t.c } })),
            h('span', { role: 'cell', className: 'alt-adm-filas__m' }, nf(f.pendentes) + (f.pendentes === 1 ? ' pendente' : ' pendentes') + (f.pendentes > 0 ? ' · ' + f.espera + ' s' : '')),
            h('span', { role: 'cell', className: 'alt-adm-filas__menuwrap' },
              h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Mais ações da fila ' + f.nome, 'aria-haspopup': 'menu', 'aria-expanded': menu === f.id, onClick: function () { setMenu(menu === f.id ? null : f.id); } }, h(A.Icon, { name: 'more', size: 16 })),
              menu === f.id ? h('div', { className: 'alt-popover alt-adm-filas__menu', role: 'menu' },
                h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: function () { pauseQueue(f); } }, h(A.Icon, { name: f.pausada ? 'play' : 'pause', size: 15 }), f.pausada ? 'Retomar fila' : 'Pausar fila'),
                h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item alt-adm-filas__danger', disabled: f.pendentes === 0, onClick: function () { setMenu(null); setConfirm(f.id); } }, h(A.Icon, { name: 'trash', size: 15 }), 'Limpar fila…')) : null));
        })),
      h('div', { className: 'alt-adm-filas__con' },
        h('div', { className: 'alt-adm-filas__conhd' }, h(A.Icon, { name: 'command', size: 13 }), 'Saída de comandos'),
        h('pre', { className: 'alt-adm-filas__pre', 'aria-live': 'polite' }, linhas.slice(-8).map(function (l, i) {
          return h('span', { key: i, className: l.charAt(0) === '$' ? 'alt-adm-filas__cmd' : null }, l + '\n');
        }))),
      conf ? h('div', { className: 'alt-adm-filas__scrim', onClick: function (e) { if (e.target === e.currentTarget) setConfirm(null); } },
        h('aside', { className: 'alt-glass alt-glass--overlay alt-adm-filas__drawer', role: 'alertdialog', 'aria-modal': 'true', 'aria-labelledby': 'alt-adm-filas-ct' },
          h('header', null, h('div', { className: 'alt-adm-filas__eyebrow alt-adm-filas__eyebrow--err' }, 'Ação irreversível'), h('h3', { id: 'alt-adm-filas-ct' }, 'Limpar a fila ' + conf.nome + '?')),
          h('p', { className: 'alt-num' }, nf(conf.pendentes) + ' jobs pendentes serão descartados e não poderão ser recuperados. Os jobs em execução terminam normalmente.'),
          h('footer', null,
            h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { setConfirm(null); } }, 'Cancelar'),
            h('button', { type: 'button', className: 'alt-adm-filas__btn-danger', autoFocus: true, onClick: function () { clearQueue(conf); } }, h(A.Icon, { name: 'trash', size: 14 }), 'Limpar fila')))) : null);
  }
  AdmFilas.estadoFila = estadoFila;
  A.AdmFilas = AdmFilas;
})();


/* ---- AdmPipeline ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  var DEFAULT_NODES = [
    { id: 'origem', eyebrow: 'Tecnologia', title: 'Macro 14 · Sighra', desc: 'INICIO DESCARGA' },
    { id: 'padrao', eyebrow: 'Macro canônica CORE', title: 'DESCARGA_INICIO', chips: [{ label: 'Estado: descarga', tone: 'info' }] },
    { id: 'efeito', eyebrow: 'Efeito', title: 'Abre descarga', chips: [{ label: 'Transição: abre', tone: 'success' }], done: true }
  ];
  var DEFAULT_EQUIP = [
    { id: 1, placa: 'ABC-1D23', equipamento: 'SGH-004812', minutos: 3 },
    { id: 2, placa: 'QRT-4F56', equipamento: 'SGH-005107', minutos: 47 },
    { id: 3, placa: 'MNO-2B71', estado: 'aguardando' },
    { id: 4, placa: 'JHK-7G89', estado: 'na' }
  ];

  function rel(min) {
    if (min < 1) return 'agora';
    if (min < 60) return 'há ' + min + ' min';
    var hh = Math.floor(min / 60); if (hh < 24) return 'há ' + hh + ' h';
    return 'há ' + Math.floor(hh / 24) + ' d';
  }

  /* Observed device cell (DispositivoObservadoCell). */
  function Equipamento(p) {
    if (p.estado === 'aguardando') return h('span', { className: 'alt-adm-pipeline__eq alt-adm-pipeline__eq--off' }, h(A.Icon, { name: 'clock', size: 13 }), 'aguardando 1ª leitura');
    if (p.estado === 'na' || !p.equipamento) return h('span', { className: 'alt-adm-pipeline__eq alt-adm-pipeline__eq--off' }, 'não se aplica');
    var stale = p.minutos >= (p.limiteMin || 30);
    return h('span', { className: 'alt-adm-pipeline__eq alt-num' },
      h('span', { className: 'alt-adm-pipeline__eqid' }, p.equipamento),
      h('span', { className: cx('alt-adm-pipeline__eqt', stale && 'alt-adm-pipeline__eqt--stale') }, stale ? h(A.Icon, { name: 'alert', size: 12 }) : null, rel(p.minutos)));
  }

  function Arrow() {
    return h('span', { className: 'alt-adm-pipeline__arrow', 'aria-hidden': 'true' }, h(A.Icon, { name: 'arrow-right', size: 16, strokeWidth: 2.2 }));
  }

  function AdmPipeline(p) {
    var nodes = p.nodes || DEFAULT_NODES;
    var equip = p.equipamentos === null ? null : (p.equipamentos || DEFAULT_EQUIP);
    var ss = R.useState(p.defaultSelected || null), sel = p.selected !== undefined ? p.selected : ss[0];
    function pick(id) { var n = sel === id ? null : id; ss[1](n); if (p.onSelectNode) p.onSelectNode(n); }

    var flow = [];
    nodes.forEach(function (n, i) {
      if (i) flow.push(h(Arrow, { key: 'a' + i }));
      var on = sel === n.id;
      flow.push(h('button', { key: n.id, type: 'button', className: cx('alt-adm-pipeline__node', on && 'alt-adm-pipeline__node--on'), 'aria-pressed': on, onClick: function () { pick(n.id); } },
        h('span', { className: 'alt-adm-pipeline__ey' }, h('span', { className: 'alt-adm-pipeline__step alt-num' }, String(i + 1).padStart(2, '0')), n.eyebrow,
          n.done ? h('span', { className: 'alt-adm-pipeline__done' }, h(A.Icon, { name: 'check-circle', size: 13 }), 'Completo') : null),
        h('span', { className: 'alt-adm-pipeline__nt' }, n.title),
        n.desc ? h('span', { className: 'alt-adm-pipeline__nd' }, n.desc) : null,
        n.chips && n.chips.length ? h('span', { className: 'alt-adm-pipeline__chips' }, n.chips.map(function (c) {
          var t = tone(c.tone);
          return h('span', { key: c.label, className: 'alt-chip', style: { background: t.bg, color: t.c } }, c.label);
        })) : null));
    });

    return h('section', { className: cx('alt-adm-pipeline alt-glass', p.className), style: p.style, 'aria-label': p.title || 'Mapeamento de macro' },
      h('header', { className: 'alt-adm-pipeline__hd' },
        h('div', null, h('div', { className: 'alt-adm-pipeline__eyebrow' }, p.eyebrow || 'Integrador CORE'), h('h3', null, p.title || 'Mapeamento de macro')),
        p.subtitle !== null ? h('p', null, p.subtitle || 'Origem → padrão → efeito: como a macro da tecnologia vira estado e cálculo no CORE.') : null),
      h('div', { className: 'alt-adm-pipeline__flow', role: 'list' }, flow),
      equip ? h('div', { className: 'alt-adm-pipeline__cells alt-panel' },
        h('div', { className: 'alt-adm-pipeline__cl alt-adm-pipeline__cl--head' }, h('span', null, 'Placa'), h('span', null, 'Equipamento observado')),
        equip.map(function (e) {
          return h('div', { key: e.id, className: 'alt-adm-pipeline__cl' }, h('span', { className: 'alt-num alt-adm-pipeline__placa' }, e.placa), h(Equipamento, e));
        })) : null);
  }
  AdmPipeline.Equipamento = Equipamento;
  A.AdmPipeline = AdmPipeline;
})();


/* ---- AppHeader ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var Icon = A.Icon;

  var DEF_CRUMBS = ['CCO', 'Operação', 'Violações'];
  var DEF_USER = { name: 'Ana Ribeiro', group: 'Operação CCO', initials: 'AR' };
  var DEF_SHORTCUTS = [
    { id: 'viagens', label: 'Viagens', icon: 'truck' },
    { id: 'violacoes', label: 'Violações', icon: 'alert' },
    { id: 'motoristas', label: 'Motoristas', icon: 'user' }
  ];
  var DEF_PALETTES = [
    { id: 'blue-grey', label: 'Blue/Grey', swatch: 'var(--slate)' },
    { id: 'ocean', label: 'OceanBlue', swatch: 'var(--teal-light)' },
    { id: 'trasse', label: 'Trasse', swatch: 'var(--signal-info)' }
  ];

  function initials(name) {
    return String(name || '').split(/\s+/).filter(Boolean).slice(0, 2).map(function (s) { return s[0]; }).join('').toUpperCase();
  }

  function val(p, k, d) { return p[k] === undefined ? d : p[k]; }

  function AppHeader(p) {
    var title = val(p, 'title', 'Central de Violações');
    var crumbs = val(p, 'breadcrumbs', DEF_CRUMBS) || [];
    var user = val(p, 'user', DEF_USER);
    var env = val(p, 'env', 'Dev');
    var notif = val(p, 'notifications', 3);
    var shortcuts = val(p, 'shortcuts', DEF_SHORTCUTS) || [];
    var palettes = val(p, 'palettes', DEF_PALETTES) || [];
    var logo = p.logo === undefined ? h(A.Logo, { size: 24 }) : p.logo;

    var sc = R.useState(p.defaultShortcut || (shortcuts[0] && shortcuts[0].id));
    var activeShortcut = p.activeShortcut !== undefined ? p.activeShortcut : sc[0];
    var mo = R.useState(!!p.defaultUserMenuOpen);
    var menuOpen = mo[0], setMenuOpen = mo[1];
    var pal = R.useState(p.defaultPalette || 'ocean');
    var palette = p.palette !== undefined ? p.palette : pal[0];
    var dm = R.useState(p.defaultDarkMode !== undefined ? p.defaultDarkMode : true);
    var dark = p.darkMode !== undefined ? p.darkMode : dm[0];
    var rootRef = R.useRef(null);

    R.useEffect(function () {
      if (!menuOpen) return;
      function onDoc(e) { if (rootRef.current && !rootRef.current.contains(e.target)) setMenuOpen(false); }
      function onKey(e) { if (e.key === 'Escape') setMenuOpen(false); }
      document.addEventListener('mousedown', onDoc);
      document.addEventListener('keydown', onKey);
      return function () { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); };
    }, [menuOpen]);

    function pickShortcut(s) { sc[1](s.id); if (p.onShortcut) p.onShortcut(s.id, s); }
    function pickPalette(x) { pal[1](x.id); if (p.onPaletteChange) p.onPaletteChange(x.id); }
    function toggleDark() { dm[1](!dark); if (p.onDarkModeChange) p.onDarkModeChange(!dark); }
    function act(fn) { return function () { setMenuOpen(false); if (fn) fn(); }; }

    var ini = user ? (user.initials || initials(user.name)) : '';
    var menuIcon = p.menuIcon || 'menu';

    var bar = h('div', { className: 'alt-app-header__bar alt-glass' },
      p.showMenuButton === false ? null : h('button', {
        type: 'button', className: 'alt-app-header__btn',
        'aria-label': p.menuLabel || 'Abrir menu', 'aria-expanded': p.menuExpanded,
        onClick: p.onMenuClick
      }, typeof menuIcon === 'string' ? h(Icon, { name: menuIcon, size: 20 }) : menuIcon),
      logo ? h('span', { className: 'alt-app-header__logo' }, logo) : null,
      logo ? h('span', { className: 'alt-app-header__vr', 'aria-hidden': 'true' }) : null,
      h('div', { className: 'alt-app-header__titles' },
        h('h1', { className: 'alt-app-header__title' }, title),
        crumbs.length ? h('nav', { className: 'alt-app-header__crumbs', 'aria-label': 'Trilha' },
          crumbs.map(function (c, i) {
            var last = i === crumbs.length - 1;
            return h(R.Fragment, { key: i },
              i > 0 ? h(Icon, { name: 'chevron-right', size: 12 }) : null,
              h('span', { className: last ? 'alt-app-header__crumb--current' : null, 'aria-current': last ? 'page' : undefined }, c));
          })) : null),
      user ? h('div', { className: 'alt-app-header__user' },
        h('span', { className: 'alt-app-header__user-name' },
          env ? h('span', { className: 'alt-app-header__env', title: 'Ambiente: ' + env }, h('span', { className: 'alt-app-header__env-dot', 'aria-hidden': 'true' }), env) : null,
          user.name),
        user.group ? h('span', { className: 'alt-app-header__user-group' }, user.group) : null) : null,
      notif !== null && notif !== false ? h('button', {
        type: 'button', className: 'alt-app-header__btn alt-app-header__notif',
        'aria-label': notif ? 'Notificações, ' + notif + ' não lidas' : 'Notificações',
        onClick: p.onNotifications
      }, h(Icon, { name: 'bell', size: 20 }),
        notif ? h('span', { className: 'alt-app-header__badge alt-num', 'aria-hidden': 'true' }, notif > 99 ? '99+' : notif) : null) : null,
      user ? h('div', { className: 'alt-app-header__profile-wrap' },
        h('button', {
          type: 'button', className: 'alt-app-header__profile', 'aria-label': 'Menu do usuário',
          'aria-haspopup': 'menu', 'aria-expanded': menuOpen,
          onClick: function () { setMenuOpen(!menuOpen); }
        }, h('span', { className: 'alt-app-header__avatar' }, ini), h(Icon, { name: menuOpen ? 'chevron-up' : 'chevron-down', size: 16 })),
        menuOpen ? h('div', { className: 'alt-app-header__menu alt-popover', role: 'menu', 'aria-label': 'Menu do usuário' },
          h('div', { className: 'alt-app-header__menu-head' },
            h('span', { className: 'alt-app-header__avatar alt-app-header__avatar--lg' }, ini),
            h('div', null, h('b', null, user.name), user.group ? h('span', null, user.group) : null)),
          h('div', { className: 'alt-app-header__hr', role: 'separator' }),
          h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: act(p.onProfile) }, h(Icon, { name: 'user', size: 18 }), 'Perfil'),
          p.showDarkMode === false ? null : h('button', { type: 'button', role: 'menuitemcheckbox', 'aria-checked': dark, className: 'alt-menu-item', onClick: toggleDark },
            h(Icon, { name: 'moon', size: 18 }), 'Modo escuro',
            h('span', { className: cx('alt-app-header__switch', dark && 'is-on'), 'aria-hidden': 'true' })),
          palettes.length ? h(R.Fragment, null,
            h('div', { className: 'alt-app-header__hr', role: 'separator' }),
            h('div', { className: 'alt-app-header__section' }, p.paletteLabel || 'Paleta'),
            palettes.map(function (x) {
              var on = x.id === palette;
              return h('button', { key: x.id, type: 'button', role: 'menuitemradio', 'aria-checked': on, className: cx('alt-menu-item', on && 'alt-app-header__item--on'), onClick: function () { pickPalette(x); } },
                h('span', { className: 'alt-app-header__sq', style: { background: x.swatch }, 'aria-hidden': 'true' }), x.label,
                on ? h('span', { className: 'alt-app-header__check' }, h(Icon, { name: 'check', size: 16 })) : null);
            })) : null,
          h('div', { className: 'alt-app-header__hr', role: 'separator' }),
          h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: act(p.onAbout) }, h(Icon, { name: 'info', size: 18 }), 'Sobre'),
          h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: act(p.onLogout) }, h(Icon, { name: 'log-out', size: 18 }), 'Sair')) : null) : null);

    var shortcutBar = shortcuts.length ? h('nav', { className: 'alt-app-header__shortcuts', 'aria-label': 'Atalhos' },
      shortcuts.map(function (s) {
        var on = s.id === activeShortcut;
        return h('button', { key: s.id, type: 'button', className: cx('alt-app-header__shortcut', on && 'is-active'), 'aria-current': on ? 'page' : undefined, onClick: function () { pickShortcut(s); } },
          s.icon ? h(Icon, { name: s.icon, size: 14 }) : null, s.label);
      })) : null;

    return h('header', { ref: rootRef, className: cx('alt-app-header', p.className), style: p.style }, bar, shortcutBar);
  }

  A.AppHeader = AppHeader;
})();


/* ---- AppShell ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  var Icon = A.Icon;

  var DEF_MENU = [
    { section: 'Operação', items: [
      { id: 'inicio', label: 'Início', icon: 'home' },
      { id: 'torre', label: 'Torre de Controle', icon: 'target' },
      { id: 'violacoes', label: 'Central de Violações', icon: 'alert', count: 12, countLabel: 'abertas' },
      { id: 'paradas', label: 'Pontos de Parada', icon: 'pin' }
    ] },
    { section: 'Análise', items: [
      { id: 'bi', label: 'BI · Relatórios', icon: 'chart-bar' }
    ] }
  ];
  var DEF_SHORTCUTS = [
    { id: 'viagens', label: 'Viagens', icon: 'truck' },
    { id: 'violacoes', label: 'Violações', icon: 'alert' },
    { id: 'motoristas', label: 'Motoristas', icon: 'user' }
  ];
  var DEF_USER = { name: 'Ana Ribeiro', group: 'Operação CCO', initials: 'AR' };

  function findLabel(menu, id) {
    for (var i = 0; i < menu.length; i++) for (var j = 0; j < menu[i].items.length; j++) if (menu[i].items[j].id === id) return { item: menu[i].items[j], section: menu[i].section };
    return null;
  }

  /* Conteúdo de exemplo: KPIs + painel de viagens (3 colunas expandido, 4 recolhido). */
  function DemoContent(p) {
    var kpis = [
      { label: 'Em andamento', value: '128' },
      { label: 'Críticas', value: '7', note: '+2', tone: 'error' },
      { label: 'Paradas', value: '15', note: '3 longas', tone: 'warning' },
      { label: 'No prazo', value: nf(94.2, 1) + '%', note: 'meta 92%', tone: 'success' }
    ];
    if (!p.collapsed) kpis = kpis.slice(0, 3);
    var rows = [
      { plate: 'ABC-1D23', who: 'João Pereira · CCO Sul', st: 'Em dia', tone: 'success', pct: 92 },
      { plate: 'QRT-4F56', who: 'Marta Souza · CCO Norte', st: 'Crítica', tone: 'error', pct: 74 },
      { plate: 'JHK-7G89', who: 'Carlos Lima · CCO Sul', st: 'Parada', tone: 'warning', pct: 83 }
    ];
    return h('div', { className: 'alt-app-shell__demo', style: { gridTemplateColumns: 'repeat(' + kpis.length + ',minmax(0,1fr))' } },
      kpis.map(function (k) {
        var t = k.tone && K.tone(k.tone);
        return h('div', { key: k.label, className: 'alt-app-shell__kpi alt-glass' },
          h('span', null, k.label),
          h('div', null, h('b', { className: 'alt-num' }, k.value),
            k.note ? h('i', { style: { color: t.c } }, h(Icon, { name: t.icon, size: 13 }), k.note) : null));
      }),
      h('div', { className: 'alt-app-shell__wide alt-panel' },
        h('b', null, 'Viagens em andamento'),
        rows.map(function (r) {
          var t = K.tone(r.tone);
          return h('div', { key: r.plate, className: 'alt-app-shell__row' },
            h('span', { className: 'alt-app-shell__plate alt-num' }, r.plate),
            h('span', { className: 'alt-app-shell__who' }, r.who),
            h('span', { className: 'alt-app-shell__bar' }, h('span', { style: { width: r.pct + '%' } })),
            h('span', { className: 'alt-chip', style: { color: t.c, background: t.bg } }, h(Icon, { name: t.icon, size: 12 }), r.st));
        })));
  }

  function AppShell(p) {
    var menu = p.menu || DEF_MENU;
    var cs = R.useState(!!p.collapsed);
    var collapsed = cs[0];
    R.useEffect(function () { if (p.collapsed !== undefined) cs[1](!!p.collapsed); }, [p.collapsed]);
    var as = R.useState(p.defaultActive || 'torre');
    var active = p.active !== undefined ? p.active : as[0];
    var user = p.user === undefined ? DEF_USER : p.user;

    function toggle() { var n = !collapsed; cs[1](n); if (p.onCollapsedChange) p.onCollapsedChange(n); }
    function pick(it) { as[1](it.id); if (p.onNavigate) p.onNavigate(it.id, it); }

    var found = findLabel(menu, active);
    var title = p.title !== undefined ? p.title : (found ? found.item.label : '');
    var crumbs = p.breadcrumbs !== undefined ? p.breadcrumbs : (found ? ['CCO', found.section, found.item.label] : []);

    var nav = h('nav', { className: cx('alt-app-shell__nav alt-glass', collapsed && 'is-collapsed'), 'aria-label': 'Menu principal' },
      h('div', { className: 'alt-app-shell__brand' }, p.logo !== undefined ? p.logo : h(A.Logo, { size: 24, markOnly: collapsed })),
      menu.map(function (g, gi) {
        return h(R.Fragment, { key: gi },
          collapsed ? (gi > 0 ? h('div', { className: 'alt-app-shell__sep', role: 'separator' }) : null)
            : h('div', { className: 'alt-app-shell__section' }, g.section),
          g.items.map(function (it) {
            var on = it.id === active;
            var aria = it.label + (it.count ? ', ' + it.count + ' ' + (it.countLabel || '') : '');
            return h('button', {
              key: it.id, type: 'button', className: cx('alt-app-shell__item', on && 'is-active'),
              'aria-current': on ? 'page' : undefined, 'aria-label': collapsed ? aria : undefined,
              onClick: function () { pick(it); }
            },
              h(Icon, { name: it.icon || 'circle', size: 18 }),
              collapsed ? null : h('span', { className: 'alt-app-shell__label' }, it.label),
              it.count ? (collapsed ? h('span', { className: 'alt-app-shell__dot', 'aria-hidden': 'true' })
                : h('span', { className: 'alt-app-shell__count alt-num', 'aria-label': it.count + ' ' + (it.countLabel || '') }, it.count)) : null,
              collapsed ? h('span', { className: 'alt-app-shell__tip', role: 'tooltip', 'aria-hidden': 'true' }, it.label,
                it.count ? h('em', { className: 'alt-num' }, it.count) : null) : null);
          }));
      }),
      h('div', { style: { flex: 1 } }),
      user ? h('div', { className: 'alt-app-shell__me', title: user.name },
        h('span', { className: 'alt-app-shell__avatar', 'aria-label': user.name }, user.initials),
        collapsed ? null : h('div', null, h('b', null, user.name), h('span', null, user.group))) : null);

    var header = h(A.AppHeader, Object.assign({
      title: title, breadcrumbs: crumbs, logo: null, user: user,
      shortcuts: p.shortcuts || DEF_SHORTCUTS,
      menuIcon: collapsed ? h('svg', { width: 20, height: 20, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
        h('rect', { x: 3, y: 4, width: 18, height: 16, rx: 2 }), h('path', { d: 'M9 4v16M13 10l2 2-2 2' })) : 'menu',
      menuLabel: collapsed ? 'Expandir menu' : 'Recolher menu',
      menuExpanded: !collapsed, onMenuClick: toggle
    }, p.headerProps || {}));

    return h('div', { className: cx('alt-app-shell', collapsed && 'is-collapsed', p.className), style: Object.assign({ height: p.height || 440 }, p.style) },
      nav,
      h('div', { className: 'alt-app-shell__main' },
        header,
        h('main', { className: 'alt-app-shell__content alt-scroll' },
          p.children !== undefined ? p.children : h(DemoContent, { collapsed: collapsed }))));
  }

  A.AppShell = AppShell;
})();


/* ---- BiGraficos ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  /* ---- example data (R$ mil por dia, setembro/2026) ---- */
  var DAYS = []; for (var d = 1; d <= 30; d++) DAYS.push(d);
  function wk(d) { return (d + 1) % 7; } /* 01/09/2026 = terça */
  var REAL = DAYS.slice(0, 22).map(function (d) { var w = wk(d); return w === 0 ? 58 : w === 6 ? 112 : 184 + ((d * 37) % 40); });
  var PLAN = DAYS.map(function (d) { var w = wk(d); return w === 0 ? 76 : w === 6 ? 140 : 232; });
  var BUDG = DAYS.map(function (d) { var w = wk(d); return w === 0 ? 66 : w === 6 ? 120 : 200; });
  var COMP = DAYS.slice(0, 22).map(function (d) { return 8 + (d * 13) % 9; });
  var SERIES = [
    { id: 'real', name: 'Realizado', data: REAL, color: 'var(--chart-1)' },
    { id: 'plan', name: 'Planejado', data: PLAN, color: 'var(--chart-2)' },
    { id: 'budget', name: 'Budget', data: BUDG, color: 'var(--chart-3)' },
    { id: 'comp', name: 'Complemento', data: COMP, color: 'var(--chart-6)', dashed: true, off: true }
  ];
  function cum(a) { var s = 0; return a.map(function (v) { s += v; return s; }); }
  function weeks(a) { var o = []; for (var i = 0; i < 30; i += 7) { var part = a.slice(i, i + 7); if (part.length) o.push(part.reduce(function (x, y) { return x + y; }, 0)); } return o; }

  function Seg(p) {
    return h('div', { className: 'alt-bi-graficos__seg', role: 'group', 'aria-label': p.label }, p.options.map(function (o) {
      return h('button', { key: o[0], type: 'button', 'aria-pressed': p.value === o[0], onClick: function () { p.onChange(o[0]); } }, o[1]);
    }));
  }
  function Switch(p) {
    return h('button', { type: 'button', role: 'switch', 'aria-checked': p.checked, className: 'alt-bi-graficos__sw', onClick: function () { p.onChange(!p.checked); } }, h('i', { 'aria-hidden': 'true' }), p.label);
  }
  function Chips(p) {
    return h('div', { className: 'alt-bi-graficos__chips', role: 'group', 'aria-label': 'Séries' }, p.series.map(function (s) {
      var on = !p.hidden[s.id];
      return h('button', { key: s.id, type: 'button', className: 'alt-bi-graficos__chip', 'aria-pressed': on, style: { '--c': s.color, opacity: s.opacity == null ? null : undefined }, onClick: function () { p.onToggle(s.id); } },
        h('i', { className: s.dashed ? 'alt-bi-graficos__ln' : 'alt-bi-graficos__dot', style: s.opacity != null ? { opacity: s.opacity } : null }), s.name);
    }));
  }

  /* ---- Planejado × Realizado ---- */
  function PlanReal(p) {
    var G = A.Graphics, series = p.series || SERIES;
    var hs = R.useState(function () { var o = {}; series.forEach(function (s) { if (s.off) o[s.id] = true; }); return o; }), hidden = hs[0];
    var ms = R.useState(p.defaultMode || 'linha'), mode = ms[0];
    var gs = R.useState(p.defaultGrain || 'dia'), grain = gs[0];
    var as = R.useState(p.defaultAcumulado !== false), acc = as[0];
    var days = p.days || DAYS;
    var x = grain === 'dia' ? days.map(String) : ['S1', 'S2', 'S3', 'S4', 'S5'];
    var conv = function (a) { var b = grain === 'dia' ? a : weeks(a); return acc ? cum(b) : b; };
    var scale = acc ? 1000 : 1;
    var shown = series.map(function (s) { return Object.assign({}, s, { hidden: !!hidden[s.id], data: conv(s.data).map(function (v) { return v / scale; }) }); });
    var fmt = acc ? function (v) { return nf(v, 1); } : function (v) { return nf(v); };
    var tfmt = acc ? function (v) { return 'R$ ' + nf(v, 2) + ' mi'; } : function (v) { return 'R$ ' + nf(v) + ' mil'; };
    var nReal = series[0].data.length, projFrom = grain === 'dia' ? nReal - 1 : Math.floor((nReal - 1) / 7);
    var proj = null;
    if (mode === 'linha' && acc && !hidden[series[0].id]) {
      var r = shown[0].data, last = r[r.length - 1];
      proj = { from: projFrom, value: last / (nReal) * days.length, label: 'Projeção' };
    }
    var unit = acc ? 'R$ mi acumulado' : (grain === 'dia' ? 'R$ mil por dia' : 'R$ mil por semana');
    var chart = mode === 'linha'
      ? h(G.Line, { x: x, series: shown, projection: proj, reference: p.reference || null, yMax: p.yMax, height: p.height || 210, format: fmt, tipFormat: tfmt, endLabel: !hidden[series[0].id],
          tipTitle: function (v) { return grain === 'dia' ? v + '/09/2026' : 'Semana ' + v.slice(1); }, xTicks: grain === 'dia' ? [0, 7, 14, 21, 29] : [0, 1, 2, 3, 4], label: 'Planejado e realizado de faturamento em setembro' })
      : h(G.Columns, { yMax: p.yMax, categories: x.length > 10 ? x.map(function (v, i) { return i % 7 === 0 ? v : ''; }) : x, series: shown.filter(function (s) { return !s.dashed; }), height: p.height || 210, format: fmt, tipFormat: tfmt, partialLast: false, label: 'Planejado e realizado de faturamento em setembro' });
    return h(G.Card, { title: p.title || 'Planejado × Realizado · Faturamento', subtitle: unit + ' · setembro/2026', className: 'alt-bi-graficos', style: p.style,
      actions: h(Seg, { label: 'Tipo de gráfico', value: mode, onChange: ms[1], options: [['linha', 'Linha'], ['barra', 'Barra']] }),
      table: { head: ['Período'].concat(shown.map(function (s) { return s.name; })), rows: x.map(function (v, i) { return [v].concat(shown.map(function (s) { return s.data[i] == null ? '—' : fmt(s.data[i]); })); }) },
      toolbar: h('div', { className: 'alt-bi-graficos__tb' },
        h(Chips, { series: series, hidden: hidden, onToggle: function (id) { var o = Object.assign({}, hidden); o[id] = !o[id]; hs[1](o); } }),
        h('div', { className: 'alt-bi-graficos__opts' },
          h(Seg, { label: 'Granularidade', value: grain, onChange: gs[1], options: [['dia', 'Dia 1–30'], ['semana', 'Semana']] }),
          h(Switch, { label: 'Acumulado', checked: acc, onChange: as[1] }))),
      note: proj ? 'Projeção pelo ritmo médio diário até o dia ' + nReal + '.' : null }, chart);
  }

  /* ---- Comparativo diário (meses anteriores na mesma cor, opacidade 0.35–0.8) ---- */
  function Comparativo(p) {
    var G = A.Graphics;
    var months = p.months || [
      { id: 'set', name: 'Set/26', data: cum(REAL).map(function (v) { return v / 1000; }) },
      { id: 'ago', name: 'Ago/26', data: cum(PLAN.map(function (v, i) { return v * (0.93 + (i % 5) * 0.012); })).slice(0, 31).map(function (v) { return v / 1000; }) },
      { id: 'jul', name: 'Jul/26', data: cum(PLAN.map(function (v, i) { return v * (0.9 + (i % 4) * 0.01); })).map(function (v) { return v / 1000; }) },
      { id: 'jun', name: 'Jun/26', data: cum(PLAN.map(function (v, i) { return v * (0.86 + (i % 3) * 0.012); })).map(function (v) { return v / 1000; }) }];
    var op = [1, 0.8, 0.55, 0.35];
    var hs = R.useState({}), hidden = hs[0];
    var series = months.map(function (m, i) { return Object.assign({ color: 'var(--chart-1)', opacity: op[Math.min(i, 3)], width: i === 0 ? 2.5 : 2 }, m, { hidden: !!hidden[m.id] }); });
    return h(G.Card, { title: p.title || 'Comparativo diário', subtitle: 'R$ mi acumulado · dia do mês', className: 'alt-bi-graficos', style: p.style,
      toolbar: h(Chips, { series: series, hidden: hidden, onToggle: function (id) { var o = Object.assign({}, hidden); o[id] = !o[id]; hs[1](o); } }),
      table: { head: ['Dia'].concat(series.map(function (s) { return s.name; })), rows: DAYS.map(function (d, i) { return [d].concat(series.map(function (s) { return s.data[i] == null ? '—' : nf(s.data[i], 2); })); }) } },
      h(G.Line, { x: DAYS.map(String), series: series, height: p.height || 190, projection: null, reference: null, yMax: p.yMax, tipTitle: function (v) { return 'Dia ' + v; },
        tipFormat: function (v) { return 'R$ ' + nf(v, 2) + ' mi'; }, label: 'Faturamento acumulado de setembro comparado a junho, julho e agosto' }));
  }

  /* ---- Mapa por UF (tile grid, escala sequencial) ---- */
  var TILES = [['RR', 0, 2], ['AP', 0, 4], ['AM', 1, 1], ['PA', 1, 3], ['MA', 1, 4], ['CE', 1, 5], ['RN', 1, 6], ['AC', 2, 0], ['RO', 2, 1], ['MT', 2, 2], ['TO', 2, 3], ['PI', 2, 4], ['PE', 2, 5], ['PB', 2, 6],
    ['MS', 3, 2], ['GO', 3, 3], ['BA', 3, 4], ['SE', 3, 5], ['AL', 3, 6], ['DF', 4, 3], ['MG', 4, 4], ['ES', 4, 5], ['PR', 5, 2], ['SP', 5, 3], ['RJ', 5, 4], ['SC', 6, 2], ['RS', 7, 2]];
  var UF = { SP: 2140, MG: 820, PR: 610, RJ: 540, SC: 420, RS: 380, GO: 260, BA: 240, PE: 150, MS: 130, ES: 120, MT: 110, DF: 90, CE: 70, PA: 50, AM: 40, SE: 22, AL: 18, PB: 16, RN: 14, MA: 12, PI: 8, TO: 6, RO: 4, AC: 0, RR: 0, AP: 0 };
  function Mapa(p) {
    var G = A.Graphics, vals = p.values || UF, th = p.thresholds || [50, 150, 400, 800, 1500];
    function b(v) { if (!v) return 0; for (var i = 0; i < th.length; i++) if (v < th[i]) return i + 1; return 5; }
    return h(G.Card, { title: p.title || 'Faturamento por UF de destino', subtitle: 'R$ mil · setembro/2026', className: 'alt-bi-graficos', style: p.style,
      toolbar: h(G.SeqLegend, { max: 'mais (≥ ' + nf(th[4]) + ')' }),
      table: { head: ['UF', 'R$ mil'], rows: Object.keys(vals).sort(function (a, c) { return vals[c] - vals[a]; }).map(function (k) { return [k, nf(vals[k])]; }) } },
      h(G.Frame, { height: p.height || 256, label: 'Mapa do Brasil por UF: São Paulo concentra o faturamento', render: function (W, H, tp) {
        var cs = Math.min((W - 8) / 7, (H - 4) / 8), gap = 3, x0 = (W - cs * 7) / 2, out = [];
        TILES.forEach(function (t) {
          var v = vals[t[0]] || 0, k = b(v), x = x0 + t[2] * cs, y = t[1] * cs + 2, on = tp.key === t[0];
          out.push(h('g', Object.assign(tp.mark({ key: t[0], title: t[0], rows: [{ c: G.util.seq(k), v: 'R$ ' + nf(v) + ' mil' }] }, t[0] + ': R$ ' + nf(v) + ' mil'), { key: t[0] }),
            h('rect', { x: x + gap / 2, y: y + gap / 2, width: cs - gap, height: cs - gap, rx: 5, fill: G.util.seq(k), stroke: on ? 'var(--teal-light)' : 'none', strokeWidth: 1.5 }),
            h('text', { x: x + cs / 2, y: y + cs / 2 + 4, textAnchor: 'middle', className: k >= 4 ? 'alt-bi-graficos__uf alt-bi-graficos__uf--dark' : 'alt-bi-graficos__uf' }, t[0])));
        });
        return out;
      } }));
  }

  function BiGraficos(p) {
    var v = p.variant || 'planejadoRealizado', G = A.Graphics;
    if (v === 'comparativo') return h(Comparativo, p);
    if (v === 'mapa') return h(Mapa, p);
    if (v === 'dispersao') return h(G, Object.assign({ type: 'scatter', className: 'alt-bi-graficos', height: 190 }, p));
    if (v === 'barras') return h(G, Object.assign({ type: 'hbar', className: 'alt-bi-graficos', title: 'Documentos emitidos', subtitle: 'quantidade · setembro/2026', height: 190, labelWidth: 136,
      items: [{ label: 'CT-e', value: 3412 }, { label: 'NF-e', value: 2980 }, { label: 'MDF-e', value: 1206 }, { label: 'CT-e complementar', value: 184 }, { label: 'Cancelados', value: 42, other: true }],
      format: function (v) { return v >= 1000 ? nf(v / 1000, 1) + ' mil' : nf(v); }, tipFormat: function (v) { return nf(v) + ' documentos'; }, note: null }, p));
    return h(PlanReal, p);
  }
  BiGraficos.PlanejadoRealizado = PlanReal; BiGraficos.Comparativo = Comparativo; BiGraficos.Mapa = Mapa;
  A.BiGraficos = BiGraficos;
})();


/* ---- BiSelecaoMeses ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  var MONTHS = [
    { id: '2026-09', label: 'Set/2026', faturamento: 4.82e6, planejamento: 5.40e6, budget: 5.10e6 },
    { id: '2026-08', label: 'Ago/2026', faturamento: 5.01e6, planejamento: 5.20e6, budget: 5.00e6 },
    { id: '2026-07', label: 'Jul/2026', faturamento: 4.66e6, planejamento: null, budget: 4.90e6 },
    { id: '2026-06', label: 'Jun/2026', faturamento: 4.71e6, planejamento: 4.95e6, budget: 4.80e6 },
    { id: '2026-05', label: 'Mai/2026', faturamento: 4.38e6, planejamento: 4.60e6, budget: 4.70e6 }
  ];
  function mi(v) { return v == null ? null : nf(v / 1e6, 2) + ' mi'; }
  function Check(p) {
    return h('span', { className: 'alt-bi-selecao-meses__cb' },
      h('input', { type: 'checkbox', checked: !!p.checked, onChange: p.onChange, 'aria-label': p.label, ref: function (el) { if (el) el.indeterminate = !!p.mixed; } }),
      h('span', { 'aria-hidden': 'true' }, p.mixed ? h(A.Icon, { name: 'minus', size: 12, strokeWidth: 3 }) : p.checked ? h(A.Icon, { name: 'check', size: 12, strokeWidth: 3 }) : null));
  }
  function Grid(p) {
    var months = p.months, sel = p.selected, base = p.base, fmt = p.format || mi;
    var cols = p.columns || [['faturamento', 'Faturamento'], ['planejamento', 'Planejamento'], ['budget', 'Budget']];
    function toggle(id) {
      var on = sel.indexOf(id) >= 0, next = on ? sel.filter(function (x) { return x !== id; }) : sel.concat([id]);
      var nb = base; if (on && id === base) nb = next[0] || null; if (!on && !base) nb = id;
      p.set(next, nb);
    }
    function setBase(id) { p.set(sel.indexOf(id) >= 0 ? sel : sel.concat([id]), id); }
    var all = sel.length === months.length, some = sel.length > 0 && !all;
    return h('table', { className: 'alt-bi-selecao-meses__table alt-num' },
      h('thead', null, h('tr', null,
        h('th', null, h('span', { className: 'alt-bi-selecao-meses__mc' }, h(Check, { checked: all, mixed: some, label: 'Selecionar todos os meses', onChange: function () { p.set(all ? [] : months.map(function (m) { return m.id; }), all ? null : base || months[0].id); } }), 'Mês')),
        cols.map(function (c) { return h('th', { key: c[0] }, c[1]); }))),
      h('tbody', null, months.map(function (m) {
        var on = sel.indexOf(m.id) >= 0, isBase = base === m.id;
        return h('tr', { key: m.id, 'data-sel': on ? 'true' : null },
          h('td', null, h('span', { className: 'alt-bi-selecao-meses__mc' },
            h(Check, { checked: on, label: 'Comparar ' + m.label, onChange: function () { toggle(m.id); } }),
            h('button', { type: 'button', className: 'alt-bi-selecao-meses__star', 'aria-pressed': isBase, 'aria-label': isBase ? m.label + ' é o mês base' : 'Definir ' + m.label + ' como mês base', title: isBase ? 'Mês base' : 'Definir como mês base', onClick: function () { setBase(m.id); } },
              h(A.Icon, { name: 'star', size: 16 })),
            h('span', { className: 'alt-bi-selecao-meses__ml' }, m.label), isBase ? h('span', { className: 'alt-bi-selecao-meses__tag' }, 'base') : null)),
          cols.map(function (c) { var v = fmt(m[c[0]]); return h('td', { key: c[0] }, v == null ? h('span', { className: 'alt-bi-selecao-meses__na', 'aria-label': 'sem dado' }, '—') : v); }));
      })));
  }
  function BiSelecaoMeses(p) {
    var months = p.months || MONTHS;
    var ss = R.useState(p.defaultSelected || ['2026-09', '2026-08']), bs = R.useState(p.defaultBase === undefined ? '2026-09' : p.defaultBase);
    var sel = p.selected || ss[0], base = p.base !== undefined ? p.base : bs[0];
    var op = R.useState(!!p.defaultOpen), wrap = R.useRef(null);
    function set(s, b) { ss[1](s); bs[1](b); p.onChange && p.onChange({ selected: s, base: b }); }
    R.useEffect(function () {
      if (!op[0]) return;
      var k = function (e) { if (e.key === 'Escape') op[1](false); };
      var c = function (e) { if (wrap.current && !wrap.current.contains(e.target)) op[1](false); };
      document.addEventListener('keydown', k); document.addEventListener('mousedown', c);
      return function () { document.removeEventListener('keydown', k); document.removeEventListener('mousedown', c); };
    }, [op[0]]);
    var grid = h(Grid, { months: months, selected: sel, base: base, set: set, columns: p.columns, format: p.format });
    if (p.variant !== 'menu') return h('div', { className: cx('alt-bi-selecao-meses alt-panel', p.className), style: p.style }, grid);
    var bl = (months.filter(function (m) { return m.id === base; })[0] || {}).label;
    return h('div', { ref: wrap, className: cx('alt-bi-selecao-meses-menu', p.className), style: p.style },
      h('button', { type: 'button', className: 'alt-tbtn', 'aria-haspopup': 'dialog', 'aria-expanded': op[0], onClick: function () { op[1](!op[0]); } },
        h(A.Icon, { name: 'calendar', size: 16 }), sel.length + (sel.length === 1 ? ' mês' : ' meses'), bl ? h('span', { className: 'alt-bi-selecao-meses__bl' }, '· base ' + bl) : null, h(A.Icon, { name: 'chevron-down', size: 14 })),
      op[0] ? h('div', { className: 'alt-popover alt-bi-selecao-meses__pop', role: 'dialog', 'aria-label': 'Selecionar meses' },
        h('div', { className: 'alt-bi-selecao-meses__ph' }, 'Meses para comparar', h('span', null, 'Estrela = mês base')),
        grid,
        h('div', { className: 'alt-bi-selecao-meses__pf' }, h('span', null, sel.length + ' selecionados'),
          h(A.Button, { size: 'sm', onClick: function () { op[1](false); p.onApply && p.onApply({ selected: sel, base: base }); } }, 'Aplicar'))) : null);
  }
  A.BiSelecaoMeses = BiSelecaoMeses;
})();


/* ---- BiSemaforo ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  /* 5 faixas discretas: <50 crítico, 50–79 atenção, 80–99 próximo, 100–110 na meta, >110 superou */
  function band(pct) {
    if (pct == null || isNaN(pct)) return { key: 'none', label: 'Sem dado', c: 'var(--muted)', icon: 'minus' };
    if (pct < 50) return { key: 'crit', label: 'Crítico', c: 'var(--signal-error)', icon: 'x-circle' };
    if (pct < 80) return { key: 'warn', label: 'Atenção', c: 'var(--signal-warning)', icon: 'alert' };
    if (pct < 100) return { key: 'near', label: 'Próximo da meta', c: 'color-mix(in srgb, var(--signal-success) 60%, var(--foreground))', icon: 'trend-up' };
    if (pct <= 110) return { key: 'ok', label: 'Na meta', c: 'var(--signal-success)', icon: 'check-circle' };
    return { key: 'over', label: 'Superou', c: 'var(--signal-info)', icon: 'award' };
  }
  var ITEMS = [
    { title: 'Granéis · Faturamento', planejado: 'R$ 1,20 mi', realizado: 'R$ 1,16 mi', pct: 96.4, spark: [6, 9, 8, 13, 14, 19, 22] },
    { title: 'Vidros · Volume', planejado: '8.400 t', realizado: '3.528 t', pct: 42.0, spark: [11, 10, 8, 9, 6, 7] },
    { title: 'Químicos · Viagens', planejado: '640', realizado: '725', pct: 113.3, spark: [4, 6, 7, 9, 10, 12, 13] },
    { title: 'Paletizado · Faturamento', planejado: 'R$ 880 mil', realizado: 'R$ 612 mil', pct: 69.5, spark: [5, 7, 6, 8, 7, 9, 9] }
  ];
  function Spark(p) {
    var d = p.data, mn = Math.min.apply(null, d), mx = Math.max.apply(null, d), rg = mx - mn || 1;
    var pts = d.map(function (v, i) { return (i / (d.length - 1) * 100).toFixed(1) + ' ' + (3 + (1 - (v - mn) / rg) * 26).toFixed(1); });
    var path = 'M' + pts.join('L');
    return h('svg', { className: 'alt-bi-semaforo__spark', viewBox: '0 0 100 32', preserveAspectRatio: 'none', 'aria-hidden': 'true' },
      h('path', { d: path + 'L100 32L0 32Z', className: 'alt-bi-semaforo__area' }), h('path', { d: path, className: 'alt-bi-semaforo__line' }));
  }
  function Cell(p) {
    var b = band(p.pct);
    return h('span', { className: cx('alt-bi-semaforo__cell alt-num', p.className), title: b.label },
      h('i', { className: 'alt-dot', style: { color: b.c }, 'aria-hidden': 'true' }),
      p.pct == null ? h('span', { className: 'alt-bi-semaforo__nd' }, '—') : nf(p.pct, 1) + '%',
      h('span', { className: 'alt-bi-semaforo__sr' }, ' ' + b.label));
  }
  function Card(p) {
    var b = band(p.pct), on = p.selected;
    return h('button', { type: 'button', className: 'alt-bi-semaforo__card alt-glass', 'aria-pressed': on, onClick: p.onClick, 'data-band': b.key },
      h('span', { className: 'alt-bi-semaforo__h' }, h('i', { className: 'alt-dot', style: { color: b.c } }), h('span', null, p.title)),
      h('span', { className: 'alt-bi-semaforo__p alt-num', style: { color: b.c } }, p.pct == null ? '—' : nf(p.pct, 1) + '%'),
      h('span', { className: 'alt-bi-semaforo__band', style: { color: b.c } }, h(A.Icon, { name: b.icon, size: 13 }), b.label),
      h('span', { className: 'alt-bi-semaforo__kv alt-num' },
        h('span', null, p.labels ? p.labels[0] : 'Planej.'), h('b', null, p.planejado),
        h('span', null, p.labels ? p.labels[1] : 'Realizado'), h('b', null, p.realizado)),
      p.spark ? h(Spark, { data: p.spark }) : null);
  }
  function Table(p) {
    return h('div', { className: 'alt-bi-semaforo__tw alt-panel alt-scroll' }, h('table', { className: 'alt-bi-semaforo__table alt-num' },
      h('thead', null, h('tr', null, h('th', null, p.rowLabel || 'Operação'), p.cols.map(function (c, i) { return h('th', { key: i }, c); }))),
      h('tbody', null, p.rows.map(function (r, i) { return h('tr', { key: i }, h('th', { scope: 'row' }, r.label), r.values.map(function (v, j) { return h('td', { key: j }, h(Cell, { pct: v })); })); }))));
  }
  function BiSemaforo(p) {
    var items = p.items || ITEMS, s = R.useState(p.defaultSelected == null ? null : p.defaultSelected);
    return h('div', { className: cx('alt-bi-semaforo', p.className), style: p.style },
      h('div', { className: 'alt-bi-semaforo__grid' }, items.map(function (it, i) {
        return h(Card, Object.assign({ key: i, selected: s[0] === i, onClick: function () { var n = s[0] === i ? null : i; s[1](n); p.onSelect && p.onSelect(n == null ? null : it, n); } }, it));
      })),
      p.showScale !== false ? h('div', { className: 'alt-bi-semaforo__scale', 'aria-label': 'Escala' },
        [[40, '< 50%'], [60, '50–79%'], [90, '80–99%'], [105, '100–110%'], [120, '> 110%'], [null, '']].map(function (x, i) {
          var b = band(x[0]); return h('span', { key: i }, h('i', { className: 'alt-dot', style: { color: b.c } }), h('b', null, b.label), ' ' + x[1]);
        })) : null,
      p.table ? h(Table, p.table) : null);
  }
  BiSemaforo.Cell = Cell; BiSemaforo.Card = Card; BiSemaforo.Table = Table; BiSemaforo.band = band;
  A.BiSemaforo = BiSemaforo;
})();


/* ---- BiTotalizadores ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  var ITEMS = [
    { label: 'Faturamento', value: 'R$ 4,82 mi', sub: 'Set/2026', kind: 'bar', pct: 88, pctLabel: '88% do mês decorrido', color: 'var(--chart-1)' },
    { label: 'Planejamento', value: 'R$ 5,40 mi', kind: 'ring', pct: 89.3, pctLabel: 'atingido', color: 'var(--chart-2)' },
    { label: 'Budget', value: 'R$ 5,10 mi', kind: 'ring', pct: 94.5, pctLabel: 'atingido', color: 'var(--chart-3)' }
  ];
  function Ring(p) {
    var r = 20, c = 2 * Math.PI * r, v = Math.max(0, Math.min(100, p.pct));
    return h('svg', { className: 'alt-bi-totalizadores__ring', width: 52, height: 52, viewBox: '0 0 52 52', 'aria-hidden': 'true' },
      h('circle', { cx: 26, cy: 26, r: r, className: 'alt-bi-totalizadores__track' }),
      h('circle', { cx: 26, cy: 26, r: r, stroke: p.color, className: 'alt-bi-totalizadores__arc', strokeDasharray: (c * v / 100) + ' ' + c, transform: 'rotate(-90 26 26)' }));
  }
  function Tile(p) {
    if (p.loading) return h('div', { className: 'alt-bi-totalizadores__tile alt-glass', 'aria-busy': 'true' },
      h('div', { className: 'alt-skel', style: { width: 90, height: 12 } }), h('div', { className: 'alt-skel', style: { width: 140, height: 26, marginTop: 8 } }), h('div', { className: 'alt-skel', style: { width: '100%', height: 4, marginTop: 18 } }));
    var pct = p.pct != null ? nf(p.pct, p.pct % 1 ? 1 : 0) + '%' : null;
    return h('div', { className: 'alt-bi-totalizadores__tile alt-glass', role: 'group', 'aria-label': p.label + ': ' + p.value + (pct ? ', ' + pct + ' ' + (p.pctLabel || '') : '') },
      h('span', { className: 'alt-bi-totalizadores__l' }, h('i', { className: 'alt-bi-totalizadores__dot', style: { background: p.color } }), p.label),
      h('b', { className: 'alt-bi-totalizadores__v alt-num' }, p.value),
      p.kind === 'ring' ? h('div', { className: 'alt-bi-totalizadores__dn' }, h(Ring, { pct: p.pct, color: p.color }),
        h('div', null, h('span', { className: 'alt-bi-totalizadores__pct alt-num' }, pct), h('small', null, p.pctLabel)))
        : h(R.Fragment, null,
          p.sub ? h('small', { className: 'alt-bi-totalizadores__sub alt-num' }, p.sub) : null,
          p.pct != null ? h('div', { className: 'alt-bi-totalizadores__bar', role: 'progressbar', 'aria-valuenow': p.pct, 'aria-valuemin': 0, 'aria-valuemax': 100, 'aria-label': p.pctLabel },
            h('i', { style: { width: Math.min(100, p.pct) + '%', background: p.color } })) : null,
          p.pct != null ? h('small', { className: 'alt-bi-totalizadores__sub alt-num' }, p.pctLabel) : null));
  }
  function BiTotalizadores(p) {
    var items = p.items || ITEMS;
    return h('div', { className: cx('alt-bi-totalizadores', p.className), style: p.style },
      items.map(function (it, i) { return h(Tile, Object.assign({ key: i, loading: p.loading }, it)); }));
  }
  A.BiTotalizadores = BiTotalizadores;
})();


/* ---- BridgeVendas ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  var MESES = ['04/2026', '05/2026', '06/2026', '07/2026', '08/2026', '09/2026'];
  var ROWS = [
    { unidade: 'Granéis SP', baseT: 1200, compT: 1180, baseRpt: 182.40, compRpt: 188.10 },
    { unidade: 'Vidros PF', baseT: 820, compT: 857, baseRpt: 240.00, compRpt: 236.50 },
    { unidade: 'Paletizado Jundiaí', baseT: 610, compT: 655, baseRpt: 198.20, compRpt: 201.90 },
    { unidade: 'Transferência CD Campinas', baseT: 980, compT: 912, baseRpt: 96.40, compRpt: 99.10 },
    { unidade: 'Granéis PR', baseT: 540, compT: 588, baseRpt: 176.00, compRpt: 171.30 },
    { unidade: 'Químicos Paulínia', baseT: 430, compT: 430, baseRpt: 312.80, compRpt: 312.80 }
  ];
  var FILTERS = [
    { id: 'unidade', label: 'Unidade', options: ['Granéis SP', 'Granéis PR', 'Vidros PF', 'Paletizado Jundiaí', 'Transferência CD Campinas', 'Químicos Paulínia'] },
    { id: 'cliente', label: 'Cliente', options: ['Vidroporto', 'Owens-Illinois', 'Saint-Gobain', 'Verallia', 'Ambev'] },
    { id: 'produto', label: 'Produto', options: ['Areia', 'Barrilha', 'Calcário', 'Vidro plano', 'Paletes'] },
    { id: 'vendedor', label: 'Vendedor', options: ['Ana Martins', 'Carlos Lima', 'Paula Souza'] }
  ];
  function calc(r) { var preco = (r.compRpt - r.baseRpt) * r.compT, volume = (r.compT - r.baseT) * r.baseRpt; return Object.assign({ preco: preco, volume: volume, total: preco + volume }, r); }
  function Signed(p) {
    var v = Math.round(p.v * 100) / 100, k = v > 0 ? 'pos' : v < 0 ? 'neg' : 'zero';
    return h('span', { className: 'alt-bridge-vendas__s alt-bridge-vendas__s--' + k },
      k === 'zero' ? null : h(A.Icon, { name: k === 'pos' ? 'arrow-up' : 'arrow-down', size: 12, strokeWidth: 2.4 }),
      h('span', { className: 'alt-bridge-vendas__sr' }, k === 'pos' ? 'aumento ' : k === 'neg' ? 'redução ' : ''),
      (v < 0 ? '−' : '') + nf(Math.abs(v), 2));
  }
  function Check(p) {
    return h('label', { className: 'alt-bridge-vendas__cb' },
      h('input', { type: 'checkbox', checked: !!p.checked, onChange: function (e) { p.onChange(e.target.checked); } }),
      h('span', { className: 'alt-bridge-vendas__box', 'aria-hidden': 'true' }, p.checked ? h(A.Icon, { name: 'check', size: 12, strokeWidth: 3 }) : null), p.label);
  }
  function Filter(p) {
    var op = R.useState(false), ref = R.useRef(null), sel = p.value || [];
    R.useEffect(function () {
      if (!op[0]) return;
      var c = function (e) { if (ref.current && !ref.current.contains(e.target)) op[1](false); }, k = function (e) { if (e.key === 'Escape') op[1](false); };
      document.addEventListener('mousedown', c); document.addEventListener('keydown', k);
      return function () { document.removeEventListener('mousedown', c); document.removeEventListener('keydown', k); };
    }, [op[0]]);
    return h('div', { ref: ref, className: 'alt-bridge-vendas__flt' },
      h('button', { type: 'button', className: 'alt-tbtn', 'aria-haspopup': 'listbox', 'aria-expanded': op[0], 'data-active': sel.length ? 'true' : null, onClick: function () { op[1](!op[0]); } },
        h('span', { className: 'alt-bridge-vendas__fl' }, p.label + (sel.length ? ':' : '')),
        sel.length ? h('span', { className: 'alt-bridge-vendas__fv' }, sel[0]) : h('span', { className: 'alt-bridge-vendas__fall' }, 'Todos'),
        sel.length > 1 ? h('span', { className: 'alt-count' }, '+' + (sel.length - 1)) : null,
        h(A.Icon, { name: 'chevron-down', size: 14 })),
      op[0] ? h('div', { className: 'alt-popover alt-bridge-vendas__menu', role: 'listbox', 'aria-multiselectable': 'true', 'aria-label': p.label },
        p.options.map(function (o) {
          var on = sel.indexOf(o) >= 0;
          return h('button', { key: o, type: 'button', role: 'option', 'aria-selected': on, className: 'alt-menu-item', onClick: function () { p.onChange(on ? sel.filter(function (x) { return x !== o; }) : sel.concat([o])); } },
            h('span', { className: 'alt-bridge-vendas__box', 'data-on': on ? 'true' : null, 'aria-hidden': 'true' }, on ? h(A.Icon, { name: 'check', size: 12, strokeWidth: 3 }) : null), o);
        }),
        sel.length ? h('button', { type: 'button', className: 'alt-menu-item alt-bridge-vendas__clear', onClick: function () { p.onChange([]); } }, 'Limpar seleção') : null) : null);
  }
  function Mes(p) {
    return h('label', { className: 'alt-field' }, h('span', { className: 'alt-field__label' }, p.label),
      h('span', { className: 'alt-ctl alt-ctl--sm alt-bridge-vendas__mes' }, h(A.Icon, { name: 'calendar', size: 15 }),
        h('select', { value: p.value, onChange: function (e) { p.onChange(e.target.value); } }, p.meses.map(function (m) { return h('option', { key: m, value: m }, m); })),
        h(A.Icon, { name: 'chevron-down', size: 14 })));
  }
  var COLS = [
    { k: 'unidade', l: 'Unid. resumo', t: 'text' }, { k: 'baseT', l: 'Base t', f: 0 }, { k: 'compT', l: 'Comp. t', f: 0 },
    { k: 'baseRpt', l: 'Base R$/t', f: 2 }, { k: 'compRpt', l: 'Comp. R$/t', f: 2 },
    { k: 'preco', l: 'Preço R$', bridge: true }, { k: 'volume', l: 'Volume R$', bridge: true }, { k: 'total', l: 'Variação R$', bridge: true, strong: true }
  ];
  function BridgeVendas(p) {
    var rows = (p.rows || ROWS).map(calc), meses = p.meses || MESES;
    var st = R.useState({ base: p.defaultBase || '08/2026', comp: p.defaultComp || '09/2026', modo: 'realizado', complementar: false }), d = st[0];
    var ap = R.useState(d), applied = ap[0];
    var fs = R.useState(p.defaultFilters || { cliente: ['Vidroporto', 'Owens-Illinois'] }), filters = fs[0];
    var so = R.useState({ k: 'total', dir: 'desc' }), sort = so[0];
    function set(k, v) { var o = Object.assign({}, d); o[k] = v; st[1](o); }
    if (filters.unidade && filters.unidade.length) rows = rows.filter(function (r) { return filters.unidade.indexOf(r.unidade) >= 0; });
    var list = rows.slice().sort(function (a, b) {
      var x = a[sort.k], y = b[sort.k], r = typeof x === 'string' ? x.localeCompare(y, 'pt-BR') : x - y; return sort.dir === 'desc' ? -r : r;
    });
    var tB = 0, tC = 0, wB = 0, wC = 0, tp = 0, tv = 0;
    rows.forEach(function (r) { tB += r.baseT; tC += r.compT; wB += r.baseT * r.baseRpt; wC += r.compT * r.compRpt; tp += r.preco; tv += r.volume; });
    var tot = { unidade: 'Total', baseT: tB, compT: tC, baseRpt: wB / tB, compRpt: wC / tC, preco: tp, volume: tv, total: tp + tv };
    var dirty = JSON.stringify(d) !== JSON.stringify(applied);
    function cell(c, r) {
      if (c.t === 'text') return r[c.k];
      if (c.bridge) return h(Signed, { v: r[c.k] });
      return nf(r[c.k], c.f);
    }
    return h('div', { className: cx('alt-bridge-vendas', p.className), style: p.style },
      h('div', { className: 'alt-bridge-vendas__tb alt-glass' },
        h(Mes, { label: 'Mês base', value: d.base, meses: meses, onChange: function (v) { set('base', v); } }),
        h(A.Icon, { name: 'arrow-right', size: 16, className: 'alt-bridge-vendas__arrow' }),
        h(Mes, { label: 'Mês de comparação', value: d.comp, meses: meses, onChange: function (v) { set('comp', v); } }),
        h('div', { className: 'alt-field' }, h('span', { className: 'alt-field__label' }, 'Comparar com'),
          h('div', { className: 'alt-bridge-vendas__seg', role: 'group', 'aria-label': 'Comparar com' },
            [['realizado', 'Realizado'], ['budget', 'Budget']].map(function (o) { return h('button', { key: o[0], type: 'button', 'aria-pressed': d.modo === o[0], onClick: function () { set('modo', o[0]); } }, o[1]); }))),
        h(Check, { label: 'Incluir CT-e complementar', checked: d.complementar, onChange: function (v) { set('complementar', v); } }),
        h('div', { className: 'alt-bridge-vendas__apply' },
          dirty ? h('span', { className: 'alt-bridge-vendas__dirty' }, 'Alterações não aplicadas') : null,
          h(A.Button, { size: 'sm', onClick: function () { ap[1](d); p.onApply && p.onApply(Object.assign({ filtros: filters }, d)); } }, 'Aplicar'))),
      h('div', { className: 'alt-bridge-vendas__filters' },
        h(A.Icon, { name: 'filter', size: 15, className: 'alt-bridge-vendas__fi' }),
        (p.filters || FILTERS).map(function (f) {
          return h(Filter, { key: f.id, label: f.label, options: f.options, value: filters[f.id], onChange: function (v) { var o = Object.assign({}, filters); o[f.id] = v; fs[1](o); p.onFiltersChange && p.onFiltersChange(o); } });
        }),
        h('span', { className: 'alt-bridge-vendas__ctx alt-num' }, applied.base + ' → ' + applied.comp + ' · ' + (applied.modo === 'budget' ? 'Budget' : 'Realizado') + (applied.complementar ? ' · com CT-e complementar' : ''))),
      h('div', { className: 'alt-bridge-vendas__tw alt-panel alt-scroll' },
        h('table', { className: 'alt-bridge-vendas__table alt-num' },
          h('thead', null, h('tr', null, COLS.map(function (c) {
            var on = sort.k === c.k;
            return h('th', { key: c.k, 'aria-sort': on ? (sort.dir === 'desc' ? 'descending' : 'ascending') : 'none', className: c.t === 'text' ? 'alt-bridge-vendas__first' : null },
              h('button', { type: 'button', className: 'alt-bridge-vendas__sort', 'data-on': on ? 'true' : null, onClick: function () { so[1]({ k: c.k, dir: on && sort.dir === 'desc' ? 'asc' : 'desc' }); } },
                c.bridge ? h('i', { className: 'alt-bridge-vendas__bd', 'aria-hidden': 'true' }) : null, c.l,
                h(A.Icon, { name: on && sort.dir === 'asc' ? 'arrow-up' : 'arrow-down', size: 12, className: 'alt-bridge-vendas__ar' })));
          }))),
          h('tbody', null, list.map(function (r) {
            return h('tr', { key: r.unidade }, COLS.map(function (c) { return h(c.t === 'text' ? 'th' : 'td', { key: c.k, scope: c.t === 'text' ? 'row' : null, className: cx(c.t === 'text' ? 'alt-bridge-vendas__first' : null, c.strong ? 'alt-bridge-vendas__strong' : null) }, cell(c, r)); }));
          })),
          h('tfoot', null, h('tr', null, COLS.map(function (c) {
            return h(c.t === 'text' ? 'th' : 'td', { key: c.k, scope: c.t === 'text' ? 'row' : null, className: c.t === 'text' ? 'alt-bridge-vendas__first' : null }, cell(c, tot));
          }))))),
      h('p', { className: 'alt-bridge-vendas__note' }, 'Totais de R$/t por média ponderada pelo volume. Preço = Δ R$/t × volume de comparação; Volume = Δ t × R$/t base.'));
  }
  A.BridgeVendas = BridgeVendas;
})();


/* ---- Button ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, cx = K.cx, omit = K.omit;

  /* Superset of the legacy CTA Button: same classes (alt-btn, alt-btn--primary|secondary, alt-btn--sm|lg),
     same arrow rule and href behaviour, plus app variants, icons, loading, disabled, icon-only and full width. */
  var OWN = ['variant', 'size', 'arrow', 'className', 'children', 'startIcon', 'endIcon', 'icon', 'loading', 'fullWidth', 'loadingLabel'];

  function Arrow(size) {
    return h('svg', { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2.2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true' }, h('path', { d: 'M5 12h14M13 6l6 6-6 6' }));
  }
  function Spinner(size) {
    return h('svg', { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2.4, strokeLinecap: 'round', className: 'alt-btn__spin', 'aria-hidden': 'true' },
      h('path', { d: 'M21 12a9 9 0 1 1-9-9', opacity: 1 }));
  }
  function ico(v, size) {
    if (!v) return null;
    if (typeof v === 'string') return h(A.Icon, { name: v, size: size, className: 'alt-btn__icon' });
    return v;
  }

  function Button(p) {
    var variant = p.variant || 'primary', size = p.size || 'md';
    var iconOnly = !!p.icon && (p.children === undefined || p.children === null);
    var isz = size === 'lg' ? 17 : size === 'xs' ? 14 : 16;
    var arrow = p.arrow === undefined ? (variant === 'primary' && size !== 'sm' && size !== 'xs' && !iconOnly && !p.endIcon && !p.loading) : p.arrow;
    var disabled = !!p.disabled || !!p.loading;
    var isLink = !!p.href;
    var rest = omit(p, OWN);
    var props = Object.assign(rest, {
      className: cx('alt-btn', 'alt-btn--' + variant, size !== 'md' && 'alt-btn--' + size,
        iconOnly && 'alt-btn--icon', p.fullWidth && 'alt-btn--block', p.loading && 'alt-btn--loading', p.className),
      type: isLink ? undefined : (p.type || 'button')
    });
    if (p.loading) props['aria-busy'] = 'true';
    if (isLink && disabled) {
      props.href = undefined; props['aria-disabled'] = 'true'; props.tabIndex = -1; props.role = 'link';
      props.onClick = function (e) { e.preventDefault(); };
    } else if (!isLink) {
      props.disabled = disabled;
    }
    var lead = p.loading ? Spinner(isz) : ico(iconOnly ? p.icon : p.startIcon, isz);
    var label = p.loading && p.loadingLabel ? p.loadingLabel : p.children;
    return h(isLink ? 'a' : 'button', props,
      lead,
      !iconOnly ? label : null,
      !iconOnly ? ico(p.endIcon, isz) : null,
      arrow && !iconOnly ? Arrow(size === 'lg' ? 17 : 16) : null);
  }

  A.Button = Button;
})();


/* ---- CampoBusca ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  /* Parse "ctrl+k" / "alt+n" / "/" into a matcher. ctrl also matches cmd (meta). */
  function parseShortcut(s) {
    var parts = String(s || '').toLowerCase().split('+');
    var key = parts.pop();
    return { ctrl: parts.indexOf('ctrl') >= 0, alt: parts.indexOf('alt') >= 0, shift: parts.indexOf('shift') >= 0, key: key };
  }
  function isEditable(el) {
    if (!el) return false;
    var t = (el.tagName || '').toLowerCase();
    return t === 'input' || t === 'textarea' || t === 'select' || el.isContentEditable;
  }
  function matches(e, sc) {
    if (!!sc.ctrl !== !!(e.ctrlKey || e.metaKey)) return false;
    if (!!sc.alt !== !!e.altKey) return false;
    if (sc.shift && !e.shiftKey) return false;
    var k = (e.key || '').toLowerCase();
    if (k === sc.key) return true;
    /* alt+letter changes e.key on macOS; fall back to the physical key */
    return sc.key.length === 1 && /[a-z]/.test(sc.key) && e.code === 'Key' + sc.key.toUpperCase();
  }

  /* useShortcut('ctrl+k', fn): registers on mount, removes on unmount. Bare keys ("/") ignore typing in fields. */
  function useShortcut(shortcut, fn, enabled) {
    var ref = R.useRef(fn); ref.current = fn;
    R.useEffect(function () {
      if (!shortcut || enabled === false) return undefined;
      var sc = parseShortcut(shortcut);
      function onKey(e) {
        if (!sc.ctrl && !sc.alt && isEditable(e.target)) return;
        if (matches(e, sc)) { e.preventDefault(); ref.current(e); }
      }
      window.addEventListener('keydown', onKey);
      return function () { window.removeEventListener('keydown', onKey); };
    }, [shortcut, enabled]);
  }

  /* Key indicator ("ctrl+k", "alt+n"). tone="onAccent" inside a teal-light button. */
  function ShortcutIndicator(p) {
    return h('kbd', {
      className: cx('alt-campo-busca__kbd', p.tone === 'onAccent' && 'alt-campo-busca__kbd--on-accent', p.className),
      onMouseDown: p.onClick ? function (e) { e.preventDefault(); } : undefined,
      onClick: p.onClick, title: p.title, 'aria-hidden': p.onClick ? undefined : 'true'
    }, p.shortcut || p.children);
  }

  function CampoBusca(p) {
    var controlled = p.value !== undefined;
    var st = R.useState(p.defaultValue || ''); var inner = st[0], setInner = st[1];
    var value = controlled ? p.value : inner;
    var localRef = R.useRef(null);
    var inputRef = p.inputRef || localRef;
    var shortcut = p.shortcut === undefined ? 'ctrl+k' : p.shortcut;
    var label = p.label === undefined ? 'Pesquisar' : p.label;
    var idRef = R.useRef('alt-cb-' + Math.random().toString(36).slice(2, 8));
    function focus() { if (inputRef.current) { inputRef.current.focus(); inputRef.current.select(); } }
    useShortcut(shortcut, focus, !p.disabled);

    function change(v, e) { if (!controlled) setInner(v); if (p.onChange) p.onChange(v, e); }
    function clear() { change(''); if (p.onClear) p.onClear(); if (inputRef.current) inputRef.current.focus(); }

    var w = p.width === undefined ? 400 : p.width;
    var ctl = h('div', { className: cx('alt-ctl', 'alt-campo-busca__ctl', p.size === 'md' && 'alt-campo-busca__ctl--md', p.disabled && 'alt-ctl--disabled') },
      h(A.Icon, { name: 'search', size: 16 }),
      h('input', {
        id: idRef.current, ref: inputRef, type: 'search', value: value, disabled: p.disabled,
        placeholder: p.placeholder === undefined ? 'Nome da rota ou descrição' : p.placeholder,
        'aria-label': label ? undefined : (p.ariaLabel || p.placeholder || 'Pesquisar'),
        'aria-keyshortcuts': shortcut ? shortcut.replace('ctrl', 'Control').replace('alt', 'Alt') : undefined,
        onChange: function (e) { change(e.target.value, e); },
        onKeyDown: function (e) {
          if (e.key === 'Escape' && value) { e.preventDefault(); clear(); }
          if (p.onKeyDown) p.onKeyDown(e);
        }
      }),
      value
        ? h('button', { type: 'button', className: 'alt-campo-busca__clear', 'aria-label': 'Limpar busca', onClick: clear }, h(A.Icon, { name: 'x', size: 14 }))
        : (shortcut ? h(ShortcutIndicator, { shortcut: shortcut, onClick: focus, title: 'Focar a busca (' + shortcut + ')' }) : null));

    return h('div', { className: cx('alt-campo-busca', p.className), style: Object.assign({ width: typeof w === 'number' ? w + 'px' : w }, p.style) },
      label ? h('label', { className: 'alt-field__label', htmlFor: idRef.current }, label) : null,
      ctl);
  }

  CampoBusca.ShortcutIndicator = ShortcutIndicator;
  CampoBusca.useShortcut = useShortcut;
  A.CampoBusca = CampoBusca;
})();


/* ---- CardBase ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var Icon = A.Icon;
  var STATUS = { error: 'Crítico', warning: 'Atenção', success: 'Ok', info: 'Info' };

  function readPref(name) {
    try { var v = window.localStorage.getItem('alt-card-base:' + name); return v === null ? null : v === '1'; } catch (e) { return null; }
  }
  function writePref(name, v) {
    try { window.localStorage.setItem('alt-card-base:' + name, v ? '1' : '0'); } catch (e) { /* sem storage */ }
  }

  function CardBase(p) {
    var color = p.color || 'default';
    var showStatus = STATUS[color] !== undefined;
    var t = showStatus ? K.tone(color) : null;
    var persist = p.savePreferences && p.name;
    var ms = R.useState(function () {
      var saved = persist ? readPref(p.name) : null;
      return saved !== null ? saved : !!p.defaultMinimized;
    });
    var minimized = p.minimized !== undefined ? p.minimized : ms[0];
    var id = R.useRef('alt-card-base-' + Math.random().toString(36).slice(2, 8)).current;

    function toggle() {
      var n = !minimized; ms[1](n);
      if (persist) writePref(p.name, n);
      if (p.onMinimizeChange) p.onMinimizeChange(n);
    }

    var title = p.title !== undefined ? p.title : 'Viagens em andamento';
    var body = p.children !== undefined ? p.children : h(R.Fragment, null,
      h(CardBase.Value, null, '128'), h(CardBase.Caption, null, 'Atualizado há 2 min'));

    return h('section', {
      className: cx('alt-card-base alt-glass', minimized && 'is-minimized', p.className),
      style: Object.assign({ height: minimized ? 'auto' : p.height }, p.style),
      'aria-busy': p.loading ? 'true' : undefined, 'aria-labelledby': id + '-t'
    },
      h('header', { className: 'alt-card-base__head' },
        showStatus ? h('span', { className: 'alt-card-base__status', style: { color: t.c }, title: p.statusLabel || STATUS[color] },
          h(Icon, { name: t.icon, size: 15, title: p.statusLabel || STATUS[color] })) : null,
        h('h3', { id: id + '-t', className: 'alt-card-base__title' }, title),
        p.headerActions || null,
        p.minimize ? h('button', {
          type: 'button', className: 'alt-card-base__icon-btn', onClick: toggle,
          'aria-expanded': !minimized, 'aria-controls': id + '-b', 'aria-label': minimized ? 'Expandir' : 'Minimizar'
        }, h(Icon, { name: minimized ? 'chevron-down' : 'chevron-up', size: 16 })) : null),
      p.loading ? h('div', { className: 'alt-card-base__progress', role: 'progressbar', 'aria-label': 'Carregando' }) : null,
      h('div', { id: id + '-b', className: 'alt-card-base__collapse', 'aria-hidden': minimized ? 'true' : undefined },
        h('div', { className: 'alt-card-base__inner' },
          h('div', { className: 'alt-card-base__body' }, body),
          p.actions ? h('div', { className: 'alt-card-base__actions' }, p.actions) : null)));
  }

  /* Peças de conteúdo recomendadas. */
  CardBase.Value = function (p) { return h('div', { className: 'alt-card-base__value alt-num' }, p.children); };
  CardBase.Caption = function (p) { return h('div', { className: 'alt-card-base__caption' }, p.children); };
  CardBase.Delta = function (p) {
    var t = K.tone(p.tone || 'success');
    return h('span', { className: 'alt-card-base__delta alt-num', style: { color: t.c } },
      h(Icon, { name: p.icon || (p.direction === 'down' ? 'trend-down' : 'trend-up'), size: 14 }), p.children);
  };
  CardBase.Action = function (p) {
    return h('button', { type: 'button', className: 'alt-card-base__action', onClick: p.onClick },
      p.children, h(Icon, { name: p.icon || 'chevron-right', size: 14 }));
  };
  CardBase.IconButton = function (p) {
    return h('button', { type: 'button', className: 'alt-card-base__icon-btn', onClick: p.onClick, 'aria-label': p.label, title: p.label },
      h(Icon, { name: p.icon || 'more', size: 16 }));
  };

  A.CardBase = CardBase;
})();


/* ---- CcoAcoesLinha ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  /* "macros" glyph (keyboard) is not in the kit: inline svg, same stroke style */
  function MacroIcon(p) {
    var s = p.size || 16;
    return h('svg', { width: s, height: s, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      h('rect', { x: 3, y: 6, width: 18, height: 12, rx: 2 }), h('path', { d: 'M7 10h.01M11 10h.01M15 10h.01M7 14h10' }));
  }
  function icon(name, size) {
    if (name === 'macros') return h(MacroIcon, { size: size });
    if (typeof name === 'string') return h(A.Icon, { name: name, size: size });
    return name;
  }

  var DEFAULT_ACTIONS = [
    { id: 'rastreamento', label: 'Rastreamento', icon: 'pin' },
    { id: 'mensagens', label: 'Mensagens', icon: 'message', count: 3 },
    { id: 'violacoes', label: 'Violações', icon: 'shield', count: 2, tone: 'error' },
    { id: 'macros', label: 'Macros', icon: 'macros', count: 0, disabledHint: 'Sem macros' },
    { id: 'reprocessar', label: 'Reprocessar', icon: 'refresh' }
  ];
  var DEFAULT_MENU = [
    { id: 'editar-tempos', label: 'Editar tempos', icon: 'clock' },
    { id: 'alterar-status', label: 'Alterar status', icon: 'edit', children: [
      { id: 'X', label: 'Cancelado (X)', tone: 'error' },
      { id: 'T', label: 'Finalizado pelo sistema (T)', tone: 'warning' }
    ] },
    { id: 'rastreamento', label: 'Rastreamento', icon: 'pin' },
    { id: 'reprocessar', label: 'Reprocessar', icon: 'refresh' }
  ];
  var LOCKED = ['F', 'X', 'T'];

  function CcoAcoesLinha(p) {
    var actions = p.actions || DEFAULT_ACTIONS;
    var menu = p.menuItems || DEFAULT_MENU;
    var locked = p.disabled || (p.status && LOCKED.indexOf(p.status) >= 0);
    var lockedHint = p.disabledHint || 'Viagem encerrada (status ' + (p.status || '') + ')';

    var mo = R.useState(null), menuPos = mo[0], setMenuPos = mo[1];
    var so = R.useState(p.defaultSubmenu || null), sub = so[0], setSub = so[1];
    var timer = R.useRef(null), root = R.useRef(null), menuRef = R.useRef(null);

    R.useEffect(function () {
      if (!menuPos) return undefined;
      function onDoc(e) { if (root.current && !root.current.contains(e.target) && !(menuRef.current && menuRef.current.contains(e.target))) close(); }
      function onKey(e) { if (e.key === 'Escape') close(); }
      document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey);
      return function () { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); };
    }, [menuPos]);
    R.useEffect(function () { return function () { clearTimeout(timer.current); }; }, []);
    R.useLayoutEffect(function () {
      if (p.defaultMenuOpen && root.current) { var r = root.current.getBoundingClientRect(); setMenuPos({ x: r.left, y: r.bottom + 6 }); }
    }, []);
    R.useEffect(function () {
      if (!menuPos) return undefined;
      function onScroll(e) { if (!menuRef.current || !menuRef.current.contains(e.target)) close(); }
      window.addEventListener('scroll', onScroll, true);
      return function () { window.removeEventListener('scroll', onScroll, true); };
    }, [menuPos]);

    function close() { setMenuPos(null); setSub(null); }
    function openAt(e) {
      if (locked || p.menu === false) return;
      e.preventDefault();
      setMenuPos({ x: e.clientX, y: e.clientY }); setSub(null);
    }
    function openFromButton(e) {
      var b = e.currentTarget.getBoundingClientRect();
      setMenuPos(menuPos ? null : { x: b.left, y: b.bottom + 4 }); setSub(null);
    }
    function enter(id) { clearTimeout(timer.current); setSub(id); }
    function leave() { clearTimeout(timer.current); timer.current = setTimeout(function () { setSub(null); }, 200); }
    function pick(id, parent) { if (p.onMenuSelect) p.onMenuSelect(id, parent); close(); }

    var buttons = p.loading
      ? actions.map(function (a) { return h('span', { key: a.id, className: 'alt-skel alt-cco-acoes-linha__skel', 'aria-hidden': 'true' }); })
      : actions.map(function (a) {
        var empty = a.count === 0 && a.disabledHint;
        var dis = locked || a.disabled || empty;
        var hint = locked ? lockedHint : (a.disabled || empty) ? (a.disabledHint || a.label + ' indisponível') : a.label;
        var badge = a.count > 0 ? h('span', { className: cx('alt-cco-acoes-linha__badge', a.tone === 'error' && 'alt-cco-acoes-linha__badge--error'), 'aria-hidden': 'true' }, a.count > 99 ? '99+' : a.count) : null;
        return h('button', {
          key: a.id, type: 'button', className: 'alt-cco-acoes-linha__btn', 'data-tip': hint,
          'aria-label': a.label + (a.count > 0 ? ', ' + a.count + (a.tone === 'error' ? ' ativas' : ' novas') : '') + (dis ? ' — ' + hint : ''),
          'aria-disabled': dis ? 'true' : undefined,
          onClick: function () { if (!dis && p.onAction) p.onAction(a.id); }
        }, icon(a.icon, 16), badge);
      });

    var menuEl = menuPos ? h('div', { ref: menuRef, className: 'alt-cco-acoes-linha__menuwrap', style: { left: Math.max(8, Math.min(menuPos.x, window.innerWidth - 480)), top: menuPos.y } },
      h('div', { className: 'alt-popover alt-cco-acoes-linha__menu', role: 'menu', 'aria-label': 'Ações da viagem' },
        menu.map(function (m) {
          var has = m.children && m.children.length;
          return h('button', {
            key: m.id, type: 'button', role: 'menuitem', className: 'alt-menu-item alt-cco-acoes-linha__mi',
            'data-active': sub === m.id ? 'true' : undefined, 'aria-haspopup': has ? 'menu' : undefined, 'aria-expanded': has ? sub === m.id : undefined,
            onMouseEnter: function () { if (has) enter(m.id); else leave(); },
            onMouseLeave: has ? leave : undefined,
            onFocus: function () { if (has) enter(m.id); },
            onKeyDown: function (e) { if (has && e.key === 'ArrowRight') enter(m.id); if (e.key === 'ArrowLeft') setSub(null); },
            onClick: function () { if (has) enter(m.id); else pick(m.id); }
          }, m.icon ? icon(m.icon, 16) : null, h('span', { className: 'alt-cco-acoes-linha__ml' }, m.label), has ? h(A.Icon, { name: 'chevron-right', size: 14, className: 'alt-cco-acoes-linha__chev' }) : null);
        })),
      menu.filter(function (m) { return m.id === sub && m.children; }).map(function (m) {
        var idx = menu.indexOf(m);
        return h('div', { key: m.id, className: 'alt-popover alt-cco-acoes-linha__menu alt-cco-acoes-linha__sub', role: 'menu', 'aria-label': m.label, style: { top: 6 + idx * 36 }, onMouseEnter: function () { enter(m.id); }, onMouseLeave: leave },
          m.children.map(function (c) {
            var t = tone(c.tone);
            return h('button', { key: c.id, type: 'button', role: 'menuitem', className: 'alt-menu-item alt-cco-acoes-linha__mi', onClick: function () { pick(c.id, m.id); } },
              h('span', { className: 'alt-dot', style: { color: t.c } }), h('span', { className: 'alt-cco-acoes-linha__ml' }, c.label));
          }));
      })) : null;

    return h('div', { ref: root, className: cx('alt-cco-acoes-linha', locked && 'alt-cco-acoes-linha--locked', p.className), style: p.style, onContextMenu: openAt },
      h('div', { className: 'alt-cco-acoes-linha__row' },
        p.label ? h('span', { className: 'alt-cco-acoes-linha__label alt-num' }, p.label) : null,
        h('div', { className: 'alt-cco-acoes-linha__acts', role: 'toolbar', 'aria-label': 'Ações rápidas' + (p.label ? ' de ' + p.label : ''), 'aria-busy': p.loading ? 'true' : undefined }, buttons),
        p.menu === false || p.loading ? null : h('button', { type: 'button', className: 'alt-cco-acoes-linha__btn', 'data-tip': locked ? lockedHint : 'Mais ações', 'aria-label': 'Mais ações', 'aria-haspopup': 'menu', 'aria-expanded': !!menuPos, 'aria-disabled': locked ? 'true' : undefined, onClick: function (e) { if (!locked) openFromButton(e); } }, h(A.Icon, { name: 'more-v', size: 16 }))),
      menuEl && window.ReactDOM && window.ReactDOM.createPortal ? window.ReactDOM.createPortal(menuEl, document.body) : menuEl);
  }
  A.CcoAcoesLinha = CcoAcoesLinha;
})();


/* ---- CcoCelulas ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, cx = K.cx;

  /* Chegada / ETA: hour in 13px 600 + "chegou" (success soft) or "ETA" (neutral outline). */
  function ChegadaEta(p) {
    if (p.cancelada || !p.horario) {
      return h('span', { className: 'alt-cco-celulas__eta alt-cco-celulas__eta--vazio', 'aria-label': p.cancelada ? 'Sem chegada: viagem cancelada' : 'Sem horário' }, '—');
    }
    var chegou = !!p.chegou;
    return h('span', { className: cx('alt-cco-celulas__eta alt-num', p.className), 'data-tip': p.tooltip || null, tabIndex: p.tooltip ? 0 : undefined, 'aria-label': (chegou ? 'Chegou às ' : 'Previsão de chegada ') + p.horario + (p.tooltip ? '. ' + p.tooltip : '') },
      h('b', { className: 'alt-cco-celulas__hora' }, p.horario),
      chegou
        ? h('span', { className: 'alt-chip alt-cco-celulas__tag alt-cco-celulas__tag--ok' }, h(A.Icon, { name: 'check', size: 12, strokeWidth: 2.4 }), p.chegouLabel || 'chegou')
        : h('span', { className: 'alt-chip alt-cco-celulas__tag alt-cco-celulas__tag--eta' }, p.etaLabel || 'ETA'));
  }

  /* Progress to the consignee: 6px bar + % at the right, no animated stripes. */
  function Progresso(p) {
    var v = Math.max(0, Math.min(100, Number(p.value) || 0));
    if (p.cancelada) {
      return h('div', { className: cx('alt-cco-celulas__pg alt-cco-celulas__pg--cancel', p.className) },
        h('div', { className: 'alt-cco-celulas__track', 'aria-hidden': 'true' }),
        h('span', { className: 'alt-cco-celulas__pct alt-cco-celulas__pct--cancel' }, h(A.Icon, { name: 'x-circle', size: 13 }), p.canceladaLabel || 'Cancelada'));
    }
    return h('div', { className: cx('alt-cco-celulas__pg', v >= 100 && 'alt-cco-celulas__pg--done', p.className), role: 'progressbar', 'aria-valuemin': 0, 'aria-valuemax': 100, 'aria-valuenow': v, 'aria-label': p.label || 'Progresso até o destinatário' },
      h('div', { className: 'alt-cco-celulas__track' }, h('i', { style: { width: v + '%' } })),
      h('span', { className: 'alt-cco-celulas__pct alt-num' }, Math.round(v) + '%'));
  }

  var DEFAULT_ROWS = [
    { id: 1, placa: 'ABC-1D23', horario: '14:20', chegou: true, progresso: 100, tooltip: 'Chegou ao destinatário às 14:20 (Vidroporto · PF)' },
    { id: 2, placa: 'QRT-4F56', horario: '17:45', chegou: false, progresso: 62, tooltip: 'ETA calculado pela posição de 16:02' },
    { id: 3, placa: 'MNO-2B71', horario: '19:10', chegou: false, progresso: 18, tooltip: 'ETA calculado pela posição de 16:05' },
    { id: 4, placa: 'JHK-7G89', cancelada: true }
  ];

  /* Demo table with both cells; the cells are exported as CcoCelulas.ChegadaEta and CcoCelulas.Progresso. */
  function CcoCelulas(p) {
    var rows = p.rows || DEFAULT_ROWS;
    var cols = p.columns || ['Placa', 'Chegada / ETA', 'Progresso até o destinatário'];
    return h('div', { className: cx('alt-cco-celulas alt-panel', p.className), style: p.style },
      h('table', { className: 'alt-cco-celulas__table' },
        h('thead', null, h('tr', null, cols.map(function (c) { return h('th', { key: c, scope: 'col' }, c); }))),
        h('tbody', null, rows.map(function (r) {
          return h('tr', { key: r.id },
            h('td', { className: 'alt-num' }, r.placa),
            h('td', null, h(ChegadaEta, { horario: r.horario, chegou: r.chegou, cancelada: r.cancelada, tooltip: r.tooltip })),
            h('td', null, h(Progresso, { value: r.progresso, cancelada: r.cancelada })));
        }))));
  }
  CcoCelulas.ChegadaEta = ChegadaEta;
  CcoCelulas.Progresso = Progresso;
  A.CcoCelulas = CcoCelulas;
})();


/* ---- CcoEntrega ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  var COLORS = { remetente: 'var(--signal-info)', trajeto: 'rgba(255,255,255,.34)', destinatario: 'var(--signal-success)' };

  var DEFAULT_ETAPAS = [
    { id: 'remetente', label: 'Remetente', duracao: '1h40', peso: 18, done: true, inicio: '08:10', fim: '09:50', destaque: { de: 0.22, ate: 0.85, label: 'Carga 08:32 → 09:35' } },
    { id: 'trajeto', label: 'Trajeto', duracao: '3h05', peso: 44, current: true, progresso: 0.7, inicio: '09:50', fim: 'em curso' },
    { id: 'destinatario', label: 'Destinatário', duracao: null, peso: 20, inicio: 'ETA 14:20' }
  ];
  var DEFAULT_BLOCOS = [
    { id: 'remetente', label: 'Remetente', items: [['Entrada', '08:10'], ['Início carga', '08:32'], ['Final carga', '09:35'], ['Saída', '09:50']] },
    { id: 'trajeto', label: 'Trajeto', items: [['Km rota', '168'], ['Km rod.', '121'], ['Km dif.', '−47'], ['Peso (t)', '27,4'], ['Km/l', '2,31']] },
    { id: 'destinatario', label: 'Destinatário', items: [['Chegada', 'N/D'], ['Início desc.', 'N/D'], ['Final desc.', 'N/D'], ['Saída', 'N/D']] }
  ];

  function CcoEntrega(p) {
    var etapas = p.etapas || DEFAULT_ETAPAS;
    var blocos = p.blocos || DEFAULT_BLOCOS;
    var st = p.status || { label: 'Em trajeto', tone: 'info' };
    var hv = R.useState(null), hover = hv[0], setHover = hv[1];
    var total = etapas.reduce(function (s, e) { return s + (e.peso || 1); }, 0);
    var t = tone(st.tone);

    return h('section', { className: cx('alt-cco-entrega alt-glass', p.className), style: p.style, 'aria-label': p.title || 'Entrega' },
      h('header', { className: 'alt-cco-entrega__hd' },
        h('h3', { className: 'alt-cco-entrega__tt' }, p.title || 'Entrega · Viagem 88213'),
        h('span', { className: 'alt-chip', style: { background: t.bg, color: t.c } }, h(A.Icon, { name: st.icon || 'truck', size: 13 }), st.label)),
      h('div', { className: 'alt-cco-entrega__bd' },
        h('div', { className: 'alt-cco-entrega__bar', role: 'list', 'aria-label': 'Etapas da entrega' }, etapas.map(function (e) {
          var c = COLORS[e.id] || 'var(--muted)';
          var pending = !e.done && !e.current;
          var desc = e.label + (e.inicio ? ' · ' + e.inicio + (e.fim ? ' → ' + e.fim : '') : '') + (e.duracao ? ' (' + e.duracao + ')' : '') + (e.destaque ? ' · ' + e.destaque.label : '');
          return h('div', {
            key: e.id, role: 'listitem', tabIndex: 0, 'aria-label': desc,
            className: cx('alt-cco-entrega__seg', pending && 'alt-cco-entrega__seg--pending', hover === e.id && 'alt-cco-entrega__seg--hover'),
            style: { flex: (e.peso || 1) + ' 1 0', '--c': c },
            onMouseEnter: function () { setHover(e.id); }, onMouseLeave: function () { setHover(null); },
            onFocus: function () { setHover(e.id); }, onBlur: function () { setHover(null); },
            onClick: function () { if (p.onSelectEtapa) p.onSelectEtapa(e.id); }
          },
            h('span', { className: 'alt-cco-entrega__fill', style: { width: e.current ? Math.round((e.progresso || 0) * 100) + '%' : e.done ? '100%' : '0%' } }),
            e.destaque ? h('span', { className: 'alt-cco-entrega__strong', style: { left: e.destaque.de * 100 + '%', width: (e.destaque.ate - e.destaque.de) * 100 + '%' } }) : null,
            e.current ? h('span', { className: 'alt-cco-entrega__pulse', style: { left: Math.round((e.progresso || 0) * 100) + '%' }, 'aria-hidden': 'true' }) : null,
            hover === e.id ? h('span', { className: 'alt-cco-entrega__tip alt-num', role: 'tooltip' }, desc) : null);
        })),
        h('div', { className: 'alt-cco-entrega__leg alt-num' }, etapas.map(function (e) {
          return h('span', { key: e.id, style: { flex: (e.peso || 1) + ' 1 0' } }, e.label, ' ', h('b', null, e.duracao || '—'));
        })),
        h('div', { className: 'alt-cco-entrega__blk alt-num' }, blocos.map(function (b) {
          return h('div', { key: b.id, className: cx('alt-cco-entrega__col', hover === b.id && 'alt-cco-entrega__col--hover') },
            h('h4', null, h('span', { className: 'alt-dot', style: { color: COLORS[b.id] || 'var(--muted)' } }), b.label),
            h('dl', { className: 'alt-cco-entrega__kv' }, b.items.map(function (kv, i) {
              return [h('dt', { key: 'k' + i }, kv[0]), h('dd', { key: 'v' + i, className: kv[1] === 'N/D' ? 'alt-cco-entrega__nd' : null }, kv[1])];
            })));
        }))));
  }
  A.CcoEntrega = CcoEntrega;
})();


/* ---- CcoEstadia ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone, nf = K.nf;

  var STATUS = [
    { id: 'pendente', label: 'Pendentes', one: 'Pendente', tone: 'warning', icon: 'clock' },
    { id: 'aprovada', label: 'Aprovadas', one: 'Aprovada', tone: 'success', icon: 'check-circle' },
    { id: 'rejeitada', label: 'Rejeitadas', one: 'Rejeitada', tone: 'error', icon: 'x-circle' },
    { id: 'cobrada', label: 'Cobradas', one: 'Cobrada', tone: 'info', icon: 'dollar' }
  ];
  var ORIGENS = [{ id: 'todos', label: 'Todos' }, { id: 'remetente', label: 'Remetente' }, { id: 'destinatario', label: 'Destinatário' }];

  var DEFAULT_ROWS = [
    { id: 'e1', placa: 'ABC-1D23', motorista: 'João Pereira', local: 'Vidroporto · Porto Ferreira', origem: 'remetente', entrada: '08:10', saida: '11:40', permanencia: '3h30', valor: 480, status: 'pendente' },
    { id: 'e2', placa: 'QRT-4F56', motorista: 'Marta Souza', local: 'Cebrace · Jacareí', origem: 'destinatario', entrada: '09:05', saida: '13:20', permanencia: '4h15', valor: 620, status: 'pendente' },
    { id: 'e3', placa: 'MNO-2B71', motorista: 'Carlos Lima', local: 'Saint-Gobain · Canoas', origem: 'remetente', entrada: '07:30', saida: '10:55', permanencia: '3h25', valor: 540, status: 'pendente' },
    { id: 'e4', placa: 'JHK-7G89', motorista: 'Ana Ribeiro', local: 'Guardian · Porto Real', origem: 'destinatario', entrada: '10:15', saida: '13:45', permanencia: '3h30', valor: 500, status: 'pendente' },
    { id: 'e5', placa: 'PLX-3C40', motorista: 'Rafael Costa', local: 'AGC · Guaratinguetá', origem: 'remetente', entrada: '06:50', saida: '09:30', permanencia: '2h40', valor: 320, status: 'pendente' },
    { id: 'e6', placa: 'TRV-8K12', motorista: 'Luiz Andrade', local: 'Cebrace · Barra Velha', origem: 'destinatario', entrada: '05:40', saida: '09:10', permanencia: '3h30', valor: 500, status: 'aprovada' },
    { id: 'e7', placa: 'BRS-5H09', motorista: 'Paula Nunes', local: 'Vidroporto · Porto Ferreira', origem: 'remetente', entrada: '13:00', saida: '15:20', permanencia: '2h20', valor: 280, status: 'rejeitada' },
    { id: 'e8', placa: 'KLM-6J33', motorista: 'Diego Martins', local: 'Guardian · Tatuí', origem: 'destinatario', entrada: '11:10', saida: '16:00', permanencia: '4h50', valor: 760, status: 'cobrada' }
  ];
  function brl(v) { return 'R$ ' + nf(v, 2); }

  function Seg(p) {
    return h('div', { className: 'alt-cco-estadia__seg', role: 'radiogroup', 'aria-label': p.label },
      p.options.map(function (o) {
        var on = o.id === p.value;
        return h('button', { key: o.id, type: 'button', role: 'radio', 'aria-checked': on, className: 'alt-cco-estadia__segbtn', onClick: function () { p.onChange(o.id); } },
          o.label, o.count != null ? h('b', { className: 'alt-num' }, o.count) : null);
      }));
  }

  function CcoEstadia(p) {
    var rs = R.useState(p.rows || DEFAULT_ROWS), rows = p.rows && p.onChangeRows ? p.rows : rs[0], setRows = rs[1];
    var fs = R.useState(p.defaultStatus || 'pendente'), status = fs[0], setStatus = fs[1];
    var os = R.useState('todos'), origem = os[0], setOrigem = os[1];
    var ss = R.useState(p.defaultSelected || ['e1', 'e2', 'e3', 'e4']), sel = ss[0], setSel = ss[1];
    var ds = R.useState(p.defaultDrawer || null), drawer = ds[0], setDrawer = ds[1];
    var js = R.useState(''), just = js[0], setJust = js[1];
    var max = p.maxJustificativa || 1000, min = p.minJustificativa || 10;

    var counts = p.counts || STATUS.reduce(function (o, s) { o[s.id] = rows.filter(function (r) { return r.status === s.id; }).length; return o; }, {});
    var visible = rows.filter(function (r) { return r.status === status && (origem === 'todos' || r.origem === origem); });
    var selRows = rows.filter(function (r) { return sel.indexOf(r.id) >= 0; });
    var total = selRows.reduce(function (s, r) { return s + r.valor; }, 0);
    var allOn = visible.length > 0 && visible.every(function (r) { return sel.indexOf(r.id) >= 0; });
    var someOn = visible.some(function (r) { return sel.indexOf(r.id) >= 0; });
    var headRef = R.useRef(null);
    R.useEffect(function () { if (headRef.current) headRef.current.indeterminate = someOn && !allOn; });

    function toggle(id) { setSel(sel.indexOf(id) >= 0 ? sel.filter(function (x) { return x !== id; }) : sel.concat(id)); }
    function toggleAll() { var ids = visible.map(function (r) { return r.id; }); setSel(allOn ? sel.filter(function (x) { return ids.indexOf(x) < 0; }) : sel.concat(ids.filter(function (x) { return sel.indexOf(x) < 0; }))); }
    function apply(next, extra) {
      var ids = sel.slice();
      var nr = rows.map(function (r) { return ids.indexOf(r.id) >= 0 ? Object.assign({}, r, { status: next }) : r; });
      setRows(nr); if (p.onChangeRows) p.onChangeRows(nr);
      setSel([]); setDrawer(null); setJust('');
      var cb = { aprovada: p.onAprovar, rejeitada: p.onRejeitar, cobrada: p.onCobrar }[next];
      if (cb) cb(ids, extra);
    }
    function changeStatus(s) { setStatus(s); setSel([]); }

    var statusOpts = STATUS.map(function (s) { return { id: s.id, label: s.label, count: counts[s.id] }; });
    var dOk = just.trim().length >= min;

    return h('div', { className: cx('alt-cco-estadia', p.className), style: p.style },
      h('div', { className: 'alt-cco-estadia__bar' },
        h(Seg, { label: 'Status da estadia', options: statusOpts, value: status, onChange: changeStatus }),
        h(Seg, { label: 'Origem', options: ORIGENS, value: origem, onChange: setOrigem })),
      p.kpis === false ? null : A.CcoIndicadores ? h(A.CcoIndicadores, { layout: 'compact', items: p.kpis || [
        { id: 'det', label: 'Detectadas', value: String(rows.length) },
        { id: 'pend', label: 'Pendentes', value: String(counts.pendente || 0), tone: 'warning' },
        { id: 'apr', label: 'Aprovadas', value: String(counts.aprovada || 0), tone: 'success' },
        { id: 'valor', label: 'Valor total', value: brl(rows.reduce(function (s, r) { return s + r.valor; }, 0)) }], className: 'alt-cco-estadia__kpis' }) : null,
      h('div', { className: 'alt-panel alt-cco-estadia__panel' },
        sel.length ? h('div', { className: 'alt-cco-estadia__selbar alt-num', role: 'region', 'aria-label': 'Ações em lote' },
          h('b', null, sel.length + (sel.length === 1 ? ' selecionada' : ' selecionadas')),
          h('span', { className: 'alt-cco-estadia__muted' }, brl(total)),
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-cco-estadia__clear', onClick: function () { setSel([]); } }, 'Limpar seleção'),
          h('span', { className: 'alt-cco-estadia__sp' }),
          h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { setDrawer('rejeitar'); } }, h(A.Icon, { name: 'x', size: 15 }), 'Rejeitar'),
          h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { setDrawer('cobrar'); } }, h(A.Icon, { name: 'dollar', size: 15 }), 'Cobrar'),
          h('button', { type: 'button', className: 'alt-cco-estadia__primary', onClick: function () { apply('aprovada'); } }, h(A.Icon, { name: 'check', size: 15, strokeWidth: 2.4 }), 'Aprovar (' + sel.length + ')'))
          : h('div', { className: 'alt-cco-estadia__selbar alt-cco-estadia__selbar--idle' }, h('span', { className: 'alt-cco-estadia__muted' }, visible.length + ' estadias · selecione para aprovar, rejeitar ou cobrar')),
        h('div', { className: 'alt-scroll' }, h('table', { className: 'alt-cco-estadia__table alt-num' },
          h('thead', null, h('tr', null,
            h('th', { className: 'alt-cco-estadia__ck' }, h('input', { ref: headRef, type: 'checkbox', checked: allOn, onChange: toggleAll, 'aria-label': 'Selecionar todas' })),
            ['Placa · motorista', 'Local', 'Origem', 'Entrada – saída', 'Permanência', 'Valor', 'Status'].map(function (c) { return h('th', { key: c, scope: 'col', className: c === 'Valor' ? 'alt-cco-estadia__r' : null }, c); }))),
          h('tbody', null, visible.length ? visible.map(function (r) {
            var on = sel.indexOf(r.id) >= 0, s = STATUS.filter(function (x) { return x.id === r.status; })[0] || STATUS[0], t = tone(s.tone);
            return h('tr', { key: r.id, className: on ? 'alt-cco-estadia__on' : null, onClick: function (e) { if (e.target.tagName !== 'INPUT') toggle(r.id); } },
              h('td', { className: 'alt-cco-estadia__ck' }, h('input', { type: 'checkbox', checked: on, onChange: function () { toggle(r.id); }, 'aria-label': 'Selecionar ' + r.placa })),
              h('td', null, h('span', { className: 'alt-cco-estadia__strong' }, r.placa), h('span', { className: 'alt-cco-estadia__sub' }, r.motorista)), h('td', null, h('span', { className: 'alt-cco-estadia__local', title: r.local }, r.local)),
              h('td', null, r.origem === 'remetente' ? 'Remetente' : 'Destinatário'), h('td', null, r.entrada + ' – ' + r.saida), h('td', null, r.permanencia),
              h('td', { className: 'alt-cco-estadia__r alt-cco-estadia__strong' }, brl(r.valor)),
              h('td', null, h('span', { className: 'alt-chip', style: { background: t.bg, color: t.c } }, h(A.Icon, { name: s.icon, size: 13 }), s.one)));
          }) : h('tr', null, h('td', { colSpan: 8, className: 'alt-cco-estadia__empty' }, 'Nenhuma estadia neste filtro.')))))),
      drawer ? h('div', { className: 'alt-cco-estadia__scrim', onClick: function (e) { if (e.target === e.currentTarget) setDrawer(null); } },
        h('aside', { className: 'alt-glass alt-glass--overlay alt-cco-estadia__drawer', role: 'dialog', 'aria-modal': 'true', 'aria-label': 'Justificativa' },
          h('header', { className: 'alt-cco-estadia__dhd' },
            h('div', null, h('div', { className: 'alt-cco-estadia__eyebrow' }, drawer === 'rejeitar' ? 'Rejeitar estadias' : 'Cobrar estadias'),
              h('h3', null, sel.length + (sel.length === 1 ? ' estadia · ' : ' estadias · ') + brl(total))),
            h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Fechar', onClick: function () { setDrawer(null); } }, h(A.Icon, { name: 'x', size: 16 }))),
          h('label', { className: 'alt-field alt-cco-estadia__dbody' },
            h('span', { className: 'alt-field__label' }, 'Justificativa'),
            h('div', { className: 'alt-ctl alt-cco-estadia__ta' }, h('textarea', { rows: 6, maxLength: max, value: just, autoFocus: true, placeholder: drawer === 'rejeitar' ? 'Explique por que a estadia não procede' : 'Detalhe a cobrança ao cliente', onChange: function (e) { setJust(e.target.value); } })),
            h('span', { className: 'alt-field__help alt-cco-estadia__count alt-num' }, h('span', null, 'Mínimo de ' + min + ' caracteres'), h('span', null, just.length + '/' + max))),
          h('footer', { className: 'alt-cco-estadia__dft' },
            h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { setDrawer(null); } }, 'Cancelar'),
            h('button', { type: 'button', className: 'alt-cco-estadia__primary', disabled: !dOk, onClick: function () { apply(drawer === 'rejeitar' ? 'rejeitada' : 'cobrada', just.trim()); } }, drawer === 'rejeitar' ? 'Rejeitar' : 'Cobrar')))) : null);
  }
  A.CcoEstadia = CcoEstadia;
})();


/* ---- CcoIndicadores ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, cx = K.cx, tone = K.tone;

  var DEFAULT_ITEMS = [
    { id: 'espera-remet', label: 'Tempo méd. de espera - remet.', value: '1h12', tone: 'warning', sub: 'Esperando: 6' },
    { id: 'carga', label: 'Tempo médio de carga', value: '0h58', tone: 'info' },
    { id: 'trajeto', label: 'Tempo no trajeto', value: '3h24', tone: 'success', sub: 'Em trajeto: 31' },
    { id: 'espera-dest', label: 'Tempo méd. de espera - dest.', value: '0h47', tone: 'warning', sub: 'Esperando: 4' },
    { id: 'descarga', label: 'Tempo médio de descarga', value: '1h05', tone: 'info' },
    { id: 'totais', label: 'Totais', value: '58', sub: 'viagens no recorte' }
  ];

  /* Tiles for the control tower and Gestão. layout: default | compact | kpi (kpi = default). */
  function CcoIndicadores(p) {
    var items = p.items || DEFAULT_ITEMS;
    var layout = p.layout === 'compact' ? 'compact' : 'default';
    var clickable = !!p.onSelect;
    return h('div', { className: cx('alt-cco-indicadores', 'alt-cco-indicadores--' + layout, p.className), style: p.style, role: clickable ? 'group' : 'list', 'aria-label': p.ariaLabel || 'Indicadores' },
      items.map(function (it) {
        var t = it.tone ? tone(it.tone) : null;
        var sel = p.selected === it.id;
        var inner = [
          h('span', { key: 'l', className: 'alt-cco-indicadores__label' }, t ? h('span', { className: 'alt-dot', style: { color: t.c }, 'aria-hidden': 'true' }) : null, h('span', { className: 'alt-cco-indicadores__lt' }, it.label)),
          h('b', { key: 'v', className: 'alt-cco-indicadores__value alt-num' }, it.loading ? h('span', { className: 'alt-skel alt-cco-indicadores__skel' }) : it.value),
          it.sub && layout === 'default' ? h('span', { key: 's', className: 'alt-cco-indicadores__sub alt-num' }, it.sub) : null
        ];
        var props = { key: it.id, className: cx('alt-cco-indicadores__tile alt-glass', clickable && 'alt-cco-indicadores__tile--btn', sel && 'alt-cco-indicadores__tile--sel'), title: it.hint || undefined };
        if (clickable) {
          props.type = 'button'; props['aria-pressed'] = sel;
          props.onClick = function () { p.onSelect(sel ? null : it.id); };
          return h('button', props, inner);
        }
        props.role = 'listitem';
        return h('div', props, inner);
      }));
  }
  A.CcoIndicadores = CcoIndicadores;
})();


/* ---- CcoJornada ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  function hm(min) {
    var neg = min < 0, m = Math.abs(Math.round(min));
    return (neg ? '−' : '') + Math.floor(m / 60) + 'h' + ('0' + (m % 60)).slice(-2);
  }

  var DEFAULT_DIAS = [
    { id: 'd1', label: 'Dia 1', jornada: 480, extra: 70, refeicao: 60, descanso: 45, noturno: 0, espera: 50, movimento: 425 },
    { id: 'd2', label: 'Dia 2', jornada: 480, extra: 145, refeicao: 60, descanso: 45, noturno: 0, espera: 70, movimento: 450 }
  ];

  /* JORNADA_LIMITE_MIN = 600 (10h CLT). Scale goes to 12h. */
  function CcoJornada(p) {
    var dias = p.dias || DEFAULT_DIAS;
    var limite = p.limite || 600, escala = p.escala || 720;
    var ds = R.useState(p.defaultDia || dias[dias.length - 1].id), diaId = p.dia || ds[0];
    function setDia(id) { ds[1](id); if (p.onChangeDia) p.onChangeDia(id); }
    var d = dias.filter(function (x) { return x.id === diaId; })[0] || dias[0];
    var usado = d.jornada + d.extra, restante = limite - usado, excedida = restante < 0;
    var pct = function (v) { return Math.max(0, Math.min(100, v / escala * 100)); };
    var ok = tone('success'), warn = tone('warning'), err = tone('error');

    var big = [
      { k: 'Jornada', v: hm(d.jornada), c: ok.c },
      { k: 'Extra', v: hm(d.extra), c: warn.c },
      { k: 'Restante', v: hm(restante), c: excedida ? err.c : 'var(--muted)', alert: excedida }
    ];
    var det = [['Refeição', d.refeicao], ['Descanso', d.descanso], ['Noturno', d.noturno], ['Espera', d.espera], ['Movimento', d.movimento]];

    return h('section', { className: cx('alt-cco-jornada alt-glass', p.className), style: p.style, 'aria-label': 'Jornada do motorista' },
      h('header', { className: 'alt-cco-jornada__hd' },
        h('h3', { className: 'alt-cco-jornada__tt' }, p.title || 'Jornada'),
        dias.length > 1 ? h('div', { className: 'alt-cco-jornada__seg', role: 'tablist', 'aria-label': 'Dia da jornada' }, dias.map(function (x) {
          return h('button', { key: x.id, type: 'button', role: 'tab', 'aria-selected': x.id === d.id, className: 'alt-cco-jornada__tab', onClick: function () { setDia(x.id); } }, x.label);
        })) : null),
      h('div', { className: 'alt-cco-jornada__bd alt-num', role: 'tabpanel' },
        h('div', { className: 'alt-cco-jornada__big' }, big.map(function (b) {
          return h('div', { key: b.k },
            h('span', { className: 'alt-cco-jornada__k' }, h('span', { className: 'alt-dot', style: { color: b.c } }), b.k),
            h('b', { className: cx('alt-cco-jornada__v', b.alert && 'alt-cco-jornada__v--alert') }, b.v));
        })),
        h('div', { className: 'alt-cco-jornada__barwrap' },
          h('div', { className: 'alt-cco-jornada__bar', role: 'img', 'aria-label': 'Jornada ' + hm(d.jornada) + ', extra ' + hm(d.extra) + ', limite ' + hm(limite) },
            h('i', { style: { width: pct(d.jornada) + '%', background: ok.c } }),
            h('i', { style: { width: pct(d.extra) + '%', background: warn.c } })),
          h('span', { className: 'alt-cco-jornada__lim', style: { left: pct(limite) + '%' }, 'aria-hidden': 'true' })),
        h('div', { className: 'alt-cco-jornada__scale', 'aria-hidden': 'true' },
          h('span', null, '0h'), h('span', { style: { left: pct(limite) + '%' }, className: 'alt-cco-jornada__limlbl' }, 'limite ' + Math.round(limite / 60) + 'h'), h('span', null, Math.round(escala / 60) + 'h')),
        excedida ? h('div', { className: 'alt-cco-jornada__alert' },
          h('span', { className: 'alt-chip', style: { background: err.bg, color: err.c } }, h(A.Icon, { name: 'alert', size: 13 }), 'Acima do limite CLT'),
          h('span', { className: 'alt-cco-jornada__hint' }, 'Excedeu ' + hm(-restante) + ' além de ' + Math.round(limite / 60) + 'h')) : null,
        h('dl', { className: 'alt-cco-jornada__kv' }, det.map(function (kv) {
          return h('div', { key: kv[0] }, h('dt', null, kv[0]), h('dd', null, hm(kv[1] || 0)));
        }))));
  }
  A.CcoJornada = CcoJornada;
})();


/* ---- CcoMacros ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  /* Families → chart tokens (início = full tone, fim = 60%). */
  var FAMILIAS = {
    jornada: { label: 'Jornada', c: 'var(--chart-4)' },
    carga: { label: 'Carga', c: 'var(--chart-2)' },
    carregado: { label: 'Carregado', c: 'var(--chart-1)' },
    refeicao: { label: 'Refeição', c: 'var(--chart-5)' },
    descarga: { label: 'Descarga', c: 'var(--chart-3)' },
    descanso: { label: 'Descanso', c: 'var(--chart-6)' }
  };

  var DEFAULT_EVENTOS = [
    { id: 'm1', familia: 'jornada', fase: 'inicio', hora: '06:40', label: 'Início de jornada' },
    { id: 'm2', familia: 'carga', fase: 'inicio', hora: '08:30', label: 'Início de carga' },
    { id: 'm3', familia: 'carga', fase: 'fim', hora: '09:50', label: 'Fim de carga' },
    { id: 'm4', familia: 'refeicao', fase: 'inicio', hora: '13:00', label: 'Início de refeição' },
    { id: 'm5', familia: 'refeicao', fase: 'fim', hora: '13:40', label: 'Fim de refeição' },
    { id: 'm6', familia: 'descarga', fase: 'inicio', hora: '17:30', label: 'Início de descarga' },
    { id: 'm7', familia: 'descarga', fase: 'fim', hora: '18:50', label: 'Fim de descarga' },
    { id: 'm8', familia: 'descanso', fase: 'inicio', hora: '21:00', label: 'Início de descanso' }
  ];
  var DEFAULT_VIOLACOES = [
    { id: 'v1', hora: '14:20', label: 'Direção contínua acima de 5h30' },
    { id: 'v2', hora: '20:00', label: 'Jornada acima de 10h' }
  ];
  var DEFAULT_DISTRIB = [
    { familia: 'carga', min: 80 }, { familia: 'carregado', min: 460 }, { familia: 'refeicao', min: 40 },
    { familia: 'descarga', min: 80 }, { familia: 'descanso', min: 120 }
  ];

  function toMin(t) { var a = String(t).split(':'); return (+a[0]) * 60 + (+a[1] || 0); }
  function fmt(m) { return ('0' + Math.floor(m / 60)).slice(-2) + ':' + ('0' + (m % 60)).slice(-2); }

  function CcoMacros(p) {
    var eventos = p.eventos || DEFAULT_EVENTOS, viol = p.violacoes || DEFAULT_VIOLACOES, dist = p.distribuicao || DEFAULT_DISTRIB;
    var ini = toMin(p.inicio || '06:00'), fim = toMin(p.fim || '22:00'), span = Math.max(1, fim - ini);
    var tip = R.useState(null), hover = tip[0], setHover = tip[1];
    var ss = R.useState(null), sel = ss[0], setSel = ss[1];
    var pos = function (t) { return Math.max(0, Math.min(100, (toMin(t) - ini) / span * 100)); };
    var ticks = []; for (var m = ini; m <= fim; m += Math.max(60, Math.round(span / 4 / 60) * 60)) ticks.push(m);
    var totalMin = dist.reduce(function (s, d) { return s + d.min; }, 0) || 1;
    var err = tone('error');

    function tipEl(id, x, text) {
      return hover === id ? h('span', { className: cx('alt-cco-macros__tip alt-num', x > 70 && 'alt-cco-macros__tip--r', x < 20 && 'alt-cco-macros__tip--l'), role: 'tooltip' }, text) : null;
    }

    return h('section', { className: cx('alt-cco-macros alt-glass', p.className), style: p.style, 'aria-label': 'Linha do tempo de macros' },
      h('header', { className: 'alt-cco-macros__hd' },
        h('h3', { className: 'alt-cco-macros__tt' }, p.title || 'Linha do tempo · macros'),
        viol.length ? h('span', { className: 'alt-chip', style: { background: err.bg, color: err.c } }, h(A.Icon, { name: 'alert', size: 13 }), viol.length + (viol.length === 1 ? ' violação' : ' violações'))
          : h('span', { className: 'alt-chip', style: { background: tone('success').bg, color: tone('success').c } }, h(A.Icon, { name: 'check-circle', size: 13 }), 'Sem violações')),
      h('div', { className: 'alt-cco-macros__bd' },
        h('div', { className: 'alt-cco-macros__tl', role: 'list', 'aria-label': 'Macros entre ' + fmt(ini) + ' e ' + fmt(fim) },
          h('span', { className: 'alt-cco-macros__line', 'aria-hidden': 'true' }),
          viol.map(function (v) {
            var x = pos(v.hora), text = v.label + ' · ' + v.hora;
            return h('span', { key: v.id, role: 'listitem', tabIndex: 0, className: 'alt-cco-macros__viol', style: { left: x + '%' }, 'aria-label': 'Violação: ' + text,
              onMouseEnter: function () { setHover(v.id); }, onMouseLeave: function () { setHover(null); }, onFocus: function () { setHover(v.id); }, onBlur: function () { setHover(null); } },
              h(A.Icon, { name: 'alert', size: 14 }), tipEl(v.id, x, 'Violação · ' + text));
          }),
          eventos.map(function (e) {
            var f = FAMILIAS[e.familia] || { label: e.familia, c: 'var(--muted)' }, x = pos(e.hora), text = e.label + ' · ' + e.hora;
            return h('button', { key: e.id, type: 'button', role: 'listitem', className: cx('alt-cco-macros__mk', e.fase === 'fim' && 'alt-cco-macros__mk--fim', sel === e.id && 'alt-cco-macros__mk--sel'),
              style: { left: x + '%', '--c': f.c }, 'aria-label': text, 'aria-pressed': sel === e.id,
              onMouseEnter: function () { setHover(e.id); }, onMouseLeave: function () { setHover(null); }, onFocus: function () { setHover(e.id); }, onBlur: function () { setHover(null); },
              onClick: function () { var n = sel === e.id ? null : e.id; setSel(n); if (p.onSelect) p.onSelect(n && e); } },
              tipEl(e.id, x, text));
          })),
        h('div', { className: 'alt-cco-macros__hr alt-num', 'aria-hidden': 'true' }, ticks.map(function (t) { return h('span', { key: t, style: { left: (t - ini) / span * 100 + '%' } }, fmt(t)); })),
        h('div', { className: 'alt-cco-macros__key' },
          h('span', null, h('i', { className: 'alt-cco-macros__kmk' }), 'Início'),
          h('span', null, h('i', { className: 'alt-cco-macros__kmk alt-cco-macros__kmk--fim' }), 'Fim'),
          h('span', null, h(A.Icon, { name: 'alert', size: 12, style: { color: 'var(--signal-error)' } }), 'Violação')),
        h('div', { className: 'alt-cco-macros__dlabel' }, 'Distribuição do tempo'),
        h('div', { className: 'alt-cco-macros__ds', role: 'img', 'aria-label': dist.map(function (d) { return (FAMILIAS[d.familia] || {}).label + ' ' + Math.round(d.min / totalMin * 100) + '%'; }).join(', ') },
          dist.map(function (d) { return h('i', { key: d.familia, style: { flex: d.min + ' 1 0', background: (FAMILIAS[d.familia] || {}).c } }); })),
        h('ul', { className: 'alt-cco-macros__lg alt-num' }, dist.map(function (d) {
          var f = FAMILIAS[d.familia] || { label: d.familia };
          return h('li', { key: d.familia }, h('span', { className: 'alt-dot', style: { color: f.c } }), f.label, h('b', null, Math.round(d.min / totalMin * 100) + '%'));
        }))));
  }
  CcoMacros.FAMILIAS = FAMILIAS;
  A.CcoMacros = CcoMacros;
})();


/* ---- CcoTelemetria ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  var DEFAULT_METRICAS = [
    { id: 'distancia', label: 'Distância percorrida', icon: 'route', value: '121 km', realizado: 121, referencia: 168, refLabel: 'rota 168 km' },
    { id: 'combustivel', label: 'Combustível consumido', icon: 'fuel', value: '52,4 l', realizado: 52.4, referencia: 58, refLabel: 'previsto 58 l' },
    { id: 'eficiencia', label: 'Eficiência média', icon: 'activity', value: '2,31 km/l', realizado: 2.31, referencia: 2.5, refLabel: 'meta 2,50 km/l' },
    { id: 'velocidade', label: 'Velocidade média', icon: 'gauge', value: '58 km/h', sub: 'máx. 87 km/h' },
    { id: 'parado', label: 'Tempo parado', icon: 'clock', value: '0h42', sub: 'ligado 0h18' },
    { id: 'paradas', label: 'Paradas no percurso', icon: 'pin', value: '3', sub: '1 fora de cerca' },
    { id: 'jornada', label: 'Balanço de jornada', icon: 'user', value: '−0h25', tone: 'error', sub: 'acima do limite' }
  ];
  var DEFAULT_PARADAS = [
    { id: 'p1', local: 'Posto Graal · Km 182, SP-330', inicio: '11:05', duracao: '0h22', tipo: { label: 'Abastecimento', tone: 'info' } },
    { id: 'p2', local: 'Restaurante Rota 116 · Lorena', inicio: '13:00', duracao: '0h40', tipo: { label: 'Refeição', tone: 'neutral' } },
    { id: 'p3', local: 'Acostamento · Km 64, BR-116', inicio: '15:12', duracao: '0h12', tipo: { label: 'Fora de cerca', tone: 'warning' } }
  ];
  var DEFAULT_FAIXA = [
    { label: '0–60 km/h', pct: 62, tone: 'success' }, { label: '60–80 km/h', pct: 18, tone: 'info' },
    { label: '80–90 km/h', pct: 14, tone: 'warning' }, { label: 'Acima de 90', pct: 6, tone: 'error' }
  ];
  var DEFAULT_TEMPO = [
    { label: 'Em movimento', pct: 55, c: 'var(--teal-light)' },
    { label: 'Ocioso ligado', pct: 30, c: 'rgba(94,234,212,.55)' },
    { label: 'Desligado', pct: 15, c: 'rgba(94,234,212,.25)' }
  ];

  function Stack(p) {
    return h('div', { className: 'alt-cco-telemetria__stack' },
      h('span', { className: 'alt-cco-telemetria__slabel' }, p.label),
      h('div', { className: 'alt-cco-telemetria__sbar', role: 'img', 'aria-label': p.label + ': ' + p.items.map(function (i) { return i.label + ' ' + i.pct + '%'; }).join(', ') },
        p.items.map(function (i) { return h('i', { key: i.label, style: { flex: i.pct + ' 1 0', background: i.c || tone(i.tone).c } }); })),
      h('ul', { className: 'alt-cco-telemetria__lg alt-num' }, p.items.map(function (i) {
        return h('li', { key: i.label }, h('span', { className: 'alt-dot', style: { color: i.c || tone(i.tone).c } }), i.label, h('b', null, i.pct + '%'));
      })));
  }

  function CcoTelemetria(p) {
    var met = p.metricas || DEFAULT_METRICAS, paradas = p.paradas || DEFAULT_PARADAS;
    var variant = p.variant || 'default';
    var ps = R.useState(null), sel = ps[0], setSel = ps[1];

    return h('section', { className: cx('alt-cco-telemetria alt-glass', 'alt-cco-telemetria--' + variant, p.className), style: p.style, 'aria-label': p.title || 'Pulso operacional da viagem' },
      h('header', { className: 'alt-cco-telemetria__hd' },
        h('div', { className: 'alt-cco-telemetria__ht' },
          h('h3', null, p.title || 'Pulso operacional da viagem'),
          variant === 'default' ? h('p', null, p.subtitle || 'Deslocamento, eficiência, paradas e jornada em uma única leitura') : null),
        p.viagem !== null ? h('span', { className: 'alt-chip alt-cco-telemetria__viagem alt-num' }, h(A.Icon, { name: 'truck', size: 13 }), p.viagem || 'Viagem 88213') : null),
      h('div', { className: 'alt-cco-telemetria__mg alt-num' }, met.map(function (m) {
        var t = m.tone ? tone(m.tone) : null;
        var frac = m.referencia ? Math.min(1, m.realizado / Math.max(m.realizado, m.referencia)) : null;
        return h('div', { key: m.id, className: 'alt-cco-telemetria__m' },
          h('span', { className: 'alt-cco-telemetria__ml' }, h(A.Icon, { name: m.icon, size: 14 }), m.label),
          h('b', { className: 'alt-cco-telemetria__mv', style: t ? { color: t.c } : null }, m.value),
          frac != null ? h('div', { className: 'alt-cco-telemetria__cmp', role: 'img', 'aria-label': 'Realizado ' + m.value + ', ' + m.refLabel },
            h('i', { style: { width: Math.round(frac * 100) + '%' } })) : null,
          m.refLabel || m.sub ? h('span', { className: 'alt-cco-telemetria__ms', style: t ? { color: t.c } : null }, t ? h(A.Icon, { name: t.icon, size: 12 }) : null, m.refLabel || m.sub) : null);
      })),
      variant === 'default' ? h('div', { className: 'alt-cco-telemetria__lower' },
        h('div', { className: 'alt-cco-telemetria__paradas' },
          h('div', { className: 'alt-cco-telemetria__sec' }, 'Paradas no percurso', h('span', { className: 'alt-num' }, paradas.length)),
          h('table', { className: 'alt-cco-telemetria__pt alt-num' },
            h('thead', null, h('tr', null, h('th', null, 'Local'), h('th', null, 'Início'), h('th', null, 'Duração'), h('th', null, 'Tipo'))),
            h('tbody', null, paradas.map(function (r) {
              var t = tone(r.tipo.tone), on = sel === r.id;
              return h('tr', { key: r.id, tabIndex: 0, 'aria-selected': on, className: on ? 'alt-cco-telemetria__on' : null,
                onClick: function () { var n = on ? null : r.id; setSel(n); if (p.onSelectParada) p.onSelectParada(n && r); },
                onKeyDown: function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); e.currentTarget.click(); } } },
                h('td', { className: 'alt-cco-telemetria__loc' }, r.local), h('td', null, r.inicio), h('td', null, r.duracao),
                h('td', null, h('span', { className: 'alt-chip', style: { background: t.bg, color: r.tipo.tone === 'neutral' ? 'var(--foreground)' : t.c } }, r.tipo.label)));
            })))),
        h('div', { className: 'alt-cco-telemetria__acc' },
          h('div', { className: 'alt-cco-telemetria__sec' }, 'Aceleração'),
          h(Stack, { label: 'Faixa de velocidade', items: p.faixa || DEFAULT_FAIXA }),
          h(Stack, { label: 'Tempo', items: p.tempo || DEFAULT_TEMPO }))) : null);
  }
  A.CcoTelemetria = CcoTelemetria;
})();


/* ---- CcoTorreTabela ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  var STATUS = {
    C: { label: 'Carregando', tone: 'info', icon: 'package' },
    V: { label: 'Em trajeto', tone: 'accent', icon: 'truck' },
    D: { label: 'Descarregando', tone: 'warning', icon: 'package' },
    F: { label: 'Finalizada', tone: 'success', icon: 'check-circle' },
    X: { label: 'Cancelada', tone: 'error', icon: 'x-circle' }
  };

  var DEFAULT_GROUPS = [
    { id: 'bitrem', label: 'Bitrem graneleiro', count: 24 },
    { id: 'carreta', label: 'Carreta LS', count: 17 },
    { id: 'truck', label: 'Truck', count: 9 }
  ];
  var DEFAULT_ROWS = [
    { id: 88213, grupo: 'bitrem', placa: 'QRT-4F56', motorista: 'Marta Souza', remetente: 'Vidroporto · Porto Ferreira', destinatario: 'Cebrace · Jacareí', status: 'V', eta: '17:45', chegou: false, progresso: 62,
      detalhe: { entrega: [['Remetente', '1h40'], ['Trajeto', '3h05'], ['Destinatário', '—']], jornada: [['Jornada', '8h00'], ['Extra', '2h25'], ['Restante', '−0h25']], telemetria: [['Distância', '121 km'], ['Vel. média', '58 km/h'], ['Eficiência', '2,31 km/l']], alerta: 'Acima do limite CLT' } },
    { id: 88190, grupo: 'bitrem', placa: 'ABC-1D23', motorista: 'João Pereira', remetente: 'AGC · Guaratinguetá', destinatario: 'Vidroporto · Porto Ferreira', status: 'F', eta: '14:20', chegou: true, progresso: 100 },
    { id: 88231, grupo: 'bitrem', placa: 'MNO-2B71', motorista: 'Carlos Lima', remetente: 'Saint-Gobain · Canoas', destinatario: 'Guardian · Porto Real', status: 'C', eta: '21:10', chegou: false, progresso: 8 },
    { id: 88174, grupo: 'carreta', placa: 'JHK-7G89', motorista: 'Ana Ribeiro', remetente: 'Cebrace · Barra Velha', destinatario: 'AGC · Guaratinguetá', status: 'X', cancelada: true },
    { id: 88202, grupo: 'carreta', placa: 'TRV-8K12', motorista: 'Luiz Andrade', remetente: 'Guardian · Tatuí', destinatario: 'Cebrace · Jacareí', status: 'D', eta: '15:55', chegou: true, progresso: 91 }
  ];

  function defaultColumns() {
    var Cel = A.CcoCelulas;
    return [
      { id: 'placa', label: 'Placa', sortable: true, width: 104, render: function (r) { return h('span', { className: 'alt-cco-torre-tabela__strong' }, r.placa); } },
      { id: 'motorista', label: 'Motorista', sortable: true, width: 150 },
      { id: 'destinatario', label: 'Remetente → destinatário', sortable: true, render: function (r) {
        return h('div', { className: 'alt-cco-torre-tabela__rota', title: r.remetente + ' → ' + r.destinatario },
          h('span', { className: 'alt-cco-torre-tabela__dest' }, r.destinatario), h('span', { className: 'alt-cco-torre-tabela__rem' }, 'de ' + r.remetente));
      } },
      { id: 'status', label: 'Status', sortable: true, width: 150, render: function (r) {
        var s = STATUS[r.status] || { label: r.status, tone: 'neutral', icon: 'circle' }, t = tone(s.tone);
        return h('span', { className: 'alt-chip', style: { background: t.bg, color: t.c } }, h(A.Icon, { name: s.icon, size: 13 }), s.label);
      } },
      { id: 'eta', label: 'Chegada / ETA', sortable: true, width: 140, render: function (r) {
        return Cel ? h(Cel.ChegadaEta, { horario: r.eta, chegou: r.chegou, cancelada: r.cancelada }) : (r.eta || '—');
      } },
      { id: 'progresso', label: 'Progresso', sortable: true, width: 170, render: function (r) {
        return Cel ? h(Cel.Progresso, { value: r.progresso, cancelada: r.cancelada }) : (r.progresso || 0) + '%';
      } },
      { id: 'acoes', label: 'Ações', width: 214, render: function (r) {
        return A.CcoAcoesLinha ? h(A.CcoAcoesLinha, { status: r.status }) : null;
      } }
    ];
  }

  function DefaultArea(p) {
    var d = p.row.detalhe;
    if (!d) return h('p', { className: 'alt-cco-torre-tabela__muted' }, 'Sem detalhes carregados para esta viagem.');
    var err = tone('error');
    function card(title, icon, kv, extra) {
      return h('div', { className: 'alt-cco-torre-tabela__mini' },
        h('div', { className: 'alt-cco-torre-tabela__minihd' }, h(A.Icon, { name: icon, size: 14 }), title),
        h('dl', null, kv.map(function (x) { return h('div', { key: x[0] }, h('dt', null, x[0]), h('dd', { className: 'alt-num' }, x[1])); })), extra || null);
    }
    return h('div', { className: 'alt-cco-torre-tabela__area' },
      card('Entrega', 'route', d.entrega),
      card('Jornada', 'clock', d.jornada, d.alerta ? h('span', { className: 'alt-chip', style: { background: err.bg, color: err.c, marginTop: 8 } }, h(A.Icon, { name: 'alert', size: 13 }), d.alerta) : null),
      card('Telemetria', 'gauge', d.telemetria),
      h('div', { className: 'alt-cco-torre-tabela__mini alt-cco-torre-tabela__mini--map' }, h(A.Icon, { name: 'map', size: 18 }), h('span', null, 'Mapa da viagem'), h('button', { type: 'button', className: 'alt-tbtn' }, 'Abrir rastreamento', h(A.Icon, { name: 'external', size: 14 }))));
  }

  function cmp(a, b) { if (a == null) return 1; if (b == null) return -1; return typeof a === 'number' && typeof b === 'number' ? a - b : String(a).localeCompare(String(b), 'pt-BR'); }

  function CcoTorreTabela(p) {
    var rows = p.dataRows || DEFAULT_ROWS;
    var groups = p.groups || DEFAULT_GROUPS;
    var cols = p.columns || defaultColumns();
    var getId = p.onGetRowId || function (r) { return r.id; };
    var ss = R.useState(p.defaultSort || { id: 'eta', dir: 'asc' }), sort = ss[0], setSort = ss[1];
    var es = R.useState(p.defaultExpanded || [88213]), exp = es[0], setExp = es[1];
    var cs = R.useState(p.defaultCollapsedGroups || ['truck']), collapsed = cs[0], setCollapsed = cs[1];
    var zs = R.useState(p.zoom || 1), zoom = zs[0], setZoom = zs[1];
    var bs = R.useState(p.batchProgress === undefined ? { grupo: 'bitrem', processed: 12, total: 40 } : p.batchProgress), batch = p.onReprocessBatch && p.batchProgress !== undefined ? p.batchProgress : bs[0], setBatch = bs[1];
    var timer = R.useRef(null);
    R.useEffect(function () { return function () { clearInterval(timer.current); }; }, []);

    function sortBy(id) {
      var n = sort.id === id ? { id: id, dir: sort.dir === 'asc' ? 'desc' : 'asc' } : { id: id, dir: 'asc' };
      setSort(n); if (p.onSortColumn) p.onSortColumn(n.id, n.dir);
    }
    function toggleRow(id) { setExp(exp.indexOf(id) >= 0 ? exp.filter(function (x) { return x !== id; }) : exp.concat(id)); }
    function toggleGroup(id) { setCollapsed(collapsed.indexOf(id) >= 0 ? collapsed.filter(function (x) { return x !== id; }) : collapsed.concat(id)); }
    function reprocess(g, total) {
      if (p.onReprocessBatch) { p.onReprocessBatch(g); return; }
      clearInterval(timer.current);
      var st = { grupo: g, processed: 0, total: total }; setBatch(st);
      timer.current = setInterval(function () {
        st = Object.assign({}, st, { processed: st.processed + 1 });
        if (st.processed >= st.total) { clearInterval(timer.current); st = null; }
        setBatch(st);
      }, 120);
    }
    function setZ(z) { z = Math.round(Math.max(0.8, Math.min(1.4, z)) * 10) / 10; setZoom(z); if (p.onZoomChange) p.onZoomChange(z); }

    var sorted = rows.slice().sort(function (a, b) { var r = cmp(a[sort.id], b[sort.id]); return sort.dir === 'asc' ? r : -r; });
    var rowH = Math.round(44 * zoom), fs = Math.round(13 * zoom * 10) / 10;
    var colCount = cols.length + 1;

    var body = [];
    if (p.loading) {
      for (var i = 0; i < 6; i++) body.push(h('tr', { key: 'sk' + i }, h('td', { colSpan: colCount, style: { height: rowH } }, h('span', { className: 'alt-skel alt-cco-torre-tabela__skel', style: { width: (60 + (i * 7) % 30) + '%' } }))));
    } else {
      groups.forEach(function (g) {
        var gr = sorted.filter(function (r) { return r.grupo === g.id; });
        var isC = collapsed.indexOf(g.id) >= 0;
        var bp = batch && batch.grupo === g.id ? batch : null;
        var total = g.count != null ? g.count : gr.length;
        body.push(h('tr', { key: 'g-' + g.id, className: 'alt-cco-torre-tabela__group' },
          h('td', { colSpan: colCount },
            h('div', { className: 'alt-cco-torre-tabela__gin' },
              h('button', { type: 'button', className: 'alt-cco-torre-tabela__gbtn', 'aria-expanded': !isC, onClick: function () { toggleGroup(g.id); } },
                h(A.Icon, { name: isC ? 'chevron-right' : 'chevron-down', size: 16 }),
                h('span', { className: 'alt-cco-torre-tabela__gt' }, g.label),
                h('span', { className: 'alt-cco-torre-tabela__gc alt-num' }, total + (total === 1 ? ' viagem' : ' viagens'))),
              h('span', { className: 'alt-cco-torre-tabela__sp' }),
              bp ? h('span', { className: 'alt-cco-torre-tabela__batch alt-num', role: 'status', 'aria-label': 'Reprocessando ' + bp.processed + ' de ' + bp.total },
                h('span', { className: 'alt-cco-torre-tabela__bbar' }, h('i', { style: { width: Math.round(bp.processed / bp.total * 100) + '%' } })), 'Reprocessando ' + bp.processed + '/' + bp.total)
                : h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-cco-torre-tabela__rp', onClick: function () { reprocess(g.id, total); } }, h(A.Icon, { name: 'refresh', size: 14 }), 'Reprocessar')))));
        if (isC) return;
        gr.forEach(function (r) {
          var id = getId(r), open = exp.indexOf(id) >= 0;
          var extra = p.getRowSx ? p.getRowSx(r) : null;
          body.push(h('tr', { key: 'r-' + id, className: cx('alt-cco-torre-tabela__row', open && 'alt-cco-torre-tabela__row--open'), style: Object.assign({ height: rowH }, extra) },
            h('td', { className: 'alt-cco-torre-tabela__exp' }, p.onRenderCollapseExpandCell ? p.onRenderCollapseExpandCell(r, open, function () { toggleRow(id); }) :
              h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost alt-cco-torre-tabela__xbtn', 'aria-expanded': open, 'aria-label': (open ? 'Recolher ' : 'Expandir ') + (r.placa || id), onClick: function () { toggleRow(id); } }, h(A.Icon, { name: open ? 'chevron-down' : 'chevron-right', size: 16 }))),
            cols.map(function (c) {
              return h('td', { key: c.id, className: cx(c.ellipsis && 'alt-cco-torre-tabela__ell') }, c.render ? c.render(r) : (c.ellipsis ? h('span', { title: r[c.id] }, r[c.id]) : r[c.id]));
            })));
          if (open) body.push(h('tr', { key: 'x-' + id, className: 'alt-cco-torre-tabela__xrow' }, h('td', { colSpan: colCount }, p.onRenderCollapsibleArea ? p.onRenderCollapsibleArea(r) : h(DefaultArea, { row: r }))));
        });
      });
    }

    return h('div', { className: cx('alt-cco-torre-tabela alt-panel', p.className), style: p.style },
      h('div', { className: 'alt-cco-torre-tabela__bar' },
        h('div', { className: 'alt-cco-torre-tabela__title' }, h('h3', null, p.title || 'Torre de controle'), h('span', { className: 'alt-num' }, (p.total || groups.reduce(function (s, g) { return s + (g.count || 0); }, 0) || rows.length) + ' viagens')),
        h('span', { className: 'alt-cco-torre-tabela__sp' }),
        h('div', { className: 'alt-cco-torre-tabela__zoom', role: 'group', 'aria-label': 'Zoom da tabela' },
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon', 'aria-label': 'Diminuir zoom', disabled: zoom <= 0.8, onClick: function () { setZ(zoom - 0.1); } }, h(A.Icon, { name: 'minus', size: 15 })),
          h('span', { className: 'alt-num', 'aria-live': 'polite' }, Math.round(zoom * 100) + '%'),
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon', 'aria-label': 'Aumentar zoom', disabled: zoom >= 1.4, onClick: function () { setZ(zoom + 0.1); } }, h(A.Icon, { name: 'plus', size: 15 })))),
      h('div', { className: 'alt-scroll alt-cco-torre-tabela__scroll', style: { maxHeight: p.scrollHeight || undefined } },
        h('table', { className: 'alt-cco-torre-tabela__table alt-num', style: { fontSize: fs }, 'aria-busy': p.loading ? 'true' : undefined },
          h('thead', null, h('tr', null,
            h('th', { className: 'alt-cco-torre-tabela__exp' }, h('span', { className: 'alt-cco-torre-tabela__sr' }, 'Expandir')),
            cols.map(function (c) {
              var on = sort.id === c.id;
              return h('th', { key: c.id, scope: 'col', style: { width: c.width }, 'aria-sort': on ? (sort.dir === 'asc' ? 'ascending' : 'descending') : undefined },
                c.sortable ? h('button', { type: 'button', className: cx('alt-cco-torre-tabela__sort', on && 'alt-cco-torre-tabela__sort--on'), onClick: function () { sortBy(c.id); } },
                  c.label, h(A.Icon, { name: on && sort.dir === 'desc' ? 'arrow-down' : 'arrow-up', size: 12 })) : c.label);
            }))),
          h('tbody', null, body))));
  }
  CcoTorreTabela.STATUS = STATUS;
  A.CcoTorreTabela = CcoTorreTabela;
})();


/* ---- ColumnPicker ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  function norm(s) { return String(s == null ? '' : s).normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase(); }
  function labelOf(c) { return c.headerName || c.label || c.field; }

  var DEFAULT_ITENS = [
    { field: 'placa', headerName: 'Placa', locked: true },
    { field: 'motorista', headerName: 'Motorista' },
    { field: 'cliente', headerName: 'Cliente', hide: true },
    { field: 'km', headerName: 'Km rodado' },
    { field: 'status', headerName: 'Status' },
    { field: 'posicao', headerName: 'Última posição', hide: true }
  ];

  function Check(p) {
    return h('span', { className: cx('alt-column-picker__cb', p.state === true && 'is-on', p.state === 'mixed' && 'is-on', p.locked && 'is-locked'), 'aria-hidden': 'true' },
      p.state === true ? h(A.Icon, { name: 'check', size: 11, strokeWidth: 3 }) : p.state === 'mixed' ? h(A.Icon, { name: 'minus', size: 11, strokeWidth: 3 }) : null);
  }

  function ColumnPicker(p) {
    var controlled = p.itens !== undefined;
    var st = R.useState(p.defaultItens || DEFAULT_ITENS); var itens = controlled ? p.itens : st[0];
    var os = R.useState(!!p.defaultOpen); var open = p.open !== undefined ? p.open : os[0];
    var qs = R.useState(''); var q = qs[0], setQ = qs[1];
    var root = R.useRef(null), search = R.useRef(null);
    var id = R.useRef('alt-cp-' + Math.random().toString(36).slice(2, 8)).current;

    function setOpen(v) { if (p.open === undefined) os[1](v); if (!v) setQ(''); if (p.onOpenChange) p.onOpenChange(v); }
    R.useEffect(function () {
      if (!open) return undefined;
      function f(e) { if (root.current && !root.current.contains(e.target)) setOpen(false); }
      function k(e) { if (e.key === 'Escape') setOpen(false); }
      document.addEventListener('mousedown', f); document.addEventListener('keydown', k);
      return function () { document.removeEventListener('mousedown', f); document.removeEventListener('keydown', k); };
    }, [open]);

    function emit(next) { if (!controlled) st[1](next); if (p.callbackChange) p.callbackChange(next); }
    function toggle(field) { emit(itens.map(function (c) { return c.field === field && !c.locked ? Object.assign({}, c, { hide: !c.hide }) : c; })); }
    function setAll(show) { emit(itens.map(function (c) { return c.locked ? c : Object.assign({}, c, { hide: !show }); })); }

    var visible = itens.filter(function (c) { return !c.hide; }).length;
    var hidden = itens.length - visible;
    var free = itens.filter(function (c) { return !c.locked; });
    var freeVisible = free.filter(function (c) { return !c.hide; }).length;
    var allState = freeVisible === free.length ? true : freeVisible === 0 ? false : 'mixed';
    var list = q ? itens.filter(function (c) { return norm(labelOf(c)).indexOf(norm(q)) >= 0; }) : itens;

    return h('div', { ref: root, className: cx('alt-column-picker', p.align === 'end' && 'alt-column-picker--end', p.className), style: p.style },
      h('button', {
        type: 'button', className: 'alt-tbtn alt-column-picker__trigger', 'aria-haspopup': 'true', 'aria-expanded': open, 'aria-controls': id, 'aria-pressed': open,
        onClick: function () { setOpen(!open); }
      }, h(A.Icon, { name: 'columns', size: 16 }), p.label || 'Colunas',
        hidden ? h('span', { className: 'alt-count', title: hidden === 1 ? '1 coluna oculta' : hidden + ' colunas ocultas' }, hidden) : null),
      open ? h('div', { id: id, className: 'alt-popover alt-column-picker__pop', role: 'menu', 'aria-label': 'Colunas visíveis' },
        h('div', { className: 'alt-column-picker__hd' },
          h('span', { className: 'app-label' }, p.title || 'Colunas visíveis'),
          h('span', { className: 'alt-num' }, visible + ' de ' + itens.length)),
        h('div', { className: 'alt-column-picker__search' },
          h('div', { className: 'alt-ctl alt-ctl--sm' }, h(A.Icon, { name: 'search', size: 15 }),
            h('input', { ref: search, value: q, autoFocus: p.autoFocusSearch !== false && !p.defaultOpen, placeholder: 'Buscar coluna…', 'aria-label': 'Buscar coluna', onChange: function (e) { setQ(e.target.value); } }))),
        h('div', { className: 'alt-scroll alt-column-picker__list' },
          list.length ? list.map(function (c) {
            return h('button', {
              key: c.field, type: 'button', role: 'menuitemcheckbox', 'aria-checked': !c.hide, 'aria-disabled': c.locked || undefined,
              title: c.locked ? 'Coluna fixa' : undefined,
              className: cx('alt-column-picker__item', !c.hide && 'is-on', c.locked && 'is-locked'),
              onClick: function () { if (!c.locked) toggle(c.field); }
            }, h(Check, { state: !c.hide, locked: c.locked }), h('span', { className: 'alt-column-picker__name' }, labelOf(c)),
              c.locked ? h(A.Icon, { name: 'lock', size: 14, className: 'alt-column-picker__lock', title: 'Coluna fixa' }) : null);
          }) : h('div', { className: 'alt-column-picker__empty' }, 'Nenhuma coluna encontrada')),
        h('div', { className: 'alt-column-picker__ft' },
          h('button', { type: 'button', role: 'menuitemcheckbox', 'aria-checked': allState, className: 'alt-column-picker__all', onClick: function () { setAll(allState !== true); } },
            h(Check, { state: allState }), 'Mostrar todas'),
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-column-picker__reset', onClick: function () { if (p.onReset) p.onReset(); else setAll(true); } },
            h(A.Icon, { name: 'refresh', size: 15 }), 'Redefinir'))) : null);
  }

  A.ColumnPicker = ColumnPicker;
})();


/* ---- CommandPalette ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var DEFAULT_ITEMS = [
    { label: 'Central de Violações', module: 'CCO', icon: 'alert' },
    { label: 'Parametrização de violações', module: 'Cadastros', icon: 'sliders' },
    { label: 'Violações por motorista', module: 'BI · Relatórios', icon: 'chart-bar' },
    { label: 'Viagens em andamento', module: 'CCO', icon: 'truck' },
    { label: 'Mapa da frota', module: 'CCO', icon: 'map' },
    { label: 'Painel de entregas', module: 'Operação', icon: 'package' },
    { label: 'Cadastro de motoristas', module: 'Cadastros', icon: 'users' },
    { label: 'Rotas e pontos de parada', module: 'Cadastros', icon: 'route' },
    { label: 'Chamados HelpDesk', module: 'Suporte', icon: 'message' },
    { label: 'Notificações', module: 'Sistema', icon: 'bell' }
  ];
  var ENTER = 'M20 5v7a3 3 0 0 1-3 3H5M9 11l-4 4 4 4';
  function Svg(d, size) {
    return h('svg', { width: size || 12, height: size || 12, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true' }, h('path', { d: d }));
  }
  function fold(s) {
    var out = '', map = [];
    for (var i = 0; i < s.length; i++) {
      var c = s[i].normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
      for (var j = 0; j < c.length; j++) { out += c[j]; map.push(i); }
    }
    return { s: out, map: map };
  }
  function highlight(label, q) {
    if (!q) return label;
    var f = fold(label), at = f.s.indexOf(fold(q).s);
    if (at < 0) return label;
    var a = f.map[at], b = f.map[at + fold(q).s.length - 1] + 1;
    return [label.slice(0, a), h('mark', { key: 'm' }, label.slice(a, b)), label.slice(b)];
  }
  function isMac() { try { return /Mac|iPhone|iPad/.test(navigator.platform); } catch (e) { return false; } }

  function CommandPalette(p) {
    var items = p.items || DEFAULT_ITEMS;
    var modal = !!p.modal;
    var so = R.useState(p.defaultOpen != null ? !!p.defaultOpen : !modal);
    var open = p.open != null ? p.open : so[0];
    var sq = R.useState(p.defaultQuery || ''), q = p.query != null ? p.query : sq[0];
    var sa = R.useState(0), active = sa[0], setActive = sa[1];
    var input = R.useRef(null), listRef = R.useRef(null);
    var limit = q ? (p.limit || 24) : (p.emptyLimit || 40);

    function setOpen(v) { if (p.open == null) so[1](v); if (!v && p.onClose) p.onClose(); if (v && p.onOpen) p.onOpen(); }
    function setQ(v) { if (p.query == null) sq[1](v); if (p.onQueryChange) p.onQueryChange(v); setActive(0); }

    R.useEffect(function () {
      if (!modal || p.hotkey === false) return;
      function onKey(e) { if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) { e.preventDefault(); setOpen(!open); } }
      document.addEventListener('keydown', onKey);
      return function () { document.removeEventListener('keydown', onKey); };
    }, [modal, open]);
    R.useEffect(function () { if (open && modal && input.current) input.current.focus(); }, [open]);

    var fq = fold(q.trim()).s;
    var results = items.filter(function (it) { return !fq || fold(it.label).s.indexOf(fq) >= 0 || fold(it.module || '').s.indexOf(fq) >= 0; }).slice(0, limit);
    var groups = [], byName = {};
    results.forEach(function (it, i) {
      var g = it.section || 'Telas';
      if (!byName[g]) { byName[g] = { name: g, rows: [] }; groups.push(byName[g]); }
      byName[g].rows.push({ it: it, i: i });
    });
    var flat = []; groups.forEach(function (g) { g.rows.forEach(function (r) { flat.push(r.it); }); });
    var act = Math.min(active, Math.max(flat.length - 1, 0));

    R.useEffect(function () {
      var el = listRef.current && listRef.current.querySelector('[data-idx="' + act + '"]');
      if (el && el.scrollIntoView) el.scrollIntoView({ block: 'nearest' });
    }, [act]);

    function choose(it) { if (!it) return; if (p.onSelect) p.onSelect(it); if (modal) setOpen(false); }
    function onKey(e) {
      var n = flat.length;
      if (e.key === 'ArrowDown' || e.key === 'ArrowRight' && e.target.selectionStart === e.target.value.length) { if (n) { e.preventDefault(); setActive((act + 1) % n); } }
      else if (e.key === 'ArrowUp' || e.key === 'ArrowLeft' && e.target.selectionStart === 0) { if (n) { e.preventDefault(); setActive((act - 1 + n) % n); } }
      else if (e.key === 'Enter') { e.preventDefault(); choose(flat[act]); }
      else if (e.key === 'Escape') { e.preventDefault(); if (q) setQ(''); else setOpen(false); }
    }

    if (!open) return null;
    var listId = 'alt-command-palette-list';
    var idx = -1;
    var box = h('div', { className: cx('alt-glass', 'alt-glass--overlay', 'alt-command-palette', p.className), style: p.style, role: 'dialog', 'aria-modal': modal ? 'true' : undefined, 'aria-label': p.ariaLabel || 'Busca global' },
      h('div', { className: 'alt-command-palette__search' },
        h(A.Icon, { name: 'search', size: 20 }),
        h('input', { ref: input, className: 'alt-command-palette__input', value: q, placeholder: p.placeholder || 'Buscar telas, relatórios e cadastros', autoFocus: modal,
          role: 'combobox', 'aria-expanded': true, 'aria-controls': listId, 'aria-activedescendant': flat.length ? listId + '-' + act : undefined, 'aria-autocomplete': 'list',
          onChange: function (e) { setQ(e.target.value); }, onKeyDown: onKey }),
        h('kbd', { className: 'alt-command-palette__kbd' }, 'Esc')),
      h('div', { ref: listRef, className: 'alt-command-palette__list alt-scroll', role: 'listbox', id: listId },
        flat.length ? groups.map(function (g) {
          return h('div', { key: g.name, role: 'group', 'aria-label': g.name },
            h('div', { className: 'alt-command-palette__section' }, g.name),
            g.rows.map(function (r) {
              idx++; var me = idx, on = me === act;
              return h('div', { key: me, id: listId + '-' + me, 'data-idx': me, role: 'option', 'aria-selected': on, className: cx('alt-command-palette__item', on && 'alt-command-palette__item--active'),
                onMouseMove: function () { if (!on) setActive(me); }, onMouseDown: function (e) { e.preventDefault(); }, onClick: function () { choose(r.it); } },
                h('span', { className: 'alt-command-palette__icon' }, h(A.Icon, { name: r.it.icon || 'file', size: 16 })),
                h('span', { className: 'alt-command-palette__text' },
                  h('span', { className: 'alt-command-palette__label' }, highlight(r.it.label, q.trim())),
                  r.it.module ? h('span', { className: 'alt-command-palette__module' }, r.it.module) : null),
                on ? h('kbd', { className: 'alt-command-palette__kbd', 'aria-hidden': 'true' }, Svg(ENTER)) : null);
            }));
        }) : h('div', { className: 'alt-command-palette__empty' }, h(A.Icon, { name: 'search', size: 20 }), p.emptyText || 'Nenhum resultado encontrado')),
      h('div', { className: 'alt-command-palette__foot' },
        h('span', { className: 'alt-command-palette__count alt-num' }, flat.length + (flat.length === 1 ? ' resultado' : ' resultados')),
        h('span', { className: 'alt-command-palette__hint alt-command-palette__hint--opt' }, h('kbd', { className: 'alt-command-palette__kbd' }, Svg('M12 19V5M6 11l6-6 6 6')), h('kbd', { className: 'alt-command-palette__kbd' }, Svg('M12 5v14M18 13l-6 6-6-6')), 'navegar'),
        h('span', { className: 'alt-command-palette__hint' }, h('kbd', { className: 'alt-command-palette__kbd' }, Svg(ENTER)), 'abrir'),
        h('span', { className: 'alt-command-palette__hint' }, h('kbd', { className: 'alt-command-palette__kbd' }, isMac() ? '⌘' : 'Ctrl'), h('kbd', { className: 'alt-command-palette__kbd' }, 'K'))));
    if (!modal) return box;
    return h('div', { className: 'alt-command-palette__scrim', onMouseDown: function (e) { if (e.target === e.currentTarget) setOpen(false); } }, box);
  }

  A.CommandPalette = CommandPalette;
})();


/* ---- CorporateDrawer ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var Icon = A.Icon;

  /* Chip do header: status (ponto + ícone + palavra) ou informação (ícone + texto). */
  function CorporateDrawerHeaderChip(p) {
    var t = p.tone ? K.tone(p.tone) : null;
    return h('span', { className: 'alt-corporate-drawer__chip alt-num', style: t ? { color: t.c, background: t.bg, borderColor: 'transparent' } : null },
      t ? h(Icon, { name: p.icon || t.icon, size: 13 }) : (p.icon ? h(Icon, { name: p.icon, size: 13 }) : null),
      p.label || p.children);
  }

  function CorporateDrawerHeader(p) {
    return h('header', { className: 'alt-corporate-drawer__head' },
      h('div', { className: 'alt-corporate-drawer__head-text' },
        p.eyebrow ? h('div', { className: 'alt-corporate-drawer__eyebrow' }, p.eyebrow) : null,
        h('h2', { id: p.titleId, className: 'alt-corporate-drawer__title' }, p.title),
        p.subtitle ? h('div', { className: 'alt-corporate-drawer__subtitle alt-num' }, p.subtitle) : null,
        p.chips && p.chips.length ? h('div', { className: 'alt-corporate-drawer__chips' },
          p.chips.map(function (c, i) { return h(CorporateDrawerHeaderChip, Object.assign({ key: i }, c)); })) : null),
      p.onClose ? h('button', { ref: p.closeRef, type: 'button', className: 'alt-corporate-drawer__close', 'aria-label': 'Fechar', onClick: p.onClose },
        h(Icon, { name: 'x', size: 18 })) : null);
  }

  var REASONS = ['Parada não programada', 'Desvio de rota autorizado', 'Falha de sinal do rastreador', 'Manutenção emergencial'];

  function DemoBody() {
    var r = R.useState(REASONS[0]), o = R.useState('');
    return h(R.Fragment, null,
      h('label', { className: 'alt-field' },
        h('span', { className: 'alt-field__label' }, 'Motivo'),
        h('span', { className: 'alt-ctl' },
          h('select', { value: r[0], onChange: function (e) { r[1](e.target.value); } },
            REASONS.map(function (x) { return h('option', { key: x, value: x }, x); })),
          h(Icon, { name: 'chevron-down', size: 16 }))),
      h('label', { className: 'alt-field' },
        h('span', { className: 'alt-field__label' }, 'Observação'),
        h('span', { className: 'alt-ctl alt-corporate-drawer__textarea' },
          h('textarea', { rows: 3, value: o[0], placeholder: 'Descreva o que aconteceu…', onChange: function (e) { o[1](e.target.value); } })),
        h('span', { className: 'alt-field__help' }, 'Visível para o CCO e para o gestor da frota.')));
  }

  function CorporateDrawer(p) {
    var open = p.open !== false;
    var closeRef = R.useRef(null);
    var titleId = R.useRef('alt-cd-' + Math.random().toString(36).slice(2, 8)).current;
    var onClose = p.onClose;

    R.useEffect(function () {
      if (!open) return;
      if (closeRef.current && p.autoFocus !== false) closeRef.current.focus({ preventScroll: true });
      function onKey(e) { if (e.key === 'Escape' && onClose) onClose(); }
      document.addEventListener('keydown', onKey);
      return function () { document.removeEventListener('keydown', onKey); };
    }, [open]);

    if (!open) return null;

    var chips = p.chips !== undefined ? p.chips : [
      { tone: 'error', label: 'Crítica' },
      { icon: 'clock', label: '30/09 14:37' }
    ];
    var footer = p.footer !== undefined ? p.footer : h(R.Fragment, null,
      h(A.Button, { variant: 'secondary', size: 'sm', onClick: onClose }, p.cancelLabel || 'Cancelar'),
      h(A.Button, { variant: 'primary', size: 'sm', arrow: false, onClick: p.onPrimary },
        h(Icon, { name: 'check', size: 16 }), p.primaryLabel || 'Salvar justificativa'));

    return h('div', { className: cx('alt-corporate-drawer', p.contained && 'alt-corporate-drawer--contained', p.className), style: p.style },
      h('div', { className: 'alt-corporate-drawer__scrim', onClick: p.disableBackdropClose ? undefined : onClose, 'aria-hidden': 'true' }),
      h('aside', { className: 'alt-corporate-drawer__paper alt-glass alt-glass--overlay', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': titleId, style: { width: p.width || 420 } },
        h(CorporateDrawerHeader, {
          titleId: titleId, closeRef: closeRef, onClose: onClose,
          eyebrow: p.eyebrow !== undefined ? p.eyebrow : 'Violação · CCO',
          title: p.title !== undefined ? p.title : 'Justificar ocorrência',
          subtitle: p.subtitle !== undefined ? p.subtitle : 'Placa ABC-1D23 · Viagem 88213',
          chips: chips
        }),
        h('div', { className: 'alt-corporate-drawer__body alt-scroll' }, p.children !== undefined ? p.children : h(DemoBody)),
        footer ? h('footer', { className: 'alt-corporate-drawer__foot' }, footer) : null));
  }

  CorporateDrawer.Header = CorporateDrawerHeader;
  CorporateDrawer.HeaderChip = CorporateDrawerHeaderChip;
  A.CorporateDrawer = CorporateDrawer;
})();


/* ---- CredentialWarning ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, cx = K.cx;

  /* Crachá de traço (não existe no kit). */
  function BadgeIcon(size) {
    return h('svg', { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      h('rect', { x: 3, y: 5, width: 18, height: 14, rx: 2 }),
      h('circle', { cx: 9, cy: 11, r: 2 }),
      h('path', { d: 'M6.5 16c.5-1.5 1.4-2 2.5-2s2 .5 2.5 2M14 10h4M14 14h3' }));
  }

  function CredentialWarning(p) {
    var title = p.title !== undefined ? p.title : 'Credencial do usuário';
    var description = p.description !== undefined ? p.description : 'Aguardando a credencial do usuário…';
    var icon = p.icon !== undefined ? (typeof p.icon === 'string' ? h(A.Icon, { name: p.icon, size: 28 }) : p.icon) : BadgeIcon(28);
    return h('div', { className: cx('alt-credential-warning', p.fullscreen && 'alt-credential-warning--full', p.className), style: p.style, role: 'status', 'aria-live': 'polite' },
      h('div', { className: 'alt-credential-warning__orb' },
        h('span', { className: 'alt-credential-warning__fill', 'aria-hidden': 'true' }),
        h('svg', { className: 'alt-credential-warning__ring', viewBox: '0 0 88 88', 'aria-hidden': 'true' },
          h('circle', { className: 'alt-credential-warning__track', cx: 44, cy: 44, r: 42.5 }),
          h('circle', { className: 'alt-credential-warning__arc', cx: 44, cy: 44, r: 42.5 })),
        h('span', { className: 'alt-credential-warning__icon' }, icon)),
      h('h2', { className: 'alt-credential-warning__title' }, title),
      description ? h('div', { className: 'alt-credential-warning__desc' }, description) : null);
  }

  A.CredentialWarning = CredentialWarning;
})();


/* ---- CteLeitor ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone, nf = K.nf;

  var UF = { 11: 'RO', 12: 'AC', 13: 'AM', 14: 'RR', 15: 'PA', 16: 'AP', 17: 'TO', 21: 'MA', 22: 'PI', 23: 'CE', 24: 'RN', 25: 'PB', 26: 'PE', 27: 'AL', 28: 'SE', 29: 'BA', 31: 'MG', 32: 'ES', 33: 'RJ', 35: 'SP', 41: 'PR', 42: 'SC', 43: 'RS', 50: 'MS', 51: 'MT', 52: 'GO', 53: 'DF' };

  function digits(s) { return String(s || '').replace(/\D/g, '').slice(0, 44); }
  function groups(s) { return s.replace(/(\d{4})(?=\d)/g, '$1 '); }
  function dv(k43) {
    var w = [2, 3, 4, 5, 6, 7, 8, 9], s = 0;
    for (var i = 0; i < k43.length; i++) s += (+k43.charAt(k43.length - 1 - i)) * w[i % 8];
    var r = s % 11; return r < 2 ? 0 : 11 - r;
  }
  function cnpj(s) { return s.replace(/^(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})$/, '$1.$2.$3/$4-$5'); }

  /* Splits a CT-e access key into its parts (only the parts already typed). */
  function partes(k) {
    var P = [
      { id: 'uf', label: 'UF', a: 0, b: 2, tip: 'Código IBGE da UF do emitente', fmt: function (v) { return UF[+v] ? v + ' · ' + UF[+v] : v; } },
      { id: 'emissao', label: 'Emissão', a: 2, b: 6, tip: 'Ano e mês de emissão (AAMM)', fmt: function (v) { return v.slice(2) + '/' + v.slice(0, 2); } },
      { id: 'cnpj', label: 'CNPJ', a: 6, b: 20, tip: 'CNPJ do emitente', fmt: cnpj },
      { id: 'modelo', label: 'Modelo', a: 20, b: 22, tip: 'Modelo do documento: 57 CT-e, 67 CT-e OS' },
      { id: 'serie', label: 'Série', a: 22, b: 25, tip: 'Série do CT-e', fmt: function (v) { return String(+v); } },
      { id: 'numero', label: 'Número', a: 25, b: 34, tip: 'Número do CT-e', fmt: function (v) { return String(+v); } },
      { id: 'tpemis', label: 'Forma', a: 34, b: 35, tip: 'Forma de emissão (1 = normal)' },
      { id: 'codigo', label: 'Código', a: 35, b: 43, tip: 'Código numérico aleatório' },
      { id: 'dv', label: 'DV', a: 43, b: 44, tip: 'Dígito verificador (módulo 11)' }
    ];
    return P.filter(function (p) { return k.length >= p.b; }).map(function (p) { var v = k.slice(p.a, p.b); return Object.assign({}, p, { value: p.fmt ? p.fmt(v) : v }); });
  }

  function validar(k, registradas) {
    if (k.length !== 44) return { ok: false, text: 'A chave tem ' + k.length + ' dígitos; são necessários 44.' };
    var mod = k.slice(20, 22);
    if (mod !== '57' && mod !== '67') return { ok: false, text: 'Modelo ' + mod + ' não é CT-e (esperado 57 ou 67).' };
    var esp = dv(k.slice(0, 43));
    if (esp !== +k.charAt(43)) return { ok: false, text: 'Dígito verificador inválido: esperado ' + esp + ', lido ' + k.charAt(43) + '.' };
    if (registradas.indexOf(k) >= 0) return { ok: false, text: 'Este CT-e já foi registrado neste lote.' };
    return { ok: true };
  }

  var DEFAULT_LOTE = { id: 42, nome: 'Setembro', previstos: 150, lidos: 127, fechado: false };

  function CteLeitor(p) {
    var lote = Object.assign({}, DEFAULT_LOTE, p.lote);
    var ms = R.useState(p.defaultModo || 'leitor'), modo = ms[0], setModo = ms[1];
    var vs = R.useState(digits(p.defaultChave)), val = vs[0], setVal = vs[1];
    var rs = R.useState(null), res = rs[0], setRes = rs[1];
    var ls = R.useState(lote.lidos), lidos = ls[0], setLidos = ls[1];
    var gs = R.useState([]), regs = gs[0], setRegs = gs[1];
    var fs = R.useState(!!p.defaultFocused), focused = fs[0], setFocused = fs[1];
    var inp = R.useRef(null);

    function registrar(k) {
      var v = validar(k, regs);
      if (!v.ok) { setRes({ tone: 'error', text: v.text }); if (p.onError) p.onError(k, v.text); return; }
      var n = lidos + 1;
      setRegs(regs.concat(k)); setLidos(n);
      setRes({ tone: 'success', text: 'CT-e ' + (+k.slice(25, 34)) + ' registrado no lote · ' + nf(n) + ' de ' + nf(lote.previstos) + ' previstos' });
      if (p.onSubmit) p.onSubmit(k);
    }
    R.useEffect(function () { if (p.defaultChave && p.autoSubmit !== false) registrar(digits(p.defaultChave)); }, []);

    function onChange(e) {
      var k = digits(e.target.value); setVal(k);
      if (res && k.length < 44) setRes(null);
      if (modo === 'leitor' && k.length === 44) registrar(k);
    }
    function onKey(e) { if (e.key === 'Enter') { e.preventDefault(); registrar(val); } }
    function trocaModo(m) { setModo(m); setRes(null); if (inp.current) inp.current.focus(); }

    var ativo = !lote.fechado && modo === 'leitor' && focused;
    var stTone = lote.fechado ? tone('neutral') : ativo ? tone('success') : tone('neutral');
    var parts = partes(val);
    var rt = res ? tone(res.tone) : null;
    var pct = Math.min(100, lidos / Math.max(1, lote.previstos) * 100);

    return h('section', { className: cx('alt-cte-leitor alt-glass', p.className), style: p.style, 'aria-label': 'Leitura de CT-e' },
      h('header', { className: 'alt-cte-leitor__hd' },
        h('span', { className: 'alt-cte-leitor__ic' }, h(A.Icon, { name: 'scan', size: 18 })),
        h('div', { className: 'alt-cte-leitor__ht' }, h('h3', null, 'Lote ' + lote.id + ' · ' + lote.nome),
          h('span', { className: 'alt-num' }, nf(lidos) + ' de ' + nf(lote.previstos) + ' CT-e lidos')),
        h('span', { className: 'alt-chip', style: { background: stTone.bg, color: ativo ? stTone.c : 'var(--foreground)' }, role: 'status' },
          h(A.Icon, { name: lote.fechado ? 'lock' : ativo ? 'check-circle' : 'clock', size: 13 }), lote.fechado ? 'Lote fechado' : ativo ? 'Leitor ativo' : 'Aguardando'),
        h('div', { className: 'alt-cte-leitor__seg', role: 'radiogroup', 'aria-label': 'Modo de entrada' },
          [['leitor', 'Leitor'], ['digitar', 'Digitar']].map(function (m) {
            return h('button', { key: m[0], type: 'button', role: 'radio', 'aria-checked': modo === m[0], disabled: lote.fechado, onClick: function () { trocaModo(m[0]); } }, m[1]);
          }))),
      h('div', { className: 'alt-cte-leitor__bd' },
        h('label', { className: 'alt-cte-leitor__lbl', htmlFor: 'alt-cte-' + lote.id + '-' + modo }, modo === 'leitor' ? 'Aponte o leitor para o código de barras do DACTE' : 'Digite os 44 dígitos da chave de acesso'),
        h('div', { className: cx('alt-cte-leitor__field', res && 'alt-cte-leitor__field--' + res.tone, lote.fechado && 'alt-cte-leitor__field--off') },
          h('textarea', { id: 'alt-cte-' + lote.id + '-' + modo, ref: inp, rows: 2, className: 'alt-num', inputMode: 'numeric', autoComplete: 'off', spellCheck: false, disabled: lote.fechado,
            value: groups(val), placeholder: '0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000', onChange: onChange, onKeyDown: onKey,
            onFocus: function () { setFocused(true); }, onBlur: function () { setFocused(false); },
            'aria-invalid': res && res.tone === 'error' ? 'true' : undefined, 'aria-describedby': 'alt-cte-msg-' + lote.id }),
          h('span', { className: 'alt-cte-leitor__count alt-num' }, val.length + '/44'),
          modo === 'digitar' ? h('button', { type: 'button', className: 'alt-cte-leitor__go', disabled: lote.fechado || val.length !== 44, onClick: function () { registrar(val); } }, 'Registrar') : null),
        parts.length ? h('ul', { className: 'alt-cte-leitor__parts alt-num', 'aria-label': 'Partes da chave' }, parts.map(function (x) {
          return h('li', { key: x.id, className: 'alt-cte-leitor__part', tabIndex: 0, 'data-tip': x.tip, 'aria-label': x.label + ' ' + x.value + '. ' + x.tip }, h('small', null, x.label), x.value);
        })) : null,
        h('div', { id: 'alt-cte-msg-' + lote.id, 'aria-live': 'polite' },
          lote.fechado ? h('div', { className: 'alt-cte-leitor__alert', style: { background: tone('neutral').bg } }, h(A.Icon, { name: 'lock', size: 16 }), 'Lote fechado em ' + (lote.fechadoEm || '30/09') + '. Abra um novo lote para continuar a leitura.')
            : res ? h('div', { className: 'alt-cte-leitor__alert', role: res.tone === 'error' ? 'alert' : 'status', style: { background: rt.bg, color: rt.c } }, h(A.Icon, { name: rt.icon, size: 16 }), h('span', { style: { color: 'var(--foreground)' } }, res.text))
            : modo === 'leitor' && !focused ? h('p', { className: 'alt-cte-leitor__hint' }, 'Clique no campo para ativar o leitor. A chave é registrada assim que os 44 dígitos chegam.') : null),
        h('div', { className: 'alt-cte-leitor__prog', role: 'progressbar', 'aria-valuemin': 0, 'aria-valuemax': lote.previstos, 'aria-valuenow': lidos, 'aria-label': 'Progresso do lote' }, h('i', { style: { width: pct + '%' } }))));
  }
  CteLeitor.validar = function (k) { return validar(digits(k), []); };
  CteLeitor.partes = function (k) { return partes(digits(k)); };
  A.CteLeitor = CteLeitor;
})();


/* ---- CustomBackdrop ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, cx = K.cx;

  /* Overlay de carregamento local: cobre só o container pai (que precisa de position:relative). */
  function CustomBackdrop(p) {
    if (p.open === false) return null;
    var label = p.children !== undefined ? p.children : 'Carregando posições…';
    return h('div', { className: cx('alt-custom-backdrop', p.className), role: 'status', 'aria-live': 'polite', style: p.style },
      h('span', { className: 'alt-custom-backdrop__pill' },
        h('span', { className: 'alt-custom-backdrop__spinner', 'aria-hidden': 'true' }),
        label));
  }

  A.CustomBackdrop = CustomBackdrop;
})();


/* ---- CustomMap ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var WW = 1000, WH = 560;

  var DEF = {
    route: [[300, 440], [360, 400], [440, 352], [500, 300], [600, 248], [700, 196]],
    predicted: [[700, 196], [780, 176], [850, 168]],
    fences: [{ x: 500, y: 300, r: 70, label: 'Cerca · CD Campinas' }],
    markers: [
      { id: 'ini', kind: 'start', x: 300, y: 440, label: 'Início', detail: 'Vidroporto · Guarulhos · 30/09 08:12' },
      { id: 'vei', kind: 'vehicle', x: 500, y: 300, label: 'ABC-1D23', status: 'success', statusLabel: 'Em dia', detail: 'João Pereira · 62 km/h · 30/09 14:31' },
      { id: 'crit', kind: 'critical', x: 700, y: 196, label: 'Parada crítica', detail: 'Parado há 42 min · fora da rota prevista' },
      { id: 'dst', kind: 'destination', x: 850, y: 168, label: 'Destino', detail: 'Owens-Illinois · Jundiaí · previsto 16:05' }
    ],
    fleet: [
      { id: 'f1', x: 250, y: 160, label: 'DEF-4G56' }, { id: 'f2', x: 268, y: 178, label: 'GHI-7J89' }, { id: 'f3', x: 242, y: 186, label: 'JKL-0M12' }, { id: 'f4', x: 282, y: 150, label: 'MNO-3P45' },
      { id: 'f5', x: 820, y: 420, label: 'PQR-6S78' }, { id: 'f6', x: 845, y: 436, label: 'STU-9V01' }, { id: 'f7', x: 830, y: 452, label: 'VWX-2Y34' },
      { id: 'f8', x: 560, y: 440, label: 'YZA-5B67' }, { id: 'f9', x: 640, y: 420, label: 'BCD-8E90' }
    ],
    info: { placa: 'ABC-1D23', status: 'success', statusLabel: 'Em dia', meta: 'João Pereira · 62 km/h · 30/09 14:31' }
  };
  var KIND = {
    start: { c: 'var(--chart-4)', icon: 'flag', name: 'Início' },
    vehicle: { c: 'var(--chart-1)', icon: 'truck', name: 'Veículo' },
    critical: { c: 'var(--signal-error)', icon: 'alert', name: 'Parada crítica' },
    destination: { c: 'var(--chart-2)', icon: 'pin', name: 'Destino' }
  };

  function useBox() {
    var ref = R.useRef(null), s = R.useState({ w: 0, h: 0 });
    R.useLayoutEffect(function () {
      var el = ref.current; if (!el) return;
      var set = function () { s[1]({ w: el.clientWidth, h: el.clientHeight }); };
      set(); if (!window.ResizeObserver) return;
      var ro = new ResizeObserver(set); ro.observe(el); return function () { ro.disconnect(); };
    }, []);
    return [ref, s[0]];
  }
  function iconPath(name) { return (K.icons[name] || K.icons.circle).split(/(?=M)/); }

  /* stylised base map: blocks, park, water, roads */
  function Base(p) {
    var sat = p.layer === 'satelite', blocks = [];
    for (var i = 0; i < 11; i++) for (var j = 0; j < 7; j++) {
      var seed = (i * 7 + j * 13) % 9;
      blocks.push(h('rect', { key: i + '-' + j, x: i * 92 + 8, y: j * 82 + 8, width: 76, height: 66, rx: 6,
        fill: sat ? 'rgba(94,234,212,' + (0.02 + seed * 0.006).toFixed(3) + ')' : 'rgba(255,255,255,' + (0.018 + (seed % 3) * 0.008).toFixed(3) + ')' }));
    }
    return h('g', { className: 'alt-custom-map__base' },
      h('rect', { x: -2000, y: -2000, width: 5000, height: 5000, className: sat ? 'alt-custom-map__ground--sat' : 'alt-custom-map__ground' }),
      blocks,
      h('path', { d: 'M560 90h170v110H560z', className: 'alt-custom-map__park', rx: 8 }),
      h('path', { d: 'M-20 500 C120 470 240 540 380 510 S560 520 620 580 L-20 600Z', className: 'alt-custom-map__water' }),
      h('path', { d: 'M-20 290 H1020 M190 -20 V600 M740 -20 V600 M-20 80 L1020 520 M460 -20 C440 200 520 360 470 600', className: 'alt-custom-map__road' }),
      h('path', { d: 'M-20 290 H1020 M190 -20 V600 M740 -20 V600 M-20 80 L1020 520 M460 -20 C440 200 520 360 470 600', className: 'alt-custom-map__road-line' }),
      h('path', { d: 'M-20 170 H1020 M-20 420 H1020 M330 -20 V600 M620 -20 V600 M880 -20 V600', className: 'alt-custom-map__street' }),
      h('text', { x: 200, y: 282, className: 'alt-custom-map__label' }, 'Rod. dos Bandeirantes'),
      h('text', { x: 590, y: 150, className: 'alt-custom-map__label' }, 'Parque Ecológico'));
  }

  var CustomMap = R.forwardRef(function (p, ref) {
    var route = p.route || DEF.route, predicted = p.predicted || DEF.predicted, fences = p.fences || DEF.fences;
    var markers = p.markers || DEF.markers, fleet = p.fleet || DEF.fleet, info = p.info === undefined ? DEF.info : p.info;
    var home = p.center || { x: WW / 2, y: WH / 2, z: 1 };
    var vw = R.useState(home), view = vw[0];
    var ly = R.useState(p.defaultLayer || 'mapa'), layer = p.layer || ly[0];
    var se = R.useState(p.defaultSelected === undefined ? 'vei' : p.defaultSelected), sel = se[0];
    var bx = useBox(), box = bx[1], drag = R.useRef(null);
    var ar = box.w && box.h ? box.w / box.h : WW / WH, w, hh;
    if (ar >= WW / WH) { w = WW / view.z; hh = w / ar; } else { hh = WH / view.z; w = hh * ar; }
    var vx = view.x - w / 2, vy = view.y - hh / 2, sc = box.w ? box.w / w : 1, ox = 0, oy = 0;
    function scr(x, y) { return { x: (x - vx) * sc + ox, y: (y - vy) * sc + oy }; }
    function go(x, y, z) { vw[1]({ x: x, y: y, z: Math.max(1, Math.min(6, z == null ? view.z : z)) }); }
    function zoom(f) { go(view.x, view.y, view.z * f); }
    function centralizarPonto(x, y, z) { go(x, y, z || Math.max(view.z, 2)); }
    var veh = markers.filter(function (m) { return m.kind === 'vehicle'; })[0];
    R.useImperativeHandle(ref, function () {
      return { centralizarPonto: centralizarPonto, centralizarCerca: function (i) { var f = fences[i || 0]; if (f) go(f.x, f.y, Math.min(6, 160 / f.r)); }, zoomIn: function () { zoom(1.5); }, zoomOut: function () { zoom(1 / 1.5); } };
    });
    function setLayer(l) { ly[1](l); p.onLayerChange && p.onLayerChange(l); }
    function select(id, m) { se[1](id); p.onMarkerClick && m && p.onMarkerClick(m); }

    /* clusters (fleet only, grid in world units) */
    var cell = 90 / view.z, groups = {};
    fleet.forEach(function (f) { var k = Math.floor(f.x / cell) + ':' + Math.floor(f.y / cell); (groups[k] = groups[k] || []).push(f); });
    var inv = 1 / view.z;
    var ms = function (x, y) { return 'translate(' + x + ' ' + y + ') scale(' + inv + ')'; };

    var pts = function (a) { return a.map(function (q) { return q.join(' '); }).join(' '); };
    var selM = markers.filter(function (m) { return m.id === sel; })[0] || fleet.filter(function (m) { return m.id === sel; })[0];
    var pop = selM && box.w ? scr(selM.x, selM.y) : null;
    if (pop && (pop.x < 0 || pop.x > box.w || pop.y < 0 || pop.y > box.h)) pop = null;

    function onDown(e) { if (e.button !== 0) return; drag.current = { x: e.clientX, y: e.clientY, vx: view.x, vy: view.y, moved: false, el: e.currentTarget, id: e.pointerId }; }
    function onMove(e) {
      var d = drag.current; if (!d) return;
      var dx = e.clientX - d.x, dy = e.clientY - d.y; if (!d.moved && Math.abs(dx) + Math.abs(dy) > 3) { d.moved = true; try { d.el.setPointerCapture(d.id); } catch (er) {} }
      if (d.moved) vw[1]({ x: d.vx - dx / sc, y: d.vy - dy / sc, z: view.z });
    }
    function onUp() { setTimeout(function () { drag.current = null; }, 0); }
    function clickMark(fn) { return function (e) { e.stopPropagation(); if (drag.current && drag.current.moved) return; fn(); }; }
    function keyAct(fn) { return function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); fn(); } }; }

    var svgMarks = [];
    Object.keys(groups).forEach(function (k) {
      var g = groups[k];
      if (g.length > 1) {
        var gx = g.reduce(function (a, b) { return a + b.x; }, 0) / g.length, gy = g.reduce(function (a, b) { return a + b.y; }, 0) / g.length;
        var act = function () { go(gx, gy, view.z * 2); };
        svgMarks.push(h('g', { key: 'c' + k, transform: ms(gx, gy), className: 'alt-custom-map__cluster', tabIndex: 0, role: 'button', 'aria-label': g.length + ' veículos agrupados; aproximar', onClick: clickMark(act), onKeyDown: keyAct(act) },
          h('circle', { r: 22, className: 'alt-custom-map__cluster-halo' }), h('circle', { r: 15, className: 'alt-custom-map__cluster-core' }),
          h('text', { y: 4.5, textAnchor: 'middle', className: 'alt-custom-map__cluster-n' }, g.length)));
      } else {
        var f = g[0], act2 = function () { select(f.id, f); };
        svgMarks.push(h('g', { key: f.id, transform: ms(f.x, f.y), className: 'alt-custom-map__fleet', tabIndex: 0, role: 'button', 'aria-label': 'Veículo ' + f.label, 'aria-pressed': sel === f.id, onClick: clickMark(act2), onKeyDown: keyAct(act2) },
          h('circle', { r: 12, fill: 'transparent' }), h('circle', { r: 6.5, className: 'alt-custom-map__fleet-dot' })));
      }
    });
    markers.forEach(function (m) {
      var k = KIND[m.kind] || KIND.destination, on = sel === m.id, act = function () { select(m.id, m); };
      svgMarks.push(h('g', { key: m.id, transform: ms(m.x, m.y), className: 'alt-custom-map__pin', 'data-on': on ? 'true' : null, tabIndex: 0, role: 'button', 'aria-label': k.name + ': ' + m.label, 'aria-pressed': on, onClick: clickMark(act), onKeyDown: keyAct(act), style: { '--c': m.color || k.c } },
        h('ellipse', { cx: 0, cy: 0, rx: 7, ry: 2.5, className: 'alt-custom-map__pin-shadow' }),
        h('path', { d: 'M0 0C-3 -8 -15 -17 -15 -29A15 15 0 1 1 15 -29C15 -17 3 -8 0 0Z', className: 'alt-custom-map__pin-body' }),
        h('g', { transform: 'translate(-8 -37) scale(.6667)', className: 'alt-custom-map__pin-icon' }, iconPath(m.icon || k.icon).map(function (d, i) { return h('path', { key: i, d: d }); }))));
    });

    var popEl = null;
    if (selM && pop) {
      var isVeh = selM.kind === 'vehicle', kk = KIND[selM.kind], t = isVeh ? K.tone(selM.status || 'success') : null;
      var left = Math.max(12, Math.min(box.w - 272, pop.x - 130)), above = pop.y > 190;
      popEl = h('div', { className: 'alt-popover alt-custom-map__pop', role: 'dialog', 'aria-label': selM.label, style: { left: left, top: above ? pop.y - 48 : pop.y + 14, transform: above ? 'translateY(-100%)' : null }, onPointerDown: function (e) { e.stopPropagation(); } },
        h('div', { className: 'alt-custom-map__pop-h' },
          h('span', { className: 'alt-custom-map__pop-k' }, kk ? kk.name : 'Veículo da frota'),
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost alt-custom-map__x', 'aria-label': 'Fechar', onClick: function () { se[1](null); } }, h(A.Icon, { name: 'x', size: 14 }))),
        h('div', { className: 'alt-custom-map__pop-t' }, h('b', { className: 'alt-num' }, selM.label),
          t ? h('span', { className: 'alt-custom-map__st', style: { color: t.c } }, h('i', { className: 'alt-dot' }), selM.statusLabel) : null),
        h('div', { className: 'alt-custom-map__pop-m alt-num' }, selM.detail || 'Em trânsito · sem viagem vinculada'),
        h('div', { className: 'alt-custom-map__pop-a' },
          h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { centralizarPonto(selM.x, selM.y); } }, h(A.Icon, { name: 'target', size: 14 }), 'Centralizar'),
          p.onOpen ? h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { p.onOpen(selM); } }, h(A.Icon, { name: 'external', size: 14 }), 'Abrir viagem') : null));
    }

    var it = info ? K.tone(info.status || 'success') : null;
    return h('div', { className: cx('alt-custom-map alt-panel', p.className), style: Object.assign({ height: p.height || 480 }, p.style), role: 'region', 'aria-label': p.ariaLabel || 'Mapa da viagem' },
      h('div', { ref: bx[0], className: 'alt-custom-map__stage', onPointerDown: onDown, onPointerMove: onMove, onPointerUp: onUp, onPointerCancel: onUp,
        onDoubleClick: function () { zoom(1.5); } },
        h('svg', { className: 'alt-custom-map__svg', viewBox: [vx, vy, w, hh].join(' '), preserveAspectRatio: 'xMidYMid slice', role: 'img', 'aria-label': 'Mapa ilustrativo com rota, cerca e marcadores' },
          h(Base, { layer: layer }),
          fences.map(function (f, i) { return h('circle', { key: 'f' + i, cx: f.x, cy: f.y, r: f.r, className: 'alt-custom-map__fence' }); }),
          h('polyline', { points: pts(route), className: 'alt-custom-map__route' }),
          h('polyline', { points: pts(predicted), className: 'alt-custom-map__pred' }),
          svgMarks)),
      h('div', { className: 'alt-custom-map__fp alt-custom-map__zoom' },
        h('button', { type: 'button', 'aria-label': 'Aproximar', onClick: function () { zoom(1.5); }, disabled: view.z >= 6 }, h(A.Icon, { name: 'plus', size: 16 })),
        h('button', { type: 'button', 'aria-label': 'Afastar', onClick: function () { zoom(1 / 1.5); }, disabled: view.z <= 1 }, h(A.Icon, { name: 'minus', size: 16 }))),
      veh ? h('button', { type: 'button', className: 'alt-custom-map__fp alt-custom-map__loc', 'aria-label': 'Centralizar no veículo', title: 'Centralizar no veículo', onClick: function () { centralizarPonto(veh.x, veh.y); } }, h(A.Icon, { name: 'target', size: 16 })) : null,
      h('button', { type: 'button', className: 'alt-custom-map__fp alt-custom-map__home', 'aria-label': 'Ver mapa inteiro', title: 'Ver mapa inteiro', onClick: function () { vw[1](home); } }, h(A.Icon, { name: 'scan', size: 16 })),
      h('div', { className: 'alt-custom-map__fp alt-custom-map__seg', role: 'group', 'aria-label': 'Camada' },
        [['mapa', 'map', 'Mapa'], ['satelite', 'layers', 'Satélite']].map(function (o) {
          return h('button', { key: o[0], type: 'button', 'aria-pressed': layer === o[0], onClick: function () { setLayer(o[0]); } }, h(A.Icon, { name: o[1], size: 15 }), h('span', null, o[2]));
        })),
      info ? h('div', { className: 'alt-custom-map__fp alt-custom-map__info' },
        h('div', { className: 'alt-custom-map__info-h' }, h('b', { className: 'alt-num' }, info.placa), h('span', { className: 'alt-custom-map__st', style: { color: it.c } }, h('i', { className: 'alt-dot' }), info.statusLabel)),
        h('div', { className: 'alt-custom-map__info-m alt-num' }, info.meta),
        h('div', { className: 'alt-custom-map__lg' },
          h('span', null, h('i', { className: 'alt-custom-map__k alt-custom-map__k--route' }), 'Rota'),
          h('span', null, h('i', { className: 'alt-custom-map__k alt-custom-map__k--pred' }), 'Previsto'),
          h('span', null, h('i', { className: 'alt-custom-map__k alt-custom-map__k--fence' }), 'Cerca'),
          h('span', null, h('i', { className: 'alt-custom-map__k alt-custom-map__k--fleet' }), 'Frota'),
          h('span', null, h('i', { className: 'alt-custom-map__k alt-custom-map__k--crit' }), 'Parada crítica'))) : null,
      popEl,
      h('div', { className: 'alt-custom-map__attr' }, p.attribution || 'Mapa ilustrativo'));
  });
  CustomMap.displayName = 'CustomMap';
  A.CustomMap = CustomMap;
})();


/* ---- CustomSlider ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function clamp(v, a, b) { return Math.min(b, Math.max(a, v)); }
  function same(a, b) { return Array.isArray(a) ? (Array.isArray(b) && a[0] === b[0] && a[1] === b[1]) : a === b; }

  /* Number box: edits locally, confirms on Enter or blur. */
  function NumBox(p) {
    var st = R.useState(String(p.value)); var text = st[0], setText = st[1];
    R.useEffect(function () { setText(String(p.value)); }, [p.value]);
    return h('input', {
      className: 'alt-custom-slider__num alt-num', value: text, inputMode: 'numeric', disabled: p.disabled, 'aria-label': p.label,
      onChange: function (e) {
        var t = e.target.value.replace(/[^\d\-]/g, ''); setText(t);
        var n = parseInt(t, 10); if (!isNaN(n)) p.onDraft(n);
      },
      onKeyDown: function (e) { if (e.key === 'Enter') { e.preventDefault(); p.onCommit(); } },
      onBlur: function () { p.onCommit(); }
    });
  }

  function CustomSlider(p) {
    var min = p.min == null ? 0 : p.min, max = p.max == null ? 100 : p.max, step = p.step || 1;
    var initial = p.value !== undefined ? p.value : (p.defaultValue !== undefined ? p.defaultValue : [20, 60]);
    var cs = R.useState(initial); var committed = p.value !== undefined ? p.value : cs[0];
    var ds = R.useState(p.defaultDraft !== undefined ? p.defaultDraft : initial); var draft = ds[0], setDraft = ds[1];
    var dragging = R.useRef(null), rail = R.useRef(null);
    var range = Array.isArray(committed);
    var ids = R.useRef('alt-cs-' + Math.random().toString(36).slice(2, 7)).current;

    /* External value change resets the draft. */
    var prev = R.useRef(committed);
    R.useEffect(function () { if (!same(prev.current, committed)) { prev.current = committed; setDraft(committed); } });

    var pending = !same(draft, committed);
    var vals = range ? draft : [draft];

    function snap(v) { return clamp(Math.round((v - min) / step) * step + min, min, max); }
    function setThumb(i, v) {
      v = snap(v);
      if (!range) { setDraft(v); return v; }
      var next = draft.slice();
      if (i === 0) next[0] = Math.min(v, next[1]); else next[1] = Math.max(v, next[0]);
      setDraft(next); return next;
    }
    function commit(v) {
      var val = v === undefined ? draft : v;
      if (range) val = [Math.min(val[0], val[1]), Math.max(val[0], val[1])].map(function (x) { return snap(x); });
      else val = snap(val);
      setDraft(val);
      if (p.value === undefined) cs[1](val);
      if (!same(val, committed) && p.callback) p.callback(val, p.name);
    }
    function fromPointer(e) {
      var r = rail.current.getBoundingClientRect();
      return min + clamp((e.clientX - r.left) / r.width, 0, 1) * (max - min);
    }
    function onDown(e) {
      if (p.disabled || e.button !== 0) return;
      var v = fromPointer(e);
      var i = range ? (Math.abs(v - draft[0]) <= Math.abs(v - draft[1]) ? 0 : 1) : 0;
      if (range && draft[0] === draft[1]) i = v > draft[0] ? 1 : 0;
      dragging.current = { i: i, last: setThumb(i, v) };
      e.currentTarget.setPointerCapture(e.pointerId);
      var th = e.currentTarget.querySelectorAll('[role="slider"]')[i];
      if (th) th.focus();
      e.preventDefault();
    }
    function onMove(e) { if (dragging.current) dragging.current.last = setThumb(dragging.current.i, fromPointer(e)); }
    function onUp() { if (dragging.current) { var last = dragging.current.last; dragging.current = null; commit(last); } }
    function onKey(i, e) {
      var cur = vals[i], big = Math.max(step, (max - min) / 10), n = null;
      if (e.key === 'ArrowRight' || e.key === 'ArrowUp') n = cur + step;
      else if (e.key === 'ArrowLeft' || e.key === 'ArrowDown') n = cur - step;
      else if (e.key === 'PageUp') n = cur + big;
      else if (e.key === 'PageDown') n = cur - big;
      else if (e.key === 'Home') n = min;
      else if (e.key === 'End') n = max;
      else if (e.key === 'Enter') { e.preventDefault(); commit(); return; }
      else if (e.key === 'Escape') { setDraft(committed); return; }
      if (n != null) { e.preventDefault(); setThumb(i, n); }
    }
    function pct(v) { return ((clamp(v, min, max) - min) / (max - min)) * 100; }

    var lo = range ? pct(Math.min(vals[0], vals[1])) : 0, hi = pct(range ? Math.max(vals[0], vals[1]) : vals[0]);
    var label = p.label === undefined ? 'Velocidade média (km/h)' : p.label;
    var unit = p.unit ? ' ' + p.unit : '';
    var thumbNames = range ? ['Mínimo', 'Máximo'] : [label || 'Valor'];

    return h('div', { className: cx('alt-custom-slider', pending && 'is-pending', p.disabled && 'is-disabled', p.className), style: p.style, role: 'group', 'aria-labelledby': label ? ids + '-l' : undefined },
      h('div', { className: 'alt-custom-slider__hd' },
        label ? h('span', { id: ids + '-l', className: 'alt-field__label' }, label) : h('span'),
        p.showStatus === false ? null : h('span', { className: 'alt-custom-slider__status', role: 'status' },
          h('span', { className: 'alt-custom-slider__dot', 'aria-hidden': 'true' }),
          pending ? (p.pendingText || 'Não confirmado · solte ou tecle Enter') : (p.confirmedText || 'Confirmado'))),
      h('div', { className: 'alt-custom-slider__row' },
        h('div', { className: 'alt-custom-slider__rail-wrap', onPointerDown: onDown, onPointerMove: onMove, onPointerUp: onUp, onPointerCancel: onUp },
          h('div', { ref: rail, className: 'alt-custom-slider__rail' },
            h('div', { className: 'alt-custom-slider__fill', style: { left: lo + '%', width: (hi - lo) + '%' } }),
            vals.map(function (v, i) {
              return h('span', {
                key: i, role: 'slider', tabIndex: p.disabled ? -1 : 0, 'aria-valuemin': min, 'aria-valuemax': max, 'aria-valuenow': v,
                'aria-valuetext': nf(v) + unit + (pending ? ', não confirmado' : ''), 'aria-label': thumbNames[i], 'aria-disabled': p.disabled || undefined,
                className: 'alt-custom-slider__thumb', style: { left: pct(v) + '%' },
                onKeyDown: function (e) { onKey(i, e); }, onBlur: function () { if (!dragging.current && pending) commit(); }
              });
            })),
          h('div', { className: 'alt-custom-slider__ticks alt-num', 'aria-hidden': 'true' }, h('span', null, nf(min)), h('span', null, nf(max)))),
        h('div', { className: 'alt-custom-slider__nums' },
          vals.map(function (v, i) {
            return [i ? h('span', { key: 'a', className: 'alt-custom-slider__sep' }, 'a') : null,
              h(NumBox, { key: 'n' + i, value: v, disabled: p.disabled, label: thumbNames[i] + (p.unit ? ' (' + p.unit + ')' : ''), onDraft: function (n) { setThumb(i, n); }, onCommit: function () { commit(); } })];
          }))));
  }

  A.CustomSlider = CustomSlider;
})();


/* ---- DashWidgets ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  var Icon = A.Icon;

  /* Moldura comum de widget. */
  function Frame(p) {
    return h('section', { className: cx('alt-dash-widgets__card alt-glass', p.className), style: p.style, 'aria-label': p.title },
      h('header', { className: 'alt-dash-widgets__head' },
        p.icon ? h('span', { className: 'alt-dash-widgets__head-icon', style: { color: p.iconColor || 'var(--teal-light)' } }, h(Icon, { name: p.icon, size: 16 })) : null,
        h('h3', { className: 'alt-dash-widgets__title' }, p.title),
        p.extra || null),
      p.children);
  }

  var DEF_SHORTCUTS = [
    { id: 'helpdesk', label: 'Helpdesk', icon: 'inbox' },
    { id: 'paradas', label: 'Pontos de parada', icon: 'pin' },
    { id: 'bi', label: 'BI operação', icon: 'gauge' },
    { id: 'esg', label: 'Core ESG', icon: 'layers' },
    { id: 'admin', label: 'Administração', icon: 'settings' }
  ];

  function Shortcuts(p) {
    var items = p.items || DEF_SHORTCUTS;
    return h(Frame, { title: p.title || 'Atalhos', className: p.className, style: p.style },
      items.length ? h('div', { className: 'alt-dash-widgets__tiles' },
        items.map(function (s) {
          return h('a', { key: s.id, href: s.href || '#', className: 'alt-dash-widgets__tile', onClick: function (e) { if (!s.href) e.preventDefault(); if (p.onSelect) p.onSelect(s.id, s); } },
            h('span', { className: 'alt-dash-widgets__tile-icon' }, h(Icon, { name: s.icon, size: 20 })), s.label);
        })) : h(A.EmptyStates.Item, { variant: 'empty', bare: true, compact: true, icon: 'lock', title: 'Nenhum atalho disponível', description: 'Seu perfil não tem módulos liberados.', actionLabel: null }));
  }

  function Kpi(p) {
    var trend = p.trend !== undefined ? p.trend : 4.2;
    var up = trend >= 0;
    var t = K.tone(p.trendTone || (up ? 'success' : 'error'));
    var bars = p.bars || [62, 70, 58, 81, 76, 90, 84, 95, 88, 92, 99, 104];
    var max = Math.max.apply(null, bars);
    return h(Frame, { title: p.title || 'Energia solar', icon: p.icon || 'sun', iconColor: p.iconColor || 'var(--signal-warning)', className: p.className, style: p.style },
      h('div', { className: 'alt-dash-widgets__kpi' },
        h('div', { className: 'alt-dash-widgets__kpi-value alt-num' }, p.value || nf(41.8, 1), h('small', null, p.unit || 'MWh')),
        h('div', { className: 'alt-dash-widgets__kpi-row' },
          h('span', { className: 'alt-dash-widgets__sub' }, p.period || 'Últimos 3 meses'),
          h('span', { className: 'alt-chip alt-num', style: { color: t.c, background: t.bg } },
            h(Icon, { name: up ? 'trend-up' : 'trend-down', size: 12 }), (up ? '+' : '') + nf(trend, 1) + '% ' + (p.trendLabel || 'vs 3 meses'))),
        h('div', { className: 'alt-dash-widgets__spark', 'aria-hidden': 'true' },
          bars.map(function (b, i) { return h('span', { key: i, style: { height: Math.round(b / max * 100) + '%' } }); }))));
  }

  var DEF_SERIES = [
    { label: 'Seg', value: 14 }, { label: 'Ter', value: 9 }, { label: 'Qua', value: 17 }, { label: 'Qui', value: 12 },
    { label: 'Sex', value: 21 }, { label: 'Sáb', value: 6 }, { label: 'Dom', value: 4 }
  ];

  function Chart(p) {
    var data = p.data || DEF_SERIES;
    var hv = R.useState(null), hover = hv[0];
    var W = 320, H = 150, padL = 26, padB = 22, padT = 10;
    var max = Math.max.apply(null, data.map(function (d) { return d.value; }).concat([1]));
    var step = Math.ceil(max / 4 / 5) * 5 || 1, top = step * 4;
    var bw = (W - padL) / data.length;
    function y(v) { return padT + (H - padT - padB) * (1 - v / top); }
    return h(Frame, { title: p.title || 'Violações por dia', icon: 'chart-bar', className: p.className, style: p.style,
      extra: h('span', { className: 'alt-dash-widgets__sub alt-num' }, p.subtitle || 'Últimos 7 dias · ' + data.reduce(function (s, d) { return s + d.value; }, 0) + ' no total') },
      h('div', { className: 'alt-dash-widgets__chart' },
        h('svg', { viewBox: '0 0 ' + W + ' ' + H, role: 'img', 'aria-label': (p.title || 'Violações por dia') + ': ' + data.map(function (d) { return d.label + ' ' + d.value; }).join(', ') },
          [0, 1, 2, 3, 4].map(function (i) {
            var v = step * i;
            return h('g', { key: i },
              h('line', { x1: padL, x2: W, y1: y(v), y2: y(v), stroke: 'var(--chart-grid)' }),
              h('text', { x: padL - 6, y: y(v) + 3, textAnchor: 'end', className: 'alt-dash-widgets__axis' }, v));
          }),
          data.map(function (d, i) {
            var x = padL + i * bw + bw * 0.22, w = bw * 0.56, on = hover === i;
            return h('g', { key: d.label, onMouseEnter: function () { hv[1](i); }, onMouseLeave: function () { hv[1](null); } },
              h('rect', { x: padL + i * bw, y: padT, width: bw, height: H - padT - padB, fill: on ? 'var(--hover-wash)' : 'transparent' }),
              h('rect', { x: x, y: y(d.value), width: w, height: Math.max(0, y(0) - y(d.value)), rx: 4, fill: 'var(--chart-1)', opacity: hover === null || on ? 1 : 0.45 }),
              h('text', { x: x + w / 2, y: H - 6, textAnchor: 'middle', className: 'alt-dash-widgets__axis' }, d.label));
          })),
        hover !== null ? h('div', { className: 'alt-dash-widgets__tip alt-num', style: { left: ((padL + (hover + 0.5) * bw) / W * 100) + '%', top: (y(data[hover].value) / H * 100) + '%' } },
          data[hover].label + ' · ' + data[hover].value + ' violações') : null));
  }

  var DEF_LIST = [
    { id: 4821, title: '#4821 Integração TMS fora do ar', meta: 'Sistemas · há 12 min', status: 'Estourado', tone: 'error' },
    { id: 4817, title: '#4817 Ajuste de rota no painel', meta: 'Operação · há 1 h', status: 'No prazo', tone: 'success' },
    { id: 4809, title: '#4809 Acesso ao BI operação', meta: 'Administração · há 3 h', status: 'Em risco', tone: 'warning' },
    { id: 4802, title: '#4802 Cadastro de ponto de parada', meta: 'Cadastros · ontem', status: 'Aguardando', tone: 'info' }
  ];

  function List(p) {
    var items = p.items || DEF_LIST;
    return h(Frame, { title: p.title || 'Chamados recentes', icon: 'inbox', className: p.className, style: p.style,
      extra: p.onSeeAll ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: p.onSeeAll }, 'Ver todos') : null },
      items.length ? h('ul', { className: 'alt-dash-widgets__list' },
        items.map(function (it) {
          var t = K.tone(it.tone);
          return h('li', { key: it.id },
            h('button', { type: 'button', className: 'alt-dash-widgets__row', onClick: function () { if (p.onSelect) p.onSelect(it.id, it); } },
              h('span', { className: 'alt-dash-widgets__row-text' }, h('b', null, it.title), h('small', null, it.meta)),
              it.status ? h('span', { className: 'alt-chip', style: { color: t.c, background: t.bg } }, h(Icon, { name: t.icon, size: 12 }), it.status) : null,
              h(Icon, { name: 'chevron-right', size: 16 })));
        })) : h(A.EmptyStates.Item, { variant: 'empty', bare: true, compact: true, icon: 'inbox', title: p.emptyTitle || 'Nenhum chamado recente', description: null, actionLabel: null }));
  }

  function State(p) {
    return h(Frame, { title: p.title || 'Violações recentes', icon: p.icon || 'alert', className: p.className, style: p.style },
      h(A.EmptyStates.Item, Object.assign({ variant: 'empty', bare: true, compact: true, icon: 'check-circle', tone: 'success',
        title: 'Nenhuma violação no período', description: 'Nada para revisar nas últimas 24 h.', actionLabel: null }, p.state || {})));
  }

  function DashWidgets(p) {
    return h('div', { className: cx('alt-dash-widgets', p.className), style: p.style },
      h(Shortcuts, { items: p.shortcuts, onSelect: p.onShortcut, style: { gridColumn: 'span 5' } }),
      h(Kpi, Object.assign({ style: { gridColumn: 'span 3' } }, p.kpi || {})),
      h(Chart, { data: p.chartData, style: { gridColumn: 'span 4' } }),
      h(List, { items: p.tickets, onSelect: p.onTicket, onSeeAll: p.onSeeAllTickets, style: { gridColumn: 'span 8' } }),
      h(State, { style: { gridColumn: 'span 4' } }));
  }

  DashWidgets.Frame = Frame;
  DashWidgets.Shortcuts = Shortcuts;
  DashWidgets.Kpi = Kpi;
  DashWidgets.Chart = Chart;
  DashWidgets.List = List;
  DashWidgets.State = State;
  A.DashWidgets = DashWidgets;
})();


/* ---- DropFiles ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function truncateFileName(name, max) {
    max = max || 32; name = String(name || '');
    if (name.length <= max) return name;
    var dot = name.lastIndexOf('.'), ext = dot > 0 ? name.slice(dot) : '';
    var keep = Math.max(4, max - ext.length - 1);
    return name.slice(0, Math.ceil(keep * 0.6)) + '…' + name.slice(dot > 0 ? dot - Math.floor(keep * 0.4) : name.length - Math.floor(keep * 0.4), dot > 0 ? dot : undefined) + ext;
  }
  function fmtSize(b) {
    if (b == null) return '';
    if (b < 1024) return nf(b) + ' B';
    if (b < 1024 * 1024) return nf(b / 1024, b < 10240 ? 1 : 0) + ' KB';
    var mb = b / 1024 / 1024; return nf(mb, mb % 1 === 0 ? 0 : 1) + ' MB';
  }
  function extOf(name) { var i = String(name).lastIndexOf('.'); return i > 0 ? String(name).slice(i).toLowerCase() : ''; }
  /* ACCEPTED_FILE_TYPES: [".pdf"] or { "application/pdf": [".pdf"] } */
  function acceptList(t) {
    if (!t) return [];
    if (Array.isArray(t)) return t.map(function (x) { return String(x).toLowerCase(); });
    var out = [];
    Object.keys(t).forEach(function (mime) { out.push(mime.toLowerCase()); (t[mime] || []).forEach(function (e) { out.push(String(e).toLowerCase()); }); });
    return out;
  }
  function accepts(file, list) {
    if (!list.length) return true;
    var e = extOf(file.name), type = String(file.type || '').toLowerCase();
    return list.some(function (a) { return a[0] === '.' ? a === e : (a.slice(-2) === '/*' ? type.indexOf(a.slice(0, -1)) === 0 : a === type); });
  }

  var DEFAULT_CONFIG = { VALID_FILE_SIZE: 5 * 1024 * 1024, MAX_UPLOAD_FILE_COUNT: 1, ACCEPTED_FILE_TYPES: ['.pdf'], label: 'Clique ou arraste o PDF da rota aqui', showFileRejections: true };

  function DropFiles(p) {
    var cfg = Object.assign({}, DEFAULT_CONFIG, p.config || {});
    var maxSize = cfg.VALID_FILE_SIZE, maxCount = cfg.MAX_UPLOAD_FILE_COUNT, list = acceptList(cfg.ACCEPTED_FILE_TYPES);
    var controlled = p.files !== undefined;
    var fs = R.useState(p.defaultFiles || []); var files = controlled ? p.files : fs[0];
    var rs = R.useState(p.defaultRejections || []); var rejections = rs[0], setRejections = rs[1];
    var ds = R.useState(false); var drag = ds[0], setDrag = ds[1];
    var input = R.useRef(null), depth = R.useRef(0);
    var id = R.useRef('alt-df-' + Math.random().toString(36).slice(2, 7)).current;

    function setFiles(next) { if (!controlled) fs[1](next); if (p.setFiles) p.setFiles(next); if (p.onChange) p.onChange(next); }
    function add(list0) {
      var incoming = Array.prototype.slice.call(list0 || []), ok = [], bad = [];
      var room = maxCount ? Math.max(0, maxCount - files.length) : Infinity;
      incoming.forEach(function (f) {
        if (!accepts(f, list)) bad.push({ file: f, message: f.name + ' não é um tipo permitido.' });
        else if (maxSize && f.size > maxSize) bad.push({ file: f, message: f.name + ' tem ' + fmtSize(f.size) + '; o máximo é ' + fmtSize(maxSize) + '.' });
        else if (files.some(function (x) { return x.name === f.name && x.size === f.size; })) bad.push({ file: f, message: f.name + ' já foi adicionado.' });
        else if (ok.length >= room) bad.push({ file: f, message: f.name + ' excede o limite de ' + maxCount + (maxCount === 1 ? ' arquivo.' : ' arquivos.') });
        else ok.push(f);
      });
      setRejections(bad);
      if (ok.length) setFiles(maxCount === 1 ? ok.slice(0, 1) : files.concat(ok));
    }
    function remove(i) { setFiles(files.filter(function (_, j) { return j !== i; })); }

    var hasErr = cfg.showFileRejections !== false && rejections.length > 0;
    var full = maxCount && files.length >= maxCount && maxCount > 1;
    var types = list.filter(function (x) { return x[0] === '.'; }).join(', ') || 'todos';
    var hint = 'Tamanho máximo: ' + (maxSize ? fmtSize(maxSize) : 'sem limite') + ' · Tipos: ' + types + (maxCount ? ' · ' + maxCount + (maxCount === 1 ? ' arquivo' : ' arquivos') : '');

    return h('div', { className: cx('alt-drop-files', p.className), style: p.style },
      h('div', {
        role: 'button', tabIndex: p.disabled || full ? -1 : 0, 'aria-disabled': p.disabled || full || undefined, 'aria-describedby': id + '-hint',
        className: cx('alt-drop-files__zone', drag && 'is-drag', hasErr && 'is-error', (p.disabled || full) && 'is-disabled'),
        onClick: function () { if (!p.disabled && !full && input.current) input.current.click(); },
        onKeyDown: function (e) { if ((e.key === 'Enter' || e.key === ' ') && !p.disabled && !full) { e.preventDefault(); input.current.click(); } },
        onDragEnter: function (e) { e.preventDefault(); depth.current++; setDrag(true); },
        onDragOver: function (e) { e.preventDefault(); },
        onDragLeave: function () { depth.current = Math.max(0, depth.current - 1); if (!depth.current) setDrag(false); },
        onDrop: function (e) { e.preventDefault(); depth.current = 0; setDrag(false); if (!p.disabled && !full) add(e.dataTransfer.files); }
      },
        h('span', { className: 'alt-drop-files__icon', 'aria-hidden': 'true' }, h(A.Icon, { name: 'upload', size: 20 })),
        h('span', { className: 'alt-drop-files__label' }, drag ? 'Solte para adicionar' : full ? 'Limite de arquivos atingido' : cfg.label),
        h('span', { id: id + '-hint', className: 'alt-drop-files__hint alt-num' }, hint),
        h('input', {
          ref: input, type: 'file', hidden: true, multiple: maxCount !== 1, accept: list.join(','), tabIndex: -1,
          onChange: function (e) { add(e.target.files); e.target.value = ''; }
        })),
      hasErr ? h('ul', { className: 'alt-drop-files__errors', role: 'alert' }, rejections.map(function (r, i) {
        return h('li', { key: i }, h(A.Icon, { name: 'x-circle', size: 14 }), r.message || (r.file && r.file.name + ' foi recusado.'));
      })) : null,
      files.length ? h('ul', { className: 'alt-drop-files__list', 'aria-label': 'Arquivos adicionados' }, files.map(function (f, i) {
        var ext = extOf(f.name).replace('.', '').toUpperCase().slice(0, 4) || 'ARQ';
        return h('li', { key: f.name + i, className: 'alt-drop-files__file' },
          h('span', { className: 'alt-drop-files__thumb', 'aria-hidden': 'true' }, ext),
          h('span', { className: 'alt-drop-files__name', title: f.name }, truncateFileName(f.name, p.maxNameLength || 40)),
          h('span', { className: 'alt-drop-files__size alt-num' }, fmtSize(f.size)),
          h('span', { className: 'alt-drop-files__ok' }, h(A.Icon, { name: 'check-circle', size: 14 }), 'Pronto'),
          h('button', { type: 'button', className: 'alt-drop-files__rm', 'aria-label': 'Remover ' + f.name, onClick: function () { remove(i); } }, h(A.Icon, { name: 'x', size: 15 })));
      })) : null,
      p.children);
  }

  DropFiles.truncateFileName = truncateFileName;
  A.DropFiles = DropFiles;
})();


/* ---- EmptyStates ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var Icon = A.Icon;

  var PRESETS = {
    loading: { tone: 'accent', loading: true, title: 'Aguardando permissões', description: 'Verificando o que você pode acessar nesta tela.' },
    forbidden: { tone: 'error', icon: 'lock', code: 'Erro 403', title: 'Acesso negado', description: 'Você não tem acesso a esta tela. Fale com o administrador do CCO.', actionLabel: 'Voltar ao início', actionIcon: 'home' },
    empty: { tone: 'neutral', icon: 'search', title: 'Nenhum registro encontrado', description: 'Nenhuma viagem corresponde aos filtros aplicados.', actionLabel: 'Limpar filtros', actionIcon: 'x' }
  };

  /* Um estado isolado. variant: 'loading' | 'forbidden' | 'empty' (cada prop sobrescreve o preset). */
  function EmptyState(p) {
    var base = PRESETS[p.variant] || PRESETS.empty;
    var c = Object.assign({}, base, K.omit(p, ['variant', 'className', 'style']));
    Object.keys(c).forEach(function (k) { if (c[k] === undefined) c[k] = base[k]; });
    var t = K.tone(c.tone);
    var action = c.action !== undefined ? c.action : (c.actionLabel ? h('button', { type: 'button', className: 'alt-tbtn alt-empty-states__action', onClick: c.onAction },
      c.actionIcon ? h(Icon, { name: c.actionIcon, size: 16 }) : null, c.actionLabel) : null);
    return h('div', {
      className: cx('alt-empty-states__item', c.bare ? null : 'alt-glass', c.compact && 'alt-empty-states__item--compact', p.className), style: p.style,
      role: c.loading ? 'status' : undefined, 'aria-live': c.loading ? 'polite' : undefined
    },
      h('div', { className: 'alt-empty-states__icon', style: { color: t.c, background: t.bg } },
        c.loading ? h('span', { className: 'alt-empty-states__spinner', 'aria-hidden': 'true' }) : h(Icon, { name: c.icon || 'inbox', size: c.compact ? 18 : 20 })),
      c.code ? h('span', { className: 'alt-empty-states__code alt-num' }, c.code) : null,
      h('div', { className: 'alt-empty-states__title' }, c.title),
      c.description ? h('div', { className: 'alt-empty-states__desc' }, c.description) : null,
      action);
  }

  /* Sem variant: mostra os três estados lado a lado. Com variant: só aquele. */
  function EmptyStates(p) {
    if (p.variant) return h(EmptyState, p);
    var items = p.items || [{ variant: 'loading' }, { variant: 'forbidden', onAction: p.onHome }, { variant: 'empty', onAction: p.onClearFilters }];
    return h('div', { className: cx('alt-empty-states', p.className), style: p.style },
      items.map(function (it, i) { return h(EmptyState, Object.assign({ key: i }, it)); }));
  }

  EmptyStates.Item = EmptyState;
  EmptyStates.Loading = function (p) { return h(EmptyState, Object.assign({ variant: 'loading' }, p)); };
  EmptyStates.Forbidden = function (p) { return h(EmptyState, Object.assign({ variant: 'forbidden' }, p)); };
  EmptyStates.NoRecords = function (p) { return h(EmptyState, Object.assign({ variant: 'empty' }, p)); };
  A.EmptyStates = EmptyStates;
})();


/* ---- EnhancedDataGrid ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function norm(s) { return String(s == null ? '' : s).normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase(); }
  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function fmtDateTime(v) { var d = new Date(v); return isNaN(d) ? String(v || '') : pad(d.getDate()) + '/' + pad(d.getMonth() + 1) + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes()); }

  var STATUS_TONES = { 'Em dia': 'success', 'Crítica': 'error', 'Parada': 'warning', 'Sem sinal': 'neutral', 'Em carga': 'info' };

  var DEFAULT_COLUMNS = [
    { field: 'placa', headerName: 'Placa', width: 120, locked: true, mono: true },
    { field: 'motorista', headerName: 'Motorista', sub: 'cco', groupable: true, groupField: 'cco', groupLabel: 'CCO' },
    { field: 'cliente', headerName: 'Cliente', filterable: true, groupable: true },
    { field: 'km', headerName: 'Km rodado', type: 'number', decimals: 1, width: 130 },
    { field: 'status', headerName: 'Status', type: 'status', filterable: true, groupable: true, width: 150 },
    { field: 'posicao', headerName: 'Última posição', type: 'datetime', width: 170 }
  ];

  var DEMO = null;
  function demoRows() {
    if (DEMO) return DEMO;
    var first = ['João', 'Marta', 'Carlos', 'Ana', 'Paulo', 'Rita', 'Bruno', 'Lúcia', 'Pedro', 'Sônia', 'Rafael', 'Juliana', 'Diego', 'Camila'];
    var last = ['Pereira', 'Souza', 'Lima', 'Alves', 'Costa', 'Ramos', 'Gomes', 'Ribeiro', 'Martins'];
    var ccos = ['CCO Sul', 'CCO Norte', 'CCO Leste'];
    var clientes = ['Transportes Sul', 'Vidroporto S.A.', 'Ambev Jundiaí', 'Natura Cajamar'];
    var statuses = ['Em dia', 'Em dia', 'Em dia', 'Crítica', 'Parada', 'Em dia', 'Sem sinal', 'Parada', 'Em carga'];
    var L = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
    var rows = [
      { id: 'ABC-1D23', placa: 'ABC-1D23', motorista: 'João Pereira', cco: 'CCO Sul', cliente: 'Transportes Sul', km: 1284.5, status: 'Em dia', posicao: '2026-09-30T14:31' },
      { id: 'QRT-4F56', placa: 'QRT-4F56', motorista: 'Marta Souza', cco: 'CCO Norte', cliente: 'Transportes Sul', km: 842.0, status: 'Crítica', posicao: '2026-09-30T14:12' },
      { id: 'JHK-7G89', placa: 'JHK-7G89', motorista: 'Carlos Lima', cco: 'CCO Sul', cliente: 'Transportes Sul', km: 2019.3, status: 'Parada', posicao: '2026-09-30T13:58' }
    ];
    var seed = 11;
    function rnd(n) { seed = (seed * 9301 + 49297) % 233280; return Math.floor(seed / 233280 * n); }
    for (var i = rows.length; i < 128; i++) {
      var pl = L[rnd(24)] + L[rnd(24)] + L[rnd(24)] + '-' + rnd(10) + L[rnd(24)] + rnd(10) + rnd(10);
      var mins = 14 * 60 + 37 - (i * 7 + rnd(6));
      rows.push({
        id: pl + i, placa: pl, motorista: first[rnd(first.length)] + ' ' + last[rnd(last.length)], cco: ccos[rnd(3)], cliente: clientes[rnd(4)],
        km: Math.round((40 + rnd(2460) + rnd(10) / 10) * 10) / 10, status: statuses[rnd(statuses.length)],
        posicao: '2026-09-' + (mins < 0 ? '29T' + pad(Math.floor((mins + 1440) / 60)) + ':' + pad((mins + 1440) % 60) : '30T' + pad(Math.floor(mins / 60)) + ':' + pad(mins % 60))
      });
    }
    DEMO = rows; return rows;
  }

  function useOutside(ref, open, onOut) {
    R.useEffect(function () {
      if (!open) return undefined;
      function f(e) { if (ref.current && !ref.current.contains(e.target)) onOut(); }
      function k(e) { if (e.key === 'Escape') onOut(); }
      document.addEventListener('mousedown', f); document.addEventListener('keydown', k);
      return function () { document.removeEventListener('mousedown', f); document.removeEventListener('keydown', k); };
    }, [open]);
  }

  function Check(p) {
    return h('span', { className: cx('alt-enhanced-data-grid__cb', p.state && 'is-on'), 'aria-hidden': 'true' },
      p.state === true ? h(A.Icon, { name: 'check', size: 11, strokeWidth: 3 }) : p.state === 'mixed' ? h(A.Icon, { name: 'minus', size: 11, strokeWidth: 3 }) : null);
  }

  /* Toolbar menu button with popover. */
  function MenuButton(p) {
    var st = R.useState(false), open = st[0], setOpen = st[1], ref = R.useRef(null);
    useOutside(ref, open, function () { setOpen(false); });
    return h('div', { ref: ref, className: 'alt-enhanced-data-grid__mb' },
      h('button', { type: 'button', className: cx('alt-tbtn', p.iconOnly && 'alt-tbtn--icon'), 'aria-haspopup': 'true', 'aria-expanded': open, 'aria-pressed': open || !!p.active, 'aria-label': p.ariaLabel, title: p.ariaLabel,
        onClick: function () { setOpen(!open); } }, h(A.Icon, { name: p.icon, size: 16 }), p.iconOnly ? null : p.label, p.badge ? h('span', { className: 'alt-count' }, p.badge) : null),
      open ? h('div', { className: cx('alt-popover', 'alt-enhanced-data-grid__pop', p.wide && 'alt-enhanced-data-grid__pop--wide') }, p.render(function () { setOpen(false); })) : null);
  }

  function StatusCell(p) {
    var t = K.tone(p.tones[p.value] || 'neutral');
    return h('span', { className: 'alt-enhanced-data-grid__status' }, h('span', { className: 'alt-dot', style: { color: t.c } }), p.value);
  }

  function EnhancedDataGrid(p) {
    var columns0 = p.columns || DEFAULT_COLUMNS;
    var rows = p.rows || demoRows();
    var getId = p.getRowId || function (r) { return r.id; };
    var tones = Object.assign({}, STATUS_TONES, p.statusTones || {});
    var noun = p.entityLabel || ['viagem', 'viagens'];

    var cs = R.useState(columns0.map(function (c) { return Object.assign({}, c); })); var cols = cs[0], setCols = cs[1];
    var ss = R.useState(p.defaultSort === undefined ? { field: 'posicao', dir: 'desc' } : p.defaultSort); var sort = ss[0], setSort = ss[1];
    var qs = R.useState(p.defaultSearch || ''); var q = qs[0], setQ = qs[1];
    var fs = R.useState(p.defaultFilters || (p.rows ? {} : { status: ['Em dia', 'Crítica', 'Parada'], cliente: ['Transportes Sul'] })); var filters = fs[0], setFilters = fs[1];
    var sel = R.useState(p.defaultSelection || (p.rows ? [] : ['QRT-4F56'])); var selected = sel[0], setSelected = sel[1];
    var dn = R.useState(p.defaultDensity || 'standard'); var density = dn[0], setDensity = dn[1];
    var gb = R.useState(p.defaultGroupBy || null); var groupBy = gb[0], setGroupBy = gb[1];
    var cg = R.useState({}); var collapsed = cg[0], setCollapsed = cg[1];
    var pg = R.useState(0); var page = pg[0], setPage = pg[1];
    var ps = R.useState(p.pageSize || 25); var pageSize = ps[0], setPageSize = ps[1];
    var am = R.useState(null); var actionMenu = am[0], setActionMenu = am[1];
    var menuRef = R.useRef(null);
    useOutside(menuRef, !!actionMenu, function () { setActionMenu(null); });

    var visible = cols.filter(function (c) { return !c.hide; });
    var filterCols = cols.filter(function (c) { return c.filterable; });
    var groupCols = [];
    cols.forEach(function (c) { if (c.groupable) groupCols.push({ field: c.groupField || c.field, label: c.groupLabel || c.headerName }); });
    var activeFilters = Object.keys(filters).filter(function (k) { return filters[k] && filters[k].length; });

    function emitSel(next) { setSelected(next); if (p.onSelectionChange) p.onSelectionChange(next); }
    function reset() { setPage(0); }

    /* filter → search → sort */
    var data = R.useMemo(function () {
      var nq = norm(q).trim();
      var out = rows.filter(function (r) {
        for (var i = 0; i < activeFilters.length; i++) { var f = activeFilters[i]; if (filters[f].indexOf(r[f]) < 0) return false; }
        if (!nq) return true;
        return visible.some(function (c) {
          var v = c.type === 'datetime' ? fmtDateTime(r[c.field]) : c.type === 'number' ? nf(r[c.field], c.decimals) : r[c.field];
          return norm(v).indexOf(nq) >= 0 || (c.sub && norm(r[c.sub]).indexOf(nq) >= 0);
        });
      });
      if (sort) {
        var col = cols.filter(function (c) { return c.field === sort.field; })[0] || {};
        var m = sort.dir === 'desc' ? -1 : 1;
        out = out.slice().sort(function (a, b) {
          var x = a[sort.field], y = b[sort.field];
          if (col.type === 'number') return ((x || 0) - (y || 0)) * m;
          return String(x).localeCompare(String(y), 'pt-BR', { numeric: true }) * m;
        });
      }
      if (groupBy) {
        var order = [], buckets = {};
        out.forEach(function (r) { var k = r[groupBy]; if (!buckets[k]) { buckets[k] = []; order.push(k); } buckets[k].push(r); });
        order.sort(function (a, b) { return String(a).localeCompare(String(b), 'pt-BR'); });
        out = [].concat.apply([], order.map(function (k) { return buckets[k]; }));
      }
      return out;
    }, [rows, q, filters, sort, cols, groupBy]);

    var groupCounts = {};
    if (groupBy) data.forEach(function (r) { groupCounts[r[groupBy]] = (groupCounts[r[groupBy]] || 0) + 1; });
    var pages = Math.max(1, Math.ceil(data.length / pageSize));
    var pageIdx = Math.min(page, pages - 1);
    var start = pageIdx * pageSize, pageRows = data.slice(start, start + pageSize);
    var pageIds = pageRows.map(getId);
    var selOnPage = pageIds.filter(function (id) { return selected.indexOf(id) >= 0; }).length;
    var headState = selOnPage === 0 ? false : selOnPage === pageIds.length ? true : 'mixed';
    var rowH = { compact: 36, standard: 44, comfortable: 52 }[density] || 44;

    function toggleSort(c) {
      if (c.sortable === false) return;
      if (!sort || sort.field !== c.field) setSort({ field: c.field, dir: 'asc' });
      else if (sort.dir === 'asc') setSort({ field: c.field, dir: 'desc' });
      else setSort(null);
    }
    function toggleRow(id) { emitSel(selected.indexOf(id) >= 0 ? selected.filter(function (x) { return x !== id; }) : selected.concat([id])); }
    function toggleAll() {
      if (headState === true) emitSel(selected.filter(function (x) { return pageIds.indexOf(x) < 0; }));
      else emitSel(selected.concat(pageIds.filter(function (x) { return selected.indexOf(x) < 0; })));
    }
    function setFilter(field, vals) { var n = Object.assign({}, filters); n[field] = vals; setFilters(n); reset(); }
    function toggleFilterValue(field, v) {
      var cur = filters[field] || [];
      setFilter(field, cur.indexOf(v) >= 0 ? cur.filter(function (x) { return x !== v; }) : cur.concat([v]));
    }

    function cell(c, r) {
      var v = r[c.field];
      if (c.renderCell) return c.renderCell(v, r);
      if (c.type === 'status') return h(StatusCell, { value: v, tones: tones });
      if (c.type === 'number') return v == null ? '—' : nf(v, c.decimals);
      if (c.type === 'datetime') return fmtDateTime(v);
      if (c.sub) return h('span', { className: 'alt-enhanced-data-grid__two' }, h('span', null, v), h('small', null, r[c.sub]));
      return v;
    }

    /* footer pagination: 1 2 3 … 6 */
    function pageList() {
      var out = [], n = pages, c = pageIdx;
      for (var i = 0; i < n; i++) if (i < 3 || i === n - 1 || Math.abs(i - c) <= 1) out.push(i); else if (out[out.length - 1] !== '…') out.push('…');
      return out;
    }

    var subtitle = (activeFilters.length || q ? nf(data.length) + ' de ' : '') + nf(rows.length) + ' ' + (rows.length === 1 ? noun[0] : noun[1]) + (p.updatedAt !== null ? ' · atualizado ' + (p.updatedAt || '14:37') : '');
    var colCount = visible.length + 2;
    var body = [], lastGroup = {};
    pageRows.forEach(function (r, i) {
      if (groupBy && r[groupBy] !== lastGroup.v) {
        lastGroup = { v: r[groupBy] };
        var gk = String(r[groupBy]), isC = !!collapsed[gk];
        var isStatus = cols.some(function (c) { return c.field === groupBy && c.type === 'status'; });
        body.push(h('tr', { key: 'g-' + gk + i, className: 'alt-enhanced-data-grid__group' },
          h('td', { colSpan: colCount },
            h('button', { type: 'button', 'aria-expanded': !isC, onClick: function () { var n = Object.assign({}, collapsed); n[gk] = !isC; setCollapsed(n); } },
              h(A.Icon, { name: isC ? 'chevron-right' : 'chevron-down', size: 15 }),
              isStatus ? h(StatusCell, { value: r[groupBy], tones: tones }) : h('span', null, gk),
              h('span', { className: 'alt-enhanced-data-grid__gcount alt-num' }, nf(groupCounts[r[groupBy]]) + ' ' + (groupCounts[r[groupBy]] === 1 ? noun[0] : noun[1]))))));
      }
      if (groupBy && collapsed[String(r[groupBy])]) return;
      var id = getId(r), isSel = selected.indexOf(id) >= 0;
      body.push(h('tr', { key: id, className: cx(isSel && 'is-selected'), 'aria-selected': isSel, style: { height: rowH }, onClick: p.onRowClick ? function (e) { if (!e.target.closest('button')) p.onRowClick(r); } : undefined },
        h('td', { className: 'alt-enhanced-data-grid__w' },
          h('button', { type: 'button', role: 'checkbox', 'aria-checked': isSel, 'aria-label': 'Selecionar ' + (r[visible[0] && visible[0].field] || id), className: 'alt-enhanced-data-grid__cbb', onClick: function () { toggleRow(id); } }, h(Check, { state: isSel }))),
        visible.map(function (c) {
          return h('td', { key: c.field, className: cx(c.type === 'number' && 'is-num', c.type === 'datetime' && 'is-muted', c.mono && 'is-key', 'alt-num') }, cell(c, r));
        }),
        h('td', { className: 'alt-enhanced-data-grid__w alt-enhanced-data-grid__act' },
          h('button', { type: 'button', className: 'alt-enhanced-data-grid__more', 'aria-label': 'Ações de ' + (r.placa || id), 'aria-haspopup': 'menu', 'aria-expanded': actionMenu && actionMenu.id === id,
            onClick: function (e) { var b = e.currentTarget.getBoundingClientRect(); setActionMenu(actionMenu && actionMenu.id === id ? null : { id: id, row: r, top: b.bottom + 4, right: window.innerWidth - b.right }); } },
            h(A.Icon, { name: 'more', size: 18 })))));
    });

    var rowActions = p.rowActions || [{ id: 'detalhes', label: 'Ver detalhes', icon: 'eye' }, { id: 'rastrear', label: 'Rastrear no mapa', icon: 'map' }, { id: 'ocorrencia', label: 'Registrar ocorrência', icon: 'flag' }];

    return h('section', { className: cx('alt-panel', 'alt-enhanced-data-grid', 'alt-enhanced-data-grid--' + density, p.className), style: p.style, 'aria-label': p.title || 'Viagens em andamento' },
      h('div', { className: 'alt-enhanced-data-grid__tb' },
        h('div', { className: 'alt-enhanced-data-grid__tt' }, h('h3', null, p.title || 'Viagens em andamento'), h('span', { className: 'alt-num' }, subtitle)),
        h('div', { className: 'alt-enhanced-data-grid__sp' }),
        h(A.CampoBusca, { label: null, width: 260, shortcut: '/', placeholder: p.searchPlaceholder || 'Buscar placa, motorista…', ariaLabel: 'Busca rápida', value: q, onChange: function (v) { setQ(v); reset(); } }),
        filterCols.length ? h(MenuButton, { icon: 'filter', label: 'Filtros', badge: activeFilters.length || null, active: activeFilters.length > 0, wide: true,
          render: function () {
            return filterCols.map(function (c) {
              var vals = []; rows.forEach(function (r) { if (vals.indexOf(r[c.field]) < 0) vals.push(r[c.field]); });
              vals.sort(function (a, b) { return String(a).localeCompare(String(b), 'pt-BR'); });
              var cur = filters[c.field] || [];
              return h('div', { key: c.field, className: 'alt-enhanced-data-grid__fgroup' },
                h('div', { className: 'app-label alt-enhanced-data-grid__fhd' }, c.headerName, cur.length ? h('button', { type: 'button', onClick: function () { setFilter(c.field, []); } }, 'Limpar') : null),
                vals.map(function (v) {
                  var on = cur.indexOf(v) >= 0;
                  return h('button', { key: String(v), type: 'button', role: 'menuitemcheckbox', 'aria-checked': on, className: 'alt-menu-item alt-enhanced-data-grid__fopt', onClick: function () { toggleFilterValue(c.field, v); } },
                    h(Check, { state: on }), c.type === 'status' ? h(StatusCell, { value: v, tones: tones }) : h('span', null, v),
                    h('span', { className: 'alt-enhanced-data-grid__fcount alt-num' }, nf(rows.filter(function (r) { return r[c.field] === v; }).length)));
                }));
            });
          } }) : null,
        groupCols.length ? h(MenuButton, { icon: 'layers', label: groupBy ? 'Agrupado' : 'Agrupar', active: !!groupBy,
          render: function (close) {
            return [{ field: null, label: 'Sem agrupamento' }].concat(groupCols).map(function (g) {
              return h('button', { key: String(g.field), type: 'button', role: 'menuitemradio', 'aria-checked': groupBy === g.field, className: 'alt-menu-item', 'aria-selected': groupBy === g.field,
                onClick: function () { setGroupBy(g.field); setCollapsed({}); reset(); close(); } }, g.field ? 'Por ' + g.label.toLowerCase() : g.label);
            });
          } }) : null,
        h(MenuButton, { icon: 'list', iconOnly: true, ariaLabel: 'Densidade das linhas',
          render: function (close) {
            return [['compact', 'Compacta', 36], ['standard', 'Padrão', 44], ['comfortable', 'Confortável', 52]].map(function (d) {
              return h('button', { key: d[0], type: 'button', role: 'menuitemradio', 'aria-checked': density === d[0], 'aria-selected': density === d[0], className: 'alt-menu-item', onClick: function () { setDensity(d[0]); close(); } },
                h('span', null, d[1]), h('span', { className: 'alt-enhanced-data-grid__fcount alt-num' }, d[2] + 'px'));
            });
          } }),
        h(A.ColumnPicker, { align: 'end', itens: cols, callbackChange: setCols, autoFocusSearch: true }),
        p.hideExport ? null : h(A.ExportarXlsx, { variant: 'contained', nome: p.exportName || 'viagens', onExport: p.onExport ? function (name, opt) { return p.onExport(name, data, opt); } : undefined })),
      activeFilters.length ? h('div', { className: 'alt-enhanced-data-grid__chips' },
        activeFilters.map(function (f) {
          var c = cols.filter(function (x) { return x.field === f; })[0] || { headerName: f };
          return h('span', { key: f, className: 'alt-chip alt-enhanced-data-grid__fchip' },
            h('span', { className: 'alt-enhanced-data-grid__fk' }, c.headerName + ':'), filters[f].join(', '),
            h('button', { type: 'button', 'aria-label': 'Remover filtro ' + c.headerName, onClick: function () { setFilter(f, []); } }, h(A.Icon, { name: 'x', size: 12 })));
        }),
        h('button', { type: 'button', className: 'alt-enhanced-data-grid__link', onClick: function () { setFilters({}); reset(); } }, 'Limpar filtros')) : null,
      h('div', { className: 'alt-scroll alt-enhanced-data-grid__wrap', style: p.height ? { maxHeight: p.height } : undefined },
        h('table', { className: 'alt-enhanced-data-grid__table' },
          h('colgroup', null, h('col', { style: { width: 44 } }), visible.map(function (c) { return h('col', { key: c.field, style: c.width ? { width: c.width } : undefined }); }), h('col', { style: { width: 52 } })),
          h('thead', null, h('tr', null,
            h('th', { className: 'alt-enhanced-data-grid__w' },
              h('button', { type: 'button', role: 'checkbox', 'aria-checked': headState, 'aria-label': 'Selecionar página', className: 'alt-enhanced-data-grid__cbb', onClick: toggleAll, disabled: !pageIds.length }, h(Check, { state: headState }))),
            visible.map(function (c) {
              var on = sort && sort.field === c.field;
              return h('th', { key: c.field, className: cx(c.type === 'number' && 'is-num', on && 'is-sorted'), 'aria-sort': on ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none' },
                h('button', { type: 'button', onClick: function () { toggleSort(c); }, title: 'Ordenar por ' + c.headerName },
                  h('span', null, c.headerName),
                  h(A.Icon, { name: on && sort.dir === 'desc' ? 'arrow-down' : 'arrow-up', size: 13, className: cx('alt-enhanced-data-grid__arrow', on && 'is-on') })));
            }),
            h('th', { className: 'alt-enhanced-data-grid__w alt-enhanced-data-grid__act' }, h('span', { style: { position: 'absolute', width: 1, height: 1, overflow: 'hidden', clip: 'rect(0 0 0 0)' } }, 'Ações')))),
          h('tbody', null, body.length ? body : h('tr', { className: 'alt-enhanced-data-grid__empty' }, h('td', { colSpan: colCount },
            h(A.Icon, { name: 'search', size: 20 }),
            h('div', null, 'Nenhuma ' + noun[0] + ' encontrada' + (q ? ' para “' + q + '”' : '') + '.'),
            h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { setQ(''); setFilters({}); reset(); } }, 'Limpar busca e filtros')))))),
      h('div', { className: 'alt-enhanced-data-grid__ft alt-num' },
        h('span', { className: cx('alt-enhanced-data-grid__selcount', selected.length && 'is-on') }, selected.length ? (selected.length === 1 ? '1 selecionada' : nf(selected.length) + ' selecionadas') : 'Nenhuma selecionada'),
        h('label', { className: 'alt-enhanced-data-grid__psize' }, 'Linhas',
          h('select', { value: pageSize, onChange: function (e) { setPageSize(+e.target.value); reset(); } },
            (p.pageSizeOptions || [pageSize === 25 ? 10 : pageSize, 25, 50, 100]).filter(function (v, i, a) { return a.indexOf(v) === i; }).map(function (n) { return h('option', { key: n, value: n }, n); }))),
        h('span', null, data.length ? (start + 1) + '–' + Math.min(start + pageSize, data.length) + ' de ' + nf(data.length) : '0 de 0'),
        h('nav', { className: 'alt-enhanced-data-grid__pg', 'aria-label': 'Paginação' },
          h('button', { type: 'button', disabled: pageIdx === 0, 'aria-label': 'Página anterior', onClick: function () { setPage(pageIdx - 1); } }, h(A.Icon, { name: 'chevron-left', size: 15 })),
          pageList().map(function (x, i) {
            return x === '…' ? h('span', { key: 'e' + i, className: 'alt-enhanced-data-grid__gap' }, '…')
              : h('button', { key: x, type: 'button', 'aria-current': x === pageIdx ? 'page' : undefined, className: cx(x === pageIdx && 'is-current'), onClick: function () { setPage(x); } }, x + 1);
          }),
          h('button', { type: 'button', disabled: pageIdx >= pages - 1, 'aria-label': 'Próxima página', onClick: function () { setPage(pageIdx + 1); } }, h(A.Icon, { name: 'chevron-right', size: 15 })))),
      actionMenu ? h('div', { ref: menuRef, role: 'menu', className: 'alt-popover alt-enhanced-data-grid__rowmenu', style: { top: actionMenu.top, right: actionMenu.right } },
        rowActions.map(function (a) {
          return h('button', { key: a.id, type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: function () { setActionMenu(null); if (p.onRowAction) p.onRowAction(a.id, actionMenu.row); } },
            h(A.Icon, { name: a.icon || 'circle', size: 16 }), a.label);
        })) : null);
  }

  EnhancedDataGrid.demoRows = demoRows;
  EnhancedDataGrid.defaultColumns = DEFAULT_COLUMNS;
  A.EnhancedDataGrid = EnhancedDataGrid;
})();


/* ---- EnhancedDataTable ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function norm(s) { return String(s == null ? '' : s).normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase(); }
  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function toDate(v) { if (v instanceof Date) return v; var d = new Date(String(v).length === 10 ? v + 'T12:00:00' : v); return d; }
  function fmt(c, v) {
    if (v == null || v === '') return '—';
    if (c.type === 'number') return nf(v, c.decimals || 0);
    if (c.type === 'date' || c.type === 'datetime') {
      var d = toDate(v); if (isNaN(d)) return String(v);
      var s = pad(d.getDate()) + '/' + pad(d.getMonth() + 1) + '/' + d.getFullYear();
      return c.type === 'datetime' ? s + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes()) : s;
    }
    if (c.type === 'simnao') return v ? 'Sim' : 'Não';
    return String(v);
  }

  var DEFAULT_COLUMNS = [
    { field: 'rota', label: 'Rota', sticky: true, width: 110 },
    { field: 'origem', label: 'Origem' },
    { field: 'destino', label: 'Destino' },
    { field: 'km', label: 'Km', type: 'number', decimals: 1, width: 120 },
    { field: 'ativa', label: 'Ativa', type: 'simnao', width: 150 },
    { field: 'atualizada', label: 'Atualizada em', type: 'datetime', width: 200 }
  ];
  var DEMO = null;
  function demoData() {
    if (DEMO) return DEMO;
    var cities = ['Campinas', 'Santos', 'Jundiaí', 'Sorocaba', 'Limeira', 'Piracicaba', 'Americana', 'São Paulo', 'Paulínia', 'Sumaré', 'Indaiatuba', 'Itu', 'Bragança Paulista', 'Mogi Mirim', 'Rio Claro', 'Valinhos'];
    var out = [
      { rota: 'R-102', origem: 'Campinas', destino: 'Santos', km: 168.4, ativa: true, atualizada: '2026-09-28T09:12' },
      { rota: 'R-117', origem: 'Jundiaí', destino: 'Sorocaba', km: 74.9, ativa: true, atualizada: '2026-09-29T16:40' },
      { rota: 'R-121', origem: 'Limeira', destino: 'Piracicaba', km: 41.2, ativa: true, excluida: true, atualizada: '2026-09-30T08:05' },
      { rota: 'R-130', origem: 'Americana', destino: 'São Paulo', km: 127.0, ativa: false, atualizada: '2026-09-30T11:27' }
    ];
    var seed = 5;
    function rnd(n) { seed = (seed * 9301 + 49297) % 233280; return Math.floor(seed / 233280 * n); }
    for (var i = 0; i < 44; i++) {
      var o = cities[rnd(cities.length)], d = cities[(cities.indexOf(o) + 1 + rnd(cities.length - 1)) % cities.length];
      var day = 1 + rnd(30), hr = 6 + rnd(13), mi = rnd(60);
      out.push({ rota: 'R-' + (131 + i * 3 + rnd(3)), origem: o, destino: d, km: Math.round((12 + rnd(260) + rnd(10) / 10) * 10) / 10, ativa: rnd(5) > 0, excluida: rnd(14) === 0, atualizada: '2026-09-' + pad(day) + 'T' + pad(hr) + ':' + pad(mi) });
    }
    DEMO = out; return out;
  }

  var PERIODS = [['', 'Período'], ['hoje', 'Hoje'], ['7', 'Últimos 7 dias'], ['30', 'Últimos 30 dias']];

  function FilterCell(p) {
    var c = p.col, v = p.value, active = v != null && v !== '';
    function clear() { p.onChange(''); }
    var x = active ? h('button', { type: 'button', className: 'alt-enhanced-data-table__fx', 'aria-label': 'Limpar filtro ' + c.label, onClick: clear }, h(A.Icon, { name: 'x', size: 12 })) : null;
    var cls = cx('alt-enhanced-data-table__fi', active && 'is-active');
    if (c.filter === false) return null;
    if (c.type === 'simnao') {
      return h('div', { className: cls },
        h('select', { value: v || '', 'aria-label': 'Filtrar ' + c.label, onChange: function (e) { p.onChange(e.target.value); } },
          h('option', { value: '' }, 'Sim/Não'), h('option', { value: 'sim' }, 'Sim'), h('option', { value: 'nao' }, 'Não')),
        active ? x : h(A.Icon, { name: 'chevron-down', size: 13 }));
    }
    if (c.type === 'date' || c.type === 'datetime') {
      return h('div', { className: cls }, h(A.Icon, { name: 'calendar', size: 13 }),
        h('select', { value: v || '', 'aria-label': 'Filtrar ' + c.label, onChange: function (e) { p.onChange(e.target.value); } },
          PERIODS.map(function (o) { return h('option', { key: o[0], value: o[0] }, o[1]); })), x);
    }
    if (c.type === 'number') {
      return h('div', { className: cls }, h('span', { className: 'alt-enhanced-data-table__op', 'aria-hidden': 'true' }, '≥'),
        h('input', { value: v || '', inputMode: 'decimal', placeholder: 'mín.', 'aria-label': 'Filtrar ' + c.label + ' maior ou igual a', onChange: function (e) { p.onChange(e.target.value.replace(/[^\d,.\-]/g, '')); } }), x);
    }
    return h('div', { className: cls },
      h('input', { value: v || '', placeholder: 'Contém…', 'aria-label': 'Filtrar ' + c.label + ' contendo', onChange: function (e) { p.onChange(e.target.value); } }), x);
  }

  function EnhancedDataTable(p) {
    var columns = p.columns || DEFAULT_COLUMNS;
    var data = p.data || demoData();
    var noun = p.entityLabel || ['rota', 'rotas'];
    var statusField = p.statusField || (p.columns ? null : 'ativa');
    var deletedField = p.deletedField || 'excluida';
    var ref = p.referenceDate ? toDate(p.referenceDate) : (p.data ? new Date() : toDate('2026-09-30T23:59'));
    var rpp0 = p.rowsPerPage || 10;
    var showFiltersProp = p.filters !== false;

    var ss = R.useState(p.defaultSort === undefined ? { field: columns[0].field, dir: 'asc' } : p.defaultSort); var sort = ss[0], setSort = ss[1];
    var qs = R.useState(''); var q = qs[0], setQ = qs[1];
    var fs = R.useState(p.defaultFilterValues || (p.data ? {} : { km: '40' })); var fv = fs[0], setFv = fs[1];
    var fo = R.useState(p.defaultFiltersOpen !== undefined ? p.defaultFiltersOpen : true); var filtersOpen = fo[0], setFiltersOpen = fo[1];
    var si = R.useState(p.selectedRowIndex !== undefined ? p.selectedRowIndex : (p.data ? null : 1)); var selIdx = p.selectedRowIndex !== undefined && p.callbackRowClick === undefined ? p.selectedRowIndex : si[0];
    var pg = R.useState(0); var page = pg[0], setPage = pg[1];
    var rp = R.useState(rpp0); var rpp = rp[0], setRpp = rp[1];
    R.useEffect(function () { if (p.selectedRowIndex !== undefined) si[1](p.selectedRowIndex); }, [p.selectedRowIndex]);

    var activeKeys = Object.keys(fv).filter(function (k) { return fv[k] != null && fv[k] !== ''; });
    function setFilter(k, v) { var n = Object.assign({}, fv); n[k] = v; setFv(n); setPage(0); }

    var rows = R.useMemo(function () {
      var nq = norm(q).trim();
      var list = data.map(function (r, i) { return { r: r, i: i }; }).filter(function (o) {
        var r = o.r;
        for (var j = 0; j < columns.length; j++) {
          var c = columns[j], f = fv[c.field];
          if (f == null || f === '') continue;
          var v = r[c.field];
          if (c.type === 'number') { var n = parseFloat(String(f).replace(',', '.')); if (!isNaN(n) && !(v >= n)) return false; }
          else if (c.type === 'simnao') { if ((f === 'sim') !== !!v) return false; }
          else if (c.type === 'date' || c.type === 'datetime') {
            var d = toDate(v), diff = (ref - d) / 86400000;
            if (f === 'hoje' ? d.toDateString() !== ref.toDateString() : !(diff >= 0 && diff <= +f)) return false;
          } else if (norm(v).indexOf(norm(f)) < 0) return false;
        }
        if (!nq) return true;
        return columns.some(function (c) { return norm(fmt(c, r[c.field])).indexOf(nq) >= 0; });
      });
      if (sort) {
        var col = columns.filter(function (c) { return c.field === sort.field; })[0] || {}, m = sort.dir === 'desc' ? -1 : 1;
        list.sort(function (a, b) {
          var x = a.r[sort.field], y = b.r[sort.field];
          if (col.type === 'number' || col.type === 'simnao') return ((+x || 0) - (+y || 0)) * m;
          if (col.type === 'date' || col.type === 'datetime') return (toDate(x) - toDate(y)) * m;
          return String(x).localeCompare(String(y), 'pt-BR', { numeric: true }) * m;
        });
      }
      return list;
    }, [data, q, fv, sort, columns]);

    var paginate = p.pagination !== false;
    var pages = paginate ? Math.max(1, Math.ceil(rows.length / rpp)) : 1;
    var pIdx = Math.min(page, pages - 1), start = paginate ? pIdx * rpp : 0;
    var shown = paginate ? rows.slice(start, start + rpp) : rows;

    function toggleSort(c) {
      if (!sort || sort.field !== c.field) setSort({ field: c.field, dir: 'asc' });
      else if (sort.dir === 'asc') setSort({ field: c.field, dir: 'desc' });
      else setSort(null);
    }
    function exportCSV() {
      if (typeof p.exportCSV === 'function') { p.exportCSV(rows.map(function (o) { return o.r; }), columns); return; }
      var esc = function (s) { s = String(s); return /[";\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s; };
      var lines = [columns.map(function (c) { return esc(c.label); }).join(';')].concat(rows.map(function (o) {
        return columns.map(function (c) { var v = fmt(c, o.r[c.field]); return esc(v === '—' ? '' : v); }).join(';');
      }));
      var blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
      var url = URL.createObjectURL(blob), a = document.createElement('a');
      a.href = url; a.download = (p.exportName || noun[1]) + '.csv'; document.body.appendChild(a); a.click(); a.remove();
      setTimeout(function () { URL.revokeObjectURL(url); }, 0);
    }

    function cellContent(c, r) {
      if (c.render) return c.render(r[c.field], r);
      var isDel = !!r[deletedField];
      if (c.type === 'simnao' || c.field === statusField) {
        if (c.field === statusField && isDel) return h('span', { className: 'alt-enhanced-data-table__st is-del' }, h('span', { className: 'alt-dot' }), 'Excluída');
        return h('span', { className: cx('alt-enhanced-data-table__st', r[c.field] ? 'is-yes' : 'is-no') }, h('span', { className: 'alt-dot' }), r[c.field] ? 'Sim' : 'Não');
      }
      return fmt(c, r[c.field]);
    }

    var subtitle = nf(rows.length) + ' ' + (rows.length === 1 ? noun[0] : noun[1]) + (activeKeys.length ? ' · ' + activeKeys.length + (activeKeys.length === 1 ? ' filtro ativo' : ' filtros ativos') : '');

    return h('section', { className: cx('alt-panel', 'alt-enhanced-data-table', p.dense && 'alt-enhanced-data-table--dense', p.rowHover === false && 'alt-enhanced-data-table--nohover', p.className), style: p.style },
      p.toolbar === false ? null : h('div', { className: 'alt-enhanced-data-table__tb' },
        h('div', { className: 'alt-enhanced-data-table__tt' }, h('h3', null, p.title || 'Rotas cadastradas'), h('span', { className: 'alt-num' }, subtitle)),
        p.search === false ? null : h(A.CampoBusca, { label: null, shortcut: null, width: 220, placeholder: p.searchPlaceholder || 'Buscar rota, cidade…', ariaLabel: 'Buscar na tabela', value: q, onChange: function (v) { setQ(v); setPage(0); } }),
        showFiltersProp ? h('button', { type: 'button', className: 'alt-tbtn', 'aria-pressed': filtersOpen, onClick: function () { setFiltersOpen(!filtersOpen); } },
          h(A.Icon, { name: 'filter', size: 16 }), 'Filtros', activeKeys.length ? h('span', { className: 'alt-count' }, activeKeys.length) : null) : null,
        p.exportCSV === false ? null : h('button', { type: 'button', className: 'alt-tbtn', onClick: exportCSV }, h(A.Icon, { name: 'download', size: 16 }), 'Exportar CSV'),
        p.toolbarActions || null),
      h('div', { className: 'alt-scroll alt-enhanced-data-table__wrap', style: p.height ? { maxHeight: p.height } : undefined },
        h('table', { className: 'alt-enhanced-data-table__table' },
          h('colgroup', null, columns.map(function (c) { return h('col', { key: c.field, style: c.width ? { width: c.width } : undefined }); })),
          h('thead', null,
            h('tr', null, columns.map(function (c) {
              var on = sort && sort.field === c.field;
              return h('th', { key: c.field, className: cx(c.sticky && 'is-sticky', c.type === 'number' && 'is-num', on && 'is-sorted'), 'aria-sort': on ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none' },
                h('button', { type: 'button', onClick: function () { toggleSort(c); } }, h('span', null, c.label),
                  h(A.Icon, { name: on && sort.dir === 'desc' ? 'arrow-down' : 'arrow-up', size: 13, className: cx('alt-enhanced-data-table__arrow', on && 'is-on') })));
            })),
            showFiltersProp && filtersOpen ? h('tr', { className: 'alt-enhanced-data-table__frow' }, columns.map(function (c) {
              return h('th', { key: c.field, className: cx(c.sticky && 'is-sticky') }, h(FilterCell, { col: c, value: fv[c.field], onChange: function (v) { setFilter(c.field, v); } }));
            })) : null),
          h('tbody', null, shown.length ? shown.map(function (o) {
            var r = o.r, isSel = o.i === selIdx, isDel = !!r[deletedField];
            return h('tr', {
              key: o.i, className: cx(isSel && 'is-selected', isDel && 'is-deleted'), 'aria-selected': isSel, tabIndex: 0,
              onClick: function () { si[1](o.i); if (p.callbackRowClick) p.callbackRowClick(r, o.i); },
              onKeyDown: function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); si[1](o.i); if (p.callbackRowClick) p.callbackRowClick(r, o.i); } }
            }, columns.map(function (c) {
              return h('td', { key: c.field, className: cx(c.sticky && 'is-sticky', c.type === 'number' && 'is-num', (c.type === 'simnao' || c.field === statusField) && 'is-status', 'alt-num') }, cellContent(c, r));
            }));
          }) : h('tr', { className: 'alt-enhanced-data-table__empty' }, h('td', { colSpan: columns.length }, 'Nenhum registro encontrado com os filtros atuais.'))))),
      paginate ? h('div', { className: 'alt-enhanced-data-table__ft alt-num' },
        h('label', { className: 'alt-enhanced-data-table__rpp' }, 'Linhas por página',
          h('select', { value: rpp, onChange: function (e) { setRpp(+e.target.value); setPage(0); } },
            (p.rowsPerPageOptions || [10, 25, 50]).concat(rpp0).filter(function (v, i, a) { return a.indexOf(v) === i; }).sort(function (a, b) { return a - b; }).map(function (n) { return h('option', { key: n, value: n }, n); }))),
        h('span', null, rows.length ? (start + 1) + '–' + Math.min(start + rpp, rows.length) + ' de ' + nf(rows.length) : '0 de 0'),
        h('div', { className: 'alt-enhanced-data-table__pg' },
          h('button', { type: 'button', disabled: pIdx === 0, 'aria-label': 'Página anterior', onClick: function () { setPage(pIdx - 1); } }, h(A.Icon, { name: 'chevron-left', size: 15 })),
          h('button', { type: 'button', disabled: pIdx >= pages - 1, 'aria-label': 'Próxima página', onClick: function () { setPage(pIdx + 1); } }, h(A.Icon, { name: 'chevron-right', size: 15 })))) : null);
  }

  EnhancedDataTable.demoData = demoData;
  A.EnhancedDataTable = EnhancedDataTable;
})();


/* ---- ExportarXlsx ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function pad(n) { return (n < 10 ? '0' : '') + n; }
  /* <nome>-AAAA-MM-DD_HHmm.xlsx */
  function fileName(nome, d) {
    d = d || new Date();
    var base = String(nome || 'arquivo').trim().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^\w-]+/g, '-').replace(/^-+|-+$/g, '').toLowerCase() || 'arquivo';
    return base + '-' + d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + '_' + pad(d.getHours()) + pad(d.getMinutes()) + '.xlsx';
  }

  function Spinner() { return h('span', { className: 'alt-exportar-xlsx__spin', 'aria-hidden': 'true' }); }

  function ExportarXlsx(p) {
    var ctl = p.state !== undefined;
    var ss = R.useState(p.defaultState || 'idle'); var state = ctl ? p.state : ss[0];
    var ms = R.useState(p.defaultMenuOpen ? 'menu' : p.defaultConfirmOpen ? 'confirm' : null); var pop = ms[0], setPop = ms[1];
    var pend = R.useRef(null), root = R.useRef(null), timer = R.useRef(null);
    var opts = p.options;
    var needsConfirm = p.confirmAbove != null && p.rowCount != null && p.rowCount > p.confirmAbove;

    R.useEffect(function () { return function () { clearTimeout(timer.current); }; }, []);
    R.useEffect(function () {
      if (!pop) return undefined;
      function f(e) { if (root.current && !root.current.contains(e.target)) setPop(null); }
      function k(e) { if (e.key === 'Escape') setPop(null); }
      document.addEventListener('mousedown', f); document.addEventListener('keydown', k);
      return function () { document.removeEventListener('mousedown', f); document.removeEventListener('keydown', k); };
    }, [pop]);

    function setState(s) { if (!ctl) ss[1](s); if (p.onStateChange) p.onStateChange(s); }
    function run(opt) {
      setPop(null);
      var name = fileName(p.nome, new Date());
      setState('loading');
      var r;
      try { r = p.onExport ? p.onExport(name, opt && opt.value) : new Promise(function (ok) { setTimeout(ok, 1200); }); }
      catch (err) { r = Promise.reject(err); }
      Promise.resolve(r).then(function () {
        setState('success');
        clearTimeout(timer.current);
        timer.current = setTimeout(function () { setState('idle'); }, p.successDuration || 2500);
      }, function () { setState('error'); });
    }
    function click() {
      if (state === 'loading') return;
      if (opts && opts.length) { setPop(pop === 'menu' ? null : 'menu'); return; }
      if (needsConfirm) { pend.current = null; setPop(pop === 'confirm' ? null : 'confirm'); return; }
      run(null);
    }
    function choose(o) {
      if (needsConfirm && o.value !== 'pagina') { pend.current = o; setPop('confirm'); return; }
      run(o);
    }

    var label = p.label || 'Exportar';
    var content;
    if (state === 'loading') content = [h(Spinner, { key: 's' }), p.loadingLabel || 'Gerando…'];
    else if (state === 'success') content = [h(A.Icon, { key: 'i', name: 'check-circle', size: 16 }), 'Exportado'];
    else if (state === 'error') content = [h(A.Icon, { key: 'i', name: 'alert', size: 16 }), 'Falha ao exportar · Tentar de novo'];
    else content = [h(A.Icon, { key: 'i', name: 'download', size: 16 }), label, opts && opts.length ? h(A.Icon, { key: 'c', name: 'chevron-down', size: 14, className: 'alt-exportar-xlsx__chev' }) : null];

    var contained = p.variant === 'contained';
    return h('div', { ref: root, className: cx('alt-exportar-xlsx', p.className), style: p.style },
      h('button', {
        type: 'button', disabled: p.disabled, 'aria-busy': state === 'loading' || undefined, 'aria-haspopup': opts && opts.length ? 'menu' : needsConfirm ? 'dialog' : undefined,
        'aria-expanded': pop ? true : (opts || needsConfirm ? false : undefined),
        className: cx('alt-exportar-xlsx__btn', contained ? 'alt-exportar-xlsx__btn--contained' : 'alt-tbtn', 'is-' + state),
        onClick: state === 'error' ? function () { run(pend.current); } : click,
        title: state === 'idle' ? 'Exporta para planilha .xlsx' : undefined
      }, content),
      h('span', { className: 'alt-exportar-xlsx__sr', role: 'status', 'aria-live': 'polite' }, state === 'loading' ? 'Gerando planilha' : state === 'success' ? 'Planilha exportada' : state === 'error' ? 'Falha ao exportar' : ''),
      pop === 'menu' ? h('div', { className: cx('alt-popover', 'alt-exportar-xlsx__pop', p.align === 'start' && 'alt-exportar-xlsx__pop--start'), role: 'menu' },
        opts.map(function (o) {
          return h('button', { key: o.value, type: 'button', role: 'menuitem', className: 'alt-menu-item alt-exportar-xlsx__item', onClick: function () { choose(o); } },
            h(A.Icon, { name: o.icon || 'spreadsheet', size: 16 }),
            h('span', { className: 'alt-exportar-xlsx__item-text' }, h('span', null, o.label), o.description ? h('small', { className: 'alt-num' }, o.description) : null));
        })) : null,
      pop === 'confirm' ? h('div', { className: cx('alt-popover', 'alt-exportar-xlsx__pop', 'alt-exportar-xlsx__dlg', p.align === 'start' && 'alt-exportar-xlsx__pop--start'), role: 'dialog', 'aria-label': 'Confirmar exportação' },
        h('div', { className: 'alt-exportar-xlsx__dlg-title' }, h(A.Icon, { name: 'info', size: 16 }), 'Exportar ' + nf(p.rowCount) + ' linhas?'),
        h('p', null, p.confirmText || 'A planilha pode levar alguns segundos para ser gerada. Você pode continuar usando a tela.'),
        h('div', { className: 'alt-exportar-xlsx__dlg-acts' },
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { setPop(null); } }, 'Cancelar'),
          h('button', { type: 'button', className: 'alt-exportar-xlsx__btn alt-exportar-xlsx__btn--contained', onClick: function () { run(pend.current); } }, h(A.Icon, { name: 'download', size: 16 }), 'Exportar'))) : null);
  }

  ExportarXlsx.fileName = fileName;
  A.ExportarXlsx = ExportarXlsx;
})();


/* ---- FilterAutocomplete ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function norm(s) { return String(s == null ? '' : s).normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase(); }
  function optValue(o) { return o && typeof o === 'object' ? o.value : o; }
  function optLabel(o) { return o && typeof o === 'object' ? (o.label != null ? o.label : String(o.value)) : String(o); }

  function useOutside(ref, open, onOut) {
    R.useEffect(function () {
      if (!open) return undefined;
      function f(e) { if (ref.current && !ref.current.contains(e.target)) onOut(); }
      document.addEventListener('mousedown', f);
      return function () { document.removeEventListener('mousedown', f); };
    }, [open]);
  }

  function Check(p) {
    return h('span', { className: cx('alt-filter-autocomplete__cb', p.on && 'is-on', p.disabled && 'is-disabled'), 'aria-hidden': 'true' },
      p.on ? h(A.Icon, { name: 'check', size: 11, strokeWidth: 3 }) : null);
  }

  var DEFAULT_OPTIONS = ['ABC-1D23', 'QRT-4F56', 'JHK-7G89', 'LMN-0P12', 'RST-3U45', 'GHI-6J78', 'XYZ-9A01', 'DEF-2B34'];

  function FilterAutocomplete(p) {
    var name = p.name || 'placa';
    var options = p.options || DEFAULT_OPTIONS;
    var max = p.maxSelections === undefined ? 50 : p.maxSelections;
    var limitTags = p.limitTags === undefined ? 1 : p.limitTags;
    var renderLabel = p.renderOptionLabel || optLabel;
    var controlled = p.values !== undefined;
    var vs = R.useState(p.defaultValues || []); var inner = vs[0], setInner = vs[1];
    var values = (controlled ? p.values : inner) || [];
    var os = R.useState(!!p.defaultOpen); var open = os[0], setOpen = os[1];
    var qs = R.useState(''); var q = qs[0], setQ = qs[1];
    var as = R.useState(p.defaultActiveIndex == null ? -1 : p.defaultActiveIndex); var active = as[0], setActive = as[1];
    var root = R.useRef(null), input = R.useRef(null), list = R.useRef(null);
    var idRef = R.useRef('alt-fa-' + Math.random().toString(36).slice(2, 8)), id = idRef.current;
    useOutside(root, open, function () { setOpen(false); setQ(''); });

    var atLimit = max != null && values.length >= max;
    var filtered = R.useMemo(function () {
      var n = norm(q);
      return n ? options.filter(function (o) { return norm(renderLabel(o)).indexOf(n) >= 0; }) : options;
    }, [options, q]);

    function emit(next) {
      if (!controlled) setInner(next);
      if (p.onChange) p.onChange(name, next.length ? next : null);
    }
    function isSel(o) { return values.indexOf(optValue(o)) >= 0; }
    function toggle(o) {
      var v = optValue(o);
      if (isSel(o)) emit(values.filter(function (x) { return x !== v; }));
      else if (!atLimit) emit(values.concat([v]));
    }
    function labelOf(v) {
      for (var i = 0; i < options.length; i++) if (optValue(options[i]) === v) return renderLabel(options[i]);
      return String(v);
    }
    function onKey(e) {
      if (e.key === 'ArrowDown') { e.preventDefault(); setOpen(true); setActive(Math.min(filtered.length - 1, active + 1)); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); setActive(Math.max(0, active - 1)); }
      else if (e.key === 'Enter' && open && filtered[active]) { e.preventDefault(); toggle(filtered[active]); }
      else if (e.key === 'Escape') { setOpen(false); setQ(''); }
      else if (e.key === 'Backspace' && !q && values.length) emit(values.slice(0, -1));
    }
    R.useEffect(function () {
      if (!open || active < 0 || !list.current) return;
      var el = list.current.querySelector('[data-idx="' + active + '"]');
      if (el && el.scrollIntoView) el.scrollIntoView({ block: 'nearest' });
    }, [active, open]);

    var shown = values.slice(0, limitTags);
    var rest = values.length - shown.length;
    var error = p.error || (atLimit && max != null);
    var helper = p.helperText || (atLimit ? 'Limite de ' + nf(max) + ' seleções atingido.' : null);

    return h('div', { ref: root, className: cx('alt-filter-autocomplete', p.className), style: p.style },
      p.label !== null ? h('label', { className: 'alt-field__label', htmlFor: id }, p.label || 'Placa') : null,
      h('div', {
        className: cx('alt-ctl', 'alt-filter-autocomplete__ctl', error && 'alt-filter-autocomplete__ctl--error', open && 'is-open', p.disabled && 'alt-ctl--disabled'),
        onMouseDown: function (e) { if (e.target === e.currentTarget) { e.preventDefault(); input.current && input.current.focus(); setOpen(true); } }
      },
        shown.map(function (v) {
          return h('span', { key: String(v), className: 'alt-filter-autocomplete__tag' },
            h('span', { className: 'alt-filter-autocomplete__tag-text' }, labelOf(v)),
            h('button', { type: 'button', 'aria-label': 'Remover ' + labelOf(v), onClick: function () { emit(values.filter(function (x) { return x !== v; })); } }, h(A.Icon, { name: 'x', size: 12 })));
        }),
        rest > 0 ? h('span', { className: 'alt-filter-autocomplete__more alt-num', title: rest + ' outras seleções' }, '+' + nf(rest)) : null,
        h('input', {
          id: id, ref: input, value: q, disabled: p.disabled, role: 'combobox', 'aria-expanded': open, 'aria-controls': id + '-list', 'aria-autocomplete': 'list',
          'aria-activedescendant': open && active >= 0 ? id + '-o' + active : undefined,
          placeholder: values.length ? '' : (p.placeholder || 'Selecionar…'),
          onChange: function (e) { setQ(e.target.value); setOpen(true); setActive(0); },
          onFocus: function () { setOpen(true); }, onKeyDown: onKey
        }),
        h('span', { className: 'alt-filter-autocomplete__adorn' },
          values.length ? h('button', { type: 'button', className: 'alt-filter-autocomplete__ib', 'aria-label': 'Limpar seleção', onClick: function () { emit([]); } }, h(A.Icon, { name: 'x', size: 15 })) : null,
          h('button', { type: 'button', tabIndex: -1, className: 'alt-filter-autocomplete__ib', 'aria-label': open ? 'Fechar lista' : 'Abrir lista', onClick: function () { setOpen(!open); if (!open && input.current) input.current.focus(); } },
            h(A.Icon, { name: open ? 'chevron-up' : 'chevron-down', size: 16 })))),
      helper ? h('div', { className: cx('alt-filter-autocomplete__help', error && 'is-error') }, error ? h(A.Icon, { name: 'info', size: 14 }) : null, helper) : null,
      open ? h('div', { className: 'alt-popover alt-filter-autocomplete__pop' },
        h('div', { ref: list, id: id + '-list', role: 'listbox', 'aria-multiselectable': 'true', className: 'alt-scroll alt-filter-autocomplete__list' },
          filtered.length ? filtered.map(function (o, i) {
            var sel = isSel(o), dis = !sel && atLimit;
            return h('div', {
              key: String(optValue(o)), id: id + '-o' + i, 'data-idx': i, role: 'option', 'aria-selected': sel, 'aria-disabled': dis || undefined,
              className: cx('alt-filter-autocomplete__opt', i === active && 'is-active', dis && 'is-disabled'),
              onMouseDown: function (e) { e.preventDefault(); }, onMouseEnter: function () { setActive(i); },
              onClick: function () { if (!dis) toggle(o); }
            }, h(Check, { on: sel, disabled: dis }), h('span', { className: 'alt-filter-autocomplete__opt-text' }, renderLabel(o)));
          }) : h('div', { className: 'alt-filter-autocomplete__empty' }, 'Nenhuma opção para “' + q + '”')),
        h('div', { className: 'alt-filter-autocomplete__foot alt-num' },
          h('span', null, values.length === 1 ? '1 selecionada' : nf(values.length) + ' selecionadas'),
          h('button', { type: 'button', className: 'alt-filter-autocomplete__link', disabled: !values.length, onMouseDown: function (e) { e.preventDefault(); }, onClick: function () { emit([]); } }, 'Limpar'))) : null);
  }

  A.FilterAutocomplete = FilterAutocomplete;
})();


/* ---- FormBody ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  /* Pill action button used by FormBody and FormCRUD. variant: primary | secondary | danger | ghost */
  function Action(p) {
    var v = p.variant || 'secondary';
    return h('button', {
      type: p.type || 'button', disabled: p.disabled || p.loading, onClick: p.onClick, 'aria-busy': p.loading || undefined, title: p.title,
      className: cx('alt-form-body__btn', 'alt-form-body__btn--' + v, p.className)
    }, p.loading ? h('span', { className: 'alt-form-body__spin', 'aria-hidden': 'true' }) : (p.icon ? h(A.Icon, { name: p.icon, size: 16 }) : null), p.children);
  }

  function DemoFields() {
    return h('div', { className: 'alt-form-body__grid' },
      h('div', { className: 'alt-field' }, h('label', { className: 'alt-field__label', htmlFor: 'alt-fb-desc' }, 'Descrição'),
        h('div', { className: 'alt-ctl' }, h('input', { id: 'alt-fb-desc', defaultValue: 'Posto Graal km 102' }))),
      h('div', { className: 'alt-field' }, h('label', { className: 'alt-field__label', htmlFor: 'alt-fb-raio' }, 'Raio (m)'),
        h('div', { className: 'alt-ctl alt-num' }, h('input', { id: 'alt-fb-raio', defaultValue: '250', inputMode: 'numeric' }), h('span', { className: 'alt-form-body__unit' }, 'm'))));
  }

  var LABELS = {
    INSERT: { cancel: 'Cancelar', save: 'Gravar' },
    UPDATE: { cancel: 'Cancelar', save: 'Gravar' },
    DELETE: { cancel: 'Abortar', save: 'Excluir' },
    VIEW: { save: 'Fechar' },
    NONE: { cancel: 'Cancelar' }
  };

  function FormBody(p) {
    var action = String(p.formAction || 'UPDATE').toUpperCase();
    var L = LABELS[action] || LABELS.NONE;
    var children = p.children === undefined ? h(DemoFields) : p.children;
    var footer = p.toolbarFooter === undefined ? 'Última alteração: 30/09/2026 14:37' : p.toolbarFooter;
    var busy = !!p.loading;

    var buttons = [];
    if (action === 'INSERT' || action === 'UPDATE') {
      buttons.push(h(Action, { key: 'c', onClick: p.callbackCancel, disabled: busy }, L.cancel));
      buttons.push(h(Action, { key: 's', variant: 'primary', icon: 'check', loading: busy, disabled: p.disableSaveButton, onClick: p.callbackSave }, busy ? 'Gravando…' : L.save));
    } else if (action === 'DELETE') {
      buttons.push(h(Action, { key: 'c', onClick: p.callbackCancel, disabled: busy }, L.cancel));
      buttons.push(h(Action, { key: 's', variant: 'danger', icon: 'trash', loading: busy, disabled: p.disableSaveButton, onClick: p.callbackSave }, busy ? 'Excluindo…' : L.save));
    } else if (action === 'VIEW') {
      buttons.push(h(Action, { key: 's', variant: 'primary', onClick: p.callbackCancel }, L.save));
    } else {
      buttons.push(h(Action, { key: 'c', disabled: true }, L.cancel));
    }

    return h('section', { className: cx('alt-glass', 'alt-form-body', p.className), style: p.style, 'aria-busy': busy || undefined },
      children ? h('div', { className: 'alt-form-body__fields' }, children) : null,
      p.hideActionButtons ? null : h('div', { className: cx('alt-form-body__bar', !children && 'alt-form-body__bar--solo') },
        footer ? h('span', { className: 'alt-form-body__meta alt-num' }, h(A.Icon, { name: 'clock', size: 14 }), h('span', null, footer)) : null,
        h('div', { className: 'alt-form-body__acts' },
          p.callbackRefresh ? h(Action, { variant: 'ghost', icon: 'refresh', onClick: p.callbackRefresh, disabled: busy }, 'Atualizar') : null,
          p.callbackRefresh ? h('span', { className: 'alt-form-body__sep', 'aria-hidden': 'true' }) : null,
          buttons)));
  }

  FormBody.Action = Action;
  A.FormBody = FormBody;
})();


/* ---- FormCRUD ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var TITLES = { INSERT: 'Novo', UPDATE: 'Modificar', DELETE: 'Apagar', VIEW: 'Visualizar' };

  var DEMO_SCHEMA = {
    type: 'object', required: ['descricao', 'raio'],
    properties: {
      descricao: { type: 'string', title: 'Descrição', maxLength: 80 },
      raio: { type: 'integer', title: 'Raio', minimum: 50, errorMessage: 'O raio mínimo é 50 m' },
      tipo: { type: 'string', title: 'Tipo', enum: ['Abastecimento', 'Descanso', 'Pernoite', 'Fiscalização'] },
      latitude: { type: 'number', title: 'Latitude' },
      longitude: { type: 'number', title: 'Longitude' },
      endereco: { type: 'string', title: 'Endereço' }
    }
  };
  var DEMO_UI = {
    type: 'VerticalLayout', elements: [
      { type: 'Group', label: 'Identificação', elements: [
        { type: 'Control', scope: '#/properties/descricao', options: { span: 'full' } },
        { type: 'Control', scope: '#/properties/raio', options: { unit: 'm' } },
        { type: 'Control', scope: '#/properties/tipo' }
      ] },
      { type: 'Group', label: 'Localização', options: { collapsed: true }, elements: [
        { type: 'Control', scope: '#/properties/latitude', options: { decimals: 6 } },
        { type: 'Control', scope: '#/properties/longitude', options: { decimals: 6 } },
        { type: 'Control', scope: '#/properties/endereco', options: { span: 'full' } }
      ] }
    ]
  };
  var DEMO_DATA = { descricao: 'Posto Graal km 102', raio: 250, tipo: 'Abastecimento', latitude: -22.912345, longitude: -47.061234, endereco: 'Rod. Anhanguera, km 102 · Sumaré/SP' };

  function FormCRUD(p) {
    var action = String(p.action || 'UPDATE').toUpperCase();
    var variant = p.variant || 'drawer';
    var open = p.open === undefined ? true : p.open;
    var custom = !!p.schema;
    var schema = p.schema || DEMO_SCHEMA, ui = p.uischema || DEMO_UI;
    var ds = R.useState(p.data || (custom ? {} : DEMO_DATA)); var data = ds[0], setData = ds[1];
    var es = R.useState(false); var showErrors = es[0], setShowErrors = es[1];
    var panel = R.useRef(null);
    var titleId = R.useRef('alt-fc-' + Math.random().toString(36).slice(2, 7)).current;
    var Action = A.FormBody.Action;
    var readonly = action === 'VIEW' || action === 'DELETE';

    R.useEffect(function () { if (p.data) setData(p.data); }, [p.data]);
    R.useEffect(function () {
      if (!open || variant === 'inline') return undefined;
      function k(e) { if (e.key === 'Escape' && p.handleClose) p.handleClose(); }
      document.addEventListener('keydown', k);
      if (panel.current) panel.current.focus();
      return function () { document.removeEventListener('keydown', k); };
    }, [open, variant]);
    if (!open) return null;

    function confirm() {
      if (action === 'VIEW') { if (p.handleClose) p.handleClose(); return; }
      var errs = A.JsonFormsRenderers.validate(schema, ui, data);
      if (action !== 'DELETE' && Object.keys(errs).length) { setShowErrors(true); return; }
      if (p.onConfirm) p.onConfirm(data, action);
    }

    var body;
    if (p.loading) {
      body = h('div', { className: 'alt-form-crud__state', role: 'status' },
        h('div', { className: 'alt-form-crud__skel' }, [0, 1, 2].map(function (i) { return h('div', { key: i, className: 'alt-skel', style: { height: i ? 40 : 18, width: i ? '100%' : '40%' } }); })),
        h('span', null, 'Aguarde, carregando ...'));
    } else if (p.error) {
      body = h('div', { className: 'alt-form-crud__state alt-form-crud__state--error', role: 'alert' },
        h(A.Icon, { name: 'x-circle', size: 20 }), h('span', null, typeof p.error === 'string' ? p.error : 'Não foi possível carregar o formulário!'));
    } else {
      body = [
        action === 'DELETE' ? h('div', { key: 'w', className: 'alt-form-crud__warn', role: 'note' }, h(A.Icon, { name: 'alert', size: 16 }),
          h('span', null, h('strong', null, 'Atenção: '), 'este registro será apagado e deixará de aparecer nas listagens.')) : null,
        h(A.JsonFormsRenderers, { key: 'f', bare: true, schema: schema, uischema: ui, data: data, readonly: readonly, showErrors: showErrors || undefined, onChange: function (d) { setData(d); if (p.onChange) p.onChange(d); } })
      ];
    }

    var footer = action === 'VIEW'
      ? [h(Action, { key: 'c', variant: 'primary', onClick: p.handleClose }, 'Fechar')]
      : [h(Action, { key: 'c', onClick: p.handleClose, disabled: p.saving }, 'Cancelar'),
        h(Action, { key: 's', variant: action === 'DELETE' ? 'danger' : 'primary', icon: action === 'DELETE' ? 'trash' : null, loading: p.saving, disabled: p.loading || !!p.error, onClick: confirm },
          action !== 'DELETE' && !p.saving ? SaveIcon() : null, p.saving ? 'Salvando…' : (action === 'DELETE' ? 'Apagar' : 'Confirmar'))];

    var sheet = h('div', {
      ref: panel, tabIndex: -1, role: variant === 'inline' ? 'region' : 'dialog', 'aria-modal': variant === 'inline' ? undefined : 'true', 'aria-labelledby': titleId,
      className: cx('alt-glass', 'alt-glass--overlay', 'alt-form-crud', 'alt-form-crud--' + variant, p.className),
      style: Object.assign(p.width ? { width: p.width } : {}, p.style)
    },
      h('header', { className: 'alt-form-crud__hd' },
        h('div', { className: 'alt-form-crud__ti' },
          h('h2', { id: titleId }, p.title || TITLES[action] || action),
          h('span', { className: 'alt-num' }, (p.descricao || p.nomeCadastro || 'Pontos de parada') + (p.id === undefined ? (custom ? '' : ' · #1284') : (p.id != null ? ' · #' + p.id : '')))),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost alt-form-crud__x', 'aria-label': 'Fechar', onClick: p.handleClose }, h(A.Icon, { name: 'x', size: 18 }))),
      h('div', { className: 'alt-scroll alt-form-crud__bd' }, body),
      h('footer', { className: 'alt-form-crud__ft' }, footer));

    if (variant === 'inline') return sheet;
    return h('div', { className: cx('alt-form-crud__layer', 'alt-form-crud__layer--' + variant) },
      h('div', { className: 'alt-form-crud__scrim', onClick: p.handleClose, 'aria-hidden': 'true' }), sheet);
  }

  /* Floppy-disk stroke icon (not in the kit). */
  function SaveIcon() {
    return h('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      h('path', { d: 'M5 3h11l5 5v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z' }), h('path', { d: 'M7 3v5h8M7 21v-7h10v7' }));
  }

  FormCRUD.demo = { schema: DEMO_SCHEMA, uischema: DEMO_UI, data: DEMO_DATA };
  A.FormCRUD = FormCRUD;
})();


/* ---- GradeSLA ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  var FAIXAS = [{ label: '15 min', minutos: 15 }, { label: '30 min', minutos: 30 }, { label: '1 h', minutos: 60 }, { label: '2 h', minutos: 120 }, { label: '3 h', minutos: 180 }, { label: '4 h', minutos: 240 },
    { label: '5 h', minutos: 300 }, { label: '6 h', minutos: 360 }, { label: '12 h', minutos: 720 }, { label: '24 h', minutos: 1440 }, { label: '48 h', minutos: 2880 }, { label: '4.800 h', minutos: 288000 }];
  function z(a) { while (a.length < 12) a.push(0); return a; }
  var ROWS = [
    { ocorrencia: 'Tempo excedido em coleta', sla: { dentroAte: 0, atencaoAte: 1 }, valores: z([4, 2, 1]) },
    { ocorrencia: 'Troca de motorista / frota', sla: { dentroAte: 0, atencaoAte: 1 }, valores: z([0, 1]) },
    { ocorrencia: 'Possível atraso (veículo em trânsito)', sla: { dentroAte: 0, atencaoAte: 1 }, valores: z([7, 3, 2, 1]) },
    { ocorrencia: 'Veículo em congestionamento / acidente', sla: { dentroAte: 0, atencaoAte: 1 }, valores: z([2, 0, 0, 0, 0, 0, 1]) },
    { ocorrencia: 'Problema mecânico interno', sla: { dentroAte: 0, atencaoAte: 1 }, valores: z([]) },
    { ocorrencia: 'Atraso de chegada', sla: { dentroAte: 0, atencaoAte: 0 }, valores: z([3, 2, 0, 1]) },
    { ocorrencia: 'Falta de conduta do motorista', sla: { dentroAte: 0, atencaoAte: 1 }, valores: z([1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1]) },
    { ocorrencia: 'Problema mecânico externo', sla: { dentroAte: 0, atencaoAte: 1 }, valores: z([]) }
  ];
  var ZONE = { ok: 'dentro do SLA', at: 'atenção', out: 'fora do SLA' };
  function zoneOf(sla, j) { return j <= sla.dentroAte ? 'ok' : j <= sla.atencaoAte ? 'at' : 'out'; }
  function sum(a) { return a.reduce(function (x, y) { return x + y; }, 0); }

  function GradeSLA(p) {
    var faixas = p.faixas || FAIXAS, rows = p.rows || ROWS;
    var q = R.useState(''), hz = R.useState(!!p.hideZeroRows), so = R.useState(p.sort || { key: 'qtd', dir: 'desc' }), sel = R.useState(null);
    var sort = so[0];
    var list = rows.map(function (r) { return Object.assign({ qtd: sum(r.valores) }, r); }).filter(function (r) {
      if (hz[0] && !r.qtd) return false;
      return !q[0] || r.ocorrencia.toLowerCase().indexOf(q[0].toLowerCase()) >= 0;
    });
    list.sort(function (a, b) {
      var d = sort.key === 'qtd' ? a.qtd - b.qtd : a.ocorrencia.localeCompare(b.ocorrencia, 'pt-BR');
      return sort.dir === 'desc' ? -d : d;
    });
    var total = sum(rows.map(function (r) { return sum(r.valores); }));
    var colTot = faixas.map(function (f, j) { return sum(list.map(function (r) { return r.valores[j] || 0; })); });
    function sortBy(key) { so[1](sort.key === key ? { key: key, dir: sort.dir === 'desc' ? 'asc' : 'desc' } : { key: key, dir: key === 'qtd' ? 'desc' : 'asc' }); }
    function sortTh(key, label, cls) {
      var on = sort.key === key;
      return h('th', { className: cls, 'aria-sort': on ? (sort.dir === 'desc' ? 'descending' : 'ascending') : 'none' },
        h('button', { type: 'button', className: 'alt-grade-sla__sort', 'data-on': on ? 'true' : null, onClick: function () { sortBy(key); } }, label,
          h(A.Icon, { name: on && sort.dir === 'asc' ? 'arrow-up' : 'arrow-down', size: 13, className: 'alt-grade-sla__ar' })));
    }
    return h('section', { className: cx('alt-grade-sla alt-panel', p.className), style: p.style },
      h('div', { className: 'alt-grade-sla__tb' },
        h('div', { className: 'alt-grade-sla__tt' }, h('h3', null, p.title || 'SLA por ocorrência'),
          h('span', { className: 'alt-num' }, (p.periodo || 'Hoje · 00:00–14:37') + ' · ' + nf(total) + ' ocorrências')),
        h('label', { className: 'alt-ctl alt-ctl--sm alt-grade-sla__q' }, h(A.Icon, { name: 'search', size: 16 }),
          h('input', { type: 'search', placeholder: 'Buscar ocorrência…', value: q[0], onChange: function (e) { q[1](e.target.value); }, 'aria-label': 'Buscar ocorrência' })),
        h('button', { type: 'button', role: 'switch', 'aria-checked': hz[0], className: 'alt-grade-sla__sw', onClick: function () { hz[1](!hz[0]); } }, h('i', { 'aria-hidden': 'true' }), 'Ocultar zeradas')),
      h('div', { className: 'alt-grade-sla__lg' },
        ['ok', 'at', 'out'].map(function (k) { return h('span', { key: k, className: 'alt-grade-sla__lk alt-grade-sla__lk--' + k }, k === 'ok' ? 'Dentro do SLA' : k === 'at' ? 'Atenção' : 'Fora do SLA'); }),
        h('span', { className: 'alt-grade-sla__lnone' }, '· sem ocorrências'),
        h('span', { className: 'alt-grade-sla__lnone' }, h('i', { className: 'alt-grade-sla__ul' }), 'violação com ocorrência')),
      h('div', { className: 'alt-grade-sla__wrap alt-scroll', style: { maxHeight: p.maxHeight || 360 } },
        h('table', { className: 'alt-grade-sla__table alt-num' },
          h('thead', null, h('tr', null,
            sortTh('qtd', 'Qtd', 'alt-grade-sla__q1'), sortTh('ocorrencia', 'Ocorrência', 'alt-grade-sla__oc'),
            faixas.map(function (f, j) { return h('th', { key: j, className: 'alt-grade-sla__n', scope: 'col' }, h('span', null, 'Até'), f.label); }))),
          h('tbody', null, list.length ? list.map(function (r) {
            return h('tr', { key: r.ocorrencia },
              h('td', { className: 'alt-grade-sla__q1' }, nf(r.qtd)),
              h('th', { scope: 'row', className: 'alt-grade-sla__oc' }, r.ocorrencia),
              faixas.map(function (f, j) {
                var v = r.valores[j] || 0, zn = zoneOf(r.sla, j), lab = r.ocorrencia + ' · até ' + f.label + ' · ' + ZONE[zn] + ' · ' + v + (v === 1 ? ' ocorrência' : ' ocorrências');
                var isSel = sel[0] && sel[0].o === r.ocorrencia && sel[0].j === j;
                return h('td', { key: j, className: cx('alt-grade-sla__c', 'alt-grade-sla__c--' + zn, v ? 'alt-grade-sla__c--hv' : null), title: lab },
                  v ? h('button', { type: 'button', 'aria-label': lab, 'aria-pressed': isSel, onClick: function () { sel[1]({ o: r.ocorrencia, j: j }); p.onCellClick && p.onCellClick(r.ocorrencia, f); } }, nf(v))
                    : h('span', { 'aria-label': '0' }, '·'));
              }));
          }) : h('tr', null, h('td', { colSpan: faixas.length + 2, className: 'alt-grade-sla__empty' }, 'Nenhuma ocorrência encontrada.'))),
          h('tfoot', null, h('tr', null,
            h('td', { className: 'alt-grade-sla__q1' }, nf(sum(colTot))), h('th', { scope: 'row', className: 'alt-grade-sla__oc' }, 'Total'),
            colTot.map(function (v, j) { return h('td', { key: j, className: 'alt-grade-sla__t' }, nf(v)); }))))));
  }
  A.GradeSLA = GradeSLA;
})();


/* ---- GraficoBase ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  var DAYS = []; for (var i = 1; i <= 30; i++) DAYS.push(String(i));
  var PLAN = DAYS.map(function (d, i) { return +(0.17 + i * 0.165).toFixed(2); });
  var REAL = [0.15, 0.31, 0.47, 0.6, 0.78, 0.95, 1.1, 1.24, 1.42, 1.58, 1.77, 1.9, 2.06, 2.24, 2.39, 2.57, 2.7, 2.88, 3.03, 3.21];
  var DEF_SERIES = [{ name: 'Realizado', data: REAL, area: true }, { name: 'Planejado', data: PLAN }];

  function GraficoBase(p) {
    var series = p.series || DEF_SERIES, x = p.x || DAYS, n = x.length;
    var hid = R.useState({}), hidden = hid[0];
    var zm = R.useState([0, n - 1]), range = zm[0];
    var plotRef = R.useRef(null);
    var G = A.Graphics, SER = G.util.SERIES;
    function restore() { hid[1]({}); zm[1]([0, n - 1]); p.onRestore && p.onRestore(); }
    function save() {
      if (p.onSaveImage) return p.onSaveImage();
      var svg = plotRef.current && plotRef.current.querySelector('svg'); if (!svg) return;
      var clone = svg.cloneNode(true); clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
      var url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(clone)], { type: 'image/svg+xml' }));
      var a = document.createElement('a'); a.href = url; a.download = (p.fileName || 'grafico') + '.svg'; a.click();
      setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    }
    var a0 = range[0], a1 = range[1];
    var shown = series.map(function (s, i) { return Object.assign({}, s, { color: s.color || SER[i], hidden: !!hidden[i], data: s.data.slice(a0, a1 + 1) }); });
    var xs = x.slice(a0, a1 + 1), m = xs.length - 1;
    var xTicks = [0, Math.round(m / 4), Math.round(m / 2), Math.round(3 * m / 4), m].filter(function (v, i, arr) { return arr.indexOf(v) === i; });
    var features = p.features !== false;
    var tools = h(R.Fragment, null,
      (p.buttons || []).map(function (b, i) { return h('button', { key: i, type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': b.label, title: b.label, onClick: b.onClick }, h(A.Icon, { name: b.icon, size: 16 })); }),
      features ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Restaurar', title: 'Restaurar', onClick: restore }, h(A.Icon, { name: 'refresh', size: 16 })) : null,
      features ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Salvar imagem', title: 'Salvar imagem', onClick: save }, h(A.Icon, { name: 'download', size: 16 })) : null);
    var legend = p.legend === false ? null : h('div', { className: 'alt-grafico-base__legend', role: 'group', 'aria-label': 'Séries' }, series.map(function (s, i) {
      var off = !!hidden[i];
      return h('button', { key: i, type: 'button', className: 'alt-grafico-base__chip', 'aria-pressed': !off, style: { '--c': s.color || SER[i] },
        onClick: function () { var o = Object.assign({}, hidden); o[i] = !off; hid[1](o); p.onToggleSeries && p.onToggleSeries(s, off); } },
        h('i', { className: 'alt-grafico-base__dot' }), s.name);
    }));
    var chart = p.children || h(G.Line, { x: xs, series: shown, xTicks: xTicks, height: p.height || 190, format: p.format || function (v) { return nf(v, 1); },
      tipFormat: p.tipFormat || function (v) { return 'R$ ' + nf(v, 2) + ' mi'; }, reference: p.reference || null, projection: null, tipTitle: p.tipTitle || function (d) { return 'Dia ' + d; }, yMax: p.yMax, endLabel: p.endLabel,
      label: p.ariaLabel || p.title });
    return h('section', { className: cx('alt-grafico-base alt-glass', p.className), style: p.style, 'aria-busy': p.loading ? 'true' : null },
      h('header', { className: 'alt-grafico-base__hd' },
        h('div', { className: 'alt-grafico-base__ttw' }, h('h3', { className: 'alt-grafico-base__tt' }, p.title || 'Faturamento diário · set/2026'),
          p.subtitle !== false ? h('span', { className: 'alt-grafico-base__sub' }, p.subtitle || 'R$ mi acumulado') : null),
        tools),
      legend,
      h('div', { ref: plotRef, className: 'alt-grafico-base__plot', 'data-loading': p.loading ? 'true' : null },
        chart,
        p.loading ? h('div', { className: 'alt-grafico-base__loading', role: 'status' }, h('span', { className: 'alt-grafico-base__spin', 'aria-hidden': 'true' }), h('span', null, p.loadingText || 'Carregando…')) : null),
      p.zoom ? h('div', { className: 'alt-grafico-base__zoom alt-num' },
        h('span', { className: 'alt-grafico-base__zl' }, 'Zoom'),
        h('div', { className: 'alt-grafico-base__range' },
          h('div', { className: 'alt-grafico-base__sel', style: { left: (a0 / (n - 1) * 100) + '%', right: (100 - a1 / (n - 1) * 100) + '%' } }),
          h('input', { type: 'range', min: 0, max: n - 1, value: a0, 'aria-label': 'Início do período', onChange: function (e) { zm[1]([Math.min(+e.target.value, a1 - 2), a1]); } }),
          h('input', { type: 'range', min: 0, max: n - 1, value: a1, 'aria-label': 'Fim do período', onChange: function (e) { zm[1]([a0, Math.max(+e.target.value, a0 + 2)]); } })),
        h('span', { className: 'alt-grafico-base__zv' }, x[a0] + '–' + x[a1])) : null);
  }
  A.GraficoBase = GraficoBase;
})();


/* ---- Graphics ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  var SERIES = ['var(--chart-1)', 'var(--chart-2)', 'var(--chart-3)', 'var(--chart-4)', 'var(--chart-5)', 'var(--chart-6)'];
  var MUTED = 'var(--chart-6)';
  var SEQ = [22, 40, 58, 78, 100];
  function seq(i) { return i <= 0 ? 'var(--track)' : 'color-mix(in srgb, var(--chart-2) ' + SEQ[Math.min(i, 5) - 1] + '%, var(--surface-panel))'; }
  function f1(v) { return nf(v, 1); }
  function signed(v, d) { return (v > 0 ? '+' : v < 0 ? '−' : '') + nf(Math.abs(v), d == null ? 1 : d); }

  /* ---------- helpers ---------- */
  function useSize() {
    var ref = R.useRef(null), s = R.useState(0);
    R.useLayoutEffect(function () {
      var el = ref.current; if (!el) return;
      var set = function () { s[1](el.clientWidth); };
      set();
      if (!window.ResizeObserver) return;
      var ro = new ResizeObserver(set); ro.observe(el);
      return function () { ro.disconnect(); };
    }, []);
    return [ref, s[0]];
  }
  function ticks(min, max, n) {
    var raw = (max - min) / (n || 4) || 1, mag = Math.pow(10, Math.floor(Math.log10(raw))), r = raw / mag;
    var step = (r <= 1 ? 1 : r <= 2 ? 2 : r <= 2.5 ? 2.5 : r <= 5 ? 5 : 10) * mag;
    var out = [], a = Math.floor(min / step) * step, b = Math.ceil(max / step - 1e-9) * step;
    for (var v = a; v <= b + step / 2; v += step) out.push(+v.toFixed(6));
    return out;
  }
  /* bar with 4px rounded data end and flat base. dir: 'up' | 'down' | 'right' */
  function barPath(x, y, w, ht, r, dir) {
    if (w <= 0 || ht <= 0) return '';
    if (dir === 'right') {
      r = Math.min(r, ht / 2, w);
      return 'M' + x + ' ' + y + 'H' + (x + w - r) + 'Q' + (x + w) + ' ' + y + ' ' + (x + w) + ' ' + (y + r) + 'V' + (y + ht - r) + 'Q' + (x + w) + ' ' + (y + ht) + ' ' + (x + w - r) + ' ' + (y + ht) + 'H' + x + 'Z';
    }
    r = Math.min(r, w / 2, ht);
    if (dir === 'down') return 'M' + x + ' ' + y + 'H' + (x + w) + 'V' + (y + ht - r) + 'Q' + (x + w) + ' ' + (y + ht) + ' ' + (x + w - r) + ' ' + (y + ht) + 'H' + (x + r) + 'Q' + x + ' ' + (y + ht) + ' ' + x + ' ' + (y + ht - r) + 'Z';
    return 'M' + x + ' ' + (y + ht) + 'V' + (y + r) + 'Q' + x + ' ' + y + ' ' + (x + r) + ' ' + y + 'H' + (x + w - r) + 'Q' + (x + w) + ' ' + y + ' ' + (x + w) + ' ' + (y + r) + 'V' + (y + ht) + 'Z';
  }
  function useTip() {
    var ref = R.useRef(null), s = R.useState(null);
    function at(c, x, y) { s[1](Object.assign({ x: x, y: y, w: ref.current ? ref.current.clientWidth : 0 }, c)); }
    function ev(e, c) { var r = ref.current.getBoundingClientRect(); at(c, e.clientX - r.left, e.clientY - r.top); }
    function el(e, c) { var r = ref.current.getBoundingClientRect(), b = e.currentTarget.getBoundingClientRect(); at(c, b.left + b.width / 2 - r.left, b.top - r.top); }
    function hide() { s[1](null); }
    function mark(c, label) {
      return { tabIndex: 0, 'aria-label': label, className: 'alt-graphics__mark',
        onPointerMove: function (e) { ev(e, c); }, onPointerLeave: hide, onFocus: function (e) { el(e, c); }, onBlur: hide };
    }
    return { ref: ref, tip: s[0], at: at, ev: ev, mark: mark, hide: hide, key: s[0] ? s[0].key : null };
  }
  function Tip(p) {
    var t = p.tip; if (!t) return null;
    var flip = t.x > t.w * 0.62, below = t.y < 70;
    return h('div', { className: 'alt-graphics__tip alt-num', role: 'status',
      style: { left: flip ? t.x - 12 : t.x + 12, top: below ? t.y + 16 : t.y - 10, transform: (flip ? 'translateX(-100%) ' : '') + (below ? '' : 'translateY(-100%)') } },
      t.title ? h('div', { className: 'alt-graphics__tip-h' }, t.title) : null,
      (t.rows || []).map(function (r, i) {
        return h('div', { key: i, className: 'alt-graphics__tip-r' },
          r.c ? h('i', { className: 'alt-graphics__key alt-graphics__key--' + (r.kind || 'sq'), style: { '--c': r.c } }) : null,
          h('b', null, r.v), r.n ? h('span', null, r.n) : null);
      }));
  }
  /* Frame: measures width, renders an SVG plot + tooltip layer */
  function Frame(p) {
    var sz = useSize(), tp = useTip();
    var setRef = function (el) { sz[0].current = el; tp.ref.current = el; };
    var W = sz[1], H = p.height;
    return h('div', { ref: setRef, className: cx('alt-graphics__plot', p.className), style: { height: H } },
      W > 0 ? h('svg', { width: W, height: H, className: 'alt-graphics__svg alt-num', role: 'img', 'aria-label': p.label }, p.render(W, H, tp)) : null,
      h(Tip, { tip: tp.tip }));
  }
  function yAxis(tk, m, W, Y, fmt) {
    return tk.map(function (v, i) {
      return h('g', { key: 'y' + i },
        h('line', { x1: m.l, x2: W - m.r, y1: Y(v), y2: Y(v), className: 'alt-graphics__grid' }),
        h('text', { x: m.l - 8, y: Y(v) + 4, textAnchor: 'end', className: 'alt-graphics__ax' }, fmt(v)));
    });
  }

  /* ---------- default data (source example content) ---------- */
  var days = []; for (var d = 1; d <= 30; d++) days.push(d);
  var real = [], plan = [];
  (function () { var a = 0, b = 0; for (var i = 0; i < 30; i++) { b += 0.4; plan.push(+b.toFixed(2)); if (i < 22) { a += i === 21 ? 0.38 : (0.42 + ((i * 37) % 9) / 100 - 0.02); real.push(+a.toFixed(2)); } } real[21] = 9.84; })();
  var D = {
    line: { title: 'Faturamento acumulado', subtitle: 'R$ mi · setembro/2026',
      x: days.map(function (d) { return (d < 10 ? '0' : '') + d + '/09'; }), xTicks: [0, 7, 14, 21, 29],
      series: [{ name: 'Realizado', data: real, area: true }, { name: 'Planejado', data: plan }],
      reference: { value: 12.5, label: 'Meta R$ 12,5 mi' }, projection: { from: 21, value: 12.1, label: 'Projeção' },
      yMax: 14, format: f1, tipFormat: function (v) { return 'R$ ' + nf(v, 2) + ' mi'; },
      note: 'Projeção pelo ritmo médio dos dias úteis: R$ 12,1 mi (−3% da meta).',
      label: 'Faturamento acumulado de setembro: realizado R$ 9,84 mi até dia 22, planejado R$ 12 mi, meta R$ 12,5 mi' },
    columns: { title: 'Faturamento por mês', subtitle: 'R$ mi · últimos 6 meses',
      categories: ['abr', 'mai', 'jun', 'jul', 'ago', 'set'],
      series: [{ name: 'Realizado', data: [10.6, 11.2, 10.9, 11.8, 11.6, 9.8] }, { name: 'Planejado', data: [11.0, 11.4, 11.6, 11.8, 12.0, 12.0] }],
      partialLast: true, partialLabel: 'Mês em curso', format: f1, label: 'Faturamento mensal realizado e planejado, abril a setembro' },
    hbar: { title: 'Top clientes', subtitle: 'R$ mi · setembro/2026',
      items: [{ label: 'Vidroporto', value: 2.8 }, { label: 'Owens-Illinois', value: 2.2 }, { label: 'Saint-Gobain', value: 1.8 }, { label: 'Verallia', value: 1.3 }, { label: 'Ambev', value: 1.0 }, { label: 'Outros (14)', value: 0.7, other: true }],
      format: f1, note: 'Os 5 maiores somam 93% do faturamento.', label: 'Ranking de clientes por faturamento' },
    stacked: { title: 'Mix de modal por região', subtitle: '% das viagens',
      parts: ['Lotação', 'Fracionado', 'Dedicado', 'Transferência'],
      rows: [{ label: 'Sudeste', values: [52, 22, 18, 8] }, { label: 'Sul', values: [44, 30, 14, 12] }, { label: 'Centro-Oeste', values: [61, 17, 12, 10] }, { label: 'Nordeste', values: [38, 35, 18, 9] }, { label: 'Norte', values: [29, 41, 24, 6] }],
      label: 'Mix de modal por região, percentual das viagens' },
    donut: { title: 'Viagens por tipo de carga', subtitle: 'setembro/2026',
      items: [{ label: 'Vidro', value: 1399 }, { label: 'Granéis', value: 819 }, { label: 'Químicos', value: 580 }, { label: 'Paletizado', value: 375 }, { label: 'Outros', value: 239, other: true }],
      centerLabel: 'viagens', label: 'Viagens por tipo de carga: vidro lidera com 41%' },
    heatmap: { title: 'Entregas por dia e horário', subtitle: 'últimas 4 semanas · média por semana',
      cols: ['0–4h', '4–8h', '8–12h', '12–16h', '16–20h', '20–24h'], rowsLabels: ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'],
      values: [[12, 180, 520, 560, 310, 40], [8, 170, 540, 590, 330, 52], [10, 190, 500, 545, 300, 38], [14, 205, 560, 610, 350, 61], [9, 230, 580, 620, 410, 88], [0, 60, 240, 190, 70, 10], [0, 0, 40, 30, 12, 0]],
      thresholds: [110, 220, 330, 440, 550], label: 'Entregas por dia da semana e faixa horária' },
    bullet: { title: 'Meta por filial', subtitle: 'faturamento realizado ÷ meta do mês',
      items: [{ label: 'Campinas', value: 108 }, { label: 'Jundiaí', value: 96 }, { label: 'Sorocaba', value: 91 }, { label: 'Santos', value: 84 }, { label: 'Ribeirão Preto', value: 71 }],
      target: 100, max: 120, label: 'Realizado contra meta por filial' },
    bridge: { title: 'Ponte de faturamento', subtitle: 'R$ mi · agosto → setembro',
      start: { label: 'Ago/26', value: 11.6 }, steps: [{ label: 'Volume', value: 0.9 }, { label: 'Preço', value: 0.4 }, { label: 'Mix', value: -0.3 }, { label: 'Cancel.', value: -0.5 }, { label: 'Frete retorno', value: -0.2 }],
      end: { label: 'Set/26*' }, min: 10, note: '* Setembro projetado. Eixo começa em R$ 10 mi para mostrar as variações.', label: 'Ponte de faturamento de agosto para setembro' },
    scatter: { title: 'Volume × faturamento', subtitle: 'por cliente · R$ mi',
      points: [{ label: 'Vidroporto', x: 1120, y: 1.42 }, { label: 'Owens-Illinois', x: 980, y: 1.18 }, { label: 'Saint-Gobain', x: 640, y: 1.02 }, { label: 'Verallia', x: 760, y: 0.86 }, { label: 'Ambev', x: 520, y: 0.74 }, { label: 'Usiminas', x: 410, y: 0.48 }, { label: 'Braskem', x: 300, y: 0.52 }, { label: 'Suzano', x: 260, y: 0.31 }, { label: 'Klabin', x: 180, y: 0.26 }, { label: 'Gerdau', x: 350, y: 0.39 }, { label: 'Heineken', x: 220, y: 0.34 }, { label: 'Nadir', x: 120, y: 0.12 }],
      threshold: 1310, xLabel: 'volume (t)', xMax: 1200, yMax: 1.6, label: 'Volume por faturamento por cliente' },
    kpi: { items: [
      { label: 'Faturamento no mês', value: 'R$ 9,84 mi', delta: 6.2, unit: '%', period: 'vs ago/26', spark: [7.8, 8.1, 8.4, 8.2, 8.9, 9.1, 8.8, 9.3, 9.0, 9.4, 9.27, 9.84] },
      { label: 'Viagens realizadas', value: '3.412', delta: 3.8, unit: '%', period: 'vs ago/26', spark: [3010, 3120, 3080, 3190, 3220, 3150, 3260, 3300, 3240, 3310, 3287, 3412] },
      { label: 'Custo por km', value: 'R$ 4,12', delta: 2.1, unit: '%', period: 'vs ago/26', upIsGood: false, spark: [3.8, 3.85, 3.9, 3.88, 3.95, 4.0, 3.98, 4.02, 4.05, 4.0, 4.03, 4.12] },
      { label: 'Entregas no prazo', value: '94,3%', delta: -0.9, unit: ' p.p.', period: 'vs ago/26', spark: [95.8, 95.1, 95.6, 96.0, 95.4, 95.2, 95.7, 95.5, 95.0, 95.3, 95.2, 94.3] }] }
  };
  function def(type, p) { return Object.assign({}, D[type], p || {}); }
  function colorOf(s, i) { return s.color || (s.other ? MUTED : SERIES[i % SERIES.length]); }

  /* ---------- Line / area ---------- */
  function Line(p0) {
    var p = def('line', p0), H = p.height || 200, fmt = p.format || f1, tfmt = p.tipFormat || fmt;
    return h(Frame, { height: H, label: p.label, render: function (W, H, tp) {
      var m = { l: 40, r: 18, t: 16, b: 26 }, n = p.x.length;
      var vals = [0];
      p.series.forEach(function (s) { if (!s.hidden) s.data.forEach(function (v) { if (v != null) vals.push(v); }); });
      if (p.reference) vals.push(p.reference.value); if (p.projection) vals.push(p.projection.value);
      var tk = ticks(p.yMin || 0, p.yMax || Math.max.apply(null, vals), 4), ymax = tk[tk.length - 1], ymin = tk[0];
      var X = function (i) { return m.l + (n <= 1 ? 0 : i * (W - m.l - m.r) / (n - 1)); };
      var Y = function (v) { return m.t + (1 - (v - ymin) / (ymax - ymin)) * (H - m.t - m.b); };
      var xt = p.xTicks || [0, Math.round((n - 1) / 4), Math.round((n - 1) / 2), Math.round(3 * (n - 1) / 4), n - 1];
      var out = yAxis(tk, m, W, Y, fmt);
      xt.forEach(function (i, k) { out.push(h('text', { key: 'x' + k, x: X(i), y: H - 6, textAnchor: k === 0 ? 'start' : k === xt.length - 1 ? 'end' : 'middle', className: 'alt-graphics__ax' }, p.xFormat ? p.xFormat(p.x[i]) : p.x[i])); });
      if (p.projection) {
        var px = X(p.projection.from);
        out.push(h('rect', { key: 'pb', x: px, y: m.t, width: W - m.r - px, height: H - m.t - m.b, className: 'alt-graphics__proj' }));
        out.push(h('text', { key: 'pl', x: px + 6, y: H - m.b - 6, className: 'alt-graphics__ax' }, p.projection.label || 'Projeção'));
      }
      if (p.reference) {
        out.push(h('line', { key: 'rf', x1: m.l, x2: W - m.r, y1: Y(p.reference.value), y2: Y(p.reference.value), className: 'alt-graphics__ref' }));
        out.push(h('text', { key: 'rft', x: W - m.r, y: Y(p.reference.value) - 6, textAnchor: 'end', className: 'alt-graphics__ax' }, p.reference.label));
      }
      p.series.map(function (s, i) { return [s, i]; }).reverse().forEach(function (pr) {
        var s = pr[0], si = pr[1];
        if (s.hidden) return;
        var c = colorOf(s, si), pts = [];
        s.data.forEach(function (v, i) { if (v != null) pts.push([X(i), Y(v)]); });
        if (!pts.length) return;
        var dpath = 'M' + pts.map(function (q) { return q[0].toFixed(1) + ' ' + q[1].toFixed(1); }).join('L');
        if (s.area) out.push(h('path', { key: 'a' + si, d: dpath + 'L' + pts[pts.length - 1][0] + ' ' + Y(ymin) + 'L' + pts[0][0] + ' ' + Y(ymin) + 'Z', fill: c, fillOpacity: 0.1 }));
        out.push(h('path', { key: 'l' + si, d: dpath, fill: 'none', stroke: c, strokeWidth: s.width || 2, strokeOpacity: s.opacity == null ? 1 : s.opacity, strokeDasharray: s.dashed ? '6 4' : null, strokeLinejoin: 'round', strokeLinecap: 'round' }));
        if (si === 0 && p.projection) {
          var last = pts[pts.length - 1];
          out.push(h('path', { key: 'pj', d: 'M' + last[0] + ' ' + last[1] + 'L' + X(n - 1) + ' ' + Y(p.projection.value), fill: 'none', stroke: c, strokeWidth: 2, strokeDasharray: '2 4', strokeLinecap: 'round' }));
        }
        if (si === 0 && p.endLabel !== false) {
          var lp = pts[pts.length - 1], lv = s.data[pts.length - 1];
          out.push(h('circle', { key: 'e' + si, cx: lp[0], cy: lp[1], r: 4, fill: c, className: 'alt-graphics__ring' }));
          out.push(h('text', { key: 'et' + si, x: lp[0] - 8, y: lp[1] - 10, textAnchor: 'end', className: 'alt-graphics__val' }, fmt(lv)));
        }
      });
      var k = tp.key;
      if (k != null) {
        out.push(h('line', { key: 'xh', x1: X(k), x2: X(k), y1: m.t, y2: H - m.b, className: 'alt-graphics__xh' }));
        p.series.forEach(function (s, si) { if (!s.hidden && s.data[k] != null) out.push(h('circle', { key: 'xd' + si, cx: X(k), cy: Y(s.data[k]), r: 4, fill: colorOf(s, si), className: 'alt-graphics__ring' })); });
      }
      function content(i) {
        return { key: i, title: p.tipTitle ? p.tipTitle(p.x[i], i) : p.x[i], rows: p.series.filter(function (s) { return !s.hidden && s.data[i] != null; }).map(function (s) {
          return { c: colorOf(s, p.series.indexOf(s)), kind: 'line', v: tfmt(s.data[i]), n: s.name }; }) };
      }
      function idx(x) { return Math.max(0, Math.min(n - 1, Math.round((x - m.l) / ((W - m.l - m.r) / Math.max(1, n - 1))))); }
      out.push(h('rect', { key: 'hit', x: m.l, y: m.t, width: W - m.l - m.r, height: H - m.t - m.b, fill: 'transparent', tabIndex: 0, className: 'alt-graphics__mark',
        'aria-label': 'Explorar valores; use as setas',
        onPointerMove: function (e) { var r = tp.ref.current.getBoundingClientRect(); var i = idx(e.clientX - r.left); tp.at(content(i), X(i), e.clientY - r.top); },
        onPointerLeave: tp.hide, onBlur: tp.hide,
        onFocus: function () { var i = k == null ? n - 1 : k; tp.at(content(i), X(i), m.t + 30); },
        onKeyDown: function (e) { if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return; e.preventDefault(); var i = Math.max(0, Math.min(n - 1, (k == null ? n - 1 : k) + (e.key === 'ArrowRight' ? 1 : -1))); tp.at(content(i), X(i), m.t + 30); } }));
      return out;
    } });
  }

  /* ---------- Grouped columns ---------- */
  function Columns(p0) {
    var p = def('columns', p0), H = p.height || 200, fmt = p.format || f1, tfmt = p.tipFormat || fmt;
    return h(Frame, { height: H, label: p.label, render: function (W, H, tp) {
      var m = { l: 36, r: 8, t: 18, b: 26 }, n = p.categories.length, ser = p.series.filter(function (s) { return !s.hidden; }), k = ser.length;
      var vals = [0]; ser.forEach(function (s) { vals = vals.concat(s.data); });
      var tk = ticks(0, p.yMax || Math.max.apply(null, vals), 3), ymax = tk[tk.length - 1];
      var Y = function (v) { return m.t + (1 - v / ymax) * (H - m.t - m.b); };
      var gw = (W - m.l - m.r) / n, bw = Math.max(4, Math.min(24, (gw * 0.66 - 2 * (k - 1)) / Math.max(1, k)));
      var out = yAxis(tk, m, W, Y, fmt);
      p.categories.forEach(function (c, i) {
        var gx = m.l + gw * i + (gw - (bw * k + 2 * (k - 1))) / 2, partial = p.partialLast && i === n - 1;
        out.push(h('text', { key: 'x' + i, x: m.l + gw * i + gw / 2, y: H - 6, textAnchor: 'middle', className: 'alt-graphics__ax' }, c));
        ser.forEach(function (s, si) {
          var v = s.data[i], x = gx + si * (bw + 2), y = Y(v), key = i + ':' + si, on = tp.key == null || tp.key === key;
          var mk = tp.mark({ key: key, title: c + (partial ? ' (parcial)' : ''), rows: [{ c: colorOf(s, p.series.indexOf(s)), v: tfmt(v), n: s.name }] }, s.name + ' ' + c + ': ' + tfmt(v));
          out.push(h('path', Object.assign(mk, { key: 'b' + key, d: barPath(x, y, bw, Y(0) - y, 4, 'up'), fill: colorOf(s, p.series.indexOf(s)), fillOpacity: (partial ? 0.55 : 1) * (on ? 1 : 0.4) })));
          if (i === n - 1 && bw >= 14) out.push(h('text', { key: 'v' + key, x: x + bw / 2, y: y - 6, textAnchor: 'middle', className: 'alt-graphics__val' }, fmt(v)));
        });
      });
      return out;
    } });
  }

  /* ---------- Horizontal ranking ---------- */
  function HBar(p0) {
    var p = def('hbar', p0), n = p.items.length, H = p.height || Math.max(120, n * 26 + 26), fmt = p.format || f1, tfmt = p.tipFormat || fmt;
    return h(Frame, { height: H, label: p.label, render: function (W, H, tp) {
      var m = { l: p.labelWidth || 104, r: 36, t: 4, b: 22 };
      var tk = ticks(0, p.xMax || Math.max.apply(null, p.items.map(function (it) { return it.value; })), 3), xmax = tk[tk.length - 1];
      var X = function (v) { return m.l + v / xmax * (W - m.l - m.r); }, rh = (H - m.t - m.b) / n, bh = Math.min(16, rh - 8);
      var out = tk.map(function (v, i) { return h('g', { key: 't' + i }, h('line', { x1: X(v), x2: X(v), y1: m.t, y2: H - m.b, className: 'alt-graphics__grid' }), h('text', { x: X(v), y: H - 6, textAnchor: 'middle', className: 'alt-graphics__ax' }, fmt(v))); });
      p.items.forEach(function (it, i) {
        var y = m.t + rh * i + (rh - bh) / 2, c = it.color || (it.other ? MUTED : SERIES[0]), on = tp.key == null || tp.key === i;
        out.push(h('text', { key: 'l' + i, x: m.l - 10, y: y + bh / 2 + 4, textAnchor: 'end', className: 'alt-graphics__lbl' }, it.label));
        out.push(h('path', Object.assign(tp.mark({ key: i, title: it.label, rows: [{ c: c, v: tfmt(it.value) }] }, it.label + ': ' + tfmt(it.value)),
          { key: 'b' + i, d: barPath(m.l, y, X(it.value) - m.l, bh, 4, 'right'), fill: c, fillOpacity: on ? 1 : 0.4 })));
        out.push(h('text', { key: 'v' + i, x: X(it.value) + 6, y: y + bh / 2 + 4, className: 'alt-graphics__val' }, fmt(it.value)));
      });
      return out;
    } });
  }

  /* ---------- 100% stacked bars ---------- */
  function Stacked(p0) {
    var p = def('stacked', p0), n = p.rows.length, H = p.height || Math.max(120, n * 28 + 24);
    return h(Frame, { height: H, label: p.label, render: function (W, H, tp) {
      var m = { l: p.labelWidth || 96, r: 8, t: 4, b: 22 }, rh = (H - m.t - m.b) / n, bh = Math.min(18, rh - 8);
      var X = function (v) { return m.l + v / 100 * (W - m.l - m.r); };
      var out = [0, 50, 100].map(function (v, i) { return h('g', { key: 't' + i }, h('line', { x1: X(v), x2: X(v), y1: m.t, y2: H - m.b, className: 'alt-graphics__grid' }), h('text', { x: X(v), y: H - 6, textAnchor: i === 0 ? 'start' : i === 2 ? 'end' : 'middle', className: 'alt-graphics__ax' }, v + '%')); });
      p.rows.forEach(function (r, i) {
        var y = m.t + rh * i + (rh - bh) / 2, tot = r.values.reduce(function (a, b) { return a + b; }, 0), acc = 0;
        out.push(h('text', { key: 'l' + i, x: m.l - 10, y: y + bh / 2 + 4, textAnchor: 'end', className: 'alt-graphics__lbl' }, r.label));
        r.values.forEach(function (v, j) {
          var pct = v / tot * 100, x0 = X(acc) + (j ? 1 : 0), x1 = X(acc + pct) - (j < r.values.length - 1 ? 1 : 0), key = i + ':' + j, on = tp.key == null || tp.key === key;
          acc += pct;
          var c = SERIES[j % SERIES.length];
          out.push(h('path', Object.assign(tp.mark({ key: key, title: r.label, rows: [{ c: c, v: nf(pct, 0) + '%', n: p.parts[j] }] }, r.label + ', ' + p.parts[j] + ': ' + nf(pct, 0) + '%'),
            { key: 's' + key, d: j === r.values.length - 1 ? barPath(x0, y, x1 - x0, bh, 4, 'right') : 'M' + x0 + ' ' + y + 'h' + (x1 - x0) + 'v' + bh + 'h' + (x0 - x1) + 'Z', fill: c, fillOpacity: on ? 1 : 0.4 })));
          if (x1 - x0 > 32 && j < 3) out.push(h('text', { key: 't' + key, x: x0 + 6, y: y + bh / 2 + 4, className: 'alt-graphics__in' }, nf(pct, 0) + '%'));
        });
      });
      return out;
    } });
  }

  /* ---------- Donut ---------- */
  function Donut(p0) {
    var p = def('donut', p0), tp = useTip(), S = p.size || 148, T = p.thickness || 18, r = (S - T) / 2 - 2, C = S / 2;
    var tot = p.items.reduce(function (a, b) { return a + b.value; }, 0), a0 = -Math.PI / 2, gap = 0.035;
    function pt(a, rr) { return [C + rr * Math.cos(a), C + rr * Math.sin(a)]; }
    var arcs = p.items.map(function (it, i) {
      var sw = it.value / tot * Math.PI * 2, s = a0 + gap / 2, e = a0 + sw - gap / 2; a0 += sw;
      var c = it.color || (it.other ? MUTED : SERIES[i % SERIES.length]), on = tp.key == null || tp.key === i;
      var r1 = r + T / 2, r0 = r - T / 2, A1 = pt(s, r1), B1 = pt(e, r1), A0 = pt(e, r0), B0 = pt(s, r0), lg = e - s > Math.PI ? 1 : 0;
      var d = 'M' + A1 + 'A' + r1 + ' ' + r1 + ' 0 ' + lg + ' 1 ' + B1 + 'L' + A0 + 'A' + r0 + ' ' + r0 + ' 0 ' + lg + ' 0 ' + B0 + 'Z';
      var pc = nf(it.value / tot * 100, 0) + '%';
      return h('path', Object.assign(tp.mark({ key: i, title: it.label, rows: [{ c: c, v: nf(it.value), n: pc }] }, it.label + ': ' + nf(it.value) + ' (' + pc + ')'),
        { key: i, d: d.replace(/,/g, ' '), fill: c, fillOpacity: on ? 1 : 0.35 }));
    });
    return h('div', { ref: tp.ref, className: 'alt-graphics__plot alt-graphics__donut' },
      h('svg', { width: S, height: S, className: 'alt-graphics__svg alt-num', role: 'img', 'aria-label': p.label },
        arcs,
        h('text', { x: C, y: C + 4, textAnchor: 'middle', className: 'alt-graphics__center' }, p.center != null ? p.center : nf(tot)),
        h('text', { x: C, y: C + 22, textAnchor: 'middle', className: 'alt-graphics__ax' }, p.centerLabel)),
      p.legend === false ? null : h('ul', { className: 'alt-graphics__dlg alt-num' }, p.items.map(function (it, i) {
        return h('li', { key: i, 'data-dim': tp.key != null && tp.key !== i ? 'true' : null },
          h('i', { className: 'alt-graphics__key alt-graphics__key--sq', style: { '--c': it.color || (it.other ? MUTED : SERIES[i % SERIES.length]) } }),
          h('span', null, it.label), h('b', null, nf(it.value / tot * 100, 0) + '%'));
      })),
      h(Tip, { tip: tp.tip }));
  }

  /* ---------- Heatmap ---------- */
  function Heatmap(p0) {
    var p = def('heatmap', p0), nr = p.rowsLabels.length, H = p.height || nr * 22 + 26, th = p.thresholds;
    function bucket(v) { if (!v) return 0; for (var i = 0; i < th.length; i++) if (v < th[i]) return i + 1; return 5; }
    return h(Frame, { height: H, label: p.label, render: function (W, H, tp) {
      var m = { l: 34, r: 0, t: 22, b: 0 }, nc = p.cols.length, cw = (W - m.l - m.r) / nc, ch = (H - m.t - m.b) / nr, out = [];
      p.cols.forEach(function (c, j) { out.push(h('text', { key: 'c' + j, x: m.l + cw * j + cw / 2, y: 13, textAnchor: 'middle', className: 'alt-graphics__ax' }, c)); });
      p.rowsLabels.forEach(function (rl, i) {
        out.push(h('text', { key: 'r' + i, x: 0, y: m.t + ch * i + ch / 2 + 4, className: 'alt-graphics__ax' }, rl));
        p.cols.forEach(function (c, j) {
          var v = p.values[i][j], key = i + ':' + j;
          out.push(h('rect', Object.assign(tp.mark({ key: key, title: rl + ' · ' + c, rows: [{ c: seq(bucket(v)), v: nf(v), n: 'entregas' }] }, rl + ' ' + c + ': ' + nf(v) + ' entregas'),
            { key: key, x: m.l + cw * j + 1, y: m.t + ch * i + 1, width: cw - 2, height: ch - 2, rx: 4, fill: seq(bucket(v)), stroke: tp.key === key ? 'var(--teal-light)' : 'none', strokeWidth: 1.5 })));
        });
      });
      return out;
    } });
  }
  function SeqLegend(p) {
    return h('div', { className: 'alt-graphics__seq' }, h('span', null, '0'), h('i', { style: { background: seq(0) } }), h('span', null, 'menos'),
      [1, 2, 3, 4, 5].map(function (i) { return h('i', { key: i, style: { background: seq(i) } }); }), h('span', null, p.max || 'mais'));
  }

  /* ---------- Bullet ---------- */
  function Bullet(p0) {
    var p = def('bullet', p0), n = p.items.length, H = p.height || n * 26 + 24;
    return h(Frame, { height: H, label: p.label, render: function (W, H, tp) {
      var m = { l: p.labelWidth || 104, r: 40, t: 4, b: 22 }, rh = (H - m.t - m.b) / n;
      var X = function (v) { return m.l + Math.min(v, p.max) / p.max * (W - m.l - m.r); };
      var out = [0, p.max / 2, p.max].map(function (v, i) { return h('text', { key: 't' + i, x: X(v), y: H - 6, textAnchor: i === 0 ? 'start' : i === 2 ? 'end' : 'middle', className: 'alt-graphics__ax' }, nf(v) + '%'); });
      p.items.forEach(function (it, i) {
        var y = m.t + rh * i + rh / 2, on = tp.key == null || tp.key === i;
        out.push(h('text', { key: 'l' + i, x: m.l - 10, y: y + 4, textAnchor: 'end', className: 'alt-graphics__lbl' }, it.label));
        out.push(h('rect', { key: 'tr' + i, x: m.l, y: y - 7, width: X(p.max) - m.l, height: 14, rx: 3, fill: 'var(--track)' }));
        out.push(h('path', Object.assign(tp.mark({ key: i, title: it.label, rows: [{ c: SERIES[0], v: nf(it.value) + '%', n: 'da meta' }] }, it.label + ': ' + nf(it.value) + '% da meta'),
          { key: 'b' + i, d: barPath(m.l, y - 4, X(it.value) - m.l, 8, 4, 'right'), fill: SERIES[0], fillOpacity: on ? 1 : 0.4 })));
        out.push(h('rect', { key: 'tg' + i, x: X(p.target) - 1, y: y - 9, width: 2, height: 18, rx: 1, fill: 'var(--foreground)' }));
        out.push(h('text', { key: 'v' + i, x: W - m.r + 8, y: y + 4, className: 'alt-graphics__val' }, nf(it.value) + '%'));
      });
      return out;
    } });
  }

  /* ---------- Bridge / waterfall ---------- */
  function Bridge(p0) {
    var p = def('bridge', p0), H = p.height || 200, fmt = p.format || f1;
    var endV = p.start.value + p.steps.reduce(function (a, s) { return a + s.value; }, 0);
    var bars = [{ label: p.start.label, a: 0, b: p.start.value, total: true }], acc = p.start.value;
    p.steps.forEach(function (s) { bars.push({ label: s.label, a: acc, b: acc + s.value, v: s.value }); acc += s.value; });
    bars.push({ label: p.end.label, a: 0, b: p.end.value != null ? p.end.value : endV, total: true });
    return h(Frame, { height: H, label: p.label, render: function (W, H, tp) {
      var m = { l: 32, r: 8, t: 20, b: 34 }, mx = Math.max.apply(null, bars.map(function (b) { return Math.max(b.a, b.b); }));
      var tk = ticks(p.min || 0, mx, 3), ymin = tk[0], ymax = tk[tk.length - 1];
      var Y = function (v) { return m.t + (1 - (Math.max(v, ymin) - ymin) / (ymax - ymin)) * (H - m.t - m.b); };
      var n = bars.length, gw = (W - m.l - m.r) / n, bw = Math.min(36, gw * 0.62);
      var out = yAxis(tk, m, W, Y, function (v) { return nf(v); });
      bars.forEach(function (b, i) {
        var x = m.l + gw * i + (gw - bw) / 2, top = Y(Math.max(b.a, b.b)), bot = Y(Math.min(b.a, b.b)), key = i;
        var c = b.total ? 'var(--chart-4)' : b.v >= 0 ? 'var(--chart-2)' : 'var(--chart-5)', on = tp.key == null || tp.key === key;
        var val = b.total ? fmt(b.b) : signed(b.v);
        out.push(h('path', Object.assign(tp.mark({ key: key, title: b.label, rows: [{ c: c, v: (b.total ? 'R$ ' : '') + val + ' mi', n: b.total ? 'total' : b.v >= 0 ? 'aumenta' : 'reduz' }] }, b.label + ': ' + val),
          { key: 'b' + i, d: barPath(x, top, bw, Math.max(1, bot - top), 3, b.total || b.v >= 0 ? 'up' : 'down'), fill: c, fillOpacity: on ? 1 : 0.4 })));
        out.push(h('text', { key: 'v' + i, x: x + bw / 2, y: top - 6, textAnchor: 'middle', className: 'alt-graphics__val' }, val));
        String(b.label).split(' ').forEach(function (w, wi, arr) {
          out.push(h('text', { key: 'x' + i + wi, x: x + bw / 2, y: H - (arr.length > 1 ? 20 - wi * 14 : 14), textAnchor: 'middle', className: 'alt-graphics__ax' }, w));
        });
        if (i < n - 1) { var yb = Y(b.b); out.push(h('line', { key: 'c' + i, x1: x + bw, x2: x + gw, y1: yb, y2: yb, className: 'alt-graphics__conn' })); }
      });
      return out;
    } });
  }

  /* ---------- Scatter ---------- */
  function Scatter(p0) {
    var p = def('scatter', p0), H = p.height || 200, thr = p.threshold;
    return h(Frame, { height: H, label: p.label, render: function (W, H, tp) {
      var m = { l: 34, r: 12, t: 10, b: 30 }, xt = ticks(0, p.xMax, 3), yt = ticks(0, p.yMax, 2), xmax = xt[xt.length - 1], ymax = yt[yt.length - 1];
      var X = function (v) { return m.l + v / xmax * (W - m.l - m.r); }, Y = function (v) { return m.t + (1 - v / ymax) * (H - m.t - m.b); };
      var out = yAxis(yt, m, W, Y, f1);
      xt.forEach(function (v, i) { out.push(h('text', { key: 'xt' + i, x: X(v), y: H - 16, textAnchor: i === 0 ? 'start' : 'middle', className: 'alt-graphics__ax' }, nf(v))); });
      out.push(h('text', { key: 'xl', x: W - m.r, y: H - 2, textAnchor: 'end', className: 'alt-graphics__ax' }, p.xLabel));
      var sx = 0, sy = 0, sxy = 0, sxx = 0, n = p.points.length;
      p.points.forEach(function (q) { sx += q.x; sy += q.y; sxy += q.x * q.y; sxx += q.x * q.x; });
      var b = (n * sxy - sx * sy) / (n * sxx - sx * sx), a = (sy - b * sx) / n;
      out.push(h('line', { key: 'tr', x1: X(0), y1: Y(Math.max(0, a)), x2: X(xmax), y2: Y(a + b * xmax), stroke: 'var(--chart-6)', strokeWidth: 1.5, strokeDasharray: '6 4' }));
      p.points.forEach(function (q, i) {
        var rpt = q.y * 1e6 / q.x, hi = thr && rpt > thr, c = hi ? SERIES[1] : SERIES[0], on = tp.key == null || tp.key === i;
        var g = h('g', Object.assign(tp.mark({ key: i, title: q.label, rows: [{ c: c, v: nf(q.x) + ' t', n: 'volume' }, { c: c, v: 'R$ ' + nf(q.y, 2) + ' mi', n: 'faturamento' }, { v: 'R$ ' + nf(rpt) + '/t' }] }, q.label + ': ' + nf(q.x) + ' t, R$ ' + nf(q.y, 2) + ' mi'), { key: 'p' + i }),
          h('circle', { cx: X(q.x), cy: Y(q.y), r: 12, fill: 'transparent' }),
          h('circle', { cx: X(q.x), cy: Y(q.y), r: tp.key === i ? 6 : 5, fill: c, fillOpacity: on ? 1 : 0.35, className: 'alt-graphics__ring' }));
        out.push(g);
      });
      return out;
    } });
  }

  /* ---------- Sparkline ---------- */
  function Sparkline(p) {
    var data = p.data || D.kpi.items[0].spark, c = p.color || SERIES[0], H = p.height || 32;
    var mn = Math.min.apply(null, data), mx = Math.max.apply(null, data), rg = mx - mn || 1;
    var pts = data.map(function (v, i) { return [i / (data.length - 1) * 100, 3 + (1 - (v - mn) / rg) * (H - 6)]; });
    var d = 'M' + pts.map(function (q) { return q[0].toFixed(2) + ' ' + q[1].toFixed(2); }).join('L');
    return h('svg', { className: cx('alt-graphics__spark', p.className), viewBox: '0 0 100 ' + H, preserveAspectRatio: 'none', style: { height: H }, 'aria-hidden': 'true' },
      p.area === false ? null : h('path', { d: d + 'L100 ' + H + 'L0 ' + H + 'Z', fill: c, fillOpacity: 0.12 }),
      h('path', { d: d, fill: 'none', stroke: c, strokeWidth: 2, vectorEffect: 'non-scaling-stroke', strokeLinejoin: 'round' }));
  }

  /* ---------- KPI tiles ---------- */
  function Kpi(p) {
    var good = p.delta === 0 ? null : (p.delta > 0) === (p.upIsGood !== false);
    return h('div', { className: cx('alt-graphics__kpi alt-glass', p.className) },
      h('span', { className: 'alt-graphics__kpi-l' }, p.label),
      h('b', { className: 'alt-graphics__kpi-v alt-num' }, p.value),
      h('div', { className: 'alt-graphics__kpi-r' },
        p.delta != null ? h('span', { className: 'alt-graphics__delta alt-num', 'data-good': good == null ? null : String(good) },
          h(A.Icon, { name: p.delta >= 0 ? 'arrow-up' : 'arrow-down', size: 13 }), signed(p.delta) + (p.unit || '%')) : null,
        h('span', { className: 'alt-graphics__kpi-p' }, p.period)),
      p.spark ? h(Sparkline, { data: p.spark, color: p.sparkColor, height: 28 }) : null);
  }
  function Kpis(p0) {
    var p = def('kpi', p0);
    return h('div', { className: cx('alt-graphics__kpis', p.className) }, p.items.map(function (it, i) { return h(Kpi, Object.assign({ key: i }, it)); }));
  }

  /* ---------- Legend ---------- */
  function Legend(p) {
    return h('div', { className: cx('alt-graphics__legend', p.className) }, (p.items || []).map(function (it, i) {
      return h('span', { key: i }, h('i', { className: 'alt-graphics__key alt-graphics__key--' + (it.kind || 'sq'), style: { '--c': it.color } }), it.name);
    }));
  }

  /* ---------- Card ---------- */
  function Card(p) {
    var st = R.useState(false), asTable = st[0], mn = R.useState(false), menu = mn[0];
    R.useEffect(function () {
      if (!menu) return;
      var f = function (e) { if (e.key === 'Escape') mn[1](false); };
      var c = function () { mn[1](false); };
      document.addEventListener('keydown', f); document.addEventListener('click', c);
      return function () { document.removeEventListener('keydown', f); document.removeEventListener('click', c); };
    }, [menu]);
    var acts = p.menu || [{ id: 'csv', label: 'Exportar CSV', icon: 'download' }, { id: 'png', label: 'Exportar PNG', icon: 'download' }, { id: 'full', label: 'Tela cheia', icon: 'external' }];
    return h('section', { className: cx('alt-graphics__card alt-glass', p.className), style: p.style },
      h('header', { className: 'alt-graphics__hd' },
        h('div', { className: 'alt-graphics__ttw' }, h('h3', { className: 'alt-graphics__tt' }, p.title), p.subtitle ? h('span', { className: 'alt-graphics__sub' }, p.subtitle) : null),
        p.actions,
        p.table ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-pressed': asTable, 'aria-label': 'Ver como tabela', title: 'Ver como tabela', onClick: function () { st[1](!asTable); } }, h(A.Icon, { name: 'table', size: 16 })) : null,
        p.menu === false ? null : h('div', { className: 'alt-graphics__more' },
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Mais opções', 'aria-haspopup': 'menu', 'aria-expanded': menu, onClick: function (e) { e.stopPropagation(); mn[1](!menu); } }, h(A.Icon, { name: 'more-v', size: 16 })),
          menu ? h('div', { className: 'alt-popover alt-graphics__menu', role: 'menu' }, acts.map(function (a) {
            return h('button', { key: a.id, type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: function () { mn[1](false); p.onAction && p.onAction(a.id); } }, h(A.Icon, { name: a.icon, size: 16 }), a.label);
          })) : null)),
      p.legend && p.legend.length && !asTable ? h(Legend, { items: p.legend }) : null,
      p.toolbar,
      h('div', { className: 'alt-graphics__body' }, asTable && p.table ? h(DataTable, p.table) : p.children),
      p.note ? h('p', { className: 'alt-graphics__note' }, p.note) : null);
  }
  function DataTable(p) {
    return h('div', { className: 'alt-graphics__tablewrap alt-scroll' }, h('table', { className: 'alt-graphics__table alt-num' },
      h('thead', null, h('tr', null, p.head.map(function (c, i) { return h('th', { key: i }, c); }))),
      h('tbody', null, p.rows.map(function (r, i) { return h('tr', { key: i }, r.map(function (c, j) { return h('td', { key: j }, c); })); }))));
  }

  /* ---------- type registry ---------- */
  function lg(series, kind) { return series.map(function (s, i) { return { name: s.name, color: colorOf(s, i), kind: s.dashed ? 'ref' : kind }; }); }
  var TYPES = {
    line: { C: Line, legend: function (p) { var l = lg(p.series, 'line'); if (p.reference) l.push({ name: 'Meta', color: 'var(--muted)', kind: 'ref' }); if (p.projection) l.push({ name: 'Projeção', color: colorOf(p.series[0], 0), kind: 'proj' }); return l; },
      table: function (p) { return { head: ['Dia'].concat(p.series.map(function (s) { return s.name; })), rows: p.x.map(function (x, i) { return [x].concat(p.series.map(function (s) { return s.data[i] == null ? '—' : (p.format || f1)(s.data[i]); })); }) }; } },
    columns: { C: Columns, legend: function (p) { var l = lg(p.series, 'sq'); if (p.partialLast) l.push({ name: p.partialLabel, color: colorOf(p.series[0], 0), kind: 'partial' }); return l; },
      table: function (p) { return { head: ['Mês'].concat(p.series.map(function (s) { return s.name; })), rows: p.categories.map(function (c, i) { return [c].concat(p.series.map(function (s) { return (p.format || f1)(s.data[i]); })); }) }; } },
    hbar: { C: HBar, legend: function () { return null; }, table: function (p) { return { head: ['Item', 'Valor'], rows: p.items.map(function (it) { return [it.label, (p.format || f1)(it.value)]; }) }; } },
    stacked: { C: Stacked, legend: function (p) { return p.parts.map(function (n, i) { return { name: n, color: SERIES[i] }; }); },
      table: function (p) { return { head: [''].concat(p.parts), rows: p.rows.map(function (r) { return [r.label].concat(r.values.map(function (v) { return v + '%'; })); }) }; } },
    donut: { C: Donut, legend: function () { return null; }, table: function (p) { return { head: ['Categoria', 'Valor'], rows: p.items.map(function (it) { return [it.label, nf(it.value)]; }) }; } },
    heatmap: { C: Heatmap, legend: function () { return null; }, table: function (p) { return { head: [''].concat(p.cols), rows: p.rowsLabels.map(function (r, i) { return [r].concat(p.values[i].map(function (v) { return nf(v); })); }) }; } },
    bullet: { C: Bullet, legend: function () { return [{ name: 'Realizado', color: SERIES[0] }, { name: 'Meta (100%)', color: 'var(--foreground)', kind: 'tick' }, { name: 'Faixa até 120%', color: 'var(--track)' }]; },
      table: function (p) { return { head: ['Filial', '% da meta'], rows: p.items.map(function (it) { return [it.label, nf(it.value) + '%']; }) }; } },
    bridge: { C: Bridge, legend: function () { return [{ name: 'Total', color: 'var(--chart-4)' }, { name: 'Aumenta', color: 'var(--chart-2)' }, { name: 'Reduz', color: 'var(--chart-5)' }]; },
      table: function (p) { return { head: ['Etapa', 'R$ mi'], rows: [[p.start.label, f1(p.start.value)]].concat(p.steps.map(function (s) { return [s.label, signed(s.value)]; })) }; } },
    scatter: { C: Scatter, legend: function (p) { return [{ name: 'Dentro da tendência', color: SERIES[0], kind: 'dot' }, { name: 'Acima de R$ ' + nf(p.threshold) + '/t', color: SERIES[1], kind: 'dot' }, { name: 'Tendência', color: 'var(--chart-6)', kind: 'ref' }]; },
      table: function (p) { return { head: ['Cliente', 'Volume (t)', 'R$ mi'], rows: p.points.map(function (q) { return [q.label, nf(q.x), nf(q.y, 2)]; }) }; } },
    sparkline: { C: Sparkline },
    kpi: { C: Kpis }
  };

  function Graphics(p) {
    var type = p.type || 'line', T = TYPES[type] || TYPES.line, d = def(type, p);
    if (p.bare || type === 'kpi' || type === 'sparkline') return h(T.C, d);
    var legend = p.legend !== undefined ? p.legend : (T.legend ? T.legend(d) : null);
    var toolbar = type === 'heatmap' ? h(SeqLegend, { max: 'mais (≥ ' + nf(d.thresholds[4]) + ')' }) : null;
    return h(Card, { title: d.title, subtitle: d.subtitle, legend: legend, note: d.note, toolbar: toolbar, table: T.table ? T.table(d) : null,
      className: cx('alt-graphics', 'alt-graphics--' + type, p.className), style: p.style, onAction: p.onAction, actions: p.actions, menu: p.menu }, h(T.C, d));
  }
  Graphics.Line = Line; Graphics.Columns = Columns; Graphics.Bar = Columns; Graphics.HBar = HBar; Graphics.Stacked = Stacked;
  Graphics.Donut = Donut; Graphics.Heatmap = Heatmap; Graphics.Bullet = Bullet; Graphics.Bridge = Bridge; Graphics.Scatter = Scatter;
  Graphics.Sparkline = Sparkline; Graphics.Kpi = Kpi; Graphics.Kpis = Kpis; Graphics.Card = Card; Graphics.Legend = Legend;
  Graphics.Tip = Tip; Graphics.Frame = Frame; Graphics.DataTable = DataTable; Graphics.SeqLegend = SeqLegend;
  Graphics.util = { useSize: useSize, useTip: useTip, ticks: ticks, barPath: barPath, seq: seq, signed: signed, SERIES: SERIES };
  Graphics.defaults = D;
  A.Graphics = Graphics;
})();


/* ---- HdCaixaEntrada ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var DEF_FOLDERS = [
    { id: 'all', label: 'Visão geral', icon: 'layers' },
    { id: 'grupos', label: 'Meus grupos', icon: 'inbox' },
    { id: 'meus', label: 'Atribuídos a mim', icon: 'user' },
    { id: 'sem', label: 'Sem responsável', icon: 'help' }
  ];
  var DEF_TICKETS = [
    { id: 4821, subject: 'Integração TMS fora do ar', requester: 'Ana Ribeiro', ago: '12min', unread: true, status: 'open', folders: ['grupos', 'meus'],
      sla: { status: 'breached', time: '−2h' }, gut: 64, openedAt: '30/09 08:12', responsavel: 'Carlos Lima', grupo: 'Sistemas', categoria: 'Integrações', idade: '6h 25min', awaitingRating: true,
      messages: [{ id: 1, author: 'Ana Ribeiro', time: '08:12', text: 'O painel não recebe viagens desde as 7h.' }, { id: 2, author: 'Carlos Lima', time: '08:40', text: 'Verificando a fila de integração.', me: true }] },
    { id: 4819, subject: 'Motorista sem acesso ao app de entregas', requester: 'João Pereira', ago: '35min', unread: true, status: 'open', folders: ['grupos', 'sem'],
      sla: { status: 'warning', time: '1h' }, gut: 36, openedAt: '30/09 13:55', responsavel: null, grupo: 'Suporte', categoria: 'Acesso', idade: '35min',
      messages: [{ id: 1, author: 'João Pereira', time: '13:55', text: 'O app pede nova senha e não aceita o código enviado.' }] },
    { id: 4817, subject: 'Ajuste de rota no painel', requester: 'Carlos Lima', ago: '1h', unread: false, status: 'open', folders: ['grupos', 'meus'],
      sla: { status: 'warning', time: '3h' }, gut: 27, openedAt: '30/09 13:20', responsavel: 'Carlos Lima', grupo: 'Operações', categoria: 'Roteirização', idade: '1h', locked: true, lockReason: 'Aguardando fornecedor do TMS',
      messages: [{ id: 1, author: 'Carlos Lima', time: '13:20', text: 'A rota Jacareí → Santos aparece com desvio.' }] },
    { id: 4812, subject: 'Relatório de ocupação com valores zerados', requester: 'Marta Souza', ago: '3h', unread: true, status: 'open', folders: ['grupos', 'meus'],
      sla: { status: 'ok', time: '5h' }, gut: 18, openedAt: '30/09 11:05', responsavel: 'Carlos Lima', grupo: 'Sistemas', categoria: 'Relatórios', idade: '3h',
      messages: [{ id: 1, author: 'Marta Souza', time: '11:05', text: 'O relatório de setembro mostra ocupação 0% para Bitrem.' }] },
    { id: 4808, subject: 'Cadastro de nova placa QRT-4F56', requester: 'Pedro Melo', ago: '5h', unread: true, status: 'open', folders: ['grupos', 'sem'],
      sla: { status: 'none' }, gut: 6, openedAt: '30/09 09:40', responsavel: null, grupo: 'Cadastros', categoria: 'Veículos', idade: '5h',
      messages: [{ id: 1, author: 'Pedro Melo', time: '09:40', text: 'Preciso incluir a carreta QRT-4F56 na frota de Jacareí.' }] },
    { id: 4790, subject: 'CT-e sem vínculo com a nota fiscal', requester: 'Marta Souza', ago: '8 dias', unread: false, status: 'closed', folders: ['grupos', 'meus'],
      sla: { status: 'ok', time: '5h' }, gut: 4, openedAt: '22/09 10:02', responsavel: 'Carlos Lima', grupo: 'Fiscal', categoria: 'CT-e', idade: '1 dia', closed: true, messages: [] }
  ];
  var SEG = [['open', 'Abertos'], ['closed', 'Fechados'], ['all', 'Todos']];

  function HdCaixaEntrada(p) {
    var folders = p.folders || DEF_FOLDERS, tickets = p.tickets || DEF_TICKETS;
    var sF = R.useState(p.defaultFolder || 'grupos'), folder = p.folder !== undefined ? p.folder : sF[0];
    var sS = R.useState(p.defaultStatus || 'open'), status = p.status !== undefined ? p.status : sS[0];
    var sQ = R.useState(''), q = sQ[0];
    var sSel = R.useState(p.defaultSelectedId !== undefined ? p.defaultSelectedId : null), selId = p.selectedId !== undefined ? p.selectedId : sSel[0];
    var sRead = R.useState(function () { var r = {}; if (p.defaultSelectedId != null) r[p.defaultSelectedId] = true; return r; }), read = sRead[0];
    var Sla = A.HdSlaGut && A.HdSlaGut.Sla, Gut = A.HdSlaGut && A.HdSlaGut.Gut;

    function isUnread(t) { return t.unread && !read[t.id]; }
    function inFolder(t, f) { return f === 'all' || (t.folders || []).indexOf(f) >= 0; }
    function setFolder(f) { sF[1](f); if (p.onFolderChange) p.onFolderChange(f); }
    function setStatus(s) { sS[1](s); if (p.onStatusChange) p.onStatusChange(s); }
    function select(t) {
      sSel[1](t.id); var r = Object.assign({}, read); r[t.id] = true; sRead[1](r);
      if (p.onSelect) p.onSelect(t);
    }

    var ql = q.trim().toLowerCase();
    var list = tickets.filter(function (t) {
      return inFolder(t, folder) && (status === 'all' || t.status === status) &&
        (!ql || (t.subject + ' ' + t.requester + ' ' + t.id).toLowerCase().indexOf(ql) >= 0);
    });
    var sel = tickets.filter(function (t) { return t.id === selId; })[0];
    var fLabel = (folders.filter(function (f) { return f.id === folder; })[0] || {}).label;

    function onListKey(e) {
      if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
      e.preventDefault();
      var i = list.findIndex(function (t) { return t.id === selId; });
      var n = list[Math.max(0, Math.min(list.length - 1, i + (e.key === 'ArrowDown' ? 1 : -1)))];
      if (n) { select(n); var el = e.currentTarget.querySelector('[data-id="' + n.id + '"]'); if (el) el.focus(); }
    }

    var detail = sel
      ? (p.renderDetail ? p.renderDetail(sel) : A.HdChamado ? h(A.HdChamado, {
          key: sel.id, className: 'alt-hd-caixa-entrada__chamado', currentUser: p.currentUser,
          ticket: Object.assign({}, sel, { openedBy: sel.requester, gut: undefined, gutScore: sel.gut }), messages: sel.messages || [] }) : null)
      : h('div', { className: 'alt-glass alt-hd-caixa-entrada__empty' },
          h('span', { className: 'alt-hd-caixa-entrada__empty-ico' }, h(A.Icon, { name: 'message', size: 22 })),
          h('b', null, p.emptyTitle || 'Nenhum chamado selecionado'),
          h('span', null, p.emptyText || 'Escolha um chamado na lista para ver os detalhes.'));

    return h('div', { className: cx('alt-hd-caixa-entrada', p.className), style: Object.assign({ height: p.height || 640 }, p.style) },
      h('nav', { className: 'alt-glass alt-hd-caixa-entrada__folders', 'aria-label': 'Pastas' },
        h('button', { type: 'button', className: 'alt-btn alt-btn--primary alt-hd-caixa-entrada__new', onClick: p.onNew }, h(A.Icon, { name: 'plus', size: 16 }), p.newLabel || 'Novo chamado'),
        folders.map(function (f) {
          var inF = tickets.filter(function (t) { return inFolder(t, f.id) && t.status === 'open'; });
          var unread = inF.filter(isUnread).length;
          return h('button', { key: f.id, type: 'button', className: 'alt-hd-caixa-entrada__fi', 'aria-current': folder === f.id ? 'true' : undefined, onClick: function () { setFolder(f.id); } },
            h(A.Icon, { name: f.icon || 'inbox', size: 16 }),
            h('span', { className: 'alt-hd-caixa-entrada__fi-l' }, f.label),
            f.count != null ? h('span', { className: 'alt-hd-caixa-entrada__total alt-num' }, f.count)
              : unread ? h('span', { className: 'alt-count', 'aria-label': unread + ' não lidos' }, unread)
              : h('span', { className: 'alt-hd-caixa-entrada__total alt-num', 'aria-label': inF.length + ' abertos' }, inF.length));
        })),
      h('section', { className: 'alt-panel alt-hd-caixa-entrada__list', 'aria-label': 'Chamados' },
        h('div', { className: 'alt-hd-caixa-entrada__lh' },
          h('div', { className: 'alt-hd-caixa-entrada__lt' }, h('h2', null, fLabel), h('span', { className: 'alt-num' }, list.length)),
          h('label', { className: 'alt-ctl alt-ctl--sm' }, h(A.Icon, { name: 'search', size: 15 }),
            h('input', { type: 'search', placeholder: p.searchPlaceholder || 'Buscar chamados', 'aria-label': 'Buscar chamados', value: q, onChange: function (e) { sQ[1](e.target.value); } })),
          h('div', { className: 'alt-hd-caixa-entrada__seg', role: 'group', 'aria-label': 'Situação' }, SEG.map(function (s) {
            return h('button', { key: s[0], type: 'button', 'aria-pressed': status === s[0], onClick: function () { setStatus(s[0]); } }, s[1]);
          }))),
        h('div', { className: 'alt-hd-caixa-entrada__items alt-scroll', role: 'listbox', 'aria-label': 'Lista de chamados', onKeyDown: onListKey },
          list.length ? list.map(function (t) {
            var un = isUnread(t), on = t.id === selId;
            return h('button', { key: t.id, 'data-id': t.id, type: 'button', role: 'option', 'aria-selected': on, tabIndex: on || (!selId && t === list[0]) ? 0 : -1,
              className: cx('alt-hd-caixa-entrada__it', 'alt-num', un && 'is-unread'), onClick: function () { select(t); } },
              h('span', { className: 'alt-hd-caixa-entrada__l1' },
                un ? h('span', { className: 'alt-hd-caixa-entrada__dot', title: 'Não lido' }, h('span', { className: 'alt-hd-caixa-entrada__sr' }, 'Não lido')) : null,
                h('span', { className: 'alt-hd-caixa-entrada__who' }, t.requester),
                h('span', { className: 'alt-hd-caixa-entrada__ago' }, t.ago)),
              h('span', { className: 'alt-hd-caixa-entrada__subj' }, h('span', { className: 'alt-hd-caixa-entrada__id' }, '#' + t.id), ' ', t.subject),
              h('span', { className: 'alt-hd-caixa-entrada__l3' },
                Sla ? h(Sla, Object.assign({ compact: true }, t.sla)) : null,
                Gut ? h(Gut, { score: t.gut, variant: 'outlined' }) : null,
                t.locked ? h('span', { className: 'alt-hd-caixa-entrada__lock', title: 'Travado' }, h(A.Icon, { name: 'lock', size: 13 }), 'Travado') : null,
                t.status === 'closed' ? h('span', { className: 'alt-hd-caixa-entrada__lock' }, h(A.Icon, { name: 'check', size: 13 }), 'Fechado') : null));
          }) : h('div', { className: 'alt-hd-caixa-entrada__none' }, p.noResultsText || 'Nenhum chamado nesta pasta.'))),
      h('div', { className: 'alt-hd-caixa-entrada__det' }, detail));
  }
  A.HdCaixaEntrada = HdCaixaEntrada;
})();


/* ---- HdChamado ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  var DEF_TICKET = {
    id: 4821, subject: 'Integração TMS fora do ar', openedBy: 'Ana Ribeiro', openedAt: '30/09 08:12',
    sla: { status: 'breached', time: '−2h' }, gut: { g: 4, u: 4, t: 4 },
    responsavel: 'Carlos Lima', grupo: 'Sistemas', categoria: 'Integrações', idade: '6h 25min',
    locked: false, closed: false, awaitingRating: true
  };
  var DEF_MSGS = [
    { id: 1, author: 'Ana Ribeiro', time: '08:12', text: 'O painel não recebe viagens desde as 7h. As cargas de Paulínia não aparecem no TMS.', attachments: [{ name: 'print-painel.png', size: '182 KB' }] },
    { id: 2, author: 'Carlos Lima', time: '08:40', text: 'Verificando a fila de integração.', me: true },
    { id: 3, author: 'Carlos Lima', time: '09:02', text: 'A fila voltou a processar. Pode confirmar se as viagens aparecem?', me: true }
  ];
  var TYPES = ['Resposta', 'Nota interna', 'Solução'];

  function Btn(p) {
    return h('button', Object.assign({ type: 'button' }, K.omit(p, ['primary', 'icon', 'children', 'className']), {
      className: cx(p.primary ? 'alt-btn alt-btn--primary alt-hd-chamado__primary' : 'alt-tbtn', p.className)
    }), p.icon ? h(A.Icon, { name: p.icon, size: 15 }) : null, p.children);
  }

  function Banner(p) {
    var t = tone(p.tone);
    return h('div', { className: 'alt-hd-chamado__alert', role: 'status', style: { background: t.bg } },
      h('span', { style: { color: t.c } }, h(A.Icon, { name: p.icon || t.icon, size: 16 })),
      h('div', { className: 'alt-hd-chamado__alert-tx' }, h('b', null, p.title), p.text ? h('span', null, p.text) : null),
      p.children);
  }

  function HdChamado(p) {
    var t0 = Object.assign({}, DEF_TICKET, p.ticket || {});
    var stT = R.useState(t0), tk = stT[0], setTk = stT[1];
    var stM = R.useState(p.messages || DEF_MSGS), msgs = stM[0], setMsgs = stM[1];
    var stTx = R.useState(''), text = stTx[0], setText = stTx[1];
    var stTy = R.useState(TYPES[0]), type = stTy[0], setType = stTy[1];
    var stF = R.useState([]), files = stF[0], setFiles = stF[1];
    var stD = R.useState(false), drag = stD[0], setDrag = stD[1];
    var stR = R.useState(0), rating = stR[0], setRating = stR[1];
    var stRO = R.useState(false), rateOpen = stRO[0], setRateOpen = stRO[1];
    var fileRef = R.useRef(null);
    var me = p.currentUser || 'Carlos Lima';
    var Sla = A.HdSlaGut && A.HdSlaGut.Sla, Gut = A.HdSlaGut && A.HdSlaGut.Gut;
    var disabled = tk.locked || tk.closed;

    function patch(o) { setTk(function (x) { return Object.assign({}, x, o); }); }
    function addFiles(list) {
      var arr = Array.prototype.slice.call(list || []).map(function (f) { return { name: f.name, size: Math.max(1, Math.round(f.size / 1024)) + ' KB' }; });
      if (arr.length) setFiles(function (x) { return x.concat(arr); });
    }
    function send() {
      if (!text.trim() && !files.length) return;
      var m = { id: Date.now(), author: me, time: p.now || 'agora', text: text.trim(), me: true, type: type, attachments: files };
      setMsgs(function (x) { return x.concat([m]); }); setText(''); setFiles([]);
      if (p.onSend) p.onSend(m);
    }

    var vitals = [
      ['SLA', Sla ? h(Sla, tk.sla) : tk.sla.time],
      ['GUT', Gut ? h(Gut, { value: tk.gut, score: tk.gutScore, short: true }) : null],
      ['Responsável', tk.responsavel || h('span', { className: 'alt-hd-chamado__none' }, 'Sem responsável')],
      ['Grupo', tk.grupo], ['Categoria', tk.categoria], ['Aberto há', tk.idade]
    ];

    return h('article', {
      className: cx('alt-glass', 'alt-hd-chamado', p.className), style: p.style, 'aria-label': 'Chamado #' + tk.id,
      onDragOver: function (e) { if (disabled) return; e.preventDefault(); setDrag(true); },
      onDragLeave: function (e) { if (e.currentTarget === e.target || !e.currentTarget.contains(e.relatedTarget)) setDrag(false); },
      onDrop: function (e) { if (disabled) return; e.preventDefault(); setDrag(false); addFiles(e.dataTransfer.files); }
    },
      h('header', { className: 'alt-hd-chamado__hd alt-num' },
        h('div', { className: 'alt-hd-chamado__hd-main' },
          h('div', { className: 'alt-hd-chamado__sub' }, '#' + tk.id + ' · aberto por ' + tk.openedBy + ' em ' + tk.openedAt),
          h('h2', { className: 'alt-hd-chamado__subject' }, tk.subject)),
        tk.locked ? h('span', { className: 'alt-chip', style: { background: tone('warning').bg, color: tone('warning').c } }, h(A.Icon, { name: 'lock', size: 13 }), 'Travado') : null,
        tk.closed ? h('span', { className: 'alt-chip', style: { background: tone('success').bg, color: tone('success').c } }, h(A.Icon, { name: 'check-circle', size: 13 }), 'Fechado') : null,
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Mais ações', onClick: p.onMore }, h(A.Icon, { name: 'more-v', size: 16 }))),
      h('div', { className: 'alt-hd-chamado__vit alt-num' },
        h('dl', { className: 'alt-hd-chamado__vit-grid' }, vitals.map(function (v) {
          return h('div', { key: v[0] }, h('dt', null, v[0]), h('dd', null, v[1]));
        })),
        tk.responsavel === me || tk.closed ? null : h(Btn, { icon: 'user', onClick: function () { patch({ responsavel: me }); if (p.onAssume) p.onAssume(tk); } }, 'Assumir')),
      msgs.length === 0 ? h('div', { className: 'alt-hd-chamado__empty' }, p.emptyText || 'Sem mensagens neste chamado.') : h('div', { className: 'alt-hd-chamado__msgs alt-scroll', style: p.maxConversationHeight ? { maxHeight: p.maxConversationHeight } : null, 'aria-live': 'polite' },
        msgs.map(function (m) {
          return h('div', { key: m.id, className: cx('alt-hd-chamado__bb', m.me && 'is-me', m.type === 'Nota interna' && 'is-note') },
            h('div', { className: 'alt-hd-chamado__bb-meta alt-num' }, m.author + ' · ' + m.time + (m.type && m.type !== 'Resposta' ? ' · ' + m.type : '')),
            m.text ? h('div', null, m.text) : null,
            m.attachments && m.attachments.length ? h('div', { className: 'alt-hd-chamado__files' }, m.attachments.map(function (f, i) {
              return h('span', { key: i, className: 'alt-chip alt-hd-chamado__file', title: f.name + (f.size ? ' · ' + f.size : '') }, h(A.Icon, { name: 'file', size: 13 }), f.name);
            })) : null);
        })),
      tk.locked ? h(Banner, { tone: 'warning', icon: 'lock', title: 'Chamado travado — destrave para interagir', text: tk.lockReason },
        h(Btn, { icon: null, onClick: function () { patch({ locked: false }); if (p.onUnlock) p.onUnlock(tk); } }, 'Destravar')) : null,
      !tk.locked && tk.awaitingRating && !tk.closed ? h(Banner, { tone: 'info', icon: 'star', title: rating ? 'Obrigado pela avaliação' : 'Avalie este atendimento', text: rating ? rating + ' de 5 estrelas registradas' : 'Seu feedback ajuda a melhorar o suporte' },
        rating ? null : rateOpen
          ? h('div', { className: 'alt-hd-chamado__rate', role: 'radiogroup', 'aria-label': 'Nota' }, [1, 2, 3, 4, 5].map(function (n) {
              return h('button', { key: n, type: 'button', role: 'radio', 'aria-checked': false, 'aria-label': n + ' estrelas', onClick: function () { setRating(n); if (p.onRate) p.onRate(n, tk); } }, h(A.Icon, { name: 'star', size: 18 }));
            }))
          : h(Btn, { onClick: function () { setRateOpen(true); } }, 'Avaliar agora')) : null,
      tk.closed ? h(Banner, { tone: 'success', icon: 'check-circle', title: 'Chamado fechado', text: tk.closedText || 'Reabra para continuar a conversa' },
        h(Btn, { icon: 'refresh', onClick: function () { patch({ closed: false }); if (p.onReopen) p.onReopen(tk); } }, 'Reabrir')) : null,
      tk.closed && p.hideComposerWhenClosed !== false ? null : h('div', { className: cx('alt-hd-chamado__cmp', disabled && 'is-disabled', drag && 'is-drag') },
        h('label', { className: 'alt-hd-chamado__sr', htmlFor: 'hdc-msg-' + tk.id }, 'Mensagem'),
        h('textarea', { id: 'hdc-msg-' + tk.id, className: 'alt-hd-chamado__ta', placeholder: p.placeholder || 'Mensagem...', value: text, disabled: disabled, rows: 2,
          onChange: function (e) { setText(e.target.value); },
          onKeyDown: function (e) { if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); send(); } } }),
        files.length ? h('div', { className: 'alt-hd-chamado__files alt-hd-chamado__files--cmp' }, files.map(function (f, i) {
          return h('span', { key: i, className: 'alt-chip alt-hd-chamado__file' }, h(A.Icon, { name: 'file', size: 13 }), f.name,
            h('button', { type: 'button', className: 'alt-hd-chamado__x', 'aria-label': 'Remover ' + f.name, onClick: function () { setFiles(files.filter(function (_, j) { return j !== i; })); } }, h(A.Icon, { name: 'x', size: 12 })));
        })) : null,
        h('div', { className: 'alt-hd-chamado__ft' },
          h('label', { className: 'alt-ctl alt-ctl--sm alt-hd-chamado__type' },
            h('span', { className: 'alt-hd-chamado__sr' }, 'Tipo'),
            h('select', { value: type, disabled: disabled, onChange: function (e) { setType(e.target.value); } }, TYPES.map(function (t) { return h('option', { key: t }, t); })),
            h(A.Icon, { name: 'chevron-down', size: 14 })),
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Anexar arquivo', disabled: disabled, onClick: function () { fileRef.current && fileRef.current.click(); } }, h(A.Icon, { name: 'paperclip', size: 16 })),
          h('input', { ref: fileRef, type: 'file', multiple: true, hidden: true, onChange: function (e) { addFiles(e.target.files); e.target.value = ''; } }),
          h('span', { className: 'alt-hd-chamado__hint' }, 'Ctrl + Enter envia'),
          h('span', { style: { flex: 1 } }),
          h(Btn, { primary: true, disabled: disabled || (!text.trim() && !files.length), onClick: send }, h(A.Icon, { name: 'send', size: 15 }), 'Enviar')),
        drag ? h('div', { className: 'alt-hd-chamado__drop' }, h(A.Icon, { name: 'upload', size: 18 }), 'Solte os arquivos para anexar') : null));
  }
  A.HdChamado = HdChamado;
})();


/* ---- HdDashboard ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf, tone = K.tone;

  var PERIODS = [['7d', '7 dias'], ['30d', '30 dias'], ['90d', '90 dias'], ['ano', 'Ano'], ['tudo', 'Tudo']];
  var RANGES = { '7d': ['24/09/2026', '30/09/2026'], '30d': ['01/09/2026', '30/09/2026'], '90d': ['03/07/2026', '30/09/2026'], 'ano': ['01/01/2026', '30/09/2026'], 'tudo': ['12/03/2023', '30/09/2026'] };
  var SCALE = { '7d': 0.24, '30d': 1, '90d': 2.8, 'ano': 8.6, 'tudo': 27, custom: 1 };

  /* Dados de exemplo por período (o consumidor passa `data` ou `getData(period)`). */
  function sampleData(period) {
    var k = SCALE[period] || 1, r = function (n) { return Math.max(1, Math.round(n * k)); };
    var drift = { '7d': -2, '30d': 0, '90d': 1, 'ano': 2, 'tudo': 3 }[period] || 0;
    return {
      kpis: [
        { id: 'res', label: 'Taxa de resolução', value: (91 + drift) + '%', delta: '+3 p.p.', trend: 'up', good: true, icon: 'check-circle' },
        { id: 'sla', label: 'Conformidade de SLA', value: (86 - drift) + '%', delta: '−2 p.p.', trend: 'down', good: false, icon: 'clock' },
        { id: 'tm', label: 'Tempo médio', value: '7h 40min', delta: '−35min', trend: 'down', good: true, icon: 'activity' },
        { id: 'resp', label: 'Responsáveis ativos', value: String(period === '7d' ? 9 : 12), delta: 'sem variação', trend: null, icon: 'users' }
      ],
      status: [{ key: 'open', label: 'Abertos', value: r(38), color: 'var(--chart-2)' }, { key: 'closed', label: 'Fechados', value: r(50), color: 'var(--chart-1)' }, { key: 'other', label: 'Outros', value: r(12), color: 'var(--chart-6)' }],
      ratings: { avg: 4.6, dist: [r(92), r(24), r(7), r(3), r(2)] },
      groups: [{ label: 'Sistemas', value: r(34) }, { label: 'Operações', value: r(27) }, { label: 'Fiscal', value: r(18) }, { label: 'Cadastros', value: r(12) }, { label: 'Suporte', value: r(9) }],
      load: [{ label: 'Carlos Lima', open: 14, cap: 16 }, { label: 'Ana Ribeiro', open: 9, cap: 16 }, { label: 'Pedro Melo', open: 13, cap: 16 }, { label: 'Marta Souza', open: 5, cap: 16 }, { label: 'João Pereira', open: 3, cap: 12 }],
      breached: [
        { id: 4821, subject: 'Integração TMS fora do ar', grupo: 'Sistemas', time: '−2h' },
        { id: 4803, subject: 'Divergência de peso no romaneio', grupo: 'Operações', time: '−5h' },
        { id: 4799, subject: 'Nota fiscal rejeitada pela SEFAZ', grupo: 'Fiscal', time: '−1 dia' },
        { id: 4786, subject: 'Rastreador sem sinal na BR-116', grupo: 'Operações', time: '−1 dia' },
        { id: 4771, subject: 'Acesso do cliente Cebrace bloqueado', grupo: 'Suporte', time: '−2 dias' }
      ],
      breachedTotal: period === '7d' ? 5 : r(7)
    };
  }

  function Panel(p) {
    return h('section', { className: cx('alt-glass', 'alt-hd-dashboard__card', p.className), 'aria-label': p.title },
      h('header', { className: 'alt-hd-dashboard__ch' }, h('h3', null, p.title), p.extra || null), h('div', { className: 'alt-hd-dashboard__cb' }, p.children));
  }

  function Donut(p) {
    var total = p.items.reduce(function (s, i) { return s + i.value; }, 0), r = 46, C = 2 * Math.PI * r, acc = 0;
    var sH = R.useState(null), hot = sH[0];
    return h('div', { className: 'alt-hd-dashboard__donut alt-num' },
      h('svg', { width: 120, height: 120, viewBox: '0 0 120 120', role: 'img', 'aria-label': p.items.map(function (i) { return i.label + ' ' + i.value; }).join(', ') },
        h('circle', { cx: 60, cy: 60, r: r, fill: 'none', stroke: 'var(--track)', strokeWidth: 8 }),
        p.items.map(function (it) {
          var len = total ? (it.value / total) * C : 0, gap = p.items.length > 1 ? 3 : 0;
          var el = h('circle', { key: it.key, cx: 60, cy: 60, r: r, fill: 'none', stroke: it.color, strokeWidth: hot === it.key ? 11 : 8,
            strokeDasharray: Math.max(0, len - gap) + ' ' + (C - Math.max(0, len - gap)), strokeDashoffset: -acc, transform: 'rotate(-90 60 60)',
            opacity: hot && hot !== it.key ? 0.35 : 1, className: 'alt-hd-dashboard__arc' });
          acc += len; return el;
        }),
        h('text', { x: 60, y: 60, textAnchor: 'middle', className: 'alt-hd-dashboard__dt' }, nf(total)),
        h('text', { x: 60, y: 77, textAnchor: 'middle', className: 'alt-hd-dashboard__ds' }, 'chamados')),
      h('ul', { className: 'alt-hd-dashboard__legend' }, p.items.map(function (it) {
        return h('li', { key: it.key, onMouseEnter: function () { sH[1](it.key); }, onMouseLeave: function () { sH[1](null); }, tabIndex: 0, onFocus: function () { sH[1](it.key); }, onBlur: function () { sH[1](null); } },
          h('span', { className: 'alt-hd-dashboard__sw', style: { background: it.color } }),
          h('span', { className: 'alt-hd-dashboard__ll' }, it.label),
          h('b', null, nf(it.value)),
          h('span', { className: 'alt-hd-dashboard__pc' }, total ? Math.round(it.value / total * 100) + '%' : '—'));
      })));
  }

  function Bars(p) {
    var max = p.max || Math.max.apply(null, p.items.map(function (i) { return i.value; }).concat([1]));
    return h('ul', { className: 'alt-hd-dashboard__bars alt-num' }, p.items.map(function (it) {
      return h('li', { key: it.label },
        h('span', { className: 'alt-hd-dashboard__bl' }, it.label),
        h('span', { className: 'alt-hd-dashboard__bt' }, h('i', { style: { width: Math.round(it.value / max * 100) + '%', background: it.color || 'var(--teal-light)' } })),
        h('span', { className: 'alt-hd-dashboard__bv' }, it.text || nf(it.value)));
    }));
  }

  function HdDashboard(p) {
    var sP = R.useState(p.defaultPeriod || '30d'), period = p.period !== undefined ? p.period : sP[0];
    var sR = R.useState(null), custom = sR[0];
    var data = p.data || (p.getData ? p.getData(period) : sampleData(period));
    var range = custom || RANGES[period] || RANGES['30d'];
    var threshold = p.overloadThreshold || 0.75;
    function setPeriod(v) { sP[1](v); sR[1](null); if (p.onPeriodChange) p.onPeriodChange(v); }
    function setRange(i, v) { var nr = range.slice(); nr[i] = v; sR[1](nr); sP[1]('custom'); if (p.onRangeChange) p.onRangeChange(nr); }
    var ratTotal = data.ratings.dist.reduce(function (s, n) { return s + n; }, 0);
    var Sla = A.HdSlaGut && A.HdSlaGut.Sla;

    return h('div', { className: cx('alt-hd-dashboard', p.className), style: p.style },
      h('div', { className: 'alt-hd-dashboard__top' },
        h('h2', { className: 'alt-hd-dashboard__title' }, p.title || 'Visão geral'),
        h('div', { className: 'alt-hd-dashboard__seg', role: 'group', 'aria-label': 'Período' }, PERIODS.map(function (o) {
          return h('button', { key: o[0], type: 'button', 'aria-pressed': period === o[0], onClick: function () { setPeriod(o[0]); } }, o[1]);
        })),
        h('div', { className: 'alt-ctl alt-ctl--sm alt-hd-dashboard__range alt-num' },
          h(A.Icon, { name: 'calendar', size: 15 }),
          h('input', { 'aria-label': 'Data inicial', value: range[0], onChange: function (e) { setRange(0, e.target.value); } }),
          h('span', { 'aria-hidden': 'true', className: 'alt-hd-dashboard__arrow' }, '→'),
          h('input', { 'aria-label': 'Data final', value: range[1], onChange: function (e) { setRange(1, e.target.value); } }))),
      h('div', { className: 'alt-hd-dashboard__kpis alt-num' }, data.kpis.map(function (k) {
        var tn = k.trend ? tone(k.good ? 'success' : 'error') : null;
        return h('div', { key: k.id, className: 'alt-glass alt-hd-dashboard__kpi' },
          h('div', { className: 'alt-hd-dashboard__kl' }, h('span', { className: 'alt-hd-dashboard__ki' }, h(A.Icon, { name: k.icon || 'activity', size: 15 })), k.label),
          h('div', { className: 'alt-hd-dashboard__kv' }, k.value),
          h('div', { className: 'alt-hd-dashboard__kd', style: tn ? { color: tn.c } : null },
            k.trend ? h(A.Icon, { name: k.trend === 'up' ? 'trend-up' : 'trend-down', size: 14 }) : null,
            h('span', null, k.delta), h('span', { className: 'alt-hd-dashboard__kd-sub' }, k.trend ? 'vs. período anterior' : '')));
      })),
      h('div', { className: 'alt-hd-dashboard__grid' },
        h(Panel, { title: 'Status dos chamados', className: 'alt-hd-dashboard__a' }, h(Donut, { items: data.status })),
        h(Panel, { title: 'Avaliações', className: 'alt-hd-dashboard__b' },
          h('div', { className: 'alt-hd-dashboard__rating alt-num' },
            h('div', { className: 'alt-hd-dashboard__ravg' },
              h('b', null, nf(data.ratings.avg, 1)),
              h('span', { className: 'alt-hd-dashboard__stars', role: 'img', 'aria-label': nf(data.ratings.avg, 1) + ' de 5' }, [1, 2, 3, 4, 5].map(function (i) {
                return h(A.Icon, { key: i, name: 'star', size: 14, className: i <= Math.round(data.ratings.avg) ? 'is-on' : '' });
              })),
              h('span', { className: 'alt-hd-dashboard__rsub' }, nf(ratTotal) + ' avaliações')),
            h(Bars, { items: data.ratings.dist.map(function (n, i) { return { label: (5 - i) + (i === 4 ? ' estrela' : ' estrelas'), value: n, color: 'var(--chart-3)' }; }) }))),
        h(Panel, {
          title: 'SLA estourado', className: 'alt-hd-dashboard__c',
          extra: h('span', { className: 'alt-chip alt-num', style: { background: tone('error').bg, color: tone('error').c } }, h(A.Icon, { name: 'x-circle', size: 13 }), data.breachedTotal + ' chamados')
        },
          h('ul', { className: 'alt-hd-dashboard__queue alt-num' }, data.breached.map(function (t) {
            return h('li', { key: t.id }, h('button', { type: 'button', onClick: function () { if (p.onSelectTicket) p.onSelectTicket(t); } },
              h('span', { className: 'alt-hd-dashboard__qm' }, h('span', { className: 'alt-hd-dashboard__qs' }, h('span', { className: 'alt-hd-dashboard__qid' }, '#' + t.id + ' '), t.subject), h('span', { className: 'alt-hd-dashboard__qg' }, t.grupo)),
              Sla ? h(Sla, { status: 'breached', time: t.time, compact: true }) : t.time));
          })),
          h('button', { type: 'button', className: 'alt-tbtn alt-hd-dashboard__all', onClick: p.onViewAll }, (p.viewAllLabel || 'Ver todos'), h(A.Icon, { name: 'arrow-right', size: 14 }))),
        h(Panel, { title: 'Chamados por grupo', className: 'alt-hd-dashboard__d' }, h(Bars, { items: data.groups })),
        h(Panel, { title: 'Carga por responsável', className: 'alt-hd-dashboard__e', extra: h('span', { className: 'alt-hd-dashboard__hint' }, 'abertos / capacidade') },
          h('ul', { className: 'alt-hd-dashboard__bars alt-num' }, data.load.map(function (l) {
            var ratio = l.open / l.cap, over = ratio > threshold;
            return h('li', { key: l.label },
              h('span', { className: 'alt-hd-dashboard__bl' }, l.label),
              h('span', { className: 'alt-hd-dashboard__bt' }, h('i', { style: { width: Math.min(100, ratio * 100) + '%', background: over ? 'var(--signal-error)' : 'var(--teal-light)' } })),
              h('span', { className: 'alt-hd-dashboard__bv alt-hd-dashboard__bv--w' }, l.open + ' / ' + l.cap,
                over ? h('span', { className: 'alt-hd-dashboard__over' }, h(A.Icon, { name: 'alert', size: 12 }), 'Sobrecarga') : null));
          })))));
  }
  HdDashboard.sampleData = sampleData;
  A.HdDashboard = HdDashboard;
})();


/* ---- HdFiltrosLote ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  var DEF_GROUPS = ['Sistemas', 'Operações', 'Fiscal', 'Cadastros', 'Suporte', 'Financeiro'];
  var DEF_CATS = ['Integrações', 'Roteirização', 'Relatórios', 'Acesso', 'CT-e', 'Veículos', 'Rastreamento'];
  var DEF_PEOPLE = ['Carlos Lima', 'Ana Ribeiro', 'Pedro Melo', 'Marta Souza', 'João Pereira'];
  var SLA_OPTS = [['breached', 'Estourado', 'error'], ['warning', 'Atenção', 'warning'], ['ok', 'No prazo', 'success'], ['none', 'Sem SLA', 'neutral']];
  var BANDS = [['any', 'Qualquer'], ['baixa', 'Baixa'], ['media', 'Média'], ['alta', 'Alta'], ['critica', 'Crítica']];
  var SORTS = ['SLA (mais urgente)', 'Prioridade GUT', 'Mais recentes', 'Mais antigos', 'Última atividade'];
  var EMPTY = { grupos: [], categorias: [], sla: [], faixa: 'any', ordem: SORTS[0] };
  var MONTHS = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

  function useOutside(ref, on, close) {
    R.useEffect(function () {
      if (!on) return;
      function f(e) { if (ref.current && !ref.current.contains(e.target)) close(); }
      function k(e) { if (e.key === 'Escape') close(); }
      document.addEventListener('mousedown', f); document.addEventListener('keydown', k);
      return function () { document.removeEventListener('mousedown', f); document.removeEventListener('keydown', k); };
    }, [on]);
  }

  /* Autocomplete múltiplo (equivalente ao NewFilterAutocomplete). */
  function Multi(p) {
    var sQ = R.useState(''), q = sQ[0], sO = R.useState(false), open = sO[0], ref = R.useRef(null);
    useOutside(ref, open, function () { sO[1](false); });
    var opts = p.options.filter(function (o) { return p.value.indexOf(o) < 0 && o.toLowerCase().indexOf(q.trim().toLowerCase()) >= 0; });
    function add(o) { p.onChange(p.value.concat([o])); sQ[1](''); }
    function rm(o) { p.onChange(p.value.filter(function (x) { return x !== o; })); }
    return h('div', { className: 'alt-hd-filtros-lote__multi', ref: ref },
      h('div', { className: 'alt-ctl alt-ctl--sm alt-hd-filtros-lote__mctl', onClick: function (e) { var i = e.currentTarget.querySelector('input'); if (i) i.focus(); } },
        p.value.map(function (v) {
          return h('span', { key: v, className: 'alt-chip alt-hd-filtros-lote__tag' }, v,
            h('button', { type: 'button', 'aria-label': 'Remover ' + v, onClick: function (e) { e.stopPropagation(); rm(v); } }, h(A.Icon, { name: 'x', size: 11 })));
        }),
        h('input', { id: p.id, value: q, placeholder: p.value.length ? '' : p.placeholder, 'aria-autocomplete': 'list', 'aria-expanded': open,
          onFocus: function () { sO[1](true); }, onChange: function (e) { sQ[1](e.target.value); sO[1](true); },
          onKeyDown: function (e) {
            if (e.key === 'Enter' && opts[0]) { e.preventDefault(); add(opts[0]); }
            if (e.key === 'Backspace' && !q && p.value.length) rm(p.value[p.value.length - 1]);
          } })),
      open && opts.length ? h('div', { className: 'alt-popover alt-hd-filtros-lote__menu', role: 'listbox' }, opts.slice(0, 6).map(function (o) {
        return h('button', { key: o, type: 'button', role: 'option', className: 'alt-menu-item', onMouseDown: function (e) { e.preventDefault(); add(o); } }, o);
      })) : null);
  }

  function Calendar(p) {
    var base = p.month || new Date(2026, 9, 1), sM = R.useState(new Date(base.getFullYear(), base.getMonth(), 1)), m = sM[0];
    var first = m.getDay(), days = new Date(m.getFullYear(), m.getMonth() + 1, 0).getDate(), cells = [];
    for (var i = 0; i < first; i++) cells.push(null);
    for (var d = 1; d <= days; d++) cells.push(d);
    function shift(n) { sM[1](new Date(m.getFullYear(), m.getMonth() + n, 1)); }
    var today = p.today;
    return h('div', { className: 'alt-hd-filtros-lote__cal alt-num' },
      h('div', { className: 'alt-hd-filtros-lote__cal-hd' },
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Mês anterior', onClick: function () { shift(-1); } }, h(A.Icon, { name: 'chevron-left', size: 15 })),
        h('b', null, MONTHS[m.getMonth()] + ' ' + m.getFullYear()),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Próximo mês', onClick: function () { shift(1); } }, h(A.Icon, { name: 'chevron-right', size: 15 }))),
      h('div', { className: 'alt-hd-filtros-lote__cal-g' },
        ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'].map(function (w, i) { return h('span', { key: 'w' + i, className: 'alt-hd-filtros-lote__cal-w' }, w); }),
        cells.map(function (d, i) {
          if (!d) return h('span', { key: i });
          var dt = new Date(m.getFullYear(), m.getMonth(), d), past = today && dt < today;
          var isToday = today && dt.getTime() === today.getTime();
          return h('button', { key: i, type: 'button', disabled: past, className: cx(isToday && 'is-today'), 'aria-label': d + ' de ' + MONTHS[m.getMonth()],
            onClick: function () { p.onPick(dt); } }, d);
        })));
  }

  function fmt(d) { return ('0' + d.getDate()).slice(-2) + '/' + ('0' + (d.getMonth() + 1)).slice(-2) + '/' + d.getFullYear(); }

  function HdFiltrosLote(p) {
    var ctl = p.value !== undefined, sV = R.useState(Object.assign({}, EMPTY, { sla: ['breached', 'warning'], faixa: 'alta' }, p.defaultValue || {}));
    var v = ctl ? p.value : sV[0];
    var sOpen = R.useState(p.defaultOpen != null ? p.defaultOpen : false), open = sOpen[0];
    var sMenu = R.useState(p.defaultMenu || null), menu = sMenu[0];
    var sSel = R.useState(p.defaultSelectedCount != null ? p.defaultSelectedCount : 3), selCount = p.selectedCount !== undefined ? p.selectedCount : sSel[0];
    var sMsg = R.useState(''), msg = sMsg[0];
    var refF = R.useRef(null), refB = R.useRef(null);
    useOutside(refF, open, function () { sOpen[1](false); });
    useOutside(refB, !!menu, function () { sMenu[1](null); });

    function set(k, val) { var nv = Object.assign({}, v); nv[k] = val; if (!ctl) sV[1](nv); if (p.onChange) p.onChange(nv); }
    function clear() { var nv = Object.assign({}, EMPTY); if (!ctl) sV[1](nv); if (p.onChange) p.onChange(nv); }
    function bulk(action, payload, text) { sMenu[1](null); sMsg[1](text); if (p.onBulk) p.onBulk(action, payload); }
    var active = v.grupos.length + v.categorias.length + v.sla.length + (v.faixa !== 'any' ? 1 : 0);
    var bandLabel = (BANDS.filter(function (b) { return b[0] === v.faixa; })[0] || [])[1];

    var summary = [].concat(
      v.sla.map(function (s) { var o = SLA_OPTS.filter(function (x) { return x[0] === s; })[0]; return { k: 'sla:' + s, label: 'SLA ' + o[1].toLowerCase(), rm: function () { set('sla', v.sla.filter(function (x) { return x !== s; })); } }; }),
      v.faixa !== 'any' ? [{ k: 'faixa', label: 'Prioridade ' + bandLabel.toLowerCase(), rm: function () { set('faixa', 'any'); } }] : [],
      v.grupos.map(function (g) { return { k: 'g:' + g, label: g, rm: function () { set('grupos', v.grupos.filter(function (x) { return x !== g; })); } }; }),
      v.categorias.map(function (g) { return { k: 'c:' + g, label: g, rm: function () { set('categorias', v.categorias.filter(function (x) { return x !== g; })); } }; }));

    return h('div', { className: cx('alt-hd-filtros-lote', p.className), style: p.style },
      p.showFilters === false ? null : h('div', { className: 'alt-hd-filtros-lote__filters', ref: refF },
        h('button', { type: 'button', className: 'alt-tbtn', 'aria-expanded': open, 'aria-pressed': open, onClick: function () { sOpen[1](!open); } },
          h(A.Icon, { name: 'filter', size: 15 }), 'Filtros', active ? h('span', { className: 'alt-count' }, active) : null),
        h('div', { className: 'alt-hd-filtros-lote__summary' }, summary.map(function (s) {
          return h('span', { key: s.k, className: 'alt-chip alt-hd-filtros-lote__tag' }, s.label,
            h('button', { type: 'button', 'aria-label': 'Remover filtro ' + s.label, onClick: s.rm }, h(A.Icon, { name: 'x', size: 11 })));
        })),
        open ? h('div', { className: 'alt-popover alt-hd-filtros-lote__pop', role: 'dialog', 'aria-label': 'Filtros avançados' },
          h('div', { className: 'alt-hd-filtros-lote__ph' }, h('b', null, 'Filtros avançados'),
            h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: clear, disabled: !active }, 'Limpar')),
          h('div', { className: 'alt-hd-filtros-lote__ps' }, h('label', { htmlFor: 'hdfl-g' }, 'Grupo'), h(Multi, { id: 'hdfl-g', options: p.groups || DEF_GROUPS, value: v.grupos, placeholder: 'Todos os grupos', onChange: function (x) { set('grupos', x); } })),
          h('div', { className: 'alt-hd-filtros-lote__ps' }, h('label', { htmlFor: 'hdfl-c' }, 'Categoria'), h(Multi, { id: 'hdfl-c', options: p.categories || DEF_CATS, value: v.categorias, placeholder: 'Todas as categorias', onChange: function (x) { set('categorias', x); } })),
          h('div', { className: 'alt-hd-filtros-lote__ps' }, h('span', { id: 'hdfl-s' }, 'SLA'),
            h('div', { className: 'alt-hd-filtros-lote__chips', role: 'group', 'aria-labelledby': 'hdfl-s' }, SLA_OPTS.map(function (o) {
              var on = v.sla.indexOf(o[0]) >= 0, t = tone(o[2]);
              return h('button', { key: o[0], type: 'button', 'aria-pressed': on, className: 'alt-chip alt-hd-filtros-lote__fchip',
                style: on ? { background: t.bg, color: o[2] === 'neutral' ? 'var(--foreground)' : t.c, borderColor: 'transparent' } : null,
                onClick: function () { set('sla', on ? v.sla.filter(function (x) { return x !== o[0]; }) : v.sla.concat([o[0]])); } },
                on ? h(A.Icon, { name: 'check', size: 13 }) : null, o[1]);
            }))),
          h('div', { className: 'alt-hd-filtros-lote__ps' }, h('span', { id: 'hdfl-f' }, 'Faixa de prioridade (GUT)'),
            h('div', { className: 'alt-hd-filtros-lote__seg', role: 'group', 'aria-labelledby': 'hdfl-f' }, BANDS.map(function (b) {
              return h('button', { key: b[0], type: 'button', 'aria-pressed': v.faixa === b[0], onClick: function () { set('faixa', b[0]); } }, b[1]);
            }))),
          h('div', { className: 'alt-hd-filtros-lote__ps' }, h('label', { htmlFor: 'hdfl-o' }, 'Ordenação'),
            h('div', { className: 'alt-ctl alt-ctl--sm alt-hd-filtros-lote__sel' },
              h('select', { id: 'hdfl-o', value: v.ordem, onChange: function (e) { set('ordem', e.target.value); } }, (p.sorts || SORTS).map(function (s) { return h('option', { key: s }, s); })),
              h(A.Icon, { name: 'chevron-down', size: 14 })))) : null),
      p.showBulk === false || !selCount ? null : h('div', { className: 'alt-hd-filtros-lote__bulk alt-num', ref: refB, role: 'toolbar', 'aria-label': 'Ações em lote' },
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Limpar seleção', onClick: function () { sSel[1](0); if (p.onClearSelection) p.onClearSelection(); } }, h(A.Icon, { name: 'x', size: 15 })),
        h('b', null, selCount + (selCount === 1 ? ' selecionado' : ' selecionados')),
        h('span', { className: 'alt-hd-filtros-lote__msg', 'aria-live': 'polite' }, msg),
        h('div', { className: 'alt-hd-filtros-lote__anchor' },
          h('button', { type: 'button', className: 'alt-tbtn', 'aria-expanded': menu === 'assign', onClick: function () { sMenu[1](menu === 'assign' ? null : 'assign'); } }, h(A.Icon, { name: 'user', size: 15 }), 'Atribuir', h(A.Icon, { name: 'chevron-down', size: 13 })),
          menu === 'assign' ? h('div', { className: 'alt-popover alt-hd-filtros-lote__drop', role: 'menu' }, (p.people || DEF_PEOPLE).map(function (n) {
            return h('button', { key: n, type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: function () { bulk('assign', n, selCount + ' atribuídos a ' + n); } }, h(A.Icon, { name: 'user', size: 15 }), n);
          })) : null),
        h('div', { className: 'alt-hd-filtros-lote__anchor' },
          h('button', { type: 'button', className: 'alt-tbtn', 'aria-expanded': menu === 'forecast', onClick: function () { sMenu[1](menu === 'forecast' ? null : 'forecast'); } }, h(A.Icon, { name: 'calendar', size: 15 }), 'Previsão'),
          menu === 'forecast' ? h('div', { className: 'alt-popover alt-hd-filtros-lote__drop alt-hd-filtros-lote__drop--cal', role: 'dialog', 'aria-label': 'Nova previsão' },
            h(Calendar, { month: p.calendarMonth, today: p.today || new Date(2026, 9, 7), onPick: function (d) { bulk('forecast', d, 'Previsão: ' + fmt(d)); } })) : null),
        h('button', { type: 'button', className: 'alt-btn alt-btn--primary alt-hd-filtros-lote__primary', onClick: function () { bulk('close', null, selCount + ' fechados'); } }, h(A.Icon, { name: 'check', size: 15 }), 'Fechar')));
  }
  HdFiltrosLote.EMPTY = EMPTY;
  A.HdFiltrosLote = HdFiltrosLote;
})();


/* ---- HdHistorico ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  /* Marcos do chamado. Cor só para travado (warning) e fechado (success). */
  var TYPES = {
    opened: { icon: 'plus', label: 'Aberto' },
    transferred: { icon: 'route', label: 'Transferido' },
    reclassified: { icon: 'sliders', label: 'Reclassificado' },
    locked: { icon: 'lock', label: 'Travado', tone: 'warning' },
    unlocked: { icon: 'unlock', label: 'Destravado' },
    assigned: { icon: 'user', label: 'Atribuído' },
    forecast: { icon: 'calendar', label: 'Previsão alterada' },
    closed: { icon: 'check-circle', label: 'Fechado', tone: 'success' },
    message: { icon: 'message', label: 'Mensagem' },
    rating: { icon: 'star', label: 'Avaliação' }
  };

  function Unlock(size) {
    return h('svg', { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      h('path', { d: 'M4 11h16v10H4z' }), h('path', { d: 'M8 11V7a4 4 0 0 1 7.8-1.2' }));
  }
  function Ico(name, size) { return name === 'unlock' ? Unlock(size) : h(A.Icon, { name: name, size: size }); }

  /* Estado real: concluído até o último marco com hora; o último concluído é o ativo
     (a menos que o chamado esteja fechado); sem hora = pendente. */
  function withStatus(items) {
    var lastDone = -1, closed = false;
    items.forEach(function (it, i) { if (it.time) { lastDone = i; if (it.type === 'closed') closed = true; } });
    var lastMilestone = -1;
    items.forEach(function (it, i) { if (it.time && i <= lastDone && it.type !== 'message' && it.type !== 'rating') lastMilestone = i; });
    return items.map(function (it, i) {
      var st = it.status || (!it.time ? 'pending' : (!closed && i === lastMilestone ? 'active' : 'complete'));
      return Object.assign({}, it, { status: st });
    });
  }

  var DEF = [
    { id: 1, type: 'opened', time: '30/09 08:12', author: 'Ana Ribeiro' },
    { id: 2, type: 'message', time: '08:14', author: 'Ana Ribeiro', text: 'O painel não recebe viagens desde as 7h.' },
    { id: 3, type: 'transferred', title: 'Transferido para Sistemas', time: '08:30' },
    { id: 4, type: 'assigned', title: 'Atribuído a Carlos Lima', time: '08:38' },
    { id: 5, type: 'message', time: '08:40', author: 'Carlos Lima', text: 'Verificando a fila de integração.' },
    { id: 6, type: 'reclassified', title: 'Reclassificado: GUT 27 → 64', time: '08:52', author: 'Carlos Lima' },
    { id: 7, type: 'locked', time: '09:05', author: 'Carlos Lima', note: 'Aguardando fornecedor do TMS.' },
    { id: 8, type: 'forecast', title: 'Previsão alterada para 01/10 12:00', time: '09:06', author: 'Carlos Lima' },
    { id: 9, type: 'closed', title: 'Resolução' },
    { id: 10, type: 'rating', title: 'Avaliação do solicitante' }
  ];

  function Stars(n) {
    return h('span', { className: 'alt-hd-historico__stars', role: 'img', 'aria-label': n + ' de 5 estrelas' },
      [1, 2, 3, 4, 5].map(function (i) { return h(A.Icon, { key: i, name: 'star', size: 13, className: i <= n ? 'is-on' : '' }); }));
  }

  function HdHistorico(p) {
    var items = withStatus(p.items || DEF);
    var ctl = p.filter !== undefined, st = R.useState(p.defaultFilter || 'all');
    var filter = ctl ? p.filter : st[0];
    function setFilter(f) { if (!ctl) st[1](f); if (p.onFilterChange) p.onFilterChange(f); }
    var shown = filter === 'milestones' ? items.filter(function (i) { return i.type !== 'message'; }) : items;
    var nMsg = items.filter(function (i) { return i.type === 'message'; }).length;

    return h('section', { className: cx('alt-glass', 'alt-hd-historico', p.className), style: p.style, 'aria-label': p.title || 'Histórico' },
      h('header', { className: 'alt-hd-historico__hd' },
        h('h3', { className: 'alt-hd-historico__title' }, p.title || 'Histórico'),
        h('div', { className: 'alt-hd-historico__seg', role: 'group', 'aria-label': 'Filtrar histórico' },
          [['all', 'Tudo'], ['milestones', 'Só marcos']].map(function (o) {
            return h('button', { key: o[0], type: 'button', 'aria-pressed': filter === o[0], onClick: function () { setFilter(o[0]); } }, o[1]);
          }))),
      h('ol', { className: 'alt-hd-historico__tl alt-num' },
        shown.map(function (it, i) {
          var T = TYPES[it.type] || TYPES.opened, tn = T.tone ? tone(T.tone) : null;
          var pend = it.status === 'pending', act = it.status === 'active', msg = it.type === 'message';
          var title = it.title || T.label;
          return h('li', { key: it.id || i, className: cx('alt-hd-historico__m', 'is-' + it.status, msg && 'is-msg', i === shown.length - 1 && 'is-last') },
            h('span', { className: 'alt-hd-historico__i', style: tn && !pend ? { background: tn.bg, color: tn.c } : null, 'aria-hidden': 'true' }, pend ? null : Ico(T.icon, 13)),
            h('div', { className: 'alt-hd-historico__body' },
              msg
                ? h(R.Fragment, null,
                    h('div', { className: 'alt-hd-historico__line' }, h('span', { className: 'alt-hd-historico__who' }, it.author), h('span', { className: 'alt-hd-historico__meta' }, it.time)),
                    h('p', { className: 'alt-hd-historico__msg' }, it.text))
                : h(R.Fragment, null,
                    h('div', { className: 'alt-hd-historico__line' },
                      h('span', { className: 'alt-hd-historico__name' }, title),
                      it.time ? h('span', { className: 'alt-hd-historico__meta' }, it.time + (it.author ? ' · ' + it.author : '')) : null,
                      pend ? h('span', { className: 'alt-hd-historico__tag' }, 'Pendente') : null,
                      act ? h('span', { className: 'alt-hd-historico__tag alt-hd-historico__tag--active' }, 'Atual') : null),
                    it.note ? h('p', { className: 'alt-hd-historico__note' }, it.note) : null,
                    it.type === 'rating' && it.rating ? h('div', { className: 'alt-hd-historico__note' }, Stars(it.rating), it.comment ? ' ' + it.comment : null) : null)));
        })),
      filter === 'milestones' && nMsg ? h('p', { className: 'alt-hd-historico__foot' }, nMsg + (nMsg === 1 ? ' mensagem oculta' : ' mensagens ocultas')) : null);
  }
  HdHistorico.computeStatus = withStatus;
  A.HdHistorico = HdHistorico;
})();


/* ---- HdSlaGut ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  /* Uma tabela de rótulos para SLA (substitui os 3 textos divergentes). */
  var SLA = {
    ok: { label: 'No prazo', tone: 'success', icon: 'clock' },
    warning: { label: 'Atenção', tone: 'warning', icon: 'alert' },
    breached: { label: 'Estourado', tone: 'error', icon: 'x-circle' },
    none: { label: 'Sem SLA', tone: 'neutral', icon: 'clock' }
  };
  /* Escala única de GUT, a do tema. */
  function gutBand(score) {
    if (score >= 64) return { label: 'Crítica', tone: 'error', key: 'critica' };
    if (score >= 27) return { label: 'Alta', tone: 'warning', key: 'alta' };
    if (score >= 8) return { label: 'Média', tone: 'success', key: 'media' };
    return { label: 'Baixa', tone: 'neutral', key: 'baixa' };
  }
  function gutScore(v) { return v ? (v.g || 1) * (v.u || 1) * (v.t || 1) : 0; }

  function SlaChip(p) {
    var s = SLA[p.status] || SLA.none, t = tone(s.tone), compact = p.compact;
    var text = compact ? (p.time || s.label) : (p.status === 'none' || !p.time ? s.label : s.label + ' · ' + p.time);
    return h('span', {
      className: cx('alt-chip', 'alt-num', 'alt-hd-sla-gut__chip', p.status === 'none' && 'alt-hd-sla-gut__chip--outlined', p.className),
      style: p.status === 'none' ? null : { background: t.bg, color: t.c },
      title: compact ? 'SLA ' + s.label.toLowerCase() + (p.time ? ' · ' + p.time : '') : undefined,
      'aria-label': compact ? 'SLA ' + s.label + (p.time ? ', ' + p.time : '') : undefined
    }, h(A.Icon, { name: compact ? s.icon : 'clock', size: 14 }), h('span', null, text));
  }

  function GutChip(p) {
    var score = p.score != null ? p.score : gutScore(p.value), b = gutBand(score), t = tone(b.tone);
    if (p.variant === 'outlined') {
      return h('span', { className: cx('alt-chip', 'alt-num', 'alt-hd-sla-gut__chip', 'alt-hd-sla-gut__chip--outlined', p.className), title: 'Prioridade ' + b.label },
        h('span', { className: 'alt-dot', style: { color: t.c }, 'aria-hidden': 'true' }), 'GUT ' + score);
    }
    return h('span', { className: cx('alt-chip', 'alt-num', 'alt-hd-sla-gut__chip', p.className), style: b.tone === 'neutral' ? { background: t.bg, color: 'var(--foreground)' } : { background: t.bg, color: t.c } },
      h(A.Icon, { name: 'flag', size: 14 }), (p.short ? score : 'GUT ' + score) + ' · ' + (p.short ? b.label.toLowerCase() : b.label));
  }

  var FACTORS = [{ key: 'g', label: 'Gravidade' }, { key: 'u', label: 'Urgência' }, { key: 't', label: 'Tendência' }];

  function GutSelector(p) {
    var ctl = p.value !== undefined, st = R.useState(p.defaultValue || { g: 4, u: 4, t: 3 });
    var v = ctl ? p.value : st[0];
    function set(k, n) {
      if (p.readOnly) return;
      var nv = Object.assign({}, v); nv[k] = n;
      if (!ctl) st[1](nv);
      if (p.onChange) p.onChange(nv, gutScore(nv));
    }
    function onKey(e, k) {
      var d = e.key === 'ArrowRight' || e.key === 'ArrowUp' ? 1 : e.key === 'ArrowLeft' || e.key === 'ArrowDown' ? -1 : 0;
      if (!d) return; e.preventDefault();
      var n = Math.min(5, Math.max(1, (v[k] || 1) + d)); set(k, n);
      var g = e.currentTarget.parentNode.children[n - 1]; if (g) g.focus();
    }
    return h('div', { className: cx('alt-hd-sla-gut__grid', 'alt-num', p.className) },
      FACTORS.map(function (f) {
        var labelId = 'gut-' + f.key + '-' + (p.idSuffix || 'x');
        return R.createElement(R.Fragment, { key: f.key },
          h('span', { className: 'alt-hd-sla-gut__label', id: labelId }, (p.labels && p.labels[f.key]) || f.label),
          h('div', { className: 'alt-hd-sla-gut__seg5', role: 'radiogroup', 'aria-labelledby': labelId },
            [1, 2, 3, 4, 5].map(function (n) {
              var on = v[f.key] === n;
              return h('button', { key: n, type: 'button', role: 'radio', 'aria-checked': on, tabIndex: on ? 0 : -1, disabled: p.readOnly,
                className: 'alt-hd-sla-gut__opt', onClick: function () { set(f.key, n); }, onKeyDown: function (e) { onKey(e, f.key); } }, n);
            })));
      }),
      h('span', { className: 'alt-hd-sla-gut__label' }, 'Prioridade'),
      h('span', { 'aria-live': 'polite' }, h(GutChip, { value: v })));
  }

  var DEF_SLAS = [{ status: 'ok', time: '5h' }, { status: 'warning', time: '1h' }, { status: 'breached', time: '−2h' }, { status: 'none' }];

  function HdSlaGut(p) {
    var slas = p.slas || DEF_SLAS;
    return h('div', { className: cx('alt-glass', 'alt-hd-sla-gut', p.className), style: p.style },
      p.showSla === false ? null : h('section', { className: 'alt-hd-sla-gut__sec' },
        h('div', { className: 'alt-hd-sla-gut__title' }, p.slaTitle || 'SLA'),
        h('div', { className: 'alt-hd-sla-gut__row' }, slas.map(function (s, i) { return h(SlaChip, Object.assign({ key: i }, s)); })),
        p.showCompact === false ? null : h('div', { className: 'alt-hd-sla-gut__row' },
          h('span', { className: 'alt-hd-sla-gut__hint' }, 'Compacto (lista)'),
          slas.map(function (s, i) { return h(SlaChip, Object.assign({ key: i, compact: true }, s)); })),
        p.showCompact === false ? null : h('div', { className: 'alt-hd-sla-gut__row' },
          h('span', { className: 'alt-hd-sla-gut__hint' }, 'GUT na lista'),
          [4, 8, 27, 64].map(function (n) { return h(GutChip, { key: n, score: n, variant: 'outlined' }); }))),
      p.showSelector === false ? null : h('section', { className: 'alt-hd-sla-gut__sec' },
        h('div', { className: 'alt-hd-sla-gut__title' }, p.gutTitle || 'Prioridade GUT'),
        h('p', { className: 'alt-hd-sla-gut__hint' }, 'Gravidade × urgência × tendência. Baixa < 8 · Média 8–26 · Alta 27–63 · Crítica 64+'),
        h(GutSelector, { value: p.value, defaultValue: p.defaultValue, onChange: p.onChange, readOnly: p.readOnly, idSuffix: p.id || 'main' })));
  }
  HdSlaGut.Sla = SlaChip;
  HdSlaGut.Gut = GutChip;
  HdSlaGut.Selector = GutSelector;
  HdSlaGut.gutBand = gutBand;
  HdSlaGut.gutScore = gutScore;
  HdSlaGut.SLA_LABELS = SLA;
  A.HdSlaGut = HdSlaGut;
})();


/* ---- InfoKPI ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  var Icon = A.Icon;
  var MAP = { primary: 'accent', secondary: 'neutral', default: 'neutral' };

  function toneOf(color) { return K.tone(MAP[color] || color || 'accent'); }

  function useCountUp(target, on) {
    var s = R.useState(on ? 0 : target);
    R.useEffect(function () {
      if (!on || typeof target !== 'number') { s[1](target); return; }
      var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      if (reduce) { s[1](target); return; }
      var t0 = null, raf;
      function step(ts) {
        if (t0 === null) t0 = ts;
        var k = Math.min(1, (ts - t0) / 700);
        s[1](target * (1 - Math.pow(1 - k, 3)));
        if (k < 1) raf = requestAnimationFrame(step);
      }
      raf = requestAnimationFrame(step);
      return function () { cancelAnimationFrame(raf); };
    }, [target, on]);
    return s[0];
  }

  function InfoKPI(p) {
    var size = p.size || 'medium';
    var t = toneOf(p.color || 'primary');
    var value = p.value !== undefined ? p.value : 1284.5;
    var decimals = p.decimals !== undefined ? p.decimals : 2;
    var shown = useCountUp(typeof value === 'number' ? value : 0, !!p.animated && !p.locked);
    var display = p.locked ? '—' : (typeof value === 'number' ? nf(p.animated ? shown : value, decimals) : value);
    var icon = p.icon !== undefined ? p.icon : 'truck';
    var desc = p.description !== undefined ? p.description : 'Km rodados hoje';
    return h('div', {
      className: cx('alt-info-kpi alt-glass', 'alt-info-kpi--' + size, p.locked && 'is-locked', p.className), style: p.style,
      role: 'group', 'aria-label': desc + ': ' + (p.locked ? 'sem permissão' : display + (p.unit || ''))
    },
      p.locked ? h('span', { className: 'alt-info-kpi__lock', title: 'Sem permissão' }, h(Icon, { name: 'lock', size: 14, title: 'Sem permissão' })) : null,
      h('span', { className: 'alt-info-kpi__icon', style: { color: p.locked ? 'var(--muted)' : t.c, background: p.locked ? 'var(--hover-wash)' : t.bg } },
        typeof icon === 'string' ? h(Icon, { name: icon, size: size === 'small' ? 16 : 20 }) : icon),
      h('div', null,
        h('div', { className: 'alt-info-kpi__value alt-num' }, display, !p.locked && p.unit ? h('small', null, p.unit) : null),
        h('div', { className: 'alt-info-kpi__desc' }, desc)));
  }

  /* InfoCell: célula compacta de 55px com ponto na cor + título + valor. */
  function InfoCell(p) {
    var t = toneOf(p.color);
    return h('div', { className: cx('alt-info-kpi__cell alt-glass', p.className), style: p.style },
      h('span', { className: 'alt-info-kpi__cell-title' }, h('span', { className: 'alt-dot', style: { color: t.c }, 'aria-hidden': 'true' }), p.title),
      h('b', { className: 'alt-num' }, typeof p.value === 'number' ? nf(p.value, p.decimals || 0) : p.value));
  }

  InfoKPI.Cell = InfoCell;
  A.InfoKPI = InfoKPI;
})();


/* ---- JsonFormsRenderers ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  /* ---------- schema helpers ---------- */
  function scopePath(scope) { return String(scope || '').replace(/^#\//, '').split('/').filter(function (s, i) { return i % 2 === 1; }); }
  function resolveProp(schema, path) {
    var s = schema;
    for (var i = 0; i < path.length; i++) { s = s && s.properties && s.properties[path[i]]; }
    return s || {};
  }
  function isRequired(schema, path) {
    var parent = resolveProp(schema, path.slice(0, -1));
    if (!path.length) return false;
    var req = path.length === 1 ? schema.required : parent.required;
    return !!(req && req.indexOf(path[path.length - 1]) >= 0);
  }
  function getIn(o, path) { for (var i = 0; i < path.length; i++) { if (o == null) return undefined; o = o[path[i]]; } return o; }
  function setIn(o, path, v) {
    var copy = Object.assign({}, o || {});
    if (path.length === 1) { if (v === undefined) delete copy[path[0]]; else copy[path[0]] = v; return copy; }
    copy[path[0]] = setIn(copy[path[0]], path.slice(1), v);
    return copy;
  }
  function labelFor(el, prop, path) { return el.label != null ? el.label : (prop.title || path[path.length - 1]); }
  function countControls(el) {
    if (!el) return 0;
    if (el.type === 'Control') return 1;
    return (el.elements || []).reduce(function (n, e) { return n + countControls(e); }, 0);
  }
  function collectControls(el, out) {
    out = out || [];
    if (!el) return out;
    if (el.type === 'Control') out.push(el);
    (el.elements || []).forEach(function (e) { collectControls(e, out); });
    return out;
  }

  /* Validation: required, number bounds, string length, date format. Returns { "a.b": "mensagem" }. */
  function validate(schema, uischema, data) {
    var errs = {};
    collectControls(uischema).forEach(function (el) {
      var path = scopePath(el.scope), prop = resolveProp(schema, path), v = getIn(data, path), key = path.join('.');
      var empty = v === undefined || v === null || v === '';
      if (empty) { if (isRequired(schema, path)) errs[key] = prop.requiredMessage || 'Campo obrigatório'; return; }
      if (prop.type === 'number' || prop.type === 'integer') {
        if (typeof v !== 'number' || isNaN(v)) errs[key] = 'Informe um número';
        else if (prop.exclusiveMinimum != null && v <= prop.exclusiveMinimum) errs[key] = prop.errorMessage || 'Informe um valor maior que ' + nf(prop.exclusiveMinimum);
        else if (prop.minimum != null && v < prop.minimum) errs[key] = prop.errorMessage || 'Valor mínimo: ' + nf(prop.minimum);
        else if (prop.maximum != null && v > prop.maximum) errs[key] = prop.errorMessage || 'Valor máximo: ' + nf(prop.maximum);
      } else if (prop.format === 'date') {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(v)) errs[key] = 'Data inválida (dd/mm/aaaa)';
      } else if (typeof v === 'string') {
        if (prop.minLength && v.length < prop.minLength) errs[key] = 'Mínimo de ' + prop.minLength + ' caracteres';
        if (prop.maxLength && v.length > prop.maxLength) errs[key] = 'Máximo de ' + prop.maxLength + ' caracteres';
      }
    });
    return errs;
  }

  /* ---------- controls ---------- */
  function NumberCtl(p) {
    var dec = p.decimals == null ? (p.integer ? 0 : 2) : p.decimals;
    function fmt(v) { return v == null || v === '' || isNaN(v) ? '' : (p.integer ? nf(v) : Number(v).toLocaleString('pt-BR', { maximumFractionDigits: dec })); }
    var st = R.useState(fmt(p.value)); var text = st[0], setText = st[1];
    var focused = R.useRef(false);
    R.useEffect(function () { if (!focused.current) setText(fmt(p.value)); }, [p.value]);
    return h('input', {
      id: p.id, value: text, inputMode: p.integer ? 'numeric' : 'decimal', disabled: p.disabled, readOnly: p.readOnly, placeholder: p.placeholder || '0',
      'aria-invalid': !!p.error || undefined, 'aria-describedby': p.describedBy,
      onFocus: function () { focused.current = true; },
      onChange: function (e) {
        var t = e.target.value.replace(/[^\d,.\-]/g, '');
        setText(t);
        var n = t.replace(/\./g, '').replace(',', '.');
        p.onChange(n === '' || n === '-' ? undefined : (p.integer ? parseInt(n, 10) : parseFloat(n)));
      },
      onBlur: function () { focused.current = false; setText(fmt(p.value)); p.onBlur(); }
    });
  }

  /* Money mask: digits fill from the right (12480,00). */
  function CurrencyCtl(p) {
    var text = p.value == null || isNaN(p.value) ? '' : nf(p.value, 2);
    return h('input', {
      id: p.id, value: text, inputMode: 'numeric', disabled: p.disabled, readOnly: p.readOnly, placeholder: '0,00',
      'aria-invalid': !!p.error || undefined, 'aria-describedby': p.describedBy,
      onChange: function (e) {
        var d = e.target.value.replace(/\D/g, '').replace(/^0+/, '');
        p.onChange(d ? parseInt(d, 10) / 100 : undefined);
      },
      onBlur: p.onBlur
    });
  }

  function isoToBr(v) { var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(v || ''); return m ? m[3] + '/' + m[2] + '/' + m[1] : (v || ''); }
  function DateCtl(p) {
    return h('input', {
      id: p.id, value: isoToBr(p.value), inputMode: 'numeric', disabled: p.disabled, readOnly: p.readOnly, placeholder: 'dd/mm/aaaa', maxLength: 10,
      'aria-invalid': !!p.error || undefined, 'aria-describedby': p.describedBy,
      onChange: function (e) {
        var d = e.target.value.replace(/\D/g, '').slice(0, 8);
        var t = d.length > 4 ? d.slice(0, 2) + '/' + d.slice(2, 4) + '/' + d.slice(4) : d.length > 2 ? d.slice(0, 2) + '/' + d.slice(2) : d;
        if (d.length === 8) {
          var dd = +d.slice(0, 2), mm = +d.slice(2, 4), yy = +d.slice(4);
          var dt = new Date(yy, mm - 1, dd);
          if (dt.getFullYear() === yy && dt.getMonth() === mm - 1 && dt.getDate() === dd) { p.onChange(d.slice(4) + '-' + d.slice(2, 4) + '-' + d.slice(0, 2)); return; }
        }
        p.onChange(t || undefined);
      },
      onBlur: p.onBlur
    });
  }

  function choices(prop) {
    if (prop.oneOf) return prop.oneOf.map(function (o) { return { value: o.const, label: o.title != null ? o.title : String(o.const) }; });
    return (prop.enum || []).map(function (v) { return { value: v, label: String(v) }; });
  }

  function Control(p) {
    var el = p.el, schema = p.schema, path = scopePath(el.scope), prop = resolveProp(schema, path), key = path.join('.');
    var o = el.options || {};
    var value = getIn(p.data, path);
    var disabled = !!(p.readonly || prop.readOnly || o.readonly || el.enabled === false);
    var err = p.showError(key) ? p.errors[key] : null;
    var id = p.idBase + '-' + key.replace(/\W/g, '-'), helpId = id + '-help';
    var label = labelFor(el, prop, path), req = isRequired(schema, path);
    function set(v) { p.onValue(path, v); }
    function blur() { p.onTouch(key); }
    var common = { id: id, value: value, onChange: set, onBlur: blur, disabled: disabled, error: err, describedBy: err || prop.description ? helpId : undefined };
    var kind, inner, wrapCls = '';
    var isNum = prop.type === 'number' || prop.type === 'integer';

    if (prop.type === 'boolean') {
      kind = 'boolean';
      inner = h('button', {
        id: id, type: 'button', role: 'switch', 'aria-checked': !!value, disabled: disabled, className: 'alt-json-forms-renderers__switch',
        onClick: function () { set(!value); blur(); }
      }, h('span', { className: 'alt-json-forms-renderers__track', 'aria-hidden': 'true' }, h('span', { className: 'alt-json-forms-renderers__thumb' })), h('span', null, value ? 'Sim' : 'Não'));
    } else if (prop.enum || prop.oneOf) {
      kind = 'select';
      inner = h('div', { className: cx('alt-ctl', 'alt-json-forms-renderers__ctl', err && 'alt-ctl--error', disabled && 'alt-json-forms-renderers__ctl--disabled') },
        h('select', {
          id: id, value: value == null ? '' : value, disabled: disabled, 'aria-invalid': !!err || undefined, 'aria-describedby': common.describedBy,
          onChange: function (e) { var c = choices(prop).filter(function (x) { return String(x.value) === e.target.value; })[0]; set(c ? c.value : undefined); }, onBlur: blur
        }, h('option', { value: '' }, o.placeholder || 'Selecionar…'), choices(prop).map(function (c) { return h('option', { key: String(c.value), value: c.value }, c.label); })),
        h(A.Icon, { name: 'chevron-down', size: 16 }));
    } else if (isNum && (o.format === 'currency' || prop.format === 'currency')) {
      kind = 'currency';
      inner = h('div', { className: cx('alt-ctl', 'alt-json-forms-renderers__ctl', 'alt-num', err && 'alt-ctl--error', disabled && 'alt-json-forms-renderers__ctl--disabled') },
        h('span', { className: 'alt-json-forms-renderers__prefix' }, o.currency || 'R$'), h(CurrencyCtl, common));
    } else if (isNum) {
      kind = 'number';
      inner = h('div', { className: cx('alt-ctl', 'alt-json-forms-renderers__ctl', 'alt-num', err && 'alt-ctl--error', disabled && 'alt-json-forms-renderers__ctl--disabled') },
        h(NumberCtl, Object.assign({ integer: prop.type === 'integer', decimals: o.decimals }, common)),
        o.unit ? h('span', { className: 'alt-json-forms-renderers__unit' }, o.unit) : null);
    } else if (prop.format === 'date') {
      kind = 'date';
      inner = h('div', { className: cx('alt-ctl', 'alt-json-forms-renderers__ctl', 'alt-num', err && 'alt-ctl--error', disabled && 'alt-json-forms-renderers__ctl--disabled') },
        h(DateCtl, common), h(A.Icon, { name: 'calendar', size: 16 }));
    } else if (o.multi || prop.format === 'textarea') {
      kind = 'textarea'; wrapCls = 'alt-json-forms-renderers__field--full';
      inner = h('div', { className: cx('alt-ctl', 'alt-json-forms-renderers__ctl', 'alt-json-forms-renderers__ctl--area', err && 'alt-ctl--error', disabled && 'alt-json-forms-renderers__ctl--disabled') },
        h('textarea', { id: id, rows: o.rows || 3, value: value || '', disabled: disabled, placeholder: o.placeholder, 'aria-invalid': !!err || undefined, 'aria-describedby': common.describedBy, onChange: function (e) { set(e.target.value || undefined); }, onBlur: blur }));
    } else {
      kind = 'text';
      inner = h('div', { className: cx('alt-ctl', 'alt-json-forms-renderers__ctl', o.mono && 'alt-num', err && 'alt-ctl--error', disabled && 'alt-json-forms-renderers__ctl--disabled') },
        h('input', { id: id, value: value == null ? '' : value, disabled: disabled, placeholder: o.placeholder, maxLength: prop.maxLength, 'aria-invalid': !!err || undefined, 'aria-describedby': common.describedBy, onChange: function (e) { set(e.target.value === '' ? undefined : e.target.value); }, onBlur: blur }));
    }

    return h('div', { className: cx('alt-field', 'alt-json-forms-renderers__field', 'alt-json-forms-renderers__field--' + kind, o.span === 'full' && 'alt-json-forms-renderers__field--full', wrapCls) },
      h('label', { className: 'alt-field__label', htmlFor: id }, label, req ? h('span', { className: 'alt-json-forms-renderers__req', 'aria-hidden': 'true' }, ' *') : null),
      inner,
      err ? h('div', { id: helpId, className: 'alt-field__error', role: 'alert' }, h(A.Icon, { name: 'info', size: 14 }), err)
        : prop.description ? h('div', { id: helpId, className: 'alt-field__help' }, prop.description) : null);
  }

  function Section(p) {
    var el = p.el, o = el.options || {};
    var st = R.useState(!!o.collapsed); var collapsed = st[0], setCollapsed = st[1];
    var n = countControls(el), bodyId = p.idBase + '-g' + p.index;
    return h('section', { className: cx('alt-json-forms-renderers__section', collapsed && 'is-collapsed') },
      h('button', { type: 'button', className: 'alt-json-forms-renderers__sh', 'aria-expanded': !collapsed, 'aria-controls': bodyId, onClick: function () { setCollapsed(!collapsed); } },
        h('span', { className: 'alt-json-forms-renderers__st' }, el.label),
        collapsed ? h('span', { className: 'alt-json-forms-renderers__count alt-num' }, '· ' + n + (n === 1 ? ' campo' : ' campos')) : null,
        h(A.Icon, { name: 'chevron-down', size: 16, className: 'alt-json-forms-renderers__chev' })),
      collapsed ? null : h('div', { id: bodyId, className: 'alt-json-forms-renderers__grid' }, p.renderChildren(el.elements || [])));
  }

  /* ---------- demo ---------- */
  var DEMO_SCHEMA = {
    type: 'object', required: ['cliente', 'peso'],
    properties: {
      cliente: { type: 'string', title: 'Cliente', enum: ['Vidroporto S.A.', 'Transportes Sul', 'Ambev Jundiaí', 'Natura Cajamar'] },
      valorFrete: { type: 'number', title: 'Valor do frete' },
      dataColeta: { type: 'string', format: 'date', title: 'Data de coleta' },
      peso: { type: 'number', title: 'Peso', exclusiveMinimum: 0, errorMessage: 'Informe um peso maior que 0' },
      tipoCarga: { type: 'string', title: 'Tipo de carga', enum: ['Paletizada', 'Granel', 'Refrigerada', 'Fracionada'] },
      codigo: { type: 'string', title: 'Código', readOnly: true },
      escolta: { type: 'boolean', title: 'Exige escolta' },
      observacoes: { type: 'string', title: 'Observações' },
      janelaInicio: { type: 'string', title: 'Início da janela' },
      janelaFim: { type: 'string', title: 'Fim da janela' },
      tempoCarga: { type: 'integer', title: 'Tempo de carga' },
      tolerancia: { type: 'integer', title: 'Tolerância' }
    }
  };
  var DEMO_UI = {
    type: 'VerticalLayout', elements: [
      { type: 'Group', label: 'Dados do frete', elements: [
        { type: 'Control', scope: '#/properties/cliente' },
        { type: 'Control', scope: '#/properties/valorFrete', options: { format: 'currency' } },
        { type: 'Control', scope: '#/properties/dataColeta' },
        { type: 'Control', scope: '#/properties/peso', options: { unit: 'kg' } },
        { type: 'Control', scope: '#/properties/tipoCarga' },
        { type: 'Control', scope: '#/properties/codigo', options: { mono: true } },
        { type: 'Control', scope: '#/properties/escolta' },
        { type: 'Control', scope: '#/properties/observacoes', options: { multi: true, rows: 2, placeholder: 'Instruções para a coleta' } }
      ] },
      { type: 'Group', label: 'Horários', options: { collapsed: true }, elements: [
        { type: 'Control', scope: '#/properties/janelaInicio' },
        { type: 'Control', scope: '#/properties/janelaFim' },
        { type: 'Control', scope: '#/properties/tempoCarga', options: { unit: 'min' } },
        { type: 'Control', scope: '#/properties/tolerancia', options: { unit: 'min' } }
      ] }
    ]
  };
  var DEMO_DATA = { cliente: 'Vidroporto S.A.', valorFrete: 12480, dataColeta: '2026-09-30', tipoCarga: 'Paletizada', codigo: 'FR-2026-0932', escolta: false };

  function JsonFormsRenderers(p) {
    var schema = p.schema || DEMO_SCHEMA, ui = p.uischema || DEMO_UI;
    var ctl = p.data !== undefined;
    var ds = R.useState(p.defaultData || (p.schema ? {} : DEMO_DATA)); var data = ctl ? p.data : ds[0];
    var ts = R.useState({}); var touched = ts[0];
    var idBase = R.useRef('alt-jf-' + Math.random().toString(36).slice(2, 7)).current;
    var errors = R.useMemo(function () { return validate(schema, ui, data); }, [schema, ui, data]);
    function showError(k) { return p.showErrors === true || (p.showErrors !== false && touched[k]); }
    function onValue(path, v) {
      var next = setIn(data, path, v);
      if (!ctl) ds[1](next);
      if (p.onChange) p.onChange(next, validate(schema, ui, next));
    }
    function onTouch(k) { if (!touched[k]) ts[1](Object.assign({}, touched, (function () { var o = {}; o[k] = true; return o; })())); }

    var sectionIndex = 0;
    function renderChildren(els) {
      return els.map(function (el, i) { return renderEl(el, i); });
    }
    function renderEl(el, i) {
      if (!el) return null;
      if (el.type === 'Control') return h(Control, { key: el.scope + i, el: el, schema: schema, data: data, errors: errors, showError: showError, onValue: onValue, onTouch: onTouch, idBase: idBase, readonly: p.readonly });
      if (el.type === 'Group') return h(Section, { key: 'g' + i + el.label, el: el, index: sectionIndex++, idBase: idBase, renderChildren: renderChildren });
      if (el.type === 'Label') return h('p', { key: 'l' + i, className: 'alt-json-forms-renderers__text alt-json-forms-renderers__field--full' }, el.text);
      if (el.type === 'HorizontalLayout') return h('div', { key: 'h' + i, className: 'alt-json-forms-renderers__grid alt-json-forms-renderers__grid--flat alt-json-forms-renderers__field--full' }, renderChildren(el.elements || []));
      /* VerticalLayout and unknown layouts */
      var hasGroups = (el.elements || []).some(function (e) { return e.type === 'Group'; });
      return h('div', { key: 'v' + i, className: hasGroups ? 'alt-json-forms-renderers__stack' : 'alt-json-forms-renderers__grid alt-json-forms-renderers__grid--flat alt-json-forms-renderers__field--full' }, renderChildren(el.elements || []));
    }

    return h('div', { className: cx('alt-json-forms-renderers', !p.bare && 'alt-glass', p.bare && 'alt-json-forms-renderers--bare', p.className), style: p.style },
      renderEl(ui, 0));
  }

  JsonFormsRenderers.validate = validate;
  JsonFormsRenderers.countControls = countControls;
  JsonFormsRenderers.demo = { schema: DEMO_SCHEMA, uischema: DEMO_UI, data: DEMO_DATA };
  A.JsonFormsRenderers = JsonFormsRenderers;
})();


/* ---- LayoutEditavel ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var Icon = A.Icon;

  var COLORS = [
    { id: 'default', label: 'Padrão', c: 'var(--glass-border-strong)' },
    { id: 'primary', label: 'Primária', c: 'var(--teal-light)' },
    { id: 'secondary', label: 'Secundária', c: 'var(--slate)' },
    { id: 'info', label: 'Info', c: 'var(--signal-info)' },
    { id: 'success', label: 'Sucesso', c: 'var(--signal-success)' },
    { id: 'warning', label: 'Alerta', c: 'var(--signal-warning)' },
    { id: 'error', label: 'Erro', c: 'var(--signal-error)' }
  ];
  function colorOf(id) { for (var i = 0; i < COLORS.length; i++) if (COLORS[i].id === id) return COLORS[i]; return COLORS[0]; }

  var DEF_WIDGETS = [
    { id: 'viagens', title: 'Viagens ativas', w: 4, h: 2, color: 'info', value: '87', caption: '12 em rota crítica' },
    { id: 'chamados', title: 'Chamados', w: 3, h: 2, value: '12', caption: '3 estourados' },
    { id: 'prazo', title: 'Entregas no prazo', w: 5, h: 2, color: 'success', value: '94,2%', caption: 'Meta 92,0%' },
    { id: 'violacoes', title: 'Violações recentes', w: 7, h: 2, color: 'error', text: '5 violações nas últimas 24h · 2 críticas aguardando justificativa' },
    { id: 'custo', title: 'Custo por km', w: 5, h: 2, locked: true }
  ];

  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }

  function LayoutEditavel(p) {
    var cols = p.cols || 12, rowHeight = p.rowHeight || 64, margin = p.margin !== undefined ? p.margin : 12;
    var initial = p.widgets || DEF_WIDGETS;
    var ws = R.useState(function () {
      if (p.storageKey) { try { var s = window.localStorage.getItem(p.storageKey); if (s) return JSON.parse(s); } catch (e) { /* sem storage */ } }
      return initial;
    });
    var widgets = ws[0];
    var ed = R.useState(!!p.defaultEditing), editing = ed[0];
    var pk = R.useState(p.defaultColorPickerFor || null), picker = pk[0];
    var dg = R.useState(null), drag = dg[0];
    var ov = R.useState(null), over = ov[0];
    var gridRef = R.useRef(null);
    var counter = R.useRef(1);

    function commit(next) {
      ws[1](next);
      if (p.storageKey) { try { window.localStorage.setItem(p.storageKey, JSON.stringify(next)); } catch (e) { /* sem storage */ } }
      if (p.onLayoutChange) p.onLayoutChange(next);
    }
    function patch(id, ch) { commit(widgets.map(function (w) { return w.id === id ? Object.assign({}, w, ch) : w; })); }
    function remove(id) { commit(widgets.filter(function (w) { return w.id !== id; })); if (picker === id) pk[1](null); }
    function add() {
      var nw = p.newWidget ? p.newWidget(widgets) : { id: 'novo-' + (counter.current++), title: 'Novo widget', w: 4, h: 2, value: '—', caption: 'Configure a fonte de dados' };
      commit(widgets.concat([nw]));
      if (p.onAdd) p.onAdd(nw);
    }
    function reset() { commit(initial); pk[1](null); }
    function setEditing(v) { ed[1](v); if (!v) pk[1](null); if (p.onEditingChange) p.onEditingChange(v); }

    function moveTo(srcId, dstId) {
      if (!srcId || srcId === dstId) return;
      var list = widgets.slice(), from = -1, to = -1;
      list.forEach(function (w, i) { if (w.id === srcId) from = i; if (w.id === dstId) to = i; });
      if (from < 0 || to < 0) return;
      var item = list.splice(from, 1)[0];
      list.splice(to, 0, item);
      commit(list);
    }

    function startResize(e, w) {
      e.preventDefault(); e.stopPropagation();
      var grid = gridRef.current; if (!grid) return;
      var colW = (grid.clientWidth - margin * (cols - 1)) / cols;
      var x0 = e.clientX, y0 = e.clientY, w0 = w.w, h0 = w.h;
      function mv(ev) {
        var nw = clamp(w0 + Math.round((ev.clientX - x0) / (colW + margin)), 2, cols);
        var nh = clamp(h0 + Math.round((ev.clientY - y0) / (rowHeight + margin)), 1, 6);
        ws[1](function (cur) { return cur.map(function (x) { return x.id === w.id ? Object.assign({}, x, { w: nw, h: nh }) : x; }); });
      }
      function up() {
        window.removeEventListener('pointermove', mv); window.removeEventListener('pointerup', up);
        ws[1](function (cur) { if (p.onLayoutChange) p.onLayoutChange(cur); if (p.storageKey) { try { window.localStorage.setItem(p.storageKey, JSON.stringify(cur)); } catch (er) { /* sem storage */ } } return cur; });
      }
      window.addEventListener('pointermove', mv); window.addEventListener('pointerup', up);
    }
    function keyResize(e, w) {
      var d = { ArrowRight: [1, 0], ArrowLeft: [-1, 0], ArrowDown: [0, 1], ArrowUp: [0, -1] }[e.key];
      if (!d) return;
      e.preventDefault();
      patch(w.id, { w: clamp(w.w + d[0], 2, cols), h: clamp(w.h + d[1], 1, 6) });
    }

    function body(w) {
      if (w.locked) return h('div', { className: 'alt-layout-editavel__locked' }, h(Icon, { name: 'lock', size: 16 }), 'Sem permissão para exibir este widget');
      if (w.render) return w.render(w);
      if (w.text) return h('div', { className: 'alt-layout-editavel__text' }, w.text);
      return h('div', null,
        h('div', { className: 'alt-layout-editavel__value alt-num' }, w.value),
        w.caption ? h('div', { className: 'alt-layout-editavel__caption' }, w.caption) : null);
    }

    var cards = widgets.map(function (w) {
      var col = colorOf(w.color);
      var isDrop = editing && over === w.id && drag && drag !== w.id;
      return h('section', {
        key: w.id,
        className: cx('alt-layout-editavel__widget alt-glass', editing && 'is-editing', drag === w.id && 'is-dragging', isDrop && 'is-drop'),
        style: { gridColumn: 'span ' + Math.min(w.w, cols), gridRow: 'span ' + w.h },
        draggable: editing ? true : undefined,
        onDragStart: editing ? function (e) { dg[1](w.id); try { e.dataTransfer.setData('text/plain', w.id); e.dataTransfer.effectAllowed = 'move'; } catch (er) { /* ok */ } } : undefined,
        onDragEnd: function () { dg[1](null); ov[1](null); },
        onDragOver: editing ? function (e) { e.preventDefault(); if (over !== w.id) ov[1](w.id); } : undefined,
        onDrop: editing ? function (e) { e.preventDefault(); moveTo(drag, w.id); dg[1](null); ov[1](null); } : undefined,
        'aria-label': w.title
      },
        h('header', { className: 'alt-layout-editavel__head' },
          editing ? h('span', { className: 'alt-layout-editavel__grip', title: 'Arraste para mover', 'aria-hidden': 'true' }, h(Icon, { name: 'drag', size: 16 })) : null,
          w.color && w.color !== 'default' ? h('span', { className: 'alt-layout-editavel__dot', style: { background: col.c }, title: 'Cor: ' + col.label }) : null,
          h('h3', { className: 'alt-layout-editavel__title' }, w.title),
          editing ? h('div', { className: 'alt-layout-editavel__pick-wrap' },
            h('button', { type: 'button', className: 'alt-layout-editavel__ib', 'aria-label': 'Cor do widget', 'aria-expanded': picker === w.id, onClick: function () { pk[1](picker === w.id ? null : w.id); } },
              h('svg', { width: 15, height: 15, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true' },
                h('circle', { cx: 12, cy: 12, r: 9 }), h('circle', { cx: 8, cy: 10, r: 1 }), h('circle', { cx: 12, cy: 7, r: 1 }), h('circle', { cx: 16, cy: 10, r: 1 }))),
            picker === w.id ? h('div', { className: 'alt-layout-editavel__picker alt-popover', role: 'radiogroup', 'aria-label': 'Cor do widget' },
              COLORS.map(function (c) {
                var on = (w.color || 'default') === c.id;
                return h('button', { key: c.id, type: 'button', role: 'radio', 'aria-checked': on, 'aria-label': c.label, title: c.label,
                  className: cx('alt-layout-editavel__swatch', on && 'is-on'), style: { background: c.id === 'default' ? 'transparent' : c.c, borderColor: c.c },
                  onClick: function () { patch(w.id, { color: c.id }); pk[1](null); } }, on ? h(Icon, { name: 'check', size: 12 }) : null);
              })) : null) : null,
          editing ? h('button', { type: 'button', className: 'alt-layout-editavel__ib', 'aria-label': 'Remover ' + w.title, onClick: function () { remove(w.id); } }, h(Icon, { name: 'x', size: 15 })) : null),
        h('div', { className: 'alt-layout-editavel__body' }, body(w)),
        editing ? h('span', {
          className: 'alt-layout-editavel__resize', role: 'slider', tabIndex: 0,
          'aria-label': 'Redimensionar ' + w.title + ' (setas)', 'aria-valuetext': w.w + ' colunas por ' + w.h + ' linhas', 'aria-valuenow': w.w, 'aria-valuemin': 2, 'aria-valuemax': cols,
          onPointerDown: function (e) { startResize(e, w); }, onKeyDown: function (e) { keyResize(e, w); }
        }) : null);
    });

    var grid = widgets.length ? h('div', {
      ref: gridRef, className: 'alt-layout-editavel__grid',
      style: { gridTemplateColumns: 'repeat(' + cols + ',minmax(0,1fr))', gridAutoRows: rowHeight + 'px', gap: margin }
    }, cards) : h(A.EmptyStates.Item, { variant: 'empty', icon: 'grid', title: 'Nenhum widget no layout', description: 'Adicione widgets para montar sua visão.', actionLabel: 'Adicionar widget', actionIcon: 'plus', onAction: add, style: { minHeight: 260 } });

    var count = widgets.length + (widgets.length === 1 ? ' widget' : ' widgets');
    var toolbar = editing ? h('aside', { className: 'alt-layout-editavel__toolbar alt-glass', 'aria-label': 'Editar layout' },
      h('div', { className: 'alt-layout-editavel__tb-head' },
        h('span', { className: 'alt-layout-editavel__tb-icon' }, h(Icon, { name: 'grid', size: 16 })),
        h('div', null, h('b', null, 'Editar layout'), h('span', { className: 'alt-num' }, count))),
      h('p', { className: 'alt-layout-editavel__tb-hint' }, 'Arraste pelo cabeçalho para mover e pelo canto para redimensionar.'),
      h(A.Button, { variant: 'primary', size: 'sm', arrow: false, onClick: function () { setEditing(false); } }, h(Icon, { name: 'check', size: 16 }), 'Concluir'),
      h('button', { type: 'button', className: 'alt-tbtn', onClick: add }, h(Icon, { name: 'plus', size: 16 }), 'Adicionar widget'),
      h('button', { type: 'button', className: 'alt-tbtn', onClick: reset }, h(Icon, { name: 'refresh', size: 16 }), 'Resetar'),
      h('div', { className: 'alt-layout-editavel__tb-note' }, h(Icon, { name: 'info', size: 14 }), p.saveNote || 'Layout salvo apenas neste dispositivo')) : null;

    return h('div', { className: cx('alt-layout-editavel', editing && 'is-editing', p.className), style: p.style },
      h('div', { className: 'alt-layout-editavel__main' },
        editing ? null : h('div', { className: 'alt-layout-editavel__bar' },
          h('span', { className: 'alt-layout-editavel__count alt-num' }, count),
          h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { setEditing(true); } }, h(Icon, { name: 'edit', size: 16 }), 'Editar layout')),
        grid),
      toolbar);
  }

  LayoutEditavel.COLORS = COLORS;
  A.LayoutEditavel = LayoutEditavel;
})();


/* ---- ManBoxes ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var PRIO = { baixa: { tone: 'neutral', label: 'Baixa' }, normal: { tone: 'info', label: 'Normal' }, alta: { tone: 'warning', label: 'Alta' }, critica: { tone: 'error', label: 'Crítica' } };
  function metaTone(pct, allocated) { return pct >= 100 ? 'error' : pct >= 80 ? 'warning' : allocated ? 'success' : 'neutral'; }
  function chip(tn, text, icon) { var t = K.tone(tn); return h('span', { className: 'alt-chip', style: { color: t.c, background: t.bg } }, icon ? h(A.Icon, { name: icon, size: 12 }) : h('span', { className: 'alt-dot' }), text); }

  var DEFAULT_ORDERS = {
    'os-7712': { id: 'os-7712', numero: '7712', placa: 'ABC-1D23', servico: 'Troca de pastilhas', equipe: 2, meta: 92, prioridade: 'alta' },
    'os-7715': { id: 'os-7715', numero: '7715', placa: 'MNO-2B34', servico: 'Troca de óleo e filtros', equipe: 1, meta: 104, prioridade: 'normal' },
    'os-7720': { id: 'os-7720', numero: '7720', placa: 'QRT-4F56', servico: 'Revisão 60 mil km', prioridade: 'critica', socorro: true },
    'os-7722': { id: 'os-7722', numero: '7722', placa: 'JHK-7G89', servico: 'Alinhamento', prioridade: 'normal' },
    'os-7725': { id: 'os-7725', numero: '7725', placa: 'XYZ-9K87', servico: 'Diagnóstico elétrico', prioridade: 'baixa' }
  };
  var DEFAULT_LANES = [
    { id: 'box-1', kind: 'box', label: 'BOX 1', orders: ['os-7712'] },
    { id: 'box-2', kind: 'box', label: 'BOX 2', orders: [] },
    { id: 'box-3', kind: 'box', label: 'BOX 3', orders: ['os-7715'] },
    { id: 'fila', kind: 'queue', label: 'Aguardando BOX', orders: ['os-7720', 'os-7722', 'os-7725'] }
  ];

  function OrderCard(p) {
    var o = p.order, inBox = p.inBox, mn = R.useState(false), menu = mn[0], setMenu = mn[1];
    var mt = o.meta != null ? metaTone(o.meta, inBox) : null, pr = PRIO[o.prioridade] || PRIO.normal;
    return h('article', { className: cx('alt-man-boxes__card', p.dragging && 'is-dragging'), draggable: true,
      onDragStart: function (e) { try { e.dataTransfer.setData('text/plain', o.id); e.dataTransfer.effectAllowed = 'move'; } catch (x) {} p.onDragStart(o.id); },
      onDragEnd: p.onDragEnd, 'aria-label': 'OS ' + o.numero + ' · ' + o.placa },
      h('div', { className: 'alt-man-boxes__card-top' },
        h('span', { className: 'alt-man-boxes__grip', 'aria-hidden': 'true' }, h(A.Icon, { name: 'drag', size: 14 })),
        h('b', { className: 'alt-num' }, 'OS ' + o.numero + ' · ' + o.placa),
        h('div', { className: 'alt-man-boxes__more' },
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost alt-man-boxes__more-btn', 'aria-label': 'Mover OS ' + o.numero, 'aria-haspopup': 'menu', 'aria-expanded': menu ? 'true' : 'false', onClick: function () { setMenu(!menu); } }, h(A.Icon, { name: 'more', size: 16 })),
          menu ? h('div', { className: 'alt-popover alt-man-boxes__menu', role: 'menu' },
            h('div', { className: 'alt-man-boxes__menu-lbl' }, 'Mover para'),
            p.targets.map(function (t) {
              return h('button', { key: t.id, type: 'button', role: 'menuitem', className: 'alt-menu-item', disabled: t.disabled, onClick: function () { setMenu(false); p.onMove(o.id, t.id); } },
                h(A.Icon, { name: t.kind === 'box' ? 'box' : 'inbox', size: 16 }), t.label, t.disabled ? h('span', { className: 'alt-man-boxes__busy' }, 'Ocupado') : null);
            })) : null)),
      h('div', { className: 'alt-man-boxes__card-sub' }, o.servico + (o.equipe ? ' · ' + o.equipe + (o.equipe === 1 ? ' profissional' : ' profissionais') : '')),
      h('div', { className: 'alt-man-boxes__chips' },
        o.socorro ? chip('error', 'Socorro', 'alert') : null,
        chip(pr.tone, pr.label),
        mt ? chip(mt, o.meta + '% da meta', mt === 'error' ? 'clock' : null) : null),
      o.meta != null ? h('div', { className: 'alt-man-boxes__pb', role: 'meter', 'aria-valuenow': o.meta, 'aria-valuemin': 0, 'aria-valuemax': 100, 'aria-label': 'Tempo consumido da meta' },
        h('i', { style: { width: Math.min(100, o.meta) + '%', background: K.tone(mt).c } })) : null);
  }

  function ManBoxes(p) {
    var orders = p.orders || DEFAULT_ORDERS;
    var ls = R.useState(p.lanes || DEFAULT_LANES), lanes = ls[0], setLanes = ls[1];
    var dg = R.useState(null), dragging = dg[0], setDragging = dg[1];
    var ov = R.useState(null), over = ov[0], setOver = ov[1];
    var fq = R.useState('todas'), filter = fq[0], setFilter = fq[1];

    function canDrop(lane) { return lane.kind !== 'box' || lane.orders.length === 0 || lane.orders.indexOf(dragging) >= 0; }
    function move(id, to) {
      var next = lanes.map(function (l) { return Object.assign({}, l, { orders: l.orders.filter(function (x) { return x !== id; }) }); });
      next.forEach(function (l) { if (l.id === to) l.orders = l.orders.concat(id); });
      setLanes(next); if (p.onMove) p.onMove(id, to, next);
    }
    var visible = function (id) { var o = orders[id]; return filter === 'todas' || (filter === 'socorro' ? o.socorro : o.prioridade === filter); };
    var waiting = lanes.filter(function (l) { return l.kind === 'queue'; }).reduce(function (s, l) { return s + l.orders.length; }, 0);
    var free = lanes.filter(function (l) { return l.kind === 'box' && !l.orders.length; }).length;

    return h('section', { className: cx('alt-man-boxes', p.className), style: p.style },
      h('div', { className: 'alt-man-boxes__bar' },
        h('div', { className: 'alt-man-boxes__ttl' }, h('h3', null, p.title || 'Quadro da oficina'),
          h('span', { className: 'alt-num' }, free + (free === 1 ? ' BOX livre' : ' BOXes livres') + ' · ' + waiting + ' na fila')),
        h('div', { className: 'alt-man-boxes__filters', role: 'group', 'aria-label': 'Filtrar por prioridade' },
          [['todas', 'Todas'], ['socorro', 'Socorro'], ['critica', 'Crítica'], ['alta', 'Alta'], ['normal', 'Normal']].map(function (f) {
            return h('button', { key: f[0], type: 'button', className: 'alt-tbtn', 'aria-pressed': filter === f[0] ? 'true' : 'false', onClick: function () { setFilter(f[0]); } }, f[1]);
          })),
        h('button', { type: 'button', className: 'alt-tbtn', onClick: p.onOpenFilters }, h(A.Icon, { name: 'filter', size: 16 }), 'Filtros')),
      h('div', { className: 'alt-man-boxes__board alt-scroll', style: { gridTemplateColumns: 'repeat(' + lanes.length + ', minmax(220px, 1fr))' } }, lanes.map(function (l) {
        var occupied = l.kind === 'box' && l.orders.length > 0;
        var isOver = over === l.id && dragging && canDrop(l);
        var stateChip = l.kind === 'box' ? (occupied ? chip('success', 'Em execução', 'wrench') : h('span', { className: 'alt-chip alt-man-boxes__free-chip' }, 'Livre')) : h('span', { className: 'alt-count' }, l.orders.length);
        return h('div', { key: l.id, className: cx('alt-panel', 'alt-man-boxes__lane', isOver && 'is-over', dragging && !canDrop(l) && 'is-blocked'), role: 'region', 'aria-label': l.label,
          onDragOver: function (e) { if (dragging && canDrop(l)) { e.preventDefault(); if (over !== l.id) setOver(l.id); } },
          onDragLeave: function (e) { if (!e.currentTarget.contains(e.relatedTarget)) setOver(null); },
          onDrop: function (e) { e.preventDefault(); if (dragging && canDrop(l)) move(dragging, l.id); setOver(null); setDragging(null); } },
          h('div', { className: 'alt-man-boxes__lh' }, h(A.Icon, { name: l.kind === 'box' ? 'box' : 'inbox', size: 16 }), h('span', null, l.label), stateChip),
          l.orders.filter(visible).map(function (id) {
            return h(OrderCard, { key: id, order: orders[id], inBox: l.kind === 'box', dragging: dragging === id,
              onDragStart: setDragging, onDragEnd: function () { setDragging(null); setOver(null); }, onMove: move,
              targets: lanes.filter(function (t) { return t.id !== l.id; }).map(function (t) { return { id: t.id, label: t.label, kind: t.kind, disabled: t.kind === 'box' && t.orders.length > 0 }; }) });
          }),
          l.kind === 'box' && !occupied ? h('div', { className: 'alt-man-boxes__drop' }, h(A.Icon, { name: 'download', size: 16 }), isOver ? 'Soltar aqui' : 'BOX livre · solte a OS aqui') : null,
          l.kind === 'queue' && !l.orders.length ? h('div', { className: 'alt-man-boxes__drop' }, 'Fila vazia') : null);
      })));
  }
  A.ManBoxes = ManBoxes;
})();


/* ---- ManCabecalhoPagina ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var DEFAULT_KPIS = [
    { key: 'abertas', label: 'Abertas', value: '38', help: 'OS abertas ainda não encerradas, em qualquer etapa.' },
    { key: 'atrasadas', label: 'Atrasadas', value: '6', tone: 'error', help: 'OS cujo tempo consumido passou da meta de execução.' },
    { key: 'concluidas', label: 'Concluídas hoje', value: '12', tone: 'success', help: 'OS encerradas desde 00:00 de hoje.' },
    { key: 'tempo', label: 'Tempo médio', value: '3h40', help: 'Média entre abertura e encerramento das OS dos últimos 30 dias.', delta: { text: '−12 min', tone: 'success', label: 'vs. mês anterior' } }
  ];
  var DEFAULT_META = ['Filial Porto Ferreira', 'Atualizado 14:37'];

  /* Chip tonal único: concluída = success em todo lugar */
  var STATUS = { aberta: ['info', 'Aberta'], execucao: ['accent', 'Em execução'], aguardando: ['warning', 'Aguardando peça'], atrasada: ['error', 'Atrasada'], concluida: ['success', 'Concluída'], cancelada: ['neutral', 'Cancelada'] };
  function ToneChip(p) {
    var s = STATUS[p.status] || [p.tone || 'neutral', p.children], t = K.tone(s[0]);
    return h('span', { className: 'alt-chip alt-man-cabecalho-pagina__tchip', style: { color: t.c, background: t.bg } }, h('span', { className: 'alt-dot' }), p.children || s[1]);
  }

  function Kpi(p) {
    var k = p.kpi, o = R.useState(false), open = o[0], setOpen = o[1], t = k.tone ? K.tone(k.tone) : null;
    var id = 'alt-mcp-help-' + k.key;
    return h('div', { className: 'alt-glass alt-man-cabecalho-pagina__kpi' },
      h('div', { className: 'alt-man-cabecalho-pagina__kl' },
        t ? h('span', { className: 'alt-dot', style: { color: t.c }, 'aria-hidden': 'true' }) : null,
        h('span', null, k.label),
        k.help ? h('button', { type: 'button', className: 'alt-man-cabecalho-pagina__info', 'aria-label': 'Sobre ' + k.label, 'aria-describedby': open ? id : undefined, 'aria-expanded': open ? 'true' : 'false',
          onMouseEnter: function () { setOpen(true); }, onMouseLeave: function () { setOpen(false); }, onFocus: function () { setOpen(true); }, onBlur: function () { setOpen(false); }, onClick: function () { setOpen(!open); } },
          h(A.Icon, { name: 'info', size: 14 })) : null,
        open && k.help ? h('span', { id: id, role: 'tooltip', className: 'alt-man-cabecalho-pagina__tip' }, k.help) : null),
      h('div', { className: 'alt-man-cabecalho-pagina__kv alt-num' }, k.value),
      k.delta ? h('div', { className: 'alt-man-cabecalho-pagina__kd alt-num' }, h('b', { style: { color: K.tone(k.delta.tone).c } }, k.delta.text), ' ' + (k.delta.label || '')) : null);
  }

  function ManCabecalhoPagina(p) {
    var kpis = p.kpis === undefined ? DEFAULT_KPIS : p.kpis;
    var meta = p.meta || DEFAULT_META;
    var actions = p.actions !== undefined ? p.actions : [
      h('button', { key: 'e', type: 'button', className: 'alt-tbtn', onClick: p.onExport }, h(A.Icon, { name: 'download', size: 16 }), 'Exportar'),
      h(A.Button, { key: 'n', size: 'sm', arrow: false, onClick: p.onCreate }, h(A.Icon, { name: 'plus', size: 16 }), 'Nova OS')
    ];
    var section = p.section === undefined ? { title: 'Ordens em execução', caption: '5 OS nos BOXes · atualiza a cada 60 s', action: 'Ver quadro' } : p.section;
    return h('div', { className: cx('alt-man-cabecalho-pagina', p.className), style: p.style },
      h('header', { className: 'alt-man-cabecalho-pagina__ph' },
        h('div', { className: 'alt-man-cabecalho-pagina__main' },
          p.eyebrow !== null ? h('div', { className: 'alt-man-cabecalho-pagina__eyebrow' }, p.eyebrow || 'Manutenção') : null,
          h('h1', null, p.title || 'Ordens de serviço'),
          p.subtitle !== null ? h('p', null, p.subtitle || 'Abertura, execução e encerramento das OS da oficina.') : null,
          meta.length ? h('div', { className: 'alt-man-cabecalho-pagina__meta alt-num' }, meta.map(function (m, i) { return h('span', { key: i, className: 'alt-chip alt-man-cabecalho-pagina__mchip' }, m); })) : null),
        actions ? h('div', { className: 'alt-man-cabecalho-pagina__actions' }, actions) : null),
      kpis && kpis.length ? h('div', { className: 'alt-man-cabecalho-pagina__kg' }, kpis.map(function (k) { return h(Kpi, { key: k.key, kpi: k }); })) : null,
      section ? h('div', { className: 'alt-man-cabecalho-pagina__sh' },
        h('div', null, h('h2', null, section.title), section.caption ? h('span', null, section.caption) : null),
        section.chips ? h('div', { className: 'alt-man-cabecalho-pagina__chips' }, section.chips) : h('div', { className: 'alt-man-cabecalho-pagina__chips' },
          h(ToneChip, { status: 'execucao' }), h(ToneChip, { status: 'aguardando' }), h(ToneChip, { status: 'atrasada' }), h(ToneChip, { status: 'concluida' })),
        section.action ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: section.onAction }, section.action, h(A.Icon, { name: 'arrow-right', size: 16 })) : null) : null);
  }
  ManCabecalhoPagina.ToneChip = ToneChip;
  ManCabecalhoPagina.STATUS = STATUS;
  A.ManCabecalhoPagina = ManCabecalhoPagina;
})();


/* ---- ManEquipe ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var PSTATUS = { livre: ['success', 'Livre'], trabalho: ['info', 'Em trabalho'], pausado: ['warning', 'Pausado'] };
  var OSTATE = { execucao: ['info', 'Em execução'], pausada: ['warning', 'Pausada'], aguardando: ['neutral', 'Aguardando equipe'] };
  function chip(tn, text) { var t = K.tone(tn); return h('span', { className: 'alt-chip', style: { color: t.c, background: t.bg } }, h('span', { className: 'alt-dot' }), text); }
  function initials(n) { return n.split(' ').filter(Boolean).slice(0, 2).map(function (x) { return x[0]; }).join('').toUpperCase(); }

  var DEFAULT_PROS = [
    { id: 'rs', nome: 'Rafael Souza', funcao: 'Mecânico', status: 'livre' },
    { id: 'lm', nome: 'Luana Melo', funcao: 'Eletricista', status: 'trabalho' },
    { id: 'jp', nome: 'João Prado', funcao: 'Mecânico', status: 'trabalho' },
    { id: 'pc', nome: 'Paulo Costa', funcao: 'Borracheiro', status: 'pausado' },
    { id: 'ma', nome: 'Marina Alves', funcao: 'Funileira', status: 'livre' },
    { id: 'td', nome: 'Tiago Dias', funcao: 'Mecânico', status: 'livre' }
  ];
  var DEFAULT_ORDERS = [
    { id: 'os-7712', numero: '7712', placa: 'ABC-1D23', servico: 'Troca de pastilhas', box: 'BOX 1', tempo: '1h10 de 1h20', state: 'execucao', equipe: ['lm', 'jp'] },
    { id: 'os-7715', numero: '7715', placa: 'MNO-2B34', servico: 'Troca de óleo e filtros', box: 'BOX 3', tempo: '0h25 de 0h40', state: 'pausada', equipe: ['pc'] },
    { id: 'os-7720', numero: '7720', placa: 'QRT-4F56', servico: 'Revisão 60 mil km', box: 'BOX 2', tempo: 'não iniciada', state: 'aguardando', equipe: [] }
  ];

  function ManEquipe(p) {
    var ps = R.useState(p.profissionais || DEFAULT_PROS), pros = ps[0], setPros = ps[1];
    var os = R.useState(p.ordens || DEFAULT_ORDERS), orders = os[0], setOrders = os[1];
    var sl = R.useState('rs'), sel = sl[0], setSel = sl[1];
    var dg = R.useState(null), dragging = dg[0], setDragging = dg[1];
    var ov = R.useState(null), over = ov[0], setOver = ov[1];
    var cf = R.useState(null), confirm = cf[0], setConfirm = cf[1];
    var op = R.useState({ execucao: true, pausada: true, aguardando: true }), open = op[0], setOpen = op[1];
    var byId = {}; pros.forEach(function (x) { byId[x.id] = x; });

    function setProStatus(ids, st) { setPros(pros.map(function (x) { return ids.indexOf(x.id) >= 0 ? Object.assign({}, x, { status: st }) : x; })); }
    function assign(pid, oid) {
      if (!pid) return;
      var o = orders.filter(function (x) { return x.id === oid; })[0];
      if (!o || o.equipe.indexOf(pid) >= 0) return;
      setOrders(orders.map(function (x) {
        var eq = x.equipe.filter(function (e) { return e !== pid; });
        if (x.id === oid) eq = eq.concat(pid);
        return Object.assign({}, x, { equipe: eq, state: x.id === oid && x.state === 'aguardando' ? 'execucao' : x.state });
      }));
      setProStatus([pid], o.state === 'pausada' ? 'pausado' : 'trabalho');
      if (p.onAssign) p.onAssign(pid, oid);
    }
    function setState(o, st) {
      setOrders(orders.map(function (x) { return x.id === o.id ? Object.assign({}, x, { state: st }) : x; }));
      setProStatus(o.equipe, st === 'pausada' ? 'pausado' : st === 'execucao' ? 'trabalho' : 'livre');
      if (p.onAction) p.onAction(o.id, st);
    }
    function finish(o) {
      setOrders(orders.filter(function (x) { return x.id !== o.id; }));
      setProStatus(o.equipe, 'livre'); setConfirm(null);
      if (p.onAction) p.onAction(o.id, 'finalizada');
    }

    function osCard(o) {
      var st = OSTATE[o.state], isOver = over === o.id;
      var shown = o.equipe.slice(0, 4), extra = o.equipe.length - shown.length;
      return h('article', { key: o.id, className: cx('alt-man-equipe__os', isOver && 'is-over') },
        h('div', { className: 'alt-man-equipe__os-top' },
          h('b', { className: 'alt-num' }, 'OS ' + o.numero + ' · ' + o.placa), chip(st[0], st[1]),
          h('span', { className: 'alt-man-equipe__avs', 'aria-label': 'Equipe: ' + (o.equipe.map(function (e) { return byId[e] && byId[e].nome; }).join(', ') || 'nenhum profissional') },
            shown.map(function (e) { return h('span', { key: e, title: byId[e] && byId[e].nome }, byId[e] ? initials(byId[e].nome) : '?'); }),
            extra > 0 ? h('span', { className: 'alt-man-equipe__extra' }, '+' + extra) : null)),
        h('div', { className: 'alt-man-equipe__os-sub alt-num' }, o.servico + ' · ' + o.box + ' · ' + o.tempo),
        h('button', { type: 'button', className: 'alt-man-equipe__dz', 'aria-label': 'Alocar profissional selecionado na OS ' + o.numero,
          onClick: function () { assign(sel, o.id); },
          onDragOver: function (e) { if (dragging) { e.preventDefault(); if (over !== o.id) setOver(o.id); } },
          onDragLeave: function () { setOver(null); },
          onDrop: function (e) { e.preventDefault(); assign(dragging, o.id); setOver(null); setDragging(null); } },
          h(A.Icon, { name: 'users', size: 16 }),
          isOver ? 'Soltar ' + (byId[dragging] ? byId[dragging].nome : '') : (sel && byId[sel] ? 'Solte os profissionais aqui ou clique para alocar ' + byId[sel].nome.split(' ')[0] : 'Solte os profissionais aqui')),
        confirm === o.id ? h('div', { className: 'alt-man-equipe__confirm', role: 'alertdialog', 'aria-label': 'Finalizar OS ' + o.numero },
          h(A.Icon, { name: 'alert', size: 16 }), h('span', null, 'Finalizar a OS ' + o.numero + ' e liberar a equipe?'),
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { setConfirm(null); } }, 'Cancelar'),
          h('button', { type: 'button', className: 'alt-tbtn alt-man-equipe__ok', onClick: function () { finish(o); } }, 'Finalizar')) :
        h('div', { className: 'alt-man-equipe__acts' },
          o.state === 'execucao' ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { setState(o, 'pausada'); } }, h(A.Icon, { name: 'pause', size: 14 }), 'Pausar') : null,
          o.state === 'pausada' ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { setState(o, 'execucao'); } }, h(A.Icon, { name: 'play', size: 14 }), 'Retomar') : null,
          o.state !== 'aguardando' ? h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { setConfirm(o.id); } }, h(A.Icon, { name: 'check', size: 14 }), 'Finalizar') : null));
    }

    var groups = ['execucao', 'pausada', 'aguardando'];
    return h('div', { className: cx('alt-man-equipe', p.className), style: p.style },
      h('section', { className: 'alt-glass alt-man-equipe__team', 'aria-label': 'Equipe' },
        h('header', { className: 'alt-man-equipe__hd' }, h('h3', null, 'Equipe'), h('span', { className: 'alt-count' }, pros.length),
          h('span', { className: 'alt-man-equipe__hint' }, 'Arraste ou selecione')),
        h('ul', { className: 'alt-man-equipe__list', role: 'listbox', 'aria-label': 'Profissionais' }, pros.map(function (x) {
          var s = PSTATUS[x.status];
          return h('li', { key: x.id, role: 'option', 'aria-selected': sel === x.id ? 'true' : 'false', tabIndex: 0, draggable: true,
            className: cx('alt-man-equipe__pro', sel === x.id && 'is-sel', dragging === x.id && 'is-dragging'),
            onClick: function () { setSel(sel === x.id ? null : x.id); },
            onKeyDown: function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setSel(sel === x.id ? null : x.id); } },
            onDragStart: function (e) { try { e.dataTransfer.setData('text/plain', x.id); } catch (z) {} setDragging(x.id); },
            onDragEnd: function () { setDragging(null); setOver(null); } },
            h('span', { className: 'alt-man-equipe__av' }, initials(x.nome)),
            h('span', { className: 'alt-man-equipe__who' }, h('b', null, x.nome), h('small', null, x.funcao)),
            chip(s[0], s[1]));
        }))),
      h('div', { className: 'alt-man-equipe__orders' }, groups.map(function (g) {
        var list = orders.filter(function (o) { return o.state === g; }), st = OSTATE[g], t = K.tone(st[0]);
        return h('section', { key: g, className: 'alt-panel alt-man-equipe__grp' },
          h('button', { type: 'button', className: 'alt-man-equipe__grp-h', 'aria-expanded': open[g] ? 'true' : 'false', onClick: function () { var n = Object.assign({}, open); n[g] = !n[g]; setOpen(n); } },
            h('span', { className: 'alt-dot', style: { color: t.c } }), st[1], h('span', { className: 'alt-man-equipe__n alt-num' }, list.length),
            h(A.Icon, { name: open[g] ? 'chevron-up' : 'chevron-down', size: 16 })),
          open[g] ? h('div', { className: 'alt-man-equipe__grp-b' }, list.length ? list.map(osCard) : h('div', { className: 'alt-man-equipe__empty' }, 'Nenhuma OS neste estado')) : null);
      })));
  }
  A.ManEquipe = ManEquipe;
})();


/* ---- ManQuiosque ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var DEFAULT_ORDERS = [
    { id: 'os-7712', box: 'BOX 1', placa: 'ABC-1D23', servico: 'Troca de pastilhas', tempo: '1h10', state: 'execucao' },
    { id: 'os-7720', box: 'BOX 3', placa: 'QRT-4F56', servico: 'Revisão 60 mil km', tempo: '0h25', state: 'execucao' },
    { id: 'os-7715', box: 'BOX 2', placa: 'MNO-2B34', servico: 'Troca de óleo e filtros', tempo: '0h40', state: 'pausada' },
    { id: 'os-7722', box: 'BOX 4', placa: 'JHK-7G89', servico: 'Alinhamento e balanceamento', tempo: '0h00', state: 'aguardando' },
    { id: 'os-7725', box: 'BOX 5', placa: 'XYZ-9K87', servico: 'Diagnóstico elétrico', tempo: '2h05', state: 'execucao' },
    { id: 'os-7730', box: 'BOX 6', placa: 'LMN-0P12', servico: 'Troca de embreagem', tempo: '3h15', state: 'execucao' },
    { id: 'os-7731', box: 'Pátio', placa: 'DEF-3G45', servico: 'Inspeção de pneus', tempo: '0h00', state: 'aguardando' }
  ];
  var STATE = { execucao: ['success', 'Em execução', 'play'], pausada: ['warning', 'Pausada', 'pause'], aguardando: ['neutral', 'Aguardando início', 'clock'] };

  function usePrefersReduced() {
    var m = R.useState(false);
    R.useEffect(function () { try { var q = window.matchMedia('(prefers-reduced-motion: reduce)'); m[1](q.matches); } catch (e) {} }, []);
    return m[0];
  }

  function ManQuiosque(p) {
    var orders = p.orders || DEFAULT_ORDERS;
    var perPage = p.perPage || 6, maxPin = p.pinLength || 8, interval = Math.max(3, Math.min(120, p.pageInterval || 10));
    var pages = Math.max(1, Math.ceil(orders.length / perPage));
    var pg = R.useState(0), page = pg[0], setPage = pg[1];
    var sl = R.useState(p.defaultSelected === undefined ? orders[0] && orders[0].id : p.defaultSelected), sel = sl[0], setSel = sl[1];
    var pn = R.useState(p.defaultPin || ''), pin = pn[0], setPin = pn[1];
    var ps = R.useState(false), paused = ps[0], setPaused = ps[1];
    var ms = R.useState(null), msg = ms[0], setMsg = ms[1];
    var reduced = usePrefersReduced();
    var selected = orders.filter(function (o) { return o.id === sel; })[0];

    R.useEffect(function () {
      if (paused || pages < 2 || p.autoAdvance === false) return;
      var t = setInterval(function () { setPage(function (x) { return (x + 1) % pages; }); }, interval * 1000);
      return function () { clearInterval(t); };
    }, [paused, pages, interval]);

    function press(k) {
      setMsg(null);
      if (k === 'clear') return setPin('');
      if (k === 'back') return setPin(pin.slice(0, -1));
      if (pin.length < maxPin) setPin(pin + k);
    }
    function identify() {
      if (pin.length < 4 || !selected) return;
      var r = p.onIdentify ? p.onIdentify(pin, selected) : null;
      setMsg(r || { tone: 'success', text: 'Apontamento registrado em ' + selected.placa + '.' });
      setPin('');
    }
    function onKey(e) {
      if (/^[0-9]$/.test(e.key)) { press(e.key); e.preventDefault(); }
      else if (e.key === 'Backspace') { press('back'); e.preventDefault(); }
      else if (e.key === 'Enter') { identify(); e.preventDefault(); }
    }

    var shown = orders.slice(page * perPage, page * perPage + perPage);
    var verb = !selected ? 'Identificar' : selected.state === 'execucao' ? 'Identificar e pausar' : selected.state === 'pausada' ? 'Identificar e retomar' : 'Identificar e iniciar';
    var keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'clear', '0', 'back'];

    return h('div', { className: cx('alt-man-quiosque', p.className), style: p.style, onMouseEnter: function () { setPaused(true); }, onMouseLeave: function () { setPaused(false); } },
      h('section', { className: 'alt-man-quiosque__cards', 'aria-label': 'Ordens de serviço', 'aria-roledescription': 'carrossel' },
        h('header', { className: 'alt-man-quiosque__hd' },
          h('h3', null, p.title || 'Apontamento de tempo'),
          h('span', { className: 'alt-man-quiosque__upd alt-num' }, (p.updatedLabel || 'Atualizado às 14:37') + ' · troca a cada ' + interval + ' s'),
          h('div', { className: 'alt-man-quiosque__pager' },
            h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon', 'aria-label': 'Página anterior', onClick: function () { setPage((page - 1 + pages) % pages); } }, h(A.Icon, { name: 'chevron-left', size: 18 })),
            h('span', { className: 'alt-man-quiosque__dots', role: 'tablist' }, Array.apply(null, Array(pages)).map(function (_, i) {
              return h('button', { key: i, type: 'button', role: 'tab', 'aria-selected': i === page ? 'true' : 'false', 'aria-label': 'Página ' + (i + 1) + ' de ' + pages, className: cx(i === page && 'is-on'), onClick: function () { setPage(i); } });
            })),
            h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon', 'aria-label': 'Próxima página', onClick: function () { setPage((page + 1) % pages); } }, h(A.Icon, { name: 'chevron-right', size: 18 })))),
        h('div', { className: cx('alt-man-quiosque__grid', !reduced && 'alt-man-quiosque__grid--anim'), key: page, 'aria-live': 'polite' }, shown.map(function (o) {
          var st = STATE[o.state] || STATE.aguardando, t = K.tone(st[0]);
          return h('button', { key: o.id, type: 'button', className: cx('alt-glass', 'alt-man-quiosque__card', sel === o.id && 'is-sel'), 'aria-pressed': sel === o.id ? 'true' : 'false',
            onClick: function () { setSel(o.id); setMsg(null); if (p.onSelect) p.onSelect(o); } },
            h('span', { className: 'alt-man-quiosque__box' }, h(A.Icon, { name: 'box', size: 14 }), o.box,
              h('span', { className: 'alt-chip', style: { color: t.c, background: t.bg } }, h(A.Icon, { name: st[2], size: 12 }), st[1])),
            h('span', { className: 'alt-man-quiosque__placa alt-num' }, o.placa),
            h('span', { className: 'alt-man-quiosque__srv' }, o.servico),
            h('span', { className: 'alt-man-quiosque__tempo alt-num' }, o.tempo),
            h('span', { className: 'alt-man-quiosque__tap' }, sel === o.id ? 'Selecionada · digite seu PIN' : 'Toque para apontar'));
        }))),
      h('section', { className: 'alt-glass alt-man-quiosque__pad', 'aria-label': 'Identificação por PIN', tabIndex: 0, onKeyDown: onKey },
        h('div', { className: 'alt-man-quiosque__for' }, selected ? [h('span', { key: 'a' }, 'Apontar em'), h('b', { key: 'b', className: 'alt-num' }, selected.placa + ' · ' + selected.box)] : h('span', null, 'Selecione uma OS')),
        h('div', { className: 'alt-man-quiosque__pin-lbl', id: 'alt-mq-pin' }, 'Digite seu PIN'),
        h('div', { className: 'alt-man-quiosque__pin', role: 'status', 'aria-labelledby': 'alt-mq-pin', 'aria-label': pin.length + ' de ' + maxPin + ' dígitos' },
          Array.apply(null, Array(maxPin)).map(function (_, i) { return h('i', { key: i, className: cx(i < pin.length && 'is-on') }); })),
        h('div', { className: 'alt-man-quiosque__keys' }, keys.map(function (k) {
          var ghost = k === 'clear' || k === 'back';
          return h('button', { key: k, type: 'button', className: cx('alt-man-quiosque__key', ghost && 'alt-man-quiosque__key--ghost', 'alt-num'), onClick: function () { press(k); },
            'aria-label': k === 'clear' ? 'Limpar' : k === 'back' ? 'Apagar' : k },
            k === 'clear' ? 'Limpar' : k === 'back' ? 'Apagar' : k);
        })),
        msg ? h('div', { className: 'alt-man-quiosque__msg', role: 'status', style: { color: K.tone(msg.tone).c, background: K.tone(msg.tone).bg } }, h(A.Icon, { name: K.tone(msg.tone).icon, size: 16 }), h('span', null, msg.text)) : null,
        h(A.Button, { className: 'alt-man-quiosque__go', arrow: false, disabled: pin.length < 4 || !selected, onClick: identify }, verb)));
  }
  A.ManQuiosque = ManQuiosque;
})();


/* ---- Notificacoes ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var IMP = {
    A: { tone: 'error', icon: 'alert', word: 'Importância alta' },
    M: { tone: 'warning', icon: 'info', word: 'Importância média' },
    B: { tone: 'info', icon: 'check-circle', word: 'Importância baixa' }
  };
  function ago(min) { return new Date(Date.now() - min * 60000); }
  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function when(d) {
    if (!d) return '';
    if (typeof d === 'string' && isNaN(Date.parse(d))) return d;
    var t = d instanceof Date ? d : new Date(d), diff = (Date.now() - t.getTime()) / 60000;
    if (diff < 1) return 'agora';
    if (diff < 60) return 'há ' + Math.floor(diff) + ' min';
    if (diff < 1440) return 'há ' + Math.floor(diff / 60) + ' h';
    return pad(t.getDate()) + '/' + pad(t.getMonth() + 1) + ' ' + pad(t.getHours()) + ':' + pad(t.getMinutes());
  }
  function defaults() {
    var old = new Date(); old.setDate(old.getDate() - 7); old.setHours(9, 12, 0, 0);
    return [
      { id: 4821, importance: 'A', type: 'HelpDesk', title: 'SLA estourado · #4821', message: 'O chamado "Integração TMS fora do ar" passou do prazo em 2 h.', date: ago(12), read: false, link: '#helpdesk/4821' },
      { id: 88213, importance: 'M', type: 'Viagem', title: 'Viagem 88213 atrasada', message: 'ETA previsto 17:45, 1h10 acima do planejado.', date: ago(40), read: false, link: '#viagens/88213' },
      { id: 88190, importance: 'B', type: 'Entrega', title: 'Entrega finalizada', message: 'Entrega #88190 concluída em Santos.', date: old, read: true, link: '#entregas/88190' }
    ];
  }

  function Item(p) {
    var it = p.item, imp = IMP[it.importance] || IMP.B, tn = K.tone(imp.tone);
    return h('li', { className: 'alt-notificacoes__li' },
      h('button', { type: 'button', className: cx('alt-notificacoes__item', !it.read && 'alt-notificacoes__item--unread'), onClick: function () { p.onOpen(it); },
        'aria-label': (it.read ? '' : 'Não lida. ') + imp.word + '. ' + it.title },
        h('span', { className: 'alt-notificacoes__icon', style: { background: tn.bg, color: tn.c } }, h(A.Icon, { name: imp.icon, size: 16 })),
        h('span', { className: 'alt-notificacoes__text' },
          h('span', { className: 'alt-notificacoes__title' }, it.title),
          it.message ? h('span', { className: 'alt-notificacoes__msg' }, it.message) : null,
          h('span', { className: 'alt-notificacoes__meta alt-num' }, (it.type ? it.type + ' · ' : '') + when(it.date || it.time))),
        !it.read ? h('span', { className: 'alt-notificacoes__dot', 'aria-hidden': 'true' }) : null));
  }

  function Panel(p) {
    var list = p.items.slice(0, p.max || 10), unread = p.items.filter(function (i) { return !i.read; }).length;
    return h('div', { className: cx('alt-glass', 'alt-glass--overlay', 'alt-notificacoes__panel', p.floating && 'alt-notificacoes__panel--floating'), role: 'region', 'aria-label': 'Notificações', id: p.id },
      h('div', { className: 'alt-notificacoes__head' },
        h('span', { className: 'alt-notificacoes__heading' }, 'Notificações'),
        unread ? h('span', { className: 'alt-notificacoes__unread alt-num' }, unread + (unread > 1 ? ' não lidas' : ' não lida')) : null,
        h('button', { type: 'button', className: 'alt-notificacoes__all', disabled: !unread, onClick: p.onMarkAll }, 'Marcar todas como lidas')),
      list.length ? h('ul', { className: 'alt-notificacoes__list alt-scroll' }, list.map(function (it) { return h(Item, { key: it.id, item: it, onOpen: p.onOpen }); }))
        : h('div', { className: 'alt-notificacoes__empty' }, h(A.Icon, { name: 'bell', size: 20 }), h('span', null, p.emptyText || 'Nenhuma notificação')),
      list.length ? h('button', { type: 'button', className: 'alt-notificacoes__foot alt-num', onClick: p.onViewAll }, 'Ver todas (' + (p.total != null ? p.total : p.items.length) + ')') : null);
  }

  function Notificacoes(p) {
    var s = R.useState(function () { return p.defaultItems || defaults(); }), local = s[0], setLocal = s[1];
    var controlled = !!p.items, items = controlled ? p.items : local;
    var so = R.useState(!!p.defaultOpen), open = so[0], setOpen = so[1];
    var root = R.useRef(null), btn = R.useRef(null);
    var trigger = p.trigger !== false;
    R.useEffect(function () {
      if (!open || !trigger) return;
      function onDoc(e) { if (root.current && !root.current.contains(e.target)) setOpen(false); }
      function onKey(e) { if (e.key === 'Escape') { setOpen(false); if (btn.current) btn.current.focus(); } }
      document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey);
      return function () { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); };
    }, [open, trigger]);
    function markRead(id) { if (!controlled) setLocal(function (c) { return c.map(function (i) { return i.id === id ? Object.assign({}, i, { read: true }) : i; }); }); }
    function onOpen(it) { markRead(it.id); if (p.onMarkRead && !it.read) p.onMarkRead(it.id); if (p.onOpen) p.onOpen(it); }
    function onMarkAll() { if (!controlled) setLocal(function (c) { return c.map(function (i) { return Object.assign({}, i, { read: true }); }); }); if (p.onMarkAllRead) p.onMarkAllRead(); }
    var panelProps = { items: items, total: p.total != null ? p.total : 38, max: p.maxItems, emptyText: p.emptyText, onOpen: onOpen, onMarkAll: onMarkAll, onViewAll: p.onViewAll, id: 'alt-notificacoes-panel' };
    if (!trigger) return h('div', { className: cx('alt-notificacoes', p.className), style: p.style }, h(Panel, panelProps));
    var unread = items.filter(function (i) { return !i.read; }).length;
    return h('div', { ref: root, className: cx('alt-notificacoes', 'alt-notificacoes--trigger', p.className), style: p.style },
      h('button', { type: 'button', ref: btn, className: cx('alt-notificacoes__bell', open && 'alt-notificacoes__bell--open'), 'aria-haspopup': 'true', 'aria-expanded': open, 'aria-controls': panelProps.id,
        'aria-label': 'Notificações' + (unread ? ', ' + unread + ' não lidas' : ''), onClick: function () { setOpen(!open); } },
        h(A.Icon, { name: 'bell', size: 20 }),
        unread ? h('span', { className: 'alt-count alt-notificacoes__badge' }, unread > 99 ? '99+' : unread) : null),
      open ? h(Panel, Object.assign({ floating: true }, panelProps)) : null);
  }

  A.Notificacoes = Notificacoes;
})();


/* ---- PerfilSeguranca ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var Icon = A.Icon;

  /* Chave de traço (a do kit fica ilegível em 14–16px). */
  function KeyIcon(size) {
    return h('svg', { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      h('circle', { cx: 8, cy: 15, r: 4 }), h('path', { d: 'm11 12 9-9M17 6l3 3M15 8l2 2' }));
  }

  function Section(p) {
    return h('section', { className: 'alt-perfil-seguranca__card alt-glass', 'aria-label': p.title },
      h('header', { className: 'alt-perfil-seguranca__head' },
        h('span', { className: 'alt-perfil-seguranca__head-icon' }, (p.icon === 'key' ? KeyIcon(16) : h(Icon, { name: p.icon, size: 16 }))),
        h('h3', { className: 'alt-perfil-seguranca__title' }, p.title),
        p.extra || null),
      h('div', { className: 'alt-perfil-seguranca__body' }, p.children));
  }

  function StatusChip(p) {
    var t = K.tone(p.on ? 'success' : 'neutral');
    return h('span', { className: 'alt-chip', style: { color: t.c, background: t.bg } }, h(Icon, { name: p.on ? 'check-circle' : 'circle', size: 12 }), p.on ? 'Ativada' : 'Desativada');
  }

  /* 6 caixas de código OTP com avanço automático, backspace e colar. */
  function Otp(p) {
    var len = p.length || 6;
    var refs = R.useRef([]);
    var v = p.value || '';
    function set(i, ch) {
      var arr = v.split(''); while (arr.length < len) arr.push('');
      arr[i] = ch; var next = arr.join('').slice(0, len);
      p.onChange(next.replace(/\s/g, ''));
    }
    return h('div', { className: 'alt-perfil-seguranca__otp', role: 'group', 'aria-label': 'Código de 6 dígitos' },
      Array.apply(null, Array(len)).map(function (_, i) {
        return h('input', {
          key: i, ref: function (el) { refs.current[i] = el; }, className: 'alt-perfil-seguranca__otp-box alt-num',
          inputMode: 'numeric', autoComplete: i === 0 ? 'one-time-code' : 'off', maxLength: 1, 'aria-label': 'Dígito ' + (i + 1),
          value: v[i] || '',
          onChange: function (e) {
            var d = e.target.value.replace(/\D/g, '').slice(-1);
            set(i, d || '');
            if (d && refs.current[i + 1]) refs.current[i + 1].focus();
          },
          onKeyDown: function (e) { if (e.key === 'Backspace' && !v[i] && refs.current[i - 1]) refs.current[i - 1].focus(); },
          onPaste: function (e) {
            var t = (e.clipboardData.getData('text') || '').replace(/\D/g, '').slice(0, len);
            if (t) { e.preventDefault(); p.onChange(t); var f = refs.current[Math.min(t.length, len - 1)]; if (f) f.focus(); }
          }
        });
      }));
  }

  var DEF_PASSKEYS = [
    { id: 'mac', name: 'MacBook · Touch ID', created: '12/08/2026', lastUse: 'hoje' },
    { id: 'iphone', name: 'iPhone', created: '02/09/2026', lastUse: null }
  ];
  var DEF_CODES = ['8F2K-Q9LM', 'T7XD-4NPA', 'C3VR-H6WE', 'M9BZ-2KJT', 'P5QS-8YGU', 'L1NE-7FXC', 'W4HD-3RMZ', 'A6TP-9CQB'];
  var MIN = 8;

  function PerfilSeguranca(p) {
    var user = p.user || { name: 'Ana Ribeiro', email: 'ana.ribeiro@coreplatform.com.br', initials: 'AR' };
    var pw = R.useState({ current: '', password: '', confirm: '' }), f = pw[0];
    var pwMsg = R.useState(null);
    var tfa = R.useState(p.twoFactorEnabled !== undefined ? p.twoFactorEnabled : true), tfaOn = tfa[0];
    var otp = R.useState(p.defaultOtp || '');
    var codes = R.useState(!!p.defaultShowCodes);
    var keys = R.useState(p.passkeys || DEF_PASSKEYS);
    var confirmDel = R.useState(null);
    var copied = R.useState(false);
    var fileRef = R.useRef(null);

    var tooShort = f.password && f.password.length < MIN;
    var mismatch = f.confirm && f.confirm !== f.password;
    var canSave = f.current && f.password && !tooShort && !mismatch && f.confirm;

    function upd(k) { return function (e) { var n = Object.assign({}, f); n[k] = e.target.value; pw[1](n); pwMsg[1](null); }; }
    function savePw(e) {
      e.preventDefault(); if (!canSave) return;
      /* envia a senha atual junto (o original não enviava). */
      if (p.onChangePassword) p.onChangePassword({ current: f.current, password: f.password, confirm: f.confirm });
      pw[1]({ current: '', password: '', confirm: '' }); pwMsg[1]('Senha atualizada.');
    }
    function field(id, label, k, extra) {
      return h('label', { className: 'alt-field' },
        h('span', { className: 'alt-field__label' }, label),
        h('span', { className: cx('alt-ctl alt-ctl--sm', extra && extra.error && 'alt-ctl--error') },
          h(Icon, { name: 'lock', size: 14 }),
          h('input', { type: 'password', value: f[k], onChange: upd(k), autoComplete: k === 'current' ? 'current-password' : 'new-password', 'aria-invalid': extra && extra.error ? 'true' : undefined })),
        extra && extra.error ? h('span', { className: 'alt-field__error' }, h(Icon, { name: 'alert', size: 12 }), extra.error)
          : (extra && extra.help ? h('span', { className: 'alt-field__help' }, extra.help) : null));
    }
    function copyCodes() {
      try { navigator.clipboard.writeText(DEF_CODES.join('\n')); } catch (e) { /* sem clipboard */ }
      copied[1](true); setTimeout(function () { copied[1](false); }, 1600);
    }
    function downloadCodes() {
      try {
        var a = document.createElement('a');
        a.href = URL.createObjectURL(new Blob([(p.recoveryCodes || DEF_CODES).join('\n')], { type: 'text/plain' }));
        a.download = 'codigos-recuperacao.txt'; a.click();
      } catch (e) { /* sem download */ }
    }

    var photo = h(Section, { title: 'Foto', icon: 'user' },
      h('div', { className: 'alt-perfil-seguranca__photo' },
        h('span', { className: 'alt-perfil-seguranca__avatar', 'aria-hidden': user.photo ? undefined : 'true' },
          user.photo ? h('img', { src: user.photo, alt: user.name }) : user.initials),
        h('div', null,
          h('b', null, user.name), h('span', null, user.email),
          h('input', { ref: fileRef, type: 'file', accept: 'image/*', hidden: true, onChange: function (e) { if (p.onPhotoChange && e.target.files[0]) p.onPhotoChange(e.target.files[0]); } }),
          h('button', { type: 'button', className: 'alt-tbtn alt-perfil-seguranca__ghost', onClick: function () { fileRef.current && fileRef.current.click(); } }, h(Icon, { name: 'upload', size: 14 }), 'Alterar foto'))));

    var password = h(Section, { title: 'Senha', icon: 'lock' },
      h('form', { className: 'alt-perfil-seguranca__form', onSubmit: savePw },
        field('cur', 'Senha atual', 'current'),
        field('new', 'Nova senha', 'password', { error: tooShort ? 'Use pelo menos ' + MIN + ' caracteres.' : null, help: 'Mínimo de ' + MIN + ' caracteres.' }),
        field('conf', 'Confirmar nova senha', 'confirm', { error: mismatch ? 'As senhas não conferem.' : null }),
        h('div', { className: 'alt-perfil-seguranca__row' },
          pwMsg[0] ? h('span', { className: 'alt-perfil-seguranca__ok', role: 'status' }, h(Icon, { name: 'check-circle', size: 14 }), pwMsg[0]) : h('span'),
          h('button', { type: 'submit', className: 'alt-tbtn', disabled: !canSave, 'aria-disabled': !canSave }, 'Atualizar senha'))));

    var twoFa = h(Section, { title: 'Autenticação em dois fatores', icon: 'shield', extra: h(StatusChip, { on: tfaOn }) },
      tfaOn ? h(R.Fragment, null,
        h('span', { className: 'alt-perfil-seguranca__sub' }, 'Confirme o código do aplicativo autenticador'),
        h(Otp, { value: otp[0], onChange: function (v) { otp[1](v); if (v.length === 6 && p.onVerifyOtp) p.onVerifyOtp(v); } }),
        h('div', { className: 'alt-perfil-seguranca__actions' },
          h('button', { type: 'button', className: 'alt-tbtn', 'aria-expanded': codes[0], onClick: function () { codes[1](!codes[0]); } }, KeyIcon(14), 'Códigos de recuperação'),
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-perfil-seguranca__danger', onClick: function () { tfa[1](false); codes[1](false); if (p.onDisable2fa) p.onDisable2fa(); } }, 'Desativar')),
        codes[0] ? h('div', { className: 'alt-perfil-seguranca__codes' },
          h('div', { className: 'alt-perfil-seguranca__code-grid alt-num' }, (p.recoveryCodes || DEF_CODES).map(function (c) { return h('code', { key: c }, c); })),
          h('div', { className: 'alt-perfil-seguranca__actions' },
            h('button', { type: 'button', className: 'alt-tbtn', onClick: copyCodes }, h(Icon, { name: copied[0] ? 'check' : 'copy', size: 14 }), copied[0] ? 'Copiado' : 'Copiar'),
            h('button', { type: 'button', className: 'alt-tbtn', onClick: downloadCodes }, h(Icon, { name: 'download', size: 14 }), 'Baixar'))) : null)
        : h(R.Fragment, null,
          h('span', { className: 'alt-perfil-seguranca__sub' }, 'Proteja o acesso com um código do aplicativo autenticador.'),
          h('div', { className: 'alt-perfil-seguranca__actions' },
            h(A.Button, { variant: 'primary', size: 'sm', arrow: false, onClick: function () { tfa[1](true); otp[1](''); if (p.onEnable2fa) p.onEnable2fa(); } }, 'Ativar 2FA'))));

    var passkeys = h(Section, { title: 'Chaves de acesso', icon: 'key',
      extra: h('button', { type: 'button', className: 'alt-tbtn', onClick: function () {
        var n = { id: 'k' + Date.now(), name: 'Nova chave', created: new Date().toLocaleDateString('pt-BR'), lastUse: null };
        keys[1](keys[0].concat([n])); if (p.onAddPasskey) p.onAddPasskey(n);
      } }, h(Icon, { name: 'plus', size: 14 }), 'Adicionar') },
      keys[0].length ? h('ul', { className: 'alt-perfil-seguranca__keys' }, keys[0].map(function (k) {
        var asking = confirmDel[0] === k.id;
        return h('li', { key: k.id },
          h('span', { className: 'alt-perfil-seguranca__key-icon' }, KeyIcon(16)),
          h('div', { className: 'alt-perfil-seguranca__key-text' }, h('b', null, k.name),
            h('small', { className: 'alt-num' }, 'Criada em ' + k.created + ' · ' + (k.lastUse ? 'Último uso: ' + k.lastUse : 'Nunca utilizada'))),
          asking ? h('div', { className: 'alt-perfil-seguranca__actions' },
            h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { confirmDel[1](null); } }, 'Cancelar'),
            h('button', { type: 'button', className: 'alt-tbtn alt-perfil-seguranca__danger', onClick: function () {
              keys[1](keys[0].filter(function (x) { return x.id !== k.id; })); confirmDel[1](null); if (p.onRemovePasskey) p.onRemovePasskey(k.id);
            } }, h(Icon, { name: 'trash', size: 14 }), 'Remover'))
            : h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Remover ' + k.name, onClick: function () { confirmDel[1](k.id); } }, h(Icon, { name: 'x', size: 16 })));
      })) : h('span', { className: 'alt-perfil-seguranca__sub' }, 'Nenhuma chave de acesso cadastrada.'));

    return h('div', { className: cx('alt-perfil-seguranca', p.className), style: p.style },
      h('div', { className: 'alt-perfil-seguranca__col' }, photo, password),
      h('div', { className: 'alt-perfil-seguranca__col' }, twoFa, passkeys));
  }

  A.PerfilSeguranca = PerfilSeguranca;
})();


/* ---- PlanOperacaoCard ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 14 }); }
  var SCALE = 'M12 3v18M5 7h14M7 7l-3 7a3 3 0 0 0 6 0zM17 7l-3 7a3 3 0 0 0 6 0zM8 21h8';
  function scale() {
    return h('svg', { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      SCALE.split(/(?=M)/).map(function (d, i) { return h('path', { key: i, d: d }); }));
  }

  /* metricas: [{ id:'valor'|'volume'|'carros'|..., label, valor, budget? , icon? }] */
  var DEFAULT_METRICAS = [
    { id: 'valor', label: 'Valor', valor: 'R$ 2,90 mi', budget: 'R$ 2,70 mi' },
    { id: 'volume', label: 'Volume', valor: '10.840 t', budget: '10.200 t' },
    { id: 'carros', label: 'Carros', valor: '412 carros', budget: '398 carros' }
  ];
  var ICON = { valor: 'dollar', volume: null, carros: 'truck' };

  function PlanOperacaoCard(p) {
    var C = 'alt-plan-operacao-card';
    var total = !!p.total;
    var op = p.operacao || { id: 'graneis', nome: total ? 'Total' : 'Granéis' };
    var cS = R.useState(p.defaultChecked !== false);
    var checked = total ? true : (p.checked !== undefined ? p.checked : cS[0]);
    var metricas = p.metricas || DEFAULT_METRICAS;
    var id = 'alt-poc-' + op.id;

    function toggle() {
      if (total) return;
      cS[1](!checked); p.onToggle && p.onToggle(op.id, !checked);
    }

    var body = h('div', { className: C + '__mt alt-num' }, metricas.map(function (m) {
      var iconName = m.icon !== undefined ? m.icon : ICON[m.id];
      return h('div', { key: m.id, className: C + '__m' },
        h('span', { className: C + '__mi', title: m.label }, iconName ? ic(iconName) : scale()),
        h('div', { className: C + '__mv' },
          h('span', { className: C + '__sr' }, m.label + ': '),
          h('b', null, m.valor),
          m.budget ? h('small', null, 'Budget ' + m.budget) : null));
    }));

    if (total) {
      return h('div', { className: cx(C, C + '--total', p.className), role: 'group', 'aria-labelledby': id },
        h('div', { className: C + '__hd' }, h('span', { className: C + '__sum', 'aria-hidden': 'true' }, 'Σ'), h('span', { id: id, className: C + '__name' }, op.nome), h('span', { className: C + '__fixed' }, 'fixo')), body);
    }
    return h('div', { className: cx(C, checked ? C + '--on' : C + '--off', p.className), role: 'checkbox', 'aria-checked': checked ? 'true' : 'false', 'aria-labelledby': id, tabIndex: 0,
      onClick: toggle, onKeyDown: function (e) { if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); toggle(); } } },
      h('div', { className: C + '__hd' },
        h('span', { className: C + '__cb', 'aria-hidden': 'true' }, checked ? h(A.Icon, { name: 'check', size: 12, strokeWidth: 3 }) : null),
        h('span', { id: id, className: C + '__name' }, op.nome),
        checked ? null : h('span', { className: C + '__fixed' }, 'fora do filtro')),
      body);
  }
  A.PlanOperacaoCard = PlanOperacaoCard;
})();


/* ---- PplAgendaSemanal ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 16 }); }
  function p2(n) { return (n < 10 ? '0' : '') + n; }
  function parts(x) { var hh = Math.floor(x + 1e-6), mm = Math.round((x - hh) * 60); if (mm === 60) { hh++; mm = 0; } return [hh, mm]; }
  function full(x) { var t = parts(x); return p2(t[0]) + ':' + p2(t[1]); }
  function comp(x) { var t = parts(x); return t[1] ? t[0] + ':' + p2(t[1]) : String(t[0]); }

  var TIPOS = {
    viagem: { label: 'Viagem', c: 'var(--chart-1)' },
    manutencao: { label: 'Manutenção', c: 'var(--chart-5)' },
    treinamento: { label: 'Treinamento', c: 'var(--chart-2)' },
    folga: { label: 'Folga', c: 'var(--chart-6)' }
  };
  var DIAS = [
    { label: 'Seg 29' }, { label: 'Ter 30', hoje: true }, { label: 'Qua 01' }, { label: 'Qui 02' },
    { label: 'Sex 03' }, { label: 'Sáb 04', fds: true }, { label: 'Dom 05', fds: true }
  ];
  /* horas contadas a partir de 00:00 do primeiro dia; lane: 'agendado' | 'realizado' */
  function E(lane, tipo, titulo, ini, fim, live) { return { lane: lane, tipo: tipo, titulo: titulo, ini: ini, fim: fim, live: live }; }
  var D = 24;
  var DATA = {
    veiculo: [
      { id: 'v1', titulo: 'ABC-1D23', sub: 'Ana R. · Granéis', eventos: [
        E('agendado', 'viagem', 'Viagem #88213', 8, 18), E('realizado', 'viagem', 'Viagem #88213', 8.5, 17.75),
        E('agendado', 'manutencao', 'Manutenção preventiva', D + 7, D + 13), E('realizado', 'manutencao', 'Manutenção preventiva', D + 7.5, D + 14.25),
        E('agendado', 'viagem', 'Viagem #88251', 2 * D + 5, 2 * D + 19), E('agendado', 'viagem', 'Viagem #88266', 3 * D + 6, 3 * D + 11.5),
        E('agendado', 'folga', 'Folga', 5 * D, 6 * D)] },
      { id: 'v2', titulo: 'QRT-4F56', sub: 'Ana R. · Vidros', eventos: [
        E('agendado', 'treinamento', 'Treinamento NR-11', 5, 12), E('realizado', 'treinamento', 'Treinamento NR-11', 5, 12),
        E('agendado', 'viagem', 'Viagem #88227', D + 10, 2 * D + 9.5), E('realizado', 'viagem', 'Viagem #88227', D + 10.75, null, true),
        E('agendado', 'viagem', 'Viagem #88260', 3 * D + 13, 3 * D + 16.25), E('agendado', 'viagem', 'Viagem #88270', 4 * D + 4, 4 * D + 20)] },
      { id: 'v3', titulo: 'JKL-7H89', sub: 'Carlos M. · Granéis', eventos: [
        E('agendado', 'viagem', 'Viagem #88199', 6, 15), E('realizado', 'viagem', 'Viagem #88199', 6.25, 16),
        E('agendado', 'viagem', 'Viagem #88231', D + 8, D + 17), E('realizado', 'viagem', 'Viagem #88231', D + 8.25, null, true),
        E('agendado', 'manutencao', 'Troca de pneus', 2 * D + 13, 2 * D + 15), E('agendado', 'viagem', 'Viagem #88281', 4 * D + 20, 5 * D + 6),
        E('agendado', 'folga', 'Folga', 6 * D, 7 * D)] },
      { id: 'v4', titulo: 'MNO-2B34', sub: 'Paulo S. · Químicos', eventos: [
        E('agendado', 'manutencao', 'Revisão 120 mil km', 0, D + 12), E('realizado', 'manutencao', 'Revisão 120 mil km', 0, D + 9),
        E('agendado', 'viagem', 'Viagem #88240', 2 * D + 1, 2 * D + 13), E('agendado', 'viagem', 'Viagem #88262', 3 * D + 7, 3 * D + 18)] }
    ],
    motorista: [
      { id: 'm1', titulo: 'João Pereira', sub: 'Ana R. · Granéis', eventos: [
        E('agendado', 'viagem', 'Viagem #88213', 8, 18), E('realizado', 'viagem', 'Viagem #88213', 8.5, 17.75),
        E('agendado', 'treinamento', 'Direção defensiva', D + 8, D + 12), E('realizado', 'treinamento', 'Direção defensiva', D + 8, D + 12),
        E('agendado', 'viagem', 'Viagem #88251', 2 * D + 5, 2 * D + 19), E('agendado', 'folga', 'Folga', 5 * D, 7 * D)] },
      { id: 'm2', titulo: 'Marta Souza', sub: 'Ana R. · Vidros', eventos: [
        E('agendado', 'folga', 'Afastamento médico', 0, 2 * D), E('agendado', 'viagem', 'Viagem #88240', 2 * D + 1, 2 * D + 13), E('agendado', 'viagem', 'Viagem #88262', 3 * D + 7, 3 * D + 18)] }
    ],
    conjunto: [
      { id: 'c1', titulo: 'ABC-1D23 + IMP-0A11', sub: 'Bitrem · Granéis', eventos: [
        E('agendado', 'viagem', 'Viagem #88213', 8, 18), E('realizado', 'viagem', 'Viagem #88213', 8.5, 17.75),
        E('agendado', 'viagem', 'Viagem #88251', 2 * D + 5, 2 * D + 19)] }
    ]
  };
  var ENT = [{ id: 'veiculo', label: 'Veículos', un: 'veículos' }, { id: 'motorista', label: 'Motoristas', un: 'motoristas' }, { id: 'conjunto', label: 'Conjuntos', un: 'conjuntos' }];

  function Bar(p) {
    var C = 'alt-ppl-agenda-semanal';
    var s = p.s, t = TIPOS[s.ev.tipo] || TIPOS.viagem;
    var allDay = s.a === 0 && s.b === 24 && s.ev.tipo === 'folga';
    var a = s.a, b = s.b;
    var tip = (s.ev.lane === 'agendado' ? 'Agendado' : 'Realizado') + ' · ' + s.ev.titulo + ' · ' + (allDay ? 'dia todo' : full(a) + ' → ' + (s.live ? 'agora' : full(b))) + (s.cr ? ' (continua no dia seguinte)' : '') + (s.cl ? ' (vem do dia anterior)' : '');
    var endF = s.live ? 'agora' : full(b), endC = s.live ? '…' : comp(b);
    return h('button', { type: 'button', className: cx(C + '__bar', s.cr && C + '__bar--cr', s.cl && C + '__bar--cl', s.live && C + '__bar--live', p.sel && C + '__bar--sel'),
      style: { left: (a / 24 * 100) + '%', width: ((b - a) / 24 * 100) + '%', '--c': t.c }, title: tip, 'aria-label': tip,
      onClick: function (e) { e.stopPropagation(); p.onSelect && p.onSelect(s.ev); } },
      p.simple ? null : h('span', { className: cx(C + '__lbl', 'alt-num', (comp(a) + endC).length > 4 && C + '__lbl--long') }, allDay ? h('b', null, 'Dia todo') : [
        h('span', { key: 'f', className: C + '__f' }, h('b', null, full(a)), h('em', null, '–' + endF)),
        h('span', { key: 'c', className: C + '__c' }, h('b', null, comp(a)), h('em', null, '–' + endC)),
        h('span', { key: 's', className: C + '__s' }, h('b', null, comp(a)))]));
  }

  function PplAgendaSemanal(p) {
    var C = 'alt-ppl-agenda-semanal';
    var dias = p.dias || DIAS, nd = dias.length;
    var eS = R.useState(p.entityKey || 'veiculo'), ek = p.entityKey && p.onEntityKeyChange ? p.entityKey : eS[0];
    var oS = R.useState(!!p.showOnlyScheduled), only = oS[0];
    var sS = R.useState(p.selectedEntity !== undefined ? p.selectedEntity : 'v2'), selEnt = sS[0];
    var vS = R.useState(p.selectedEvent || null), selEv = vS[0];
    var src = p.data ? (Array.isArray(p.data) ? p.data : p.data[ek] || []) : DATA[ek] || [];
    var ent = ENT.filter(function (x) { return x.id === ek; })[0] || ENT[0];
    var now = p.agora != null ? p.agora : D + 14.5;
    var hojeIdx = -1; dias.forEach(function (d, i) { if (d.hoje) hojeIdx = i; });
    var lanes = only ? ['agendado'] : ['agendado', 'realizado'];
    var simple = !!p.simplifiedVision;

    function segs(evs, lane, di) {
      var d0 = di * 24, out = [];
      evs.forEach(function (ev) {
        if (ev.lane !== lane) return;
        var fim = ev.live ? now : ev.fim;
        var a = Math.max(ev.ini, d0), b = Math.min(fim, d0 + 24);
        if (b <= a) return;
        out.push({ ev: ev, a: a - d0, b: b - d0, cr: fim > d0 + 24, cl: ev.ini < d0, live: ev.live && b === fim });
      });
      return out;
    }
    function pickEnt(id) { sS[1](id); p.onSelectEntity && p.onSelectEntity(id); }
    function pickEv(ev) { vS[1](ev); p.onSelectEvent && p.onSelectEvent(ev); }

    var axis = h('div', { className: C + '__ax alt-num', 'aria-hidden': 'true' }, h('span', { style: { left: 0 } }, '0h'), h('span', { style: { left: '25%' } }, '6'), h('span', { style: { left: '50%' } }, '12'), h('span', { style: { left: '75%' } }, '18'));

    return h('section', { className: cx(C, 'alt-panel', p.className), 'aria-label': 'Agenda semanal · ' + ent.label },
      h('header', { className: C + '__hd' },
        h('div', { className: C + '__seg', role: 'tablist', 'aria-label': 'Entidade' }, ENT.map(function (x) {
          return h('button', { key: x.id, type: 'button', role: 'tab', 'aria-selected': ek === x.id ? 'true' : 'false', onClick: function () { eS[1](x.id); p.onEntityKeyChange && p.onEntityKeyChange(x.id); } }, x.label);
        })),
        h('div', { className: C + '__week' },
          h('button', { type: 'button', className: C + '__ib', 'aria-label': 'Semana anterior', onClick: function () { p.onWeekChange && p.onWeekChange(-1); } }, ic('chevron-left', 16)),
          h('span', { className: 'alt-num' }, p.periodo || '29/09 – 05/10'),
          h('button', { type: 'button', className: C + '__ib', 'aria-label': 'Próxima semana', onClick: function () { p.onWeekChange && p.onWeekChange(1); } }, ic('chevron-right', 16))),
        h('span', { className: C + '__sub alt-num' }, (p.total != null ? p.total : src.length) + ' ' + ent.un),
        h('span', { className: C + '__sp' }),
        h('label', { className: C + '__sw' },
          h('input', { type: 'checkbox', role: 'switch', checked: only, onChange: function (e) { oS[1](e.target.checked); p.onShowOnlyScheduledChange && p.onShowOnlyScheduledChange(e.target.checked); } }),
          h('span', { className: C + '__swt', 'aria-hidden': 'true' }), 'Somente agendados'),
        p.headerActions || null),
      h('div', { className: C + '__wrap alt-scroll' },
        h('table', { className: cx(C + '__t', simple && C + '__t--simple') },
          h('colgroup', null, h('col', { style: { width: '15%' } }), h('col', { style: { width: '7%' } }), dias.map(function (d, i) { return h('col', { key: i }); })),
          h('thead', null,
            h('tr', { className: C + '__h0' },
              h('th', { scope: 'col', rowSpan: simple ? 1 : 2 }, 'Descrição'), h('th', { scope: 'col', rowSpan: simple ? 1 : 2 }, 'Tipo'),
              dias.map(function (d, i) { return h('th', { key: i, scope: 'col', className: cx(d.fds && C + '__we', d.hoje && C + '__today') }, d.hoje ? h('i', { className: C + '__tdot', 'aria-hidden': 'true' }) : null, d.label, d.hoje ? h('span', { className: C + '__hoje' }, 'hoje') : null); })),
            simple ? null : h('tr', { className: C + '__h1' }, dias.map(function (d, i) { return h('th', { key: i, className: cx(d.fds && C + '__we', d.hoje && C + '__today') }, axis); }))),
          h('tbody', null, src.length ? src.map(function (row) {
            var isSel = selEnt === row.id;
            return h('tr', { key: row.id, className: cx(C + '__row', isSel && C + '__row--sel'), onClick: function () { pickEnt(isSel ? null : row.id); }, 'aria-selected': isSel ? 'true' : 'false' },
              h('th', { scope: 'row', className: C + '__pl' },
                h('button', { type: 'button', className: C + '__plb', onClick: function (e) { e.stopPropagation(); pickEnt(isSel ? null : row.id); }, 'aria-pressed': isSel ? 'true' : 'false' },
                  h('b', null, row.titulo), h('span', null, row.sub))),
              h('td', { className: C + '__ty' }, lanes.map(function (l) { return h('span', { key: l }, l === 'agendado' ? 'Agend.' : 'Real.'); })),
              dias.map(function (d, di) {
                return h('td', { key: di, className: cx(C + '__dy', d.fds && C + '__we', d.hoje && C + '__today') },
                  lanes.map(function (l, li) {
                    return h('div', { key: l, className: C + '__ln' },
                      segs(row.eventos || [], l, di).map(function (s, k) { return h(Bar, { key: k, s: s, simple: simple, sel: selEv === s.ev, onSelect: pickEv }); }),
                      di === hojeIdx && now >= di * 24 && now <= di * 24 + 24 ? h('s', { className: cx(C + '__now', li === 0 && C + '__now--top'), style: { left: ((now - di * 24) / 24 * 100) + '%' } }) : null);
                  }));
              }));
          }) : h('tr', null, h('td', { colSpan: nd + 2, className: C + '__empty' }, 'Nenhum ' + ent.label.toLowerCase().replace(/s$/, '') + ' com eventos nesta semana.'))))),
      h('footer', { className: C + '__lg' },
        h('span', null, h('i', { className: C + '__k' }), 'Início–fim do evento'),
        h('span', null, h('i', { className: C + '__k ' + C + '__k--cr' }), 'Continua no dia seguinte'),
        only ? null : h('span', null, h('i', { className: C + '__k ' + C + '__k--live' }), 'Em andamento'),
        hojeIdx >= 0 ? h('span', null, h('i', { className: C + '__nw' }), 'Agora ' + full(now - hojeIdx * 24)) : null,
        h('span', { className: C + '__lgsep', 'aria-hidden': 'true' }),
        Object.keys(TIPOS).map(function (k) { return h('span', { key: k }, h('i', { className: C + '__kd', style: { background: TIPOS[k].c } }), TIPOS[k].label); }),
        selEv ? h('span', { className: C + '__selinfo' }, ic('info', 14), selEv.titulo) : null));
  }
  A.PplAgendaSemanal = PplAgendaSemanal;
})();


/* ---- PplCardColumnDay ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 16 }); }
  function useCtl(value, def) {
    var s = R.useState(def);
    return [value !== undefined ? value : s[0], s[1]];
  }
  /* parse "58,5" -> 58.5 */
  function num(v) { var n = parseFloat(String(v).replace(/\./g, '').replace(',', '.')); return isNaN(n) ? 0 : n; }

  var UNLOCK = 'M4 11h16v10H4zM8 11V7a4 4 0 0 1 7.5-2';

  function PplCardColumnDay(p) {
    var max = p.quantidadeMax != null ? p.quantidadeMax : 50;
    var pesoMedio = p.pesoMedio != null ? p.pesoMedio : 27;
    var ro = !!p.somenteLeitura;
    var q = useCtl(p.quantidade, p.defaultQuantidade != null ? p.defaultQuantidade : 3);
    var man = useCtl(p.pesoManual, p.defaultPesoManual || 'N');
    var sync = useCtl(p.isSynced, p.defaultSynced != null ? p.defaultSynced : true);
    var pesoS = useCtl(p.peso, p.defaultPeso);
    var qty = q[0], manual = man[0] === 'S', synced = sync[0];
    var calc = p.pesoCalculado != null ? p.pesoCalculado : qty * pesoMedio;
    var pesoVal = manual ? (pesoS[0] != null ? pesoS[0] : nf(calc, 1)) : (qty ? nf(calc, 1) : '');
    var idx = p.index != null ? p.index : 0;
    var id = 'alt-pcd-' + (p.id || idx);

    function setQty(v) {
      v = Math.max(0, Math.min(max, v));
      q[1](v); p.onChangeQuantidade && p.onChangeQuantidade(v, idx);
    }
    function toggleManual() {
      var nv = manual ? 'N' : 'S';
      man[1](nv); if (nv === 'S') sync[1](false);
      p.onChangePesoManual && p.onChangePesoManual(nv, idx);
    }
    function toggleSync() {
      var nv = !synced; sync[1](nv);
      if (nv) { man[1]('N'); p.onChangePesoManual && p.onChangePesoManual('N', idx); }
      p.onToggleSync && p.onToggleSync(nv, idx);
    }

    var state = qty === 0 ? 'empty' : manual ? 'manual' : synced ? 'synced' : 'calc';
    var ST = {
      synced: { t: 'success', label: 'Sincronizado' },
      manual: { t: 'warning', label: 'Peso informado' },
      calc: { t: 'accent', label: 'Peso calculado' },
      empty: { t: 'neutral', label: 'Sem entregas' }
    }[state];
    var tn = K.tone(ST.t);

    return h('div', { className: cx('alt-ppl-card-column-day', 'alt-glass', ro && 'alt-ppl-card-column-day--ro', state === 'empty' && 'alt-ppl-card-column-day--empty', p.className), role: 'group', 'aria-labelledby': id },
      h('div', { className: 'alt-ppl-card-column-day__hd' },
        h('div', { className: 'alt-ppl-card-column-day__ttl' },
          h('span', { id: id, className: 'alt-ppl-card-column-day__date alt-num' }, p.data || 'Seg 29/09'),
          h('span', { className: 'alt-ppl-card-column-day__st', style: { color: tn.c } }, h('span', { className: 'alt-dot', 'aria-hidden': 'true' }), ST.label)),
        ro ? null : h('button', { type: 'button', className: 'alt-ppl-card-column-day__ib', 'aria-pressed': synced ? 'true' : 'false', title: synced ? 'Sincronizado com o peso médio' : 'Sincronizar com o peso médio', 'aria-label': 'Sincronizar com o peso médio', onClick: toggleSync, disabled: qty === 0 }, ic('link', 15))),
      h('div', { className: 'alt-ppl-card-column-day__bd' },
        h('span', { className: 'alt-ppl-card-column-day__lbl', id: id + '-q' }, 'Entregas'),
        h('div', { className: 'alt-ppl-card-column-day__stp', role: 'group', 'aria-labelledby': id + '-q' },
          h('button', { type: 'button', 'aria-label': 'Diminuir entregas', disabled: ro || qty <= 0, onClick: function () { setQty(qty - 1); } }, ic('minus', 15)),
          h('output', { className: 'alt-num', 'aria-live': 'polite' }, qty),
          h('button', { type: 'button', 'aria-label': 'Aumentar entregas', disabled: ro || qty >= max, onClick: function () { setQty(qty + 1); } }, ic('plus', 15))),
        h('label', { className: 'alt-ppl-card-column-day__lbl', htmlFor: id + '-p' }, 'Peso (t)'),
        h('div', { className: cx('alt-ctl', 'alt-ctl--sm', 'alt-ppl-card-column-day__peso', manual && 'alt-ppl-card-column-day__peso--man', ro && 'alt-ctl--disabled') },
          h('input', { id: id + '-p', className: 'alt-num', inputMode: 'decimal', value: pesoVal, placeholder: '—', readOnly: !manual || ro,
            onChange: function (e) { pesoS[1](e.target.value); p.onChangePeso && p.onChangePeso(num(e.target.value), idx); } }),
          ro || qty === 0 ? null : h('button', { type: 'button', className: 'alt-ppl-card-column-day__ib alt-ppl-card-column-day__ib--sm', onClick: toggleManual,
            title: manual ? 'Peso informado manualmente — voltar ao calculado' : 'Peso calculado — informar manualmente',
            'aria-label': manual ? 'Usar peso calculado' : 'Informar peso manualmente' },
            manual ? h('svg', { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true' }, h('path', { d: UNLOCK })) : ic('lock', 14))),
        p.pesoAnterior != null && manual ? h('span', { className: 'alt-ppl-card-column-day__help alt-num' }, 'Anterior ' + nf(p.pesoAnterior, 1) + ' t') : null));
  }
  A.PplCardColumnDay = PplCardColumnDay;
})();


/* ---- PplCenarios ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 16 }); }

  var DEFAULT = [
    { id: 'c1', nome: 'Granéis · semana', favorito: true },
    { id: 'c2', nome: 'Vidros · atrasadas', favorito: false },
    { id: 'c3', nome: 'Sem veículo', favorito: false },
    { id: 'c4', nome: 'Analista Ana R. · 3 dias', favorito: true }
  ];

  function PplCenarios(p) {
    var C = 'alt-ppl-cenarios';
    var listS = R.useState(p.cenarios || DEFAULT), list = listS[0], setList = listS[1];
    var actS = R.useState(p.defaultAtivoId !== undefined ? p.defaultAtivoId : 'c1'), ativo = actS[0], setAtivo = actS[1];
    var dirtyS = R.useState(p.dirty != null ? p.dirty : true), dirty = dirtyS[0], setDirty = dirtyS[1];
    var pendS = R.useState(p.defaultPendingId || null), pending = pendS[0], setPending = pendS[1];
    var dragS = R.useState(p.defaultDragId || null), dragId = dragS[0], setDragId = dragS[1];
    var overS = R.useState(null), over = overS[0], setOver = overS[1];
    var editS = R.useState(null), editId = editS[0], setEditId = editS[1];
    var draftS = R.useState(''), draft = draftS[0], setDraft = draftS[1];
    var confDelS = R.useState(false), confDel = confDelS[0], setConfDel = confDelS[1];

    function commit(nl) { setList(nl); p.onChange && p.onChange(nl); }
    function activate(id) {
      if (id === ativo) return;
      if (dirty) { setPending(id); setConfDel(false); return; }
      setAtivo(id); p.onAtivar && p.onAtivar(id);
    }
    function confirmSwitch() { setAtivo(pending); setDirty(false); p.onAtivar && p.onAtivar(pending); setPending(null); }
    function toggleFav(id) { commit(list.map(function (c) { return c.id === id ? Object.assign({}, c, { favorito: !c.favorito }) : c; })); }
    function move(from, to) {
      if (from === to || from < 0 || to < 0 || to >= list.length) return;
      var nl = list.slice(); var it = nl.splice(from, 1)[0]; nl.splice(to, 0, it); commit(nl);
    }
    function idx(id) { for (var i = 0; i < list.length; i++) if (list[i].id === id) return i; return -1; }
    function startEdit(c) { setEditId(c.id); setDraft(c.nome); }
    function finishEdit(ok) {
      if (ok && draft.trim()) {
        commit(list.map(function (c) { return c.id === editId ? Object.assign({}, c, { nome: draft.trim() }) : c; }));
        p.onRenomear && p.onRenomear(editId, draft.trim());
      }
      setEditId(null);
    }
    function salvarNovo() {
      var id = 'n' + Date.now(); var nome = 'Novo cenário ' + (list.length + 1);
      commit(list.concat([{ id: id, nome: nome, favorito: false }]));
      setAtivo(id); setDirty(false); setEditId(id); setDraft(nome);
      p.onSalvarNovo && p.onSalvarNovo(nome);
    }
    function excluir() {
      commit(list.filter(function (c) { return c.id !== ativo; }));
      p.onExcluir && p.onExcluir(ativo); setAtivo(null); setConfDel(false);
    }
    var ativoObj = list[idx(ativo)];

    var rows = list.map(function (c, i) {
      var on = c.id === ativo, editing = editId === c.id;
      return h('li', {
        key: c.id, className: cx(C + '__row', on && C + '__row--on', dragId === c.id && C + '__row--drag', over === c.id && dragId !== c.id && C + '__row--over'),
        draggable: !editing,
        onDragStart: function (e) { setDragId(c.id); try { e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', c.id); } catch (x) {} },
        onDragOver: function (e) { e.preventDefault(); if (over !== c.id) setOver(c.id); },
        onDrop: function (e) { e.preventDefault(); move(idx(dragId), i); setDragId(null); setOver(null); },
        onDragEnd: function () { setDragId(null); setOver(null); }
      },
        h('button', { type: 'button', className: C + '__grip', 'aria-label': 'Reordenar ' + c.nome + ' (Alt + setas)', title: 'Arraste para reordenar',
          onKeyDown: function (e) {
            if (e.altKey && e.key === 'ArrowUp') { e.preventDefault(); move(i, i - 1); }
            if (e.altKey && e.key === 'ArrowDown') { e.preventDefault(); move(i, i + 1); }
          } }, ic('drag', 16)),
        h('button', { type: 'button', className: cx(C + '__star', c.favorito && C + '__star--on'), 'aria-pressed': c.favorito ? 'true' : 'false', 'aria-label': c.favorito ? 'Remover dos favoritos' : 'Marcar como favorito', onClick: function () { toggleFav(c.id); } },
          h(A.Icon, { name: 'star', size: 16, style: c.favorito ? { fill: 'currentColor' } : null })),
        editing ? h('input', { className: C + '__edit', autoFocus: true, value: draft, 'aria-label': 'Nome do cenário',
          onChange: function (e) { setDraft(e.target.value); },
          onKeyDown: function (e) { if (e.key === 'Enter') finishEdit(true); if (e.key === 'Escape') finishEdit(false); },
          onBlur: function () { finishEdit(true); } })
          : h('button', { type: 'button', className: C + '__name', onClick: function () { activate(c.id); }, onDoubleClick: function () { startEdit(c); }, title: 'Clique para ativar · duplo clique para renomear' }, c.nome),
        on && !editing ? h('span', { className: 'alt-chip ' + C + '__chip' }, ic('check', 12), 'Ativo') : null,
        editing ? null : h('button', { type: 'button', className: C + '__ib', 'aria-label': 'Renomear ' + c.nome, title: 'Renomear', onClick: function () { startEdit(c); } }, ic('edit', 15)));
    });

    var pendObj = pending ? list[idx(pending)] : null;
    return h('section', { className: cx(C, 'alt-glass', 'alt-glass--overlay', p.className), 'aria-label': 'Cenários' },
      h('header', { className: C + '__hd' },
        h('div', { className: C + '__hgroup' },
          h('span', { className: C + '__eyebrow' }, p.contexto || 'Painel do motorista'),
          h('h2', { className: C + '__title' }, p.titulo || 'Cenários')),
        h('span', { className: C + '__count alt-num' }, list.length + ' salvos'),
        p.onClose !== null ? h('button', { type: 'button', className: C + '__ib', 'aria-label': 'Fechar', onClick: p.onClose }, ic('x', 18)) : null),
      list.length ? h('ul', { className: C + '__list' }, rows) : h('p', { className: C + '__empty' }, 'Nenhum cenário salvo. Ajuste os filtros e use "Salvar como novo".'),
      h('p', { className: C + '__hint' }, 'Arraste pela alça para ordenar. Duplo clique no nome para renomear (Enter confirma, Esc cancela).'),
      pendObj ? h('div', { className: C + '__confirm', role: 'alertdialog', 'aria-live': 'polite' },
        h('span', { className: C + '__confirm-ic' }, ic('alert', 16)),
        h('span', { className: C + '__confirm-tx' }, 'Alterações não salvas no cenário atual. Trocar para ', h('b', null, pendObj.nome), '?'),
        h('div', { className: C + '__confirm-act' },
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { setPending(null); } }, 'Cancelar'),
          h('button', { type: 'button', className: 'alt-tbtn ' + C + '__warnbtn', onClick: confirmSwitch }, 'Continuar sem salvar'))) : null,
      confDel && ativoObj ? h('div', { className: cx(C + '__confirm', C + '__confirm--err'), role: 'alertdialog' },
        h('span', { className: C + '__confirm-ic' }, ic('trash', 16)),
        h('span', { className: C + '__confirm-tx' }, 'Excluir ', h('b', null, ativoObj.nome), '? Esta ação não pode ser desfeita.'),
        h('div', { className: C + '__confirm-act' },
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { setConfDel(false); } }, 'Cancelar'),
          h('button', { type: 'button', className: 'alt-tbtn ' + C + '__errbtn', onClick: excluir }, 'Excluir'))) : null,
      h('footer', { className: C + '__ft' },
        h('button', { type: 'button', className: cx('alt-tbtn alt-tbtn--ghost', C + '__del'), disabled: !ativoObj, onClick: function () { setPending(null); setConfDel(true); } }, ic('trash', 15), 'Excluir'),
        h('span', { className: C + '__sp' }),
        h('button', { type: 'button', className: 'alt-tbtn', onClick: salvarNovo }, 'Salvar como novo'),
        h(A.Button, { size: 'sm', arrow: false, className: C + '__primary', disabled: !ativoObj || !dirty, onClick: function () { setDirty(false); p.onSalvarAtivo && p.onSalvarAtivo(ativo); } }, 'Salvar no ativo')));
  }
  A.PplCenarios = PplCenarios;
})();


/* ---- PplDiario ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 16 }); }

  /* KPIs do histórico; `meta` = limite de alerta, `maior` = maior é melhor */
  var KPIS = [
    { id: 'prod', label: 'Prod.', nome: 'Produtividade', meta: 50, maior: true },
    { id: 'indisp', label: 'Indisp.', nome: 'Indisponibilidade', meta: 20, maior: false },
    { id: 'desloc', label: 'Desloc.', nome: 'Deslocamento', meta: 35, maior: false },
    { id: 'manut', label: 'Manut.', nome: 'Manutenção', meta: 15, maior: false },
    { id: 'mdfe', label: 'MDFe', nome: 'MDF-e ativo', meta: 60, maior: true }
  ];
  var DIAS = [
    { id: '2025-09-29', label: 'Seg 29/09', v: { prod: 72.4, indisp: 6.1, desloc: 18.2, manut: 0, mdfe: 81.3 } },
    { id: '2025-09-28', label: 'Dom 28/09', v: { prod: 31.0, indisp: 24.5, desloc: 12.7, manut: 8.3, mdfe: 40.2 } },
    { id: '2025-09-27', label: 'Sáb 27/09', v: { prod: 64.8, indisp: 4.0, desloc: 22.9, manut: 0, mdfe: 70.0 } },
    { id: '2025-09-26', label: 'Sex 26/09', v: { prod: 58.3, indisp: 12.2, desloc: 41.6, manut: 4.1, mdfe: 66.4 } },
    { id: '2025-09-25', label: 'Qui 25/09', v: { prod: 47.9, indisp: 18.8, desloc: 30.3, manut: 21.0, mdfe: 52.7 } },
    { id: '2025-09-24', label: 'Qua 24/09', v: { prod: 62.2, indisp: 9.5, desloc: 26.1, manut: 2.5, mdfe: 74.8 } }
  ];
  var ORDENS = [
    { id: 'dia-desc', label: 'Dia ↓' }, { id: 'dia-asc', label: 'Dia ↑' },
    { id: 'val-desc', label: 'Maior valor' }, { id: 'val-asc', label: 'Menor valor' }
  ];

  function Sel(p) {
    return h('label', { className: 'alt-ctl alt-ctl--sm alt-ppl-diario__sel' },
      h('span', { className: 'alt-ppl-diario__sr' }, p.label),
      h('select', { value: p.value, onChange: function (e) { p.onChange(e.target.value); } },
        p.options.map(function (o) { return h('option', { key: o.id, value: o.id }, o.label); })),
      ic('chevron-down', 14));
  }

  function PplDiario(p) {
    var C = 'alt-ppl-diario';
    var kpis = p.kpis || KPIS, dias = p.dias || DIAS;
    var kS = R.useState(p.defaultKpi || kpis[0].id), kpiId = kS[0];
    var oS = R.useState(p.defaultOrdem || 'dia-desc'), ordem = oS[0];
    var sS = R.useState(p.defaultSelecionado !== undefined ? p.defaultSelecionado : dias[1] && dias[1].id), sel = sS[0];
    var kpi = kpis.filter(function (k) { return k.id === kpiId; })[0] || kpis[0];

    var rows = dias.slice();
    if (ordem === 'dia-asc') rows.reverse();
    if (ordem === 'val-desc') rows.sort(function (a, b) { return (b.v[kpi.id] || 0) - (a.v[kpi.id] || 0); });
    if (ordem === 'val-asc') rows.sort(function (a, b) { return (a.v[kpi.id] || 0) - (b.v[kpi.id] || 0); });
    var media = dias.length ? dias.reduce(function (s, d) { return s + (d.v[kpi.id] || 0); }, 0) / dias.length : 0;
    function alerta(v) { return kpi.maior ? v < kpi.meta : v > kpi.meta; }

    return h('section', { className: cx(C, 'alt-glass', p.className), 'aria-label': p.titulo || 'Diário' },
      h('header', { className: C + '__hd' },
        h('h3', { className: C + '__title' }, p.titulo || 'Diário'),
        h(Sel, { label: 'Ordem', value: ordem, options: ORDENS, onChange: function (v) { oS[1](v); p.onOrdemChange && p.onOrdemChange(v); } }),
        h(Sel, { label: 'KPI', value: kpiId, options: kpis, onChange: function (v) { kS[1](v); p.onKpiChange && p.onKpiChange(v); } })),
      h('div', { className: C + '__meta' }, kpi.nome, ' · meta ', kpi.maior ? '≥ ' : '≤ ', h('span', { className: 'alt-num' }, nf(kpi.meta) + '%')),
      h('div', { className: C + '__list', role: 'listbox', 'aria-label': 'Dias do período' },
        rows.map(function (d) {
          var v = d.v[kpi.id] || 0, on = d.id === sel, al = alerta(v);
          return h('button', { key: d.id, type: 'button', role: 'option', 'aria-selected': on ? 'true' : 'false', className: cx(C + '__it', on && C + '__it--on', al && C + '__it--al'),
            onClick: function () { sS[1](d.id); p.onSelect && p.onSelect(d.id); } },
            h('span', { className: C + '__day alt-num' }, d.label),
            h('span', { className: C + '__bar', 'aria-hidden': 'true' }, h('i', { style: { width: Math.max(0, Math.min(100, v)) + '%' } }),
              h('s', { style: { left: kpi.meta + '%' } })),
            h('span', { className: C + '__v alt-num' }, al ? h('span', { className: C + '__al', title: 'Fora da meta' }, ic('alert', 13)) : null, nf(v, 1) + '%'));
        })),
      h('footer', { className: C + '__ft' },
        h('span', null, 'Média do período'),
        h('b', { className: 'alt-num' }, nf(media, 1) + '%')));
  }
  A.PplDiario = PplDiario;
})();


/* ---- PplDocumentos ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 16 }); }
  var CLIP = 'M9 3h6v4H9zM9 5H6v16h12V5h-3';
  function svg(d, s) {
    return h('svg', { width: s || 16, height: s || 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      d.split(/(?=M)/).map(function (x, i) { return h('path', { key: i, d: x }); }));
  }

  var MAX_FILES = 10, MAX_MB = 10;
  var ACCEPT = 'image/*,.pdf,.xls,.xlsx,.ods';
  function kind(name) {
    var e = String(name).split('.').pop().toLowerCase();
    if (e === 'pdf') return { tag: 'PDF', c: 'var(--chart-5)' };
    if (e === 'xls' || e === 'xlsx' || e === 'ods') return { tag: 'XLS', c: 'var(--chart-1)' };
    if (/^(png|jpe?g|gif|webp|heic|bmp)$/.test(e)) return { tag: 'IMG', c: 'var(--chart-2)' };
    return { tag: e.slice(0, 3).toUpperCase(), c: 'var(--chart-6)' };
  }
  function okType(f) { return /^image\//.test(f.type) || /\.(pdf|xls|xlsx|ods|png|jpe?g|gif|webp|heic|bmp)$/i.test(f.name); }
  function size(b) { return b >= 1048576 ? nf(b / 1048576, 1) + ' MB' : nf(Math.max(1, Math.round(b / 1024))) + ' KB'; }

  var DEFAULT = [
    { id: 'd1', name: 'canhoto-88213.pdf', size: 421888 },
    { id: 'd2', name: 'pesagem-30-09.xlsx', size: 38912 }
  ];

  function PplDocumentos(p) {
    var C = 'alt-ppl-documentos';
    var fS = R.useState(p.arquivos || DEFAULT), files = fS[0], setFiles = fS[1];
    var dS = R.useState(false), drag = p.dragActive || dS[0], setDrag = dS[1];
    var eS = R.useState(null), err = eS[0], setErr = eS[1];
    var inp = R.useRef(null);
    var canEdit = p.canEdit !== false;
    var evento = p.evento || 'descarga';
    var obrigatorio = p.possuiComprovanteMotivo && files.length === 0;
    var max = p.maxArquivos || MAX_FILES;

    R.useEffect(function () { p.onCountChange && p.onCountChange(files.length); p.onObrigatorioChange && p.onObrigatorioChange(!!obrigatorio); }, [files.length]);

    function add(list) {
      var arr = Array.prototype.slice.call(list || []), out = files.slice(), rej = [];
      arr.forEach(function (f) {
        if (out.length >= max) { rej.push(f.name + ': limite de ' + max + ' arquivos'); return; }
        if (!okType(f)) { rej.push(f.name + ': tipo não aceito'); return; }
        if (f.size > MAX_MB * 1048576) { rej.push(f.name + ': maior que ' + MAX_MB + ' MB'); return; }
        out.push({ id: 'f' + Date.now() + Math.random(), name: f.name || 'colado.png', size: f.size, file: f });
      });
      setErr(rej.length ? rej.join(' · ') : null);
      if (out.length !== files.length) { setFiles(out); p.onChange && p.onChange(out); }
    }
    function remove(id) { var out = files.filter(function (f) { return f.id !== id; }); setFiles(out); p.onChange && p.onChange(out); }
    function onPaste(e) {
      if (!canEdit) return;
      var items = e.clipboardData && e.clipboardData.files;
      if (items && items.length) { e.preventDefault(); add(items); }
    }
    function colar() {
      if (!navigator.clipboard || !navigator.clipboard.read) { setErr('Use Ctrl+V (⌘+V) com a área selecionada para colar.'); return; }
      navigator.clipboard.read().then(function (items) {
        var ps = [];
        items.forEach(function (it) { it.types.forEach(function (t) { if (/^image\//.test(t)) ps.push(it.getType(t).then(function (b) { return new File([b], 'colado-' + Date.now() + '.png', { type: t }); })); }); });
        return Promise.all(ps);
      }).then(function (fs) { if (fs && fs.length) add(fs); else setErr('Nada para colar: copie uma imagem primeiro.'); })
        ['catch'](function () { setErr('Sem permissão para ler a área de transferência. Use Ctrl+V.'); });
    }

    var compact = !!p.compact;
    return h('section', { className: cx(C, 'alt-glass', compact && C + '--compact', p.className), 'aria-label': p.titulo || 'Documentos comprovantes', onPaste: onPaste },
      h('header', { className: C + '__hd' },
        h('h3', { className: C + '__title' }, p.titulo || 'Documentos comprovantes'),
        h('span', { className: C + '__count alt-num' }, files.length + '/' + max)),
      h('div', { className: C + '__bd' },
        obrigatorio ? h('div', { className: C + '__ob', role: 'status' }, ic('alert', 15), 'É necessário anexar um comprovante de ' + evento + '.') : null,
        canEdit ? h('div', {
          className: cx(C + '__dz', drag && C + '__dz--on'), tabIndex: 0, role: 'group', 'aria-label': 'Área para soltar ou colar arquivos',
          onDragEnter: function (e) { e.preventDefault(); setDrag(true); },
          onDragOver: function (e) { e.preventDefault(); },
          onDragLeave: function (e) { if (!e.currentTarget.contains(e.relatedTarget)) setDrag(false); },
          onDrop: function (e) { e.preventDefault(); setDrag(false); add(e.dataTransfer.files); }
        },
          h('span', { className: C + '__dz-ic' }, ic('upload', 20)),
          h('span', { className: C + '__dz-tx' }, drag ? 'Solte para anexar' : 'Solte arquivos aqui, cole com Ctrl+V ou'),
          h('div', { className: C + '__dz-act' },
            h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: colar }, svg(CLIP, 15), 'Colar'),
            h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { inp.current && inp.current.click(); } }, ic('paperclip', 15), 'Anexar')),
          compact ? null : h('span', { className: C + '__rules' }, 'Até ' + max + ' arquivos de ' + MAX_MB + ' MB · imagem, PDF, xls/xlsx/ods'),
          h('input', { ref: inp, type: 'file', multiple: true, accept: ACCEPT, hidden: true, onChange: function (e) { add(e.target.files); e.target.value = ''; } })) : null,
        err ? h('div', { className: 'alt-field__error ' + C + '__err', role: 'alert' }, ic('x-circle', 14), err) : null,
        files.length ? h('ul', { className: C + '__list alt-num' }, files.map(function (f) {
          var k = kind(f.name);
          return h('li', { key: f.id, className: C + '__fi' },
            h('span', { className: C + '__th', style: { '--c': k.c }, 'aria-hidden': 'true' }, k.tag),
            h('span', { className: C + '__n', title: f.name }, f.name),
            h('span', { className: C + '__sz' }, size(f.size)),
            canEdit ? h('button', { type: 'button', className: C + '__rm', 'aria-label': 'Remover ' + f.name, title: 'Remover', onClick: function () { remove(f.id); } }, ic('x', 16)) : null);
        })) : (canEdit ? null : h('p', { className: C + '__empty' }, 'Nenhum documento anexado.')),
        canEdit && !files.length ? h('p', { className: C + '__empty' }, 'Nenhum documento anexado.') : null));
  }
  A.PplDocumentos = PplDocumentos;
})();


/* ---- PplEventCard ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 16 }); }

  var STATUS = {
    pendente: { label: 'Pendente', t: 'info', icon: 'clock' },
    andamento: { label: 'Em andamento', t: 'success', icon: 'play' },
    finalizada: { label: 'Finalizada', t: 'success', icon: 'check', outline: true },
    cancelada: { label: 'Cancelada', t: 'neutral', icon: 'x' }
  };

  var DEFAULT_EVENT = {
    id: '88213', status: 'andamento',
    campos: [
      { label: 'Motorista', valor: 'João Pereira' },
      { label: 'Placas', valor: 'ABC-1D23 · IMP-0A11' },
      { label: 'Rota', valor: 'Jundiaí → Sorocaba' },
      { label: 'Cliente', valor: 'Vidroporto S.A.' },
      { label: 'Janela de carga', valor: '30/09 08:00–09:30' },
      { label: 'Janela de descarga', valor: '30/09 15:00–17:00' },
      { label: 'Peso programado', valor: '27,0 t' },
      { label: 'Início real', valor: '30/09 10:45 (+45 min)' }
    ],
    checklist: [
      { id: 'k1', label: 'Lacre conferido', ok: true },
      { id: 'k2', label: 'Nota fiscal anexada', ok: true },
      { id: 'k3', label: 'Pesagem de saída registrada', ok: true },
      { id: 'k4', label: 'Canhoto assinado', ok: false },
      { id: 'k5', label: 'Foto da descarga', ok: false }
    ]
  };

  function PplEventCard(p) {
    var C = 'alt-ppl-event-card';
    var ev = p.event || DEFAULT_EVENT;
    var st = STATUS[ev.status] || STATUS.pendente, tn = K.tone(st.t);
    var clS = R.useState(ev.checklist || []), cl = clS[0], setCl = clS[1];
    var openS = R.useState(p.defaultChecklistOpen !== false), open = openS[0];
    var menuS = R.useState(!!p.defaultMenuOpen), menu = menuS[0], setMenu = menuS[1];
    var addS = R.useState(false), adding = addS[0], setAdding = addS[1];
    var draftS = R.useState(''), draft = draftS[0], setDraft = draftS[1];
    var posS = R.useState({ x: 0, y: 0 }), pos = posS[0], setPos = posS[1];
    var drag = R.useRef(null);
    var menuRef = R.useRef(null);

    R.useEffect(function () {
      if (!menu) return;
      function off(e) { if (menuRef.current && !menuRef.current.contains(e.target)) setMenu(false); }
      function esc(e) { if (e.key === 'Escape') setMenu(false); }
      document.addEventListener('mousedown', off); document.addEventListener('keydown', esc);
      return function () { document.removeEventListener('mousedown', off); document.removeEventListener('keydown', esc); };
    }, [menu]);

    if (p.isOpen === false) return null;

    function onDown(e) {
      if (e.button !== 0 || e.target.closest('button')) return;
      drag.current = { x: e.clientX - pos.x, y: e.clientY - pos.y };
      e.currentTarget.setPointerCapture && e.currentTarget.setPointerCapture(e.pointerId);
    }
    function onMove(e) { if (drag.current) setPos({ x: e.clientX - drag.current.x, y: e.clientY - drag.current.y }); }
    function onUp() { drag.current = null; }
    function toggle(id) {
      var nl = cl.map(function (k) { return k.id === id ? Object.assign({}, k, { ok: !k.ok }) : k; });
      setCl(nl); var it = nl.filter(function (k) { return k.id === id; })[0];
      p.onChecklistItemToggle && p.onChecklistItemToggle(id, it.ok);
    }
    function addItem() {
      if (draft.trim()) { var it = { id: 'n' + Date.now(), label: draft.trim(), ok: false }; setCl(cl.concat([it])); p.onChecklistItemAdd && p.onChecklistItemAdd(it); }
      setDraft(''); setAdding(false);
    }
    var done = cl.filter(function (k) { return k.ok; }).length;

    var main = ev.status === 'pendente' ? { label: 'Iniciar execução', fn: p.onIniciarExecucao }
      : ev.status === 'andamento' ? { label: 'Finalizar execução', fn: p.onFinalizarExecucao } : null;
    var acoes = p.acoes || [
      { id: 'transferir', label: 'Transferir entrega', icon: 'arrow-right', fn: p.onTransferirEntrega },
      { id: 'estornar', label: ev.status === 'finalizada' ? 'Estornar finalização' : 'Estornar início', icon: 'refresh', fn: p.onEstornarExecucao, hide: ev.status === 'pendente' || ev.status === 'cancelada' },
      { id: 'desvincular', label: 'Desvincular motorista', icon: 'link', fn: p.onDesvincular },
      { id: 'cancelar', label: 'Cancelar entrega', icon: 'x-circle', fn: p.onCancelarEntrega, danger: true, hide: ev.status === 'cancelada' }
    ];

    return h('section', { className: cx(C, 'alt-glass', 'alt-glass--overlay', p.className), role: 'dialog', 'aria-label': 'Entrega #' + ev.id,
      style: Object.assign({ transform: 'translate(' + pos.x + 'px,' + pos.y + 'px)' }, p.style) },
      h('header', { className: C + '__hd', onPointerDown: onDown, onPointerMove: onMove, onPointerUp: onUp },
        h('span', { className: C + '__grip', title: 'Arraste para mover', 'aria-hidden': 'true' }, ic('drag', 16)),
        h('h3', { className: C + '__title alt-num' }, (p.tituloPrefixo || 'Entrega #') + ev.id),
        h('span', { className: cx('alt-chip', C + '__st'), style: st.outline ? { color: tn.c, borderColor: tn.c, background: 'transparent' } : { color: tn.c, background: tn.bg } }, ic(st.icon, 12), st.label),
        h('span', { className: C + '__sp' }),
        h('div', { className: C + '__menuwrap', ref: menuRef },
          h('button', { type: 'button', className: C + '__ib', 'aria-label': 'Mais ações', 'aria-haspopup': 'menu', 'aria-expanded': menu ? 'true' : 'false', onClick: function () { setMenu(!menu); } }, ic('more', 18)),
          menu ? h('div', { className: 'alt-popover ' + C + '__menu', role: 'menu' }, acoes.filter(function (a) { return !a.hide; }).map(function (a) {
            return h('button', { key: a.id, type: 'button', role: 'menuitem', className: cx('alt-menu-item', a.danger && C + '__danger'), onClick: function () { setMenu(false); a.fn && a.fn(ev); } }, ic(a.icon, 16), a.label);
          })) : null),
        h('button', { type: 'button', className: C + '__ib', 'aria-label': 'Fechar', onClick: p.onClose }, ic('x', 18))),
      h('dl', { className: C + '__dg alt-num' }, (ev.campos || []).map(function (f, i) {
        return h('div', { key: i }, h('dt', null, f.label), h('dd', null, f.valor));
      })),
      h('div', { className: C + '__cl' },
        h('button', { type: 'button', className: C + '__clh', 'aria-expanded': open ? 'true' : 'false', onClick: function () { openS[1](!open); } },
          h('span', { className: cx(C + '__chev', open && C + '__chev--open') }, ic('chevron-right', 16)),
          'Checklist',
          h('span', { className: cx(C + '__cnt', 'alt-num', done === cl.length && cl.length && C + '__cnt--ok') }, done + '/' + cl.length)),
        open ? h('div', { className: C + '__items' },
          cl.map(function (k) {
            return h('label', { key: k.id, className: cx(C + '__ci', k.ok && C + '__ci--ok') },
              h('input', { type: 'checkbox', checked: k.ok, onChange: function () { toggle(k.id); } }),
              h('span', { className: C + '__cb', 'aria-hidden': 'true' }, k.ok ? h(A.Icon, { name: 'check', size: 12, strokeWidth: 3 }) : null),
              k.label);
          }),
          adding ? h('div', { className: 'alt-ctl alt-ctl--sm ' + C + '__add' },
            h('input', { autoFocus: true, value: draft, placeholder: 'Descreva o item e tecle Enter', 'aria-label': 'Novo item do checklist', onChange: function (e) { setDraft(e.target.value); },
              onKeyDown: function (e) { if (e.key === 'Enter') addItem(); if (e.key === 'Escape') { setDraft(''); setAdding(false); } }, onBlur: addItem }))
            : h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost ' + C + '__inc', onClick: function () { setAdding(true); } }, ic('plus', 15), 'Incluir item')) : null),
      h('footer', { className: C + '__ft' },
        h('button', { type: 'button', className: 'alt-tbtn ' + C + '__sec', onClick: function () { p.onTransferirData && p.onTransferirData(ev); } }, ic('calendar', 15), 'Transferir data'),
        main ? h(A.Button, { size: 'sm', arrow: false, className: C + '__primary', onClick: function () { main.fn && main.fn(ev); } }, main.label) : null));
  }
  A.PplEventCard = PplEventCard;
})();


/* ---- PplFiltrosPainel ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 16 }); }

  var MODOS = [
    { id: 'foco7', label: 'Dia em foco (7 dias)', dias: 7 },
    { id: 'foco3', label: 'Dia em foco (3 dias)', dias: 3 },
    { id: 'foco1', label: 'Dia em foco (1 dia)', dias: 1 },
    { id: 'semanal', label: 'Semanal', dias: 7 }
  ];
  var ZOOMS = [100, 105, 110, 115, 120, 125];
  var AGRUPAR = ['Nenhum', 'Analista', 'Gestor', 'Operação'];
  var DIAS = [
    { id: '27', label: '27', sem: 'S', fds: true }, { id: '28', label: '28', sem: 'D', fds: true },
    { id: '29', label: '29', sem: 'S' }, { id: '30', label: '30', sem: 'T', hoje: true },
    { id: '01', label: '01', sem: 'Q' }, { id: '02', label: '02', sem: 'Q' },
    { id: '03', label: '03', sem: 'S' }, { id: '04', label: '04', sem: 'S', fds: true },
    { id: '05', label: '05', sem: 'D', fds: true }
  ];
  var EVENTOS = ['Viagem', 'Manutenção', 'Treinamento', 'Folga', 'Janelas de carga/descarga'];
  var STATUS = ['Pendente', 'Em andamento', 'Finalizada', 'Cancelada'];
  var VINC = ['Sem veículo', 'Com veículo', 'Todos'];

  var DEFAULTS = { modo: 'foco3', foco: '30', zoom: 100, agrupar: 'Nenhum', eventos: ['Viagem', 'Manutenção', 'Treinamento', 'Folga'], vinculo: 'Todos', motoristas: ['João Pereira'], status: ['Pendente', 'Em andamento'], atrasadas: false };

  function Section(p) {
    var C = 'alt-ppl-filtros-painel';
    var id = 'alt-pfp-' + p.id;
    return h('section', { className: cx(C + '__sec', p.open && C + '__sec--open') },
      h('h3', { className: C + '__sech' },
        h('button', { type: 'button', 'aria-expanded': p.open ? 'true' : 'false', 'aria-controls': id, onClick: p.onToggle },
          h('span', { className: C + '__chev' }, ic('chevron-right', 16)), p.title,
          p.count ? h('span', { className: 'alt-count' }, p.count) : null)),
      p.open ? h('div', { id: id, className: C + '__secb' }, p.children) : null);
  }
  function Seg(p) {
    return h('div', { className: 'alt-ppl-filtros-painel__seg', role: 'radiogroup', 'aria-label': p.label },
      p.options.map(function (o) {
        var v = o.id != null ? o.id : o, l = o.label != null ? o.label : o;
        return h('button', { key: v, type: 'button', role: 'radio', 'aria-checked': p.value === v ? 'true' : 'false', onClick: function () { p.onChange(v); } }, l);
      }));
  }
  function Chips(p) {
    return h('div', { className: 'alt-ppl-filtros-painel__chips', role: 'group', 'aria-label': p.label },
      p.options.map(function (o) {
        var on = p.value.indexOf(o) >= 0;
        return h('button', { key: o, type: 'button', className: 'alt-ppl-filtros-painel__fchip', 'aria-pressed': on ? 'true' : 'false',
          onClick: function () { p.onChange(on ? p.value.filter(function (x) { return x !== o; }) : p.value.concat([o])); } },
          on ? ic('check', 13) : null, o);
      }));
  }
  function Field(p) {
    return h('div', { className: 'alt-field' }, h('span', { className: 'alt-field__label', id: p.id }, p.label), p.children);
  }

  function PplFiltrosPainel(p) {
    var C = 'alt-ppl-filtros-painel';
    var init = Object.assign({}, DEFAULTS, p.defaultValue);
    var vS = R.useState(init), v = vS[0], setV = vS[1];
    var oS = R.useState(p.defaultOpen || { geral: true, eventos: false, motoristas: true, entregas: false }), open = oS[0];
    var qS = R.useState(''), q = qS[0], setQ = qS[1];
    var dias = p.dias || DIAS;
    var modos = p.modos || MODOS;
    function set(k, val) { var nv = Object.assign({}, v); nv[k] = val; setV(nv); p.onChange && p.onChange(nv); }
    function tog(k) { var no = Object.assign({}, open); no[k] = !no[k]; oS[1](no); }
    var modo = modos.filter(function (m) { return m.id === v.modo; })[0] || modos[1];
    var fi = 0; for (var i = 0; i < dias.length; i++) if (dias[i].id === v.foco) fi = i;
    var half = Math.floor((modo.dias - 1) / 2), vis = {};
    for (var j = fi - half; j < fi - half + modo.dias; j++) if (dias[j]) vis[dias[j].id] = true;
    var motoristasOpts = p.motoristas || ['João Pereira', 'Marta Souza', 'Carlos Lima', 'Renata Alves', 'Paulo Nunes'];
    var sug = q ? motoristasOpts.filter(function (m) { return m.toLowerCase().indexOf(q.toLowerCase()) >= 0 && v.motoristas.indexOf(m) < 0; }) : [];
    function countOf(keys) { var n = 0; keys.forEach(function (k) { if (JSON.stringify(v[k]) !== JSON.stringify(DEFAULTS[k])) n++; }); return n; }

    return h('aside', { className: cx(C, 'alt-glass', 'alt-glass--overlay', p.className), 'aria-label': 'Filtros e visualização' },
      h('header', { className: C + '__hd' },
        h('div', { className: C + '__hg' }, h('span', { className: C + '__eyebrow' }, p.contexto || 'Painel do motorista'), h('h2', { className: C + '__title' }, p.titulo || 'Filtros e visualização')),
        h('button', { type: 'button', className: C + '__ib', 'aria-label': 'Fechar', onClick: p.onClose }, ic('x', 18))),
      h('div', { className: C + '__body alt-scroll' },
        h(Section, { id: 'geral', title: 'Geral', open: open.geral, onToggle: function () { tog('geral'); }, count: countOf(['modo', 'foco', 'zoom', 'agrupar']) },
          h(Field, { label: 'Modo de visualização' },
            h('label', { className: 'alt-ctl alt-ctl--sm ' + C + '__sel' },
              h('select', { value: v.modo, 'aria-label': 'Modo de visualização', onChange: function (e) { set('modo', e.target.value); } }, modos.map(function (m) { return h('option', { key: m.id, value: m.id }, m.label); })),
              ic('chevron-down', 15))),
          h(Field, { label: 'Dia em foco' },
            h('div', { className: C + '__days alt-num', role: 'radiogroup', 'aria-label': 'Dia em foco' }, dias.map(function (d) {
              return h('button', { key: d.id, type: 'button', role: 'radio', 'aria-checked': d.id === v.foco ? 'true' : 'false', title: d.hoje ? 'Hoje' : undefined,
                className: cx(C + '__day', vis[d.id] && C + '__day--vis', d.hoje && C + '__day--hoje', d.fds && C + '__day--fds'), onClick: function () { set('foco', d.id); } },
                h('small', null, d.sem), d.label, d.hoje ? h('i', { 'aria-hidden': 'true' }) : null);
            })),
            h('span', { className: 'alt-field__help' }, 'Ponto = hoje · faixa clara = dias visíveis')),
          h(Field, { label: 'Zoom' }, h(Seg, { label: 'Zoom', value: v.zoom, options: ZOOMS.map(function (z) { return { id: z, label: z + '%' }; }), onChange: function (z) { set('zoom', z); } })),
          h(Field, { label: 'Agrupar por' }, h(Seg, { label: 'Agrupar por', value: v.agrupar, options: AGRUPAR, onChange: function (z) { set('agrupar', z); } }))),
        h(Section, { id: 'eventos', title: 'Eventos', open: open.eventos, onToggle: function () { tog('eventos'); }, count: countOf(['eventos']) },
          h(Chips, { label: 'Tipos de evento', options: p.tiposEvento || EVENTOS, value: v.eventos, onChange: function (x) { set('eventos', x); } })),
        h(Section, { id: 'motoristas', title: 'Motoristas', open: open.motoristas, onToggle: function () { tog('motoristas'); }, count: countOf(['vinculo', 'motoristas']) },
          h(Field, { label: 'Vínculo com veículo' }, h(Seg, { label: 'Vínculo com veículo', value: v.vinculo, options: VINC, onChange: function (x) { set('vinculo', x); } })),
          h(Field, { label: 'Motoristas' },
            h('div', { className: C + '__ac' },
              h('div', { className: 'alt-ctl alt-ctl--sm ' + C + '__acbox' },
                v.motoristas.map(function (m) { return h('span', { key: m, className: 'alt-chip ' + C + '__tag' }, m, h('button', { type: 'button', 'aria-label': 'Remover ' + m, onClick: function () { set('motoristas', v.motoristas.filter(function (x) { return x !== m; })); } }, ic('x', 12))); }),
                h('input', { value: q, placeholder: v.motoristas.length ? 'Adicionar…' : 'Buscar motorista', 'aria-label': 'Buscar motorista', onChange: function (e) { setQ(e.target.value); },
                  onKeyDown: function (e) { if (e.key === 'Enter' && sug[0]) { set('motoristas', v.motoristas.concat([sug[0]])); setQ(''); } } })),
              sug.length ? h('div', { className: 'alt-popover ' + C + '__menu', role: 'listbox' }, sug.map(function (m) {
                return h('button', { key: m, type: 'button', role: 'option', className: 'alt-menu-item', onClick: function () { set('motoristas', v.motoristas.concat([m])); setQ(''); } }, ic('user', 15), m);
              })) : null))),
        h(Section, { id: 'entregas', title: 'Entregas', open: open.entregas, onToggle: function () { tog('entregas'); }, count: countOf(['status', 'atrasadas']) },
          h(Field, { label: 'Status' }, h(Chips, { label: 'Status da entrega', options: STATUS, value: v.status, onChange: function (x) { set('status', x); } })),
          h('label', { className: C + '__sw' },
            h('input', { type: 'checkbox', role: 'switch', checked: v.atrasadas, onChange: function (e) { set('atrasadas', e.target.checked); } }),
            h('span', { className: C + '__swt', 'aria-hidden': 'true' }), 'Somente viagens atrasadas'))),
      h('footer', { className: C + '__ft' },
        h('button', { type: 'button', className: 'alt-tbtn ' + C + '__pill', onClick: function () { setV(DEFAULTS); p.onLimpar && p.onLimpar(); } }, 'Limpar'),
        h(A.Button, { size: 'sm', arrow: false, className: C + '__primary', onClick: function () { p.onAplicar && p.onAplicar(v); } }, 'Aplicar filtros')));
  }
  A.PplFiltrosPainel = PplFiltrosPainel;
})();


/* ---- PplFrotaTimeline ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function svg(d, s) {
    return h('svg', { width: s || 16, height: s || 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      d.split(/(?=M)/).map(function (x, i) { return h('path', { key: i, d: x }); }));
  }
  function ic(n, s) { return K.icons[n] ? h(A.Icon, { name: n, size: s || 16 }) : svg(n, s); }
  var ZIN = 'M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14zM20 20l-3.5-3.5M11 8v6M8 11h6';
  var ZOUT = 'M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14zM20 20l-3.5-3.5M8 11h6';
  var BAN = 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM5.6 5.6l12.8 12.8';

  /* estado → cor + palavra (nunca só cor) */
  var ST = {
    ok: { c: 'var(--signal-success)', w: 'OK' },
    warn: { c: 'var(--signal-warning)', w: 'Atenção' },
    err: { c: 'var(--signal-error)', w: 'Ocorrência' },
    off: { c: 'var(--chart-6)', w: 'Sem atividade' }
  };
  function hm(x) { var hh = Math.floor(x), mm = Math.round((x - hh) * 60); if (mm === 60) { hh++; mm = 0; } return (hh < 10 ? '0' : '') + hh + ':' + (mm < 10 ? '0' : '') + mm; }

  var TRILHAS = [
    { id: 'posicao', label: 'Posição', icon: 'pin', clicavel: true, seg: [
      { de: 0, ate: 5.5, st: 'off', txt: 'Sem posição' }, { de: 5.5, ate: 17.5, st: 'ok', txt: 'Com sinal' }, { de: 17.5, ate: 18.25, st: 'err', txt: 'Perda de sinal' }, { de: 18.25, ate: 23, st: 'ok', txt: 'Com sinal' }] },
    { id: 'movimento', label: 'Movimentação', icon: 'gauge', seg: [
      { de: 5.5, ate: 6.25, st: 'warn', txt: 'Ligado parado' }, { de: 6.25, ate: 11, st: 'ok', txt: 'Em movimento' }, { de: 11, ate: 12.5, st: 'off', txt: 'Desligado' }, { de: 12.5, ate: 13.25, st: 'warn', txt: 'Ligado parado' }, { de: 13.25, ate: 17.5, st: 'ok', txt: 'Em movimento' }, { de: 18.25, ate: 21.75, st: 'ok', txt: 'Em movimento' }] },
    { id: 'mdfe', label: 'MDF-e ativo', icon: 'file', seg: [{ de: 5.75, ate: 19.25, st: 'ok', txt: 'MDF-e 35250912… ativo' }] },
    { id: 'manutencao', label: 'Em manutenção', icon: 'tool', seg: [{ de: 21.75, ate: 23.5, st: 'err', txt: 'Manutenção corretiva' }] },
    { id: 'indisp', label: 'Indisponibilidade', icon: BAN, seg: [{ de: 0, ate: 5.5, st: 'warn', txt: 'Sem motorista' }, { de: 21.75, ate: 24, st: 'warn', txt: 'Oficina' }] },
    { id: 'prod', label: 'Produtividade', icon: 'trend-up', seg: [{ de: 6.25, ate: 11, st: 'ok', txt: 'Produtivo' }, { de: 11, ate: 13.25, st: 'warn', txt: 'Improdutivo' }, { de: 13.25, ate: 17.5, st: 'ok', txt: 'Produtivo' }, { de: 18.25, ate: 21.75, st: 'ok', txt: 'Produtivo' }] },
    { id: 'macros', label: 'Macros', icon: 'message', alto: true, seg: [
      { de: 5.25, ate: 5.75, c: 'var(--chart-1)', txt: 'Início de jornada' }, { de: 5.75, ate: 6.25, c: 'var(--chart-2)', txt: 'Carregamento' },
      { de: 11, ate: 12.5, c: 'var(--chart-3)', txt: 'Refeição' }, { de: 15.5, ate: 16.5, c: 'var(--chart-5)', txt: 'Descarga' },
      { de: 17.5, ate: 18.25, c: 'var(--chart-4)', txt: 'Espera' }, { de: 21.75, ate: 22.25, c: 'var(--chart-1)', txt: 'Fim de jornada' }] }
  ];
  var MACRO_LEG = [['var(--chart-1)', 'Jornada'], ['var(--chart-2)', 'Carregamento'], ['var(--chart-3)', 'Refeição'], ['var(--chart-5)', 'Descarga'], ['var(--chart-4)', 'Espera']];

  function PplFrotaTimeline(p) {
    var C = 'alt-ppl-frota-timeline';
    var zS = R.useState(p.zoom != null ? p.zoom : (p.defaultZoom || 100)), zoom = p.zoom != null ? p.zoom : zS[0];
    var hS = R.useState(null), hover = hS[0], setHover = hS[1];
    var trilhas = p.trilhas || TRILHAS;
    var min = 100, max = 300, step = 50;
    function setZoom(z) { z = Math.max(min, Math.min(max, z)); zS[1](z); p.onZoomChange && p.onZoomChange(z); }
    var hours = []; for (var i = 0; i <= 24; i++) hours.push(i);
    var labelEvery = zoom >= 200 ? 1 : zoom >= 150 ? 2 : 3;

    function bar(t) {
      return h('div', { className: cx(C + '__track', t.alto && C + '__track--alto'), role: 'list', 'aria-label': t.label },
        t.seg.map(function (s, j) {
          var st = s.st ? ST[s.st] : null;
          var tip = t.label + ' · ' + (s.txt || (st && st.w)) + ' · ' + hm(s.de) + '–' + hm(s.ate);
          var style = { left: (s.de / 24 * 100) + '%', width: ((s.ate - s.de) / 24 * 100) + '%', '--c': s.c || st.c };
          var props = { key: j, role: 'listitem', className: cx(C + '__seg', s.st === 'off' && C + '__seg--off'), style: style, title: tip, 'aria-label': tip,
            onMouseEnter: function () { setHover(tip); }, onMouseLeave: function () { setHover(null); } };
          if (t.clicavel && p.onPosicaoClick !== null) {
            return h('button', Object.assign(props, { type: 'button', onClick: function () { p.onPosicaoClick && p.onPosicaoClick(s, t.id); }, onFocus: function () { setHover(tip + ' · abrir no mapa'); }, onBlur: function () { setHover(null); } }),
              t.alto && (s.ate - s.de) * zoom / 100 > 1.2 ? h('span', null, s.txt) : null);
          }
          return h('span', props, t.alto && (s.ate - s.de) * zoom / 100 > 1.2 ? h('span', null, s.txt) : null);
        }));
    }

    return h('section', { className: cx(C, 'alt-panel', p.className), 'aria-label': p.titulo || 'Linha do tempo' },
      h('header', { className: C + '__hd' },
        h('div', { className: C + '__hg' },
          h('h3', { className: C + '__title' }, p.titulo || 'Linha do tempo'),
          h('span', { className: C + '__sub alt-num' }, p.dia || 'Seg 29/09/2025 · ABC-1D23 · João Pereira')),
        h('div', { className: C + '__zoom', role: 'group', 'aria-label': 'Zoom' },
          h('button', { type: 'button', 'aria-label': 'Diminuir zoom', disabled: zoom <= min, onClick: function () { setZoom(zoom - step); } }, svg(ZOUT, 16)),
          h('span', { className: 'alt-num', 'aria-live': 'polite' }, zoom + '%'),
          h('button', { type: 'button', 'aria-label': 'Aumentar zoom', disabled: zoom >= max, onClick: function () { setZoom(zoom + step); } }, svg(ZIN, 16)))),
      h('div', { className: C + '__grid' },
        h('div', { className: C + '__labels' },
          h('div', { className: C + '__k ' + C + '__k--ruler' }, ic('clock'), 'Horário'),
          trilhas.map(function (t) { return h('div', { key: t.id, className: cx(C + '__k', t.alto && C + '__k--alto') }, ic(t.icon), h('span', null, t.label)); })),
        h('div', { className: C + '__scroll alt-scroll' },
          h('div', { className: C + '__inner', style: { width: zoom + '%' } },
            h('div', { className: C + '__ruler alt-num', 'aria-hidden': 'true' }, hours.map(function (x) {
              return h('span', { key: x, className: cx(C + '__tick', x % labelEvery === 0 && C + '__tick--lbl'), style: { left: (x / 24 * 100) + '%' } }, x % labelEvery === 0 ? h('b', null, x + 'h') : null);
            })),
            h('div', { className: C + '__rows', style: { backgroundSize: (100 / 24 * labelEvery) + '% 100%' } },
              trilhas.map(function (t) { return h('div', { key: t.id, className: cx(C + '__row', t.alto && C + '__row--alto') }, bar(t)); }))))),
      h('footer', { className: C + '__lg' },
        Object.keys(ST).map(function (k) { return h('span', { key: k }, h('i', { style: { background: ST[k].c } }), ST[k].w); }),
        h('span', { className: C + '__sep', 'aria-hidden': 'true' }),
        h('span', { className: C + '__lgt' }, 'Macros:'),
        MACRO_LEG.map(function (m) { return h('span', { key: m[1] }, h('i', { style: { background: m[0] } }), m[1]); }),
        h('span', { className: C + '__hover alt-num', 'aria-live': 'polite' }, hover || (p.onPosicaoClick !== null ? 'Clique em um trecho de Posição para abrir o mapa' : ''))));
  }
  A.PplFrotaTimeline = PplFrotaTimeline;
})();


/* ---- PplGanttMotorista ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 16 }); }
  function hmEnd(a, b) { return b > a && b % 24 === 0 ? '24:00' : hm(b); }
  function hm(x) { x = ((x % 24) + 24) % 24; var hh = Math.floor(x), mm = Math.round((x - hh) * 60); if (mm === 60) { hh++; mm = 0; } return (hh < 10 ? '0' : '') + hh + ':' + (mm < 10 ? '0' : '') + mm; }

  /* Horas contadas a partir de 00:00 do primeiro dia visível */
  var DIAS = [{ label: 'Seg 29/09' }, { label: 'Ter 30/09', hoje: true }, { label: 'Qua 01/10' }];
  var MOTORISTAS = [
    { id: 'm1', nome: 'João Pereira', sub: 'ABC-1D23 · Granéis' },
    { id: 'm2', nome: 'Marta Souza', sub: 'QRT-4F56 · Vidros' },
    { id: 'm3', nome: 'Carlos Lima', sub: 'JKL-7H89 · Granéis' },
    { id: 'm4', nome: 'Renata Alves', sub: 'Sem veículo', aviso: 'Sem veículo vinculado' }
  ];
  var ENTREGAS = [
    { id: '88190', motorista: 'm1', status: 'finalizada', rota: 'Campinas → Santos', cliente: 'Cebrace', ini: 5.5, fim: 13.33, real: { ini: 5.6, fim: 13.67 } },
    { id: '88213', motorista: 'm1', status: 'andamento', rota: 'Jundiaí → Sorocaba', cliente: 'Vidroporto', ini: 24 + 14.5, fim: 24 + 21, aviso: 'Checklist pendente', real: { ini: 24 + 15.17, eta: 24 + 21.83 }, janelas: [{ ini: 24 + 15, fim: 24 + 17, tipo: 'Carga', obs: 'Doca 3' }] },
    { id: '88201', motorista: 'm2', status: 'cancelada', rota: 'Limeira', cliente: 'Vidroporto', ini: 3, fim: 11 },
    { id: '88240', motorista: 'm2', status: 'pendente', rota: 'Porto Ferreira → SP', cliente: 'Vidroporto', ini: 48 + 1, fim: 48 + 13, janelas: [{ ini: 48 + 6, fim: 48 + 8, tipo: 'Descarga', obs: 'Agendado' }] },
    { id: '88222', motorista: 'm3', status: 'finalizada', rota: 'Rio Verde → Santos', cliente: 'Coop. Rio Verde', ini: 9, fim: 24 + 2, real: { ini: 9.08, fim: 24 + 2.25 } },
    { id: '88231', motorista: 'm3', status: 'andamento', rota: 'Santos → Paulínia', cliente: 'Braskem', ini: 24 + 8, fim: 24 + 17, real: { ini: 24 + 8.25, eta: 24 + 17.1 } },
    { id: '88255', motorista: 'm3', status: 'pendente', rota: 'Paulínia → Cubatão', cliente: 'Braskem', ini: 48 + 7, fim: 48 + 15 },
    { id: 'FOL', motorista: 'm4', status: 'evento', tipo: 'Folga', ini: 0, fim: 24 },
    { id: 'TRN', motorista: 'm4', status: 'evento', tipo: 'Treinamento NR-11', ini: 24 + 8, fim: 24 + 12 }
  ];
  var KPIS = [
    { id: 'atrasadas', label: 'Viagens atrasadas', valor: 4, alerta: true },
    { id: 'andamento', label: 'Em andamento', valor: 18 },
    { id: 'semMot', label: 'Veículos sem motorista', valor: 2, alerta: true },
    { id: 'checklists', label: 'Checklists pendentes', valor: 7 },
    { id: 'agend', label: 'Com agendamento', valor: 11 },
    { id: 'avisos', label: 'Avisos', valor: 3 },
    { id: 'tms', label: 'Integração TMS', valor: 'OK', ok: true }
  ];
  var ST = {
    pendente: { label: 'Pendente', c: 'var(--signal-info)', icon: 'clock' },
    andamento: { label: 'Em andamento', c: 'var(--chart-1)', icon: 'play' },
    finalizada: { label: 'Finalizada', c: 'var(--signal-success)', icon: 'check' },
    cancelada: { label: 'Cancelada', c: 'var(--slate)', icon: 'x' },
    evento: { label: 'Evento', c: 'var(--chart-2)', icon: 'calendar' }
  };

  function aderencia(e, tol) {
    if (!e.real) return null;
    var dIni = (e.real.ini - e.ini) * 60, dFim = ((e.real.fim != null ? e.real.fim : e.real.eta) - e.fim) * 60;
    var pior = Math.max(dIni, dFim);
    var c = pior <= 0 ? 'var(--signal-success)' : pior <= tol ? 'var(--signal-warning)' : 'var(--signal-error)';
    var w = pior <= 0 ? 'no prazo' : pior <= tol ? 'até ' + tol + ' min de atraso' : 'mais de ' + tol + ' min de atraso';
    return { c: c, w: w, dIni: Math.round(dIni), dFim: Math.round(dFim) };
  }
  function sgn(m) { return (m > 0 ? '+' : m < 0 ? '−' : '±') + Math.abs(m) + ' min'; }

  function PplGanttMotorista(p) {
    var C = 'alt-ppl-gantt-motorista';
    var dias = p.dias || DIAS, nd = dias.length, span = nd * 24;
    var motoristas = p.motoristas || MOTORISTAS;
    var eS = R.useState(p.entregas || ENTREGAS), ents = eS[0], setEnts = eS[1];
    var sS = R.useState(p.defaultSelecionada !== undefined ? p.defaultSelecionada : '88213'), sel = sS[0], setSel = sS[1];
    var qS = R.useState(''), q = qS[0], setQ = qS[1];
    var dS = R.useState(null), drag = dS[0], setDrag = dS[1];
    var oS = R.useState(null), over = oS[0], setOver = oS[1];
    var now = p.agora != null ? p.agora : 24 + 14.62;
    var tol = p.toleranciaMin || 30;
    var search = R.useRef(null);
    var kpis = p.kpis || KPIS;

    R.useEffect(function () {
      function k(e) { if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) { e.preventDefault(); search.current && search.current.focus(); } }
      document.addEventListener('keydown', k); return function () { document.removeEventListener('keydown', k); };
    }, []);

    function pct(x) { return (Math.max(0, Math.min(span, x)) / span * 100) + '%'; }
    function w(a, b) { return ((Math.min(span, b) - Math.max(0, a)) / span * 100) + '%'; }
    var ql = q.trim().toLowerCase();
    function match(e, m) { return !ql || [m.nome, m.sub, e && e.id, e && e.rota, e && e.cliente].join(' ').toLowerCase().indexOf(ql) >= 0; }
    var rows = motoristas.filter(function (m) {
      if (!ql) return true;
      if (match(null, m)) return true;
      return ents.some(function (e) { return e.motorista === m.id && match(e, m); });
    });

    function onDrop(m, ev) {
      ev.preventDefault();
      if (!drag) return;
      var r = ev.currentTarget.getBoundingClientRect();
      var x = (ev.clientX - r.left) / r.width * span - drag.grab;
      x = Math.round(x * 4) / 4;
      var moved = null;
      var nl = ents.map(function (e) {
        if (e.id !== drag.id) return e;
        var d = e.fim - e.ini; var ini = Math.max(0, Math.min(span - d, x));
        moved = Object.assign({}, e, { motorista: m.id, ini: ini, fim: ini + d });
        return moved;
      });
      setEnts(nl); setDrag(null); setOver(null);
      p.onMove && moved && p.onMove(moved);
    }

    function card(e, m) {
      var st = ST[e.status] || ST.pendente;
      var isSel = sel === e.id, dim = sel && !isSel && e.status !== 'evento';
      var titulo = e.status === 'evento' ? e.tipo : '#' + e.id;
      var tip = e.status === 'evento' ? e.tipo + ' · ' + hm(e.ini) + '–' + hmEnd(e.ini, e.fim) : 'Entrega #' + e.id + ' · ' + st.label + ' · ' + e.rota + ' · Previsto ' + hm(e.ini) + '–' + hmEnd(e.ini, e.fim) + (e.aviso ? ' · Aviso: ' + e.aviso : '');
      var draggable = e.status === 'pendente' || e.status === 'andamento';
      return h('button', {
        key: 'c' + e.id, type: 'button', title: tip, 'aria-label': tip, 'aria-pressed': isSel ? 'true' : 'false',
        className: cx(C + '__ev', C + '__ev--' + e.status, isSel && C + '__ev--sel', dim && C + '__ev--dim', drag && drag.id === e.id && C + '__ev--drag'),
        style: { left: pct(e.ini), width: w(e.ini, e.fim), '--c': st.c },
        draggable: draggable,
        onDragStart: function (ev) {
          var r = ev.currentTarget.getBoundingClientRect(), track = ev.currentTarget.parentNode.getBoundingClientRect();
          setDrag({ id: e.id, grab: (ev.clientX - r.left) / track.width * span });
          try { ev.dataTransfer.effectAllowed = 'move'; ev.dataTransfer.setData('text/plain', e.id); } catch (x) {}
        },
        onDragEnd: function () { setDrag(null); setOver(null); },
        onClick: function () { var n = isSel ? null : e.id; setSel(n); p.onSelect && p.onSelect(n ? e : null); }
      },
        h('span', { className: C + '__evt' }, ic(st.icon, 12), h('b', null, titulo), e.status !== 'evento' ? h('em', null, st.label) : null),
        h('small', null, e.status === 'evento' ? hm(e.ini) + '–' + hmEnd(e.ini, e.fim) : e.rota),
        e.aviso ? h('i', { className: C + '__al', 'aria-hidden': 'true' }) : null);
    }
    function realizado(e) {
      var a = aderencia(e, tol); if (!a) return null;
      var out = [];
      if (e.real.fim != null) {
        out.push(h('span', { key: 'r' + e.id, className: C + '__rz', style: { left: pct(e.real.ini), width: w(e.real.ini, e.real.fim), '--c': a.c },
          title: 'Realizado ' + hm(e.real.ini) + '–' + hm(e.real.fim) + ' · saída ' + sgn(a.dIni) + ' · chegada ' + sgn(a.dFim) + ' (' + a.w + ')' }));
      } else {
        out.push(h('span', { key: 'r' + e.id, className: C + '__rz ' + C + '__rz--run', style: { left: pct(e.real.ini), width: w(e.real.ini, now), '--c': a.c },
          title: 'Iniciada ' + hm(e.real.ini) + ' (' + sgn(a.dIni) + ') · em execução' }));
        out.push(h('span', { key: 'p' + e.id, className: C + '__rz ' + C + '__rz--proj', style: { left: pct(now), width: w(now, e.real.eta), '--c': a.c },
          title: 'Chegada prevista (ETA) ' + hm(e.real.eta) + ' · ' + sgn(a.dFim) + ' (' + a.w + ')' }));
      }
      return out;
    }

    var nowIn = now >= 0 && now <= span;
    return h('section', { className: cx(C, 'alt-panel', p.className), 'aria-label': p.titulo || 'Painel do motorista' },
      h('header', { className: C + '__hd' },
        h('div', { className: C + '__hg' }, h('h3', { className: C + '__title' }, p.titulo || 'Painel do motorista'), h('span', { className: C + '__sub' }, p.modo || 'Dia em foco (3 dias) · zoom 100%')),
        h('label', { className: 'alt-ctl alt-ctl--sm ' + C + '__search' }, ic('search', 15),
          h('input', { ref: search, value: q, onChange: function (e) { setQ(e.target.value); }, placeholder: 'Motorista, placa, ID, rota ou cliente', 'aria-label': 'Buscar no painel' }),
          q ? h('button', { type: 'button', className: C + '__clr', 'aria-label': 'Limpar busca', onClick: function () { setQ(''); } }, ic('x', 14)) : h('kbd', { className: C + '__kbd' }, 'Ctrl K')),
        p.headerActions || h('button', { type: 'button', className: 'alt-tbtn', onClick: p.onFiltros }, ic('sliders', 15), 'Filtros')),
      h('div', { className: C + '__kpis alt-num', role: 'list' }, kpis.map(function (k) {
        return h('button', { key: k.id, type: 'button', role: 'listitem', className: cx(C + '__kpi', k.alerta && C + '__kpi--al'), onClick: function () { p.onKpiClick && p.onKpiClick(k.id); } },
          h('b', null, k.valor, k.alerta ? h('span', { className: C + '__kpial', title: 'Requer atenção' }, ic('alert', 13)) : k.ok ? h('span', { className: C + '__kpiok' }, ic('check-circle', 13)) : null),
          h('span', null, k.label));
      })),
      h('div', { className: C + '__lg' },
        h('span', null, h('i', { className: C + '__lgpv' }), 'Previsto (card)'),
        h('span', null, h('i', { className: C + '__lgrl', style: { background: 'var(--signal-success)' } }), 'Realizado no prazo'),
        h('span', null, h('i', { className: C + '__lgrl', style: { background: 'var(--signal-warning)' } }), 'Até ' + tol + ' min de atraso'),
        h('span', null, h('i', { className: C + '__lgrl', style: { background: 'var(--signal-error)' } }), 'Mais de ' + tol + ' min'),
        h('span', null, h('i', { className: C + '__lgpj' }), 'Projeção (ETA)'),
        h('span', null, h('i', { className: C + '__lgwin' }), 'Janela de carga/descarga'),
        h('span', null, h('i', { className: C + '__lgnow' }), 'Agora'),
        h('span', { className: C + '__lghint' }, 'Arraste para mover entre dias e motoristas · Esc cancela')),
      h('div', { className: C + '__wrap alt-scroll' },
        h('div', { className: C + '__grid', style: { '--nd': nd } },
          h('div', { className: C + '__gh ' + C + '__gh--name' }, 'Motorista', h('span', { className: 'alt-num' }, rows.length + ' de ' + motoristas.length)),
          h('div', { className: C + '__gh ' + C + '__days' }, dias.map(function (d, i) {
            return h('span', { key: i, className: cx(C + '__day', d.hoje && C + '__day--hoje') }, d.label, d.hoje ? h('em', null, 'hoje') : null,
              h('span', { className: C + '__hrs alt-num', 'aria-hidden': 'true' }, h('s', null, '6h'), h('s', null, '12h'), h('s', null, '18h')));
          }),
            nowIn ? h('span', { className: C + '__nowt alt-num', style: { left: pct(now) } }, hm(now)) : null),
          rows.length ? rows.map(function (m) {
            var mine = ents.filter(function (e) { return e.motorista === m.id; });
            return [
              h('div', { key: 'n' + m.id, className: C + '__dr' }, h('b', null, m.nome), h('small', null, m.aviso ? h('span', { className: C + '__warn' }, ic('alert', 12)) : null, m.sub)),
              h('div', { key: 't' + m.id, className: cx(C + '__tr', over === m.id && C + '__tr--over', sel && C + '__tr--focus'),
                onDragOver: function (ev) { if (drag) { ev.preventDefault(); if (over !== m.id) setOver(m.id); } },
                onDragLeave: function (ev) { if (!ev.currentTarget.contains(ev.relatedTarget)) setOver(null); },
                onDrop: function (ev) { onDrop(m, ev); } },
                mine.map(function (e) { return (e.janelas || []).map(function (j, k) { return h('span', { key: 'w' + e.id + k, className: C + '__win', style: { left: pct(j.ini), width: w(j.ini, j.fim) }, title: j.tipo + ' · ' + hm(j.ini) + '–' + hm(j.fim) + (j.obs ? ' · ' + j.obs : '') }); }); }),
                mine.map(function (e) { return card(e, m); }),
                mine.map(realizado),
                nowIn ? h('span', { className: C + '__now', style: { left: pct(now) }, 'aria-hidden': 'true' }) : null)];
          }) : h('div', { className: C + '__empty' }, 'Nenhum motorista ou entrega encontrado para "' + q + '".'))));
  }
  A.PplGanttMotorista = PplGanttMotorista;
})();


/* ---- PplGridProgramacao ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  function ic(n, s) { return h(A.Icon, { name: n, size: s || 16 }); }
  var FACTORY = 'M2 20V9l6 4V9l6 4V4h8v16zM2 20h20M17 8h1M17 12h1M17 16h1';
  function svg(d, s) {
    return h('svg', { width: s || 16, height: s || 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      d.split(/(?=M)/).map(function (x, i) { return h('path', { key: i, d: x }); }));
  }

  var DATAS = [
    { id: 'd1', label: 'Seg 29' }, { id: 'd2', label: 'Ter 30', hoje: true }, { id: 'd3', label: 'Qua 01' },
    { id: 'd4', label: 'Qui 02' }, { id: 'd5', label: 'Sex 03' }, { id: 'd6', label: 'Sáb 04', fds: true }, { id: 'd7', label: 'Dom 05', fds: true }
  ];
  /* dia: [pedido, programado, realizado] */
  var GRUPOS = [
    { id: 'g1', nome: 'Vidros · Grupo Sul', icon: 'layers', clientes: [
      { id: 'c1', nome: 'Vidroporto · Porto Ferreira', icon: 'factory', tip: 'Cliente industrial', ctx: [['pin', 'Terminal de coleta: Jundiaí'], ['route', 'Rota SP-330']], dias: [[6, 6, 6], [5, 5, 3], [4, 4, 0], [3, 3, 0], [4, 4, 0], [0, 0, 0], [0, 0, 0]] },
      { id: 'c2', nome: 'Cebrace · Jacareí', icon: 'factory', tip: 'Cliente industrial', ctx: [['pin', 'Terminal de coleta: Caçapava'], ['route', 'Rota Dutra']], dias: [[6, 6, 6], [5, 5, 5], [5, 5, 0], [4, 4, 0], [5, 5, 0], [2, 2, 0], [0, 0, 0]] }
    ] },
    { id: 'g2', nome: 'Grãos · Grupo Centro', icon: 'layers', clientes: [
      { id: 'c3', nome: 'Cooperativa Rio Verde', icon: 'users', tip: 'Cooperativa', ctx: [['pin', 'Terminal de coleta: Rio Verde'], ['route', 'Rota BR-060']], dias: [[12, 12, 10], [12, 11, 5], [10, 9, 0], [11, 10, 0], [10, 10, 0], [4, 4, 0], [0, 0, 0]] },
      { id: 'c4', nome: 'Armazém Anápolis', icon: 'building', tip: 'Armazém', ctx: [['route', 'Rota BR-153']], dias: [[12, 12, 8], [11, 11, 4], [9, 9, 0], [9, 9, 0], [8, 8, 0], [2, 2, 0], [0, 0, 0]] }
    ] },
    { id: 'g3', nome: 'Químicos · Grupo Litoral', icon: 'layers', clientes: [
      { id: 'c5', nome: 'Braskem · Cubatão', icon: 'factory', tip: 'Cliente industrial', ctx: [['route', 'Rota Anchieta']], dias: [[3, 3, 3], [4, 4, 4], [3, 3, 0], [3, 3, 0], [2, 2, 0], [0, 0, 0], [0, 0, 0]] }
    ] }
  ];

  function soma(cls, di) {
    return cls.reduce(function (s, c) { var d = c.dias[di] || [0, 0, 0]; return [s[0] + d[0], s[1] + d[1], s[2] + d[2]]; }, [0, 0, 0]);
  }

  function Cell(p) {
    var C = 'alt-ppl-grid-programacao';
    var d = p.d, ped = d[0], prog = d[1], real = d[2];
    if (!prog && !ped) return h('td', { className: cx(C + '__td', p.fds && C + '__td--fds') }, h('span', { className: C + '__empty' }, '—'));
    var t, w;
    if (p.futuro) { t = 'neutral'; w = 'A realizar'; }
    else { var r = prog ? real / prog : 1; if (r >= 1) { t = 'success'; w = 'Aderente'; } else if (r >= 0.75) { t = 'warning'; w = 'Parcial'; } else { t = 'error'; w = 'Baixa aderência'; } }
    if (p.hoje && t !== 'success') { t = 'info'; w = 'Em execução'; }
    var tn = K.tone(t);
    var a = p.visaoPedido ? prog : real, b = p.visaoPedido ? ped : prog;
    var tip = (p.visaoPedido ? 'Programado ' + prog + ' de ' + ped + ' pedidos' : 'Realizado ' + real + ' de ' + prog + ' programados') + ' · ' + w;
    return h('td', { className: cx(C + '__td', p.fds && C + '__td--fds', p.hoje && C + '__td--hoje'), title: tip },
      h('span', { className: C + '__cell' },
        p.futuro && !p.visaoPedido ? h('span', { className: C + '__v alt-num' }, h('small', null, 'prog. '), h('b', null, prog))
          : h('span', { className: C + '__v alt-num' }, h('b', null, a), h('small', null, '/' + b)),
        h('span', { className: C + '__st', style: { color: tn.c }, 'aria-label': w }, ic(p.futuro && !p.hoje ? 'circle' : tn.icon, 13))));
  }

  function PplGridProgramacao(p) {
    var C = 'alt-ppl-grid-programacao';
    var grupos = p.dadosAgrupados || GRUPOS, datas = p.datasArray || DATAS;
    var hojeIdx = -1; datas.forEach(function (d, i) { if (d.hoje) hojeIdx = i; });
    var eS = R.useState(p.expanded || { g1: true, g2: true }), exp = p.expanded && p.onExpand ? p.expanded : eS[0];
    var vS = R.useState(!!p.visaoPedido), visao = vS[0];
    var allOpen = grupos.every(function (g) { return exp[g.id]; });
    function setExp(nx) { eS[1](nx); p.onExpand && p.onExpand(nx); }
    function toggle(id) { var nx = Object.assign({}, exp); nx[id] = !nx[id]; setExp(nx); }
    function all(v) { var nx = {}; grupos.forEach(function (g) { nx[g.id] = v; }); setExp(nx); }
    var desc = p.percentLarguraDescricao || 28;

    var body = [];
    grupos.forEach(function (g) {
      var open = !!exp[g.id];
      body.push(h('tr', { key: g.id, className: C + '__g' },
        h('th', { scope: 'row', className: C + '__name' },
          h('button', { type: 'button', className: C + '__exp', 'aria-expanded': open ? 'true' : 'false', onClick: function () { toggle(g.id); } },
            h('span', { className: cx(C + '__chev', open && C + '__chev--open') }, ic('chevron-right', 16)),
            h('span', { className: C + '__gi', title: 'Grupo de clientes' }, ic(g.icon || 'layers', 16)),
            h('span', { className: C + '__nt' }, g.nome),
            h('span', { className: C + '__n alt-num' }, g.clientes.length))),
        datas.map(function (d, i) { return h(Cell, { key: d.id, d: soma(g.clientes, i), futuro: hojeIdx >= 0 && i > hojeIdx, hoje: i === hojeIdx, fds: d.fds, visaoPedido: visao }); })));
      if (open) g.clientes.forEach(function (c) {
        body.push(h('tr', { key: c.id, className: C + '__c' },
          h('th', { scope: 'row', className: C + '__name ' + C + '__name--c' }, h('div', { className: C + '__cn' },
            h('span', { className: C + '__ci', title: c.tip }, c.icon === 'factory' ? svg(FACTORY, 16) : ic(c.icon || 'user', 16)),
            h('span', { className: C + '__nt' }, c.nome),
            (c.ctx || []).map(function (x, j) { return h('span', { key: j, className: C + '__ctx', title: x[1], 'aria-label': x[1], role: 'img' }, ic(x[0], 14)); }),
            p.onClienteClick ? h('button', { type: 'button', className: C + '__more', 'aria-label': 'Abrir ' + c.nome, onClick: function () { p.onClienteClick(c); } }, ic('external', 14)) : null)),
          datas.map(function (d, i) { return h(Cell, { key: d.id, d: c.dias[i] || [0, 0, 0], futuro: hojeIdx >= 0 && i > hojeIdx, hoje: i === hojeIdx, fds: d.fds, visaoPedido: visao }); })));
      });
    });
    var tot = datas.map(function (d, i) { return grupos.reduce(function (s, g) { var x = soma(g.clientes, i); return [s[0] + x[0], s[1] + x[1], s[2] + x[2]]; }, [0, 0, 0]); });

    return h('section', { className: cx(C, 'alt-panel', p.className), 'aria-label': p.titulo || 'Programação por cliente' },
      h('header', { className: C + '__hd' },
        h('div', { className: C + '__hg' }, h('h3', { className: C + '__title' }, p.titulo || 'Programação por cliente'), h('span', { className: C + '__sub alt-num' }, p.subtitulo || 'Semana 40/2025 · 29/09 – 05/10')),
        h('div', { className: C + '__seg', role: 'radiogroup', 'aria-label': 'Visão' },
          h('button', { type: 'button', role: 'radio', 'aria-checked': !visao ? 'true' : 'false', onClick: function () { vS[1](false); p.onVisaoChange && p.onVisaoChange(false); } }, 'Realizado / Programado'),
          h('button', { type: 'button', role: 'radio', 'aria-checked': visao ? 'true' : 'false', onClick: function () { vS[1](true); p.onVisaoChange && p.onVisaoChange(true); } }, 'Programado / Pedido')),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { all(!allOpen); } }, ic(allOpen ? 'chevron-up' : 'chevron-down', 15), allOpen ? 'Recolher tudo' : 'Expandir tudo'),
        p.closeCallback ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-tbtn--icon', 'aria-label': 'Fechar', onClick: p.closeCallback }, ic('x', 16)) : null),
      h('div', { className: C + '__wrap alt-scroll' },
        h('table', { className: C + '__t' },
          h('colgroup', null, h('col', { style: { width: desc + '%' } }), datas.map(function (d) { return h('col', { key: d.id }); })),
          h('thead', null, h('tr', null,
            h('th', { scope: 'col', className: C + '__name' }, 'Cliente'),
            datas.map(function (d) { return h('th', { key: d.id, scope: 'col', className: cx(C + '__dh', d.fds && C + '__td--fds', d.hoje && C + '__dh--hoje') }, d.label, d.hoje ? h('span', { className: C + '__today' }, 'hoje') : null); }))),
          h('tbody', null, body),
          h('tfoot', null, h('tr', null,
            h('th', { scope: 'row', className: C + '__name' }, 'Total'),
            tot.map(function (x, i) { return h(Cell, { key: i, d: x, futuro: hojeIdx >= 0 && i > hojeIdx, hoje: i === hojeIdx, fds: datas[i].fds, visaoPedido: visao }); }))))),
      h('footer', { className: C + '__lg' },
        [['success', 'Aderente (100%)'], ['warning', 'Parcial (≥ 75%)'], ['error', 'Baixa aderência'], ['info', 'Hoje, em execução'], ['neutral', 'A realizar']].map(function (x) {
          var tn = K.tone(x[0]);
          return h('span', { key: x[0] }, h('span', { style: { color: tn.c, display: 'inline-flex' } }, ic(x[0] === 'neutral' ? 'circle' : tn.icon, 13)), x[1]);
        }),
        h('span', { className: C + '__lgv' }, visao ? 'Células: programado / pedido' : 'Células: realizado / programado')));
  }
  A.PplGridProgramacao = PplGridProgramacao;
})();


/* ---- PplHistoricoEntrega ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var DEFAULT_ENTRIES = [
    { kind: 'first', title: 'Primeiro registro', date: '27/09 09:12', author: 'Ana Ribeiro', text: 'Programada para 29/09 · ABC-1D23' },
    { kind: 'change', title: 'Alteração 1', date: '28/09 16:40', author: 'Ana Ribeiro', changes: [{ field: 'Data', from: '29/09', to: '30/09' }] },
    { kind: 'change', title: 'Alteração 2', date: '29/09 11:05', author: 'Marcos Dias', changes: [{ field: 'Motorista', from: 'Carlos Lima', to: 'João Pereira' }, { field: 'Janela', from: '08:00–10:00', to: '09:00–11:00' }] },
    { kind: 'current', title: 'Situação atual', date: '30/09 08:05', text: 'Em andamento · João Pereira · ABC-1D23' }
  ];

  function PplHistoricoEntrega(p) {
    var entries = p.entries || DEFAULT_ENTRIES;
    var collapseAfter = p.collapseAfter == null ? 3 : p.collapseAfter;
    var exp = R.useState(!!p.defaultExpanded), expanded = exp[0], setExpanded = exp[1];
    var changes = entries.filter(function (e) { return e.kind === 'change'; });
    var hideCount = !expanded && changes.length > collapseAfter - 1 ? changes.length - (collapseAfter - 1) : 0;
    var hidden = {};
    if (hideCount) changes.slice(0, hideCount).forEach(function (e) { hidden[entries.indexOf(e)] = true; });

    var items = [];
    entries.forEach(function (e, i) {
      if (hidden[i]) {
        if (!items.some(function (x) { return x.key === 'more'; })) {
          items.push(h('li', { key: 'more', className: 'alt-ppl-historico-entrega__node alt-ppl-historico-entrega__node--more' },
            h('button', { type: 'button', className: 'alt-ppl-historico-entrega__more', onClick: function () { setExpanded(true); }, 'aria-expanded': 'false' },
              h(A.Icon, { name: 'chevron-down', size: 14 }), 'Mostrar ' + hideCount + (hideCount === 1 ? ' alteração anterior' : ' alterações anteriores'))));
        }
        return;
      }
      var kind = e.kind || 'change';
      items.push(h('li', { key: i, className: cx('alt-ppl-historico-entrega__node', 'alt-ppl-historico-entrega__node--' + kind) },
        h('span', { className: 'alt-ppl-historico-entrega__dot', 'aria-hidden': 'true' }),
        h('div', { className: 'alt-ppl-historico-entrega__head' },
          h('span', { className: 'alt-ppl-historico-entrega__title' }, e.title),
          h('span', { className: 'alt-ppl-historico-entrega__meta alt-num' }, [e.date, e.author].filter(Boolean).join(' · '))),
        e.text ? h('div', { className: 'alt-ppl-historico-entrega__text' }, e.text) : null,
        e.changes && e.changes.length ? h('ul', { className: 'alt-ppl-historico-entrega__changes' }, e.changes.map(function (c, j) {
          return h('li', { key: j },
            h('span', { className: 'alt-ppl-historico-entrega__field' }, c.field + ':'),
            h('s', { className: 'alt-ppl-historico-entrega__from' }, c.from),
            h(A.Icon, { name: 'arrow-right', size: 12, className: 'alt-ppl-historico-entrega__arrow' }),
            h('span', { className: 'alt-ppl-historico-entrega__to' }, c.to));
        })) : null));
    });

    return h('section', { className: cx('alt-glass', 'alt-ppl-historico-entrega', p.className), style: p.style, 'aria-label': p.title || 'Histórico da entrega' },
      h('header', { className: 'alt-ppl-historico-entrega__hd' },
        h(A.Icon, { name: 'clock', size: 16, className: 'alt-ppl-historico-entrega__hd-icon' }),
        h('h3', { className: 'alt-ppl-historico-entrega__hd-title' }, p.title || 'Histórico da entrega #88213'),
        expanded && changes.length > collapseAfter - 1 ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-ppl-historico-entrega__collapse', onClick: function () { setExpanded(false); } }, 'Recolher') : null,
        p.onClose ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Fechar', onClick: p.onClose }, h(A.Icon, { name: 'x', size: 16 })) : null),
      h('ol', { className: 'alt-ppl-historico-entrega__tl' }, items));
  }
  A.PplHistoricoEntrega = PplHistoricoEntrega;
})();


/* ---- PplInfoBoxAderencia ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  var SCALE = 'M12 3v18M5 7h14M7 7l-3 7a3 3 0 0 0 6 0zM17 7l-3 7a3 3 0 0 0 6 0z';
  function Svg(d, size) {
    return h('svg', { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' }, h('path', { d: d }));
  }

  /* Faixas do código: <50 erro · <100 atenção · >110 acima (info) · demais ok · sem pedido neutro */
  function band(pct) {
    if (pct == null) return { tone: 'neutral', label: 'Sem pedido' };
    if (pct < 50) return { tone: 'error', label: 'Abaixo de 50%' };
    if (pct < 100) return { tone: 'warning', label: 'Abaixo do pedido' };
    if (pct > 110) return { tone: 'info', label: 'Acima de 110%' };
    return { tone: 'success', label: 'Aderente' };
  }
  function pctOf(real, ped) { return ped ? Math.round((real / ped) * 100) : null; }

  function Chip(pct) {
    var b = band(pct), t = K.tone(b.tone);
    return h('span', { className: 'alt-chip alt-ppl-info-box-aderencia__chip', style: { color: t.c, background: t.bg }, title: b.label },
      h(A.Icon, { name: t.icon, size: 12 }), pct == null ? '—' : pct + '%');
  }

  function Cell(v, clickable, onClick, label) {
    if (!clickable) return h('span', { className: 'alt-ppl-info-box-aderencia__v' }, v);
    return h('span', { className: 'alt-ppl-info-box-aderencia__v' },
      h('button', { type: 'button', className: 'alt-ppl-info-box-aderencia__ck', onClick: onClick, 'aria-label': label + ': ' + v }, v));
  }

  function PplInfoBoxAderencia(p) {
    var unit = p.volumeLabel || 'Volume (t)';
    var dec = p.decimals || 0;
    var rows = [
      { key: 'volume', label: unit, icon: Svg(SCALE, 14), ped: p.pedidoVolume, prog: p.programadoVolume, real: p.realizadoVolume, pct: p.percentVolume },
      { key: 'entregas', label: 'Entregas', icon: h(A.Icon, { name: 'truck', size: 14 }), ped: p.pedidoEntregas, prog: p.programadoEntregas, real: p.realizadoEntregas, pct: p.percentEntregas }
    ];
    var progClick = p.progColumnClickable && p.onClickProgColumn;
    var realClick = p.realColumnClickable && p.onClickRealColumn;
    return h('div', { className: cx('alt-panel', 'alt-ppl-info-box-aderencia', p.type === 'grid' && 'alt-ppl-info-box-aderencia--grid', p.className), style: Object.assign({ width: p.size || 272 }, p.style), role: 'table', 'aria-label': p.title || 'Aderência' },
      p.title ? h('div', { className: 'alt-ppl-info-box-aderencia__title' },
        h('span', { className: 'alt-dot', style: { color: p.accent || 'var(--teal-light)' }, 'aria-hidden': 'true' }), h('span', null, p.title), p.buttons ? h('span', { className: 'alt-ppl-info-box-aderencia__btns' }, p.buttons) : null) : null,
      h('div', { className: 'alt-ppl-info-box-aderencia__row alt-ppl-info-box-aderencia__row--h', role: 'row' },
        h('span', { role: 'columnheader' }, h('span', { className: 'alt-ppl-info-box-aderencia__sr' }, 'Medida')),
        h('span', { role: 'columnheader', title: 'Pedido' }, 'Ped.'),
        h('span', { role: 'columnheader', title: 'Programado' }, 'Prog.'),
        h('span', { role: 'columnheader', title: 'Realizado' }, 'Real.'),
        h('span', { role: 'columnheader', title: 'Aderência' }, '%')),
      rows.map(function (r) {
        var pct = r.pct != null ? r.pct : pctOf(r.real, r.ped);
        var f = function (v) { return v == null ? '—' : nf(v, r.key === 'volume' ? dec : 0); };
        return h('div', { key: r.key, className: 'alt-ppl-info-box-aderencia__row alt-num', role: 'row' },
          h('span', { className: 'alt-ppl-info-box-aderencia__ic', role: 'rowheader', title: r.label, 'aria-label': r.label }, r.icon),
          h('span', { className: 'alt-ppl-info-box-aderencia__v', role: 'cell' }, f(r.ped)),
          Cell(f(r.prog), progClick, function () { p.onClickProgColumn(r.key); }, 'Programado ' + r.label),
          Cell(f(r.real), realClick, function () { p.onClickRealColumn(r.key); }, 'Realizado ' + r.label),
          h('span', { className: 'alt-ppl-info-box-aderencia__v', role: 'cell' }, Chip(pct)));
      }));
  }
  PplInfoBoxAderencia.band = band;
  A.PplInfoBoxAderencia = PplInfoBoxAderencia;
})();


/* ---- PplKpiDonuts ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  var PRINTER = 'M6 9V3h12v6M6 18H4v-7h16v7h-2M8 14h8v7H8z';
  var DEFAULT_MONTHS = ['Jul', 'Ago', 'Set'];
  var DEFAULT_KPIS = [
    { key: 'produtividade', label: 'Produtividade', values: [62, 58, 71], total: 64 },
    { key: 'indisponibilidade', label: 'Indisponibilidade', values: [12, 9, 14], total: 12, invert: true },
    { key: 'ocupacao', label: 'Ocupação de carga', values: [84, 88, 81], total: 84 }
  ];

  function Donut(p) {
    var size = p.size, sw = p.stroke, r = (size - sw) / 2, c = 2 * Math.PI * r;
    var v = Math.max(0, Math.min(100, p.value == null ? 0 : p.value));
    return h('div', { className: cx('alt-ppl-kpi-donuts__donut', p.total && 'alt-ppl-kpi-donuts__donut--total'), style: { width: size, height: size }, role: 'img', 'aria-label': p.label },
      h('svg', { width: size, height: size, viewBox: '0 0 ' + size + ' ' + size, 'aria-hidden': 'true' },
        h('circle', { cx: size / 2, cy: size / 2, r: r, fill: 'none', stroke: 'var(--track)', strokeWidth: sw }),
        p.value == null ? null : h('circle', { cx: size / 2, cy: size / 2, r: r, fill: 'none', stroke: p.total ? 'var(--teal-light)' : 'var(--chart-1)', strokeWidth: sw, strokeLinecap: v > 0 && v < 100 ? 'round' : 'butt', strokeDasharray: (c * v / 100) + ' ' + c, transform: 'rotate(-90 ' + size / 2 + ' ' + size / 2 + ')', className: 'alt-ppl-kpi-donuts__arc' })),
      h('span', { className: 'alt-ppl-kpi-donuts__val alt-num' }, p.value == null ? '—' : nf(p.value, p.decimals) + '%'));
  }

  function toCsv(months, kpis, totalLabel) {
    var head = ['KPI'].concat(months, [totalLabel]).join(';');
    return [head].concat(kpis.map(function (k) { return [k.label].concat(k.values.map(function (v) { return v == null ? '' : nf(v, 1); }), [k.total == null ? '' : nf(k.total, 1)]).join(';'); })).join('\n');
  }

  function PplKpiDonuts(p) {
    var months = p.months || DEFAULT_MONTHS, kpis = p.kpis || DEFAULT_KPIS;
    var showTotal = p.totalMensal !== false, totalLabel = p.totalLabel || 'Total';
    var size = p.kpiWidth || 64, stroke = p.ringWidth || 8;
    var sel = R.useState(null), month = sel[0], setMonth = sel[1];
    var cols = months.length + (showTotal ? 1 : 0);

    function exportCsv() {
      if (p.onExportCsv) return p.onExportCsv(toCsv(months, kpis, totalLabel));
      try {
        var blob = new Blob(['﻿' + toCsv(months, kpis, totalLabel)], { type: 'text/csv;charset=utf-8' });
        var a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = (p.fileName || 'kpi-consolidado') + '.csv'; a.click();
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
      } catch (e) { /* sem suporte a download */ }
    }
    function print() { if (p.onPrintReport) p.onPrintReport(); else window.print(); }
    function pick(i) { var n = month === i ? null : i; setMonth(n); if (p.onSelectMonth) p.onSelectMonth(n == null ? null : months[n]); }

    return h('section', { className: cx('alt-glass', 'alt-ppl-kpi-donuts', p.className), style: p.style, 'aria-label': p.title || 'KPI consolidado' },
      h('header', { className: 'alt-ppl-kpi-donuts__hd' },
        h('div', { className: 'alt-ppl-kpi-donuts__ttl' },
          h('h3', null, p.title || 'KPI consolidado · 2026'),
          p.subtitle !== null ? h('span', null, p.subtitle || 'Frota própria · meta mensal por indicador') : null),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: exportCsv }, h(A.Icon, { name: 'download', size: 16 }), 'Exportar CSV'),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: print },
          h('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', className: 'alt-icon', 'aria-hidden': 'true' }, h('path', { d: PRINTER })),
          'Imprimir')),
      h('div', { className: 'alt-ppl-kpi-donuts__body alt-scroll' },
        h('div', { className: 'alt-ppl-kpi-donuts__grid', role: 'table', style: { gridTemplateColumns: (p.labelWidth || 150) + 'px repeat(' + cols + ', minmax(' + (p.columnWidth || 84) + 'px, 1fr))' } },
          h('span', { role: 'columnheader' }),
          months.map(function (m, i) {
            return h('button', { key: m, type: 'button', role: 'columnheader', className: 'alt-ppl-kpi-donuts__mh', 'aria-pressed': month === i ? 'true' : 'false', onClick: function () { pick(i); } }, m);
          }),
          showTotal ? h('span', { role: 'columnheader', className: 'alt-ppl-kpi-donuts__mh alt-ppl-kpi-donuts__mh--total' }, totalLabel) : null,
          kpis.map(function (k) {
            return [
              h('span', { key: k.key + '-l', role: 'rowheader', className: 'alt-ppl-kpi-donuts__k' }, k.label, k.hint ? h('small', null, k.hint) : null)
            ].concat(months.map(function (m, i) {
              return h('div', { key: k.key + '-' + i, role: 'cell', className: cx('alt-ppl-kpi-donuts__cell', month === i && 'is-sel', month != null && month !== i && 'is-dim') },
                h(Donut, { size: size, stroke: stroke, value: k.values[i], decimals: k.decimals || 0, label: k.label + ' em ' + m + ': ' + (k.values[i] == null ? 'sem dados' : nf(k.values[i], k.decimals || 0) + '%') }));
            }), showTotal ? [h('div', { key: k.key + '-t', role: 'cell', className: 'alt-ppl-kpi-donuts__cell alt-ppl-kpi-donuts__cell--total' },
              h(Donut, { size: size, stroke: stroke, value: k.total, total: true, decimals: k.decimals || 0, label: k.label + ' no total: ' + (k.total == null ? 'sem dados' : nf(k.total, k.decimals || 0) + '%') }))] : []);
          }))));
  }
  A.PplKpiDonuts = PplKpiDonuts;
})();


/* ---- PplLane ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  /* Tipos de evento → série de gráfico (substitui os hex de getEventColor) */
  var TYPES = {
    maintenance: { label: 'Manutenção', c: 'var(--chart-5)' },
    training: { label: 'Treinamento', c: 'var(--chart-3)' },
    dayoff: { label: 'Folga', c: 'var(--chart-6)' },
    'default': { label: 'Viagem', c: 'var(--chart-2)' }
  };

  function hhmm(pct) {
    var m = Math.round(pct / 100 * 24 * 60 / 5) * 5, hh = Math.floor(m / 60), mm = m % 60;
    return (hh < 10 ? '0' : '') + hh + ':' + (mm < 10 ? '0' : '') + mm;
  }

  var DEFAULT_LANES = [
    { key: 'agendado', label: 'Agendado', color: 'var(--chart-4)', items: [
      { id: 'm1', type: 'maintenance', percentStart: 33.3, percentEnd: 47.9, label: 'Manutenção' },
      { id: 'v1', type: 'default', percentStart: 52, percentEnd: 82, label: 'Viagem 88213 · Campinas → Santos' }
    ] },
    { key: 'realizado', label: 'Realizado', color: 'var(--chart-1)', items: [
      { id: 'm2', type: 'maintenance', percentStart: 34.4, percentEnd: 51, label: 'Manutenção' },
      { id: 'v2', type: 'default', percentStart: 55, percentEnd: 61, label: 'Viagem 88213' },
      { id: 'v3', type: 'training', percentStart: 4, percentEnd: 12.5, label: 'Treinamento' }
    ] },
    { key: 'motorista', label: 'Motorista', color: 'var(--chart-3)', items: [
      { id: 'f1', type: 'dayoff', percentStart: 0, percentEnd: 25, label: 'Folga' },
      { id: 'j1', type: 'default', percentStart: 30, percentEnd: 44, label: 'Jornada 1' },
      { id: 'j2', type: 'default', percentStart: 40, percentEnd: 62, label: 'Jornada 2 (sobreposta)' }
    ] }
  ];

  function Ruler(p) {
    var marks = [];
    for (var i = 0; i < 8; i++) {
      var hr = i * 3, night = hr < 6 || hr >= 18;
      var ic = (hr === 0 && p.showLeftMoon !== false) || hr === 18 ? 'moon' : hr === 6 ? 'sun' : null;
      marks.push(h('span', { key: i, className: cx('alt-ppl-lane__mark', night && 'is-night') }, ic ? h(A.Icon, { name: ic, size: 12 }) : null, p.showHourLabels === false ? null : hr + 'h'));
    }
    return h('div', { className: 'alt-ppl-lane__ruler alt-num', 'aria-hidden': 'true' }, marks,
      p.showRightMoon !== false ? h('span', { className: 'alt-ppl-lane__end' }, h(A.Icon, { name: 'moon', size: 12 }), '24h') : null);
  }

  function PplLane(p) {
    var lanes = p.lanes || DEFAULT_LANES;
    var selCtl = p.selected !== undefined;
    var s = R.useState(p.defaultSelected === undefined ? 'v1' : p.defaultSelected), sel = selCtl ? p.selected : s[0];
    var hv = R.useState(null), hover = hv[0], setHover = hv[1];
    var laneH = p.height || 36, gap = p.laneGap == null ? 6 : p.laneGap;
    var now = p.now === undefined ? 61 : p.now;

    function select(it) { if (!selCtl) s[1](it.id === sel ? null : it.id); if (p.onSelect) p.onSelect(it.id === sel ? null : it); }

    function laneRow(l) {
      /* sobreposição: o bloco que começa dentro de outro desce pelo laneGap e o de trás esmaece */
      var sorted = l.items.slice().sort(function (a, b) { return a.percentStart - b.percentStart; });
      var level = {}, behind = {};
      sorted.forEach(function (it, i) {
        level[it.id] = 0;
        for (var j = 0; j < i; j++) { var o = sorted[j]; if (o.percentEnd > it.percentStart) { level[it.id] = Math.max(level[it.id], level[o.id] + 1); behind[o.id] = true; } }
      });
      return h('div', { key: l.key, className: 'alt-ppl-lane__row' },
        h('span', { className: 'alt-ppl-lane__name' }, h('span', { className: 'alt-dot', style: { color: l.color || 'var(--teal-light)' } }), l.label),
        h('div', { className: 'alt-ppl-lane__track', style: { height: laneH }, role: 'list', 'aria-label': 'Trilha ' + l.label },
          sorted.map(function (it) {
            var t = TYPES[it.type] || TYPES['default'], c = it.color || t.c;
            var tip = it.tooltip || (t.label + ' · ' + hhmm(it.percentStart) + '–' + hhmm(it.percentEnd));
            var off = level[it.id] * gap;
            return h('button', { key: it.id, type: 'button', role: 'listitem', className: cx('alt-ppl-lane__blk', it.id === sel && 'is-sel', behind[it.id] && 'is-behind'),
              style: { '--c': c, left: it.percentStart + '%', width: (it.percentEnd - it.percentStart) + '%', top: 4 + off, bottom: Math.max(4 - off, 1) },
              'aria-pressed': it.id === sel ? 'true' : 'false', 'aria-label': (it.label || t.label) + ', ' + tip,
              onClick: function () { select(it); },
              onDoubleClick: function () { if (p.onOpen) p.onOpen(it); },
              onKeyDown: function (e) { if (e.key === 'Enter' && e.shiftKey && p.onOpen) { e.preventDefault(); p.onOpen(it); } },
              onMouseEnter: function () { setHover({ id: it.id, lane: l.key, tip: tip, x: (it.percentStart + it.percentEnd) / 2 }); },
              onMouseLeave: function () { setHover(null); },
              onFocus: function () { setHover({ id: it.id, lane: l.key, tip: tip, x: (it.percentStart + it.percentEnd) / 2 }); },
              onBlur: function () { setHover(null); } },
              h('span', { className: 'alt-ppl-lane__blk-label' }, it.label || t.label));
          }),
          now != null ? h('span', { className: 'alt-ppl-lane__now', style: { left: now + '%' }, 'aria-hidden': 'true' }) : null,
          hover && hover.lane === l.key ? h('span', { className: 'alt-ppl-lane__tip alt-num', role: 'tooltip', style: { left: hover.x + '%' } }, hover.tip) : null));
    }

    var used = {};
    lanes.forEach(function (l) { l.items.forEach(function (it) { used[it.type || 'default'] = true; }); });

    return h('section', { className: cx('alt-glass', 'alt-ppl-lane', p.className), style: p.style },
      p.title !== null ? h('header', { className: 'alt-ppl-lane__hd' },
        h(A.Icon, { name: 'truck', size: 16, className: 'alt-ppl-lane__hd-ic' }),
        h('h3', null, p.title || 'ABC-1D23 · Scania R450'),
        h('span', { className: 'alt-ppl-lane__sub alt-num' }, p.subtitle || 'Seg 29/09'),
        h('span', { className: 'alt-ppl-lane__legend' }, Object.keys(TYPES).filter(function (k) { return used[k]; }).map(function (k) {
          return h('span', { key: k }, h('i', { style: { background: TYPES[k].c } }), TYPES[k].label);
        }))) : null,
      h('div', { className: 'alt-ppl-lane__body' },
        h('div', { className: 'alt-ppl-lane__row alt-ppl-lane__row--ruler' }, h('span'), h(Ruler, p)),
        lanes.map(laneRow),
        now != null ? h('div', { className: 'alt-ppl-lane__row alt-ppl-lane__row--foot' }, h('span'),
          h('div', { className: 'alt-ppl-lane__foot' }, h('span', { className: 'alt-ppl-lane__now-lbl alt-num', style: { left: now + '%' } }, 'Agora · ' + hhmm(now)))) : null));
  }
  PplLane.TYPES = TYPES;
  A.PplLane = PplLane;
})();


/* ---- PplMapaAreas ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  /* ---------- ícones próprios (traço 2, viewBox 24) ---------- */
  var IC = {
    select: 'm4 3 16 8-7 2-2 7z',
    vertex: 'M4 18 9 6l7 4 4 9z',
    polygon: 'M12 3 21 9l-3 11H6L3 9z',
    rect: 'M4 6h16v12H4z',
    circle: 'M12 20a8 8 0 1 0 0-16 8 8 0 0 0 0 16zM12 12h8',
    free: 'M4 16c2-6 5-10 8-8s-1 6 2 7 4-5 6-7',
    corridor: 'M3 18 9 9l6 5 6-9M3 13 9 4l6 5 6-9',
    hole: 'M3 3h18v18H3zM9 9h6v6H9z',
    union: 'M9 18a6 6 0 1 0 0-12 6 6 0 0 0 0 12zM15 18a6 6 0 1 0 0-12 6 6 0 0 0 0 12z',
    subtract: 'M9 18a6 6 0 1 0 0-12 6 6 0 0 0 0 12zM15 6a6 6 0 0 1 0 12',
    split: 'M4 4h16v16H4zM3 21 21 3',
    buffer: 'M7 7h10v10H7zM3 6a3 3 0 0 1 3-3h12a3 3 0 0 1 3 3v12a3 3 0 0 1-3 3H6a3 3 0 0 1-3-3z',
    simplify: 'M3 17 13 6l8 6',
    undo: 'M9 14 4 9l5-5M4 9h11a5 5 0 0 1 0 10h-3',
    redo: 'm15 14 5-5-5-5M20 9H9a5 5 0 0 0 0 10h3',
    magnet: 'M6 3v7a6 6 0 0 0 12 0V3M6 3h4M14 3h4M6 7h4M14 7h4',
    gridIc: 'M3 3v18h18M3 9h18M3 15h18M9 3v18M15 3v18',
    ruler: 'M2 12h4M18 12h4M12 2v4M12 18v4M12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8z',
    fit: 'M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5',
    eyeOff: 'M3 3l18 18M10.6 5.1A10.9 10.9 0 0 1 12 5c6 0 10 7 10 7a17.6 17.6 0 0 1-3.2 3.9M6.6 6.6C3.9 8.3 2 12 2 12s4 7 10 7c1.6 0 3.1-.4 4.4-1.1'
  };
  function Ic(name, size) {
    if (!IC[name]) return h(A.Icon, { name: name, size: size || 16 });
    return h('svg', { width: size || 16, height: size || 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', className: 'alt-icon', 'aria-hidden': 'true' },
      IC[name].split(/(?=M)/).map(function (d, i) { return h('path', { key: i, d: d }); }));
  }

  var TYPES = { carga: { label: 'Carga', c: 'var(--chart-2)' }, descarga: { label: 'Descarga', c: 'var(--chart-1)' }, espera: { label: 'Espera', c: 'var(--chart-5)' }, indefinida: { label: 'Indefinida', c: 'var(--chart-6)' } };
  var TOOLS = [
    { group: 'Seleção', items: [
      { id: 'select', label: 'Selecionar', key: 'V', icon: 'select', hint: 'Clique numa cerca para abrir no inspetor · arraste para mover' },
      { id: 'vertex', label: 'Editar vértices', key: 'A', icon: 'vertex', hint: 'Arraste vértices · ponto médio insere · Del remove' }] },
    { group: 'Desenho', items: [
      { id: 'polygon', label: 'Polígono', key: 'P', icon: 'polygon', hint: 'Clique para adicionar vértices · duplo clique fecha · Esc cancela' },
      { id: 'rect', label: 'Retângulo', key: 'R', icon: 'rect', hint: 'Arraste de canto a canto · Shift trava em quadrado' },
      { id: 'circle', label: 'Círculo por raio', key: 'C', icon: 'circle', hint: 'Clique no centro e arraste o raio (em metros)' },
      { id: 'free', label: 'Mão livre', key: 'F', icon: 'free', hint: 'Desenhe arrastando · simplificação de 2 m ao soltar' },
      { id: 'corridor', label: 'Corredor ao longo da rota', key: 'L', icon: 'corridor', hint: 'Desenhe uma linha ou escolha uma rota · largura em metros' }] },
    { group: 'Edição', items: [
      { id: 'hole', label: 'Adicionar furo', key: 'H', icon: 'hole', hint: 'Desenhe dentro da cerca para subtrair uma área de exclusão' },
      { id: 'union', label: 'Unir cercas', icon: 'union', hint: 'Selecione 2 ou mais cercas ou partes para unir' },
      { id: 'subtract', label: 'Subtrair área', icon: 'subtract', hint: 'Recorte a área de outra forma ou cerca' },
      { id: 'split', label: 'Dividir por linha', icon: 'split', hint: 'Desenhe uma linha que corte a cerca em duas' },
      { id: 'buffer', label: 'Expandir ou contrair', icon: 'buffer', hint: 'Buffer em metros com prévia tracejada' },
      { id: 'simplify', label: 'Simplificar vértices', icon: 'simplify', hint: 'Reduz vértices com tolerância ajustável' }] }
  ];
  var ALL_TOOLS = {}; TOOLS.forEach(function (g) { g.items.forEach(function (t) { ALL_TOOLS[t.id] = t; }); });

  var DEFAULT_OUTER = [[150, 110], [250, 95], [310, 140], [300, 210], [250, 205], [240, 250], [320, 280], [185, 350], [305, 360], [170, 300], [120, 290], [135, 200]];
  var DEFAULT_HOLE = [[205, 170], [245, 165], [250, 195], [210, 205]];
  var M_PER_UNIT = 1.6;
  var ROADS_X = [130, 340], ROADS_Y = [150, 330];

  /* ---------- geometria ---------- */
  function area(pts) { var s = 0; for (var i = 0; i < pts.length; i++) { var a = pts[i], b = pts[(i + 1) % pts.length]; s += a[0] * b[1] - b[0] * a[1]; } return Math.abs(s / 2); }
  function perim(pts) { var s = 0; for (var i = 0; i < pts.length; i++) { var a = pts[i], b = pts[(i + 1) % pts.length]; s += Math.hypot(b[0] - a[0], b[1] - a[1]); } return s; }
  function cross(o, a, b) { return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]); }
  function segX(p1, p2, p3, p4) {
    var d1 = cross(p3, p4, p1), d2 = cross(p3, p4, p2), d3 = cross(p1, p2, p3), d4 = cross(p1, p2, p4);
    if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) {
      var t = d1 / (d1 - d2); return [p1[0] + t * (p2[0] - p1[0]), p1[1] + t * (p2[1] - p1[1])];
    }
    return null;
  }
  function selfX(pts) {
    var out = [], n = pts.length;
    for (var i = 0; i < n; i++) for (var j = i + 2; j < n; j++) {
      if (i === 0 && j === n - 1) continue;
      var p = segX(pts[i], pts[(i + 1) % n], pts[j], pts[(j + 1) % n]);
      if (p) out.push({ a: i, b: j, p: p });
    }
    return out;
  }
  function inside(pt, poly) {
    var c = false;
    for (var i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      var xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
      if (((yi > pt[1]) !== (yj > pt[1])) && (pt[0] < (xj - xi) * (pt[1] - yi) / (yj - yi) + xi)) c = !c;
    }
    return c;
  }
  function ll(p) { return [-22.6 - (p[1] - 100) * 0.000016, -47.19 + (p[0] - 100) * 0.000017]; }
  function fmtLL(v) { return nf(v, 4); }
  function d(pts) { return 'M' + pts.map(function (p) { return p[0] + ' ' + p[1]; }).join(' L') + ' Z'; }
  function untangle(pts, x) {
    /* desfaz o cruzamento invertendo o trecho entre os dois lados (2-opt) */
    var a = x.a + 1, b = x.b, n = pts.slice();
    var mid = n.slice(a, b + 1).reverse();
    return n.slice(0, a).concat(mid, n.slice(b + 1));
  }

  function PplMapaAreas(p) {
    var hist = R.useState({ stack: [p.outer || DEFAULT_OUTER], i: 0 }), H = hist[0], setH = hist[1];
    var outer = H.stack[H.i], hole = p.hole === undefined ? DEFAULT_HOLE : p.hole;
    var tl = R.useState('vertex'), tool = tl[0], setTool = tl[1];
    var sv = R.useState(6), selV = sv[0], setSelV = sv[1];
    var dr = R.useState(null), drag = dr[0], setDrag = dr[1];
    var cu = R.useState([340, 280]), cursor = cu[0], setCursor = cu[1];
    var sn = R.useState(true), snap = sn[0], setSnap = sn[1];
    var gd = R.useState(false), grid = gd[0], setGrid = gd[1];
    var me = R.useState(false), measure = me[0], setMeasure = me[1];
    var sh = R.useState({ x: 340, y: 280, kind: 'rua' }), snapHit = sh[0], setSnapHit = sh[1];
    var nm = R.useState(p.name || 'Vidroporto PF'), name = nm[0], setName = nm[1];
    var ty = R.useState(p.type || 'descarga'), type = ty[0], setType = ty[1];
    var to = R.useState(p.tolerance == null ? 30 : p.tolerance), tol = to[0], setTol = to[1];
    var lv = R.useState({ carga: true, espera: true, rotas: false }), layers = lv[0], setLayers = lv[1];
    var tp = R.useState(null), tip = tp[0], setTip = tp[1];
    var sv2 = R.useState(false), saved = sv2[0], setSaved = sv2[1];
    var gRef = R.useRef(null);
    var col = (TYPES[type] || TYPES.indefinida).c;

    function commit(next) { var st = H.stack.slice(0, H.i + 1).concat([next]); setH({ stack: st, i: st.length - 1 }); setSaved(false); }
    function replace(next) { var st = H.stack.slice(); st[H.i] = next; setH({ stack: st, i: H.i }); }
    function undo() { if (H.i > 0) setH({ stack: H.stack, i: H.i - 1 }); }
    function redo() { if (H.i < H.stack.length - 1) setH({ stack: H.stack, i: H.i + 1 }); }

    var xs = selfX(outer);
    var holeOk = hole ? hole.every(function (q) { return inside(q, outer); }) : true;
    var areaM2 = (area(outer) - (hole ? area(hole) : 0)) * M_PER_UNIT * M_PER_UNIT;
    var holeM2 = hole ? area(hole) * M_PER_UNIT * M_PER_UNIT : 0;
    var perM = perim(outer) * M_PER_UNIT;
    var minArea = p.minArea || 100;
    var checks = [
      { ok: outer.length >= 3, label: 'Polígono fechado' },
      { ok: !xs.length, label: xs.length ? 'Autointerseção no lado ' + (xs[0].a + 1) + '–' + (xs[0].a + 2) : 'Sem autointerseção', fix: xs.length ? function () { commit(untangle(outer, xs[0])); } : null },
      { ok: holeOk, label: holeOk ? 'Furo dentro do contorno' : 'Furo fora do contorno' },
      { ok: areaM2 >= minArea, label: areaM2 >= minArea ? 'Área mínima de ' + nf(minArea) + ' m²' : 'Área abaixo de ' + nf(minArea) + ' m²' },
      { ok: true, warn: false, label: 'Sem sobreposição com outras cercas' }
    ];
    var errors = checks.filter(function (c) { return !c.ok; }).length;

    function toLocal(e) {
      var g = gRef.current; if (!g) return null;
      var m = g.getScreenCTM(); if (!m) return null;
      var pt = g.ownerSVGElement.createSVGPoint(); pt.x = e.clientX; pt.y = e.clientY;
      var r = pt.matrixTransform(m.inverse()); return [Math.round(r.x), Math.round(r.y)];
    }
    function applySnap(pt, alt) {
      if (!snap || alt) { setSnapHit(null); return pt; }
      var best = null, R2 = 10;
      outer.forEach(function (q, i) { if (i !== (drag && drag.i)) { var dd = Math.hypot(q[0] - pt[0], q[1] - pt[1]); if (dd < R2) { R2 = dd; best = { x: q[0], y: q[1], kind: 'vértice' }; } } });
      if (!best) {
        ROADS_X.forEach(function (x) { if (Math.abs(x - pt[0]) < 10) best = { x: x, y: pt[1], kind: 'rua' }; });
        ROADS_Y.forEach(function (y) { if (Math.abs(y - pt[1]) < 10) best = { x: best ? best.x : pt[0], y: y, kind: 'rua' }; });
      }
      if (!best && grid) best = { x: Math.round(pt[0] / 20) * 20, y: Math.round(pt[1] / 20) * 20, kind: 'grade' };
      setSnapHit(best);
      return best ? [best.x, best.y] : pt;
    }
    function onMove(e) {
      var pt = toLocal(e); if (!pt) return;
      setCursor(pt);
      if (drag) { var q = applySnap(pt, e.altKey), n = outer.slice(); n[drag.i] = q; replace(n); }
    }
    function startDrag(i, e) {
      if (tool !== 'vertex') return;
      e.stopPropagation(); setSelV(i);
      try { e.currentTarget.ownerSVGElement.setPointerCapture(e.pointerId); } catch (x) {}
      commit(outer.slice()); setDrag({ i: i });
    }
    function endDrag() { if (drag) { setDrag(null); } }
    function insertMid(i) {
      if (tool !== 'vertex') return;
      var a = outer[i], b = outer[(i + 1) % outer.length], n = outer.slice();
      n.splice(i + 1, 0, [Math.round((a[0] + b[0]) / 2), Math.round((a[1] + b[1]) / 2)]);
      commit(n); setSelV(i + 1);
    }
    function removeSel() { if (selV != null && outer.length > 3) { var n = outer.slice(); n.splice(selV, 1); commit(n); setSelV(Math.min(selV, n.length - 1)); } }
    function onKey(e) {
      if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;
      var k = e.key.toUpperCase();
      if ((e.ctrlKey || e.metaKey) && k === 'Z') { e.preventDefault(); return e.shiftKey ? redo() : undo(); }
      if (e.key === 'Delete' || e.key === 'Backspace') { e.preventDefault(); return removeSel(); }
      for (var id in ALL_TOOLS) if (ALL_TOOLS[id].key === k && !e.ctrlKey && !e.metaKey) { setTool(id); e.preventDefault(); return; }
    }
    function exportGeo() {
      var gj = { type: 'Feature', properties: { nome: name, tipo: type, toleranciaM: tol }, geometry: { type: 'Polygon', coordinates: [outer.concat([outer[0]]).map(function (q) { var l = ll(q); return [l[1], l[0]]; })].concat(hole ? [hole.concat([hole[0]]).map(function (q) { var l = ll(q); return [l[1], l[0]]; })] : []) } };
      if (p.onExport) return p.onExport(gj);
      try { var a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([JSON.stringify(gj, null, 2)], { type: 'application/geo+json' })); a.download = name.replace(/\s+/g, '-').toLowerCase() + '.geojson'; a.click(); } catch (x) {}
    }
    function save() { if (errors) return; setSaved(true); if (p.onSave) p.onSave({ name: name, type: type, tolerance: tol, outer: outer, hole: hole }); }

    var t = ALL_TOOLS[tool];
    var status = drag ? 'Arrastando vértice ' + (drag.i + 1) : selV != null && tool === 'vertex' ? 'Vértice ' + (selV + 1) + ' selecionado' : t.label;
    var mods = tool === 'vertex' ? [['Alt', 'sem snap'], ['Shift', 'multisseleção'], ['Del', 'remove']] : tool === 'polygon' ? [['Enter', 'fecha'], ['Backspace', 'desfaz ponto'], ['Esc', 'cancela']] : tool === 'rect' ? [['Shift', 'quadrado']] : [['Esc', 'cancela']];
    var ll0 = ll(cursor);
    var edgeLen = selV != null ? Math.round(Math.hypot(outer[selV][0] - outer[(selV - 1 + outer.length) % outer.length][0], outer[selV][1] - outer[(selV - 1 + outer.length) % outer.length][1]) * M_PER_UNIT) : null;
    var prevV = selV != null ? outer[(selV - 1 + outer.length) % outer.length] : null;
    var vRows = selV == null ? outer.slice(0, 3).map(function (q, i) { return i; }) : [selV - 1, selV, selV + 1].map(function (i) { return (i + outer.length) % outer.length; });

    return h('div', { className: cx('alt-panel', 'alt-ppl-mapa-areas', p.className), style: p.style, onKeyDown: onKey, tabIndex: -1 },
      /* barra superior */
      h('div', { className: 'alt-ppl-mapa-areas__top' },
        h('div', { className: 'alt-ppl-mapa-areas__ttl' }, h('span', { className: 'alt-dot', style: { color: col } }), h('b', null, name),
          h('small', null, saved ? '· salva' : '· editando · alterações não salvas')),
        h('div', { className: 'alt-ppl-mapa-areas__grp', role: 'group', 'aria-label': 'Histórico' },
          h('button', { type: 'button', className: 'alt-ppl-mapa-areas__tb', 'aria-label': 'Desfazer (Ctrl+Z)', title: 'Desfazer (Ctrl+Z)', disabled: H.i === 0, onClick: undo }, Ic('undo')),
          h('button', { type: 'button', className: 'alt-ppl-mapa-areas__tb', 'aria-label': 'Refazer (Ctrl+Shift+Z)', title: 'Refazer (Ctrl+Shift+Z)', disabled: H.i >= H.stack.length - 1, onClick: redo }, Ic('redo'))),
        h('div', { className: 'alt-ppl-mapa-areas__grp', role: 'group', 'aria-label': 'Precisão' },
          [['Snap', 'magnet', snap, setSnap], ['Grade', 'gridIc', grid, setGrid], ['Medir', 'ruler', measure, setMeasure]].map(function (x) {
            return h('button', { key: x[0], type: 'button', className: 'alt-ppl-mapa-areas__tg', 'aria-pressed': x[2] ? 'true' : 'false', onClick: function () { x[3](!x[2]); } }, Ic(x[1], 15), x[0]);
          })),
        h('div', { className: 'alt-ppl-mapa-areas__grp', role: 'group', 'aria-label': 'Arquivo' },
          h('button', { type: 'button', className: 'alt-ppl-mapa-areas__tg', onClick: p.onImport, title: 'GeoJSON, KML/KMZ ou WKT' }, Ic('upload', 15), 'Importar'),
          h('button', { type: 'button', className: 'alt-ppl-mapa-areas__tg', onClick: exportGeo }, Ic('download', 15), 'Exportar'))),
      /* mapa */
      h('div', { className: 'alt-ppl-mapa-areas__map' },
        h('svg', { className: cx('alt-ppl-mapa-areas__svg', 'is-' + tool, drag && 'is-dragging'), viewBox: '0 -55 960 580', preserveAspectRatio: 'xMidYMid slice', role: 'img', 'aria-label': 'Mapa com a cerca ' + name + ' em edição',
          onPointerMove: onMove, onPointerUp: endDrag, onPointerLeave: endDrag },
          h('defs', null,
            h('pattern', { id: 'alt-pma-hatch', width: 6, height: 6, patternUnits: 'userSpaceOnUse', patternTransform: 'rotate(45)' }, h('path', { d: 'M0 0v6', className: 'alt-ppl-mapa-areas__hatch' })),
            h('pattern', { id: 'alt-pma-grid', width: 20, height: 20, patternUnits: 'userSpaceOnUse' }, h('path', { d: 'M20 0H0V20', fill: 'none', className: 'alt-ppl-mapa-areas__gridln' }))),
          h('g', { className: 'alt-ppl-mapa-areas__base', 'aria-hidden': 'true' },
            [[20, -50, 180, 90], [400, -50, 160, 90], [20, 20, 180, 110], [20, 170, 180, 140], [20, 350, 180, 110], [20, 480, 180, 60], [400, 480, 160, 60], [560, 20, 150, 110], [560, 170, 150, 140], [560, 350, 150, 110], [760, 20, 180, 110], [760, 350, 180, 110]].map(function (b, i) { return h('rect', { key: i, x: b[0], y: b[1], width: b[2], height: b[3], rx: 6, className: 'alt-ppl-mapa-areas__block' }); }),
            h('path', { d: 'M700 160 C760 150 820 190 900 180 L960 190 V340 H720 Z', className: 'alt-ppl-mapa-areas__park' }),
            h('path', { d: 'M620 -60 C635 120 685 220 715 260 S800 330 960 320', className: 'alt-ppl-mapa-areas__water' }),
            h('path', { d: 'M0 150H960M0 330H960M375 -60V530M585 -60V530M245 -60V530', className: 'alt-ppl-mapa-areas__road' }),
            h('path', { d: 'M0 60 C200 90 420 40 960 110', className: 'alt-ppl-mapa-areas__road alt-ppl-mapa-areas__road--minor' }),
            h('text', { x: 60, y: 142, className: 'alt-ppl-mapa-areas__lbl' }, 'Av. Brasil'), h('text', { x: 760, y: 322, className: 'alt-ppl-mapa-areas__lbl' }, 'Rio Mogi-Guaçu')),
          grid ? h('rect', { y: -55, width: 960, height: 580, fill: 'url(#alt-pma-grid)' }) : null,
          h('g', { ref: gRef, transform: 'translate(245 0)' },
            layers.carga ? h('g', null, h('circle', { cx: -95, cy: 70, r: 40, className: 'alt-ppl-mapa-areas__other', style: { '--c': TYPES.carga.c } }), h('text', { x: -95, y: 74, textAnchor: 'middle', className: 'alt-ppl-mapa-areas__olbl' }, 'Terminal Paulínia')) : null,
            layers.espera ? h('g', null, h('rect', { x: 430, y: 372, width: 120, height: 76, rx: 3, className: 'alt-ppl-mapa-areas__other', style: { '--c': TYPES.espera.c } }), h('text', { x: 490, y: 414, textAnchor: 'middle', className: 'alt-ppl-mapa-areas__olbl' }, 'Pátio km 102')) : null,
            layers.rotas ? h('path', { d: 'M-245 420 C-60 400 120 430 260 380 S520 250 715 230', className: 'alt-ppl-mapa-areas__route' }) : null,
            h('path', { d: d(outer) + (hole ? ' ' + d(hole) : ''), fillRule: 'evenodd', className: 'alt-ppl-mapa-areas__fill', style: { '--c': col } }),
            h('path', { d: d(outer), className: 'alt-ppl-mapa-areas__edge', style: { '--c': col } }),
            hole ? h('path', { d: d(hole), className: cx('alt-ppl-mapa-areas__hole', !holeOk && 'is-bad'), style: { '--c': col } }) : null,
            xs.map(function (x, i) {
              var a1 = outer[x.a], a2 = outer[(x.a + 1) % outer.length], b1 = outer[x.b], b2 = outer[(x.b + 1) % outer.length];
              return h('g', { key: 'x' + i }, h('path', { d: 'M' + a1 + 'L' + a2 + 'M' + b1 + 'L' + b2, className: 'alt-ppl-mapa-areas__bad' }), h('circle', { cx: x.p[0], cy: x.p[1], r: 5, className: 'alt-ppl-mapa-areas__badpt' }));
            }),
            tool === 'vertex' ? outer.map(function (a, i) {
              var b = outer[(i + 1) % outer.length];
              return h('circle', { key: 'm' + i, cx: (a[0] + b[0]) / 2, cy: (a[1] + b[1]) / 2, r: 4, className: 'alt-ppl-mapa-areas__mid', onClick: function () { insertMid(i); } }, h('title', null, 'Inserir vértice'));
            }) : null,
            measure && selV != null ? h('g', null, h('path', { d: 'M' + outer[0] + 'L' + outer[selV], className: 'alt-ppl-mapa-areas__meas' }),
              h('text', { x: (outer[0][0] + outer[selV][0]) / 2 + 6, y: (outer[0][1] + outer[selV][1]) / 2 - 6, className: 'alt-ppl-mapa-areas__dim' }, nf(Math.hypot(outer[0][0] - outer[selV][0], outer[0][1] - outer[selV][1]) * M_PER_UNIT) + ' m')) : null,
            hole ? hole.map(function (q, i) { return h('rect', { key: 'h' + i, x: q[0] - 4, y: q[1] - 4, width: 8, height: 8, rx: 2, className: 'alt-ppl-mapa-areas__v', style: { '--c': col } }); }) : null,
            outer.map(function (q, i) {
              var s = i === selV, sz = s ? 16 : 10;
              return h('rect', { key: 'v' + i, x: q[0] - sz / 2, y: q[1] - sz / 2, width: sz, height: sz, rx: s ? 3 : 2, className: cx('alt-ppl-mapa-areas__v', s && 'is-sel'), style: { '--c': col },
                tabIndex: tool === 'vertex' ? 0 : undefined, role: tool === 'vertex' ? 'button' : undefined, 'aria-label': 'Vértice ' + (i + 1),
                onPointerDown: function (e) { startDrag(i, e); }, onFocus: function () { setSelV(i); } });
            }),
            prevV && tool === 'vertex' ? h('text', { x: (prevV[0] + outer[selV][0]) / 2 + 8, y: (prevV[1] + outer[selV][1]) / 2 + 4, className: 'alt-ppl-mapa-areas__dim' }, nf(edgeLen) + ' m') : null,
            snapHit && selV != null && snap ? h('g', null,
              h('path', { d: 'M' + outer[selV] + 'L' + snapHit.x + ' ' + snapHit.y, className: 'alt-ppl-mapa-areas__snapln' }),
              h('circle', { cx: snapHit.x, cy: snapHit.y, r: 6, className: 'alt-ppl-mapa-areas__snappt' }),
              h('g', { transform: 'translate(' + (snapHit.x + 10) + ' ' + (snapHit.y - 18) + ')' }, h('rect', { width: 70, height: 18, rx: 9, className: 'alt-ppl-mapa-areas__snapbg' }), h('text', { x: 35, y: 12.5, textAnchor: 'middle', className: 'alt-ppl-mapa-areas__snaptx' }, 'Snap · ' + snapHit.kind))) : null,
            xs.length ? h('g', { transform: 'translate(' + (xs[0].p[0] - 128) + ' ' + (xs[0].p[1] + 2) + ')' }, h('rect', { width: 112, height: 20, rx: 10, className: 'alt-ppl-mapa-areas__warnbg' }), h('text', { x: 56, y: 14, textAnchor: 'middle', className: 'alt-ppl-mapa-areas__warntx' }, 'Autointerseção')) : null)),
        /* trilho de ferramentas */
        h('div', { className: 'alt-popover alt-ppl-mapa-areas__rail', role: 'toolbar', 'aria-orientation': 'vertical', 'aria-label': 'Ferramentas' },
          TOOLS.map(function (g, gi) {
            return [gi ? h('span', { key: 's' + gi, className: 'alt-ppl-mapa-areas__sep', 'aria-hidden': 'true' }) : null].concat(g.items.map(function (it) {
              return h('button', { key: it.id, type: 'button', className: 'alt-ppl-mapa-areas__tool', 'aria-pressed': tool === it.id ? 'true' : 'false', 'aria-label': it.label + (it.key ? ' (' + it.key + ')' : ''),
                onClick: function () { setTool(it.id); if (p.onToolChange) p.onToolChange(it.id); },
                onMouseEnter: function () { setTip(it.id); }, onMouseLeave: function () { setTip(null); }, onFocus: function () { setTip(it.id); }, onBlur: function () { setTip(null); } },
                Ic(it.icon, 17), it.key ? h('span', { className: 'alt-ppl-mapa-areas__k' }, it.key) : null,
                tip === it.id ? h('span', { className: 'alt-ppl-mapa-areas__rtip', role: 'tooltip' }, it.label, it.key ? h('kbd', null, it.key) : null) : null);
            }));
          })),
        h('div', { className: 'alt-popover alt-ppl-mapa-areas__meas-box alt-num', 'aria-live': 'polite' },
          h('div', null, h('b', null, nf(areaM2 / 10000, 2) + ' ha'), h('span', null, hole ? 'Área (−' + nf(holeM2 / 10000, 2) + ' do furo)' : 'Área')),
          h('div', null, h('b', null, nf(perM / 1000, 2) + ' km'), h('span', null, 'Perímetro')),
          h('div', null, h('b', null, outer.length + (hole ? hole.length : 0)), h('span', null, 'Vértices'))),
        h('div', { className: 'alt-popover alt-ppl-mapa-areas__zoom' },
          h('button', { type: 'button', className: 'alt-ppl-mapa-areas__tool', 'aria-label': 'Aproximar' }, h(A.Icon, { name: 'plus', size: 16 })),
          h('button', { type: 'button', className: 'alt-ppl-mapa-areas__tool', 'aria-label': 'Afastar' }, h(A.Icon, { name: 'minus', size: 16 })),
          h('button', { type: 'button', className: 'alt-ppl-mapa-areas__tool', 'aria-label': 'Enquadrar cerca' }, Ic('fit', 16))),
        h('div', { className: 'alt-ppl-mapa-areas__scale alt-num', 'aria-hidden': 'true' }, h('i'), '200 m')),
      /* barra de status */
      h('div', { className: 'alt-ppl-mapa-areas__bar' },
        h('b', null, status),
        h('span', { className: 'alt-ppl-mapa-areas__bar-hint' }, t.hint),
        mods.map(function (m) { return h('span', { key: m[0], className: 'alt-ppl-mapa-areas__mod' }, h('kbd', null, m[0]), m[1]); }),
        h('span', { className: 'alt-ppl-mapa-areas__xy alt-num' }, fmtLL(ll0[0]) + ' · ' + fmtLL(ll0[1]))),
      /* inspetor */
      h('aside', { className: 'alt-ppl-mapa-areas__ins', 'aria-label': 'Propriedades da cerca' },
        h('div', { className: 'alt-ppl-mapa-areas__sc alt-scroll' },
          h('label', { className: 'alt-field' }, h('span', { className: 'alt-field__label' }, 'Nome'),
            h('span', { className: 'alt-ctl alt-ctl--sm' }, h('input', { value: name, onChange: function (e) { setName(e.target.value); setSaved(false); } }))),
          h('div', { className: 'alt-ppl-mapa-areas__two' },
            h('label', { className: 'alt-field' }, h('span', { className: 'alt-field__label' }, 'Tipo'),
              h('span', { className: 'alt-ctl alt-ctl--sm' }, h('span', { className: 'alt-dot', style: { color: col } }),
                h('select', { value: type, onChange: function (e) { setType(e.target.value); setSaved(false); } }, Object.keys(TYPES).map(function (k) { return h('option', { key: k, value: k }, TYPES[k].label); })),
                h(A.Icon, { name: 'chevron-down', size: 14 }))),
            h('label', { className: 'alt-field' }, h('span', { className: 'alt-field__label' }, 'Tolerância'),
              h('span', { className: 'alt-ctl alt-ctl--sm alt-num' }, h('input', { type: 'number', min: 0, value: tol, onChange: function (e) { setTol(e.target.value); } }), h('span', { className: 'alt-ppl-mapa-areas__unit' }, 'm')))),
          h('div', { className: 'alt-ppl-mapa-areas__two alt-num' },
            h('div', { className: 'alt-ppl-mapa-areas__stat' }, h('b', null, nf(areaM2 / 10000, 2) + ' ha'), h('span', null, 'Área')),
            h('div', { className: 'alt-ppl-mapa-areas__stat' }, h('b', null, nf(perM / 1000, 2) + ' km'), h('span', null, 'Perímetro'))),
          h('div', { className: 'alt-ppl-mapa-areas__sl' }, 'Validação', h('em', { className: errors ? 'is-bad' : 'is-ok' }, errors ? errors + (errors === 1 ? ' problema' : ' problemas') : 'Tudo certo')),
          h('ul', { className: 'alt-ppl-mapa-areas__chk' }, checks.map(function (c, i) {
            return h('li', { key: i, className: c.ok ? 'is-ok' : 'is-bad' }, h(A.Icon, { name: c.ok ? 'check' : 'alert', size: 14 }), h('span', null, c.label),
              c.fix ? h('button', { type: 'button', className: 'alt-ppl-mapa-areas__fix', onClick: c.fix }, 'Corrigir') : null);
          })),
          h('div', { className: 'alt-ppl-mapa-areas__sl' }, 'Vértices', h('em', { className: 'alt-num' }, outer.length)),
          h('div', { className: 'alt-ppl-mapa-areas__vx alt-num', role: 'listbox', 'aria-label': 'Vértices' }, vRows.map(function (i) {
            var l = ll(outer[i]);
            return h('button', { key: i, type: 'button', role: 'option', 'aria-selected': i === selV ? 'true' : 'false', onClick: function () { setSelV(i); setTool('vertex'); } },
              h('span', null, i + 1), h('span', null, fmtLL(l[0])), h('span', null, fmtLL(l[1])));
          })),
          h('div', { className: 'alt-ppl-mapa-areas__sl' }, 'Camadas'),
          h('ul', { className: 'alt-ppl-mapa-areas__lyr' },
            h('li', { className: 'is-on' }, h('span', { className: 'alt-dot', style: { color: col } }), h('span', null, name), h(A.Icon, { name: 'edit', size: 14 })),
            [['carga', 'Terminal Paulínia', TYPES.carga.c, false], ['espera', 'Pátio km 102', TYPES.espera.c, true], ['rotas', 'Rotas planejadas', TYPES.indefinida.c, false]].map(function (L) {
              var on = layers[L[0]];
              return h('li', { key: L[0], className: on ? null : 'is-off' }, h('span', { className: 'alt-dot', style: { color: L[2] } }), h('span', null, L[1]),
                L[3] ? h('span', { className: 'alt-ppl-mapa-areas__lock', title: 'Bloqueada' }, h(A.Icon, { name: 'lock', size: 13 })) : null,
                h('button', { type: 'button', className: 'alt-ppl-mapa-areas__eye', 'aria-pressed': on ? 'true' : 'false', 'aria-label': (on ? 'Ocultar ' : 'Mostrar ') + L[1],
                  onClick: function () { var n = Object.assign({}, layers); n[L[0]] = !on; setLayers(n); } }, on ? h(A.Icon, { name: 'eye', size: 14 }) : Ic('eyeOff', 14)));
            }))),
        h('div', { className: 'alt-ppl-mapa-areas__ft' },
          h('button', { type: 'button', className: 'alt-tbtn', onClick: p.onDiscard }, 'Descartar'),
          h('span', { className: 'alt-ppl-mapa-areas__save', title: errors ? 'Corrija ' + (xs.length ? 'a autointerseção' : 'os erros de validação') + ' para salvar' : undefined },
            h(A.Button, { size: 'sm', arrow: false, disabled: !!errors, 'aria-disabled': errors ? 'true' : undefined, onClick: save }, h(A.Icon, { name: 'check', size: 15 }), saved ? 'Cerca salva' : 'Salvar cerca')))));
  }
  PplMapaAreas.TYPES = TYPES;
  A.PplMapaAreas = PplMapaAreas;
})();


/* ---- PplPosicaoFrota ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  var STATUS = {
    vazio: { label: 'Deslocamento vazio', c: 'var(--chart-4)' },
    carga: { label: 'Em carregamento', c: 'var(--chart-2)' },
    descarga: { label: 'Em descarga', c: 'var(--chart-5)' },
    transito: { label: 'Em trânsito', c: 'var(--signal-success)' },
    parado: { label: 'Parado', c: 'var(--signal-warning)' },
    semsinal: { label: 'Sem sinal', c: 'var(--slate)', hollow: true }
  };
  var ORDER = ['vazio', 'carga', 'descarga', 'transito', 'parado', 'semsinal'];
  var ALERTS = {
    velocidade: { label: 'Excesso de velocidade', icon: 'gauge' },
    parada: { label: 'Parada não programada', d: 'M6 6h12v12H6z' },
    janela: { label: 'Janela de descarga em risco', icon: 'clock' },
    comunicacao: { label: 'Sem comunicação', icon: 'wifi-off' },
    desvio: { label: 'Desvio de rota', d: 'M4 19 10 5l4 8 6-4' }
  };
  var GPSQ = { bom: ['Bom', 4, 'success'], regular: ['Regular', 3, 'warning'], fraco: ['Fraco', 2, 'error'], sem: ['Sem sinal', 0, 'neutral'] };
  var DEFAULT_COUNTS = { vazio: 18, carga: 9, descarga: 7, transito: 61, parado: 11, semsinal: 4 };

  var DEFAULT_VEHICLES = [
    { id: 'abc', placa: 'ABC-1D23', motorista: 'João Pereira', status: 'transito', alerts: ['velocidade'], viagem: '88213', cliente: 'Vidroporto', origem: 'Jundiaí/SP', destino: 'Sorocaba/SP', eta: '16:05', desvio: 12, progresso: 62, ultima: '30/09 14:36', ha: 'há 1 min', age: 1, gps: { q: 'bom', sat: 11, age: '1 min' }, local: 'SP-075, km 12 · Itu/SP', lat: -23.1857, lon: -47.1102, vel: 78, ign: true, x: 236, y: 112, hd: 120, o: [150, 70], dst: [420, 210] },
    { id: 'qrt', placa: 'QRT-4F56', motorista: 'Marta Souza', status: 'descarga', alerts: [], viagem: '88240', cliente: 'Owens-Illinois', origem: 'Porto Ferreira/SP', destino: 'São Paulo/SP', eta: null, desvio: 0, progresso: 100, ultima: '30/09 14:31', ha: 'há 6 min', age: 6, gps: { q: 'bom', sat: 9, age: '6 min' }, local: 'Av. Pres. Wilson, 3.200 · São Paulo/SP', lat: -23.5329, lon: -46.6395, vel: 0, ign: false, x: 1018, y: 168, o: [600, 30], dst: [1018, 168] },
    { id: 'jhk', placa: 'JHK-7G89', motorista: 'Carlos Lima', status: 'parado', parado: '35 min', alerts: ['parada', 'janela'], viagem: '88227', cliente: 'Vidroporto', origem: 'Campinas/SP', destino: 'Santos/SP', eta: '17:40', desvio: 48, progresso: 41, ultima: '30/09 14:02', ha: 'há 35 min', age: 35, gps: { q: 'regular', sat: 7, age: '2 min' }, local: 'Posto Graal, SP-348 km 102 · Jundiaí/SP', lat: -23.0012, lon: -47.0931, vel: 0, ign: false, x: 352, y: 128, o: [120, 72], dst: [1000, 176] },
    { id: 'lmn', placa: 'LMN-0P12', motorista: 'Rafael Costa', status: 'carga', alerts: [], viagem: '88251', cliente: 'Saint-Gobain', origem: 'Mauá/SP', destino: 'Rio Claro/SP', eta: '19:10', desvio: 0, progresso: 5, ultima: '30/09 14:34', ha: 'há 3 min', age: 3, gps: { q: 'bom', sat: 12, age: '3 min' }, local: 'CD Saint-Gobain, Av. Papa João XXIII · Mauá/SP', lat: -23.6677, lon: -46.4613, vel: 0, ign: true, x: 204, y: 38, o: [204, 38], dst: [40, 200] },
    { id: 'def', placa: 'DEF-3G45', motorista: 'Pedro Alves', status: 'vazio', alerts: [], viagem: null, origem: 'Santos/SP', destino: 'Paulínia/SP', eta: '15:20', desvio: null, progresso: null, ultima: '30/09 14:35', ha: 'há 2 min', age: 2, gps: { q: 'bom', sat: 10, age: '2 min' }, local: 'SP-055, km 248 · Cubatão/SP', lat: -23.8211, lon: -46.3981, vel: 64, ign: true, x: 548, y: 24, hd: 290 },
    { id: 'gtr', placa: 'GTR-5H67', motorista: 'Ana Moura', status: 'transito', alerts: ['desvio'], viagem: '88233', cliente: 'Saint-Gobain', origem: 'Limeira/SP', destino: 'Guarulhos/SP', eta: '18:25', desvio: 22, progresso: 74, ultima: '30/09 14:36', ha: 'há 1 min', age: 1, gps: { q: 'fraco', sat: 4, age: '1 min' }, local: 'SP-330, km 61 · Jundiaí/SP', lat: -23.1702, lon: -46.9121, vel: 86, ign: true, x: 742, y: 96, hd: 95 },
    { id: 'xyz', placa: 'XYZ-9K87', motorista: 'Luís Santos', status: 'semsinal', alerts: ['comunicacao', 'desvio'], viagem: '88199', cliente: 'Vidroporto', origem: 'Paulínia/SP', destino: 'Ribeirão Preto/SP', eta: null, desvio: null, etaNote: 'indeterminado', progresso: 55, ultima: '30/09 12:48', ha: 'há 1 h 49 min', age: 109, gps: { q: 'sem', sat: 0, age: '1 h 49 min' }, local: 'SP-330, km 187 · Leme/SP', lat: -22.4103, lon: -47.5602, vel: null, ign: null, x: 128, y: 150 }
  ];

  function Svg(d, size) { return h('svg', { width: size || 14, height: size || 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', className: 'alt-icon', 'aria-hidden': 'true' }, h('path', { d: d })); }
  function AlertIc(k) { var a = ALERTS[k]; return a.icon ? h(A.Icon, { name: a.icon, size: 14 }) : Svg(a.d); }
  function stLabel(v) { return v.status === 'parado' && v.parado ? 'Parado há ' + v.parado : STATUS[v.status].label; }
  function fmtClock(s) { var m = Math.floor(s / 60), r = s % 60; return m + ':' + (r < 10 ? '0' : '') + r; }

  function CopyBtn(p) {
    var s = R.useState(null), st = s[0], set = s[1];
    function go(e) {
      e.stopPropagation();
      function done(ok) { set(ok ? 'ok' : 'fail'); setTimeout(function () { set(null); }, 1500); }
      try { navigator.clipboard.writeText(String(p.text).trim()).then(function () { done(true); }, function () { done(false); }); } catch (x) { done(false); }
    }
    return h('span', { className: 'alt-ppl-posicao-frota__cpw' },
      h('button', { type: 'button', className: cx('alt-ppl-posicao-frota__cp', st && 'is-' + st), 'aria-label': 'Copiar ' + p.kind + ' ' + p.text, title: 'Copiar ' + p.kind, onClick: go },
        st === 'ok' ? h(A.Icon, { name: 'check', size: 14 }) : h(A.Icon, { name: 'copy', size: 14 })),
      st ? h('span', { className: 'alt-ppl-posicao-frota__cpmsg', role: 'status' }, st === 'ok' ? (p.kind === 'placa' ? 'Placa copiada' : 'Nome copiado') : 'Não foi possível copiar') : null);
  }

  function Marker(p) {
    var v = p.v, s = STATUS[v.status], sel = p.selected;
    var inner;
    if (s.hollow) inner = h('path', { d: 'M-3 -3.2a3 3 0 1 1 3 3.7v1.3M0 4.6v.2', className: 'alt-ppl-posicao-frota__mk-q' });
    else if (v.status === 'parado') inner = h('rect', { x: -3.5, y: -3.5, width: 7, height: 7, rx: 1.2, className: 'alt-ppl-posicao-frota__mk-in' });
    else if (v.status === 'carga') inner = h('path', { d: 'M0 4.5v-8M-3.5 -1l3.5-3.5 3.5 3.5', className: 'alt-ppl-posicao-frota__mk-ln' });
    else if (v.status === 'descarga') inner = h('path', { d: 'M0 -4.5v8M-3.5 1l3.5 3.5 3.5-3.5', className: 'alt-ppl-posicao-frota__mk-ln' });
    else inner = h('path', { d: 'M0 -5.5 4.5 4.5 0 2.2 -4.5 4.5z', transform: 'rotate(' + (v.hd || 0) + ')', className: 'alt-ppl-posicao-frota__mk-in' });
    return h('g', { transform: 'translate(' + v.x + ' ' + v.y + ')', className: cx('alt-ppl-posicao-frota__mk', sel && 'is-sel'), style: { '--c': s.c }, role: 'button', tabIndex: 0,
      'aria-label': v.placa + ', ' + stLabel(v) + (sel ? ' (selecionado)' : ''), 'aria-pressed': sel ? 'true' : 'false',
      onClick: function () { p.onSelect(v.id); }, onKeyDown: function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); p.onSelect(v.id); } } },
      sel ? h('circle', { r: 15, className: 'alt-ppl-posicao-frota__pulse' }) : null,
      s.hollow ? h('circle', { r: 9.5, className: 'alt-ppl-posicao-frota__mk-hollow' }) : [
        h('circle', { key: 'h', r: sel ? 17 : 12.5, className: 'alt-ppl-posicao-frota__mk-halo' }),
        h('circle', { key: 'b', r: sel ? 12 : 9.5, className: 'alt-ppl-posicao-frota__mk-body' })],
      sel ? h('circle', { r: 15.5, className: 'alt-ppl-posicao-frota__mk-ring' }) : null,
      inner,
      sel ? h('g', { transform: 'translate(0 -31)' }, h('rect', { x: -36, y: -10, width: 72, height: 20, rx: 10, className: 'alt-ppl-posicao-frota__mk-tagbg' }), h('text', { y: 4, textAnchor: 'middle', className: 'alt-ppl-posicao-frota__mk-tag' }, v.placa)) : null);
  }

  function Cluster(p) {
    var total = p.parts.reduce(function (s, x) { return s + x[1]; }, 0), a0 = -Math.PI / 2, r = 15;
    return h('g', { transform: 'translate(' + p.x + ' ' + p.y + ')', className: 'alt-ppl-posicao-frota__cl', role: 'button', tabIndex: 0, 'aria-label': 'Grupo de ' + total + ' veículos, aproximar', onClick: p.onClick },
      h('circle', { r: 18, className: 'alt-ppl-posicao-frota__cl-bg' }),
      p.parts.map(function (x, i) {
        var a1 = a0 + (x[1] / total) * Math.PI * 2, large = a1 - a0 > Math.PI ? 1 : 0;
        var d = 'M' + (r * Math.cos(a0)).toFixed(1) + ' ' + (r * Math.sin(a0)).toFixed(1) + 'A' + r + ' ' + r + ' 0 ' + large + ' 1 ' + (r * Math.cos(a1)).toFixed(1) + ' ' + (r * Math.sin(a1)).toFixed(1);
        a0 = a1; return h('path', { key: i, d: d, stroke: STATUS[x[0]].c, className: 'alt-ppl-posicao-frota__cl-arc' });
      }),
      h('text', { y: 4.5, textAnchor: 'middle', className: 'alt-ppl-posicao-frota__cl-n' }, total));
  }

  function PplPosicaoFrota(p) {
    var vehicles = p.vehicles || DEFAULT_VEHICLES, counts = p.counts || DEFAULT_COUNTS;
    var total = p.total || 110, inTrip = p.inTrip == null ? 87 : p.inTrip;
    var ctl = p.selected !== undefined;
    var ss = R.useState(p.defaultSelected === undefined ? 'jhk' : p.defaultSelected), sel = ctl ? p.selected : ss[0];
    var fs = R.useState([]), filt = fs[0], setFilt = fs[1];
    var qs = R.useState(''), q = qs[0], setQ = qs[1];
    var so = R.useState({ key: 'age', dir: 1 }), sort = so[0], setSort = so[1];
    var cd = R.useState(p.refreshIn == null ? 48 : p.refreshIn), left = cd[0], setLeft = cd[1];
    var rc = R.useState(p.receivedAt || '14:37:12'), recv = rc[0], setRecv = rc[1];
    var pv = R.useState(true), popOpen = pv[0], setPopOpen = pv[1];
    var ms = R.useState('mapa'), mapStyle = ms[0], setMapStyle = ms[1];
    var lf = R.useState({ cercas: true, transito: false }), lay = lf[0], setLay = lf[1];
    var rowRefs = R.useRef({});
    var interval = p.refreshInterval || 60;

    function refresh() {
      setLeft(interval);
      var d = new Date(), z = function (n) { return (n < 10 ? '0' : '') + n; };
      setRecv(z(d.getHours()) + ':' + z(d.getMinutes()) + ':' + z(d.getSeconds()));
      if (p.onRefresh) p.onRefresh();
    }
    R.useEffect(function () {
      if (p.autoRefresh === false) return;
      var t = setInterval(function () { setLeft(function (s) { if (s <= 1) { setTimeout(refresh, 0); return interval; } return s - 1; }); }, 1000);
      return function () { clearInterval(t); };
    }, []);

    function select(id, fromMap) {
      var n = id === sel ? null : id;
      if (!ctl) ss[1](n); setPopOpen(true);
      if (p.onSelect) p.onSelect(n);
      if (fromMap && n && rowRefs.current[n]) { try { rowRefs.current[n].scrollIntoView({ block: 'nearest' }); } catch (x) {} }
    }
    function toggle(k) { setFilt(filt.indexOf(k) >= 0 ? filt.filter(function (x) { return x !== k; }) : filt.concat(k)); }
    var ql = q.trim().toLowerCase();
    var shown = vehicles.filter(function (v) {
      if (filt.length && filt.indexOf(v.status) < 0) return false;
      if (!ql) return true;
      return [v.placa, v.motorista, v.viagem, v.cliente].some(function (x) { return x && String(x).toLowerCase().indexOf(ql) >= 0; });
    });
    var rows = shown.slice().sort(function (a, b) {
      var k = sort.key, x = a[k], y = b[k];
      if (x == null) return 1; if (y == null) return -1;
      return (typeof x === 'string' ? x.localeCompare(y) : x - y) * sort.dir;
    });
    var cur = vehicles.filter(function (v) { return v.id === sel; })[0];
    var curShown = cur && shown.indexOf(cur) >= 0;
    var SORT_LABEL = { age: 'última posição', placa: 'veículo', vel: 'velocidade', progresso: 'progresso' };
    function sortTh(key, label, cls) {
      var on = sort.key === key;
      return h('th', { className: cx(cls, on && 'is-on'), 'aria-sort': on ? (sort.dir === 1 ? (key === 'age' ? 'descending' : 'ascending') : (key === 'age' ? 'ascending' : 'descending')) : 'none' },
        h('button', { type: 'button', onClick: function () { setSort({ key: key, dir: on ? -sort.dir : 1 }); } }, label,
          on ? h(A.Icon, { name: sort.dir === 1 ? 'arrow-down' : 'arrow-up', size: 12 }) : null));
    }

    /* trajeto do selecionado */
    var route = null;
    if (curShown && cur.o && cur.dst) {
      var o = cur.o, v = [cur.x, cur.y], dd = cur.dst;
      route = {
        done: 'M' + o[0] + ' ' + o[1] + ' C' + (o[0] + (v[0] - o[0]) * .4) + ' ' + (o[1] + 30) + ' ' + (o[0] + (v[0] - o[0]) * .7) + ' ' + v[1] + ' ' + v[0] + ' ' + v[1],
        next: 'M' + v[0] + ' ' + v[1] + ' C' + (v[0] + (dd[0] - v[0]) * .35) + ' ' + (v[1] - 40) + ' ' + (v[0] + (dd[0] - v[0]) * .7) + ' ' + (dd[1] - 70) + ' ' + dd[0] + ' ' + dd[1], o: o, d: dd
      };
    }
    var popX = curShown ? (cur.x + 336 > 1100 ? cur.x - 332 : cur.x + 30) : 0, popY = curShown ? Math.max(6, Math.min(250 - 172, cur.y - 70)) : 0;

    return h('section', { className: cx('alt-panel', 'alt-ppl-posicao-frota', p.className), style: p.style, 'aria-label': 'Posição da frota' },
      /* barra */
      h('div', { className: 'alt-ppl-posicao-frota__top' },
        h('div', { className: 'alt-ppl-posicao-frota__ttl' }, h('h3', null, p.title || 'Posição da frota'), h('span', { className: 'alt-num' }, total + ' veículos · ' + inTrip + ' em viagem')),
        h('label', { className: 'alt-ctl alt-ctl--sm alt-ppl-posicao-frota__q' }, h(A.Icon, { name: 'search', size: 16 }),
          h('input', { type: 'search', placeholder: 'Placa, motorista, viagem, cliente…', value: q, onChange: function (e) { setQ(e.target.value); }, 'aria-label': 'Buscar veículo' })),
        h('button', { type: 'button', className: 'alt-tbtn', onClick: p.onOpenFilters }, h(A.Icon, { name: 'filter', size: 16 }), 'Filtros', p.advancedFilters !== 0 ? h('span', { className: 'alt-count' }, p.advancedFilters || 2) : null),
        h('button', { type: 'button', className: 'alt-tbtn alt-ppl-posicao-frota__upd', title: 'Atualizar agora', onClick: refresh }, h(A.Icon, { name: 'refresh', size: 16 }), h('span', { className: 'alt-num' }, 'Atualiza em ' + fmtClock(left)))),
      /* chips */
      h('div', { className: 'alt-ppl-posicao-frota__kcs', role: 'group', 'aria-label': 'Filtrar por status operacional' },
        ORDER.map(function (k) {
          var s = STATUS[k], on = filt.indexOf(k) >= 0;
          return h('button', { key: k, type: 'button', className: 'alt-ppl-posicao-frota__kc', style: { '--c': s.c }, 'aria-pressed': on ? 'true' : 'false', onClick: function () { toggle(k); } },
            h('span', { className: cx('alt-ppl-posicao-frota__kd', s.hollow && 'is-hollow') }), h('b', { className: 'alt-num' }, counts[k]), s.label);
        }),
        filt.length ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-ppl-posicao-frota__clr', onClick: function () { setFilt([]); } }, h(A.Icon, { name: 'x', size: 14 }), 'Limpar') : null),
      /* mapa */
      h('div', { className: cx('alt-ppl-posicao-frota__map', mapStyle === 'satelite' && 'is-sat'), style: { height: p.mapHeight || 280 } },
        h('svg', { className: 'alt-ppl-posicao-frota__svg', viewBox: '0 0 1100 250', preserveAspectRatio: 'xMidYMid slice', role: 'group', 'aria-label': 'Mapa da frota' },
          h('rect', { width: 1100, height: 250, className: 'alt-ppl-posicao-frota__land' }),
          h('g', { 'aria-hidden': 'true' },
            [[10, 6, 80, 36], [10, 168, 80, 36], [110, 120, 70, 36], [200, 60, 70, 36], [290, 6, 70, 36], [300, 170, 70, 36], [390, 110, 70, 36], [500, 60, 70, 36], [520, 200, 80, 36], [610, 6, 70, 36], [620, 150, 70, 36], [720, 110, 70, 36], [800, 60, 70, 36], [880, 6, 80, 36], [900, 200, 80, 36], [1000, 110, 80, 36]].map(function (b, i) { return h('rect', { key: i, x: b[0], y: b[1], width: b[2], height: b[3], rx: 3, className: 'alt-ppl-posicao-frota__block' }); }),
            h('path', { d: 'M790 0 C770 50 830 95 900 120 S1020 180 1100 172 V0z', className: 'alt-ppl-posicao-frota__park' }),
            h('path', { d: 'M0 212 C130 196 230 234 360 222 S570 190 660 250 H0z', className: 'alt-ppl-posicao-frota__water' }),
            h('path', { d: 'M65 0V250M185 0V250M275 0V250M365 0V250M575 0V250M670 0V250M770 0V250M945 0V250M1035 0V250M0 36H1100M0 86H1100M0 152H1100M0 200H1100', className: 'alt-ppl-posicao-frota__road' }),
            h('path', { id: 'alt-ppf-sp348', d: 'M-10 184 C160 170 285 108 460 115 S770 54 1110 80', className: 'alt-ppl-posicao-frota__hwy' }),
            h('path', { d: 'M515 -10 C528 72 595 152 572 260', className: 'alt-ppl-posicao-frota__hwy alt-ppl-posicao-frota__hwy--b' }),
            lay.transito ? h('g', null, h('path', { d: 'M290 125 C340 116 380 112 460 115', className: 'alt-ppl-posicao-frota__traf is-bad' }), h('path', { d: 'M700 66 C760 60 800 62 860 66', className: 'alt-ppl-posicao-frota__traf is-mid' })) : null,
            [[232, 150, 'SP-348'], [568, 186, 'SP-330']].map(function (s) { return h('g', { key: s[2], transform: 'translate(' + s[0] + ' ' + s[1] + ')' }, h('rect', { x: -24, y: -9, width: 48, height: 17, rx: 4, className: 'alt-ppl-posicao-frota__shield' }), h('text', { y: 3.5, textAnchor: 'middle', className: 'alt-ppl-posicao-frota__shield-t' }, s[2])); }),
            [[96, 56, 'Campinas'], [650, 26, 'Jundiaí'], [935, 236, 'São Paulo'], [395, 190, 'Itupeva']].map(function (c) { return h('text', { key: c[2], x: c[0], y: c[1], className: 'alt-ppl-posicao-frota__city' }, c[2]); })),
          route && lay.cercas ? h('path', { d: 'M' + (route.d[0] - 45) + ' ' + (route.d[1] - 22) + 'L' + (route.d[0] + 50) + ' ' + (route.d[1] - 34) + 'L' + (route.d[0] + 62) + ' ' + (route.d[1] + 26) + 'L' + (route.d[0] - 20) + ' ' + (route.d[1] + 42) + 'Z', className: 'alt-ppl-posicao-frota__fence' }) : null,
          route ? h('g', { 'aria-hidden': 'true' },
            h('path', { d: route.done, className: 'alt-ppl-posicao-frota__rt-case' }),
            h('path', { d: route.done, className: 'alt-ppl-posicao-frota__rt-done' }),
            h('path', { d: route.next, className: 'alt-ppl-posicao-frota__rt-next' }),
            h('g', { transform: 'translate(' + route.o[0] + ' ' + route.o[1] + ')' }, h('circle', { r: 8, className: 'alt-ppl-posicao-frota__rt-a' }), h('text', { y: 3.5, textAnchor: 'middle', className: 'alt-ppl-posicao-frota__rt-at' }, 'A')),
            h('g', { transform: 'translate(' + route.d[0] + ' ' + route.d[1] + ')' }, h('circle', { r: 9, className: 'alt-ppl-posicao-frota__rt-b' }), h('path', { d: 'M-3 5V-5h6l-1.5 2.5L3 0h-6', className: 'alt-ppl-posicao-frota__rt-flag' }))) : null,
          p.clusters !== false ? [
            h(Cluster, { key: 'c1', x: 880, y: 52, parts: [['transito', 14], ['parado', 3], ['vazio', 4], ['carga', 2]], onClick: p.onClusterClick }),
            h(Cluster, { key: 'c2', x: 780, y: 214, parts: [['transito', 6], ['descarga', 2], ['vazio', 1]], onClick: p.onClusterClick })] : null,
          shown.filter(function (v) { return v.id !== sel; }).map(function (v) { return h(Marker, { key: v.id, v: v, onSelect: function (id) { select(id, true); } }); }),
          curShown ? h(Marker, { v: cur, selected: true, onSelect: function (id) { select(id, true); } }) : null,
          curShown && popOpen ? h('foreignObject', { x: popX, y: popY, width: 304, height: 172 },
            h('div', { className: 'alt-ppl-posicao-frota__pop', role: 'dialog', 'aria-label': cur.placa },
              h('div', { className: 'alt-ppl-posicao-frota__ph' },
                h('b', { className: 'alt-num' }, cur.placa), h(CopyBtn, { text: cur.placa, kind: 'placa' }),
                h('span', { className: 'alt-ppl-posicao-frota__st', style: { '--c': STATUS[cur.status].c } }, stLabel(cur)),
                h('button', { type: 'button', className: 'alt-ppl-posicao-frota__x', 'aria-label': 'Fechar', onClick: function () { setPopOpen(false); } }, h(A.Icon, { name: 'x', size: 14 }))),
              h('dl', null,
                h('dt', null, 'Motorista'), h('dd', null, cur.motorista || '—', cur.motorista ? h(CopyBtn, { text: cur.motorista, kind: 'motorista' }) : null),
                h('dt', null, 'Viagem'), h('dd', { className: 'alt-num' }, cur.viagem ? '#' + cur.viagem + ' · ' + cur.origem.split('/')[0] + ' → ' + cur.destino.split('/')[0] : 'Sem viagem'),
                h('dt', null, 'Local'), h('dd', null, cur.local.split(' · ')[0]),
                h('dt', null, 'ETA'), h('dd', { className: 'alt-num' }, cur.eta ? h('b', { style: { color: cur.desvio > 30 ? 'var(--signal-error)' : cur.desvio > 0 ? 'var(--signal-warning)' : 'var(--signal-success)' } }, cur.eta + (cur.desvio ? ' · +' + cur.desvio + ' min' : ' · no prazo')) : (cur.etaNote || '—'))),
              h('div', { className: 'alt-ppl-posicao-frota__pact' },
                h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { if (p.onHistory) p.onHistory(cur); } }, 'Histórico'),
                h('button', { type: 'button', className: 'alt-tbtn alt-ppl-posicao-frota__primary', onClick: function () { if (p.onOpenTrip) p.onOpenTrip(cur); }, disabled: !cur.viagem }, 'Abrir viagem')))) : null),
        h('div', { className: 'alt-ppl-posicao-frota__mtop' },
          h('div', { className: 'alt-ppl-posicao-frota__seg', role: 'group', 'aria-label': 'Estilo do mapa' },
            [['mapa', 'Mapa'], ['satelite', 'Satélite']].map(function (m) { return h('button', { key: m[0], type: 'button', 'aria-pressed': mapStyle === m[0] ? 'true' : 'false', onClick: function () { setMapStyle(m[0]); } }, m[1]); })),
          [['cercas', 'Cercas', 'M12 3 21 9l-3 11H6L3 9z'], ['transito', 'Trânsito', 'M5 20 9 4M19 20 15 4M12 6v2M12 11v2M12 16v2']].map(function (L) {
            return h('button', { key: L[0], type: 'button', className: 'alt-ppl-posicao-frota__mt', 'aria-pressed': lay[L[0]] ? 'true' : 'false', onClick: function () { var n = Object.assign({}, lay); n[L[0]] = !n[L[0]]; setLay(n); } }, Svg(L[2]), L[1]);
          })),
        h('div', { className: 'alt-ppl-posicao-frota__lgm', 'aria-label': 'Legenda' },
          h('span', null, h('i', { className: 'alt-ppl-posicao-frota__lv', style: { '--c': 'var(--signal-success)' } }), 'Em movimento'),
          h('span', null, h('i', { className: 'alt-ppl-posicao-frota__lv is-sq', style: { '--c': 'var(--signal-warning)' } }), 'Parado'),
          h('span', null, h('i', { className: 'alt-ppl-posicao-frota__lv is-hollow', style: { '--c': 'var(--slate)' } }), 'Sem sinal'),
          h('span', null, h('i', { className: 'alt-ppl-posicao-frota__ll' }), 'Realizado'),
          h('span', null, h('i', { className: 'alt-ppl-posicao-frota__ll is-dot' }), 'Previsto')),
        h('div', { className: 'alt-popover alt-ppl-posicao-frota__zoom' },
          h('button', { type: 'button', 'aria-label': 'Aproximar' }, h(A.Icon, { name: 'plus', size: 16 })),
          h('button', { type: 'button', 'aria-label': 'Afastar' }, h(A.Icon, { name: 'minus', size: 16 })),
          h('button', { type: 'button', 'aria-label': 'Enquadrar frota' }, Svg('M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5', 16))),
        h('div', { className: 'alt-ppl-posicao-frota__scl alt-num', 'aria-hidden': 'true' }, h('i'), '5 km')),
      /* grid */
      h('div', { className: 'alt-ppl-posicao-frota__gw alt-scroll', style: { maxHeight: p.gridMaxHeight === undefined ? 380 : p.gridMaxHeight } },
        h('table', null,
          h('thead', null, h('tr', null,
            sortTh('placa', 'Veículo', 'is-stk'), h('th', null, 'Status'), h('th', null, 'Alertas'), h('th', null, 'Viagem'), h('th', null, 'Origem → destino'), h('th', null, 'ETA · desvio'),
            sortTh('progresso', 'Progresso'), sortTh('age', 'Última posição'), h('th', null, 'Sinal GPS'), h('th', null, 'Localização'), sortTh('vel', 'Velocidade', 'is-r'), h('th', null, 'Ignição'), h('th', null, h('span', { className: 'alt-ppl-posicao-frota__sr' }, 'Ações')))),
          h('tbody', null, rows.length ? rows.map(function (v) {
            var s = STATUS[v.status], g = GPSQ[v.gps.q], stale = v.status === 'semsinal';
            var dTone = v.desvio == null ? null : v.desvio === 0 ? 'success' : v.desvio <= 30 ? 'warning' : 'error';
            return h('tr', { key: v.id, ref: function (el) { rowRefs.current[v.id] = el; }, className: cx(v.id === sel && 'is-sel', stale && 'is-stale'), onClick: function () { select(v.id); }, 'aria-selected': v.id === sel ? 'true' : 'false' },
              h('td', { className: 'is-stk' },
                h('div', { className: 'alt-ppl-posicao-frota__veh' }, h('b', { className: 'alt-num' }, v.placa), h(CopyBtn, { text: v.placa, kind: 'placa' })),
                v.motorista ? h('div', { className: 'alt-ppl-posicao-frota__veh is-sub' }, h('span', null, v.motorista), h(CopyBtn, { text: v.motorista, kind: 'motorista' })) : h('div', { className: 'alt-ppl-posicao-frota__veh is-sub' }, 'Sem motorista')),
              h('td', { className: 'is-keep' }, h('span', { className: 'alt-ppl-posicao-frota__st', style: { '--c': s.c } }, h('i', { className: s.hollow ? 'is-hollow' : null }), s.label)),
              h('td', null, v.alerts.length ? h('span', { className: 'alt-ppl-posicao-frota__als' }, v.alerts.map(function (a) { return h('span', { key: a, className: 'alt-ppl-posicao-frota__al', title: ALERTS[a].label, 'aria-label': ALERTS[a].label, role: 'img' }, AlertIc(a)); })) : h('span', { className: 'alt-ppl-posicao-frota__mut' }, '—')),
              h('td', null, v.viagem ? [h('b', { key: 'a', className: 'alt-num' }, '#' + v.viagem), h('small', { key: 'b' }, v.cliente)] : h('span', { className: 'alt-ppl-posicao-frota__mut' }, 'Sem viagem')),
              h('td', null, h('span', { className: 'alt-ppl-posicao-frota__od' }, v.origem, h(A.Icon, { name: 'arrow-right', size: 12 }), v.destino)),
              h('td', { className: 'alt-num' }, h('b', null, v.eta || '—'),
                h('small', { style: dTone ? { color: K.tone(dTone).c } : null }, v.desvio == null ? (v.etaNote || '—') : v.desvio === 0 ? 'no prazo' : '+' + v.desvio + ' min')),
              h('td', null, v.progresso == null ? h('span', { className: 'alt-ppl-posicao-frota__mut' }, '—') : h('span', { className: 'alt-ppl-posicao-frota__pg' }, h('span', { className: 'alt-ppl-posicao-frota__pb' }, h('i', { style: { width: v.progresso + '%' } })), h('span', { className: 'alt-num' }, v.progresso + '%'))),
              h('td', { className: 'alt-num' }, h('b', null, v.ultima), h('small', null, v.ha)),
              h('td', null, h('span', { className: 'alt-ppl-posicao-frota__gps', title: g[0] + ' · ' + v.gps.sat + ' satélites · posição de ' + v.gps.age + ' atrás', style: { '--c': K.tone(g[2]).c } },
                h('span', { className: 'alt-ppl-posicao-frota__bars', 'aria-hidden': 'true' }, [4, 7, 10, 13].map(function (hh, i) { return h('i', { key: i, className: i < g[1] ? 'is-on' : null, style: { height: hh } }); })),
                h('span', null, h('b', null, g[0]), h('small', { className: 'alt-num' }, v.gps.sat + ' sat · ' + v.gps.age)))),
              h('td', { className: 'alt-ppl-posicao-frota__addr' }, h('span', null, (stale ? 'Última: ' : '') + v.local), h('small', { className: 'alt-num' }, nf(v.lat, 4) + ', ' + nf(v.lon, 4))),
              h('td', { className: 'alt-num is-r' }, v.vel == null ? '—' : v.vel + ' km/h'),
              h('td', null, v.ign == null ? h('span', { className: 'alt-ppl-posicao-frota__mut' }, '—') : h('span', { className: cx('alt-ppl-posicao-frota__ign', v.ign && 'is-on') }, v.ign ? 'Ligada' : 'Desligada')),
              h('td', null, h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost alt-ppl-posicao-frota__more', 'aria-label': 'Mais ações para ' + v.placa, onClick: function (e) { e.stopPropagation(); if (p.onRowMenu) p.onRowMenu(v); } }, h(A.Icon, { name: 'more', size: 16 }))));
          }) : h('tr', null, h('td', { colSpan: 13, className: 'alt-ppl-posicao-frota__empty' }, 'Nenhum veículo com esses filtros.'))))),
      h('div', { className: 'alt-ppl-posicao-frota__ft alt-num' },
        h('span', null, 'Exibindo ' + rows.length + ' de ' + total + ' · ordenado por ' + SORT_LABEL[sort.key]),
        h('span', null, 'Posições recebidas às ' + recv)));
  }
  PplPosicaoFrota.STATUS = STATUS;
  A.PplPosicaoFrota = PplPosicaoFrota;
})();


/* ---- PplStatusBars ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  /* Mapa único de cores por estado do segmento (StatusBar com variant) */
  var KINDS = {
    base: { c: 'transparent', label: 'Sem evento' },
    stopped: { c: 'var(--slate)', label: 'Parado' },
    slow: { c: 'var(--signal-warning)', label: '5–40 km/h' },
    fast: { c: 'var(--signal-success)', label: '> 40 km/h' },
    active: { c: 'var(--signal-success)', label: 'Ativo' },
    fault: { c: 'var(--signal-error)', label: 'Falha' },
    none: { c: null, label: 'Sem dados' }
  };
  var MACRO = ['var(--chart-1)', 'var(--chart-2)', 'var(--chart-3)', 'var(--chart-4)', 'var(--chart-5)', 'var(--chart-6)'];
  var CUSTOM = {
    gauge: 'M12 14l4-4M3.3 17a9 9 0 1 1 17.4 0',
    file: 'M14 3H6v18h12V7zM14 3v4h4',
    tool: 'M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 0 0 5.4-5.4l-2.5 2.5-2.4-.6-.6-2.4z'
  };

  var DEFAULT_ROWS = [
    { id: 'velocidade', variant: 'velocidade', label: 'Movimentação', icon: 'gauge', value: '60%', segments: [
      { w: 20, kind: 'stopped', tip: '00:00–04:48 · parado' }, { w: 25, kind: 'slow', tip: '04:48–10:48 · 5–40 km/h' }, { w: 35, kind: 'fast', tip: '10:48–19:12 · média 72 km/h' }, { w: 20, kind: 'stopped', tip: '19:12–24:00 · parado' }] },
    { id: 'mdfe', variant: 'mdfe', label: 'MDF-e ativo', icon: 'file', value: '70%', segments: [
      { w: 12, kind: 'base', tip: '00:00–02:53 · sem MDF-e' }, { w: 70, kind: 'active', tip: '02:53–19:41 · MDF-e 35260…0412 ativo' }, { w: 18, kind: 'base', tip: '19:41–24:00 · sem MDF-e' }] },
    { id: 'manutencao', variant: 'manutencao', label: 'Manutenção', icon: 'tool', value: '12%', segments: [
      { w: 30, kind: 'base', tip: '00:00–07:12 · operando' }, { w: 12, kind: 'fault', tip: '07:12–10:05 · OS 7712 em manutenção' }, { w: 40, kind: 'none', tip: '10:05–19:41 · sem dados' }, { w: 18, kind: 'base', tip: '19:41–24:00 · operando' }] },
    { id: 'macros', variant: 'macros', label: 'Macros', icon: 'message', height: 18, value: '9', segments: [
      { w: 8, kind: 'base' }, { w: 6, macro: 0, tip: 'Início de jornada · 05:10' }, { w: 14, macro: 1, tip: 'Chegada no cliente · 06:40' }, { w: 10, macro: 2, tip: 'Início de carregamento · 10:00' }, { w: 6, macro: 3, tip: 'Fim de carregamento · 12:25' }, { w: 22, kind: 'base' }, { w: 8, macro: 4, tip: 'Parada para refeição · 17:45' }, { w: 26, kind: 'base' }] },
    { id: 'produtividade', variant: 'kpi', label: 'Produtividade', icon: 'truck', kpi: 58 }
  ];

  function Ico(name) {
    if (CUSTOM[name]) return h('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', className: 'alt-icon', 'aria-hidden': 'true' }, CUSTOM[name].split(/(?=M)/).map(function (d, i) { return h('path', { key: i, d: d }); }));
    return h(A.Icon, { name: name, size: 16 });
  }

  function PplStatusBars(p) {
    var rows = p.rows || DEFAULT_ROWS;
    var s = R.useState(null), sel = s[0], setSel = s[1];
    var t = R.useState(null), tip = t[0], setTip = t[1];
    var interactive = !p.notInteractive;
    var legend = p.legend || ['stopped', 'slow', 'fast', 'fault', 'none'];

    function bar(r) {
      if (r.variant === 'kpi') {
        return h('div', { className: 'alt-ppl-status-bars__kpi', role: 'meter', 'aria-valuemin': 0, 'aria-valuemax': 100, 'aria-valuenow': r.kpi, 'aria-label': r.label },
          h('i', { style: { width: Math.min(100, r.kpi) + '%' } }));
      }
      var x = 0;
      return h('div', { className: 'alt-ppl-status-bars__bar', style: { height: r.height || p.height || 10 } }, r.segments.map(function (sg, i) {
        var k = KINDS[sg.kind] || KINDS.base, c = sg.macro != null ? MACRO[sg.macro % MACRO.length] : k.c;
        var id = r.id + ':' + i, left = x; x += sg.w;
        var label = sg.tip || k.label;
        var common = { key: i, className: cx('alt-ppl-status-bars__seg', sg.kind === 'none' && 'is-none', sel === id && 'is-sel'), style: { width: sg.w + '%', background: c || undefined },
          onMouseEnter: function () { setTip({ row: r.id, x: left + sg.w / 2, text: label }); }, onMouseLeave: function () { setTip(null); } };
        if (!interactive) return h('i', Object.assign(common, { title: label }));
        return h('button', Object.assign(common, { type: 'button', 'aria-label': r.label + ': ' + label, 'aria-pressed': sel === id ? 'true' : 'false',
          onFocus: common.onMouseEnter, onBlur: common.onMouseLeave,
          onClick: function () { var n = sel === id ? null : id; setSel(n); if (p.onClick) p.onClick(n ? { row: r.id, index: i, segment: sg } : null); } }));
      }));
    }

    return h('section', { className: cx('alt-glass', 'alt-ppl-status-bars', p.className), style: p.style },
      p.title !== null ? h('header', { className: 'alt-ppl-status-bars__hd' },
        h('h3', null, p.title || 'Histórico · ABC-1D23'), h('span', { className: 'alt-num' }, p.date || '29/09/2026')) : null,
      h('div', { className: 'alt-ppl-status-bars__body' },
        h('div', { className: cx('alt-ppl-status-bars__grid', p.notInteractive && 'alt-ppl-status-bars__grid--wide') }, rows.map(function (r) {
          return h(R.Fragment, { key: r.id },
            h('span', { className: 'alt-ppl-status-bars__lbl', title: r.label }, Ico(r.icon), h('span', null, r.label)),
            h('div', { className: 'alt-ppl-status-bars__cell' }, bar(r),
              tip && tip.row === r.id ? h('span', { className: 'alt-ppl-status-bars__tip alt-num', role: 'tooltip', style: { left: tip.x + '%' } }, tip.text) : null),
            h('span', { className: 'alt-ppl-status-bars__pc alt-num' }, r.variant === 'kpi' ? nf(r.kpi, 1) + '%' : r.value));
        })),
        h('div', { className: 'alt-ppl-status-bars__lg' }, legend.map(function (k) {
          return h('span', { key: k }, h('i', { className: k === 'none' ? 'is-none' : null, style: { background: KINDS[k].c || undefined } }), KINDS[k].label);
        }))));
  }
  PplStatusBars.KINDS = KINDS;
  A.PplStatusBars = PplStatusBars;
})();


/* ---- PplWizard ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  function Field(label, value, opts) {
    opts = opts || {};
    return h('label', { className: 'alt-field', key: label },
      h('span', { className: 'alt-field__label' }, label),
      h('span', { className: 'alt-ctl alt-ctl--sm' + (opts.num ? ' alt-num' : '') }, opts.icon ? h(A.Icon, { name: opts.icon, size: 16 }) : null,
        h('input', { defaultValue: value, readOnly: opts.readOnly })));
  }

  var DEFAULT_STEPS = [
    { key: 'destinatario', label: 'Destinatário', action: 'Avançar', render: function () { return [Field('Cliente', 'Vidroporto S.A.', { icon: 'building' }), Field('CNPJ', '12.345.678/0001-90', { num: true }), Field('Contato', 'Ana Ribeiro')]; } },
    { key: 'rota', label: 'Rota', action: 'Avançar', render: function () { return [Field('Origem', 'Porto Ferreira/SP', { icon: 'pin' }), Field('Destino', 'Santos/SP', { icon: 'flag' }), Field('Distância (km)', '312', { num: true })]; } },
    { key: 'pedido', label: 'Pedido', action: 'Criar pedido e avançar', confirm: { title: 'Confirmar criação do pedido', text: 'Um novo pedido será criado a partir do original com os dados desta etapa.', action: 'Confirmar e criar' },
      render: function () { return [Field('Produto', 'Vidro float 4mm', { icon: 'package' }), Field('Volume total (t)', '139,5', { num: true }), Field('Semana', '40/2026', { num: true })]; } },
    { key: 'documentos', label: 'Documentos', action: 'Confirmar e avançar', render: function () { return [Field('Nota fiscal', 'NF 88.214', { icon: 'file', num: true }), Field('MDF-e', 'Será emitido na saída'), Field('Observações', 'Carga paletizada')]; } },
    { key: 'resultado', label: 'Resultado', action: 'Concluir', render: function () {
      return h('div', { className: 'alt-ppl-wizard__result' }, h(A.Icon, { name: 'check-circle', size: 22 }),
        h('div', null, h('b', null, 'Pedido #4519 criado'), h('span', null, 'Clonado de #4412 · Vidroporto · semana 40/2026 · 139,5 t')));
    } }
  ];

  var CHECK = h('svg', { width: 12, height: 12, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 3, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true' }, h('path', { d: 'm5 12 5 5 9-10' }));

  function PplWizard(p) {
    var steps = p.steps || DEFAULT_STEPS;
    var ctl = p.step !== undefined;
    var st = R.useState(p.defaultStep == null ? 2 : p.defaultStep), step = ctl ? p.step : st[0];
    var cf = R.useState(false), confirming = cf[0], setConfirming = cf[1];
    var mn = R.useState(false), menu = mn[0], setMenu = mn[1];
    var cur = steps[step] || steps[0];

    function go(n) { setConfirming(false); if (!ctl) st[1](n); if (p.onStepChange) p.onStepChange(n); }
    function primary() {
      if (cur.confirm && !confirming) return setConfirming(true);
      if (step === steps.length - 1) { if (p.onFinish) p.onFinish(); return; }
      if (p.onAdvance) p.onAdvance(cur.key);
      go(step + 1);
    }

    return h('section', { className: cx('alt-glass', 'alt-ppl-wizard', p.className), style: p.style, 'aria-label': p.title || 'Clonar pedido' },
      h('header', { className: 'alt-ppl-wizard__hd' },
        h('h3', null, p.title || 'Clonar pedido #4412'),
        h('div', { className: 'alt-ppl-wizard__menu-wrap' },
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Mais ações', 'aria-haspopup': 'menu', 'aria-expanded': menu ? 'true' : 'false', onClick: function () { setMenu(!menu); } }, h(A.Icon, { name: 'more', size: 18 })),
          menu ? h('div', { className: 'alt-popover alt-ppl-wizard__menu', role: 'menu' },
            h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item alt-ppl-wizard__danger', onClick: function () { setMenu(false); if (p.onDelete) p.onDelete(); } }, h(A.Icon, { name: 'trash', size: 16 }), 'Excluir pedido')) : null),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Fechar', onClick: p.onClose }, h(A.Icon, { name: 'x', size: 18 }))),
      h('ol', { className: 'alt-ppl-wizard__steps', 'aria-label': 'Etapas' }, steps.map(function (s, i) {
        var state = i < step ? 'done' : i === step ? 'cur' : 'todo';
        return [
          i > 0 ? h('li', { key: 'c' + i, className: cx('alt-ppl-wizard__cn', i <= step && 'is-done'), 'aria-hidden': 'true' }) : null,
          h('li', { key: s.key, className: 'alt-ppl-wizard__sp alt-ppl-wizard__sp--' + state, 'aria-current': state === 'cur' ? 'step' : undefined },
            h('button', { type: 'button', disabled: state === 'todo', onClick: function () { go(i); }, 'aria-label': s.label + (state === 'done' ? ' (concluída)' : state === 'cur' ? ' (atual)' : '') },
              h('i', { className: 'alt-num' }, state === 'done' ? CHECK : i + 1), s.label))
        ];
      })),
      h('div', { className: 'alt-ppl-wizard__body' }, cur.render ? cur.render() : cur.content),
      confirming ? h('div', { className: 'alt-ppl-wizard__confirm', role: 'alertdialog', 'aria-label': cur.confirm.title },
        h(A.Icon, { name: 'alert', size: 18 }),
        h('div', { className: 'alt-ppl-wizard__confirm-txt' }, h('b', null, cur.confirm.title), h('span', null, cur.confirm.text)),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { setConfirming(false); } }, 'Cancelar'),
        h(A.Button, { size: 'sm', arrow: false, onClick: primary }, cur.confirm.action)) :
      h('footer', { className: 'alt-ppl-wizard__ft' },
        step > 0 ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { go(step - 1); } }, h(A.Icon, { name: 'arrow-left', size: 16 }), 'Voltar') : null,
        h('span', { className: 'alt-ppl-wizard__count alt-num' }, 'Etapa ' + (step + 1) + ' de ' + steps.length),
        h(A.Button, { size: 'sm', arrow: step < steps.length - 1, onClick: primary }, cur.action || 'Avançar')));
  }
  A.PplWizard = PplWizard;
})();


/* ---- RhCampoRepetidor ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var uid = 0;
  var DEF_COLS = [
    { key: 'centro', label: 'Centro de custo', type: 'select', options: ['1102 · Frota Granéis', '1103 · Frota Vidros', '1105 · Oficina PF', '1110 · Armazém CD', '2102 · Recursos humanos'], placeholder: 'Selecione' },
    { key: 'percentual', label: 'Percentual', type: 'number', suffix: '%', width: 100 },
    { key: 'valor', label: 'Valor', type: 'number', prefix: 'R$', width: 120 }
  ];
  var DEF_VALUE = [{ centro: '1102 · Frota Granéis', percentual: '60', valor: '3.000' }, { centro: '1105 · Oficina PF', percentual: '40', valor: '2.000' }];
  function RhCampoRepetidor(p) {
    var cols = p.columns || DEF_COLS;
    var st = R.useState(function () { return (p.defaultValue || (p.columns ? [] : DEF_VALUE)).map(function (r) { return Object.assign({ _id: ++uid }, r); }); });
    var rows = p.value ? p.value.map(function (r, i) { return Object.assign({ _id: r._id || i + 1 }, r); }) : st[0];
    var focusId = R.useRef(null), wrap = R.useRef(null);
    function commit(next) { if (!p.value) st[1](next); p.onChange && p.onChange(next.map(function (r) { var o = Object.assign({}, r); delete o._id; return o; })); }
    function add() { var r = { _id: ++uid }; cols.forEach(function (c) { r[c.key] = ''; }); focusId.current = r._id; commit(rows.concat([r])); }
    function remove(id) { commit(rows.filter(function (r) { return r._id !== id; })); }
    function edit(id, k, v) { commit(rows.map(function (r) { if (r._id !== id) return r; var o = Object.assign({}, r); o[k] = v; return o; })); }
    R.useEffect(function () {
      if (focusId.current == null || !wrap.current) return;
      var el = wrap.current.querySelector('[data-row="' + focusId.current + '"] input, [data-row="' + focusId.current + '"] select');
      if (el) el.focus(); focusId.current = null;
    });
    var grid = cols.map(function (c) { return c.width ? c.width + 'px' : 'minmax(0,1fr)'; }).join(' ') + ' 34px';
    var max = p.max, full = max != null && rows.length >= max;
    var label = p.label || 'Centros de custo';
    var sumCol = p.totalKey && cols.filter(function (c) { return c.key === p.totalKey; })[0];
    var total = sumCol ? rows.reduce(function (a, r) { return a + (parseFloat(String(r[p.totalKey]).replace(/\./g, '').replace(',', '.')) || 0); }, 0) : null;
    return h('div', { ref: wrap, className: cx('alt-rh-campo-repetidor', p.className), style: p.style, role: 'group', 'aria-label': label },
      h('div', { className: 'alt-rh-campo-repetidor__hd' },
        h('span', { className: 'alt-rh-campo-repetidor__lbl' }, label, h('span', { className: 'alt-rh-campo-repetidor__n alt-num' }, ' · ' + rows.length)),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-rh-campo-repetidor__add', onClick: add, disabled: full || p.disabled }, h(A.Icon, { name: 'plus', size: 15 }), p.addLabel || 'Adicionar')),
      rows.length ? h(R.Fragment, null,
        h('div', { className: 'alt-rh-campo-repetidor__cols', style: { gridTemplateColumns: grid }, 'aria-hidden': 'true' }, cols.map(function (c) { return h('span', { key: c.key, style: c.type === 'number' ? { textAlign: 'right' } : null }, c.label); }), h('span')),
        rows.map(function (r, i) {
          return h('div', { key: r._id, className: 'alt-rh-campo-repetidor__row', 'data-row': r._id, style: { gridTemplateColumns: grid } },
            cols.map(function (c) {
              var aria = c.label + ', linha ' + (i + 1);
              if (c.type === 'select') return h('label', { key: c.key, className: 'alt-ctl alt-ctl--sm alt-rh-campo-repetidor__sel' },
                h('select', { value: r[c.key] || '', 'aria-label': aria, onChange: function (e) { edit(r._id, c.key, e.target.value); }, disabled: p.disabled },
                  h('option', { value: '', disabled: true }, c.placeholder || 'Selecione'), c.options.map(function (o) { return h('option', { key: o, value: o }, o); })),
                h(A.Icon, { name: 'chevron-down', size: 14 }));
              return h('label', { key: c.key, className: 'alt-ctl alt-ctl--sm alt-rh-campo-repetidor__in' },
                c.prefix ? h('span', { className: 'alt-rh-campo-repetidor__fx' }, c.prefix) : null,
                h('input', { value: r[c.key] || '', 'aria-label': aria, inputMode: c.type === 'number' ? 'decimal' : null, placeholder: c.placeholder, disabled: p.disabled,
                  className: c.type === 'number' ? 'alt-num' : null, style: c.type === 'number' ? { textAlign: 'right' } : null, onChange: function (e) { edit(r._id, c.key, e.target.value); } }),
                c.suffix ? h('span', { className: 'alt-rh-campo-repetidor__fx' }, c.suffix) : null);
            }),
            h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost alt-rh-campo-repetidor__rm', 'aria-label': 'Remover linha ' + (i + 1), title: 'Remover', onClick: function () { remove(r._id); }, disabled: p.disabled }, h(A.Icon, { name: 'trash', size: 15 })));
        }),
        sumCol ? h('div', { className: 'alt-rh-campo-repetidor__tot alt-num', 'data-ok': p.totalTarget == null || total === p.totalTarget ? 'true' : 'false' },
          'Total ' + (sumCol.prefix ? sumCol.prefix + ' ' : '') + K.nf(total) + (sumCol.suffix || ''),
          p.totalTarget != null && total !== p.totalTarget ? h('span', null, h(A.Icon, { name: 'alert', size: 13 }), ' deve somar ' + K.nf(p.totalTarget) + (sumCol.suffix || '')) : null) : null)
        : h('div', { className: 'alt-rh-campo-repetidor__empty' }, p.emptyText || 'Nenhum item informado.'),
      p.error ? h('div', { className: 'alt-field__error' }, h(A.Icon, { name: 'alert', size: 14 }), p.error) : null);
  }
  A.RhCampoRepetidor = RhCampoRepetidor;
})();


/* ---- RhFiltros ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var CC = [
    ['1102', 'Frota Granéis', 'Operacional', 'Campinas'], ['1103', 'Frota Vidros', 'Operacional', 'Campinas'], ['1104', 'Frota Químicos', 'Operacional', 'Paulínia'],
    ['1105', 'Oficina PF', 'Manutenção', 'Jundiaí'], ['1106', 'Oficina Campinas', 'Manutenção', 'Campinas'], ['1107', 'Pneus e recapagem', 'Manutenção', 'Jundiaí'],
    ['1110', 'Armazém CD', 'Operacional', 'Jundiaí'], ['1111', 'Expedição', 'Operacional', 'Jundiaí'], ['1112', 'Torre de controle', 'Operacional', 'Campinas'],
    ['2101', 'Financeiro', 'Administrativo', 'Campinas'], ['2102', 'Recursos humanos', 'Administrativo', 'Campinas'], ['2103', 'Comercial', 'Administrativo', 'Campinas'],
    ['2104', 'Tecnologia', 'Administrativo', 'Campinas'], ['2105', 'Jurídico', 'Administrativo', 'Campinas'], ['2106', 'Qualidade e SSMA', 'Administrativo', 'Paulínia'], ['2107', 'Compras', 'Administrativo', 'Jundiaí']
  ].map(function (r) { return { id: r[0], label: r[1], tipo: r[2], unidade: r[3] }; });

  function Box(p) {
    return h('span', { className: 'alt-rh-filtros__box', 'data-on': p.checked || p.mixed ? 'true' : null, 'aria-hidden': 'true' },
      p.mixed ? h(A.Icon, { name: 'minus', size: 12, strokeWidth: 3 }) : p.checked ? h(A.Icon, { name: 'check', size: 12, strokeWidth: 3 }) : null);
  }
  function useOutside(open, setOpen, ref) {
    R.useEffect(function () {
      if (!open) return;
      var c = function (e) { if (ref.current && !ref.current.contains(e.target)) setOpen(false); }, k = function (e) { if (e.key === 'Escape') setOpen(false); };
      document.addEventListener('mousedown', c); document.addEventListener('keydown', k);
      return function () { document.removeEventListener('mousedown', c); document.removeEventListener('keydown', k); };
    }, [open]);
  }
  function summary(sel, total, word) { return !sel.length || sel.length === total ? 'Todos os ' + word : sel.length + ' de ' + total + ' ' + word; }

  /* Seletor agrupado de centros de custo com "Aplicar" */
  function CentroCusto(p) {
    var items = p.items, op = R.useState(!!p.defaultOpen), ref = R.useRef(null);
    var dr = R.useState(p.value), draft = dr[0], q = R.useState(''), gb = R.useState('tipo'), by = gb[0];
    useOutside(op[0], op[1], ref);
    R.useEffect(function () { if (op[0]) dr[1](p.value); }, [op[0]]);
    var ql = q[0].toLowerCase();
    var vis = items.filter(function (it) { return !ql || (it.id + ' ' + it.label).toLowerCase().indexOf(ql) >= 0; });
    var groups = {}; vis.forEach(function (it) { (groups[it[by]] = groups[it[by]] || []).push(it); });
    function toggle(ids, on) { var s = draft.filter(function (x) { return ids.indexOf(x) < 0; }); dr[1](on ? s.concat(ids) : s); }
    return h('div', { ref: ref, className: 'alt-field alt-rh-filtros__cc' },
      h('span', { className: 'alt-field__label', id: 'alt-rh-cc-l' }, 'Centros de custo'),
      h('button', { type: 'button', className: 'alt-ctl alt-rh-filtros__trigger', 'aria-haspopup': 'dialog', 'aria-expanded': op[0], 'aria-labelledby': 'alt-rh-cc-l', onClick: function () { op[1](!op[0]); } },
        h('span', { className: 'alt-rh-filtros__sum alt-num' }, summary(p.value, items.length, 'centros')), h(A.Icon, { name: 'chevron-down', size: 16 })),
      op[0] ? h('div', { className: 'alt-popover alt-rh-filtros__menu', role: 'dialog', 'aria-label': 'Centros de custo' },
        h('div', { className: 'alt-rh-filtros__mt' },
          h('label', { className: 'alt-ctl alt-ctl--sm' }, h(A.Icon, { name: 'search', size: 15 }), h('input', { value: q[0], onChange: function (e) { q[1](e.target.value); }, placeholder: 'Buscar código ou descrição', 'aria-label': 'Buscar centro de custo' })),
          h('div', { className: 'alt-rh-filtros__mrow' },
            h('div', { className: 'alt-rh-filtros__seg', role: 'group', 'aria-label': 'Agrupar por' }, [['tipo', 'Tipo'], ['unidade', 'Unidade']].map(function (o) {
              return h('button', { key: o[0], type: 'button', 'aria-pressed': by === o[0], onClick: function () { gb[1](o[0]); } }, o[1]);
            })),
            h('button', { type: 'button', className: 'alt-rh-filtros__link', onClick: function () { dr[1](items.map(function (i) { return i.id; })); } }, 'Selecionar todos'),
            h('button', { type: 'button', className: 'alt-rh-filtros__link', onClick: function () { dr[1]([]); } }, 'Limpar'))),
        h('div', { className: 'alt-rh-filtros__list alt-scroll', role: 'group' }, Object.keys(groups).length ? Object.keys(groups).map(function (g) {
          var ids = groups[g].map(function (i) { return i.id; }), n = ids.filter(function (id) { return draft.indexOf(id) >= 0; }).length;
          return h('div', { key: g, role: 'group', 'aria-label': g },
            h('button', { type: 'button', className: 'alt-rh-filtros__gh', role: 'checkbox', 'aria-checked': n === ids.length ? 'true' : n ? 'mixed' : 'false', onClick: function () { toggle(ids, n < ids.length); } },
              h(Box, { checked: n === ids.length, mixed: n > 0 && n < ids.length }), h('span', null, g), h('span', { className: 'alt-rh-filtros__gc alt-num' }, n + '/' + ids.length)),
            groups[g].map(function (it) {
              var on = draft.indexOf(it.id) >= 0;
              return h('button', { key: it.id, type: 'button', role: 'checkbox', 'aria-checked': on, className: 'alt-rh-filtros__oi', onClick: function () { toggle([it.id], !on); } },
                h(Box, { checked: on }), h('span', { className: 'alt-num alt-rh-filtros__code' }, it.id), h('span', null, it.label));
            }));
        }) : h('div', { className: 'alt-rh-filtros__empty' }, 'Nenhum centro encontrado.')),
        h('div', { className: 'alt-rh-filtros__mf' }, h('span', { className: 'alt-num' }, draft.length ? draft.length + ' selecionados' : 'Nenhum = todos'),
          h(A.Button, { size: 'sm', onClick: function () { p.onChange(draft); op[1](false); } }, 'Aplicar'))) : null);
  }

  /* Multi-seleção simples (Tipo, Status) */
  function Multi(p) {
    var op = R.useState(false), ref = R.useRef(null), sel = p.value;
    useOutside(op[0], op[1], ref);
    var label = !sel.length || sel.length === p.options.length ? 'Todos' : sel.length === 1 ? sel[0] : sel[0] + ' +' + (sel.length - 1);
    return h('div', { ref: ref, className: 'alt-field alt-rh-filtros__multi' },
      h('span', { className: 'alt-field__label' }, p.label),
      h('button', { type: 'button', className: 'alt-ctl alt-rh-filtros__trigger', 'aria-haspopup': 'listbox', 'aria-expanded': op[0], 'aria-label': p.label + ': ' + label, onClick: function () { op[1](!op[0]); } },
        h('span', { className: 'alt-rh-filtros__sum' }, label), h(A.Icon, { name: 'chevron-down', size: 16 })),
      op[0] ? h('div', { className: 'alt-popover alt-rh-filtros__menu alt-rh-filtros__menu--sm', role: 'listbox', 'aria-multiselectable': 'true' }, p.options.map(function (o) {
        var on = sel.indexOf(o) >= 0;
        return h('button', { key: o, type: 'button', role: 'option', 'aria-selected': on, className: 'alt-menu-item', onClick: function () { p.onChange(on ? sel.filter(function (x) { return x !== o; }) : sel.concat([o])); } }, h(Box, { checked: on }), o);
      })) : null);
  }

  function RhFiltros(p) {
    var items = p.centros || CC;
    var v = R.useState(Object.assign({ de: '01/01/2026', ate: '30/09/2026', centros: ['1102', '1103', '1104', '1105', '1110', '1112'], tipo: [], status: ['Ativos'], comparar: true }, p.defaultValue)), val = p.value || v[0];
    function set(k, x) { var o = Object.assign({}, val); o[k] = x; v[1](o); p.onChange && p.onChange(o); }
    function date(k, l) {
      return h('label', { className: 'alt-field alt-rh-filtros__date' }, h('span', { className: 'alt-field__label' }, l),
        h('span', { className: 'alt-ctl' }, h('input', { type: 'text', inputMode: 'numeric', placeholder: 'dd/mm/aaaa', maxLength: 10, value: val[k],
          onChange: function (e) { var d = e.target.value.replace(/\D/g, '').slice(0, 8); set(k, d.length > 4 ? d.slice(0, 2) + '/' + d.slice(2, 4) + '/' + d.slice(4) : d.length > 2 ? d.slice(0, 2) + '/' + d.slice(2) : d); } }), h(A.Icon, { name: 'calendar', size: 16 })));
    }
    return h('div', { className: cx('alt-rh-filtros alt-glass', p.className), style: p.style, role: 'search', 'aria-label': 'Filtros do dashboard de RH' },
      date('de', 'De'), date('ate', 'Até'),
      h(CentroCusto, { items: items, value: val.centros, onChange: function (x) { set('centros', x); }, defaultOpen: p.defaultOpen }),
      h(Multi, { label: 'Tipo', options: p.tipos || ['Administrativo', 'Operacional'], value: val.tipo, onChange: function (x) { set('tipo', x); } }),
      h(Multi, { label: 'Status', options: p.status || ['Ativos', 'Em afastamento', 'Em férias'], value: val.status, onChange: function (x) { set('status', x); } }),
      h('button', { type: 'button', role: 'switch', 'aria-checked': val.comparar, className: 'alt-rh-filtros__sw', onClick: function () { set('comparar', !val.comparar); } }, h('i', { 'aria-hidden': 'true' }), 'Comparar ano anterior'));
  }
  RhFiltros.CentroCusto = CentroCusto;
  A.RhFiltros = RhFiltros;
})();


/* ---- RhGraficoCard ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  var DEF = {
    title: 'Headcount', periodo: 'Abr–Set 2026', type: 'stacked',
    categories: ['04/26', '05/26', '06/26', '07/26', '08/26', '09/26'],
    series: [{ name: 'Motoristas', data: [780, 788, 795, 798, 805, 812], color: 'var(--chart-1)' }, { name: 'Administrativos', data: [452, 455, 458, 464, 468, 472], color: 'var(--chart-2)' }],
    prevYear: [1180, 1190, 1195, 1201, 1210, 1218], trend: false,
    kpis: [{ label: 'Total', value: 1284, delta: '+4,2% vs set/25' }, { label: 'Motoristas', value: 812, color: 'var(--chart-1)' }, { label: 'Administrativos', value: 472, color: 'var(--chart-2)' }]
  };
  function fit(y) { var n = y.length, sx = 0, sy = 0, sxy = 0, sxx = 0; y.forEach(function (v, i) { sx += i; sy += v; sxy += i * v; sxx += i * i; }); var b = (n * sxy - sx * sy) / (n * sxx - sx * sx || 1); return [(sy - b * sx) / n, b]; }
  function Chart(p) {
    var G = A.Graphics, U = G.util, type = p.type, cats = p.categories, ser = p.series, fmt = p.format, H = p.height;
    var totals = cats.map(function (c, i) { return ser.reduce(function (a, s) { return a + (type === 'stacked' ? s.data[i] : 0); }, 0); });
    return h(G.Frame, { height: H, label: p.ariaLabel || p.title, render: function (W, H, tp) {
      var m = { l: 40, r: 12, t: 22, b: 24 }, n = cats.length, gw = (W - m.l - m.r) / n;
      var vals = [0]; if (type === 'stacked') vals = vals.concat(totals); else ser.forEach(function (s) { vals = vals.concat(s.data); });
      if (p.prevYear) vals = vals.concat(p.prevYear);
      var tk = U.ticks(p.yMin || 0, Math.max.apply(null, vals) * 1.08, 3), ymin = tk[0], ymax = tk[tk.length - 1];
      var Y = function (v) { return m.t + (1 - (v - ymin) / (ymax - ymin)) * (H - m.t - m.b); }, CX = function (i) { return m.l + gw * i + gw / 2; };
      var out = tk.map(function (v, i) { return h('g', { key: 'y' + i }, h('line', { x1: m.l, x2: W - m.r, y1: Y(v), y2: Y(v), className: 'alt-graphics__grid' }), h('text', { x: m.l - 8, y: Y(v) + 4, textAnchor: 'end', className: 'alt-graphics__ax' }, fmt(v))); });
      cats.forEach(function (c, i) { out.push(h('text', { key: 'x' + i, x: CX(i), y: H - 6, textAnchor: 'middle', className: 'alt-graphics__ax' }, c)); });
      function rows(i) {
        var r = ser.map(function (s) { return { c: s.color, v: fmt(s.data[i]), n: s.name, kind: type === 'line' ? 'line' : 'sq' }; });
        if (type === 'stacked') r.push({ v: fmt(totals[i]), n: 'total' });
        if (p.prevYear) r.push({ c: 'rgba(255,255,255,.45)', kind: 'line', v: fmt(p.prevYear[i]), n: 'ano anterior' });
        return r;
      }
      if (type === 'line') {
        ser.forEach(function (s, si) {
          var pts = s.data.map(function (v, i) { return [CX(i), Y(v)]; });
          out.push(h('path', { key: 'l' + si, d: 'M' + pts.map(function (q) { return q.join(' '); }).join('L'), fill: 'none', stroke: s.color, strokeWidth: 2, strokeLinejoin: 'round' }));
          pts.forEach(function (q, i) {
            var on = tp.key === i;
            out.push(h('g', Object.assign(tp.mark({ key: i, title: cats[i], rows: rows(i) }, s.name + ' ' + cats[i] + ': ' + fmt(s.data[i])), { key: 'p' + si + i }),
              h('circle', { cx: q[0], cy: q[1], r: 12, fill: 'transparent' }), h('circle', { cx: q[0], cy: q[1], r: on ? 5 : 3.5, fill: s.color, className: 'alt-graphics__ring' })));
          });
          if (si === 0) {
            var lq = pts[pts.length - 1], lab = fmt(s.data[s.data.length - 1]), wpx = lab.length * 7 + 14;
            out.push(h('g', { key: 'pill', className: 'alt-rh-grafico-card__pill' }, h('rect', { x: lq[0] - wpx / 2, y: lq[1] - 30, width: wpx, height: 20, rx: 10 }), h('text', { x: lq[0], y: lq[1] - 16, textAnchor: 'middle' }, lab)));
          }
        });
      } else {
        var k = type === 'stacked' ? 1 : ser.length, bw = Math.min(34, (gw * 0.62 - 2 * (k - 1)) / k);
        cats.forEach(function (c, i) {
          var on = tp.key == null || tp.key === i, mk = tp.mark({ key: i, title: c, rows: rows(i) }, c + ': ' + ser.map(function (s) { return s.name + ' ' + fmt(s.data[i]); }).join(', '));
          var g = [h('rect', { key: 'hit', x: m.l + gw * i + 2, y: m.t, width: gw - 4, height: H - m.t - m.b, fill: 'transparent' })];
          if (type === 'stacked') {
            var acc = 0, x = CX(i) - bw / 2;
            ser.forEach(function (s, si) {
              var y0 = Y(acc), y1 = Y(acc + s.data[i]) + (si < ser.length - 1 ? 1 : 0); acc += s.data[i];
              var top = si === ser.length - 1;
              g.push(h('path', { key: si, d: top ? U.barPath(x, y1, bw, y0 - y1 - (si ? 1 : 0), 3, 'up') : 'M' + x + ' ' + y1 + 'h' + bw + 'V' + (y0 - (si ? 1 : 0)) + 'h' + (-bw) + 'Z', fill: s.color, fillOpacity: on ? 1 : 0.4 }));
            });
            if (p.labels !== false) g.push(h('text', { key: 't', x: CX(i), y: Y(totals[i]) - 6, textAnchor: 'middle', className: 'alt-rh-grafico-card__lbl' }, fmt(totals[i])));
          } else {
            ser.forEach(function (s, si) {
              var x = CX(i) - (bw * k + 2 * (k - 1)) / 2 + si * (bw + 2), y = Y(s.data[i]);
              g.push(h('path', { key: si, d: U.barPath(x, y, bw, Y(ymin) - y, 3, 'up'), fill: s.color, fillOpacity: on ? 1 : 0.4 }));
              if (p.labels !== false && k === 1) g.push(h('text', { key: 't' + si, x: x + bw / 2, y: y - 6, textAnchor: 'middle', className: 'alt-rh-grafico-card__lbl' }, fmt(s.data[i])));
            });
          }
          out.push(h('g', Object.assign(mk, { key: 'b' + i }), g));
        });
      }
      if (p.trend) {
        var base = type === 'stacked' ? totals : ser[0].data, f = fit(base);
        out.push(h('line', { key: 'tr', x1: CX(0), y1: Y(f[0]), x2: CX(n - 1), y2: Y(f[0] + f[1] * (n - 1)), className: 'alt-rh-grafico-card__trend' }));
      }
      if (p.prevYear) out.push(h('path', { key: 'py', d: 'M' + p.prevYear.map(function (v, i) { return CX(i) + ' ' + Y(v); }).join('L'), className: 'alt-rh-grafico-card__prev' }));
      return out;
    } });
  }
  function RhGraficoCard(p0) {
    var p = Object.assign({}, DEF, p0), G = A.Graphics;
    var fmt = p.format || function (v) { return nf(v); };
    var pr = R.useState(p.defaultRange || '6m');
    var legend = p.series.map(function (s) { return { name: s.name, color: s.color, kind: p.type === 'line' ? 'line' : 'sq' }; });
    if (p.trend) legend.push({ name: 'Tendência', color: 'var(--muted)', kind: 'ref' });
    if (p.prevYear) legend.push({ name: 'Ano anterior', color: 'rgba(255,255,255,.45)', kind: 'proj' });
    return h(G.Card, { title: p.title, subtitle: p.periodo, className: cx('alt-rh-grafico-card', p.className), style: p.style, onAction: p.onExport,
      menu: [{ id: 'csv', label: 'Exportar CSV', icon: 'download' }, { id: 'png', label: 'Exportar PNG', icon: 'download' }],
      actions: p.ranges === false ? null : h('div', { className: 'alt-rh-grafico-card__seg', role: 'group', 'aria-label': 'Período' },
        (p.ranges || [['6m', '6 m'], ['12m', '12 m'], ['ano', 'Ano']]).map(function (o) { return h('button', { key: o[0], type: 'button', 'aria-pressed': pr[0] === o[0], onClick: function () { pr[1](o[0]); p.onRangeChange && p.onRangeChange(o[0]); } }, o[1]); })),
      table: { head: [''].concat(p.series.map(function (s) { return s.name; })).concat(p.prevYear ? ['Ano anterior'] : []), rows: p.categories.map(function (c, i) { return [c].concat(p.series.map(function (s) { return fmt(s.data[i]); })).concat(p.prevYear ? [fmt(p.prevYear[i])] : []); }) },
      toolbar: h(R.Fragment, null,
        p.kpis && p.kpis.length ? h('div', { className: 'alt-rh-grafico-card__kpis alt-num' }, p.kpis.map(function (k, i) {
          return h('div', { key: i, className: 'alt-rh-grafico-card__kpi' },
            h('span', null, k.color ? h('i', { style: { background: k.color } }) : null, k.label),
            h('b', null, typeof k.value === 'number' ? nf(k.value) : k.value),
            k.delta ? h('small', null, k.delta) : null);
        })) : null,
        h(G.Legend, { items: legend })) },
      h(Chart, { type: p.type, categories: p.categories, series: p.series, prevYear: p.prevYear, trend: p.trend, labels: p.labels, format: fmt, height: p.height || 190, yMin: p.yMin, title: p.title, ariaLabel: p.ariaLabel }));
  }
  A.RhGraficoCard = RhGraficoCard;
})();


/* ---- SeletorSemanas ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var DAY = 86400000, MON0 = Date.UTC(1970, 0, 5); /* a Monday: week index origin */
  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function utc(y, m, d) { return new Date(Date.UTC(y, m, d)); }
  /* ISO-8601 week of a date → { ano, semana } */
  function isoWeek(d) {
    var t = utc(d.getFullYear(), d.getMonth(), d.getDate());
    var day = t.getUTCDay() || 7;
    t.setUTCDate(t.getUTCDate() + 4 - day);
    var y = t.getUTCFullYear();
    return { ano: y, semana: Math.ceil(((t - utc(y, 0, 1)) / DAY + 1) / 7) };
  }
  function weekMonday(ano, semana) {
    var jan4 = utc(ano, 0, 4), day = jan4.getUTCDay() || 7;
    return new Date(jan4.getTime() + ((1 - day) + (semana - 1) * 7) * DAY);
  }
  /* "2026-W40" ↔ index (Monday timestamp / week) */
  function parse(v) {
    if (v == null) return null;
    if (typeof v === 'object' && v.ano) return Math.round((weekMonday(v.ano, v.semana).getTime() - MON0) / DAY / 7);
    var m = /^(\d{4})-W(\d{1,2})$/.exec(v);
    if (m) return Math.round((weekMonday(+m[1], +m[2]).getTime() - MON0) / DAY / 7);
    var d = v instanceof Date ? v : new Date(v + (String(v).length === 10 ? 'T12:00:00' : ''));
    var w = isoWeek(d); return Math.round((weekMonday(w.ano, w.semana).getTime() - MON0) / DAY / 7);
  }
  function info(idx) {
    var mon = new Date(MON0 + idx * 7 * DAY), sun = new Date(mon.getTime() + 6 * DAY);
    var w = isoWeek(new Date(mon.getUTCFullYear(), mon.getUTCMonth(), mon.getUTCDate()));
    return {
      key: w.ano + '-W' + pad(w.semana), ano: w.ano, semana: w.semana,
      inicio: mon.getUTCFullYear() + '-' + pad(mon.getUTCMonth() + 1) + '-' + pad(mon.getUTCDate()),
      fim: sun.getUTCFullYear() + '-' + pad(sun.getUTCMonth() + 1) + '-' + pad(sun.getUTCDate()),
      faixa: pad(mon.getUTCDate()) + '/' + pad(mon.getUTCMonth() + 1) + ' – ' + pad(sun.getUTCDate()) + '/' + pad(sun.getUTCMonth() + 1)
    };
  }

  function SeletorSemanas(p) {
    var today = p.today ? new Date(p.today + (String(p.today).length === 10 ? 'T12:00:00' : '')) : new Date();
    var cur = parse(today);
    var ctl = p.value !== undefined;
    var init = p.value || p.defaultValue || { inicio: '2026-W40', fim: '2026-W42' };
    var vs = R.useState({ a: parse(init.inicio), b: parse(init.fim) }); var v = ctl ? { a: parse(p.value.inicio), b: parse(p.value.fim) } : vs[0];
    var es = R.useState(p.defaultActive || 'fim'); var active = es[0], setActive = es[1];
    var min = p.min != null ? parse(p.min) : -Infinity, max = p.max != null ? parse(p.max) : Infinity;

    function emit(a, b) {
      if (!ctl) vs[1]({ a: a, b: b });
      if (p.onChange) { var ia = info(a), ib = info(b); p.onChange({ inicio: ia.key, fim: ib.key, dataInicial: ia.inicio, dataFinal: ib.fim, semanas: b - a + 1 }); }
    }
    function step(d) {
      var a = v.a, b = v.b;
      if (active === 'inicio') { a = a + d; if (a > b) b = a; }
      else { b = b + d; }
      if (a < min || b > max || b < a) return;
      emit(a, b);
    }
    var canPrev = active === 'inicio' ? v.a - 1 >= min : v.b - 1 >= v.a;
    var canNext = active === 'inicio' ? v.a + 1 <= max && (v.a + 1 <= v.b || v.b + 1 <= max) : v.b + 1 <= max;
    var prevWhy = canPrev ? null : (active === 'fim' ? 'A semana final não pode ser anterior à inicial' : 'Não há mais semanas na lista');
    var nextWhy = canNext ? null : 'Não há mais semanas na lista';
    var A0 = info(v.a), B0 = info(v.b), n = v.b - v.a + 1;
    var isCurrent = v.a === cur && v.b === cur;

    function seg(which, w) {
      var on = active === which;
      return h('button', {
        type: 'button', className: cx('alt-seletor-semanas__seg', on && 'is-active'), 'aria-pressed': on,
        'aria-label': (which === 'inicio' ? 'Semana inicial: ' : 'Semana final: ') + 'semana ' + w.semana + ', ' + w.faixa.replace('–', 'a'),
        onClick: function () { setActive(which); },
        onKeyDown: function (e) { if (e.key === 'ArrowLeft') { e.preventDefault(); setActive(which); if (on) step(-1); } if (e.key === 'ArrowRight') { e.preventDefault(); setActive(which); if (on) step(1); } }
      }, h('span', { className: 'alt-seletor-semanas__wk' }, 'Sem ' + w.semana), h('small', null, w.faixa));
    }
    function arrow(dir) {
      var ok = dir < 0 ? canPrev : canNext, why = dir < 0 ? prevWhy : nextWhy;
      return h('span', { className: 'alt-seletor-semanas__tip', title: why || undefined },
        h('button', { type: 'button', className: 'alt-seletor-semanas__ib', disabled: !ok, 'aria-label': (dir < 0 ? 'Semana anterior' : 'Próxima semana') + (active === 'inicio' ? ' (inicial)' : ' (final)'), onClick: function () { step(dir); } },
          h(A.Icon, { name: dir < 0 ? 'chevron-left' : 'chevron-right', size: 16 })));
    }

    var crumbs = p.breadcrumb === undefined ? ['Principal', 'Agenda', 'Calendário'] : p.breadcrumb;
    return h('div', { className: cx('alt-seletor-semanas', p.className), style: p.style },
      crumbs && crumbs.length ? h('nav', { className: 'alt-seletor-semanas__crumbs', 'aria-label': 'Navegação' },
        crumbs.map(function (c, i) {
          var last = i === crumbs.length - 1;
          return [i ? h('span', { key: 's' + i, className: 'alt-seletor-semanas__slash', 'aria-hidden': 'true' }, '/') : null,
            h('span', { key: i, 'aria-current': last ? 'page' : undefined, className: last ? 'is-current' : undefined }, c)];
        })) : null,
      h('div', { className: 'alt-seletor-semanas__bar alt-num', role: 'group', 'aria-label': p.label || 'Intervalo de semanas' },
        h('div', { className: 'alt-seletor-semanas__range' },
          arrow(-1),
          h('span', { className: 'alt-seletor-semanas__mid' },
            h(A.Icon, { name: 'calendar', size: 16 }),
            seg('inicio', A0),
            h(A.Icon, { name: 'arrow-right', size: 14, className: 'alt-seletor-semanas__to' }),
            seg('fim', B0)),
          arrow(1)),
        h('span', { className: 'alt-seletor-semanas__count' }, n === 1 ? '1 semana' : n + ' semanas'),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-seletor-semanas__today', 'aria-pressed': isCurrent, disabled: cur < min || cur > max, onClick: function () { emit(cur, cur); } }, 'Esta semana'),
        p.actions || null));
  }

  SeletorSemanas.isoWeek = isoWeek;
  SeletorSemanas.weekInfo = function (v) { return info(parse(v)); };
  A.SeletorSemanas = SeletorSemanas;
})();


/* ---- ShortcutButton ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var ALIAS = { primary: 'accent', secondary: 'neutral', default: 'neutral' };

  function ShortcutButton(p) {
    var title = p.title != null ? p.title : 'Código do motorista';
    var value = p.value != null ? p.value : 'MOT-004812';
    var icon = p.icon || 'user';
    var t = K.tone(ALIAS[p.color] || p.color || 'accent');
    var events = p.events || [{ label: 'Utilizar este valor', icon: 'copy' }];
    var sDone = R.useState(false), done = sDone[0], setDone = sDone[1];
    var sOpen = R.useState(!!p.defaultOpen), open = sOpen[0], setOpen = sOpen[1];
    var root = R.useRef(null), timer = R.useRef(null);
    R.useEffect(function () { return function () { clearTimeout(timer.current); }; }, []);
    R.useEffect(function () {
      if (!open) return;
      function onDoc(e) { if (root.current && !root.current.contains(e.target)) setOpen(false); }
      function onKey(e) { if (e.key === 'Escape') setOpen(false); }
      document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey);
      return function () { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); };
    }, [open]);

    function run(ev, i) {
      if (p.disabled) return;
      if (ev.onClick) ev.onClick(value, i);
      if (p.onAction) p.onAction(ev, value, i);
      setOpen(false);
      if (events.length === 1) { setDone(true); clearTimeout(timer.current); timer.current = setTimeout(function () { setDone(false); }, 1400); }
    }

    var action;
    if (events.length === 1) {
      var ev = events[0];
      action = h('button', { type: 'button', className: cx('alt-shortcut-button__act', done && 'alt-shortcut-button__act--done'), disabled: p.disabled,
        'aria-label': done ? 'Valor utilizado' : ev.label, title: ev.label, onClick: function () { run(ev, 0); } },
        h(A.Icon, { name: done ? 'check' : (ev.icon || 'copy'), size: 17, strokeWidth: done ? 2.4 : 2 }));
    } else if (events.length > 1) {
      action = h('div', { className: 'alt-shortcut-button__menu-wrap' },
        h('button', { type: 'button', className: 'alt-shortcut-button__menu-btn', disabled: p.disabled, 'aria-haspopup': 'menu', 'aria-expanded': open,
          onClick: function () { setOpen(!open); } }, p.menuLabel || 'Ações', h(A.Icon, { name: 'chevron-down', size: 15 })),
        open ? h('div', { role: 'menu', className: 'alt-popover alt-shortcut-button__menu' }, events.map(function (e2, i) {
          return h('button', { key: i, type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: function () { run(e2, i); } },
            e2.icon ? h(A.Icon, { name: e2.icon, size: 16 }) : null, e2.label);
        })) : null);
    }

    return h('div', { ref: root, className: cx('alt-glass', 'alt-shortcut-button', p.disabled && 'alt-shortcut-button--disabled', p.className), style: p.style },
      h('span', { className: 'alt-shortcut-button__lead', style: { background: t.bg, color: t.c }, 'aria-hidden': 'true' }, h(A.Icon, { name: icon, size: 18 })),
      h('div', { className: 'alt-shortcut-button__text' },
        h('span', { className: 'alt-shortcut-button__title' }, title),
        h('span', { className: 'alt-shortcut-button__value alt-num' }, value)),
      action);
  }

  A.ShortcutButton = ShortcutButton;
})();


/* ---- SideRailAction ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  function SideRailAction(p) {
    var label = p.label != null ? p.label : 'Filtros';
    var icon = p.icon || 'sliders';
    var s = R.useState(!!p.defaultActive);
    var active = p.active != null ? !!p.active : s[0];
    function click(e) {
      if (p.active == null) s[1](!active);
      if (p.onClick) p.onClick(e, !active);
    }
    var aria = p['aria-label'] || ((active ? 'Fechar ' : 'Abrir ') + String(label).toLowerCase() + (p.count ? ', ' + p.count + ' ativos' : ''));
    return h('button', {
      type: 'button', className: cx('alt-side-rail-action', active && 'alt-side-rail-action--active', p.className),
      style: Object.assign({ width: p.width || 34 }, p.style), 'aria-expanded': active, 'aria-label': aria, 'aria-controls': p.controls,
      disabled: p.disabled, onClick: click
    },
      h(A.Icon, { name: icon, size: 16 }),
      p.count ? h('span', { className: 'alt-count alt-side-rail-action__count' }, p.count) : null,
      h('span', { className: 'alt-side-rail-action__label' }, label));
  }

  A.SideRailAction = SideRailAction;
})();


/* ---- SmartReport ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;
  var Icon = A.Icon;

  var DEF_COLUMNS = [
    { field: 'cte', label: 'CT-e', num: true },
    { field: 'especie', label: 'Espécie' },
    { field: 'emissao', label: 'Emissão', num: true },
    { field: 'cnpj', label: 'CNPJ filial', num: true },
    { field: 'conta', label: 'Conta', num: true },
    { field: 'valor', label: 'Valor', num: true, align: 'right', format: function (v) { return 'R$ ' + nf(v, 2); } }
  ];
  var DEF_ROWS = [
    { cte: 88213, especie: 'CTRC', emissao: '02/09/2026', cnpj: '12.345.678/0001-90', conta: '1.1.02', valor: 4820 },
    { cte: 88214, especie: 'NF', emissao: '03/09/2026', cnpj: '12.345.678/0002-71', conta: '1.1.04', valor: 1240.5 },
    { cte: 88231, especie: 'CTRC', emissao: '09/09/2026', cnpj: '12.345.678/0001-90', conta: '1.1.02', valor: 7315.2 },
    { cte: 88240, especie: 'CT-e', emissao: '14/09/2026', cnpj: '12.345.678/0003-52', conta: '2.1.01', valor: 2980 },
    { cte: 88257, especie: 'NF', emissao: '21/09/2026', cnpj: '12.345.678/0002-71', conta: '1.1.04', valor: 615.9 },
    { cte: 88262, especie: 'CT-e', emissao: '28/09/2026', cnpj: '12.345.678/0001-90', conta: '2.1.03', valor: 3402.75 }
  ];
  var DEF_FILTERS = { inicio: '01/09/2026', fim: '30/09/2026', contaDe: '1.1.01', contaAte: '2.9.99', especie: 'Todos' };

  function toKey(d) { var m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(d || ''); return m ? m[3] + m[2] + m[1] : null; }
  function stamp() {
    var d = new Date(), p2 = function (n) { return (n < 10 ? '0' : '') + n; };
    return p2(d.getDate()) + '/' + p2(d.getMonth() + 1) + ' ' + p2(d.getHours()) + ':' + p2(d.getMinutes());
  }

  function SmartReport(p) {
    var cols = p.columns || DEF_COLUMNS;
    var rows = p.rows || DEF_ROWS;
    var key = p.storageKey || 'alt-smart-report:' + (p.reportId || '1332');
    var fs = R.useState(Object.assign({}, DEF_FILTERS, p.defaultFilters || {})), f = fs[0];
    var ap = R.useState(f), applied = ap[0];
    var ld = R.useState(!!p.defaultLoading), loading = ld[0];
    var st = R.useState({ field: 'emissao', dir: 'asc' }), sort = st[0];
    var mn = R.useState(false), menu = mn[0];
    var sv = R.useState(p.savedAt !== undefined ? p.savedAt : '30/09 14:37');
    var menuRef = R.useRef(null);
    var especies = ['Todos'].concat(rows.map(function (r) { return r.especie; }).filter(function (x, i, a) { return a.indexOf(x) === i; }));

    R.useEffect(function () {
      if (!menu) return;
      function off(e) { if (menuRef.current && !menuRef.current.contains(e.target)) mn[1](false); }
      function esc(e) { if (e.key === 'Escape') mn[1](false); }
      document.addEventListener('mousedown', off); document.addEventListener('keydown', esc);
      return function () { document.removeEventListener('mousedown', off); document.removeEventListener('keydown', esc); };
    }, [menu]);

    function set(k) { return function (e) { var n = Object.assign({}, f); n[k] = e.target.value; fs[1](n); }; }
    function apply() {
      ld[1](true);
      if (p.onFilter) p.onFilter(f);
      setTimeout(function () { ap[1](f); ld[1](false); }, p.loadingMs || 700);
    }
    function clear() { fs[1](DEF_FILTERS); }
    function save() { try { window.localStorage.setItem(key, JSON.stringify(f)); } catch (e) { /* sem storage */ } sv[1](stamp()); mn[1](false); if (p.onSaveSettings) p.onSaveSettings(f); }
    function load() { try { var s = window.localStorage.getItem(key); if (s) fs[1](JSON.parse(s)); } catch (e) { /* sem storage */ } mn[1](false); }
    function wipe() { try { window.localStorage.removeItem(key); } catch (e) { /* sem storage */ } sv[1](null); mn[1](false); }

    var a0 = toKey(applied.inicio), a1 = toKey(applied.fim);
    var data = rows.filter(function (r) {
      var k = toKey(r.emissao);
      if (applied.especie && applied.especie !== 'Todos' && r.especie !== applied.especie) return false;
      if (a0 && k && k < a0) return false;
      if (a1 && k && k > a1) return false;
      if (applied.contaDe && r.conta < applied.contaDe) return false;
      if (applied.contaAte && r.conta > applied.contaAte) return false;
      return true;
    }).sort(function (x, y) {
      var u = x[sort.field], v = y[sort.field];
      if (sort.field === 'emissao') { u = toKey(u); v = toKey(v); }
      var c = u < v ? -1 : u > v ? 1 : 0;
      return sort.dir === 'asc' ? c : -c;
    });
    var total = data.reduce(function (s, r) { return s + (Number(r.valor) || 0); }, 0);

    function exportCsv() {
      if (p.onExport) { p.onExport(data); return; }
      try {
        var csv = [cols.map(function (c) { return c.label; }).join(';')].concat(data.map(function (r) {
          return cols.map(function (c) { return c.format ? c.format(r[c.field]) : r[c.field]; }).join(';');
        })).join('\n');
        var a = document.createElement('a');
        a.href = URL.createObjectURL(new Blob(['﻿' + csv], { type: 'text/csv' }));
        a.download = (p.title || 'relatorio') + '.csv'; a.click();
      } catch (e) { /* sem download */ }
    }

    function ctl(label, k, icon) {
      return h('label', { className: 'alt-field' },
        h('span', { className: 'alt-field__label' }, label),
        h('span', { className: 'alt-ctl alt-ctl--sm' }, icon ? h(Icon, { name: icon, size: 14 }) : null,
          h('input', { value: f[k], onChange: set(k), inputMode: 'numeric' })));
    }

    var grid = h('div', { className: 'alt-smart-report__grid alt-panel', 'aria-busy': loading },
      h('div', { className: 'alt-smart-report__grid-head' },
        h('div', { className: 'alt-smart-report__grid-title' },
          h('h3', null, p.title || 'Relatório 1332 · Livro de Saída'),
          h('span', { className: 'alt-num' }, applied.inicio + ' a ' + applied.fim + ' · ' + nf(data.length) + (data.length === 1 ? ' registro' : ' registros'))),
        h('button', { type: 'button', className: 'alt-tbtn', onClick: exportCsv }, h(Icon, { name: 'download', size: 14 }), 'Exportar')),
      h('div', { className: 'alt-smart-report__table-wrap alt-scroll' },
        h('table', { className: 'alt-smart-report__table alt-num' },
          h('thead', null, h('tr', null, cols.map(function (c) {
            var on = sort.field === c.field;
            return h('th', { key: c.field, 'aria-sort': on ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none', style: { textAlign: c.align || 'left' } },
              h('button', { type: 'button', className: cx('alt-smart-report__sort', on && 'is-on'), onClick: function () { st[1]({ field: c.field, dir: on && sort.dir === 'asc' ? 'desc' : 'asc' }); } },
                c.label, h(Icon, { name: on ? (sort.dir === 'asc' ? 'arrow-up' : 'arrow-down') : 'chevron-down', size: 12 })));
          }))),
          h('tbody', null, data.length ? data.map(function (r, i) {
            return h('tr', { key: r.cte || i }, cols.map(function (c) {
              return h('td', { key: c.field, style: { textAlign: c.align || 'left' } }, c.format ? c.format(r[c.field]) : r[c.field]);
            }));
          }) : h('tr', null, h('td', { colSpan: cols.length },
            h(A.EmptyStates.Item, { variant: 'empty', bare: true, compact: true, description: 'Nenhum documento corresponde aos filtros aplicados.', actionLabel: 'Limpar filtros', onAction: function () { clear(); ap[1](DEF_FILTERS); } })))),
          data.length ? h('tfoot', null, h('tr', null,
            h('td', { colSpan: cols.length - 1 }, 'Total'),
            h('td', { style: { textAlign: 'right' } }, 'R$ ' + nf(total, 2)))) : null)),
      h(A.CustomBackdrop, { open: loading }, 'Gerando relatório…'));

    var panel = h('aside', { className: 'alt-smart-report__filters alt-glass', 'aria-label': 'Filtros' },
      h('div', { className: 'alt-smart-report__fhead' },
        h('span', { className: 'alt-smart-report__ficon' }, h(Icon, { name: 'filter', size: 15 })),
        h('b', null, 'Filtros'),
        h('div', { className: 'alt-smart-report__menu-wrap', ref: menuRef },
          h('button', { type: 'button', className: 'alt-tbtn', 'aria-haspopup': 'menu', 'aria-expanded': menu, onClick: function () { mn[1](!menu); } },
            h(Icon, { name: 'settings', size: 14 }), 'Configurações', h(Icon, { name: 'chevron-down', size: 12 })),
          menu ? h('div', { className: 'alt-smart-report__menu alt-popover', role: 'menu' },
            h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: save }, h(Icon, { name: 'check', size: 16 }), 'Salvar'),
            h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: load }, h(Icon, { name: 'upload', size: 16 }), 'Carregar'),
            h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: wipe }, h(Icon, { name: 'trash', size: 16 }), 'Limpar')) : null)),
      h('div', { className: 'alt-smart-report__sec' },
        h('div', { className: 'alt-smart-report__sec-title' }, 'Período'),
        h('div', { className: 'alt-smart-report__two' }, ctl('Data inicial', 'inicio', 'calendar'), ctl('Data final', 'fim', 'calendar'))),
      h('div', { className: 'alt-smart-report__sec' },
        h('div', { className: 'alt-smart-report__sec-title' }, 'Conta sintética'),
        h('div', { className: 'alt-smart-report__two' }, ctl('De', 'contaDe'), ctl('Até', 'contaAte'))),
      h('div', { className: 'alt-smart-report__sec' },
        h('label', { className: 'alt-field' },
          h('span', { className: 'alt-field__label' }, 'Espécie'),
          h('span', { className: 'alt-ctl alt-ctl--sm' },
            h('select', { value: f.especie, onChange: set('especie') }, especies.map(function (e) { return h('option', { key: e, value: e }, e); })),
            h(Icon, { name: 'chevron-down', size: 14 }))),
        h('span', { className: 'alt-smart-report__saved' }, h(Icon, { name: 'clock', size: 12 }), sv[0] ? 'Configurações salvas em ' + sv[0] : 'Nenhuma configuração salva')),
      h('div', { className: 'alt-smart-report__foot' },
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: clear }, 'Limpar'),
        h(A.Button, { variant: 'primary', size: 'sm', arrow: false, onClick: apply, disabled: loading }, h(Icon, { name: 'filter', size: 14 }), 'Filtrar')));

    return h('div', { className: cx('alt-smart-report', p.className), style: p.style }, grid, panel);
  }

  A.SmartReport = SmartReport;
})();


/* ---- SplitButton ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var DEFAULT_OPTIONS = [
    { name: 'csv', label: 'Exportar CSV', icon: 'download' },
    { name: 'xlsx', label: 'Exportar Excel', icon: 'spreadsheet' },
    { name: 'divider', label: 'Relatórios' },
    { name: 'pdf', label: 'PDF consolidado', icon: 'file', disabled: true }
  ];

  function SplitButton(p) {
    var options = p.options || DEFAULT_OPTIONS;
    var variant = p.variant || 'contained', size = p.size || 'md';
    var canChange = p.canChangeSelection !== false;
    var firstIdx = 0;
    for (var i = 0; i < options.length; i++) if (options[i].name !== 'divider' && !options[i].disabled) { firstIdx = i; break; }
    var sSel = R.useState(p.defaultSelected != null ? p.defaultSelected : firstIdx);
    var sel = p.selected != null ? p.selected : sSel[0];
    var sOpen = R.useState(!!p.defaultOpen), open = sOpen[0], setOpen = sOpen[1];
    var root = R.useRef(null), arrowRef = R.useRef(null), menuRef = R.useRef(null);
    var idRef = R.useRef('alt-split-' + Math.random().toString(36).slice(2, 8));

    R.useEffect(function () {
      if (!open) return;
      function onDoc(e) { if (root.current && !root.current.contains(e.target)) setOpen(false); }
      document.addEventListener('mousedown', onDoc);
      return function () { document.removeEventListener('mousedown', onDoc); };
    }, [open]);

    var current = options[sel] || options[firstIdx];
    function fire(opt, idx) { if (p.callback) p.callback(opt, idx); if (p.onClick) p.onClick(opt, idx); }
    function choose(opt, idx) {
      if (opt.disabled || opt.name === 'divider') return;
      if (canChange) { sSel[1](idx); if (p.onSelect) p.onSelect(opt, idx); }
      else fire(opt, idx);
      setOpen(false);
      if (arrowRef.current) arrowRef.current.focus();
    }
    function focusItem(dir) {
      var m = menuRef.current; if (!m) return;
      var items = Array.prototype.slice.call(m.querySelectorAll('[role="menuitem"]:not([aria-disabled="true"])'));
      var at = items.indexOf(document.activeElement);
      var next = items[(at + dir + items.length) % items.length] || items[0];
      if (next) next.focus();
    }
    R.useEffect(function () { if (open && !p.defaultOpen) focusItem(1); }, [open]);
    function onMenuKey(e) {
      if (e.key === 'ArrowDown') { e.preventDefault(); focusItem(1); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); focusItem(-1); }
      else if (e.key === 'Escape' || e.key === 'Tab') { setOpen(false); if (e.key === 'Escape' && arrowRef.current) arrowRef.current.focus(); }
    }

    var isz = size === 'sm' ? 15 : 16;
    return h('div', { ref: root, className: cx('alt-split-button', 'alt-split-button--' + variant, size === 'sm' && 'alt-split-button--sm', p.disabled && 'alt-split-button--disabled', p.className), style: p.style },
      h('div', { className: 'alt-split-button__group', role: 'group', 'aria-label': p.ariaLabel || (current && current.label) },
        h('button', { type: 'button', className: 'alt-split-button__main', disabled: p.disabled, onClick: function () { fire(current, sel); } },
          current && current.icon ? h(A.Icon, { name: current.icon, size: isz }) : null,
          h('span', { className: 'alt-split-button__label' }, p.label || (current && current.label))),
        h('button', { type: 'button', ref: arrowRef, className: cx('alt-split-button__arrow', open && 'alt-split-button__arrow--open'), disabled: p.disabled,
          'aria-label': p.arrowLabel || 'Mais opções', 'aria-haspopup': 'menu', 'aria-expanded': open, 'aria-controls': idRef.current,
          onClick: function () { setOpen(!open); },
          onKeyDown: function (e) { if (e.key === 'ArrowDown') { e.preventDefault(); setOpen(true); setTimeout(function () { focusItem(1); }, 0); } } },
          h(A.Icon, { name: 'chevron-down', size: isz, className: 'alt-split-button__chev' }))),
      open ? h('div', { id: idRef.current, ref: menuRef, role: 'menu', className: cx('alt-popover', 'alt-split-button__menu', p.align === 'right' && 'alt-split-button__menu--right'), style: { width: p.width || 248 }, onKeyDown: onMenuKey },
        options.map(function (o, idx) {
          if (o.name === 'divider') return h('div', { key: 'd' + idx, className: 'alt-split-button__section', role: 'presentation' }, o.label);
          var isSel = canChange && idx === sel;
          return h('button', { key: o.name || idx, type: 'button', role: 'menuitem', tabIndex: -1, className: cx('alt-menu-item', 'alt-split-button__item', isSel && 'alt-split-button__item--selected', o.disabled && 'alt-split-button__item--disabled'),
            'aria-disabled': o.disabled ? 'true' : undefined, 'aria-current': isSel ? 'true' : undefined,
            onClick: function () { choose(o, idx); } },
            o.icon ? h(A.Icon, { name: o.icon, size: 16 }) : null,
            h('span', { className: 'alt-split-button__item-label' }, o.label),
            isSel ? h(A.Icon, { name: 'check', size: 16, strokeWidth: 2.4, className: 'alt-split-button__check' }) : null);
        })) : null);
  }

  A.SplitButton = SplitButton;
})();


/* ---- StatusChip ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, cx = K.cx;
  var ALIAS = { primary: 'accent', secondary: 'neutral', default: 'neutral' };

  function StatusChip(p) {
    var color = ALIAS[p.color] || p.color || 'error';
    var variant = p.variant || 'soft';
    var t = K.tone(color);
    var label = p.label != null ? p.label : (p.children != null ? p.children : 'Crítica');
    var style = Object.assign({}, p.style);
    if (variant === 'soft') { style.background = t.bg; style.color = t.c; }
    if (variant === 'outlined') { style['--alt-status-chip-c'] = t.c; }
    var iconName = p.icon === true ? t.icon : p.icon;
    var Tag = p.onClick ? 'button' : 'span';
    var main = [
      variant === 'outlined' && !iconName ? h('span', { key: 'd', className: 'alt-dot', style: { color: t.c } }) : null,
      iconName ? h(A.Icon, { key: 'i', name: iconName, size: 13, strokeWidth: 2.2 }) : null,
      variant === 'filter' && p.field ? h('span', { key: 'f', className: 'alt-status-chip__field' }, p.field + ':') : null,
      h('span', { key: 'l', className: 'alt-status-chip__label' }, label)
    ];
    return h('span', { className: cx('alt-chip', 'alt-status-chip', 'alt-status-chip--' + variant, p.size === 'sm' && 'alt-status-chip--sm', p.onDelete && 'alt-status-chip--deletable', p.className), style: style, title: p.title },
      Tag === 'button' ? h('button', { type: 'button', className: 'alt-status-chip__btn', onClick: p.onClick }, main) : main,
      p.onDelete ? h('button', { type: 'button', className: 'alt-status-chip__del', 'aria-label': 'Remover ' + (p.field ? p.field + ' ' : '') + (typeof label === 'string' ? label : ''), onClick: p.onDelete }, h(A.Icon, { name: 'x', size: 13, strokeWidth: 2.2 })) : null);
  }

  A.StatusChip = StatusChip;
})();


/* ---- StatusCircle ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var ALIAS = { primary: 'accent', secondary: 'neutral', default: 'neutral', disabled: 'neutral' };
  var DIM = { small: 8, medium: 10, large: 12 }, FONT = { small: 12, medium: 14, large: 16 };

  function StatusCircle(p) {
    var color = ALIAS[p.color] || p.color || 'success';
    var size = DIM[p.size] ? p.size : 'medium';
    var c = color === 'neutral' ? 'var(--slate)' : K.tone(color).c;
    var z = DIM[size];
    var text = p.text !== undefined ? p.text : (p.tooltip ? null : 'Em dia');
    var s = R.useState(false), show = p.tooltipOpen != null ? p.tooltipOpen : s[0];
    var dot = h('span', {
      className: cx('alt-status-circle__dot', !p.solidColor && 'alt-status-circle__dot--halo', p.pulse && 'alt-status-circle__dot--pulse'),
      style: { width: z, height: z, margin: p.solidColor ? 0 : Math.round(z * 0.35), background: c, '--alt-status-circle-c': c, '--alt-status-circle-halo': Math.round(z * 0.35) + 'px' },
      role: text ? undefined : 'img', 'aria-label': text ? undefined : p.tooltip
    });
    var hasTip = !!p.tooltip;
    var root = h('span', {
      className: cx('alt-status-circle', hasTip && 'alt-status-circle--tip', p.className),
      style: Object.assign({ fontSize: FONT[size] }, p.style),
      tabIndex: hasTip && !text ? 0 : undefined,
      onMouseEnter: hasTip ? function () { s[1](true); } : undefined, onMouseLeave: hasTip ? function () { s[1](false); } : undefined,
      onFocus: hasTip ? function () { s[1](true); } : undefined, onBlur: hasTip ? function () { s[1](false); } : undefined
    }, dot, text != null ? h('span', { className: 'alt-status-circle__text' }, text) : null,
      hasTip && show ? h('span', { role: 'tooltip', className: cx('alt-status-circle__tip', p.tooltipPlacement === 'right' && 'alt-status-circle__tip--right') }, p.tooltip) : null);
    return p.center ? h('span', { className: 'alt-status-circle__center' }, root) : root;
  }

  A.StatusCircle = StatusCircle;
})();


/* ---- Tabs ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  function Summary(title, meta) {
    return h('div', { className: 'alt-tabs__sample' }, h('div', { className: 'alt-tabs__sample-title' }, title), h('div', { className: 'alt-tabs__sample-meta alt-num' }, meta));
  }
  var DEFAULT_TABS = [
    { label: 'Resumo', content: Summary('Viagem 88213 · ABC-1D23', 'João Pereira · CCO Sul · 3 violações registradas') },
    { label: 'Eventos em viagem', count: 12, content: Summary('12 eventos desde a saída de Campinas', 'Último: parada não programada às 14:12 · km 102') },
    { label: 'Justificativas', count: 3, content: Summary('3 justificativas aguardando aprovação', 'Prazo: até 48 h após o fim da viagem') },
    { label: 'Custos', disabled: true }
  ];

  function Tabs(p) {
    var tabs = p.tabs || DEFAULT_TABS;
    var s = R.useState(p.defaultValue || 0);
    var value = p.value != null ? p.value : s[0];
    var idBase = R.useRef(p.id || ('alt-tabs-' + Math.random().toString(36).slice(2, 8))).current;
    var refs = R.useRef([]);
    function select(i) {
      if (tabs[i] && tabs[i].disabled) return;
      if (p.value == null) s[1](i);
      if (p.onChange) p.onChange(i, tabs[i]);
    }
    function move(from, dir) {
      var n = tabs.length;
      for (var k = 1; k <= n; k++) {
        var j = dir === 'first' ? k - 1 : dir === 'last' ? n - k : (from + dir * k + n * k) % n;
        if (!tabs[j].disabled) { select(j); if (refs.current[j]) refs.current[j].focus(); return; }
      }
    }
    function onKey(e, i) {
      if (e.key === 'ArrowRight') { e.preventDefault(); move(i, 1); }
      else if (e.key === 'ArrowLeft') { e.preventDefault(); move(i, -1); }
      else if (e.key === 'Home') { e.preventDefault(); move(i, 'first'); }
      else if (e.key === 'End') { e.preventDefault(); move(i, 'last'); }
    }
    var variant = p.variant || 'card';
    var list = h('div', { role: 'tablist', 'aria-label': p.ariaLabel, className: 'alt-tabs__list' },
      tabs.map(function (t, i) {
        var on = i === value;
        return h('button', {
          key: i, type: 'button', role: 'tab', id: idBase + '-tab-' + i, 'aria-selected': on, 'aria-controls': idBase + '-panel-' + i,
          tabIndex: on ? 0 : -1, disabled: t.disabled, ref: function (el) { refs.current[i] = el; },
          className: cx('alt-tabs__tab', on && 'alt-tabs__tab--active'), onClick: function () { select(i); }, onKeyDown: function (e) { onKey(e, i); }
        },
          t.icon ? h(A.Icon, { name: t.icon, size: 15 }) : null,
          h('span', null, t.label),
          t.count != null ? h('span', { className: 'alt-tabs__count alt-num' }, t.count) : null);
      }));
    var cur = tabs[value] || {};
    var panel = p.panels === false ? null : h('div', { role: 'tabpanel', id: idBase + '-panel-' + value, 'aria-labelledby': idBase + '-tab-' + value, tabIndex: 0, className: 'alt-tabs__panel' },
      p.renderPanel ? p.renderPanel(cur, value) : (cur.content != null ? cur.content : p.children));
    return h('div', { className: cx('alt-tabs', 'alt-tabs--' + variant, p.size === 'sm' && 'alt-tabs--sm', variant === 'card' && 'alt-glass', p.className), style: p.style }, list, panel);
  }

  A.Tabs = Tabs;
})();


/* ---- ThemeSwitch ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var DEFAULT_OPTIONS = [
    { value: 'light', label: 'Claro', icon: 'sun', hint: 'Paleta clara definida pelo usuário' },
    { value: 'dark', label: 'Escuro', icon: 'moon', hint: 'Paleta escura definida pelo usuário' },
    { value: 'system', label: 'Sistema', icon: 'monitor', hint: 'Paleta de cores automática' }
  ];
  function systemMode() {
    try { return window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches ? 'claro' : 'escuro'; } catch (e) { return 'escuro'; }
  }

  function ThemeSwitch(p) {
    var options = p.options || DEFAULT_OPTIONS;
    var s = R.useState(p.defaultValue || 'dark');
    var value = p.value != null ? p.value : s[0];
    var refs = R.useRef([]);
    var showLabels = p.showLabels !== false;
    function set(v, i) {
      if (p.value == null) s[1](v);
      if (p.onChange) p.onChange(v, options[i]);
      if (p.onClick) p.onClick(v);
    }
    function onKey(e, i) {
      var d = e.key === 'ArrowRight' || e.key === 'ArrowDown' ? 1 : e.key === 'ArrowLeft' || e.key === 'ArrowUp' ? -1 : 0;
      if (!d) return;
      e.preventDefault();
      var j = (i + d + options.length) % options.length;
      set(options[j].value, j);
      if (refs.current[j]) refs.current[j].focus();
    }
    var showHint = p.showHint !== false && value === 'system';
    return h('div', { className: cx('alt-theme-switch', p.size === 'sm' && 'alt-theme-switch--sm', p.className), style: p.style },
      h('div', { role: 'radiogroup', 'aria-label': p.ariaLabel || 'Tema', className: 'alt-theme-switch__group' },
        options.map(function (o, i) {
          var on = o.value === value;
          return h('button', {
            key: o.value, type: 'button', role: 'radio', 'aria-checked': on, tabIndex: on ? 0 : -1,
            'aria-label': showLabels ? undefined : o.label, title: o.hint || o.label,
            ref: function (el) { refs.current[i] = el; },
            className: cx('alt-theme-switch__opt', on && 'alt-theme-switch__opt--on', !showLabels && 'alt-theme-switch__opt--icon'),
            onClick: function () { set(o.value, i); }, onKeyDown: function (e) { onKey(e, i); }
          }, h(A.Icon, { name: o.icon, size: 16 }), showLabels ? h('span', null, o.label) : null);
        })),
      showHint ? h('span', { className: 'alt-theme-switch__hint', 'aria-live': 'polite' }, 'Segue o sistema · ', systemMode(), ' agora') : null);
  }

  A.ThemeSwitch = ThemeSwitch;
})();


/* ---- Toast ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var AUTO = { success: 4000, info: 4000, warning: 6000, error: false };
  var ICON = { success: 'check-circle', error: 'alert', warning: 'info', info: 'info' };
  var WORD = { success: 'Sucesso', error: 'Erro', warning: 'Atenção', info: 'Informação' };
  var seq = 0;
  var listeners = [];

  /* notify.success / error / warning / info (message, { title, items, autoClose }) → every mounted Toast with listen */
  var notify = {};
  ['success', 'error', 'warning', 'info'].forEach(function (type) {
    notify[type] = function (message, opts) {
      var t = Object.assign({ id: 'n' + (++seq), type: type, message: message }, opts || {});
      listeners.forEach(function (fn) { fn(t); });
      return t.id;
    };
  });
  notify.warn = notify.warning;

  var DEFAULT_TOASTS = [
    { id: 'd1', type: 'success', message: 'Entrega cancelada.' },
    { id: 'd2', type: 'error', title: 'Não foi possível salvar o pedido', items: ['Informe a rota.', 'O volume deve ser maior que zero.'] },
    { id: 'd3', type: 'warning', message: 'Só é possível anexar mais 2 arquivos.' }
  ];

  function ToastItem(p) {
    var t = p.toast, type = t.type || 'info', tn = K.tone(type);
    var auto = t.autoClose !== undefined ? t.autoClose : AUTO[type];
    var s = R.useState(false), paused = s[0], setPaused = s[1];
    var role = type === 'error' ? 'alert' : 'status';
    return h('div', {
      className: cx('alt-glass', 'alt-glass--overlay', 'alt-toast__item', 'alt-toast__item--' + type), role: role, 'aria-live': type === 'error' ? 'assertive' : 'polite',
      onMouseEnter: function () { setPaused(true); }, onMouseLeave: function () { setPaused(false); },
      onFocus: function () { setPaused(true); }, onBlur: function () { setPaused(false); }
    },
      h('span', { className: 'alt-toast__icon', style: { background: tn.bg, color: tn.c } }, h(A.Icon, { name: t.icon || ICON[type], size: 16, title: WORD[type] })),
      h('div', { className: 'alt-toast__body' },
        t.title ? h('div', { className: 'alt-toast__title' }, t.title) : null,
        t.message ? h('p', { className: 'alt-toast__msg' }, t.message) : null,
        t.items && t.items.length ? h('ul', { className: 'alt-toast__list' }, t.items.map(function (it, i) { return h('li', { key: i }, it); })) : null,
        t.action ? h('button', { type: 'button', className: 'alt-toast__action', onClick: function () { t.action.onClick && t.action.onClick(); p.onDismiss(t.id); } }, t.action.label) : null),
      h('button', { type: 'button', className: 'alt-toast__close', 'aria-label': 'Fechar notificação', onClick: function () { p.onDismiss(t.id); } }, h(A.Icon, { name: 'x', size: 15 })),
      auto ? h('span', {
        className: 'alt-toast__progress', 'aria-hidden': 'true',
        style: { background: tn.c, animationDuration: auto + 'ms', animationPlayState: paused ? 'paused' : 'running' },
        onAnimationEnd: function () { p.onDismiss(t.id); }
      }) : null);
  }

  function Toast(p) {
    var s = R.useState(p.defaultToasts || DEFAULT_TOASTS), list = s[0], setList = s[1];
    var controlled = !!p.toasts;
    var toasts = controlled ? p.toasts : list;
    var limit = p.limit || 3;
    var newestOnTop = p.newestOnTop !== false;
    R.useEffect(function () {
      if (p.listen === false) return;
      function add(t) {
        if (p.onAdd) p.onAdd(t);
        if (!controlled) setList(function (cur) { return newestOnTop ? [t].concat(cur) : cur.concat([t]); });
      }
      listeners.push(add);
      return function () { listeners = listeners.filter(function (f) { return f !== add; }); };
    }, [controlled, newestOnTop, p.listen]);
    function dismiss(id) {
      if (p.onDismiss) p.onDismiss(id);
      if (!controlled) setList(function (cur) { return cur.filter(function (t) { return t.id !== id; }); });
    }
    var shown = newestOnTop ? toasts.slice(0, limit) : toasts.slice(-limit);
    var pos = p.position || 'inline';
    return h('section', { className: cx('alt-toast', 'alt-toast--' + pos, p.className), style: p.style, 'aria-label': p.ariaLabel || 'Notificações' },
      shown.map(function (t) { return h(ToastItem, { key: t.id, toast: t, onDismiss: dismiss }); }));
  }

  Toast.notify = notify;
  A.notify = notify;
  A.Toast = Toast;
})();


/* ---- Utilities ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;
  var tipSeq = 0;

  /* Tooltip: inverted chip with a small arrow; shows on hover/focus, or always with open. */
  function Tooltip(p) {
    var s = R.useState(false), show = p.open != null ? p.open : s[0];
    var id = R.useRef('alt-utilities-tip-' + (++tipSeq)).current;
    var child = R.Children.only(p.children);
    var trigger = R.cloneElement(child, { 'aria-describedby': show ? id : undefined });
    return h('span', {
      className: cx('alt-utilities__tipwrap', p.className),
      onMouseEnter: function () { s[1](true); }, onMouseLeave: function () { s[1](false); },
      onFocus: function () { s[1](true); }, onBlur: function () { s[1](false); },
      onKeyDown: function (e) { if (e.key === 'Escape') s[1](false); }
    }, trigger,
      show ? h('span', { role: 'tooltip', id: id, className: cx('alt-utilities__tip', 'alt-utilities__tip--' + (p.placement || 'top')) }, p.title) : null);
  }

  /* TabPanel: same contract as CustomTabPanel (role, ids simple-tabpanel-{index}). */
  function TabPanel(p) {
    var hidden = p.value !== p.index;
    return h('div', { role: 'tabpanel', hidden: hidden, id: (p.idPrefix || 'simple-tabpanel') + '-' + p.index, 'aria-labelledby': (p.tabIdPrefix || 'simple-tab') + '-' + p.index, className: cx('alt-utilities__panel', p.className) },
      hidden ? null : p.children);
  }

  function Copyright(p) {
    var year = p.year || new Date().getFullYear();
    return h('div', { className: cx('alt-utilities__copyright', p.className) },
      p.version ? h('span', { className: 'alt-num' }, 'Versão ' + p.version) : null,
      p.version ? h('span', { 'aria-hidden': 'true' }, ' · ') : null,
      'Copyright © ', p.href ? h('a', { href: p.href, target: '_blank', rel: 'noopener' }, p.owner || 'TRASSE') : (p.owner || 'TRASSE'), ' ', h('span', { className: 'alt-num' }, year));
  }

  function ErrorFallback(p) {
    return h('div', { role: 'alertdialog', 'aria-labelledby': 'alt-utilities-err-t', className: cx('alt-glass', 'alt-glass--overlay', 'alt-utilities__error', p.className) },
      h('span', { className: 'alt-utilities__error-icon' }, h(A.Icon, { name: 'alert', size: 20, title: 'Erro' })),
      h('div', { className: 'alt-utilities__error-body' },
        h('div', { id: 'alt-utilities-err-t', className: 'alt-utilities__error-title' }, p.title || 'Algo deu errado nesta tela'),
        h('p', { className: 'alt-utilities__error-text' }, p.text || 'O erro foi registrado. Recarregue a página para continuar; se persistir, abra um chamado no HelpDesk.'),
        h('div', { className: 'alt-utilities__error-actions' },
          h(A.Button, { size: 'xs', variant: 'outline', onClick: p.onSupport }, 'Abrir chamado'),
          h(A.Button, { size: 'xs', startIcon: 'refresh', onClick: p.onRetry || function () { window.location.reload(); } }, 'Recarregar'))));
  }

  /* ErrorBoundary: blocks the screen on purpose with the fallback above. */
  function ErrorBoundary(props) { R.Component.call(this, props); this.state = { error: null }; }
  ErrorBoundary.prototype = Object.create(R.Component.prototype);
  ErrorBoundary.prototype.constructor = ErrorBoundary;
  ErrorBoundary.getDerivedStateFromError = function (error) { return { error: error }; };
  ErrorBoundary.prototype.componentDidCatch = function (error, info) { if (this.props.onError) this.props.onError(error, info); };
  ErrorBoundary.prototype.render = function () {
    var self = this;
    if (!this.state.error) return this.props.children;
    return this.props.fallback || h(ErrorFallback, { onRetry: function () { self.setState({ error: null }); if (self.props.onReset) self.props.onReset(); }, onSupport: this.props.onSupport });
  };

  /* Utilities: showcase of the four helpers with the source's example content. */
  function Utilities(p) {
    var tabs = p.tabs || [
      { label: 'Resumo', content: [h('b', { key: 'b' }, 'QRT-4F56'), ' · Marta Souza · ', h('span', { key: 'n', className: 'alt-num' }, '842,0 km'), ' rodados hoje'] },
      { label: 'Violações', content: '2 violações leves · nenhuma crítica' },
      { label: 'Histórico', content: 'Última viagem encerrada às 11:40 em Jundiaí' }
    ];
    var s = R.useState(0), v = s[0];
    return h('div', { className: cx('alt-utilities', p.className), style: p.style },
      h('div', { className: 'alt-utilities__top' },
        h('div', { className: 'alt-utilities__tipdemo' },
          h(Tooltip, { title: p.tooltip || 'Utilizar este valor', open: p.tooltipOpen != null ? p.tooltipOpen : true },
            h('button', { type: 'button', className: 'alt-tbtn' }, h(A.Icon, { name: 'copy', size: 15 }), h('span', { className: 'alt-num' }, p.value || '1.284,5 km')))),
        h('div', { className: 'alt-glass alt-utilities__tabs' },
          h('div', { role: 'tablist', className: 'alt-utilities__tablist' }, tabs.map(function (t, i) {
            return h('button', { key: i, type: 'button', role: 'tab', id: 'simple-tab-' + i, 'aria-selected': v === i, 'aria-controls': 'simple-tabpanel-' + i, tabIndex: v === i ? 0 : -1,
              className: 'alt-utilities__tab', onClick: function () { s[1](i); } }, t.label);
          })),
          tabs.map(function (t, i) { return h(TabPanel, { key: i, value: v, index: i }, t.content); }))),
      h(Copyright, { version: p.version || '3.18.0', owner: p.owner || 'TRASSE', href: p.href || 'https://www.trasse.com.br/', year: p.year || 2026 }));
  }

  Utilities.Tooltip = Tooltip;
  Utilities.TabPanel = TabPanel;
  Utilities.Copyright = Copyright;
  Utilities.ErrorBoundary = ErrorBoundary;
  Utilities.ErrorFallback = ErrorFallback;
  A.Utilities = Utilities;
})();


/* ---- UxClientesAdmin ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  var DEF_PAGADORES = [
    { id: 40211, nome: 'Vidroporto PF', uf: 'SP' }, { id: 40388, nome: 'Vidroporto Recife', uf: 'PE' }, { id: 40412, nome: 'Vidroporto MG', uf: 'MG' },
    { id: 40455, nome: 'Vidroporto Sul', uf: 'RS' }, { id: 40501, nome: 'Vidroporto Nordeste', uf: 'BA' }, { id: 51007, nome: 'Cebrace Jacareí', uf: 'SP' },
    { id: 51210, nome: 'Cebrace Barra Velha', uf: 'SC' }, { id: 51320, nome: 'Cebrace Caçapava', uf: 'SP' }
  ];
  var DEF_CLIENTES = [
    { id: 1, nome: 'Vidroporto S.A.', contato: 'Luciana Prado', email: 'logistica@vidroporto.com.br', telefone: '(19) 3589-1200', ativo: true, pagadores: [40211, 40388, 40501] },
    { id: 2, nome: 'Cebrace', contato: 'Fábio Teles', email: 'transportes@cebrace.com.br', telefone: '(12) 3954-7000', ativo: true, pagadores: [51007] },
    { id: 3, nome: 'Saint-Gobain Glass', contato: 'Renata Lopes', email: 'frete@sgglass.com.br', telefone: '(11) 2246-7600', ativo: true, pagadores: [] },
    { id: 4, nome: 'Vidros Paulista', contato: 'Ivo Matos', email: 'compras@vidrospaulista.com.br', telefone: '(11) 4002-1180', ativo: false, pagadores: [51320] }
  ];

  function Check(p) {
    return h('span', { className: cx('alt-ux-clientes-admin__cb', p.on && 'is-on', p.mixed && 'is-mixed'), 'aria-hidden': 'true' },
      p.on ? h(A.Icon, { name: 'check', size: 11, strokeWidth: 3 }) : p.mixed ? h(A.Icon, { name: 'minus', size: 11, strokeWidth: 3 }) : null);
  }

  function TList(p) {
    var sQ = R.useState(''), q = sQ[0].trim().toLowerCase();
    var shown = p.items.filter(function (x) { return !q || (x.nome + ' ' + x.uf + ' ' + x.id).toLowerCase().indexOf(q) >= 0; });
    var nSel = p.items.filter(function (x) { return p.checked[x.id]; }).length;
    var all = shown.length && shown.every(function (x) { return p.checked[x.id]; });
    function toggleAll() { var c = Object.assign({}, p.checked); shown.forEach(function (x) { c[x.id] = !all; }); p.onCheck(c); }
    return h('div', { className: 'alt-panel alt-ux-clientes-admin__tl' },
      h('div', { className: 'alt-ux-clientes-admin__tlh' },
        h('button', { type: 'button', role: 'checkbox', 'aria-checked': all ? 'true' : nSel ? 'mixed' : 'false', 'aria-label': 'Selecionar todos em ' + p.title, className: 'alt-ux-clientes-admin__cbtn', onClick: toggleAll, disabled: !shown.length },
          h(Check, { on: all, mixed: !all && nSel > 0 })),
        h('b', null, p.title), h('span', { className: 'alt-num' }, nSel + '/' + p.items.length + ' selecionados')),
      h('label', { className: 'alt-ctl alt-ctl--sm alt-ux-clientes-admin__tls' }, h(A.Icon, { name: 'search', size: 14 }),
        h('input', { placeholder: 'Buscar', 'aria-label': 'Buscar em ' + p.title, value: sQ[0], onChange: function (e) { sQ[1](e.target.value); } })),
      h('div', { className: 'alt-ux-clientes-admin__tli alt-scroll', role: 'group', 'aria-label': p.title },
        shown.length ? shown.map(function (x) {
          var on = !!p.checked[x.id];
          return h('button', { key: x.id, type: 'button', role: 'checkbox', 'aria-checked': on, className: 'alt-ux-clientes-admin__ti alt-num',
            onClick: function () { var c = Object.assign({}, p.checked); c[x.id] = !on; p.onCheck(c); } },
            h(Check, { on: on }), h('span', { className: 'alt-ux-clientes-admin__tin' }, x.nome + ' · ' + x.uf), h('small', null, x.id));
        }) : h('div', { className: 'alt-ux-clientes-admin__tle' }, p.empty)));
  }

  function UxClientesAdmin(p) {
    var pagadores = p.pagadores || DEF_PAGADORES;
    var sC = R.useState(p.clientes || DEF_CLIENTES), clientes = sC[0];
    var sSel = R.useState(p.defaultSelectedId != null ? p.defaultSelectedId : (p.clientes || DEF_CLIENTES)[0].id), selId = sSel[0];
    var cli = clientes.filter(function (c) { return c.id === selId; })[0];
    var sD = R.useState(cli), draft = sD[0], setDraft = sD[1];
    var sCol = R.useState(!!p.defaultCollapsed), collapsed = sCol[0];
    var sQ = R.useState(''), q = sQ[0].trim().toLowerCase();
    var sL = R.useState({}), sR = R.useState({});
    var sPw = R.useState('idle'), pw = sPw[0];
    var sSaved = R.useState(false), saved = sSaved[0];

    function choose(c) { sSel[1](c.id); setDraft(c); sL[1]({}); sR[1]({}); sPw[1]('idle'); sSaved[1](false); if (p.onSelect) p.onSelect(c); }
    function set(k, v) { var d = Object.assign({}, draft); d[k] = v; setDraft(d); sSaved[1](false); }
    var dirty = JSON.stringify(draft) !== JSON.stringify(cli);
    function save() {
      sC[1](clientes.map(function (c) { return c.id === draft.id ? draft : c; })); sSaved[1](true);
      if (p.onSave) p.onSave(draft);
    }
    var linked = pagadores.filter(function (x) { return draft.pagadores.indexOf(x.id) >= 0; });
    var avail = pagadores.filter(function (x) { return draft.pagadores.indexOf(x.id) < 0; });
    function moveOut() { set('pagadores', draft.pagadores.filter(function (id) { return !sL[0][id]; })); sL[1]({}); }
    function moveIn() { set('pagadores', draft.pagadores.concat(avail.filter(function (x) { return sR[0][x.id]; }).map(function (x) { return x.id; }))); sR[1]({}); }
    var nL = linked.filter(function (x) { return sL[0][x.id]; }).length, nR = avail.filter(function (x) { return sR[0][x.id]; }).length;
    var list = clientes.filter(function (c) { return !q || c.nome.toLowerCase().indexOf(q) >= 0; });

    function field(k, label, type) {
      return h('label', { className: 'alt-field' }, h('span', { className: 'alt-field__label' }, label),
        h('span', { className: 'alt-ctl alt-ctl--sm' }, h('input', { type: type || 'text', value: draft[k] || '', onChange: function (e) { set(k, e.target.value); } })));
    }

    return h('div', { className: cx('alt-ux-clientes-admin', collapsed && 'is-collapsed', p.className), style: p.style },
      h('nav', { className: 'alt-glass alt-ux-clientes-admin__master', 'aria-label': 'Clientes' },
        h('div', { className: 'alt-ux-clientes-admin__mh' },
          collapsed ? null : h('h2', null, 'Clientes'), collapsed ? null : h('span', { className: 'alt-num' }, clientes.length),
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': collapsed ? 'Expandir lista' : 'Recolher lista', 'aria-expanded': !collapsed, onClick: function () { sCol[1](!collapsed); } },
            h(A.Icon, { name: collapsed ? 'chevrons-right' : 'chevrons-left', size: 16 }))),
        collapsed ? h('span', { className: 'alt-ux-clientes-admin__mic', title: cli ? cli.nome : '' }, h(A.Icon, { name: 'building', size: 18 })) : h(R.Fragment, null,
          h('label', { className: 'alt-ctl alt-ctl--sm alt-ux-clientes-admin__ms' }, h(A.Icon, { name: 'search', size: 14 }),
            h('input', { placeholder: 'Buscar cliente', 'aria-label': 'Buscar cliente', value: sQ[0], onChange: function (e) { sQ[1](e.target.value); } })),
          h('div', { className: 'alt-ux-clientes-admin__ml alt-scroll' }, list.map(function (c) {
            var n = (c.id === selId ? draft : c).pagadores.length;
            return h('button', { key: c.id, type: 'button', className: 'alt-ux-clientes-admin__li', 'aria-current': c.id === selId ? 'true' : undefined, onClick: function () { choose(c); } },
              h('span', { className: 'alt-ux-clientes-admin__lit' }, h('b', null, c.nome),
                h('small', { className: 'alt-num' }, (n === 1 ? '1 pagador' : n + ' pagadores') + (c.ativo ? '' : ' · inativo'))),
              h(A.Icon, { name: 'chevron-right', size: 15 }));
          })))),
      cli ? h('section', { className: 'alt-glass alt-ux-clientes-admin__det', 'aria-label': 'Cliente ' + draft.nome },
        h('header', { className: 'alt-ux-clientes-admin__dh' },
          h('div', { className: 'alt-ux-clientes-admin__dht' }, h('h2', null, draft.nome), h('span', null, 'Usuário-cliente · ' + draft.email)),
          h('button', { type: 'button', role: 'switch', 'aria-checked': !!draft.ativo, className: 'alt-ux-clientes-admin__sw', onClick: function () { set('ativo', !draft.ativo); } },
            h('span', { className: 'alt-ux-clientes-admin__swt' }, h('i')), draft.ativo ? 'Ativo' : 'Inativo')),
        h('div', { className: 'alt-ux-clientes-admin__form' },
          field('nome', 'Razão social'), field('contato', 'Contato'), field('email', 'E-mail', 'email'), field('telefone', 'Telefone', 'tel')),
        h('div', { className: 'alt-ux-clientes-admin__pw', role: 'status' },
          pw === 'idle' ? h(R.Fragment, null, h(A.Icon, { name: 'key', size: 16 }), h('span', null, 'Senha de acesso ao painel do cliente'),
            h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { sPw[1]('confirm'); } }, 'Trocar senha'))
          : pw === 'confirm' ? h(R.Fragment, null, h(A.Icon, { name: 'alert', size: 16, style: { color: tone('warning').c } }),
            h('span', null, 'Enviar um link de troca de senha para ', h('b', null, draft.email), '?'),
            h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { sPw[1]('idle'); } }, 'Cancelar'),
            h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { sPw[1]('sent'); if (p.onResetPassword) p.onResetPassword(draft); } }, 'Enviar link'))
          : h(R.Fragment, null, h(A.Icon, { name: 'check-circle', size: 16, style: { color: tone('success').c } }), h('span', null, 'Link enviado para ' + draft.email))),
        h('div', { className: 'alt-ux-clientes-admin__tr' },
          h('div', { className: 'alt-ux-clientes-admin__trh' }, h('h3', null, 'Pagadores'), h('span', { className: 'alt-num' }, linked.length + ' de ' + pagadores.length + ' vinculados')),
          h('div', { className: 'alt-ux-clientes-admin__trg' },
            h(TList, { title: 'Vinculados', items: linked, checked: sL[0], onCheck: sL[1], empty: 'Nenhum pagador vinculado' }),
            h('div', { className: 'alt-ux-clientes-admin__mid' },
              h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon', disabled: !nL, onClick: moveOut, 'aria-label': 'Remover selecionados', title: 'Remover' }, h(A.Icon, { name: 'arrow-right', size: 15 })),
              h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon', disabled: !nR, onClick: moveIn, 'aria-label': 'Adicionar selecionados', title: 'Adicionar' }, h(A.Icon, { name: 'arrow-left', size: 15 }))),
            h(TList, { title: 'Disponíveis', items: avail, checked: sR[0], onCheck: sR[1], empty: 'Todos os pagadores já estão vinculados' }))),
        h('footer', { className: 'alt-ux-clientes-admin__df' },
          h('span', { className: 'alt-ux-clientes-admin__state', 'aria-live': 'polite' },
            saved ? h(R.Fragment, null, h(A.Icon, { name: 'check', size: 14, style: { color: tone('success').c } }), 'Alterações salvas') : dirty ? 'Alterações não salvas' : ''),
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', disabled: !dirty, onClick: function () { setDraft(cli); } }, 'Descartar'),
          h('button', { type: 'button', className: 'alt-btn alt-btn--primary alt-ux-clientes-admin__primary', disabled: !dirty, onClick: save }, 'Salvar'))) : null);
  }
  A.UxClientesAdmin = UxClientesAdmin;
})();


/* ---- UxEntregaProgresso ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, tone = K.tone;

  var STEPS = ['Chegada carregamento', 'Fim carregamento', 'Em trânsito', 'Chegada destino', 'Entrega realizada'];
  var STATUS = {
    transit: { tone: 'info', icon: 'truck', label: 'Em trânsito' },
    stopped: { tone: 'warning', icon: 'pause', label: 'Parado' },
    loading: { tone: 'accent', icon: 'package', label: 'Carregando' },
    done: { tone: 'success', icon: 'check-circle', label: 'Finalizada' },
    nosignal: { tone: 'neutral', icon: 'wifi-off', label: 'Sem sinal' }
  };
  var DEF = [
    { id: 'ABC-1D23', plate: 'ABC-1D23', vehicle: 'Bitrem', driver: 'João Pereira', from: 'Paulínia', to: 'Porto Ferreira', status: 'transit', step: 2,
      times: ['06:10', '07:42'], eta: { kind: 'eta', value: '17:45', note: 'em 2h 05min' } },
    { id: 'KLM-7G89', plate: 'KLM-7G89', vehicle: 'Carreta', driver: 'Rafael Nunes', from: 'Jundiaí', to: 'Recife', status: 'stopped', statusText: 'Parado há 2h', step: 2,
      times: ['05:20', '06:55'], eta: { kind: 'eta', value: '02/10 09:30', note: 'atraso previsto de 1h' } },
    { id: 'RST-2B34', plate: 'RST-2B34', vehicle: 'Truck', driver: 'Luiz Andrade', from: 'Paulínia', to: 'Campinas', status: 'transit', step: 3,
      times: ['08:00', '08:50', '09:05'], eta: { kind: 'arrived' } },
    { id: 'MNO-5C67', plate: 'MNO-5C67', vehicle: 'Toco', driver: 'Sérgio Dias', from: 'Jacareí', to: 'São José dos Campos', status: 'nosignal', step: 1,
      times: ['09:12'], eta: { kind: 'nogeo' } },
    { id: 'QRT-4F56', plate: 'QRT-4F56', vehicle: 'Carreta', driver: 'Marta Souza', from: 'Jacareí', to: 'Santos', status: 'done', step: 5,
      times: ['04:30', '05:45', '06:00', '09:10', '09:48'], eta: { kind: 'done', value: '09:48' } }
  ];

  function Eta(e) {
    if (!e) return null;
    if (e.kind === 'eta') return h('div', { className: 'alt-ux-entrega-progresso__eta' }, h('span', null, 'ETA'), h('b', { className: e.value.length > 6 ? 'is-long' : '' }, e.value), e.note ? h('small', null, e.note) : null);
    var m = { arrived: ['pin', 'Chegou ao destino', 'success'], nogeo: ['wifi-off', 'Sem dados de geolocalização', 'neutral'], done: ['check-circle', 'Finalizada', 'success'] }[e.kind] || ['info', e.kind, 'neutral'];
    return h('div', { className: 'alt-ux-entrega-progresso__eta' }, h('span', null, e.kind === 'done' ? 'Entrega' : 'ETA'),
      h('div', { className: 'alt-ux-entrega-progresso__eta-st', style: { color: m[2] === 'neutral' ? 'var(--muted)' : tone(m[2]).c } }, h(A.Icon, { name: m[0], size: 15 }), m[1]),
      e.value ? h('small', null, 'às ' + e.value) : null);
  }

  function Track(p) {
    var steps = p.steps, n = steps.length, done = Math.min(p.step, n);
    var pct = n > 1 ? Math.max(0, Math.min(1, (done - 1) / (n - 1))) : 0;
    return h('ol', { className: 'alt-ux-entrega-progresso__track', style: { '--n': n }, 'aria-label': 'Progresso: ' + done + ' de ' + n + ' etapas concluídas' },
      h('span', { className: 'alt-ux-entrega-progresso__rail', 'aria-hidden': 'true' }, h('i', { style: { width: (pct * 100) + '%' } })),
      steps.map(function (s, i) {
        var st = i < done ? 'ok' : i === done ? 'cur' : 'todo';
        return h('li', { key: i, className: 'alt-ux-entrega-progresso__st is-' + st, 'aria-current': st === 'cur' ? 'step' : undefined },
          h('span', { className: 'alt-ux-entrega-progresso__dot' }, st === 'ok' ? h(A.Icon, { name: 'check', size: 13, strokeWidth: 3 }) : null),
          h('span', { className: 'alt-ux-entrega-progresso__lbl' }, s),
          h('span', { className: 'alt-ux-entrega-progresso__time alt-num' }, p.times && p.times[i] ? p.times[i] : st === 'cur' ? 'atual' : '—'));
      }));
  }

  function UxEntregaProgresso(p) {
    var items = p.items || DEF, steps = p.steps || STEPS;
    var sSel = R.useState(p.defaultSelectedId !== undefined ? p.defaultSelectedId : null), sel = p.selectedId !== undefined ? p.selectedId : sSel[0];
    function pick(it) { sSel[1](it.id); if (p.onSelect) p.onSelect(it); }
    return h('section', { className: cx('alt-glass', 'alt-ux-entrega-progresso', p.className), style: p.style, 'aria-label': p.title || 'Entregas' },
      p.title === null ? null : h('header', { className: 'alt-ux-entrega-progresso__hd' },
        h('h3', null, p.title || 'Entregas em andamento'), h('span', { className: 'alt-num' }, items.length + ' veículos')),
      h('ul', { className: 'alt-ux-entrega-progresso__list' }, items.map(function (it) {
        var S = STATUS[it.status] || STATUS.transit, t = tone(S.tone);
        return h('li', { key: it.id },
          h('div', { className: cx('alt-ux-entrega-progresso__row', sel === it.id && 'is-sel'), role: 'button', tabIndex: 0, 'aria-pressed': sel === it.id,
            onClick: function () { pick(it); }, onKeyDown: function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); pick(it); } } },
            h('div', { className: 'alt-ux-entrega-progresso__who' },
              h('div', { className: 'alt-ux-entrega-progresso__chips' },
                h('span', { className: 'alt-chip', style: { background: t.bg, color: S.tone === 'neutral' ? 'var(--foreground)' : t.c } }, h(A.Icon, { name: S.icon, size: 13 }), it.statusText || S.label),
                h('span', { className: 'alt-chip alt-ux-entrega-progresso__veh' }, it.vehicle)),
              h('b', { className: 'alt-num' }, it.plate),
              h('small', null, it.driver + ' · ' + it.from + ' → ' + it.to)),
            h(Track, { steps: steps, step: it.step, times: it.times }),
            h(Eta, it.eta)));
      })));
  }
  UxEntregaProgresso.STEPS = STEPS;
  A.UxEntregaProgresso = UxEntregaProgresso;
})();


/* ---- UxFiltroPagador ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var DEF_PAGADORES = [
    { id: 40211, nome: 'Vidroporto S.A.', descricao: 'Porto Ferreira', uf: 'SP' },
    { id: 40388, nome: 'Vidroporto Recife', descricao: 'Recife', uf: 'PE' },
    { id: 51007, nome: 'Cebrace', descricao: 'Jacareí', uf: 'SP' },
    { id: 51210, nome: 'Cebrace Barra Velha', descricao: 'Barra Velha', uf: 'SC' }
  ];
  var DEF_OPTS = {
    status: ['Em trânsito', 'Parado', 'Finalizada', 'Sem sinal'],
    placas: ['ABC-1D23', 'KLM-7G89', 'QRT-4F56', 'RST-2B34', 'MNO-5C67'],
    motoristas: ['João Pereira', 'Rafael Nunes', 'Marta Souza', 'Luiz Andrade', 'Sérgio Dias'],
    remetentes: ['Paulínia', 'Jacareí', 'Jundiaí'],
    destinatarios: ['Porto Ferreira', 'Santos', 'Recife', 'Campinas', 'São José dos Campos']
  };
  var FIELDS = [['placas', 'Placas'], ['motoristas', 'Motoristas'], ['remetentes', 'Remetentes'], ['destinatarios', 'Destinatários']];
  var EMPTY = { status: [], placas: [], motoristas: [], remetentes: [], destinatarios: [] };
  var DEF_VALUE = { pagador: 40211, inicio: '01/09/2026', fim: '30/09/2026', status: ['Em trânsito'], placas: [], motoristas: ['João Pereira'], remetentes: ['Paulínia'], destinatarios: [] };

  function countActive(v) { return ['status', 'placas', 'motoristas', 'remetentes', 'destinatarios'].filter(function (k) { return v[k] && v[k].length; }).length; }

  function Multi(p) {
    var sQ = R.useState(''), q = sQ[0], sO = R.useState(false), open = sO[0];
    var opts = p.options.filter(function (o) { return p.value.indexOf(o) < 0 && o.toLowerCase().indexOf(q.trim().toLowerCase()) >= 0; });
    function add(o) { p.onChange(p.value.concat([o])); sQ[1](''); }
    return h('div', { className: 'alt-ux-filtro-pagador__multi' },
      h('div', { className: 'alt-ctl alt-ctl--sm alt-ux-filtro-pagador__mctl', onClick: function (e) { var i = e.currentTarget.querySelector('input'); if (i) i.focus(); } },
        p.value.map(function (v) {
          return h('span', { key: v, className: 'alt-chip alt-ux-filtro-pagador__tag' }, v,
            h('button', { type: 'button', 'aria-label': 'Remover ' + v, onClick: function (e) { e.stopPropagation(); p.onChange(p.value.filter(function (x) { return x !== v; })); } }, h(A.Icon, { name: 'x', size: 11 })));
        }),
        h('input', { id: p.id, value: q, placeholder: p.value.length ? '' : 'Todos', 'aria-autocomplete': 'list', 'aria-expanded': open,
          onFocus: function () { sO[1](true); }, onBlur: function () { setTimeout(function () { sO[1](false); }, 120); },
          onChange: function (e) { sQ[1](e.target.value); sO[1](true); },
          onKeyDown: function (e) {
            if (e.key === 'Enter' && opts[0]) { e.preventDefault(); add(opts[0]); }
            if (e.key === 'Escape') sO[1](false);
            if (e.key === 'Backspace' && !q && p.value.length) p.onChange(p.value.slice(0, -1));
          } })),
      open && opts.length ? h('div', { className: 'alt-popover alt-ux-filtro-pagador__menu', role: 'listbox' }, opts.slice(0, 6).map(function (o) {
        return h('button', { key: o, type: 'button', role: 'option', className: 'alt-menu-item', onMouseDown: function (e) { e.preventDefault(); add(o); } }, o);
      })) : null);
  }

  function UxFiltroPagador(p) {
    var pagadores = p.pagadores || DEF_PAGADORES, opts = Object.assign({}, DEF_OPTS, p.options || {});
    var ctl = p.value !== undefined, sV = R.useState(Object.assign({}, DEF_VALUE, p.defaultValue || {})), v = ctl ? p.value : sV[0];
    var sD = R.useState(v), draft = sD[0], setDraft = sD[1];
    var sOpen = R.useState(!!p.defaultOpen), open = sOpen[0];
    var sView = R.useState(p.defaultView || 'entregas'), view = p.view !== undefined ? p.view : sView[0];
    var sQ = R.useState(''), q = sQ[0];
    var pag = pagadores.filter(function (x) { return x.id === v.pagador; })[0] || pagadores[0];
    var n = countActive(v);

    function openDrawer() { setDraft(v); sOpen[1](true); }
    function close() { sOpen[1](false); if (p.onClose) p.onClose(); }
    function apply() { if (!ctl) sV[1](draft); if (p.onApply) p.onApply(draft); if (!p.inlineDrawer) close(); }
    function setD(k, val) { var nd = Object.assign({}, draft); nd[k] = val; setDraft(nd); }
    function setView(x) { sView[1](x); if (p.onViewChange) p.onViewChange(x); }
    function fullscreen() {
      if (p.onFullscreen) return p.onFullscreen();
      var d = document; if (d.fullscreenElement) { d.exitFullscreen && d.exitFullscreen(); } else if (d.documentElement.requestFullscreen) d.documentElement.requestFullscreen().catch(function () {});
    }
    R.useEffect(function () {
      if (!open || p.inlineDrawer) return;
      function k(e) { if (e.key === 'Escape') close(); }
      document.addEventListener('keydown', k); return function () { document.removeEventListener('keydown', k); };
    }, [open]);

    var ql = q.trim().toLowerCase();
    var drawer = open ? h('aside', { className: cx('alt-ux-filtro-pagador__drawer', p.inlineDrawer && 'is-inline'), role: 'dialog', 'aria-modal': p.inlineDrawer ? undefined : 'true', 'aria-label': 'Filtros do painel' },
      h('header', { className: 'alt-ux-filtro-pagador__dh' }, h('h2', null, 'Filtros'),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Fechar filtros', onClick: close }, h(A.Icon, { name: 'x', size: 16 }))),
      h('div', { className: 'alt-ux-filtro-pagador__db alt-scroll' },
        h('div', { className: 'alt-field' }, h('span', { className: 'alt-field__label', id: 'uxfp-pag' }, 'Pagador'),
          h('label', { className: 'alt-ctl alt-ctl--sm' }, h(A.Icon, { name: 'search', size: 14 }), h('input', { placeholder: 'Buscar pagador ou código', 'aria-label': 'Buscar pagador', value: q, onChange: function (e) { sQ[1](e.target.value); } })),
          h('div', { className: 'alt-ux-filtro-pagador__pags', role: 'radiogroup', 'aria-labelledby': 'uxfp-pag' },
            pagadores.filter(function (x) { return !ql || (x.nome + ' ' + x.id + ' ' + x.descricao).toLowerCase().indexOf(ql) >= 0; }).map(function (x) {
              var on = draft.pagador === x.id;
              return h('button', { key: x.id, type: 'button', role: 'radio', 'aria-checked': on, className: 'alt-ux-filtro-pagador__pag alt-num', onClick: function () { setD('pagador', x.id); } },
                h('span', { className: 'alt-ux-filtro-pagador__radio' }),
                h('span', { className: 'alt-ux-filtro-pagador__pag-tx' }, h('b', null, x.nome), h('small', null, 'Cód. cliente ' + x.id + ' · ' + x.descricao + ' - ' + x.uf)));
            }))),
        h('div', { className: 'alt-ux-filtro-pagador__two alt-num' },
          h('label', { className: 'alt-field' }, h('span', { className: 'alt-field__label' }, 'Início'), h('span', { className: 'alt-ctl alt-ctl--sm' }, h(A.Icon, { name: 'calendar', size: 14 }), h('input', { value: draft.inicio, onChange: function (e) { setD('inicio', e.target.value); } }))),
          h('label', { className: 'alt-field' }, h('span', { className: 'alt-field__label' }, 'Fim'), h('span', { className: 'alt-ctl alt-ctl--sm' }, h(A.Icon, { name: 'calendar', size: 14 }), h('input', { value: draft.fim, onChange: function (e) { setD('fim', e.target.value); } })))),
        h('div', { className: 'alt-field' }, h('span', { className: 'alt-field__label', id: 'uxfp-st' }, 'Status'),
          h('div', { className: 'alt-ux-filtro-pagador__chips', role: 'group', 'aria-labelledby': 'uxfp-st' }, opts.status.map(function (s) {
            var on = draft.status.indexOf(s) >= 0;
            return h('button', { key: s, type: 'button', 'aria-pressed': on, className: 'alt-chip alt-ux-filtro-pagador__fchip',
              onClick: function () { setD('status', on ? draft.status.filter(function (x) { return x !== s; }) : draft.status.concat([s])); } }, on ? h(A.Icon, { name: 'check', size: 13 }) : null, s);
          }))),
        FIELDS.map(function (f) {
          return h('div', { key: f[0], className: 'alt-field' }, h('label', { className: 'alt-field__label', htmlFor: 'uxfp-' + f[0] }, f[1]),
            h(Multi, { id: 'uxfp-' + f[0], options: opts[f[0]], value: draft[f[0]] || [], onChange: function (x) { setD(f[0], x); } }));
        })),
      h('footer', { className: 'alt-ux-filtro-pagador__df' },
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { setDraft(Object.assign({}, draft, EMPTY)); } }, 'Limpar'),
        h('button', { type: 'button', className: 'alt-btn alt-btn--primary alt-ux-filtro-pagador__primary', onClick: apply }, 'Aplicar filtros'))) : null;

    return h('div', { className: cx('alt-ux-filtro-pagador', p.className), style: p.style },
      h('div', { className: 'alt-ux-filtro-pagador__bar' },
        h('button', { type: 'button', className: 'alt-ux-filtro-pagador__tg alt-num', 'aria-haspopup': 'dialog', 'aria-expanded': open, onClick: function () { open ? close() : openDrawer(); },
          'aria-label': 'Filtros: ' + pag.nome + ', ' + v.inicio + ' a ' + v.fim + (n ? ', ' + n + ' filtros ativos' : '') },
          h('span', { className: 'alt-ux-filtro-pagador__tg-ico' }, h(A.Icon, { name: 'filter', size: 16 })),
          h('span', { className: 'alt-ux-filtro-pagador__tg-tx' }, h('b', null, pag.nome + ' · ' + pag.uf), h('small', null, 'Cód. cliente ' + pag.id + ' · ' + v.inicio + ' – ' + v.fim)),
          h(A.Icon, { name: 'chevron-down', size: 16, className: 'alt-ux-filtro-pagador__chev' }),
          n ? h('span', { className: 'alt-count alt-ux-filtro-pagador__n', 'aria-hidden': 'true' }, n) : null),
        h('span', { style: { flex: 1 } }),
        h('div', { className: 'alt-ux-filtro-pagador__seg', role: 'group', 'aria-label': 'Visão' }, [['entregas', 'Entregas', 'truck'], ['indicadores', 'Indicadores', 'chart-bar']].map(function (o) {
          return h('button', { key: o[0], type: 'button', 'aria-pressed': view === o[0], onClick: function () { setView(o[0]); } }, h(A.Icon, { name: o[2], size: 14 }), o[1]);
        })),
        p.showFullscreen === false ? null : h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon', 'aria-label': 'Tela cheia', title: 'Tela cheia', onClick: fullscreen },
          h('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
            h('path', { d: 'M8 3H5a2 2 0 0 0-2 2v3M21 8V5a2 2 0 0 0-2-2h-3M3 16v3a2 2 0 0 0 2 2h3M16 21h3a2 2 0 0 0 2-2v-3' })))),
      open && !p.inlineDrawer ? h('div', { className: 'alt-ux-filtro-pagador__scrim', onClick: close }) : null,
      drawer);
  }
  A.UxFiltroPagador = UxFiltroPagador;
})();


/* ---- UxWidgetsCliente ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  /* Séries: tokens de gráfico no lugar das cores ESG próprias (#5B2D2C, #166769, #224210). */
  var DEF_WIDGETS = [
    { id: 'distancia', label: 'Distância total percorrida', value: '48.210', unit: 'km', caption: '1.284 entregas no período', color: 'var(--chart-2)', icon: 'route', featured: true },
    { id: 'remetente', label: 'Tempo médio no remetente', value: '1h12', caption: 'carregamento', color: 'var(--chart-2)', icon: 'clock' },
    { id: 'destinatario', label: 'Tempo médio no destinatário', value: '0h58', caption: 'descarga', color: 'var(--chart-1)', icon: 'clock' },
    { id: 'co2', label: 'Gás carbônico', value: '312,4', unit: 't', caption: 'CO₂ emitido', color: 'var(--chart-1)', esg: true },
    { id: 'ch4', label: 'Metano', value: '1,82', unit: 't', caption: 'CH₄ emitido', color: 'var(--chart-5)', esg: true },
    { id: 'n2o', label: 'Óxido nitroso', value: '0,41', unit: 't', caption: 'N₂O emitido', color: 'var(--chart-3)', esg: true },
    { id: 'entregas', label: 'Entregas no prazo', value: '94', unit: '%', caption: '1.207 de 1.284', color: 'var(--chart-4)', icon: 'check-circle' }
  ];
  var DEF_LAYOUT = [{ id: 'distancia', span: 2 }, { id: 'remetente', span: 1 }, { id: 'destinatario', span: 1 }, { id: 'co2', span: 1 }, { id: 'ch4', span: 1 }, { id: 'n2o', span: 1 }, { id: 'entregas', span: 1 }];

  function UxWidgetsCliente(p) {
    var widgets = p.widgets || DEF_WIDGETS, byId = {};
    widgets.forEach(function (w) { byId[w.id] = w; });
    var initial = p.defaultLayout || DEF_LAYOUT;
    var ctl = p.layout !== undefined, sL = R.useState(initial), layout = ctl ? p.layout : sL[0];
    var sE = R.useState(!!p.defaultEditing), editing = sE[0];
    var sAdd = R.useState(false), addOpen = sAdd[0];
    var sDrag = R.useState(null), dragId = sDrag[0];
    var cols = p.columns || 4;

    function commit(nl) { if (!ctl) sL[1](nl); if (p.onLayoutChange) p.onLayoutChange(nl); }
    function move(i, d) { var j = i + d; if (j < 0 || j >= layout.length) return; var nl = layout.slice(); var t = nl[i]; nl[i] = nl[j]; nl[j] = t; commit(nl); }
    function resize(i) { var nl = layout.slice(); nl[i] = Object.assign({}, nl[i], { span: nl[i].span === 2 ? 1 : 2 }); commit(nl); }
    function remove(i) { commit(layout.filter(function (_, j) { return j !== i; })); }
    function add(id) { commit(layout.concat([{ id: id, span: 1 }])); sAdd[1](false); }
    function dropOn(targetId) {
      if (!dragId || dragId === targetId) return;
      var from = layout.findIndex(function (l) { return l.id === dragId; }), to = layout.findIndex(function (l) { return l.id === targetId; });
      var nl = layout.slice(), it = nl.splice(from, 1)[0]; nl.splice(to, 0, it); commit(nl); sDrag[1](null);
    }
    var hidden = widgets.filter(function (w) { return !layout.some(function (l) { return l.id === w.id; }); });

    return h('section', { className: cx('alt-ux-widgets-cliente', editing && 'is-editing', p.className), style: p.style, 'aria-label': p.title || 'Indicadores' },
      h('header', { className: 'alt-ux-widgets-cliente__hd' },
        h('div', { className: 'alt-ux-widgets-cliente__ht' }, h('h2', null, p.title || 'Indicadores'), h('span', { className: 'alt-num' }, p.subtitle || 'Vidroporto S.A. · 01/09/2026 – 30/09/2026')),
        editing ? h('span', { className: 'alt-ux-widgets-cliente__hint' }, h(A.Icon, { name: 'drag', size: 14 }), 'Arraste ou use as setas para reorganizar') : null,
        editing ? h('div', { className: 'alt-ux-widgets-cliente__anchor' },
          h('button', { type: 'button', className: 'alt-tbtn', disabled: !hidden.length, 'aria-expanded': addOpen, onClick: function () { sAdd[1](!addOpen); } }, h(A.Icon, { name: 'plus', size: 15 }), 'Adicionar widget'),
          addOpen && hidden.length ? h('div', { className: 'alt-popover alt-ux-widgets-cliente__menu', role: 'menu' }, hidden.map(function (w) {
            return h('button', { key: w.id, type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: function () { add(w.id); } }, h('span', { className: 'alt-dot', style: { color: w.color } }), w.label);
          })) : null) : null,
        editing ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', onClick: function () { commit(DEF_LAYOUT.filter(function (l) { return byId[l.id]; })); } }, 'Restaurar padrão') : null,
        editing
          ? h('button', { type: 'button', className: 'alt-btn alt-btn--primary alt-ux-widgets-cliente__primary', onClick: function () { sE[1](false); sAdd[1](false); if (p.onSave) p.onSave(layout); } }, h(A.Icon, { name: 'check', size: 15 }), 'Salvar layout')
          : h('button', { type: 'button', className: 'alt-tbtn', onClick: function () { sE[1](true); } }, h(A.Icon, { name: 'grid', size: 15 }), 'Editar layout')),
      h('div', { className: 'alt-ux-widgets-cliente__grid alt-num', style: { gridTemplateColumns: 'repeat(' + cols + ', minmax(0,1fr))' } },
        layout.map(function (l, i) {
          var w = byId[l.id]; if (!w) return null;
          return h('article', {
            key: l.id, className: cx('alt-glass', 'alt-ux-widgets-cliente__w', w.featured && 'is-featured', dragId === l.id && 'is-drag'),
            style: { gridColumn: 'span ' + Math.min(l.span || 1, cols) },
            draggable: editing, onDragStart: function (e) { sDrag[1](l.id); e.dataTransfer.effectAllowed = 'move'; try { e.dataTransfer.setData('text/plain', l.id); } catch (x) {} },
            onDragEnd: function () { sDrag[1](null); }, onDragOver: function (e) { if (editing) e.preventDefault(); }, onDrop: function (e) { e.preventDefault(); dropOn(l.id); },
            'aria-label': w.label + ': ' + w.value + (w.unit ? ' ' + w.unit : '')
          },
            h('div', { className: 'alt-ux-widgets-cliente__top' },
              h('span', { className: 'alt-ux-widgets-cliente__lbl' }, h('span', { className: 'alt-dot', style: { color: w.color }, 'aria-hidden': 'true' }), w.label),
              w.esg && !editing ? h('span', { className: 'alt-ux-widgets-cliente__tag' }, 'ESG') : null),
            h('div', { className: 'alt-ux-widgets-cliente__val' }, h('b', null, w.value), w.unit ? h('span', null, w.unit) : null),
            w.caption ? h('div', { className: 'alt-ux-widgets-cliente__cap' }, w.caption) : null,
            w.featured ? h('svg', { className: 'alt-ux-widgets-cliente__art', viewBox: '0 0 240 140', preserveAspectRatio: 'xMaxYMid slice', 'aria-hidden': 'true' },
              h('path', { d: 'M40 150 C 110 110, 150 70, 250 46', fill: 'none', stroke: w.color, strokeOpacity: 0.22, strokeWidth: 30, strokeLinecap: 'round' }),
              h('path', { d: 'M40 150 C 110 110, 150 70, 250 46', fill: 'none', stroke: 'var(--foreground)', strokeOpacity: 0.35, strokeWidth: 1.5, strokeDasharray: '6 8' })) : null,
            editing ? h('div', { className: 'alt-ux-widgets-cliente__tools' },
              h('span', { className: 'alt-ux-widgets-cliente__grip', title: 'Arrastar' }, h(A.Icon, { name: 'drag', size: 14 })),
              h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Mover para trás', disabled: i === 0, onClick: function () { move(i, -1); } }, h(A.Icon, { name: 'chevron-left', size: 14 })),
              h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Mover para frente', disabled: i === layout.length - 1, onClick: function () { move(i, 1); } }, h(A.Icon, { name: 'chevron-right', size: 14 })),
              h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost alt-ux-widgets-cliente__span', 'aria-label': l.span === 2 ? 'Estreitar' : 'Alargar', onClick: function () { resize(i); } }, l.span === 2 ? '2×' : '1×'),
              h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Remover ' + w.label, onClick: function () { remove(i); } }, h(A.Icon, { name: 'x', size: 14 }))) : null);
        })));
  }
  A.UxWidgetsCliente = UxWidgetsCliente;
})();


/* ---- ViolCampoDinamico ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var PARAMETROS = [
    { nome: 'velocidade_maxima', label: 'Velocidade máxima (km/h)', tipo: 'numero', obrigatorio: true,
      ajuda: { oQueE: 'Limite que dispara a violação.', paraQueServe: 'Comparar com a velocidade da telemetria.', exemplo: '80' } },
    { nome: 'vigente_desde', label: 'Vigente a partir de', tipo: 'data',
      ajuda: { oQueE: 'Data em que a regra passa a valer.', paraQueServe: 'Agendar a entrada em vigor sem publicar de novo.', exemplo: '01/10/2026' } },
    { nome: 'exige_foto', label: 'Exige foto na tratativa', tipo: 'sim_nao',
      ajuda: { oQueE: 'Pede uma foto ao operador ao tratar a violação.', paraQueServe: 'Comprovar a situação no local da parada.', exemplo: 'Sim' } },
    { nome: 'tolerancia', label: 'Tolerância', tipo: 'texto', opcoes: [
      { value: '0', label: 'Sem tolerância' }, { value: '30s', label: '30 segundos' }, { value: '2min', label: '2 minutos' }],
      ajuda: { oQueE: 'Tempo acima do limite antes de gerar a violação.', paraQueServe: 'Evitar ocorrências por picos curtos.', exemplo: '30 segundos' } }
  ];
  var VALORES = { velocidade_maxima: 80, vigente_desde: '2026-10-01', exige_foto: true, tolerancia: '30s' };
  var TRATATIVAS = [
    { nome: 'motivo_parada', label: 'Motivo da parada', tipo: 'lista', obrigatorio: true },
    { nome: 'observacao', label: 'Observação do operador', tipo: 'texto', obrigatorio: false }
  ];
  var TIPO_LABEL = { lista: 'Lista', texto: 'Texto', numero: 'Número', data: 'Data', sim_nao: 'Sim/Não', bool: 'Sim/Não' };

  function kind(par) {
    var t = String(par.tipo || '').toLowerCase();
    if (par.opcoes && par.opcoes.length) return 'select';
    if (/date|data/.test(t)) return 'date';
    if (/bool|sim_nao/.test(t)) return 'switch';
    if (/number|numeric|integer|decimal|numero/.test(t)) return 'number';
    return 'text';
  }

  /* Tooltip "O que é / Para que serve / Exemplo", abre no hover ou foco do ícone. */
  function FieldTooltip(p) {
    var s = R.useState(!!p.defaultOpen), open = s[0], set = s[1];
    var id = 'alt-vcd-tip-' + p.id;
    var aj = p.ajuda || {};
    return h('span', { className: 'alt-viol-campo__tipwrap', onMouseEnter: function () { set(true); }, onMouseLeave: function () { if (!p.defaultOpen) set(false); } },
      h('button', { type: 'button', className: 'alt-viol-campo__tipbtn', 'aria-label': 'Ajuda: ' + p.label, 'aria-describedby': open ? id : undefined, 'aria-expanded': open,
        onFocus: function () { set(true); }, onBlur: function () { set(false); }, onClick: function () { set(!open); },
        onKeyDown: function (e) { if (e.key === 'Escape') set(false); } },
        A.Icon({ name: 'info', size: 14 })),
      open ? h('span', { role: 'tooltip', id: id, className: 'alt-popover alt-viol-campo__tip' },
        aj.oQueE ? h('span', null, h('b', null, 'O que é'), aj.oQueE) : null,
        aj.paraQueServe ? h('span', null, h('b', null, 'Para que serve'), aj.paraQueServe) : null,
        aj.exemplo ? h('span', null, h('b', null, 'Exemplo'), h('span', { className: 'alt-num' }, aj.exemplo)) : null) : null);
  }

  /* Um campo gerado pelo contrato da regra. */
  function DynamicField(p) {
    var par = p.parametro, k = kind(par), id = 'alt-vcd-' + par.nome;
    var req = !!par.obrigatorio, v = p.value;
    var err = p.error;
    function set(x) { if (p.onChange) p.onChange(par.nome, x); }
    var labelEl = h('span', { className: 'alt-viol-campo__label' },
      h('label', { htmlFor: id, className: 'alt-field__label' }, par.label, req ? h('span', { className: 'alt-viol-campo__req', 'aria-hidden': 'true' }, ' *') : null),
      par.ajuda ? h(FieldTooltip, { id: par.nome, label: par.label, ajuda: par.ajuda, defaultOpen: p.ajudaAberta }) : null);
    var control;
    if (k === 'switch') {
      control = h('div', { className: 'alt-viol-campo__switchrow' },
        h('button', { type: 'button', id: id, role: 'switch', 'aria-checked': !!v, disabled: p.disabled, className: cx('alt-viol-campo__switch', v && 'is-on'), onClick: function () { set(!v); } },
          h('span', { className: 'alt-viol-campo__knob' })),
        h('span', { className: 'alt-viol-campo__switchtxt' }, v ? (par.textoSim || 'Sim') : (par.textoNao || 'Não')));
    } else if (k === 'select') {
      control = h('div', { className: cx('alt-ctl alt-viol-campo__ctl', err && 'alt-ctl--error', p.disabled && 'alt-ctl--disabled') },
        h('select', { id: id, value: v == null ? '' : v, required: req, disabled: p.disabled, onChange: function (e) { set(e.target.value); } },
          h('option', { value: '', disabled: req }, par.placeholder || 'Selecione'),
          par.opcoes.map(function (o) { var ov = typeof o === 'object' ? o.value : o; return h('option', { key: ov, value: ov }, typeof o === 'object' ? o.label : o); })),
        A.Icon({ name: 'chevron-down', size: 16 }));
    } else {
      control = h('div', { className: cx('alt-ctl alt-viol-campo__ctl', err && 'alt-ctl--error', p.disabled && 'alt-ctl--disabled') },
        h('input', { id: id, type: k === 'date' ? 'date' : k === 'number' ? 'number' : 'text', inputMode: k === 'number' ? 'decimal' : undefined,
          className: k === 'number' || k === 'date' ? 'alt-num' : undefined, value: v == null ? '' : v, required: req, disabled: p.disabled, placeholder: par.placeholder,
          'aria-invalid': err ? true : undefined,
          onChange: function (e) { set(k === 'number' ? (e.target.value === '' ? '' : Number(e.target.value)) : e.target.value); } }),
        k === 'date' ? A.Icon({ name: 'calendar', size: 16 }) : null);
    }
    return h('div', { className: cx('alt-field alt-viol-campo__field', p.className) }, labelEl, control,
      err ? h('span', { className: 'alt-field__error' }, A.Icon({ name: 'alert', size: 14 }), err)
        : par.ajudaCurta ? h('span', { className: 'alt-field__help' }, par.ajudaCurta) : null);
  }

  /* Cartão de configuração de um campo de tratativa. */
  function TratativaConfigField(p) {
    var c = p.campo, req = !!c.obrigatorio;
    var t = K.tone(req ? 'warning' : 'neutral');
    var id = 'alt-vcd-tr-' + c.nome;
    return h('div', { className: 'alt-viol-campo__card' },
      h('div', { className: 'alt-viol-campo__cardhd' },
        h('span', { className: 'alt-viol-campo__cardtt' }, c.label),
        h('span', { className: 'alt-chip', style: { background: t.bg, color: t.c } }, req ? A.Icon({ name: 'alert', size: 12 }) : null, req ? 'Obrigatório' : 'Opcional'),
        h('span', { className: 'alt-chip alt-viol-campo__chipo' }, TIPO_LABEL[c.tipo] || c.tipo)),
      h('div', { className: 'alt-viol-campo__cardbd' },
        h('span', { className: 'alt-viol-campo__cardsub' }, c.descricao || 'Campo exibido no momento da tratativa.'),
        p.onPadraoChange || c.padrao !== undefined ? h('div', { className: 'alt-field' },
          h('label', { className: 'alt-field__label', htmlFor: id }, 'Valor padrão'),
          h('div', { className: 'alt-ctl alt-ctl--sm' }, h('input', { id: id, value: c.padrao || '', placeholder: 'Sem valor padrão', onChange: function (e) { if (p.onPadraoChange) p.onPadraoChange(c.nome, e.target.value); } })),
          h('span', { className: 'alt-field__help' }, 'Deixe vazio para o operador preencher durante a resolução da violação.')) : null));
  }

  function ViolCampoDinamico(p) {
    var vs = R.useState(p.defaultValues || (p.parametro ? {} : VALORES));
    var values = p.values || vs[0];
    function change(nome, v) {
      if (!p.values) vs[1](function (o) { var n = Object.assign({}, o); n[nome] = v; return n; });
      if (p.onChange) p.onChange(nome, v);
    }
    if (p.parametro) {
      return h(DynamicField, { parametro: p.parametro, value: p.value !== undefined ? p.value : values[p.parametro.nome], onChange: change, error: p.error, disabled: p.disabled, ajudaAberta: p.ajudaAberta === p.parametro.nome || p.ajudaAberta === true, className: cx('alt-viol-campo', p.className) });
    }
    var pars = p.parametros || PARAMETROS;
    var trs = p.tratativas === undefined ? TRATATIVAS : p.tratativas;
    var ts = R.useState(function () { var o = {}; (trs || []).forEach(function (c) { o[c.nome] = c.padrao || ''; }); return o; });
    var errors = p.errors || {};
    return h('div', { className: cx('alt-viol-campo', p.layout === 'side' && 'alt-viol-campo--side', p.className), style: p.style },
      h('div', { className: 'alt-viol-campo__grid' },
        pars.map(function (par) {
          return h(DynamicField, { key: par.nome, parametro: par, value: values[par.nome], onChange: change, error: errors[par.nome], disabled: p.disabled, ajudaAberta: p.ajudaAberta === par.nome });
        })),
      trs && trs.length ? h('div', { className: 'alt-viol-campo__tratativas' },
        h('div', { className: 'alt-field__label' }, p.tituloTratativa || 'Campos da tratativa'),
        h('div', { className: 'alt-viol-campo__cards' }, trs.map(function (c) {
          return h(TratativaConfigField, { key: c.nome, campo: Object.assign({}, c, { padrao: ts[0][c.nome] }), onPadraoChange: function (n, v) {
            ts[1](function (o) { var x = Object.assign({}, o); x[n] = v; return x; });
            if (p.onTratativaChange) p.onTratativaChange(n, v);
          } });
        }))) : null);
  }

  ViolCampoDinamico.Field = DynamicField;
  ViolCampoDinamico.Tratativa = TratativaConfigField;
  ViolCampoDinamico.Tooltip = FieldTooltip;
  ViolCampoDinamico.controlKind = kind;
  A.ViolCampoDinamico = ViolCampoDinamico;
})();


/* ---- ViolCatalogoWizard ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var PASSOS = ['O que monitorar', 'Situações', 'Revisar'];
  var SUGESTOES = ['PARADA_EXCESSIVA', 'IGNICAO_OFF', 'PORTA_ABERTA_FORA_PONTO'];
  var INICIAL = {
    nome: 'Paradas',
    descricao: 'Paradas do veículo fora dos pontos previstos na rota.',
    fonte: 'Telemetria embarcada',
    ocorrencias: ['Parada não autorizada', 'Parada prolongada']
  };
  var FONTES = ['Telemetria embarcada', 'Rastreador satelital', 'App do motorista', 'Sensor de temperatura'];

  function humanize(code) {
    var s = String(code).toLowerCase().replace(/_/g, ' ');
    return s.charAt(0).toUpperCase() + s.slice(1);
  }

  function SourcesIcon() {
    return h('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      h('path', { d: 'M12 12h.01M8.5 8.5a5 5 0 0 0 0 7M15.5 8.5a5 5 0 0 1 0 7M5.6 5.6a9 9 0 0 0 0 12.8M18.4 5.6a9 9 0 0 1 0 12.8' }));
  }
  function BulbIcon() {
    return h('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      h('path', { d: 'M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.9c.6.5 1 1.2 1 2.1h5c0-.9.4-1.6 1-2.1A6 6 0 0 0 12 3z' }));
  }

  function ViolCatalogoWizard(p) {
    var passos = p.passos || PASSOS;
    var stS = R.useState(p.passoInicial || 0), passo = stS[0], setPasso = stS[1];
    var dS = R.useState(function () { var d = Object.assign({}, p.valorInicial || INICIAL); d.ocorrencias = (d.ocorrencias || []).slice(); return d; });
    var d = dS[0];
    var nS = R.useState(p.novaInicial || ''), nova = nS[0], setNova = nS[1];
    var errS = R.useState(null), err = errS[0], setErr = errS[1];
    var prS = R.useState(p.progresso == null ? null : p.progresso);
    var progresso = p.progresso !== undefined ? p.progresso : prS[0];
    var sugestoes = (p.sugestoes || SUGESTOES).filter(function (c) { return d.ocorrencias.indexOf(humanize(c)) < 0; });
    var fontes = p.fontes || FONTES;
    var editando = !!p.editando;
    var saving = progresso != null;

    function upd(k, v) { dS[1](function (o) { var n = Object.assign({}, o); n[k] = v; if (p.onChange) p.onChange(n); return n; }); }
    function addOc(nome) {
      nome = String(nome || '').trim();
      if (!nome) return;
      if (d.ocorrencias.some(function (x) { return x.toLowerCase() === nome.toLowerCase(); })) { setErr('Já existe uma ocorrência com esse nome.'); return; }
      setErr(null);
      upd('ocorrencias', d.ocorrencias.concat([nome]));
    }
    function rmOc(i) { upd('ocorrencias', d.ocorrencias.filter(function (_, j) { return j !== i; })); }
    function valido(i) {
      if (i === 0) return d.nome.trim().length > 0;
      if (i === 1) return d.ocorrencias.length > 0;
      return true;
    }
    function avancar() {
      if (!valido(passo)) { setErr(passo === 0 ? 'Informe o nome da categoria.' : 'Adicione pelo menos uma ocorrência.'); return; }
      setErr(null);
      if (passo < passos.length - 1) { setPasso(passo + 1); return; }
      if (p.onSubmit) { p.onSubmit(d); return; }
      /* sem onSubmit: simula o salvamento para demonstração */
      var v = 0; prS[1](0);
      var t = setInterval(function () { v += 20; prS[1](Math.min(v, 100)); if (v >= 100) { clearInterval(t); setTimeout(function () { prS[1](null); if (p.onDone) p.onDone(d); }, 600); } }, 250);
    }
    var cta = passo < passos.length - 1 ? 'Avançar' : (editando ? 'Salvar categoria' : 'Criar categoria e ' + d.ocorrencias.length + (d.ocorrencias.length === 1 ? ' ocorrência' : ' ocorrências'));

    var corpo, lado;
    if (passo === 0) {
      corpo = h('div', { className: 'alt-viol-wiz__form' },
        h('div', { className: 'alt-field' },
          h('label', { className: 'alt-field__label', htmlFor: 'alt-vwz-nome' }, 'Nome da categoria *'),
          h('div', { className: cx('alt-ctl', err && !d.nome.trim() && 'alt-ctl--error') }, h('input', { id: 'alt-vwz-nome', value: d.nome, placeholder: 'Ex.: Paradas', onChange: function (e) { upd('nome', e.target.value); } }))),
        h('div', { className: 'alt-field' },
          h('label', { className: 'alt-field__label', htmlFor: 'alt-vwz-desc' }, 'Descrição'),
          h('div', { className: 'alt-ctl alt-viol-wiz__area' }, h('textarea', { id: 'alt-vwz-desc', rows: 2, value: d.descricao, onChange: function (e) { upd('descricao', e.target.value); } }))),
        h('div', { className: 'alt-field' },
          h('label', { className: 'alt-field__label', htmlFor: 'alt-vwz-fonte' }, 'Fonte de eventos'),
          h('div', { className: 'alt-ctl alt-viol-wiz__select' },
            h('select', { id: 'alt-vwz-fonte', value: d.fonte, onChange: function (e) { upd('fonte', e.target.value); } }, fontes.map(function (f) { return h('option', { key: f, value: f }, f); })),
            A.Icon({ name: 'chevron-down', size: 16 }))));
      lado = h('aside', { className: 'alt-viol-wiz__help' },
        h('b', null, BulbIcon(), 'Como funciona'),
        h('span', null, 'Uma ', h('strong', null, 'categoria'), ' agrupa o que você quer monitorar. Cada ', h('strong', null, 'ocorrência'), ' é uma situação dentro dela que pode gerar violação.'),
        h('span', null, 'Exemplo: categoria "Paradas" com as ocorrências "Parada não autorizada" e "Parada prolongada".'));
    } else if (passo === 1) {
      corpo = h('div', { className: 'alt-viol-wiz__form' },
        h('div', { className: 'alt-field' },
          h('label', { className: 'alt-field__label', htmlFor: 'alt-vwz-nova' }, 'Nova ocorrência'),
          h('div', { className: 'alt-viol-wiz__add' },
            h('div', { className: cx('alt-ctl', err && 'alt-ctl--error') }, h('input', { id: 'alt-vwz-nova', value: nova, placeholder: 'Ex.: Parada em área de risco', onChange: function (e) { setNova(e.target.value); setErr(null); },
              onKeyDown: function (e) { if (e.key === 'Enter') { e.preventDefault(); addOc(nova); if (nova.trim()) setNova(''); } } })),
            h('button', { type: 'button', className: 'alt-tbtn alt-viol-wiz__addbtn', disabled: !nova.trim(), onClick: function () { addOc(nova); setNova(''); } }, A.Icon({ name: 'plus', size: 16 }), 'Adicionar'))),
        h('ul', { className: 'alt-viol-wiz__list', 'aria-label': 'Ocorrências da categoria' },
          d.ocorrencias.length ? d.ocorrencias.map(function (o, i) {
            return h('li', { key: o, className: 'alt-viol-wiz__item' },
              h('span', { className: 'alt-viol-wiz__itemnum alt-num' }, String(i + 1).padStart(2, '0')),
              h('span', { className: 'alt-viol-wiz__itemtxt' }, o),
              h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost alt-viol-wiz__rm', 'aria-label': 'Remover ' + o, onClick: function () { rmOc(i); } }, A.Icon({ name: 'x', size: 16 })));
          }) : h('li', { className: 'alt-viol-wiz__empty' }, 'Nenhuma ocorrência ainda. Digite acima ou use uma sugestão das fontes.')));
      lado = h('aside', { className: 'alt-viol-wiz__help' },
        h('b', null, SourcesIcon(), 'Chegando das suas fontes'),
        h('span', null, 'Eventos recebidos que ainda não têm ocorrência.'),
        sugestoes.length ? h('div', { className: 'alt-viol-wiz__sugs' }, sugestoes.map(function (c) {
          return h('button', { key: c, type: 'button', className: 'alt-chip alt-viol-wiz__sug', title: 'Adicionar como "' + humanize(c) + '"', onClick: function () { addOc(humanize(c)); } }, A.Icon({ name: 'plus', size: 12 }), c);
        })) : h('span', { className: 'alt-viol-wiz__ok' }, A.Icon({ name: 'check-circle', size: 14 }), 'Todos os eventos já têm ocorrência.'));
    } else {
      corpo = h('div', { className: 'alt-viol-wiz__review' },
        h('dl', { className: 'alt-viol-wiz__dl' },
          h('div', null, h('dt', null, 'Categoria'), h('dd', null, d.nome)),
          h('div', null, h('dt', null, 'Fonte de eventos'), h('dd', null, d.fonte)),
          d.descricao ? h('div', { className: 'alt-viol-wiz__full' }, h('dt', null, 'Descrição'), h('dd', null, d.descricao)) : null),
        h('div', { className: 'alt-field__label' }, 'Ocorrências ', h('span', { className: 'alt-num' }, '(' + d.ocorrencias.length + ')')),
        h('ul', { className: 'alt-viol-wiz__chips' }, d.ocorrencias.map(function (o) { return h('li', { key: o, className: 'alt-chip alt-viol-wiz__oc' }, o); })));
      lado = h('aside', { className: 'alt-viol-wiz__help' },
        h('b', null, A.Icon({ name: 'info', size: 16 }), 'Depois de criar'),
        h('span', null, 'As ocorrências ficam disponíveis no passo Mapeamentos para receber eventos brutos e, em seguida, nas Regras.'));
    }

    return h('section', { className: cx('alt-glass alt-viol-wiz', p.className), style: p.style, 'aria-label': p.titulo || (editando ? 'Editar categoria' : 'Nova categoria') },
      h('header', { className: 'alt-viol-wiz__hd' },
        h('div', { className: 'alt-viol-wiz__titles' },
          h('span', { className: 'alt-viol-wiz__tt' }, (p.titulo || (editando ? 'Editar categoria' : 'Nova categoria')) + ' · ' + passos[passo]),
          h('span', { className: 'alt-viol-wiz__sub alt-num' }, 'Passo ' + (passo + 1) + ' de ' + passos.length)),
        p.onClose ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost', 'aria-label': 'Fechar', onClick: p.onClose }, A.Icon({ name: 'x', size: 18 })) : null),
      h('ol', { className: 'alt-viol-wiz__steps' }, passos.map(function (s, i) {
        var done = i < passo, on = i === passo;
        return h('li', { key: s, className: cx('alt-viol-wiz__step', done && 'is-done', on && 'is-on'), 'aria-current': on ? 'step' : undefined },
          h('button', { type: 'button', disabled: i > passo || saving, onClick: function () { setPasso(i); setErr(null); } },
            h('span', { className: 'alt-viol-wiz__dot alt-num', 'aria-hidden': 'true' }, done ? A.Icon({ name: 'check', size: 12, strokeWidth: 3 }) : i + 1),
            h('span', null, s, done ? h('span', { className: 'alt-viol-wiz__sr' }, ' (concluído)') : null)),
          i < passos.length - 1 ? h('span', { className: 'alt-viol-wiz__line', 'aria-hidden': 'true' }) : null);
      })),
      h('div', { className: 'alt-viol-wiz__bd' }, h('div', { className: 'alt-viol-wiz__main' }, corpo,
        err ? h('span', { className: 'alt-field__error', role: 'alert' }, A.Icon({ name: 'alert', size: 14 }), err) : null), lado),
      h('footer', { className: 'alt-viol-wiz__ft' },
        saving ? h('div', { className: 'alt-viol-wiz__prog', role: 'status' },
          h('span', { className: 'alt-viol-wiz__bar' }, h('span', { style: { width: progresso + '%' } })),
          h('span', { className: 'alt-num' }, progresso >= 100 ? 'Salvo' : 'Salvando… ' + progresso + '%')) : h('span', { className: 'alt-viol-wiz__spacer' }),
        h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--ghost', disabled: saving, onClick: function () { if (passo === 0) { if (p.onCancel) p.onCancel(); } else { setPasso(passo - 1); setErr(null); } } }, passo === 0 ? 'Cancelar' : 'Voltar'),
        h(A.Button, { size: 'sm', arrow: false, disabled: saving, onClick: avancar, className: 'alt-viol-wiz__cta' }, cta, passo < passos.length - 1 ? A.Icon({ name: 'arrow-right', size: 16, strokeWidth: 2.2 }) : null)));
  }

  A.ViolCatalogoWizard = ViolCatalogoWizard;
})();


/* ---- ViolEtapas ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var PRESETS = {
    motor: [
      { id: 'catalogo', label: 'Catálogo', description: 'Categorias e ocorrências', status: 'complete' },
      { id: 'mapeamentos', label: 'Mapeamentos', description: '12 brutos sem mapeamento', status: 'error' },
      { id: 'regras', label: 'Regras', description: 'O que dispara uma violação', status: 'pending' },
      { id: 'validacao', label: 'Validação', description: 'Simular com dados reais', status: 'pending' },
      { id: 'publicacao', label: 'Publicação', description: 'Aprovar e publicar versão', status: 'pending' }
    ],
    editor: [
      { id: 'classificacao', label: 'Classificação', description: 'Categoria, ocorrência e severidade', status: 'complete' },
      { id: 'disparo', label: 'Disparo', description: 'Condição e limites do contrato', status: 'complete' },
      { id: 'tratativa', label: 'Tratativa', description: 'Campos pedidos ao operador', status: 'error' },
      { id: 'escopo', label: 'Escopo', description: 'Clientes, rotas e veículos', status: 'pending' },
      { id: 'versao', label: 'Versão & Simulação', description: 'Vigência e simulação antes de publicar', status: 'pending' }
    ]
  };
  var SUMMARY = [
    { label: 'Eventos canônicos', value: 48 },
    { label: 'Brutos sem mapeamento', value: 12, tone: 'error' },
    { label: 'Regras publicadas', value: 9 },
    { label: 'A reprocessar', value: 'sob demanda', muted: true }
  ];
  var STATUS_TXT = { complete: 'concluída', error: 'com pendência', pending: 'pendente' };

  function Check() {
    return h('svg', { width: 12, height: 12, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 3, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true' }, h('path', { d: 'm5 12 5 5 9-10' }));
  }

  function Badge(st, n) {
    var s = st.status || 'pending';
    if (s === 'complete') return h('span', { className: 'alt-viol-etapas__badge alt-viol-etapas__badge--ok', 'aria-hidden': 'true' }, Check());
    if (s === 'error') return h('span', { className: 'alt-viol-etapas__badge alt-viol-etapas__badge--err', 'aria-hidden': 'true' }, '!');
    return h('span', { className: 'alt-viol-etapas__badge alt-num', 'aria-hidden': 'true' }, n);
  }

  function ViolEtapas(p) {
    var orientation = p.orientation || 'horizontal';
    var steps = p.steps || PRESETS[p.preset || (orientation === 'vertical' ? 'editor' : 'motor')];
    var ctl = p.active !== undefined;
    var st = R.useState(p.defaultActive !== undefined ? p.defaultActive : (steps[2] || steps[0]).id);
    var active = ctl ? p.active : st[0];
    var compact = !!p.compact;
    var summary = p.summary === undefined ? (p.preset === 'editor' || orientation === 'vertical' ? null : SUMMARY) : p.summary;

    function pick(id) { if (!ctl) st[1](id); if (p.onChange) p.onChange(id); }
    function key(e, i) {
      var next = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[e.key];
      if (!next) return;
      e.preventDefault();
      var j = (i + next + steps.length) % steps.length;
      var btns = e.currentTarget.parentNode.querySelectorAll('button');
      if (btns[j]) btns[j].focus();
    }

    return h('div', { className: cx('alt-viol-etapas', 'alt-viol-etapas--' + orientation, compact && 'alt-viol-etapas--compact', p.className), style: p.style },
      h('ol', { className: 'alt-viol-etapas__rail', 'aria-label': p.ariaLabel || 'Etapas' },
        steps.map(function (s, i) {
          var on = s.id === active;
          return h('li', { key: s.id, className: 'alt-viol-etapas__item' },
            h('button', {
              type: 'button',
              className: cx('alt-viol-etapas__step', on && 'is-active', 'is-' + (s.status || 'pending')),
              'aria-current': on ? 'step' : undefined,
              'aria-label': (i + 1) + '. ' + s.label + ' — ' + STATUS_TXT[s.status || 'pending'],
              disabled: s.disabled,
              onClick: function () { pick(s.id); },
              onKeyDown: function (e) { key(e, i); }
            },
              h('span', { className: 'alt-viol-etapas__title' }, Badge(s, i + 1), h('span', { className: 'alt-viol-etapas__label' }, s.label)),
              !compact && s.description ? h('span', { className: 'alt-viol-etapas__desc' }, s.description) : null));
        })),
      summary && summary.length ? h('dl', { className: 'alt-viol-etapas__summary alt-num' },
        summary.map(function (m, i) {
          return h('div', { key: i, className: cx('alt-viol-etapas__metric', m.tone && 'is-' + m.tone, m.muted && 'is-muted') },
            h('dt', null, m.label),
            h('dd', null, m.tone === 'error' ? A.Icon({ name: 'alert', size: 14 }) : null, typeof m.value === 'number' ? K.nf(m.value) : m.value));
        })) : null);
  }

  ViolEtapas.presets = PRESETS;
  A.ViolEtapas = ViolEtapas;
})();


/* ---- ViolRegrasBoard ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var STATUS = [
    { value: 'rascunho', label: 'Rascunho', tone: 'neutral', dropHint: 'Solte aqui para voltar a rascunho' },
    { value: 'pendente', label: 'Pendente de aprovação', tone: 'warning', dropHint: 'Solte aqui para enviar à aprovação' },
    { value: 'publicada', label: 'Publicada', tone: 'success', locked: true, note: 'Publicação exige simulação e aprovação' }
  ];
  var REGRAS = [
    { id: 'r1', nome: 'Excesso de velocidade', codigo: 'VEL-001', prioridade: 100, status: 'rascunho', estado: 'Em edição', categoria: 'Condução' },
    { id: 'r2', nome: 'Parada não autorizada', codigo: 'PAR-004', prioridade: 80, status: 'rascunho', categoria: 'Paradas' },
    { id: 'r3', nome: 'Frenagem brusca', codigo: 'FRE-003', prioridade: 60, status: 'rascunho', categoria: 'Condução' },
    { id: 'r4', nome: 'Desvio de rota', codigo: 'ROT-002', prioridade: 90, status: 'publicada', categoria: 'Rota', versao: 'v3' },
    { id: 'r5', nome: 'Temperatura fora da faixa', codigo: 'TMP-010', prioridade: 85, status: 'publicada', categoria: 'Carga', versao: 'v1' }
  ];

  function RuleCard(p) {
    var r = p.regra, st = p.statusOpt, locked = !!st.locked;
    var menuS = R.useState(!!p.menuAberto), menu = menuS[0], setMenu = menuS[1];
    var ref = R.useRef(null);
    R.useEffect(function () {
      if (!menu) return;
      function out(e) { if (ref.current && !ref.current.contains(e.target)) setMenu(false); }
      document.addEventListener('mousedown', out);
      return function () { document.removeEventListener('mousedown', out); };
    }, [menu]);
    var estado = locked ? null : (r.estado || (r.status === 'pendente' ? 'Aguardando aprovação' : null));
    var et = r.status === 'pendente' ? K.tone('info') : K.tone('warning');
    var dests = p.statusOptions.filter(function (s) { return s.value !== r.status && !s.locked; });
    var canEdit = !locked && p.canParametrizar;

    function mi(label, icon, fn, disabled) {
      return h('button', { type: 'button', role: 'menuitem', className: 'alt-menu-item', disabled: disabled, onClick: function () { setMenu(false); fn(); } }, A.Icon({ name: icon, size: 16 }), label);
    }

    return h('article', {
      className: cx('alt-viol-board__card', p.selected && 'is-selected', p.dragging && 'is-dragging', locked && 'is-locked'),
      draggable: !locked && p.canParametrizar ? true : undefined,
      onDragStart: function (e) { e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', r.id); p.onDragStart(r.id); },
      onDragEnd: p.onDragEnd,
      title: !locked && p.canParametrizar ? 'Arraste para mudar etapa' : undefined,
      'aria-label': r.nome
    },
      h('button', { type: 'button', className: 'alt-viol-board__select', 'aria-pressed': !!p.selected, onClick: function () { p.onSelect(r); } },
        h('span', { className: 'alt-viol-board__name' }, r.nome),
        h('span', { className: 'alt-viol-board__meta alt-num' }, r.codigo + ' · Prioridade ' + r.prioridade + (r.versao ? ' · ' + r.versao : ''))),
      h('div', { className: 'alt-viol-board__ft' },
        locked ? h('span', { className: 'alt-chip', style: { background: K.tone('success').bg, color: K.tone('success').c } }, A.Icon({ name: 'shield', size: 12 }), 'Publicada')
          : estado ? h('span', { className: 'alt-chip', style: { background: et.bg, color: et.c } }, A.Icon({ name: r.status === 'pendente' ? 'clock' : 'edit', size: 12 }), estado) : null,
        locked ? h('span', { className: 'alt-viol-board__lock' }, A.Icon({ name: 'lock', size: 12 }), p.canAdmin ? 'Admin: somente vigência' : 'Protegida contra edição') : null,
        h('span', { className: 'alt-viol-board__grow' }),
        !locked && p.canParametrizar ? h('span', { className: 'alt-viol-board__grip', 'aria-hidden': 'true' }, A.Icon({ name: 'drag', size: 16 })) : null,
        canEdit || (locked && p.canAdmin) ? h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost alt-viol-board__ib', 'aria-label': (locked ? 'Editar vigência de ' : 'Editar ') + r.nome, onClick: function () { p.onEdit(r); } }, A.Icon({ name: 'edit', size: 16 })) : null,
        h('div', { className: 'alt-viol-board__menuwrap', ref: ref },
          h('button', { type: 'button', className: 'alt-tbtn alt-tbtn--icon alt-tbtn--ghost alt-viol-board__ib', 'aria-label': 'Mais ações', 'aria-haspopup': 'menu', 'aria-expanded': menu, onClick: function () { setMenu(!menu); },
            onKeyDown: function (e) { if (e.key === 'Escape') setMenu(false); } }, A.Icon({ name: 'more', size: 18 })),
          menu ? h('div', { className: 'alt-popover alt-viol-board__menu', role: 'menu', onKeyDown: function (e) { if (e.key === 'Escape') setMenu(false); } },
            !locked && p.canParametrizar && dests.length ? h('div', { className: 'alt-viol-board__menulbl' }, 'Mover para…') : null,
            !locked && p.canParametrizar ? dests.map(function (s) {
              return h('button', { key: s.value, type: 'button', role: 'menuitem', className: 'alt-menu-item', onClick: function () { setMenu(false); p.onMove(r.id, s.value); } },
                h('span', { className: 'alt-dot', style: { color: K.tone(s.tone).c } }), s.label);
            }) : null,
            !locked && p.canParametrizar && dests.length ? h('div', { className: 'alt-viol-board__sep', role: 'separator' }) : null,
            mi('Nova versão', 'copy', function () { p.onVersion(r); }, !p.canParametrizar),
            mi('Simular e aprovar', 'check-circle', function () { p.onApprove(r); }, !p.canAprovar || r.status !== 'pendente')) : null)));
  }

  function ViolRegrasBoard(p) {
    var statusOptions = p.statusOptions || STATUS;
    var rs = R.useState(function () { return (p.regras || REGRAS).map(function (r) { return Object.assign({}, r); }); });
    var regras = p.controlled ? p.regras : rs[0];
    var selS = R.useState(p.selectedRegra !== undefined ? p.selectedRegra : (p.regras ? null : 'r1'));
    var selected = p.selectedRegra !== undefined && p.onSelect && p.controlled ? p.selectedRegra : selS[0];
    var dragS = R.useState(null), dragId = dragS[0], setDrag = dragS[1];
    var overS = R.useState(null), over = overS[0], setOver = overS[1];
    var liveS = R.useState('');
    var canP = p.canParametrizar !== false, canAdmin = !!p.canAdmin, canAprovar = p.canAprovar !== false;
    var noop = function () {};

    function move(id, status) {
      var r = regras.filter(function (x) { return x.id === id; })[0];
      var dest = statusOptions.filter(function (s) { return s.value === status; })[0];
      if (!r || !dest || dest.locked || r.status === status) return;
      if (!p.controlled) rs[1](function (list) { return list.map(function (x) { return x.id === id ? Object.assign({}, x, { status: status, estado: status === 'pendente' ? null : x.estado }) : x; }); });
      liveS[1](r.nome + ' movida para ' + dest.label + '.');
      if (p.onDropStatus) p.onDropStatus(id, status);
    }
    function select(r) { selS[1](r.id); if (p.onSelect) p.onSelect(r); }

    return h('div', { className: cx('alt-viol-board', p.className), style: p.style },
      h('div', { className: 'alt-viol-board__cols', style: { gridTemplateColumns: 'repeat(' + statusOptions.length + ', minmax(240px, 1fr))' } },
        statusOptions.map(function (s) {
          var items = regras.filter(function (r) { return r.status === s.value; }).sort(function (a, b) { return b.prioridade - a.prioridade; });
          var t = K.tone(s.tone);
          var dragging = !!dragId;
          var dragSrc = dragging && regras.filter(function (r) { return r.id === dragId; })[0];
          var canDrop = dragging && !s.locked && dragSrc && dragSrc.status !== s.value;
          var isOver = canDrop && over === s.value;
          return h('section', {
            key: s.value,
            className: cx('alt-viol-board__col', canDrop && 'is-target', isOver && 'is-over', dragging && s.locked && 'is-blocked'),
            'aria-label': s.label + ', ' + items.length + (items.length === 1 ? ' regra' : ' regras'),
            onDragOver: function (e) { if (canDrop) { e.preventDefault(); e.dataTransfer.dropEffect = 'move'; if (over !== s.value) setOver(s.value); } },
            onDragLeave: function (e) { if (!e.currentTarget.contains(e.relatedTarget)) setOver(null); },
            onDrop: function (e) { e.preventDefault(); var id = e.dataTransfer.getData('text/plain') || dragId; setOver(null); setDrag(null); if (canDrop) move(id, s.value); }
          },
            h('header', { className: 'alt-viol-board__colhd' },
              h('span', { className: 'alt-dot', style: { color: t.c } }),
              h('span', { className: 'alt-viol-board__coltt' }, s.label),
              s.locked ? h('span', { className: 'alt-viol-board__collock', title: s.note }, A.Icon({ name: 'lock', size: 14 })) : null,
              h('span', { className: 'alt-chip alt-num alt-viol-board__count' }, items.length)),
            h('div', { className: 'alt-viol-board__list' },
              items.map(function (r) {
                return h(RuleCard, { key: r.id, regra: r, statusOpt: s, statusOptions: statusOptions, selected: selected === r.id, dragging: dragId === r.id,
                  canParametrizar: canP, canAdmin: canAdmin, canAprovar: canAprovar, menuAberto: p.menuAberto === r.id,
                  onSelect: select, onEdit: p.onEdit || noop, onVersion: p.onVersion || noop, onApprove: p.onApprove || noop, onMove: move,
                  onDragStart: function (id) { setDrag(id); }, onDragEnd: function () { setDrag(null); setOver(null); } });
              }),
              canDrop ? h('div', { className: 'alt-viol-board__hint' }, s.dropHint || 'Solte aqui para mudar a etapa')
                : !items.length ? h('div', { className: 'alt-viol-board__hint is-empty' }, A.Icon({ name: 'inbox', size: 18 }), h('span', null, 'Sem regras nesta etapa'), canP && !s.locked ? h('span', { className: 'alt-viol-board__hintsub' }, 'Arraste para mudar etapa ou use ⋯ → Mover para…') : null) : null),
            s.note ? h('footer', { className: 'alt-viol-board__note' }, A.Icon({ name: 'info', size: 14 }), s.note) : null);
        })),
      h('div', { className: 'alt-viol-board__live', 'aria-live': 'polite' }, liveS[0]));
  }

  ViolRegrasBoard.RuleCard = RuleCard;
  A.ViolRegrasBoard = ViolRegrasBoard;
})();


/* ---- ViolSimulador ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx;

  var REGRAS = [
    { id: 'todas', nome: 'Todas as regras' },
    { id: 'r1', nome: 'Excesso de velocidade · VEL-001' },
    { id: 'r2', nome: 'Parada não autorizada · PAR-004' },
    { id: 'r4', nome: 'Desvio de rota · ROT-002' }
  ];
  var CONTEXTOS = [
    { value: 'com_viagem', label: 'Somente eventos com viagem' },
    { value: 'todos', label: 'Todos os eventos' },
    { value: 'sem_viagem', label: 'Somente eventos sem viagem' }
  ];
  var VERSOES = [{ value: 'auto', label: 'Automática' }, { value: 'publicada', label: 'Publicada (v3)' }, { value: 'rascunho', label: 'Rascunho em edição' }];
  var RESULTADO = {
    periodo: '29/09–30/09',
    total: 14,
    brutos: 1286,
    mapeados: 1283,
    diagnostico: [{ tone: 'warning', texto: '3 eventos brutos sem mapeamento foram ignorados.' }],
    itens: [
      { placa: 'ABC-1D23', regra: 'Excesso de velocidade', quando: '30/09 14:12', severidade: 'Crítica', tone: 'error', valor: '97 km/h' },
      { placa: 'QRT-4F56', regra: 'Parada não autorizada', quando: '30/09 13:40', severidade: 'Crítica', tone: 'error', valor: '18 min' },
      { placa: 'KLM-7G89', regra: 'Excesso de velocidade', quando: '30/09 09:05', severidade: 'Alta', tone: 'warning', valor: '88 km/h' },
      { placa: 'PQS-2H10', regra: 'Desvio de rota', quando: '29/09 22:47', severidade: 'Alta', tone: 'warning', valor: '4,2 km' },
      { placa: 'TUV-5J21', regra: 'Parada não autorizada', quando: '29/09 17:31', severidade: 'Média', tone: 'info', valor: '9 min' }
    ]
  };

  function FlaskIcon() {
    return h('svg', { width: 18, height: 18, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': 'true', className: 'alt-icon' },
      h('path', { d: 'M9 3h6M10 3v6L4 19a1 1 0 0 0 1 2h14a1 1 0 0 0 1-2l-6-10V3M7 15h10' }));
  }

  function Select(p) {
    return h('div', { className: 'alt-field alt-viol-sim__fld' },
      h('label', { className: 'alt-field__label', htmlFor: p.id }, p.label),
      h('div', { className: 'alt-ctl alt-viol-sim__select' },
        h('select', { id: p.id, value: p.value, disabled: p.disabled, onChange: function (e) { p.onChange(e.target.value); } },
          p.options.map(function (o) { return h('option', { key: o.value, value: o.value }, o.label); })),
        A.Icon({ name: 'chevron-down', size: 16 })));
  }

  /* Bloco de resultado compartilhado (Simulador e modal de aprovação). */
  function SimulationResult(p) {
    var res = p.resultado;
    var rawS = R.useState(!!p.rawOpen), raw = rawS[0];
    var max = p.maxItens || 30;
    var itens = (res.itens || []).slice(0, max);
    return h('div', { className: 'alt-viol-sim__res' },
      h('div', { className: 'alt-viol-sim__kpi' },
        h('span', { className: 'alt-viol-sim__big alt-num' }, K.nf(res.total)),
        h('span', { className: 'alt-viol-sim__legend' }, (res.total === 1 ? 'ocorrência prevista' : 'ocorrências previstas') + (res.periodo ? ' em ' + res.periodo : '')),
        res.brutos != null ? h('span', { className: 'alt-viol-sim__aside alt-num' }, K.nf(res.brutos) + ' eventos brutos · ' + K.nf(res.mapeados) + ' mapeados') : null),
      (res.diagnostico || []).map(function (d, i) {
        var t = K.tone(d.tone || 'info');
        return h('div', { key: i, className: 'alt-viol-sim__alert', role: 'status', style: { background: t.bg } },
          h('span', { style: { color: t.c, display: 'inline-flex' } }, A.Icon({ name: t.icon, size: 16 })), h('span', null, d.texto));
      }),
      itens.length ? h('div', { className: 'alt-viol-sim__tablewrap alt-scroll' },
        h('table', { className: 'alt-viol-sim__table alt-num' },
          h('thead', null, h('tr', null, ['Placa', 'Regra', 'Quando', 'Medido', 'Severidade'].map(function (c) { return h('th', { key: c, scope: 'col' }, c); }))),
          h('tbody', null, itens.map(function (it, i) {
            var t = K.tone(it.tone || 'neutral');
            return h('tr', { key: i },
              h('td', { className: 'alt-viol-sim__plate' }, it.placa),
              h('td', null, it.regra),
              h('td', null, it.quando),
              h('td', null, it.valor || '—'),
              h('td', null, h('span', { className: 'alt-chip', style: { background: t.bg, color: t.c } }, h('span', { className: 'alt-dot' }), it.severidade)));
          })))) : h('div', { className: 'alt-viol-sim__empty' }, A.Icon({ name: 'check-circle', size: 16 }), 'Nenhuma ocorrência seria gerada neste período.'),
      res.itens && res.itens.length > max ? h('span', { className: 'alt-field__help alt-num' }, 'Mostrando ' + max + ' de ' + K.nf(res.itens.length) + '.') : null,
      h('div', { className: 'alt-viol-sim__raw' },
        h('button', { type: 'button', className: 'alt-viol-sim__rawbtn', 'aria-expanded': raw, onClick: function () { rawS[1](!raw); } },
          A.Icon({ name: raw ? 'chevron-down' : 'chevron-right', size: 16 }), raw ? 'Ocultar dados brutos' : 'Ver dados brutos'),
        raw ? h('pre', { className: 'alt-viol-sim__pre alt-scroll', tabIndex: 0, 'aria-label': 'Dados brutos da simulação' }, JSON.stringify(res.bruto || res, null, 2)) : null));
  }

  function fakeSimular(params) {
    return new Promise(function (ok) { setTimeout(function () { ok(Object.assign({}, RESULTADO, { params: params })); }, 900); });
  }

  function ViolSimulador(p) {
    var regras = p.regras || REGRAS;
    var rS = R.useState(p.initialRegraId || regras[0].id);
    var vS = R.useState(p.initialVersao || 'auto');
    var cS = R.useState(p.initialContexto || 'com_viagem');
    var fS = R.useState(p.initialFonte || '');
    var eS = R.useState(p.initialCodigoEventoExterno || '');
    var resS = R.useState(p.resultado !== undefined ? p.resultado : (p.regras ? null : RESULTADO)), res = resS[0];
    var lS = R.useState(false), loading = lS[0];
    var errS = R.useState(null);

    function run() {
      var params = { contrato: p.contrato, regraId: rS[0], versao: vS[0], contexto: cS[0], fonte: fS[0] || undefined, codigoEventoExterno: eS[0] || undefined };
      lS[1](true); errS[1](null);
      (p.simular || fakeSimular)(params).then(function (r) {
        resS[1](r); lS[1](false);
        if (p.onSimulationResult) p.onSimulationResult(r);
      }, function (e) { lS[1](false); errS[1]((e && e.message) || 'Não foi possível simular. Tente de novo.'); });
    }

    var done = !!res && !loading;
    var etapas = [
      { id: 'massa', label: 'Massa bruta', status: done ? 'complete' : 'pending' },
      { id: 'depara', label: 'De-para', status: done ? (res.diagnostico && res.diagnostico.length ? 'error' : 'complete') : 'pending' },
      { id: 'regra', label: 'Regra', status: done ? 'complete' : 'pending' },
      { id: 'saida', label: 'Saída prevista', status: done ? 'complete' : 'pending' }
    ];
    var etS = R.useState('saida');

    return h('section', { className: cx('alt-glass alt-viol-sim', p.className), style: p.style, 'aria-label': p.titulo || 'Simulador de processamento' },
      h('header', { className: 'alt-viol-sim__hd' },
        h('span', { className: 'alt-viol-sim__hdicon' }, FlaskIcon()),
        h('span', { className: 'alt-viol-sim__tt' }, p.titulo || 'Simulador de processamento'),
        p.contrato ? h('span', { className: 'alt-chip alt-viol-sim__contrato' }, A.Icon({ name: 'file', size: 12 }), p.contrato.nome || p.contrato) : null),
      h('div', { className: 'alt-viol-sim__bd' },
        A.ViolEtapas ? h(A.ViolEtapas, { compact: true, summary: null, steps: etapas, active: etS[0], onChange: etS[1], ariaLabel: 'Etapas do processamento' }) : null,
        h('div', { className: 'alt-viol-sim__ctls' },
          h(Select, { id: 'alt-vsim-regra', label: 'Regra', value: rS[0], onChange: rS[1], disabled: loading, options: regras.map(function (r) { return { value: r.id, label: r.nome }; }) }),
          h(Select, { id: 'alt-vsim-versao', label: 'Versão', value: vS[0], onChange: vS[1], disabled: loading, options: p.versoes || VERSOES }),
          h(Select, { id: 'alt-vsim-ctx', label: 'Contexto', value: cS[0], onChange: cS[1], disabled: loading, options: p.contextos || CONTEXTOS }),
          h('div', { className: 'alt-viol-sim__run' },
            h(A.Button, { size: 'sm', arrow: false, onClick: run, disabled: loading, 'aria-busy': loading, className: 'alt-viol-sim__btn' },
              A.Icon({ name: loading ? 'refresh' : 'play', size: 14, className: loading ? 'alt-viol-sim__spin' : undefined }), loading ? 'Simulando…' : 'Simular regras'))),
        p.mostrarFiltrosEvento ? h('div', { className: 'alt-viol-sim__ctls alt-viol-sim__ctls--2' },
          h('div', { className: 'alt-field' }, h('label', { className: 'alt-field__label', htmlFor: 'alt-vsim-fonte' }, 'Fonte'), h('div', { className: 'alt-ctl alt-ctl--sm' }, h('input', { id: 'alt-vsim-fonte', value: fS[0], placeholder: 'Todas as fontes', onChange: function (e) { fS[1](e.target.value); } }))),
          h('div', { className: 'alt-field' }, h('label', { className: 'alt-field__label', htmlFor: 'alt-vsim-cod' }, 'Código do evento externo'), h('div', { className: 'alt-ctl alt-ctl--sm' }, h('input', { id: 'alt-vsim-cod', value: eS[0], placeholder: 'Ex.: PARADA_EXCESSIVA', onChange: function (e) { eS[1](e.target.value); } })))) : null,
        errS[0] ? h('div', { className: 'alt-viol-sim__alert', role: 'alert', style: { background: K.tone('error').bg } }, h('span', { style: { color: K.tone('error').c, display: 'inline-flex' } }, A.Icon({ name: 'x-circle', size: 16 })), errS[0]) : null,
        loading ? h('div', { className: 'alt-viol-sim__loading', 'aria-hidden': 'true' },
          h('div', { className: 'alt-skel', style: { width: 180, height: 30 } }),
          h('div', { className: 'alt-skel', style: { height: 40 } }),
          h('div', { className: 'alt-skel', style: { height: 120 } }))
          : res ? h(SimulationResult, { resultado: res, maxItens: p.maxItens, rawOpen: p.rawOpen })
            : h('div', { className: 'alt-viol-sim__empty' }, A.Icon({ name: 'info', size: 16 }), 'Escolha a regra e o contexto e clique em Simular regras para ver quantas ocorrências seriam geradas.')));
  }

  ViolSimulador.Result = SimulationResult;
  A.ViolSimulador = ViolSimulador;
})();


/* ---- VirtualizedAutocomplete ---- */
(function () {
  var A = window.Altivy, K = A._, h = K.h, R = K.R, cx = K.cx, nf = K.nf;

  /* Strip accents char by char so indices stay aligned with the original string. */
  function fold(s) {
    s = String(s == null ? '' : s);
    var out = '';
    for (var i = 0; i < s.length; i++) { var c = s[i].normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase(); out += c.length === 1 ? c : s[i].toLowerCase(); }
    return out;
  }
  function initials(s) { var w = String(s).trim().split(/\s+/); return ((w[0] || '')[0] || '').toUpperCase() + ((w.length > 1 ? w[w.length - 1][0] : (w[0] || '')[1]) || '').toUpperCase(); }

  /* Deterministic demo list: 5.000 motoristas with CNH and CCO. */
  var DEMO = null;
  function demoOptions() {
    if (DEMO) return DEMO;
    var first = ['Marta', 'Marcos', 'Mariana', 'João', 'Ana', 'Carlos', 'Paulo', 'Rita', 'Bruno', 'Lúcia', 'Pedro', 'Sônia', 'Márcio', 'Fernanda', 'Rafael', 'Juliana', 'Diego', 'Camila', 'Tiago', 'Beatriz', 'André', 'Patrícia', 'Gustavo', 'Renata', 'Felipe'];
    var last = ['Souza', 'Oliveira', 'Alves', 'Pereira', 'Lima', 'Costa', 'Ramos', 'Gomes', 'Ribeiro', 'Martins', 'Rocha', 'Barbosa', 'Teixeira', 'Moreira', 'Cardoso', 'Nunes', 'Mendes', 'Freitas', 'Araújo', 'Pinto'];
    var cco = ['CCO Norte', 'CCO Sul', 'CCO Leste', 'CCO Oeste'];
    var out = [], seed = 7;
    for (var i = 0; i < 5000; i++) {
      seed = (seed * 9301 + 49297) % 233280;
      var f = first[i % first.length], l = last[Math.floor(i / first.length) % last.length];
      var suffix = i >= first.length * last.length ? ' ' + last[(Math.floor(i / 7)) % last.length] : '';
      var cnh = String(10000 + (seed % 89999)).padStart(5, '0');
      out.push({ id: i + 1, label: f + suffix + ' ' + l, caption: 'CNH ' + cnh + '… · ' + cco[(i + seed) % cco.length] });
    }
    DEMO = out; return out;
  }

  function Highlight(p) {
    var text = String(p.text), q = fold(p.query).trim();
    if (!q) return h('span', null, text);
    var i = fold(text).indexOf(q);
    if (i < 0) return h('span', null, text);
    return h('span', null, text.slice(0, i), h('b', null, text.slice(i, i + q.length)), text.slice(i + q.length));
  }

  function VirtualizedAutocomplete(p) {
    var options = p.options || demoOptions();
    var getLabel = p.getOptionLabel || function (o) { return o && typeof o === 'object' ? o.label : String(o); };
    var getCaption = p.getOptionCaption || function (o) { return o && typeof o === 'object' ? o.caption : null; };
    var getKey = p.getOptionKey || function (o, i) { return o && typeof o === 'object' && o.id != null ? o.id : i; };
    var itemH = p.itemHeight || 48, maxH = p.maxHeight || 300, overscan = p.overscan == null ? 4 : p.overscan;

    var vCtl = p.value !== undefined, iCtl = p.inputValue !== undefined;
    var vs = R.useState(p.defaultValue === undefined ? null : p.defaultValue); var value = vCtl ? p.value : vs[0];
    var is = R.useState(p.defaultInputValue === undefined ? '' : p.defaultInputValue); var text = iCtl ? p.inputValue : is[0];
    var os = R.useState(!!p.defaultOpen); var open = os[0], setOpen = os[1];
    var as = R.useState(0); var active = as[0], setActive = as[1];
    var ss = R.useState(0); var scrollTop = ss[0], setScrollTop = ss[1];
    var root = R.useRef(null), listRef = R.useRef(null), inputRef = R.useRef(null);
    var id = R.useRef('alt-va-' + Math.random().toString(36).slice(2, 8)).current;

    /* When the input shows the selected label, list everything. */
    var query = value != null && text === getLabel(value) ? '' : text;
    var filtered = R.useMemo(function () {
      var q = fold(query).trim();
      if (!q) return options;
      var out = [];
      for (var i = 0; i < options.length; i++) if (fold(getLabel(options[i])).indexOf(q) >= 0) out.push(options[i]);
      return out;
    }, [options, query]);

    R.useEffect(function () {
      if (!open) return undefined;
      function f(e) { if (root.current && !root.current.contains(e.target)) close(); }
      document.addEventListener('mousedown', f);
      return function () { document.removeEventListener('mousedown', f); };
    }, [open, value]);

    function setText(t) { if (!iCtl) is[1](t); if (p.onInputChange) p.onInputChange(t); }
    function select(o) {
      if (!vCtl) vs[1](o);
      if (p.onChange) p.onChange(o);
      setText(o ? getLabel(o) : '');
      setOpen(false);
    }
    function close() { setOpen(false); if (value != null) setText(getLabel(value)); }
    function scrollTo(i) {
      var el = listRef.current; if (!el) return;
      var top = i * itemH, bottom = top + itemH, viewH = el.clientHeight;
      if (top < el.scrollTop) el.scrollTop = top;
      else if (bottom > el.scrollTop + viewH) el.scrollTop = bottom - viewH;
    }
    function move(d) {
      var n = Math.max(0, Math.min(filtered.length - 1, active + d));
      setActive(n); scrollTo(n);
    }
    function onKey(e) {
      if (e.key === 'ArrowDown') { e.preventDefault(); if (!open) setOpen(true); else move(1); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); move(-1); }
      else if (e.key === 'PageDown') { e.preventDefault(); move(Math.floor(maxH / itemH)); }
      else if (e.key === 'PageUp') { e.preventDefault(); move(-Math.floor(maxH / itemH)); }
      else if (e.key === 'Enter' && open && filtered[active]) { e.preventDefault(); select(filtered[active]); }
      else if (e.key === 'Escape') { e.preventDefault(); close(); }
    }

    /* Windowing: only rows intersecting the viewport (+ overscan) are mounted. */
    var listH = Math.min(maxH, filtered.length * itemH + 8);
    var start = Math.max(0, Math.floor(scrollTop / itemH) - overscan);
    var end = Math.min(filtered.length, Math.ceil((scrollTop + listH) / itemH) + overscan);
    var rows = [];
    for (var i = start; i < end; i++) {
      (function (o, i) {
        var sel = value != null && getKey(value) === getKey(o, i);
        rows.push(h('div', {
          key: getKey(o, i), id: id + '-o' + i, role: 'option', 'aria-selected': sel, 'aria-posinset': i + 1, 'aria-setsize': filtered.length,
          className: cx('alt-virtualized-autocomplete__opt', i === active && 'is-active', sel && 'is-selected'),
          style: { top: 4 + i * itemH, height: itemH },
          onMouseDown: function (e) { e.preventDefault(); }, onMouseMove: function () { if (active !== i) setActive(i); },
          onClick: function () { select(o); }
        }, p.renderOption ? p.renderOption(o, { query: query, selected: sel, active: i === active }) : [
          h('span', { key: 'a', className: 'alt-virtualized-autocomplete__av', 'aria-hidden': 'true' }, initials(getLabel(o))),
          h('span', { key: 't', className: 'alt-virtualized-autocomplete__txt' },
            h('span', { className: 'alt-virtualized-autocomplete__name' }, h(Highlight, { text: getLabel(o), query: query })),
            getCaption(o) ? h('small', { className: 'alt-num' }, getCaption(o)) : null),
          sel ? h(A.Icon, { key: 'c', name: 'check', size: 16, className: 'alt-virtualized-autocomplete__ck', title: 'Selecionado' }) : null
        ]));
      })(filtered[i], i);
    }

    var label = p.label === undefined ? 'Motorista' : p.label;
    return h('div', { ref: root, className: cx('alt-virtualized-autocomplete', p.className), style: Object.assign({ maxWidth: p.width || 360 }, p.style) },
      label ? h('label', { className: 'alt-field__label', htmlFor: id }, label, p.required !== false && p.required !== undefined ? h('span', { className: 'alt-virtualized-autocomplete__req', 'aria-hidden': 'true' }, ' *') : null) : null,
      h('div', { className: cx('alt-ctl', 'alt-virtualized-autocomplete__ctl', open && 'is-open', p.error && 'alt-ctl--error', p.disabled && 'alt-ctl--disabled') },
        h(A.Icon, { name: 'search', size: 16 }),
        h('input', {
          id: id, ref: inputRef, value: text, disabled: p.disabled, required: p.required, role: 'combobox', autoComplete: 'off',
          'aria-expanded': open, 'aria-controls': id + '-list', 'aria-autocomplete': 'list', 'aria-activedescendant': open ? id + '-o' + active : undefined,
          placeholder: p.placeholder || 'Buscar por nome',
          onChange: function (e) { setText(e.target.value); setOpen(true); setActive(0); setScrollTop(0); if (listRef.current) listRef.current.scrollTop = 0; },
          onFocus: function () { setOpen(true); }, onKeyDown: onKey
        }),
        h('span', { className: 'alt-virtualized-autocomplete__adorn' },
          text ? h('button', { type: 'button', className: 'alt-virtualized-autocomplete__ib', 'aria-label': 'Limpar', onClick: function () { select(null); setOpen(true); inputRef.current && inputRef.current.focus(); } }, h(A.Icon, { name: 'x', size: 15 })) : null,
          h('button', { type: 'button', tabIndex: -1, className: 'alt-virtualized-autocomplete__ib', 'aria-label': open ? 'Fechar lista' : 'Abrir lista', onClick: function () { open ? close() : setOpen(true); } },
            h(A.Icon, { name: open ? 'chevron-up' : 'chevron-down', size: 16 })))),
      open ? h('div', { className: 'alt-popover alt-virtualized-autocomplete__pop' },
        filtered.length
          ? h('div', {
            ref: listRef, id: id + '-list', role: 'listbox', className: 'alt-virtualized-autocomplete__list', style: { height: listH },
            onScroll: function (e) { setScrollTop(e.currentTarget.scrollTop); }
          }, h('div', { style: { height: filtered.length * itemH + 8, position: 'relative' } }, rows))
          : h('div', { className: 'alt-virtualized-autocomplete__empty' }, 'Nenhum resultado para “' + text + '”'),
        p.showFooter === false ? null : h('div', { className: 'alt-virtualized-autocomplete__foot alt-num', title: 'Lista virtualizada: só as linhas visíveis ficam no DOM' },
          h(A.Icon, { name: 'list', size: 14 }),
          nf(options.length) + ' opções · ' + (query ? nf(filtered.length) + ' correspondem · ' : '') + itemH + 'px por item')) : null,
      p.helperText ? h('div', { className: cx('alt-field__help', p.error && 'alt-virtualized-autocomplete__err') }, p.helperText) : null);
  }

  VirtualizedAutocomplete.demoOptions = demoOptions;
  A.VirtualizedAutocomplete = VirtualizedAutocomplete;
})();

