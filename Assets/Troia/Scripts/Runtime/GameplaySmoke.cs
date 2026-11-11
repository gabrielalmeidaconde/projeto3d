#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Troia
{
    // Opt-in unattended check of a real development player, including rendered HUD.
    public class GameplaySmoke : MonoBehaviour
    {
        IEnumerator Start()
        {
            var gm = GameManager.Instance;
            gm.player.readInput = false;
            yield return new WaitForSecondsRealtime(4);
            bool passed = gm.FloorActive && gm.Alive > 0 && gm.player.IsGrounded;
            Directory.CreateDirectory("Capturas");
            CaptureCamera("gameplay-camera");
            Debug.Log($"TROIA_SMOKE {(passed ? "PASS" : "FAIL")} floor={gm.Floor} alive={gm.Alive} grounded={gm.player.IsGrounded} hp={gm.player.Health.Current}");
            gm.BeginTraining(); yield return new WaitForSecondsRealtime(.25f);
            var player = gm.player;
            var view=Camera.main.transform;
            Vector3 viewPosition=view.position; Quaternion viewRotation=view.rotation;
            view.position=player.transform.TransformPoint(new Vector3(-2.2f,1.3f,3.1f));
            view.LookAt(player.transform.position+Vector3.up*1.05f);
            CaptureCamera("personagem-frente");
            view.SetPositionAndRotation(viewPosition,viewRotation);
            foreach (var enemy in EnemyCombat.Combatants)
                if (enemy && enemy.trainingDummy && player.Targeting.Lock(enemy)) break;
            for (int i = 0; i < player.config.weapons.Length; i++)
            {
                player.SelectWeapon(i); yield return new WaitForSecondsRealtime(.2f);
                player.RequestAttack(AttackInput.Light);
                yield return new WaitForSecondsRealtime(player.Weapon.light.hitStart + .035f);
                CaptureCamera("arma-" + player.Weapon.id);
                yield return new WaitForSecondsRealtime(player.Weapon.light.duration + .05f);
            }
            // Grade fixtures exercise every visible rank without relying on random AI timing.
            var hud = gm.GetComponent<GameplayHUD>();
            player.Style.ResetMeter();
            for (int i = 0; i < 40; i++)
                player.Style.RegisterHit("qa_" + i, false, player.Weapon.id, false, false, 9000 + i, 17, "Variedade / captura de validacao");
            yield return null;
            passed &= hud.DisplayedRank == "SSS";
            CaptureCamera("estilo-SSS");
            hud.ShowMoveList(); yield return null;
            CaptureCamera("lista-de-golpes");
            gm.Restart(false); yield return null;
            gm.TogglePause(); yield return null;
            passed &= !hud.MoveListVisible && gm.State == RunState.Paused;
            CaptureCamera("pausa");
            gm.Restart(false);
            for (int floor = 1; floor <= 5; floor++)
            {
                gm.StartFloor();
                foreach (var enemy in EnemyCombat.Combatants.ToArray())
                    if (enemy && !enemy.Health.IsDead) enemy.Health.TakeDamage(new DamageInfo { source = player.gameObject, damage = 99999 });
                gm.CompleteFloor();
                yield return null;
            }
            CaptureCamera("bencaos");
            Debug.Log($"TROIA_PRESENTATION {(passed ? "PASS" : "FAIL")} arsenal={player.config.weapons.Length} ui=SSS");
            yield return new WaitForSecondsRealtime(.2f);
            Application.Quit(passed ? 0 : 1);
        }
        static void CaptureCamera(string name)
        {
            var camera = Camera.main;
            if (!camera) return;
            var rt = new RenderTexture(1280, 720, 24);
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            camera.targetTexture = rt; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.GetFullPath("Capturas/" + name + ".png"), texture.EncodeToPNG());
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            Destroy(texture); rt.Release(); Destroy(rt);
        }
    }
}
#endif
