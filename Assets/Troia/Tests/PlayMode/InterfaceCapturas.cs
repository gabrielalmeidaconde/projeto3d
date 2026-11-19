using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Troia.Tests
{
    /// <summary>
    /// Não é um teste de regra: passa pelas telas do jogo (HUD fora e dentro da luta, poder, escolha de bênção, pausa,
    /// lista de golpes, derrota e vitória) e salva imagens em Capturas/Interface. Roda só quando pedido pelo nome
    /// (Ferramentas/interface.sh).
    /// </summary>
    public class InterfaceCapturas
    {
        const int W = 1920, H = 1080;

        [UnityTest, Explicit] public IEnumerator Telas()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity");
            yield return null;
            var gm = GameManager.Instance; var player = gm.player; player.readInput = false;
            Directory.CreateDirectory("Capturas/Interface");
            yield return new WaitForSeconds(1f);
            yield return Foto("descartar");
            yield return new WaitForSeconds(.3f);
            yield return Foto("descartar");
            yield return Foto("1_fora_de_combate");
            // luta de verdade na primeira arena: alvo travado, vida baixa, nota alta e uma sequência de golpes
            gm.StartFloor();
            yield return new WaitForSeconds(.6f);
            foreach (var e in EnemyCombat.Combatants) if (e && !e.Health.IsDead && player.Targeting.Lock(e)) break;
            player.Health.TakeDamage(new DamageInfo { damage = 70 });
            yield return new WaitForSeconds(.15f);
            player.SelectWeapon(1);
            string[] armas = { "xifos", "lanca", "escudo" };
            for (int i = 0; i < 16; i++) player.Style.RegisterHit("captura." + i, i % 4 == 3, armas[i % 3], i % 5 == 0, false, 0, 37, "Investida da Lança");
            yield return new WaitForSeconds(.25f);
            yield return Foto("2_combate");
            // nome do poder ao conjurar
            foreach (var b in gm.config.blessings.Where(b => b && b.god == God.Zeus).Take(1)) player.Favor.Grant(b);
            player.Favor.Select(God.Zeus); player.Favor.Gain(100);
            bool usou = player.Favor.Activate();
            yield return new WaitForSeconds(.22f);
            yield return Foto(usou ? "3_poder" : "3_poder_nao_usou");
            yield return new WaitForSeconds(1.5f);

            // cinco andares vencidos: os deuses oferecem bênçãos
            var marcadores = Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None);
            for (int volta = 0; volta < 6 && gm.State != RunState.ChoosingBlessing; volta++)
            {
                if (!gm.FloorActive)
                {
                    int arena = gm.config.floors[gm.Floor - 1].arena;
                    var ponto = marcadores.First(m => m.kind == MarkerKind.EnemySpawn && m.arena == arena);
                    player.Respawn(ponto.transform.position + Vector3.up * .15f, ponto.transform.rotation);
                    gm.StartFloor();
                    yield return null;
                }
                foreach (var e in Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).Where(e => !e.Health.IsDead))
                    e.Health.TakeDamage(new DamageInfo { source = player.gameObject, damage = 99999, heavy = true });
                gm.CompleteFloor();
                yield return null;
            }
            Assert.That(gm.State, Is.EqualTo(RunState.ChoosingBlessing), "chegar à escolha de bênção");
            yield return new WaitForSecondsRealtime(.6f);
            yield return Foto("4_bencaos");
            gm.ChooseBlessing(0);
            yield return new WaitForSecondsRealtime(.3f);

            gm.TogglePause();
            yield return new WaitForSecondsRealtime(.5f);
            yield return Foto("5_pausa");
            gm.GetComponent<GameplayHUD>().ShowMoveList();
            yield return new WaitForSecondsRealtime(.5f);
            yield return Foto("6_golpes");
            gm.GetComponent<GameplayHUD>().MostrarControles();
            yield return new WaitForSecondsRealtime(.5f);
            yield return Foto("6b_controles");
            gm.TogglePause();
            yield return new WaitForSecondsRealtime(.2f);

            gm.EndRun(false);
            yield return new WaitForSecondsRealtime(.5f);
            yield return Foto("7_derrota");
            gm.EndRun(true);
            yield return null;
            var hud = gm.GetComponent<GameplayHUD>();
            hud.SendMessage("RefreshModals");
            yield return new WaitForSecondsRealtime(.5f);
            yield return Foto("8_vitoria");
        }

        /// <summary>Cena e interface juntas (GameplayHUD.Fotografar).</summary>
        static IEnumerator Foto(string nome)
        {
            var camera = Camera.main; if (!camera) yield break;
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            var hud = GameManager.Instance ? GameManager.Instance.GetComponent<GameplayHUD>() : null;
            if (hud) yield return hud.Fotografar(camera, rt);
            else { var alvo = camera.targetTexture; camera.targetTexture = rt; camera.Render(); camera.targetTexture = alvo; }
            Salvar(rt, nome);
            RenderTexture.ReleaseTemporary(rt);
        }

        static void Salvar(RenderTexture rt, string nome)
        {
            var ativo = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.GetFullPath($"Capturas/Interface/{nome}.png"), tex.EncodeToPNG());
            RenderTexture.active = ativo; Object.Destroy(tex);
        }
    }
}
