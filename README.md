# Troia — Hack and Slash

Unity 6 (6000.3.25f1) · URP · Input System · AI Navigation

## Fase 1 — A Noite da Invasão (mapa)

Modo de arenas com ondas: cada praça da cidade é uma arena que fica trancada por
barreiras de fogo até o jogador derrotar todas as ondas; as ruas entre elas são
corredores de transição.

| # | Arena | Ondas | Destaque |
|---|-------|-------|----------|
| 1 | Ágora (o Cavalo de Troia) | 3 | início: o herói desce do Cavalo |
| 2 | Mercado | 3 | arena apertada, barracas e carroças |
| 3 | Portas Ceias | 4 | ao vencer, o portão abre para o exército grego |
| 4 | Praça da Fonte | 4 | fonte central divide a arena |
| — | Avenida e Rampa do Pérgamo | — | transição até a cidadela |
| 5 | Pátio da Cidadela (Templo de Atena) | 5 | maior arena |
| 6 | Altar de Zeus (Palácio de Príamo) | chefe | fim da fase no mégaron |

- **Cena:** `Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity`
- **Regerar o mapa:** menu `Troia > Gerar Mapa - Fase 1`. Isso recria a cena e tudo em
  `Assets/Troia/Gerado/` (edições manuais nesses arquivos são perdidas). O resultado é
  determinístico: a mesma semente gera o mesmo mapa.
- **Prévias:** `Troia > Capturar Prévias do Mapa` salva PNGs em `Capturas/`.
- **Marcadores do mapa:** o objeto `--- GAMEPLAY ---` contém os marcadores
  `LevelMarker` (arenas com número de ondas, spawns de inimigos, barreiras de
  entrada/saída, objetivos e checkpoints) e o `NavMesh_Superficie` já gerado.
  O sistema de gameplay ativa colisores, navegação e muralhas de fogo durante o combate.

### Visual (sujeira, decals, texturas)

- Todos os materiais das construções usam o shader **`Troia/Lit Sujo`** (cópia do Lit do URP 17.3 com
  sujeira em espaço de mundo: lama na base das paredes, escorridos verticais e variação de tom que quebra a
  repetição). A quantidade por material fica em `_Parallax` (0–1).
- Ajuste fino ao vivo no objeto **`Sujeira_Global (shader)`** da cena (força, altura da lama, escorridos, cor).
- Decals do URP (fuligem, queimado, lama, sangue, cinzas, rachaduras) ficam em `--- EFEITOS --- > Decals`.
- Texturas principais em 2048 (JPG); se o URP for atualizado, recopie o `Lit.shader` do pacote para
  `Assets/Troia/Shaders/TroiaLitSujo.shader` mantendo o fragmento `TroiaLitPassFragment`.

### Jogar: arsenal e combos

Abra a cena e dê Play. O bootstrap substitui o controle temporário por movimentação e
combate; também funciona após regerar o mapa.

- **WASD** mover, **Shift** correr, **Espaço** pular (novamente no ar: pulo duplo), **Ctrl** esquivar/cancelar golpe.
- **Mouse** câmera, **Scroll** zoom, **clique esquerdo/J** leve, **clique direito/K** pesado.
- **1/2/3** trocam entre espada, foice e machado, inclusive durante sequências.
- **F/clique do meio** fixa alvo, **T** troca alvo, **E** avança com espada/foice ou puxa com machado.
- **R** dispara pistolas duplas; três rajadas exigem 0,95 s de recuperação compartilhada.
- Segure pesado para lançar; pule e pressione **E** para perseguir, ou pesado no ar para cair em área.
- **LLL** (machado: **LL**), **LP**, **PP** e **LL (pausa) LL** têm rotas próprias por arma.
- **Q** poder divino, **Tab** trocar deus, **Esc** pausa, **H** lista de golpes, **F2** treino livre.
- Controle: analógico esquerdo mover, direito câmera, **A** pulo, **B** esquiva,
  **X/Y** leve/pesado, **LT** foco, **RB** aproximação, **RT** projéteis,
  **LB** poder, direcional esquerdo/direito armas, direcional baixo trocar deus, **Start** pausa.

O HUD destaca **D → C → B → A → S → SS → SSS**, progresso, hits e dano da sequência.
Variar arma e acertar no ar rende mais estilo. Repetir o mesmo golpe deixa de pontuar;
atingir vários alvos ou dar vários impactos com a mesma ação não multiplica a nota.
Tomar dano derruba dois níveis. Trocar de arma sem acertar não gera estilo.
Pesados, lançadores e quedas têm recargas próprias; esquiva, salto e troca preservam
essas recargas e a recuperação ofensiva. O HUD mostra recuperação, pesado e habilidade E.
O combate aceita entradas antecipadas, orientação pelo movimento, aproximação curta
na preparação e cancelamento da recuperação para andar. Ataque e troca podem ser
enfileirados durante E. **C** executa o lançador diretamente. Esquivas no instante
de um ataque real geram estilo e favor; os inimigos sinalizam golpes e limitam
preparações simultâneas a dois atacantes.
O herói usa uma estilização mais sombria: rosto anguloso, olhos estreitos vermelhos,
cicatriz, cabelo cinza prateado, casaco vinho e couro preto, com proporções compactas.

São 20 andares nas seis arenas. O timer global começa em 180 segundos; acertos variados
aumentam o estilo e kills recuperam tempo/favor. Bênçãos e checkpoints após 5/10/15;
capitão no andar 10 e Heitor no 20.

Balanceamento em `Assets/Troia/Resources/Troia/Gameplay.asset`, com combos, andares e
bênçãos e armas separados. Menu `Troia > Gameplay > Criar configuracao e combos` cria os padrões
e migra configurações antigas para o arsenal. Os movimentos de combate são procedurais; integração de
animações e verificações em [docs/JOGABILIDADE.md](docs/JOGABILIDADE.md).

## Código

- `Assets/Troia/Scripts/Editor/` — gerador do mapa
  - `TroiaLayout.cs` — planta da cidade: arenas, ruas, áreas reservadas, relevo
  - `TroiaMapBuilder.cs` — monta a cena (terreno, áreas, casas, luz, marcadores, NavMesh)
  - `TroiaMeshes.cs` — geometria das construções e adereços
  - `TroiaAssets.cs` / `TexGen.cs` — texturas, materiais, céu, fogo e prefabs
  - `MeshBuilder.cs` — utilitário de malhas procedurais
- `Assets/Troia/Scripts/Runtime/` — combate, movimentação, inimigos, andares, bênçãos, HUD e componentes do mapa
- `Assets/Troia/Tests/` — testes Edit Mode e Play Mode
- `Assets/Troia/Shaders/` — `TroiaLitSujo.shader` e `TroiaGrime.hlsl`
