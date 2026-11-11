# Jogabilidade da Ilíada (plano v2)

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
| Pular | Espaço | A |
| Leve | Clique esquerdo ou J | X |
| Pesado / lançador | Clique direito ou K / segurar 0,35 s | Y / segurar |
| Esquivar e cancelar recuperação | Ctrl | B |
| Poder divino / trocar deus | Q / Tab | RB / direcional direito |
| Pausa | Esc | Start |

- Corte triplo: leve, leve, leve. O terceiro golpe empurra.
- Estocadas: leve, leve, pausa, leve, leve. No segundo golpe a janela normal abre
  em 0,371 s; a variação começa em 0,47 s e aceita entrada até 0,65 s.
- Lançador: segurar pesado no chão. Pule durante a recuperação para perseguir o alvo.
- Juggle: três leves no ar sustentam jogador e inimigo; pesado no ar faz queda com dano em área ao aterrissar.
- A esquiva cancela o golpe, preserva a próxima etapa por 0,8 s e concede invulnerabilidade
  no início. Tem recarga para impedir invulnerabilidade contínua.
- Entradas de ataque têm buffer de 0,2 s. Cada alvo recebe dano uma única vez por golpe.
  Geometria entre atacante e alvo bloqueia golpes e poderes.

O estilo vai de D a SSS. Repetições recentes dão menos pontos, inatividade provoca
decaimento e dano recebido derruba dois níveis. A nota multiplica favor e tempo por kill.

## Componentes e ajuste

- `GameplayConfig`: velocidades, gravidade, pulo, esquiva, buffer, hold pesado, vida,
  tempo inicial, tempo por kill e referências aos assets.
- `ComboSO`: dano, duração, alcance, arco, avanço, knockback, lançamento, janelas e
  conexões normal/atrasada. São 11 golpes configurados; nenhum depende de nomes de assets em runtime.
- `PlayerCombat`: estados de locomoção, ataque, esquiva, stagger e morte, câmera com
  colisão, assistência frontal leve e efeitos de impacto.
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
Regenerar o mapa não sobrescreve esses assets. O menu de configuração não sobrescreve
assets existentes; o fallback em memória permite jogar mesmo sem configuração salva.

## Animações e apresentação

O repositório original só fornece um corpo de cápsula e não contém rig/clips de combate.
Esta implementação usa espada, antecipação de inimigos, movimentos de arma, partículas,
hit stop e tremor de câmera procedurais. A lógica e os tempos funcionam sem Animator.

Para integrar clips, atribua um `Animator` ao `PlayerCombat`, configure `animationState`
nos `ComboSO` e habilite `useAnimationEvents`. Eventos dos clips devem chamar
`OpenHitbox`, `CloseHitbox`, `OpenComboWindow` e `CloseComboWindow` no jogador.
Nesse modo os eventos controlam as janelas; `duration` continua sendo o limite do estado.
O impacto do golpe de queda depende da aterrissagem física em ambos os modos.

Heitor usa o mesmo controlador de combate com vida, alcance, dano e ritmo próprios;
não há animação cinematográfica ou segunda fase exclusiva. Áudio, modelos finais e
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
timeout, morte e reinício. Um terceiro cenário verifica poderes ativos dos seis deuses,
recargas, dash, armadura, ataque da IA e projéteis. Isso não substitui uma partida humana
completa para dificuldade.

Build de desenvolvimento: execute `Troia.Editor.GameplaySetup.BuildWindows` via
`-executeMethod` em batchmode. A saída fica em `Builds/Windows/Iliada.exe`.
O argumento `-troia-smoke` na build de desenvolvimento executa uma checagem inicial,
salva a câmera em `Capturas/gameplay-camera.png` e encerra com código 0 em caso de sucesso.
As capturas da janela inteira dependem de uma janela gráfica ativa; a captura direta
da câmera também funciona em execução oculta. Resultados e limites da validação estão
em [VALIDACAO.md](VALIDACAO.md).
