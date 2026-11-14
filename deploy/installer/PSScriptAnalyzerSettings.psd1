# Análise estática dos módulos do instalador (spec 033 T004 e T057). Regras padrão do PSScriptAnalyzer 1.25.0, com as
# exclusões abaixo, cada uma por uma convenção do projeto ou um desenho registrado na spec:
# - PSUseBOMForUnicodeEncodedFile: os scripts do repositório são UTF-8 sem BOM (nota da T004).
# - PSUseSingularNouns: os nomes seguem o domínio (Get-ServiceCertificateNames devolve uma lista de nomes, Initialize-
#   PostgresSecrets cria vários segredos); renomear mudaria a API dos módulos sem ganho.
# - PSUseShouldProcessForStateChangingFunctions: a confirmação do instalador é o plano (FR-004), com --yes e as
#   confirmações nomeadas (FR-029, FR-036, FR-041), não o -WhatIf/-Confirm de cada função interna.
# - PSAvoidUsingPlainTextForPassword, PSAvoidUsingUsernameAndPasswordParams e
#   PSAvoidUsingConvertToSecureStringWithPlainText: as senhas transitam como texto por desenho, de arquivos 0600 para
#   o tar do stdin de containers avulsos (FR-033, L5); SecureString não as protege no Linux, e o configure-keycloak.ps1
#   só aceita SecureString no -AdminPassword.
@{
    Severity     = @('Error', 'Warning')
    ExcludeRules = @(
        'PSUseBOMForUnicodeEncodedFile',
        'PSUseSingularNouns',
        'PSUseShouldProcessForStateChangingFunctions',
        'PSAvoidUsingPlainTextForPassword',
        'PSAvoidUsingUsernameAndPasswordParams',
        'PSAvoidUsingConvertToSecureStringWithPlainText'
    )
}
