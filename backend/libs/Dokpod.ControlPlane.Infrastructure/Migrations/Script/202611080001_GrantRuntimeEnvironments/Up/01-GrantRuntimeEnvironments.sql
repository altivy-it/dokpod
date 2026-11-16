-- Spec 033 T016 (D-009): o grupo `dokpod_runtime` não tinha acesso a `environments` nem a `agent_identities`, o que só
-- não aparecia porque o laboratório e o CI conectavam com uma role administrativa. Concede o mínimo que o runtime usa:
-- cadastro e troca de estágio dos ambientes, e consulta e revogação da identidade do agente. O registro da identidade
-- fica com o modo administrativo, que roda com a role dona.
GRANT SELECT, INSERT, UPDATE ON dokpod.environments TO dokpod_runtime;
GRANT SELECT, UPDATE ON dokpod.agent_identities TO dokpod_runtime;
