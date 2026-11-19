using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Troia.Tests
{
    /// <summary>
    /// Combos avançados (arsenal versão 8) executados de verdade no treino: cada sequência de entradas tem de chegar ao
    /// golpe previsto. A variante explícita CapturasDosGolpes fotografa cada golpe novo no impacto (Capturas/Combos).
    /// </summary>
    public class CombosAvancadosTests
    {
        const string Map = "Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity";
        const AttackInput L = AttackInput.Light, P = AttackInput.Heavy;

        static IEnumerator Preparar()
        {
            if (!GameManager.Instance) yield return SceneManager.LoadSceneAsync(Map);
            yield return null;
            var gm = GameManager.Instance; gm.player.readInput = false;
            gm.BeginTraining();
            yield return new WaitForSeconds(.3f);
        }

        /// <summary>Aperta cada entrada assim que o golpe atual abre a janela de combo; devolve o golpe final.</summary>
        static IEnumerator Executar(PlayerCombat p, List<ComboSO> golpes, bool noAr, params AttackInput[] entradas)
        {
            golpes.Clear();
            // começa do zero: golpe anterior terminado, de pé no chão e recargas dos pesados zeradas
            float fim = Time.time + 4;
            while (Time.time < fim && (p.CurrentAttack || !p.IsGrounded)) yield return null;
            yield return new WaitForSeconds(1.2f);
            if (noAr)
            {
                p.RequestJump(); yield return new WaitForSeconds(.12f);
                p.RequestJump(); yield return new WaitForSeconds(.1f);
            }
            for (int i = 0; i < entradas.Length; i++)
            {
                var antes = p.CurrentAttack;
                if (antes)
                {
                    float limite = Time.time + 2;
                    while (Time.time < limite && p.CurrentAttack == antes && p.AttackTime < antes.comboOpen) yield return null;
                }
                p.RequestAttack(entradas[i]);
                float espera = Time.time + 1.5f;
                while (Time.time < espera && (p.CurrentAttack == antes || !p.CurrentAttack)) yield return null;
                golpes.Add(p.CurrentAttack);
            }
        }

        static IEnumerable<(string nome, int arma, bool ar, AttackInput[] entradas, System.Func<WeaponSO, ComboSO> esperado)> Casos()
        {
            yield return ("Xifos L L L P", 0, false, new[] { L, L, L, P }, w => w.light.next.next.heavyNext);
            yield return ("Xifos P P P", 0, false, new[] { P, P, P }, w => w.heavy.heavyNext.heavyNext);
            yield return ("Xifos no ar L L P", 0, true, new[] { L, L, P }, w => w.airLight.next.heavyNext);
            yield return ("Lança L L L P", 1, false, new[] { L, L, L, P }, w => w.light.next.next.heavyNext);
            yield return ("Lança P P P", 1, false, new[] { P, P, P }, w => w.heavy.heavyNext.heavyNext);
            yield return ("Lança P L L", 1, false, new[] { P, L, L }, w => w.heavy.next.next);
            yield return ("Lança no ar L L P", 1, true, new[] { L, L, P }, w => w.airLight.next.heavyNext);
            yield return ("Escudo P P P", 2, false, new[] { P, P, P }, w => w.heavy.heavyNext.heavyNext);
            yield return ("Escudo P L L", 2, false, new[] { P, L, L }, w => w.heavy.next.next);
            yield return ("Escudo no ar L L P", 2, true, new[] { L, L, P }, w => w.airLight.next.heavyNext);
        }

        [UnityTest]
        public IEnumerator CadaComboAvancadoChegaAoGolpeCerto()
        {
            yield return Preparar();
            var gm = GameManager.Instance; var p = gm.player;
            var golpes = new List<ComboSO>();
            var falhas = new List<string>();
            foreach (var caso in Casos())
            {
                p.SelectWeapon(caso.arma); yield return new WaitForSeconds(.3f);
                var alvo = caso.esperado(p.Weapon);
                Assert.That(alvo, Is.Not.Null, caso.nome + ": golpe não existe na configuração");
                p.Favor.Gain(100);
                yield return Executar(p, golpes, caso.ar, caso.entradas);
                var final = golpes.Count > 0 ? golpes[golpes.Count - 1] : null;
                if (final != alvo) falhas.Add($"{caso.nome}: chegou em {(final ? final.id : "nada")} ({string.Join(" > ", golpes.ConvertAll(g => g ? g.id : "-"))}), esperado {alvo.id}");
                yield return new WaitForSeconds(alvo.duration + .3f);
            }
            Assert.That(falhas, Is.Empty, string.Join("\n", falhas));
            gm.EndRun(false);
        }

        [UnityTest, Explicit]
        public IEnumerator CapturasDosGolpes()
        {
            yield return Preparar();
            var gm = GameManager.Instance; var p = gm.player;
            Directory.CreateDirectory("Capturas/Combos");
            var camera = Camera.main;
            // pátio aberto da cidadela (arena 5), longe do Cavalo e das paredes
            var arena = System.Array.Find(Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None), m => m.kind == MarkerKind.CombatArena && m.order == 5);
            if (arena) { p.Respawn(arena.transform.position + new Vector3(-4, .2f, -3), Quaternion.Euler(0, 0, 0)); yield return new WaitForSeconds(.6f); }
            var golpes = new List<ComboSO>();
            int n = 0;
            foreach (var caso in Casos())
            {
                p.SelectWeapon(caso.arma); yield return new WaitForSeconds(.3f);
                var alvo = caso.esperado(p.Weapon);
                yield return Executar(p, golpes, caso.ar, caso.entradas);
                // três momentos do golpe final: preparação, impacto e acompanhamento
                float[] momentos = { Mathf.Max(0, alvo.hitStart - .14f), alvo.hitStart + .02f, Mathf.Min(alvo.duration - .02f, alvo.hitEnd + .12f) };
                for (int k = 0; k < 3; k++)
                {
                    float limite = Time.time + 2;
                    while (Time.time < limite && !(p.CurrentAttack == alvo && p.AttackTime >= momentos[k])) yield return null;
                    var view = camera.transform; Vector3 pos = view.position; Quaternion rot = view.rotation;
                    view.position = p.transform.TransformPoint(new Vector3(2.6f, 1.5f, 2.4f)); view.LookAt(p.transform.position + Vector3.up * 1.05f);
                    Foto(camera, $"{n:00}_{caso.nome.Replace(' ', '_')}_{k}");
                    view.SetPositionAndRotation(pos, rot);
                }
                n++;
                yield return new WaitForSeconds(alvo.duration);
            }
            gm.EndRun(false);
        }

        static void Foto(Camera camera, string nome)
        {
            var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            var alvo = camera.targetTexture; var ativo = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.GetFullPath($"Capturas/Combos/{nome}.png"), tex.EncodeToPNG());
            camera.targetTexture = alvo; RenderTexture.active = ativo; Object.Destroy(tex); RenderTexture.ReleaseTemporary(rt);
        }
    }
}
