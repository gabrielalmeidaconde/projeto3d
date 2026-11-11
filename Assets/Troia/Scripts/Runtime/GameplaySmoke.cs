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
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Capturas/gameplay-build.png"));
            CaptureCamera();
            Debug.Log($"TROIA_SMOKE {(passed ? "PASS" : "FAIL")} floor={gm.Floor} alive={gm.Alive} grounded={gm.player.IsGrounded} hp={gm.player.Health.Current}");
            yield return new WaitForSecondsRealtime(2);
            // Capture the modal independently of random combat progression.
            foreach (var enemy in FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None)) enemy.enabled = false;
            gm.TogglePause();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Capturas/gameplay-pause.png"));
            yield return new WaitForSecondsRealtime(1);
            Application.Quit(passed ? 0 : 1);
        }
        static void CaptureCamera()
        {
            var camera = Camera.main;
            if (!camera) return;
            var rt = new RenderTexture(1280, 720, 24);
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.GetFullPath("Capturas/gameplay-camera.png"), texture.EncodeToPNG());
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            Destroy(texture); rt.Release(); Destroy(rt);
        }
    }
}
#endif
