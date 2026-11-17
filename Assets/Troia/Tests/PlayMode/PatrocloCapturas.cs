using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Troia.Tests
{
    /// <summary>
    /// Não é um teste de regra: joga no modo treino e salva imagens da câmera do jogo em Capturas/Jogo,
    /// para conferir o herói e os golpes sem abrir o editor. Roda só quando pedido pelo nome (-testFilter PatrocloCapturas).
    /// </summary>
    public class PatrocloCapturas
    {
        [UnityTest, Explicit] public IEnumerator HeroiEmJogo()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity");
            yield return null;
            var gm = GameManager.Instance; var player = gm.player; player.readInput = false;
            Directory.CreateDirectory("Capturas/Jogo");
            yield return new WaitForSeconds(.6f);
            Assert.That(player.GetComponent<PlayerPresentation>().UsaPatroclo, Is.True, "O herói do jogo deve ser o Pátroclo");
            Foto("00_inicio");
            var view = Camera.main.transform;
            gm.BeginTraining(); yield return new WaitForSeconds(.4f);
            EnemyCombat alvo = null;
            foreach (var enemy in EnemyCombat.Combatants) if (enemy && enemy.trainingDummy && player.Targeting.Lock(enemy)) { alvo = enemy; break; }
            player.SetMoveInput(new Vector2(0, 1), player.transform.eulerAngles.y, true);
            yield return new WaitForSeconds(.35f); Foto("01_correndo");
            player.SetMoveInput(Vector2.zero, 0, false); yield return new WaitForSeconds(.3f);
            float quadros = 0, tempo = 0;
            for (int w = 0; w < player.config.weapons.Length; w++)
            {
                player.SelectWeapon(w); yield return new WaitForSeconds(.25f);
                string id = player.Weapon.id;
                player.RequestApproach(); yield return new WaitForSeconds(.2f); Foto($"{w}_{id}_0_avanco");
                yield return new WaitForSeconds(.5f);
                for (int golpe = 1; golpe <= 3; golpe++)
                {
                    player.RequestAttack(AttackInput.Light);
                    float limite = Time.time + 1;
                    while (Time.time < limite && !(player.CurrentAttack && player.ComboDepth >= Mathf.Min(golpe, 2) && player.AttackTime >= player.CurrentAttack.hitStart + .02f && player.AttackTime < player.CurrentAttack.hitEnd + .08f))
                    { quadros++; tempo += Time.unscaledDeltaTime; yield return null; }
                    Foto($"{w}_{id}_{golpe}_leve");
                    if (player.CurrentAttack) yield return new WaitForSeconds(Mathf.Max(.02f, player.CurrentAttack.comboOpen - player.AttackTime - .04f));
                }
                yield return new WaitForSeconds(.9f);
                player.RequestAttack(AttackInput.Heavy);
                yield return new WaitForSeconds(player.Weapon.heavy.hitStart + .05f); Foto($"{w}_{id}_4_pesado");
                yield return new WaitForSeconds(1.1f);
                player.RequestLauncher();
                yield return new WaitForSeconds(player.Weapon.launcher.hitEnd); Foto($"{w}_{id}_5_lancador");
                yield return new WaitForSeconds(1.2f);
            }
            player.SelectWeapon(0); yield return new WaitForSeconds(.3f);
            player.RequestShoot(); yield return new WaitForSeconds(.07f); Foto("7_dardo");
            yield return new WaitForSeconds(.4f);
            player.RequestDodge(-player.transform.right); yield return new WaitForSeconds(.14f); Foto("8_esquiva");
            yield return new WaitForSeconds(.5f);
            player.RequestJump(); yield return new WaitForSeconds(.25f); player.RequestAttack(AttackInput.Light);
            yield return new WaitForSeconds(player.Weapon.airLight.hitStart + .04f); Foto("9_aereo");
            yield return new WaitForSeconds(1f);
            // retrato de perto, de frente, com a luz da fase
            Vector3 p0 = view.position; Quaternion r0 = view.rotation;
            view.position = player.transform.TransformPoint(new Vector3(-1.3f, 1.45f, 2.6f)); view.LookAt(player.transform.position + Vector3.up * 1.1f);
            Foto("A_retrato", false);
            view.SetPositionAndRotation(p0, r0);
            Debug.Log($"PATROCLO_CAPTURAS ok; quadro medio {(quadros > 0 ? tempo / quadros * 1000 : 0):0.0} ms; dano no alvo {(alvo ? 5000 - alvo.Health.Current : 0):0}");
            gm.EndRun(false);
        }
        /// <summary>Filmagem quadro a quadro de uma sequência de leves (para caçar defeitos de um golpe específico).</summary>
        [UnityTest, Explicit] public IEnumerator SequenciaDeLeves()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity");
            yield return null;
            var gm = GameManager.Instance; var player = gm.player; player.readInput = false;
            Directory.CreateDirectory("Capturas/Jogo");
            yield return new WaitForSeconds(.5f);
            gm.BeginTraining(); yield return new WaitForSeconds(.4f);
            foreach (var enemy in EnemyCombat.Combatants) if (enemy && enemy.trainingDummy && player.Targeting.Lock(enemy)) break;
            int arma = 1;
            foreach (string a in System.Environment.GetCommandLineArgs()) if (a.StartsWith("-troia-arma=")) int.TryParse(a.Substring(12), out arma);
            player.SelectWeapon(arma); yield return new WaitForSeconds(.3f);
            string vista = "lado";
            foreach (string a in System.Environment.GetCommandLineArgs()) if (a.StartsWith("-troia-vista=")) vista = a.Substring(13);
            // em campo aberto, sem alvo na frente: dá para ver o braço e a arma inteiros
            player.Targeting.Clear(); yield return new WaitForSeconds(.3f);
            var view = Camera.main.transform; int quadro = 0, n = 0;
            var sb = new System.Text.StringBuilder();
            Time.captureFramerate = 60;      // cada quadro vale 1/60 s, por mais que a captura demore
            int pedidos = 0, depois = 0;
            while (quadro < 70 && depois < 8)
            {
                if (pedidos == 0 || (pedidos < 3 && player.CurrentAttack && player.ComboDepth == pedidos && player.AttackTime >= player.CurrentAttack.comboOpen - .06f))
                { player.RequestAttack(AttackInput.Light); pedidos++; }
                if (!player.CurrentAttack && pedidos >= 3) depois++;
                if (n++ % 2 == 0)
                {
                    Vector3 p0 = view.position; Quaternion r0 = view.rotation;
                    // de cima (mostra para onde o golpe vai) e de trás, perto
                    view.position = player.transform.position + Vector3.up * 5.2f - player.transform.forward * .2f;
                    view.rotation = Quaternion.LookRotation(Vector3.down, player.transform.forward);
                    Foto($"cima_{quadro:00}", false);
                    view.position = player.transform.TransformPoint(new Vector3(.9f, 2.5f, -3.0f)); view.LookAt(player.transform.position + Vector3.up * 1.0f + player.transform.forward * .7f);
                    Foto($"tras_{quadro:00}", false);
                    view.SetPositionAndRotation(p0, r0);
                    sb.AppendLine($"quadro {quadro}: golpe {(player.CurrentAttack ? player.CurrentAttack.id : "-")} t={player.AttackTime:0.000} prof {player.ComboDepth}");
                    quadro++;
                }
                yield return null;
            }
            Time.captureFramerate = 0;
            File.WriteAllText("Capturas/Jogo/seq.txt", sb.ToString());
            gm.EndRun(false);
        }
        static void Foto(string nome, bool reposicionar = true)
        {
            var camera = Camera.main; if (!camera) return;
            var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 2 };
            var alvo = camera.targetTexture; var ativo = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.GetFullPath("Capturas/Jogo/" + nome + ".png"), tex.EncodeToPNG());
            camera.targetTexture = alvo; RenderTexture.active = ativo; Object.Destroy(tex); rt.Release(); Object.Destroy(rt);
        }
    }
}
