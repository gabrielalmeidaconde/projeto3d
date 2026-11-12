# Validação do combate v3 e da build Windows

Revisão visual de 12/11/2026, Unity 6000.3.25f1. Configuração de combate na versão 3.

| Verificação | Resultado |
| --- | --- |
| Arsenal ativo | Espada, foice e machado; 46 golpes: 16 / 15 / 15 |
| Edit Mode | 15/15 testes aprovados |
| Play Mode | 18/18 cenários aprovados |
| Build Windows 64 bits | BuildPipeline concluído com sucesso |
| Executável com gráficos | Direct3D 12; saída 0 |
| Diagnóstico funcional | TROIA_SMOKE PASS; andar 1 ativo, dois inimigos, jogador no chão e vida 120 |
| Diagnóstico visual | TROIA_PRESENTATION PASS; arsenal e HUD em SSS |

## Cobertura

O herói foi refeito em estilo chibi e recebe um shader cartoon próprio. A inspeção
do executável inclui a cabeça maior, os olhos, o cabelo, as formas arredondadas e as
três armas nos pontos de empunhadura ajustados. As regras de combate foram preservadas.
O diagnóstico aguarda o primeiro andar ativo, inimigos presentes e jogador no chão,
com limite de 60 segundos, evitando falsos negativos por um atraso fixo sob carga.

Os testes verificam os grafos persistidos, janelas válidas, rotas com pausa e pesado,
pulo duplo, aterrissagem, juggle confirmado e queda. A troca de arma aguarda o impacto
e a recuperação; saltar, esquivar ou trocar não apaga recargas ofensivas.

O puxão do machado move o inimigo sem deslocar o jogador, exige visão, respeita paredes,
não gera estilo sozinho e rejeita blindagem intacta. Quebrar a guarda abre a oportunidade.
As pistolas convergem no alvo, param em paredes e compartilham três rajadas e a
recuperação de 0,95 s entre todas as armas. Multihit e duas balas creditam uma ação de estilo.

O cenário adicional verifica que uma entrada expira após os 0,2 s do buffer,
sem se renovar automaticamente durante uma recuperação mais longa.

Há cenários de dano real para verificar o bônus de trocar a arma que acerta e a perda
de dois níveis ao sofrer dano. Apenas equipar outra arma não pontua. As regressões
incluem treino, os seis deuses, IA, armadura, movimentação, cura, pausa, lista de golpes,
progressão pelos 20 andares, checkpoints e chefe.

O pesado básico do machado foi reduzido de 88,4 para 58 durante a calibração desta
entrega, com preparação de 0,4 s, duração de 1,1 s, recuperação protegida de 0,18 s e
recarga adicional de 1 s. Os testes foram repetidos após essa calibração.

## Entrega e inspeção visual

Abra `Builds/Windows/Iliada.exe` mantendo a pasta inteira junto dele. Não requer Unity.
`LEIA-ME.txt` contém os controles; F2 inicia treino livre e H mostra a lista de golpes.

Capturas do executável em `Capturas/`: `personagem-frente.png`, `arma-Xifos.png`,
`arma-Foice.png`, `arma-Machado.png`, `gameplay-camera.png`, `estilo-SSS.png`,
`lista-de-golpes.png`, `pausa.png` e `bencaos.png`.

A câmera renderiza o personagem e o HUD reais. Foram inspecionados o casaco, cabelo,
silhuetas das armas, informações de recuperação e as telas de menus. Os menus ocultam
o HUD de combate. O diagnóstico só é ativado pelo argumento `-troia-smoke`.

## Evidências e limites

Relatórios: `Logs/editmode-results.xml`, `Logs/playmode-results.xml`, `Logs/build.log`
e `Logs/player-smoke.log`. Builds, relatórios e capturas são artefatos locais ignorados pelo Git.

O SSS visual usa acertos sintéticos para verificar o layout. A progressão automatizada
elimina inimigos diretamente. Isso valida regras e integração, mas a sensação dos
controles e o balanceamento fino ainda precisam de uma sessão humana de jogo.

O herói, as armas e as animações são procedurais; esta entrega remodela a apresentação
com malhas próprias, sem importar um personagem licenciado. O mapa continua com o aviso
do URP de redução de resolução das sombras para acomodar suas muitas luzes.
