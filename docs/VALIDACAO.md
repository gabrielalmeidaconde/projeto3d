# Validação do arsenal e do combate de estilo

Estado em 11/11/2026, Unity 6000.3.25f1.

| Verificação | Resultado |
| --- | --- |
| Base do projeto | Revisados a178377 e ce8970f; fundação anterior em eb333c3 |
| Dados do arsenal | 3 armas e 48 golpes configurados em assets |
| Edit Mode | 14/14 testes aprovados |
| Play Mode: última execução completa | 12/12 cenários aprovados |
| Cenário adicional de facas convergentes | Adicionado após essa execução; reteste pendente por falta de disco |
| Build Windows | Uma build do arsenal foi compilada e executada; a build final ficou bloqueada por disco cheio |
| Diagnóstico gráfico dessa build | TROIA_SMOKE PASS e TROIA_PRESENTATION PASS, arsenal=3, UI=SSS |

Os cenários aprovados cobrem pulo duplo e aterrissagem, rotas leve/pesado das três armas,
troca durante combo sem apagar o impacto, multihit sem multiplicar estilo, projéteis
bloqueados por paredes, aproximação terrestre/aérea, impossibilidade de sustentar voo
errando golpes, poderes em área sem pontuação artificial, treino e abertura/fechamento
da lista de golpes. Também cobrem a regressão de movimentação, esquiva, dano, cura,
estilo, seis deuses, IA, armadura e progressão pelos 20 andares.

A aproximação aérea foi corrigida após um teste detectar que um alvo em movimento
podia permanecer fora da distância exata de interceptação. O cenário passou após a
correção. A lista de golpes também foi corrigida para fechar ao retomar o jogo no
mesmo frame.

A última alteração de combate faz as duas facas convergirem no ponto de mira, mantendo
os dados da arma original quando o jogador troca de arma com os projéteis no ar.
O teste dedicado existe no projeto, mas ainda não foi possível executá-lo: a nova
tentativa de Play Mode ficou bloqueada na inicialização por armazenamento insuficiente.

Foram inspecionadas capturas reais da câmera com xifos, lança, lâminas gêmeas, ranking
SSS, lista de golpes e escolha de bênçãos. Elas revelaram sobreposição do HUD sob os
menus; o código agora oculta o HUD e adiciona escurecimento de fundo durante os menus.
As capturas finais desse ajuste ainda precisam ser regeneradas. O SSS do diagnóstico
usa acertos de teste para verificar o layout; não representa uma partida humana.

## O que falta validar

1. Liberar pelo menos 2 GB no disco do projeto.
2. Executar scripts/Test-Gameplay.ps1 -Build (14 testes Edit Mode + 13 Play Mode).
3. Executar a build de desenvolvimento com -troia-smoke e conferir as capturas finais.
4. Atualizar este relatório com os resultados e commitar a conclusão da validação.

O script verifica espaço antes de iniciar para evitar que o Editor fique aguardando
na inicialização. Durante esta sessão o C: chegou a zero bytes livres; o processamento
das DLLs falhou com System.IO.IOException: espaço insuficiente no disco. A exclusão
recursiva do cache de compilação foi bloqueada pela política de execução, e não foi
realizada. Não foram apagados código, assets ou arquivos de outros aplicativos.

Relatórios locais em Logs/editmode-results.xml, Logs/playmode-results.xml,
Logs/build.log e Logs/player-smoke.log. Capturas em Capturas/; a pasta
Builds/Windows/ contém a última build bem-sucedida, anterior aos ajustes finais.
Essas pastas são ignoradas pelo Git. O XML de Play Mode registra a última execução
completa de 12 cenários, não a tentativa interrompida por falta de espaço.

A progressão automatizada elimina inimigos diretamente para verificar os 20 andares;
não substitui uma partida humana de balanceamento. Apresentação do jogador, armas,
animações e áudio são procedurais; modelos/clips finais e o acabamento de Heitor
continuam substituíveis por assets de produção.
