using UnityEngine;

namespace Troia
{
    public class GameplayHUD : MonoBehaviour
    {
        bool help = true;
        void Update() { if (UnityEngine.InputSystem.Keyboard.current?.hKey.wasPressedThisFrame ?? false) help = !help; }
        void OnGUI()
        {
            var gm = GameManager.Instance; if (!gm || !gm.player) return;
            var p = gm.player;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .5f, 2);
            var oldMatrix = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.Box(new Rect(16, 16, 350, 144), "ILÍADA - A NOITE DA INVASÃO");
            GUI.Label(new Rect(28, 43, 330, 25), $"Vida {p.Health.Current:0}/{p.Health.maximum:0}    Favor {p.Favor.Amount:0}/100");
            GUI.Label(new Rect(28, 69, 330, 25), $"Andar {gm.Floor:00}/{gm.config.floors.Length}    Tempo {Mathf.FloorToInt(gm.RemainingTime / 60):00}:{Mathf.FloorToInt(gm.RemainingTime % 60):00}");
            GUI.Label(new Rect(28, 95, 330, 25), $"Estilo {StyleMeter.Labels[p.Style.Rank]}  x{p.Style.Multiplier:0.00}    Inimigos {gm.Alive}");
            GUI.Label(new Rect(28, 121, 330, 25), $"Deus: {p.Favor.Selected} Nv.{p.Favor.Level(p.Favor.Selected)}   Recarga {p.Favor.CooldownRemaining:0.0}s");
            GUI.Box(new Rect(width * .5f - 260, 18, 520, 34), gm.Message);
            if (gm.IsPlaying && !gm.FloorActive)
            {
                Vector3 direction = gm.Destination - p.transform.position; direction.y = 0;
                float angle = Vector3.SignedAngle(p.cameraTransform ? p.cameraTransform.forward : p.transform.forward, direction, Vector3.up);
                GUI.Label(new Rect(width * .5f - 170, 58, 400, 24), $"Proxima arena: {direction.magnitude:0}m  {(Mathf.Abs(angle) < 20 ? "EM FRENTE" : angle > 0 ? "DIREITA >" : "< ESQUERDA")}");
            }
            if (help)
            {
                GUI.Box(new Rect(16, height - 142, 620, 126), "CONTROLES  [H: ocultar]");
                GUI.Label(new Rect(28, height - 115, 600, 100), "WASD / analogico: mover | Shift: correr | Espaco / A: pular\nMouse: camera | Scroll: zoom | Ctrl / B: esquiva e cancelamento\nLMB / J / X: leve | RMB / K / Y: pesado (segure: lancador)\nLLL: corte triplo | LL, pausa, LL: estocadas | No ar: LLL ou P queda\nQ / RB: poder | Tab / direcional direita: trocar deus | Esc / Start: pausa");
            }
            if (gm.State == RunState.ChoosingBlessing)
            {
                GUI.Box(new Rect(width * .5f - 390, height * .5f - 140, 780, 280), "BÊNÇÃO DOS DEUSES - 1 / 2 / 3 ou direcional + A");
                var choices = gm.Choices;
                for (int i = 0; i < choices.Length; i++)
                {
                    var b = choices[i]; float x = width * .5f - 370 + i * 250;
                    GUI.Box(new Rect(x, height * .5f - 95, 240, 160), $"{(gm.HighlightedChoice == i ? "> " : "")}{i + 1}. {b.title} - Nv.{p.Favor.Level(b.god) + 1}");
                    var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
                    GUI.Label(new Rect(x + 12, height * .5f - 60, 215, 100), b.description + $"\nCusto: {b.cost:0} favor", style);
                    if (GUI.Button(new Rect(x, height * .5f + 80, 240, 38), "Escolher")) gm.ChooseBlessing(i);
                }
            }
            if (gm.State == RunState.Won || gm.State == RunState.Lost || gm.State == RunState.Paused)
            {
                GUI.Box(new Rect(width * .5f - 230, height * .5f - 130, 460, 270), gm.State == RunState.Paused ? "PAUSADO" : gm.Message);
                GUI.Label(new Rect(width * .5f - 200, height * .5f - 90, 420, 60), $"Andar: {gm.Floor} | Kills: {gm.Kills} | Tempo: {gm.RemainingTime:0}s\nNota media: {StyleMeter.Labels[Mathf.Clamp(Mathf.RoundToInt(p.Style.AverageRank), 0, 6)]}");
                if (gm.State == RunState.Paused && GUI.Button(new Rect(width * .5f - 200, height * .5f - 20, 400, 32), "Continuar")) gm.TogglePause();
                if (GUI.Button(new Rect(width * .5f - 200, height * .5f + 20, 400, 32), "Reiniciar partida [R]")) gm.Restart(false);
                if (gm.HasCheckpoint && GUI.Button(new Rect(width * .5f - 200, height * .5f + 60, 400, 32), "Retomar checkpoint")) gm.Restart(true);
            }
            GUI.matrix = oldMatrix;
        }
    }
}
