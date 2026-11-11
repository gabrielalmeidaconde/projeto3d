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
- **Gameplay (ainda sem lógica):** o objeto `--- GAMEPLAY ---` contém os marcadores
  `LevelMarker` (arenas com número de ondas, spawns de inimigos, barreiras de
  entrada/saída, objetivos e checkpoints) e o `NavMesh_Superficie` já gerado.
  As barreiras têm colisor e muralha de fogo desligados; o sistema de ondas deve ligá-los.

### Explorar o mapa (controle temporário)

Dê Play na cena: `WASD` mover · `Shift` correr · `Espaço` pular · `F` voo livre
(`Espaço`/`Ctrl` sobe/desce) · `Scroll` zoom · `Esc` solta o mouse · `H` esconde a ajuda.

## Código

- `Assets/Troia/Scripts/Editor/` — gerador do mapa
  - `TroiaLayout.cs` — planta da cidade: arenas, ruas, áreas reservadas, relevo
  - `TroiaMapBuilder.cs` — monta a cena (terreno, áreas, casas, luz, marcadores, NavMesh)
  - `TroiaMeshes.cs` — geometria das construções e adereços
  - `TroiaAssets.cs` / `TexGen.cs` — texturas, materiais, céu, fogo e prefabs
  - `MeshBuilder.cs` — utilitário de malhas procedurais
- `Assets/Troia/Scripts/Runtime/` — `FireFlicker`, `LevelMarker`, `MapPreviewWalker`
