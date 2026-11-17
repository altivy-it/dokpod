/*
 * Escolha automática de idioma somente na entrada neutra "/".
 * Precedência: URL explícita > escolha manual (dokpod.docs.locale) >
 * navigator.languages > português. Links profundos nunca são redirecionados.
 */
(function () {
  var storageKey = 'dokpod.docs.locale';
  var entries = {'pt-BR': '/', en: '/en/', es: '/es/'};
  var location = window.location;
  if (location.pathname !== '/' || /(^|[?&])locale=/.test(location.search)) {
    return;
  }
  var choice = null;
  try {
    choice = window.localStorage.getItem(storageKey);
  } catch (error) {
    choice = null;
  }
  if (!entries.hasOwnProperty(choice)) {
    choice = 'pt-BR';
    var languages = navigator.languages && navigator.languages.length
      ? navigator.languages
      : [navigator.language || ''];
    for (var i = 0; i < languages.length; i += 1) {
      var prefix = String(languages[i]).toLowerCase().split('-')[0];
      if (prefix === 'pt') { choice = 'pt-BR'; break; }
      if (prefix === 'en') { choice = 'en'; break; }
      if (prefix === 'es') { choice = 'es'; break; }
    }
  }
  if (choice !== 'pt-BR') {
    location.replace(entries[choice] + location.hash);
  }
})();
