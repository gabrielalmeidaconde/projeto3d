# Pátroclo no Blender

A capa, a crista de crina e as texturas pintadas do Pátroclo são feitas no Blender (5.x), por script e sem
abrir a janela. O Unity continua montando o personagem; ele só troca as peças procedurais pelas que o
Blender devolve.

## Caminho dos dados

1. **Unity → Blender.** `Troia > Personagem > Exportar base para o Blender` gera as peças procedurais e grava
   `Arte/Patroclo/troca/base.json` + `base.bin` (não versionado): malhas em repouso, pesos, UVs e ossos.
   Esse passo deixa o prefab só com as peças procedurais; o passo 5 põe as do Blender de volta.
2. **Blender.** `bl_montar.py` cria `Arte/Patroclo/Patroclo.blend`. Depois, na ordem:
   - `bl_capa.py` simula a capa (molde em `capa_molde.py`) e cria `Capa`, `Capa_Escudo` e `Capa_Broches`;
   - `bl_crina.py` cria a crista e os dois rabos de crina em mechas;
   - `bl_uv.py` abre o bronze e o linho em mapas de textura próprios (`UVMapa`);
   - `bl_exportar.py` grava `Assets/Troia/Personagens/Patroclo/Fonte/patroclo_blender.json` + `.bin`.
3. **Pintores** (Python do sistema, com numpy, scipy e Pillow), em `Texturas/Blender/`:
   - `../texturas/crina.py`: mechas de crina com alfa;
   - `pintor_capa.py`: capa (cor, barra tecida, chamuscado, alfa desfiado, normal e máscara);
   - `pintor.py`: bronze e linho, lendo o pacote exportado.
4. **Blender de novo.** `bl_materiais.py` liga essas texturas nos materiais do `.blend`.
5. **Unity.** `Troia > Personagem > Gerar Pátroclo` lê o pacote: cada malha do pacote substitui a peça
   procedural de mesmo nome (`Pat_Capa`, `Pat_Elmo_Casco`...).

`gerar.sh` roda tudo isso. `gerar.sh blender` começa no passo 2; `gerar.sh unity` faz só o passo 5.

## Conferir sem abrir o Blender

```
blender -b Arte/Patroclo/Patroclo.blend -P Ferramentas/blender/bl_previa.py -- /tmp/previa frente costas tq elmo
```

Opções: `--pose=neutra`, `--ossos='{"upperarm01.L": [0, 0, 33]}'` (graus, como no painel do Unity),
`--esconder=Objeto1,Colecao2`, `--res=800x1000`, `--barro` (tudo de uma cor só, para ver a forma).

## Mexer à mão

Abra `Arte/Patroclo/Patroclo.blend`. As coleções são `Corpo`, `Armadura`, `Elmo`, `Roupa` e `Armas`; as peças
procedurais que foram substituídas ficam escondidas em `Procedural`. Cada objeto guarda de onde veio nas
propriedades personalizadas:

| propriedade | para que serve |
| --- | --- |
| `troia_malha` | nome da malha no Unity; é por ele que a peça substitui a procedural |
| `troia_exportar` | ligado = sai no pacote. Ligue numa peça que você editou |
| `troia_rigida` | peça presa a um osso só (espada, escudos, adaga) |

Depois de editar, rode `bl_exportar.py` (aba Scripting > abrir > Run Script) e gere o personagem no Unity.
Os modificadores são aplicados na exportação. O que precisa ser mantido:

- os grupos de vértices com os nomes dos ossos (são os pesos);
- o mapa `UVMapa` das peças pintadas (a textura foi pintada nele) e o `UVMetro` (em metros, usado pela sujeira);
- o nome dos materiais (`M_Pat_...`), que é como o Unity acha o material de cada parte.

`gerar.sh` recria o `.blend` do zero. Se você editou à mão, não rode o `gerar.sh` inteiro: exporte e gere só
no Unity (`gerar.sh unity`).

## Mudar o desenho por script

- **Capa:** tamanho, rasgos e furos em `capa_molde.py` (`L`, `W_BARRA`, `RASGOS`, `FUROS`); tecido em
  `bl_capa.py` (`st.mass`, `st.bending_stiffness`: menor = mais dobras). A barra tecida e as cores estão em
  `pintor_capa.py`.
- **Crista:** `altura()`, o número de mechas e a inclinação em `bl_crina.py`.
- **Gravações do bronze e pintura do linho:** funções `bronze()` e `linho()` em `pintor.py`.
- **Que peças ganham textura pintada:** `CONJUNTOS` em `bl_uv.py`.

## Armadilhas já pagas

- Colisão do pano: só as faces de fora das peças podem ser colisor. As peças têm espessura (face de fora e
  face de dentro); com as duas, a de dentro puxa o tecido para dentro do corpo.
- A massa do pano é por vértice. Com ~12 mil vértices ela fica em 0,02; com o valor padrão o pano estica.
- Objeto escondido não tem os modificadores calculados; `bl_exportar.py` mostra as peças antes de exportar.
- Antes de abrir UVs por script, desmarque vértices e arestas e use o modo de seleção por face; senão o modo
  de edição seleciona a malha inteira.
