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

### Visual (sujeira, decals, texturas)

- Todos os materiais das construções usam o shader **`Troia/Lit Sujo`** (cópia do Lit do URP 17.3 com
  sujeira em espaço de mundo: lama na base das paredes, escorridos verticais e variação de tom que quebra a
  repetição). A quantidade por material fica em `_Parallax` (0–1).
- Ajuste fino ao vivo no objeto **`Sujeira_Global (shader)`** da cena (força, altura da lama, escorridos, cor).
- Decals do URP (fuligem, queimado, lama, sangue, cinzas, rachaduras) ficam em `--- EFEITOS --- > Decals`.
- Texturas principais em 2048 (JPG); se o URP for atualizado, recopie o `Lit.shader` do pacote para
  `Assets/Troia/Shaders/TroiaLitSujo.shader` mantendo o fragmento `TroiaLitPassFragment`.

### Explorar o mapa (controle temporário)

Dê Play na cena: `WASD` mover · `Shift` correr · `Espaço` pular · `F` voo livre
(`Espaço`/`Ctrl` sobe/desce) · `Scroll` zoom · `Esc` solta o mouse · `H` esconde a ajuda.

## Personagem: Pátroclo

- **Abrir:** `Assets/Troia/Personagens/Patroclo/Patroclo_Vitrine.unity` (vitrine noturna com braseiros,
  pedestal de pedra escaneada, reflexos de um HDRI de estúdio e pós-processamento próprio).
- **Prefab:** `Assets/Troia/Personagens/Patroclo/Patroclo.prefab` (1,83 m; com a crista passa de 2 m).
- **Corpo:** humano realista montado a partir dos assets CC0 do MakeHuman (malha, esqueleto de 163 ossos
  com dedos, olhos, sobrancelhas, cílios e cabelo), com físico de estátua grega: ombros largos, tronco em V
  e músculos limpos. Malha subdividida (~56 mil vértices) e normal map gerado por código (abdômen, veias,
  rugas de expressão discretas); barba por fazer e linha do cabelo pintadas fio a fio. A pele usa o shader
  **Troia/Pele**, com espalhamento subsuperficial (luz avermelhada na transição para a sombra e
  translucidez no contorno), que tira o aspecto de cera.
- **Elmo coríntio** medido no crânio: cobre a cabeça, as orelhas e as bochechas, com olhos amendoados,
  protetor de nariz e fenda da boca recortados a partir da posição real do rosto, sobrancelhas gravadas
  em ouro, volutas de ouro nas têmporas e crista alta de crina.
- **Traje dos Mirmidões:** corselete de linho (linotórax) cor de osso com barras carmesim, vivo de ouro
  nas costuras e meandro grego de ouro; saiote de escamas de bronze fechado por um cinturão com cravos e
  fivela; epômides (abas de ombro) amarradas no peito com argolas de ouro; disco de bronze abaulado no
  peito com a formiga dos Mirmidões; lâminas de bronze só no braço do escudo; braçadeiras com placa de
  bronze e amarração em ziguezague; cnêmides com nervura central, joelheira e correias com fivela; saia de
  quíton com pregas e barra carmesim; duas camadas de ptéruges de couro com cravos; faixa carmesim e capa longa.
- **Camadas sem interseção:** as peças são montadas em camadas que não se cruzam:
  - as barras coloridas são partes complementares da mesma casca (não chapas sobrepostas), e as epômides
    e o cinturão são recortes do próprio corselete afastados dele, então ficam sempre paralelos;
  - escamas, cravos e fivelas são assentados por raios lançados contra a superfície;
  - saia, ptéruges, faixa e capa usam os pesos da pele mais próxima, então acompanham as pernas nas poses;
  - com a armadura ligada, o corpo usa uma malha sem os polígonos escondidos sob a roupa (a pele não
    tem como vazar pelo tecido); com ela desligada, entram o corpo inteiro e uma saia com o cós justo no quadril;
  - o corselete é rígido: passa em ponte sobre o sulco da coluna e termina numa barra reta, de onde saem as tiras.
- **Acabamento:** os recortes das cascas são refinados por bisseção (bordas lisas), as bordas enroladas
  são reamostradas por comprimento de arco e a oclusão de ambiente é assada por vértice (sombras de
  contato entre as camadas), lida pelos shaders **Troia/Personagem** e **Troia/Pele**.
- **Desgaste de batalha:** o herói aparece no meio da luta, não saído da oficina:
  - sujeira: lama e poeira subindo dos pés (cnêmides, barra da capa e da saia), pó nas frestas da armadura,
    manchas e escorridos no linho e na pele; sangue seco em respingos, mais forte na ponta da lâmina;
  - rasgos: capa com a barra esfarrapada, fendas, buracos e um canto arrancado; bainha da saia rasgada;
    uma das pontas da faixa rasgada; tiras de couro de comprimentos desiguais, algumas cortadas;
  - marcas de combate: três cortes de lâmina no linho do corselete, sulcos de golpes no bronze e três
    flechas quebradas cravadas no escudo.
  A sujeira e o sangue são dois controles do painel. Funcionam com máscaras gravadas na cor dos vértices
  (G = sujeira, B = regiões de sangue) e uma textura repetível (`Texturas/Desgaste/sujeira.png`), lidas
  pelos shaders dos personagens. Os rasgos são recortes na malha do pano (`CapeTear` e `SkirtTear` em
  `PatrocloFit.cs`).
- **Espada:** lâmina em folha de ~1 m (estilo das espadas da Idade do Bronze, ~1,4 m no total), nervura
  com filete de ouro, meandro gravado, guarda em chifres com volutas e empunhadura de duas mãos.
  Na pose Neutra ela descansa no ombro direito.
- **Adaga de cintura:** no quadril direito, do lado oposto ao da faixa: bainha de couro com boca e
  ponteira de bronze, presa por uma correia na barra do corselete. O escudo ganhou rebites de ouro em volta.
- **Materiais (4K):** bronze martelado e polido gerado por código (marcas de martelo, riscos de polimento,
  manchas de mão), couro, linho, lã e pedra escaneados do Poly Haven (CC0). Pele em 4K com poros,
  vermelhidão nas extremidades, lábios, veias e mapa de brilho (zona T e lábios brilham, barba é fosca).
- **Capturas:** 18 vistas em 1600x2000 (frente, costas com e sem capa, perfil, closes, combate, sem armadura,
  capa rasgada inteira, adaga e cintura), renderizadas em 3200x4000 com MSAA 4x e reduzidas (supersampling).
- **Mexer no design:** selecione `Patroclo` e use o componente **Patroclo Design** no Inspector:
  - paleta: tinge as texturas;
  - armadura (desligue para ver o corpo inteiro e a musculatura), elmo, crista, capa, e escudo nas costas ou no braço;
  - desgaste de batalha: sujeira e sangue, de 0 (limpo) a 1;
  - pose: Repouso (A), Neutra, Combate ou Livre;
  - prévias de favor divino, nota de estilo (D a SSS) e bênção de cada deus.
- **Posar à mão:** escolha a pose **Livre** (o painel deixa de mexer nos ossos) e gire os ossos do
  MakeHuman (`upperarm01.R`, `lowerarm01.R`, `upperleg01.L`, `spine03`, `head`...). No repouso todos
  têm rotação zero e eixos iguais aos do mundo: X é a direita do personagem, Y é para cima e Z é para a frente.
- **Regerar:** menu `Troia > Personagem > Gerar Pátroclo` (recria malhas, materiais e o prefab).
- **Fontes e ferramentas** (Python 3 com numpy, scipy e Pillow), em `Ferramentas/`. Rode com a pasta
  baixada do MakeHuman (`3dobjs`, `targets`, `rigs`, `pack`):
  - `makehuman/mh_build.py <dl> Assets/.../Fonte/patroclo_mh.json 1.83`: alvos de forma do corpo e do rosto
    (listas `MACRO`, `BODY` e `FACE` no topo), realce muscular, subdivisão e peças ajustadas;
  - `makehuman/mh_skin.py <dl> Texturas/MakeHuman/pele_diffuse.png Texturas/MakeHuman`: gera
    `pele_normal.png` e `pele_albedo.png` (detalhes de `mh_detail.py`, barba, couro cabeludo, cavidade);
  - `makehuman/mh_pelos.py <dl>/pack Texturas/MakeHuman`: copia e colore cabelo, sobrancelhas e cílios;
  - `makehuman/mh_preview.py <dl> saida.png`: prévia rápida do corpo e do rosto sem abrir o Unity;
  - `texturas/prep_polyhaven.py <downloads> Texturas/PolyHaven`: prepara as texturas escaneadas em 4K;
  - `texturas/bronze_martelado.py Texturas/PolyHaven`: gera o bronze (mude `PATINA` para envelhecer);
  - `texturas/sujeira.py Texturas/Desgaste/sujeira.png`: gera a textura de sujeira (manchas, escorridos,
    respingos e poeira fina, um por canal).

  As licenças estão em `Fonte/LICENCAS.txt` e `Texturas/PolyHaven/LICENCAS.txt`.

## Código

- `Assets/Troia/Scripts/Editor/` — gerador do mapa
  - `TroiaLayout.cs` — planta da cidade: arenas, ruas, áreas reservadas, relevo
  - `TroiaMapBuilder.cs` — monta a cena (terreno, áreas, casas, luz, marcadores, NavMesh)
  - `TroiaMeshes.cs` — geometria das construções e adereços
  - `TroiaAssets.cs` / `TexGen.cs` — texturas, materiais, céu, fogo e prefabs
  - `MeshBuilder.cs` — utilitário de malhas procedurais
  - `MakeHumanImporter.cs`: carrega o corpo do MakeHuman (JSON) com esqueleto e skinning.
  - `PatrocloArmor.cs`: cascas recortadas do corpo, com suavização, subdivisão, deslocamento, bordas,
    interseção por raio e skinning.
  - `PatrocloFit.cs`: cada peça da armadura e da roupa, ajustada ao corpo.
  - `PatrocloAO.cs`: oclusão de ambiente e máscaras de sujeira e sangue assadas na cor dos vértices.
  - `PatrocloMeshes.cs`: espada, adaga, aspis, emblema e marcas.
  - `PatrocloBuilder.cs`: materiais, montagem, vitrine e capturas.
- `Assets/Troia/Scripts/Runtime/` — `FireFlicker`, `LevelMarker`, `MapPreviewWalker`, `TroiaShaderGlobals`, `PatrocloDesign`
- `Assets/Troia/Shaders/` — `TroiaLitSujo.shader` e `TroiaGrime.hlsl` (sujeira do mapa); `TroiaPersonagem.shader`,
  `TroiaPele.shader`, `TroiaCharForwardPass.hlsl` e `TroiaSkin.hlsl` (personagens: oclusão por vértice e pele)
