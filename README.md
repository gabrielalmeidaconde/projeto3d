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

### Jogar (plano v2)

Abra a cena e dê Play. O bootstrap substitui o controle temporário por movimentação e
combate; também funciona após regerar o mapa.

- **WASD** mover, **Shift** correr, **Espaço** pular, **Ctrl** esquivar/cancelar golpe.
- **Mouse** câmera, **Scroll** zoom, **clique esquerdo/J** leve, **clique direito/K** pesado.
- Segure pesado para lançar; pule para perseguir e use **LLL** no ar ou pesado para cair em área.
- **LLL** corte triplo; **LL (pausa) LL** estocadas. A pausa acontece depois do segundo golpe.
- **Q** poder divino, **Tab** trocar deus, **1/2/3** escolher bênção, **Esc** pausa, **H** ajuda.
- Controle: analógico esquerdo mover, direito câmera, **A** pulo, **B** esquiva,
  **X/Y** leve/pesado, **RB** poder, direcional direito trocar deus, **Start** pausa.

São 20 andares nas seis arenas. O timer global começa em 180 segundos; acertos variados
aumentam o estilo e kills recuperam tempo/favor. Bênçãos e checkpoints após 5/10/15;
capitão no andar 10 e Heitor no 20.

Balanceamento em `Assets/Troia/Resources/Troia/Gameplay.asset`, com combos, andares e
bênçãos separados. Menu `Troia > Gameplay > Criar configuracao e combos` cria os padrões
se ainda não existirem. Os movimentos de combate são procedurais; integração de
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
