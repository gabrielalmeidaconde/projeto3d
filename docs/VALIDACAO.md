# Validação da jogabilidade

Executada em 11/11/2026 no Unity 6000.3.25f1.

| Verificação | Resultado |
| --- | --- |
| Remoto e commits | `main` atualizado com `origin/main`; revisados `a178377` e `ce8970f` |
| Edit Mode | 10/10 testes aprovados |
| Play Mode | 3/3 cenários de integração aprovados |
| Build Windows de desenvolvimento | Compilação concluída, código de saída 0 |
| Executável com Direct3D 11 | Código de saída 0; primeiro andar ativo, dois inimigos, jogador no chão e vida 120 |
| Renderização da câmera em runtime | PNG inspecionado: mapa, personagem e espada renderizados corretamente |

Os cenários de integração verificam movimentação diagonal normalizada, pulo, buffer,
sequência de combo, invulnerabilidade de esquiva, cancelamento com ataque guardado,
lançamento, queda, ataque da IA, projétil, armadura e os seis poderes/recargas.
Verificam também que perder nota não ativa cura de Apolo.

O cenário de progressão percorre os 20 andares no mapa real, confirma composição de
inimigos, caminhos completos de navegação até o jogador, barreiras, capitão, Heitor,
escolhas de bênção, checkpoints, tempo por kill, timeout, morte, vitória e reinício.
Ele elimina inimigos diretamente para acelerar a verificação da progressão; não é
uma partida humana completa nem uma avaliação de balanceamento.

Relatórios locais em `Logs/editmode-results.xml`, `Logs/playmode-results.xml`,
`Logs/build.log` e `Logs/player-smoke.log`. Imagem de câmera em
`Capturas/gameplay-camera.png`; executável em `Builds/Windows/Iliada.exe`.
Builds, logs e capturas permanecem nas pastas ignoradas pelo Git.

A inspeção manual da interface pela janela não pôde ser realizada: o serviço nativo
de controle do computador não estava disponível. Capturas do backbuffer em janela
oculta ficaram pretas; a alternativa validada foi renderizar a câmera em RenderTexture,
que não inclui a sobreposição IMGUI do HUD. As escolhas e estados de UI foram exercitados
pelos testes de integração, mas a aparência final do HUD ainda merece conferência humana.

Modelos e movimentos de combate são procedurais. Clips de animação, áudio, apresentação
final de Heitor e ajuste de dificuldade por sessões humanas continuam como acabamento.
O gerador do mapa mantém um aviso de API obsoleta em `LightingSettings.autoGenerate`,
anterior a esta implementação e sem impedir compilação/testes.
