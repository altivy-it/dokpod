# Contrato: `--no-logo` e o logo do instalador

Acréscimo ao [contrato da CLI da 033](../../033-instalador-guiado/contracts/installer-cli.md). A implementação atualiza aquele arquivo; este registra o que muda.

## Opção global nova

| Opção | Padrão | Observação |
| --- | --- | --- |
| `--no-logo` | Logo só em terminal interativo | Aceita em qualquer comando e posição, inclusive `help`; não muda resultado, arquivos nem código de saída. |

`--non-interactive` passa a desligar o logo também.

## Saída (acréscimo)

- **Logo**: só quando entrada e saída são terminal e não há `--no-logo` nem `--non-interactive`. Vem antes de qualquer outra linha, uma vez por execução:

  ```text
  ██████╗  ██████╗ ██╗  ██╗██████╗  ██████╗ ██████╗
  ██╔══██╗██╔═══██╗██║ ██╔╝██╔══██╗██╔═══██╗██╔══██╗
  ██║  ██║██║   ██║█████╔╝ ██████╔╝██║   ██║██║  ██║
  ██║  ██║██║   ██║██╔═██╗ ██╔═══╝ ██║   ██║██║  ██║
  ██████╔╝╚██████╔╝██║  ██╗██║     ╚██████╔╝██████╔╝
  ╚═════╝  ╚═════╝ ╚═╝  ╚═╝╚═╝      ╚═════╝ ╚═════╝

  Instalador do Dokpod <versão> · Altivy
  ```

  - `<versão>` é a do próprio instalador (`dev` num build local).
  - Cor: degradê de amarelo para o lima da marca (`#b7f34a`) por linha e a linha da versão apagada; `NO_COLOR` ou `TERM=dumb` tiram a cor.
  - Sem UTF-8: o mesmo desenho com `#` no lugar dos blocos e sem a sombra.
  - Terminal com menos de 52 colunas: só a linha da versão.

## Bootstrap

O `install.sh` repassa ao container, só pelo nome e só se definidas, `NO_COLOR`, `TERM`, `COLORTERM`, `LANG`, `LC_ALL` e `LC_CTYPE`.
