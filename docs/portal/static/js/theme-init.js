/*
 * Aplica a escolha de tema do portal antes do script de cor do Docusaurus.
 * altivy.docs.theme: dark, light ou system. Sem escolha válida vale o escuro,
 * padrão da marca. A chave "theme" é a que o Docusaurus lê; em "system" ela é
 * removida para que o Docusaurus siga prefers-color-scheme.
 */
(function () {
  var choiceKey = 'altivy.docs.theme';
  var docusaurusKey = 'theme';
  var valid = {dark: true, light: true, system: true};
  try {
    var storage = window.localStorage;
    var choice = storage.getItem(choiceKey);
    if (!valid.hasOwnProperty(choice)) {
      choice = 'dark';
    }
    if (choice === 'system') {
      storage.removeItem(docusaurusKey);
    } else {
      storage.setItem(docusaurusKey, choice);
    }
  } catch (error) {
    // Armazenamento indisponível: o Docusaurus segue o sistema e a leitura continua.
  }
})();
