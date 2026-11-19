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
    /// Não é um teste de regra: joga um combo de verdade no treino (entradas no tempo da janela de combo) e filma o golpe
    /// final quadro a quadro, pela câmera do jogo (atrás do herói) e de lado. Salva em Capturas/Filme/&lt;nome&gt;.
    /// -troia-combo=LLL  (L leve, P pesado; prefixo "A" = no ar)   -troia-arma=0   -troia-quadros=12
    /// -troia-variantes=ID1,ID2  filma o mesmo combo várias vezes, cada vez com o golpe final tocando o trecho de outro
    /// golpe da biblioteca de animação (para comparar candidatos sem remontar nada).
    /// </summary>
    public class FilmeDoCombo
    {
        [UnityTest, Explicit] public IEnumerator Filmar()
        {
            string Arg(string nome, string padrao)
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a.StartsWith("-troia-" + nome + "=")) return a.Substring(nome.Length + 8);
                return padrao;
            }
            string combo = Arg("combo", "LLL"); int arma = int.Parse(Arg("arma", "0")); int quadros = int.Parse(Arg("quadros", "12"));
            var variantes = new List<string>(Arg("variantes", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            if (variantes.Count == 0) variantes.Add("");
            yield return SceneManager.LoadSceneAsync("Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity");
            yield return null;
            var gm = GameManager.Instance; var p = gm.player; p.readInput = false;
            gm.BeginTraining();
            var arena = System.Array.Find(Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None), m => m.kind == MarkerKind.CombatArena && m.order == 5);
            if (arena) p.Respawn(arena.transform.position + new Vector3(-4, .2f, -3), Quaternion.identity);
            yield return new WaitForSeconds(1f);
            p.SelectWeapon(arma); yield return new WaitForSeconds(.5f);
            Time.captureFramerate = 60;
            var biblioteca = PatrocloAnimacoesSO.Carregar();
            var cam = Camera.main;
            var lado = new GameObject("Camera_Lado").AddComponent<Camera>(); lado.CopyFrom(cam); lado.enabled = false;
            bool noAr = combo.StartsWith("A"); string sequencia = noAr ? combo.Substring(1) : combo;
            PatrocloAnimacoesSO.Trecho original = null, alvoTrecho = null;
            foreach (var variante in variantes)
            {
                // de volta ao ponto de partida, parado no chão
                float pronto = Time.time + 4;
                while (Time.time < pronto && (p.CurrentAttack || !p.IsGrounded)) yield return null;
                if (arena) p.Respawn(arena.transform.position + new Vector3(-4, .2f, -3), Quaternion.identity);
                p.SelectWeapon(arma);   // (reaparecer volta para a primeira arma)
                yield return new WaitForSeconds(1.3f);
                if (noAr) { p.RequestJump(); yield return new WaitForSeconds(.12f); p.RequestJump(); yield return new WaitForSeconds(.1f); }
                ComboSO final = null;
                for (int i = 0; i < sequencia.Length; i++)
                {
                    var antes = p.CurrentAttack;
                    if (antes) { float lim = Time.time + 2; while (Time.time < lim && p.CurrentAttack == antes && p.AttackTime < antes.comboOpen) yield return null; }
                    // o golpe final toca o trecho da variante (troca feita antes de ele começar)
                    if (i == sequencia.Length - 1 && variante != "" && biblioteca)
                    {
                        var proximo = antes ? (sequencia[i] == 'P' ? antes.heavyNext : antes.next) : null;
                        if (proximo != null)
                        {
                            alvoTrecho = biblioteca.Golpe(proximo.id);
                            var v = biblioteca.Golpe(variante);
                            Assert.That(alvoTrecho != null && v != null, $"trecho ausente: {proximo.id} ou {variante}");
                            if (original == null) original = Copia(alvoTrecho);
                            CopiarPara(v, alvoTrecho);
                        }
                    }
                    p.RequestAttack(sequencia[i] == 'P' ? AttackInput.Heavy : AttackInput.Light);
                    float espera = Time.time + 1.5f;
                    while (Time.time < espera && (p.CurrentAttack == antes || !p.CurrentAttack)) yield return null;
                    final = p.CurrentAttack;
                }
                Assert.That(final, Is.Not.Null);
                string pasta = $"Capturas/Filme/{combo}_{arma}{(variante != "" ? "_" + variante : "")}";
                if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
                Directory.CreateDirectory(pasta);
                var sb = new System.Text.StringBuilder($"{final.id} {final.displayName} dur {final.duration:0.00} impacto {final.hitStart:0.000} trecho {(variante == "" ? "próprio" : variante)}\n");
                // o golpe final inteiro, do começo ao fim, em quadros iguais
                for (int k = 0; k < quadros; k++)
                {
                    float alvo = final.duration * k / (quadros - 1f) * .98f;
                    float lim = Time.time + 3;
                    while (Time.time < lim && p.CurrentAttack == final && p.AttackTime < alvo) yield return null;
                    lado.transform.position = p.transform.TransformPoint(new Vector3(3.6f, 1.4f, .6f)); lado.transform.LookAt(p.transform.position + Vector3.up * 1.0f);
                    Foto(cam, $"{pasta}/jogo_{k:00}.png"); Foto(lado, $"{pasta}/lado_{k:00}.png");
                    sb.AppendLine($"{k:00} t={p.AttackTime:0.000} golpe={(p.CurrentAttack ? p.CurrentAttack.id : "-")}");
                }
                File.WriteAllText($"{pasta}/quadros.txt", sb.ToString());
            }
            if (original != null) CopiarPara(original, alvoTrecho);
            Time.captureFramerate = 0;
            Object.Destroy(lado.gameObject);
            gm.EndRun(false);
        }

        static PatrocloAnimacoesSO.Trecho Copia(PatrocloAnimacoesSO.Trecho t) => new PatrocloAnimacoesSO.Trecho
        { id = t.id, clip = t.clip, inicio = t.inicio, impacto = t.impacto, fim = t.fim, estocada = t.estocada, giro = t.giro };
        static void CopiarPara(PatrocloAnimacoesSO.Trecho de, PatrocloAnimacoesSO.Trecho para)
        { para.clip = de.clip; para.inicio = de.inicio; para.impacto = de.impacto; para.fim = de.fim; para.estocada = de.estocada; para.giro = de.giro; }

        static void Foto(Camera camera, string caminho)
        {
            var rt = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32);
            var alvo = camera.targetTexture; var ativo = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(caminho), tex.EncodeToPNG());
            camera.targetTexture = alvo; RenderTexture.active = ativo; Object.Destroy(tex); RenderTexture.ReleaseTemporary(rt);
        }
    }
}
