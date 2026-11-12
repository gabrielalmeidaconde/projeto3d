# Validação do combate v4

12/11/2026, Unity 6000.3.25f1. Configuração persistida na versão 4.

| Verificação | Resultado |
| --- | --- |
| Arsenal ativo | Espada, foice e machado; 46 golpes |
| Edit Mode | 16/16 aprovados |
| Play Mode | 27/27 aprovados |
| Build Windows 64 bits | BuildPipeline concluído com sucesso |
| Executável Direct3D 12 | Saída 0; TROIA_SMOKE e TROIA_PRESENTATION PASS |
| Combate real renderizado | TROIA_FLOW PASS: 7 acertos, 124,5 de dano, troca para foice |

Os 43 testes incluem regressões da progressão pelos 20 andares, física, poderes,
estilo, armas, projéteis, interface e os seguintes cenários novos:

- Entrada antecipada consumida na próxima janela de combo.
- Finalizador preserva reinício neutro sem inventar a variação com pausa.
- Movimento escolhe a direção do ataque; preparação fecha pequenas distâncias.
- Aproximação preserva ataque e troca de arma enfileirados.
- Movimento cancela a recuperação após a janela protegida.
- Esquiva perfeita exige ameaça e pontua uma vez, incluindo flechas.
- Grupos respeitam o máximo de dois inimigos preparando ataques.
- Migração de balanceamento é idempotente e não multiplica dano.

## Executável e inspeção visual

Abra `Builds/Windows/Iliada.exe`, mantendo a pasta inteira junto dele. F2 inicia treino
livre, H mostra os golpes, C lança, E avança ou puxa conforme a arma e Ctrl esquiva.

O argumento `-troia-smoke` ativa um diagnóstico que aguarda andar ativo, inimigos e
jogador no chão, com limite de 60 segundos. A sequência de combate aproxima, encadeia
leves, troca para foice e usa uma ramificação pesada contra alvos reais de treino.
`TROIA_FLOW` exige pelo menos três acertos e mais de 40 de dano, antes de qualquer
fixture de ranking. O código de saída só é zero se os critérios forem satisfeitos.

Capturas em `Capturas/`: `personagem-frente.png`, `combate-fluido.png`, `arma-Xifos.png`,
`arma-Foice.png`, `arma-Machado.png`, `estilo-SSS.png`, `lista-de-golpes.png`, `pausa.png`
e `bencaos.png`. O rosto, a paleta escura e as armas são inspecionados nas imagens
renderizadas do executável, junto do HUD real.

## Evidências e limites

Relatórios em `Logs/editmode-results.xml`, `Logs/playmode-results.xml`, `Logs/build.log`
e `Logs/player-smoke.log`. Build, logs e capturas são artefatos locais ignorados pelo Git.
As fontes da pesquisa e as decisões estão em [JOGABILIDADE.md](JOGABILIDADE.md).

A captura SSS usa acertos sintéticos apenas para verificar o layout e é separada do
teste de dano real. A progressão automatizada elimina inimigos diretamente para
verificar estados. Os testes não substituem uma sessão humana para julgar sensação
dos controles, dificuldade e balanceamento fino. Personagens e animações continuam
procedurais, sem clips de combate de produção.
