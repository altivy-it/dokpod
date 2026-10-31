# Changelog

## [0.2.0](https://github.com/altivy-it/dokpod/compare/v0.1.1...v0.2.0) (2026-10-31)


### Adicionado

* **contracts:** crie o verificador Dokpod.Licensing e publique a API de licenciamento ([#84](https://github.com/altivy-it/dokpod/issues/84)) ([40409b1](https://github.com/altivy-it/dokpod/commit/40409b199f65e2b6bdf0d6eae2e19958a3bcfe4a))

## [0.1.1](https://github.com/altivy-it/dokpod/compare/v0.1.0...v0.1.1) (2026-10-30)


### Corrigido

* **api:** filtre as leituras por scope com uma só consulta de grants ([#66](https://github.com/altivy-it/dokpod/issues/66)) ([021a9a2](https://github.com/altivy-it/dokpod/commit/021a9a23abc2b06d778ae36f7d4bc70e34ba1753))

## 0.1.0 (2026-10-29)


### Adicionado

* add Gitleaks secret scanning hook and update documentation ([#2](https://github.com/altivy-it/dokpod/issues/2)) ([8a8c01d](https://github.com/altivy-it/dokpod/commit/8a8c01de83ba882a202a3faa8012a39fd59fccbd))
* **agent:** kit de qualificação do agente Windows (spec 006) ([#43](https://github.com/altivy-it/dokpod/issues/43)) ([494408e](https://github.com/altivy-it/dokpod/commit/494408e89431aeabcc746a2b2f27d8cf5fe57192))
* **audit:** Governança → Auditoria (spec 017) ([#22](https://github.com/altivy-it/dokpod/issues/22)) ([32e9c0e](https://github.com/altivy-it/dokpod/commit/32e9c0e8e8ae82d5c56b0d68e9130916f33b6156))
* **configuracoes:** Governança → Configurações (spec 022) ([#27](https://github.com/altivy-it/dokpod/issues/27)) ([a6701c3](https://github.com/altivy-it/dokpod/commit/a6701c373b918a94943b78e14097963f054d5167))
* **console:** use o símbolo D na cor da marca, igual ao login ([#38](https://github.com/altivy-it/dokpod/issues/38)) ([97ee98d](https://github.com/altivy-it/dokpod/commit/97ee98d9bdfc7a55863985f5a3394353d79c816a))
* governança de alertas (spec 016) e consolidação dos PRs abertos ([#11](https://github.com/altivy-it/dokpod/issues/11)-[#20](https://github.com/altivy-it/dokpod/issues/20)) ([#21](https://github.com/altivy-it/dokpod/issues/21)) ([79c35d7](https://github.com/altivy-it/dokpod/commit/79c35d73df43c28e18a684068e35915890634221))
* **keycloak:** tema de login no design system do console ([#36](https://github.com/altivy-it/dokpod/issues/36)) ([7496a39](https://github.com/altivy-it/dokpod/commit/7496a39de4ab798e07d1d53b7c38c7a912b8e363))
* **pods:** Operação → Pods (spec 021) ([#26](https://github.com/altivy-it/dokpod/issues/26)) ([982448c](https://github.com/altivy-it/dokpod/commit/982448c6e5840792054c45fc352119dbda963e04))
* **qualificacao:** qualificação na stack real e 8 correções (spec 023) ([#29](https://github.com/altivy-it/dokpod/issues/29)) ([cd674fc](https://github.com/altivy-it/dokpod/commit/cd674fcb6d4ed53465a7e0c8068a8159bfe5e302))
* **redes:** Operação → Redes (spec 019) ([#24](https://github.com/altivy-it/dokpod/issues/24)) ([b567a23](https://github.com/altivy-it/dokpod/commit/b567a239bbb57489cdb412e397f8b60c1fcaccad))
* **registros:** Infraestrutura → Registros (spec 020) ([#25](https://github.com/altivy-it/dokpod/issues/25)) ([2867c25](https://github.com/altivy-it/dokpod/commit/2867c2598e88a6d05a0c399a9dd2d0f611879f58))
* **release:** qualificação de release Docker Linux (spec 005) — em andamento ([#30](https://github.com/altivy-it/dokpod/issues/30)) ([966a4ae](https://github.com/altivy-it/dokpod/commit/966a4ae13883244bf9b199ab4267a1bebe05badc))
* **volumes:** Operação → Volumes (spec 018) ([#23](https://github.com/altivy-it/dokpod/issues/23)) ([94ddc35](https://github.com/altivy-it/dokpod/commit/94ddc359a6f4d99dac2670af7c9438359bd92c70))


### Corrigido

* **agent:** calcule a CPU do container pelo tempo de parede ([#57](https://github.com/altivy-it/dokpod/issues/57)) ([d5dbcaf](https://github.com/altivy-it/dokpod/commit/d5dbcaf39e6bc01505fd046f490e13e7a35327f0))
* **api:** elimine 40001 da projeção e alertas enganosos da auditoria ([#53](https://github.com/altivy-it/dokpod/issues/53)) ([6c84722](https://github.com/altivy-it/dokpod/commit/6c84722db959d00e8b9b9b9cc9a23ed1c53573ee))
* **console:** faça o Sair encerrar a sessão e voltar à tela de entrada ([#37](https://github.com/altivy-it/dokpod/issues/37)) ([e5b7eba](https://github.com/altivy-it/dokpod/commit/e5b7eba504baa31ab048d8a925c6fe0663b52513))
* **deps:** bump @microsoft/signalr from 8.0.29 to 10.0.11 in /frontend/web in the npm-production group across 1 directory ([#45](https://github.com/altivy-it/dokpod/issues/45)) ([e51a966](https://github.com/altivy-it/dokpod/commit/e51a9666aee0660990fa6ba721e892eb34fc3276))
* **deps:** corrija js-yaml e fast-uri do console (Dependabot) ([#28](https://github.com/altivy-it/dokpod/issues/28)) ([d1e2780](https://github.com/altivy-it/dokpod/commit/d1e2780caa60336828a982c9622ea3576a5f09a5))
* **tools:** busque a policy UMA pelo nome exato no provisionamento ([#56](https://github.com/altivy-it/dokpod/issues/56)) ([52fdfc0](https://github.com/altivy-it/dokpod/commit/52fdfc0a9d9052888c1f72394cc95ee177fffc38))
* **web:** corrija os alertas do Dependabot do vite e do vitest ([#32](https://github.com/altivy-it/dokpod/issues/32)) ([07f6b19](https://github.com/altivy-it/dokpod/commit/07f6b198ee6f39dafb6f5d1d634b13b9de051dc3))

## Changelog
