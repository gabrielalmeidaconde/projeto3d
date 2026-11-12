# Jogabilidade da Ilíada: arsenal e combate de estilo

## Escopo e integração

Base revisada: `a178377` (início do projeto) e `ce8970f` (alterações do mapa).
O mapa, materiais, NavMesh e marcadores desses commits são reutilizados.
`GameManager` encontra o `PlayerSpawn` ao carregar a cena, desliga `MapPreviewWalker`
e instala o jogador e HUD. Não é necessário editar a cena gerada ou recriar o mapa.

O ciclo é: entrar na arena indicada, derrotar os inimigos de cada andar, ganhar tempo
e favor com as kills, escolher uma bênção a cada cinco andares e chegar a Heitor no 20.
Os três primeiros andares usam a Ágora, 4–6 Mercado, 7–10 Portas Ceias, 11–14 Fonte,
15–19 Cidadela e 20 Altar de Zeus. O portão abre ao concluir os andares de Portas Ceias.
O timer também corre entre arenas; pausa e escolha de bênção suspendem a simulação.
Checkpoints duram a sessão atual, com bênçãos e tempo preservados após os andares 5/10/15.

## Combate e controles

| Ação | Teclado/mouse | Controle |
| --- | --- | --- |
| Mover / correr | WASD / Shift | Analógico esquerdo / pressionar analógico |
| Câmera / zoom | Mouse / scroll | Analógico direito |
| Pular / pulo duplo | Espaço / novamente no ar | A / novamente no ar |
| Leve | Clique esquerdo ou J | X |
| Pesado / lançador | Clique direito ou K / C ou segurar 0,35 s | Y / segurar |
| Esquivar e cancelar animação | Ctrl | B |
| Fixar alvo / próximo alvo | F ou clique do meio / T | LT / pressionar analógico direito |
| Avançar (espada/foice) / puxar (machado) | E | RB |
| Pistolas duplas | R (pode segurar) | RT |
| Trocar arma | 1 / 2 / 3 | Direcional esquerdo / direito |
| Poder divino / trocar deus | Q / Tab | LB / direcional baixo |
| Lista de golpes / treino livre | H / F2 | Menu de pausa |
| Pausa | Esc | Start |

- Xifos: corte triplo, estocadas com pausa e rupturas em rotas leve/pesado.
- Foice: maior alcance, varreduras multihit, controle de grupos e avanço terrestre/aéreo.
- Machado: dois golpes de base mais lentos, alto impacto, quebra de guarda e puxão.
- Cada arma tem leve, pesado, lançador, sequência aérea, queda e aproximação próprios.
- Trocas durante um golpe aguardam a janela de dano e a recuperação mínima. O próximo leve continua
  na etapa seguinte da nova arma. Selecionar a arma atual cancela uma troca pendente.
- Estocadas: leve, leve, pausa, leve, leve. Na espada, o segundo golpe abre a janela
  normal em 0,26 s; a variação começa em 0,4 s e aceita entrada até 0,59 s.
- Lançador: segurar pesado no chão. Pule durante a recuperação para perseguir o alvo.
- Juggle: leves no ar sustentam jogador e inimigo somente ao acertar. Errar não permite
  voo infinito. Pesado no ar faz queda com dano em área ao aterrissar.
- O segundo pulo renova a velocidade vertical uma vez; cargas só voltam ao aterrissar.
- Aproximação exige alvo visível, tem recarga, usa CharacterController e termina em
  golpe. Funciona contra alvos lançados e não concede invulnerabilidade.
- Projéteis usam varredura contínua e cadência independente. Trocar de arma não altera
  um projétil já lançado; disparar não interrompe o ataque corpo a corpo.
- A esquiva cancela o golpe, preserva a próxima etapa por 0,8 s e concede invulnerabilidade
  no início. Tem recarga para impedir invulnerabilidade contínua.
- Entradas fora de um golpe têm buffer de 0,2 s. Durante um golpe, um comando pode aguardar
  sua próxima janela. Cada alvo recebe dano uma vez por pulso configurado.
  Geometria entre atacante e alvo bloqueia golpes e poderes.

O estilo vai de D a SSS, com nome, cor, barra de progresso e animação no HUD. Repetições
reduzem o ganho até zero; variar armas e acertar no ar dá bônus. Um identificador por
ação impede multiplicar estilo com multidões, multihit ou duas balas. Hits e dano são
contados separadamente. Inatividade provoca decaimento mesmo se o jogador insistir
em um golpe que não pontua. Dano recebido derruba dois níveis. A nota multiplica favor
e tempo por kill; pico e média são registrados para o placar.

F2 inicia treino livre: alvos passivos com 5000 de vida, reações completas de combate,
timer suspenso, recuperação de vida/favor e os seis deuses liberados no nível 1.
Reiniciar partida sai do treino e restaura a progressão normal.

## Pesquisa e revisão do combate (v4)

A pesquisa usou relatos dos próprios desenvolvedores. A PlatinumGames descreve
encadeamentos que preservam a sequência entre formas e vantagens de alcançar etapas
posteriores do combo em [Combat Techniques](https://www.platinumgames.com/official-blog/article/14063).
Em [Enemy Battle Design](https://www.platinumgames.com/official-blog/article/14074),
discute tempos mínimos para permitir reação mesmo com inimigos rápidos.
Essas referências orientam a revisão; não significam que o protótipo reproduza esses jogos.

Aplicações no projeto:

- Uma entrada antecipada aguarda a janela seguinte do golpe atual. Há somente um
  comando pendente; apertar repetidamente não cria uma fila de ataques automáticos.
  Aproximação também preserva uma entrada e uma troca; a troca se efetiva após o impacto.
- A direção do movimento escolhe o alvo de um novo ataque. Foco explícito prevalece.
  A preparação permite uma aproximação de até 1,1 m, com colisão e linha de visão.
- Há controle parcial de direção durante os ataques e no ar. Após impacto e recuperação
  mínima, mover cancela o restante da animação e preserva a rota por 0,8 s.
- Leves comuns não têm recarga adicional depois da duração. Finalizadores têm 0,12 s,
  pesados 0,35 s, lançadores 0,55 s e quedas 0,8 s. O pesado principal do machado tem
  preparação de 0,28 s, duração de 0,9 s e recarga de 0,45 s. Cancelamentos não zeram essas reservas.
- C executa o lançador diretamente; segurar pesado continua funcionando.
- Esquivar de um ataque real nos primeiros 0,12 s concede estilo e dez de favor uma
  vez por esquiva. Isso não consome o escudo de Atena. Apenas esquivar no vazio não pontua.
- No máximo dois inimigos preparam ataques simultaneamente. Os demais circulam;
  novos ataques exigem visibilidade. Arcos no chão mostram golpes corpo a corpo e
  linhas mostram a mira dos arqueiros. O corpo pulsa durante a preparação.
- Um impacto de grupo não repete áudio e hit stop para cada alvo no mesmo instante.

Espada encadeia golpes rápidos; foice mantém alcance de 4,2 m e controle de grupos;
machado mantém impacto, quebra de guarda e puxão. Pistolas compartilham sua cadência
e recuperação entre armas. Estilo continua premiando acertos variados e perdendo
dois níveis ao sofrer dano. Repetir a mesma ação rende progressivamente menos.

O visual continua estilizado, com cabeça menor, mandíbula angulosa, olhos estreitos,
íris vermelhas, cicatriz e cabelo cinza prateado. Blush e grandes brilhos foram removidos.
Casaco vinho, couro preto, lapelas angulosas e iluminação de maior contraste substituem
a aparência de boneco fofo. A cápsula física permanece igual.

## Componentes e ajuste

- `GameplayConfig`: velocidades, gravidade, pulo, esquiva, buffer, hold pesado, vida,
  tempo inicial, tempo por kill e referências aos assets.
- `WeaponSO`: identidade, apresentação, entradas/continuação de combo, projéteis e cadência.
- `ComboSO`: dano por impacto, pulsos, duração, alcance, arco, avanço, knockback,
  lançamento, janelas e conexões leve/pesada/atrasada, configurados por assets.
- `PlayerCombat`: estados de locomoção, ataque, esquiva, stagger e morte, câmera com
  colisão, buffer, troca de arma e aproximação. `PlayerTargeting` mantém foco e troca de alvo.
- `PlayerProjectile`: varredura, bloqueio por cenário e crédito do acerto ao dono.
- `PlayerPresentation` e `CombatAudio`: corpo articulado procedural, armas, rastros,
  movimentos, pulo duplo e áudio sintetizado localmente.
- `GameplayHUD`: Canvas uGUI na câmera, medidor de estilo, retículo, arsenal e menus.
- `Health`, `IDamageable`, `DamageInfo`: contrato comum para dano, direção,
  knockback, lançamento, stagger, finalizador e origem.
- `CombatEvents`: eventos por jogador para hit, finalizador, lançamento, esquiva e nota.
- `EnemyCombat`: soldado, arqueiro, capitão e Heitor; perseguição por NavMesh,
  antecipação de ataque, recuperação, stagger, lançamento e aterrissagem.
  Armadura recebe dano leve, mas só reage a pesado, aéreo ou poder divino.
- `WaveSO`: grupos de inimigos, multiplicadores, arena e índices opcionais dos spawns.
- `GameManager`: andares, timer, bloqueios, portão, escolhas, checkpoints, derrota,
  vitória e reinício sem recarregar o mapa.
- `BlessingSO` e `DivineFavor`: custo/recarga, níveis e ganchos dos seis deuses.
  Zeus: raios; Ares: fúria/estilo; Apolo: cura/nota S; Hermes: dash/ataque após esquiva;
  Atena: escudo/parry; Poseidon: onda/lançamento em grupo.

Os valores estão em `Assets/Troia/Resources/Troia/Gameplay.asset` e subpastas.
Regenerar o mapa não sobrescreve esses assets. O menu migra configurações antigas uma
vez para a versão do arsenal, mantendo os dados existentes do xifos e adicionando as
armas/rotas novas. Configurações já migradas não são recriadas. O fallback em memória
permite jogar mesmo sem configuração salva.

## Animações e apresentação

O repositório original não contém rig/clips de combate. O jogador agora tem corpo
articulado e três armas visuais procedurais, com movimentos de corte, estocada, giro,
lançador e queda, mais rastros, partículas, áudio, hit stop e tremor. Modelos e clips
de produção podem substituir essa apresentação; a lógica funciona sem Animator.

Para integrar clips, atribua um `Animator` ao `PlayerCombat`, configure `animationState`
nos `ComboSO` e habilite `useAnimationEvents`. Eventos dos clips devem chamar
`OpenHitbox`, `CloseHitbox`, `OpenComboWindow` e `CloseComboWindow` no jogador.
Nesse modo os eventos controlam as janelas; `duration` continua sendo o limite do estado.
O impacto do golpe de queda depende da aterrissagem física em ambos os modos.

Heitor usa o mesmo controlador de combate com vida, alcance, dano e ritmo próprios;
não há animação cinematográfica ou segunda fase exclusiva. Áudio final, modelos finais e
balanceamento por sessões humanas permanecem trabalho de apresentação.

## Executar as verificações

No Unity Test Runner (`Window > General > Test Runner`), execute EditMode e PlayMode.
Pela linha de comando, com o Editor fechado:

```powershell
./scripts/Test-Gameplay.ps1 -Build
```

Ou execute cada etapa separadamente:

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe'
Start-Process $unity -ArgumentList '-batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -logFile Logs/editmode.log' -WindowStyle Hidden -Wait
Start-Process $unity -ArgumentList '-batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml -logFile Logs/playmode.log' -WindowStyle Hidden -Wait
```

Os testes de regras verificam ramificações, janelas, assets serializados, composição dos
20 andares, dano/morte/cura, estilo, eventos e snapshot de bênçãos. Os testes de integração
usam o mapa real e uma plataforma de física isolada para movimentação, combos, buffer,
esquiva, lançamento e queda. A progressão automatizada elimina os inimigos diretamente
para verificar os 20 andares, barreiras, chefes, escolhas, tempo por kill, checkpoints,
timeout, morte e reinício. Outros cenários verificam os seis poderes/recargas, armadura,
ataque da IA, projéteis, pulo duplo, perseguição aérea, troca durante combos, multihit,
estilo sem pontuação artificial por área, treino e HUD. Isso não substitui uma partida
humana completa para dificuldade.

Build de desenvolvimento: execute `Troia.Editor.GameplaySetup.BuildWindows` via
`-executeMethod` em batchmode. A saída fica em `Builds/Windows/Iliada.exe`.
O argumento `-troia-smoke` na build de desenvolvimento executa uma checagem inicial,
salva a câmera em `Capturas/gameplay-camera.png` e encerra com código 0 em caso de sucesso.
O diagnóstico também salva as três armas, o HUD em SSS, a lista de golpes, pausa e
escolha de bênçãos. O HUD uGUI é renderizado pela câmera e aparece nessas capturas,
inclusive em execução oculta. O SSS do diagnóstico usa uma sequência de acertos de
teste para conferir o layout. Resultados e limites da validação estão
em [VALIDACAO.md](VALIDACAO.md).
