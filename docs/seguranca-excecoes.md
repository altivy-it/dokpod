# Exceções de vulnerabilidade

Aceites formais de vulnerabilidades HIGH/CRITICAL **sem correção disponível** (spec 005 FR-003). A fonte usada pelo CI é
`tools/security/trivy-acceptances.json`; este documento é a leitura humana.

**Política** (`tools/scripts/validate-release-images.ps1`, job Images do CI):

- HIGH/CRITICAL **com** correção disponível reprova a imagem;
- HIGH/CRITICAL **sem** correção só passa com aceite da mesma imagem, CVE e pacote, dentro da validade;
- aceite vencido reprova. Renovar exige nova revisão (varredura atual, impacto e nova data).

## Imagem web (base nginx 1.28 sobre Debian 12)

- **Decisão**: manter a base e aceitar formalmente (solicitante, 2026-10-17). Alternativa avaliada e não adotada agora: base nginx sobre Alpine.
- **Responsável**: lzocateli (solicitante). **Aceite**: 2026-10-17. **Expira**: 2027-01-15.
- **Varredura**: Trivy, 2026-10-17 — 94 achados (3 CRITICAL e 91 HIGH), todos com estado do Debian *affected*, *fix_deferred* ou *will_not_fix*. API, BFF e agente sem HIGH/CRITICAL.
- **Contexto**: o container web só serve os arquivos estáticos do console em HTTP interno, atrás da borda nginx (TLS termina na borda); não processa imagens, XML nem executa utilitários do sistema em operação.
- **Observação**: `curl` e `libcurl4` (10 HIGH) são instalados pelo Dockerfile do Dokpod só para o health check local; trocar o health check elimina esses achados.

| Pacote | Achados | Impacto no container web | CVEs |
| --- | --- | --- | --- |
| `bsdutils` | 6 | Utilitários de sistema (util-linux); não são executados pelo nginx em operação. | CVE-2026-53613 (HIGH), CVE-2026-53615 (HIGH), CVE-2026-76642 (HIGH), CVE-2026-78408 (HIGH), CVE-2026-78409 (HIGH), CVE-2026-78410 (HIGH) |
| `curl` | 5 | Instalado pelo Dockerfile do Dokpod só para o health check local (127.0.0.1); nenhuma URL externa é acessada. Pode ser eliminado trocando o health check. | CVE-2026-12064 (HIGH), CVE-2026-6276 (HIGH), CVE-2026-8286 (HIGH), CVE-2026-8458 (HIGH), CVE-2026-8927 (HIGH) |
| `gzip` | 1 | Utilitário gzip; o nginx comprime com zlib própria em memória, não executa gzip. | CVE-2026-41992 (HIGH) |
| `libacl1` | 1 | ACLs de arquivo; sem entrada externa no container web. | CVE-2026-54369 (HIGH) |
| `libaom3` | 2 | Codec AV1 da base de imagem; o console não decodifica mídia no servidor. | CVE-2023-39616 (HIGH), CVE-2023-6879 (CRITICAL) |
| `libblkid1` | 6 | Biblioteca de util-linux; usada por utilitários de disco, não pelo nginx. | CVE-2026-53613 (HIGH), CVE-2026-53615 (HIGH), CVE-2026-76642 (HIGH), CVE-2026-78408 (HIGH), CVE-2026-78409 (HIGH), CVE-2026-78410 (HIGH) |
| `libcurl4` | 5 | Dependência do curl do health check local; mesma observação do curl. | CVE-2026-12064 (HIGH), CVE-2026-6276 (HIGH), CVE-2026-8286 (HIGH), CVE-2026-8458 (HIGH), CVE-2026-8927 (HIGH) |
| `libexpat1` | 1 | Parser XML; o nginx do console não habilita módulos XML. | CVE-2026-77214 (HIGH) |
| `libheif1` | 8 | Decodificador HEIF da base de imagem; o nginx não processa imagens. | CVE-2025-68431 (HIGH), CVE-2026-32740 (HIGH), CVE-2026-32741 (HIGH), CVE-2026-32882 (HIGH), CVE-2026-41071 (HIGH), CVE-2026-47178 (HIGH), CVE-2026-84450 (HIGH), CVE-2026-84451 (HIGH) |
| `libldap-2.5-0` | 1 | Cliente LDAP; o container web não acessa LDAP. | CVE-2023-2953 (HIGH) |
| `libmount1` | 6 | Biblioteca de util-linux; usada por utilitários de disco, não pelo nginx. | CVE-2026-53613 (HIGH), CVE-2026-53615 (HIGH), CVE-2026-76642 (HIGH), CVE-2026-78408 (HIGH), CVE-2026-78409 (HIGH), CVE-2026-78410 (HIGH) |
| `libsmartcols1` | 6 | Biblioteca de util-linux; não usada pelo nginx. | CVE-2026-53613 (HIGH), CVE-2026-53615 (HIGH), CVE-2026-76642 (HIGH), CVE-2026-78408 (HIGH), CVE-2026-78409 (HIGH), CVE-2026-78410 (HIGH) |
| `libssl3` | 1 | OpenSSL; o container web serve HTTP interno, sem TLS (TLS termina na borda). | CVE-2026-84782 (HIGH) |
| `libsystemd0` | 1 | Biblioteca do systemd; não há systemd no container. | CVE-2026-16742 (HIGH) |
| `libtiff6` | 4 | Decodificador TIFF da base de imagem; o nginx não processa imagens. | CVE-2023-52355 (HIGH), CVE-2026-12912 (HIGH), CVE-2026-36849 (HIGH), CVE-2026-52490 (HIGH) |
| `libtinfo6` | 1 | Terminfo; sem terminal interativo em operação. | CVE-2025-69720 (HIGH) |
| `libudev1` | 1 | Biblioteca do udev; não há udev no container. | CVE-2026-16742 (HIGH) |
| `libuuid1` | 6 | Biblioteca de util-linux; não exposta a entrada externa no container web. | CVE-2026-53613 (HIGH), CVE-2026-53615 (HIGH), CVE-2026-76642 (HIGH), CVE-2026-78408 (HIGH), CVE-2026-78409 (HIGH), CVE-2026-78410 (HIGH) |
| `libx11-6` | 1 | Biblioteca X11 da base de imagem; sem servidor gráfico no container. | CVE-2026-88806 (HIGH) |
| `libx11-data` | 1 | Dados da X11; sem servidor gráfico no container. | CVE-2026-88806 (HIGH) |
| `libxml2` | 7 | Parser XML (módulo xslt da base); o console não usa xslt nem processa XML. | CVE-2026-6653 (CRITICAL), CVE-2026-74860 (HIGH), CVE-2026-86138 (HIGH), CVE-2026-86140 (HIGH), CVE-2026-86142 (HIGH), CVE-2026-86143 (HIGH), CVE-2026-86144 (HIGH) |
| `mount` | 6 | Utilitário mount; o container não monta sistemas de arquivos. | CVE-2026-53613 (HIGH), CVE-2026-53615 (HIGH), CVE-2026-76642 (HIGH), CVE-2026-78408 (HIGH), CVE-2026-78409 (HIGH), CVE-2026-78410 (HIGH) |
| `ncurses-base` | 1 | Terminfo; sem terminal interativo em operação. | CVE-2025-69720 (HIGH) |
| `ncurses-bin` | 1 | Utilitários ncurses; não executados em operação. | CVE-2025-69720 (HIGH) |
| `openssl` | 1 | Utilitário OpenSSL; não executado em operação. | CVE-2026-84782 (HIGH) |
| `perl-base` | 1 | Perl da base Debian; não executado pelo nginx. | CVE-2026-9538 (HIGH) |
| `util-linux` | 6 | Utilitários de sistema; não são executados pelo nginx em operação. | CVE-2026-53613 (HIGH), CVE-2026-53615 (HIGH), CVE-2026-76642 (HIGH), CVE-2026-78408 (HIGH), CVE-2026-78409 (HIGH), CVE-2026-78410 (HIGH) |
| `util-linux-extra` | 6 | Utilitários de sistema; não são executados pelo nginx em operação. | CVE-2026-53613 (HIGH), CVE-2026-53615 (HIGH), CVE-2026-76642 (HIGH), CVE-2026-78408 (HIGH), CVE-2026-78409 (HIGH), CVE-2026-78410 (HIGH) |
| `zlib1g` | 1 | Compressão; a CVE afeta funções (minizip) não usadas pelo nginx. | CVE-2023-45853 (CRITICAL) |
