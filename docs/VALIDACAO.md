# Validação do arsenal e da build Windows

Concluída em 11/11/2026, Unity 6000.3.25f1.

| Verificação | Resultado |
| --- | --- |
| Dados do arsenal | 3 armas e 48 golpes configurados em assets |
| Edit Mode | 14/14 testes aprovados |
| Play Mode | 13/13 cenários aprovados |
| Facas convergentes | As duas atingem o alvo distante, preservam a arma de origem após a troca e creditam uma ação de estilo |
| Build Windows 64 bits | Build Finished, Result: Success; código de saída 0 |
| Executável com API gráfica padrão | Direct3D 12 na RTX 3050; código de saída 0 |
| Diagnóstico funcional | TROIA_SMOKE PASS; primeiro andar ativo, dois inimigos, jogador no chão e vida 120 |
| Diagnóstico de apresentação | TROIA_PRESENTATION PASS; três armas e HUD em SSS |

Os 27 testes cobrem pulo duplo e aterrissagem, rotas leve/pesado das três armas,
troca durante combo sem apagar o impacto, multihit sem multiplicar estilo, projéteis
bloqueados por paredes, aproximação terrestre/aérea, impossibilidade de sustentar voo
errando golpes, poderes em área sem pontuação artificial, treino com os seis deuses,
abertura/fechamento da lista de golpes e a regressão de movimentação, esquiva, dano,
cura, estilo, IA, armadura e progressão pelos 20 andares.

Foram corrigidas e verificadas a interceptação de alvos aéreos em movimento, a retomada
do jogo após abrir a lista de golpes e a convergência das facas a longa distância.
Os bloqueios anteriores de armazenamento foram resolvidos antes da execução final.

## Entrega para teste

Execute Builds/Windows/Iliada.exe mantendo a pasta inteira junto do executável.
Ela inclui Iliada_Data, MonoBleedingEdge e as bibliotecas necessárias; não é preciso
abrir o Unity. Builds/Windows/LEIA-ME.txt traz os controles e atalhos de treino.

F2 abre o treino livre; H mostra a lista de golpes; Esc abre a pausa.
O código e os dados do arsenal estão no commit 230d4b8, sobre a fundação eb333c3.

A build foi executada com -troia-smoke usando a API gráfica padrão. O diagnóstico
encerra sozinho; ao abrir Iliada.exe normalmente esse diagnóstico não é ativado.

## Verificação visual

Capturas reais da câmera foram inspecionadas para xifos, lança, lâminas gêmeas,
ranking SSS, lista de golpes, pausa e escolha de bênçãos. Os menus agora ocultam
o HUD e escurecem o cenário, sem a sobreposição encontrada na primeira inspeção.
O diagnóstico aguarda a atualização da interface entre reiniciar e abrir a pausa,
e verifica que a lista de golpes foi fechada antes de capturar essa tela.

Arquivos locais em Capturas/:
- gameplay-camera.png
- arma-Xifos.png
- arma-Dory.png
- arma-Gemeas.png
- estilo-SSS.png
- lista-de-golpes.png
- pausa.png
- bencaos.png

O SSS do diagnóstico usa uma sequência de acertos de teste para conferir o layout;
não representa uma partida humana. O HUD uGUI é capturado junto da câmera, inclusive
na execução oculta. A progressão automatizada elimina inimigos diretamente para
verificar os 20 andares; não substitui uma sessão humana de balanceamento ou uma
avaliação manual dos controles físicos.

## Evidências e limites

Relatórios em Logs/editmode-results.xml, Logs/playmode-results.xml, Logs/build.log
e Logs/player-smoke.log. Build, logs e capturas permanecem nas pastas ignoradas pelo Git.

O URP reduz automaticamente a resolução de sombras das muitas luzes do mapa para
caber no atlas. Isso foi registrado no diagnóstico, sem impedir a execução nem
as capturas. Personagens, armas, animações e áudio continuam procedurais; modelos,
clips e acabamento de produção podem substituir essa apresentação.
