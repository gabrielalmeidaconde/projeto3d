using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Troia.Tests
{
    /// <summary>
    /// Não é um teste de regra: toca cada golpe que as armas alcançam, sem inimigos por perto, e salva cinco quadros
    /// (preparação, quase impacto, impacto, acompanhamento, fim) de cada um em Capturas/Auditoria, para julgar a animação.
    /// Roda só quando pedido pelo nome. -troia-arma=N limita a uma arma (0 xifos, 1 lança, 2 escudo).
    /// </summary>
    public class AuditoriaGolpes
    {
        [UnityTest, Explicit] public IEnumerator Golpes()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity");
            yield return null;
            var gm = GameManager.Instance; var p = gm.player; p.readInput = false;
            yield return new WaitForSeconds(1f);
            // tempo do jogo em passos fixos: cada quadro anda 1/120 s, por mais lenta que seja a captura
            // (sem isso o golpe salta vários momentos entre dois quadros e as fotos saem repetidas)
            Time.captureFramerate = 120;
            var comecar = typeof(PlayerCombat).GetMethod("BeginAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            var arena = System.Array.Find(Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None), m => m.kind == MarkerKind.CombatArena && m.order == 5);
            Vector3 lugar = arena ? arena.transform.position + new Vector3(-4, .2f, -3) : p.transform.position;
            string filtro = System.Array.Find(System.Environment.GetCommandLineArgs(), a => a.StartsWith("-troia-arma="));
            int so = filtro != null ? int.Parse(filtro.Substring(12)) : -1;
            Directory.CreateDirectory("Capturas/Auditoria");
            var camera = Camera.main;
            Foto(camera, "descartar");
            var linhas = new System.Text.StringBuilder();
            for (int w = 0; w < gm.config.weapons.Length; w++)
            {
                if (so >= 0 && w != so) continue;
                var arma = gm.config.weapons[w];
                p.SelectWeapon(w); yield return new WaitForSeconds(.4f);
                int n = 0;
                foreach (var (golpe, noAr) in Golpes(arma))
                {
                    p.Respawn(lugar, Quaternion.identity); yield return new WaitForSeconds(.5f);
                    if (noAr) { p.RequestJump(); yield return new WaitForSeconds(.15f); p.RequestJump(); yield return new WaitForSeconds(.12f); }
                    comecar.Invoke(p, new object[] { golpe, false });
                    float hs = golpe.hitStart, d = golpe.duration;
                    float[] momentos = { hs * .35f, hs * .8f, hs + .01f, hs + (d - hs) * .3f, hs + (d - hs) * .8f };
                    for (int k = 0; k < momentos.Length; k++)
                    {
                        float limite = Time.time + 3;
                        while (Time.time < limite && p.CurrentAttack == golpe && p.AttackTime < momentos[k]) yield return null;
                        var view = camera.transform; Vector3 pos = view.position; Quaternion rot = view.rotation;
                        view.position = p.transform.TransformPoint(new Vector3(2.4f, 1.45f, 2.2f)); view.LookAt(p.transform.position + Vector3.up * 1.05f);
                        Foto(camera, $"{w}_{n:00}_{golpe.id}_{k}");
                        view.SetPositionAndRotation(pos, rot);
                    }
                    linhas.AppendLine($"{w}_{n:00} {golpe.id} {golpe.displayName} ({golpe.motion}) dur {d:0.00} impacto {hs:0.000}");
                    n++;
                    float fim = Time.time + 3;
                    while (Time.time < fim && p.CurrentAttack) yield return null;
                }
            }
            Time.captureFramerate = 0;
            File.WriteAllText("Capturas/Auditoria/golpes.txt", linhas.ToString());
            gm.EndRun(false);
        }

        /// <summary>Todos os golpes que a arma alcança, uma vez cada, com a indicação de quais são no ar.</summary>
        static IEnumerable<(ComboSO, bool)> Golpes(WeaponSO arma)
        {
            var vistos = new HashSet<ComboSO>();
            var fila = new Queue<(ComboSO, bool)>();
            foreach (var (raiz, ar) in new[] { (arma.light, false), (arma.heavy, false), (arma.launcher, false), (arma.dash, false),
                                               (arma.airLight, true), (arma.slam, true), (arma.airDash, true) })
                fila.Enqueue((raiz, ar));
            while (fila.Count > 0)
            {
                var (m, ar) = fila.Dequeue();
                if (!m || !vistos.Add(m)) continue;
                yield return (m, ar || m.aerial);
                fila.Enqueue((m.next, ar)); fila.Enqueue((m.heavyNext, ar));
            }
        }

        static void Foto(Camera camera, string nome)
        {
            var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            var alvo = camera.targetTexture; var ativo = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.GetFullPath($"Capturas/Auditoria/{nome}.png"), tex.EncodeToPNG());
            camera.targetTexture = alvo; RenderTexture.active = ativo; Object.Destroy(tex); RenderTexture.ReleaseTemporary(rt);
        }
    }
}
