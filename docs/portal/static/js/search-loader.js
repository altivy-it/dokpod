/*
 * Carrega o módulo do Pagefind fora do webpack (o índice só existe depois do
 * build). import() dinâmico é permitido em script clássico.
 */
(function () {
  var pending = null;
  window.dokpodLoadPagefind = function () {
    if (!pending) {
      pending = import('/pagefind/pagefind.js');
    }
    return pending;
  };
})();
