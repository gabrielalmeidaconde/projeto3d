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
  protetor de nariz e fenda da boca recortados a partir da posição real do rosto, filete e volutas de ouro
  nas têmporas. O bronze é trabalhado: sobrancelhas em relevo, palmeta gravada na testa, volutas nas faces e
  friso de dois filetes com pontos puncionados em toda beira. A **crista** é de crina em mechas (cartões
  finos com textura de fios e transparência), presa numa régua de bronze com cravos de ouro, mais alta no
  topo e deitada para trás, e continua num rabo comprido que cai pelas costas.
- **Traje dos Mirmidões:** corselete de linho (linotórax) cor de osso com barras carmesim, vivo de ouro
  nas costuras e meandro grego de ouro; saiote de escamas de bronze fechado por um cinturão com cravos e
  fivela; epômides (abas de ombro) amarradas no peito com argolas de ouro; disco de bronze abaulado no
  peito com a formiga dos Mirmidões; lâminas de bronze só no braço do escudo; braçadeiras com placa de
  bronze e amarração em ziguezague; cnêmides com nervura central, joelheira e correias com fivela; saia de
  quíton com pregas e barra carmesim; duas camadas de ptéruges de couro com cravos e faixa carmesim.
  No linho do peito, uma estrela de dezesseis pontas pintada em carmesim em volta do disco (na Ilíada a
  couraça que Pátroclo veste é "estrelada"), dente-de-lobo na cintura, filete nas costuras e estrelas pequenas
  nas abas dos ombros.
- **Capa de lã simulada:** o pano é simulado no Blender. Um molde em leque (estreito nos ombros, largo na
  barra) fica preso por dois broches de bronze, franzido em pregas, e cai sobre as costas, a armadura e as
  pernas até a panturrilha. A textura é pintada no próprio molde: barra tecida com grega em ocre, furos de
  brasa chamuscados, rasgos compridos, um canto arrancado e a barra desfiada com fios soltos (no alfa da
  textura). O pano é uma folha só, vista dos dois lados. Com o escudo nas costas entra uma segunda capa,
  simulada com o escudo por cima.
- **Escudo:** por padrão vai no braço esquerdo (no painel dá para pôr nas costas). A face de bronze é
  gravada: anéis, faixa de onda corrida e raios em volta da formiga.
- **Camadas sem interseção:** as peças procedurais são montadas em camadas que não se cruzam:
  - as barras coloridas são partes complementares da mesma casca (não chapas sobrepostas), e as epômides
    e o cinturão são recortes do próprio corselete afastados dele, então ficam sempre paralelos;
  - escamas, cravos e fivelas são assentados por raios lançados contra a superfície;
  - saia, ptéruges, faixa e capa usam os pesos da pele mais próxima, então acompanham as pernas nas poses
    (a capa segue só o tronco e as coxas, não os braços);
  - com a armadura ligada, o corpo usa uma malha sem os polígonos escondidos sob a roupa (a pele não
    tem como vazar pelo tecido); com ela desligada, entram o corpo inteiro e uma saia com o cós justo no quadril;
  - o corselete é rígido: passa em ponte sobre o sulco da coluna e termina numa barra reta, de onde saem as tiras.
- **Acabamento:** os recortes das cascas são refinados por bisseção (bordas lisas), as bordas enroladas
  são reamostradas por comprimento de arco e a oclusão de ambiente é assada por vértice (sombras de
  contato entre as camadas), lida pelos shaders **Troia/Personagem** e **Troia/Pele**.
- **Desgaste de batalha:** o herói aparece no meio da luta, não saído da oficina:
  - sujeira: lama e poeira subindo dos pés (cnêmides, barra da capa e da saia), pó nas frestas da armadura,
    manchas e escorridos no linho e na pele; sangue seco em respingos, mais forte na ponta da lâmina;
  - rasgos: capa com a barra esfarrapada, fendas, furos de brasa e um canto arrancado; bainha da saia rasgada;
    uma das pontas da faixa rasgada; tiras de couro de comprimentos desiguais, algumas cortadas;
  - marcas de combate: três cortes de lâmina no linho do corselete, sulcos de golpes no bronze e três
    flechas quebradas cravadas no escudo.
  A sujeira e o sangue são dois controles do painel. Funcionam com máscaras gravadas na cor dos vértices
  (G = sujeira, B = regiões de sangue) e uma textura repetível (`Texturas/Desgaste/sujeira.png`), lidas
  pelos shaders dos personagens. Os rasgos da saia são recortes na malha (`SkirtTear` em `PatrocloFit.cs`);
  os da capa estão no molde (`Ferramentas/blender/capa_molde.py`), que recorta a malha e pinta o alfa.
- **Espada:** lâmina em folha de ~1 m (estilo das espadas da Idade do Bronze, ~1,4 m no total), nervura
  com filete de ouro, meandro gravado, guarda em chifres com volutas e empunhadura de duas mãos.
  Na pose Neutra ela descansa no ombro direito.
- **Adaga de cintura:** no quadril direito, do lado oposto ao da faixa: bainha de couro com boca e
  ponteira de bronze, presa por uma correia na barra do corselete. O escudo tem rebites de ouro em volta.
- **Materiais (4K):** bronze martelado e polido gerado por código (marcas de martelo, riscos de polimento,
  manchas de mão), couro, linho, lã e pedra escaneados do Poly Haven (CC0). Pele em 4K com poros,
  vermelhidão nas extremidades, lábios, veias e mapa de brilho (zona T e lábios brilham, barba é fosca).
- **Texturas pintadas (4K):** elmo, cnêmides, escudo, disco do peito, lâminas do braço, placas das
  braçadeiras e cinturão dividem um mapa de textura próprio (sem repetição), e o linho do corselete e das
  abas tem outro. O pintor (`Ferramentas/blender/pintor.py`) conhece a posição 3D, a oclusão, a curvatura e
  a distância à beira de cada ponto: põe pátina escura nas frestas e nas gravações, metal vivo nas quinas,
  mossas, cortes e arranhões. O grão fino (martelado, trama) continua vindo da textura repetida, como mapa
  de detalhe. A capa tem o seu conjunto (`pintor_capa.py`).
- **Capturas:** 20 vistas em 1600x2000 (frente, costas com e sem capa, perfil, closes, combate, sem armadura,
  capa inteira, adaga e cintura, escudo nas costas, lado do escudo), renderizadas em 3200x4000 com MSAA 4x
  e reduzidas (supersampling).
- **Mexer no design:** selecione `Patroclo` e use o componente **Patroclo Design** no Inspector:
  - paleta: tinge as texturas (nas peças com textura pintada, a cor escolhida entra como proporção sobre a
    cor padrão: no padrão a pintura sai exatamente como foi feita);
  - armadura (desligue para ver o corpo inteiro e a musculatura), elmo, crista, capa, e escudo nas costas ou no braço;
  - desgaste de batalha: sujeira e sangue, de 0 (limpo) a 1;
  - pose: Repouso (A), Neutra, Combate ou Livre;
  - prévias de favor divino, nota de estilo (D a SSS) e bênção de cada deus.
- **Posar à mão:** escolha a pose **Livre** (o painel deixa de mexer nos ossos) e gire os ossos do
  MakeHuman (`upperarm01.R`, `lowerarm01.R`, `upperleg01.L`, `spine03`, `head`...). No repouso todos
  têm rotação zero e eixos iguais aos do mundo: X é a direita do personagem, Y é para cima e Z é para a frente.
- **Regerar:** menu `Troia > Personagem > Gerar Pátroclo` (recria malhas, materiais e o prefab, usando as
  peças do Blender que estiverem em `Fonte/patroclo_blender.*`).
- **Blender:** a capa, a crista e as texturas pintadas são feitas no Blender 5.x por script, sem janela.
  - arquivo de arte: `Arte/Patroclo/Patroclo.blend` (esqueleto, todas as peças com pesos e materiais com as
    texturas do jogo); fica fora de `Assets/` de propósito, para o Unity não tentar importá-lo;
  - refazer tudo: `Ferramentas/blender/gerar.sh` (Unity exporta as peças procedurais, o Blender simula,
    abre os mapas de textura e exporta, os pintores geram as texturas, o Unity monta o personagem e tira as
    capturas); `gerar.sh blender` pula a exportação do Unity e `gerar.sh unity` só remonta no Unity;
  - mexer à mão: abra o `.blend`, edite a peça, rode `Ferramentas/blender/bl_exportar.py` na aba Scripting
    e depois `Troia > Personagem > Gerar Pátroclo` no Unity. Só saem as peças com a propriedade
    `troia_exportar` ligada. Atenção: `gerar.sh` recria o `.blend` do zero e apaga edições manuais;
  - detalhes em `Ferramentas/blender/LEIAME.md`.
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
- `Assets/Troia/Scripts/Editor/` — gerador do personagem
  - `PatrocloBuilder.cs` — materiais, montagem das peças, prefab, vitrine e capturas
  - `PatrocloFit.cs` / `PatrocloArmor.cs` / `PatrocloMeshes.cs` — peças procedurais ajustadas ao corpo
  - `PatrocloAO.cs` — oclusão e máscaras de desgaste assadas nos vértices
  - `PatrocloTroca.cs` — ponte com o Blender (exporta a base, lê as peças prontas)
  - `MakeHumanImporter.cs` — corpo e esqueleto
- `Assets/Troia/Scripts/Runtime/` — `FireFlicker`, `LevelMarker`, `MapPreviewWalker`, `TroiaShaderGlobals`,
  `PatrocloDesign` (painel do personagem)
- `Assets/Troia/Shaders/` — `TroiaLitSujo.shader` e `TroiaGrime.hlsl`
