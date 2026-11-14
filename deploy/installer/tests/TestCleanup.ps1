# Limpeza dos projetos compose dos testes (spec 033). Só aceita nome de projeto de teste (dokpod-tNNN[-sufixo]): um
# nome vazio viraria o filtro `label=com.docker.compose.project=`, que casa com todo container compose do host, e já
# removeu stacks alheias aos testes (2026-11-14). Nunca limpe por rótulo sem passar por aqui.
function Remove-TestComposeProject {
    param([string] $Project, [string] $ComposeFile)

    if ($Project -notmatch '^dokpod-t\d{3}(-[a-z0-9-]+)?$') {
        throw "Limpeza recusada: '$Project' não é um projeto de teste (dokpod-tNNN). Nada foi removido."
    }

    if ($ComposeFile -and (Test-Path -LiteralPath $ComposeFile)) {
        & docker compose --file $ComposeFile --project-name $Project down --volumes 2>$null | Out-Null
    }
    $label = "label=com.docker.compose.project=$Project"
    @(& docker ps -aq --filter $label) | Where-Object { $_ } | ForEach-Object { & docker rm -f $_ 2>$null | Out-Null }
    @(& docker volume ls -q --filter $label) | Where-Object { $_ } | ForEach-Object { & docker volume rm $_ 2>$null | Out-Null }
    @(& docker network ls -q --filter $label) | Where-Object { $_ } | ForEach-Object { & docker network rm $_ 2>$null | Out-Null }
}
