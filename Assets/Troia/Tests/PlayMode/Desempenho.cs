using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Troia.Tests
{
    /// <summary>
    /// Não é um teste de regra: mede o custo de desenhar a fase (batches, triângulos, tempo de quadro) em vários
    /// pontos do mapa e escreve Logs/desempenho.txt. Roda só quando pedido pelo nome (Ferramentas/desempenho.sh).
    /// -troia-perf-variantes liga também a medição "sem X" (sombras, decals, partículas), para saber o que pesa.
    /// -troia-perf-fotos salva uma imagem de cada ponto e closes do herói e inimigos em Capturas/Desempenho;
    /// -troia-perf-lod mede o herói com outros limiares de Mesh LOD; -troia-perf-sem-decals desliga os decals.
    /// </summary>
    public class Desempenho
    {
        static readonly (string nome, float x, float z, float yaw)[] Pontos =
        {
            ("agora_norte", -55f, -60f, 0f), ("agora_leste", -72f, -45f, 90f), ("mercado", -46f, -96f, 45f),
            ("portao", 0f, -128f, 0f), ("avenida", 0f, -95f, 0f), ("fonte", 0f, -42f, 0f),
            ("rampa_topo_sul", 0f, 50f, 180f), ("cidadela", 0f, 60f, 0f), ("palacio", 14f, 90f, 0f),
        };

        const int W = 1920, H = 1080;
        static readonly string[] Nomes = { "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Vertices Count", "Shadow Casters Count" };
        ProfilerRecorder[] rec;
        Camera cam; RenderTexture rt; Texture2D pixel;

        [UnityTest, Explicit] public IEnumerator Medir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            bool variantes = args.Contains("-troia-perf-variantes"), fotos = args.Contains("-troia-perf-fotos");
            yield return SceneManager.LoadSceneAsync("Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity");
            yield return null;
            var gm = GameManager.Instance; gm.player.readInput = false;
            yield return new WaitForSeconds(1.5f);

            rec = Nomes.Select(n => ProfilerRecorder.StartNew(ProfilerCategory.Render, n)).ToArray();
            // o mesmo MSAA que a tela do jogo usa: o do perfil do URP, não o das configurações de qualidade
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var prop = urp ? urp.GetType().GetProperty("msaaSampleCount") : null;
            int msaa = prop != null ? Mathf.Max(1, (int)prop.GetValue(urp)) : Mathf.Max(1, QualitySettings.antiAliasing);
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = msaa };
            pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var sb = new StringBuilder();
            sb.AppendLine($"# {W}x{H} MSAA {msaa}x  placa: {SystemInfo.graphicsDeviceName}  qualidade: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            sb.AppendLine("ponto;variante;batches;setpass;drawcalls;triangulos;vertices;sombras;ms");

            // 1) a câmera do próprio jogo, com herói, inimigos da primeira arena e HUD
            cam = Camera.main;
            yield return Aquecer();
            yield return Linha(sb, "jogo_inicio", "base");
            if (fotos) Foto("jogo_inicio");
            if (args.Contains("-troia-perf-lod"))
            {
                // quanto o limiar de Mesh LOD alivia o herói na câmera do jogo (e como fica)
                float limiar = QualitySettings.meshLodThreshold;
                foreach (float l in new[] { 2f, 4f, 8f })
                {
                    QualitySettings.meshLodThreshold = l;
                    yield return Linha(sb, "jogo_inicio", "lod_limiar_" + l);
                    if (fotos) Foto("jogo_inicio_lod" + l);
                }
                QualitySettings.meshLodThreshold = limiar;
                sb.AppendLine("# limiar de Mesh LOD do projeto: " + limiar);
            }

            // 2) câmera livre nos pontos do mapa (o jogo continua rodando atrás)
            var clone = Object.Instantiate(Camera.main.gameObject); clone.name = "Camera_Medidor"; clone.tag = "Untagged";
            foreach (var l in clone.GetComponents<AudioListener>()) Object.Destroy(l);
            Camera.main.enabled = false; cam = clone.GetComponent<Camera>(); cam.enabled = false;
            var luzes = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var sombras = luzes.ToDictionary(l => l, l => l.shadows);
            var decals = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(m => m.GetType().Name == "DecalProjector").ToArray();
            var particulas = Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None);

            if (args.Contains("-troia-perf-sem-decals"))
                foreach (var d in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (d.GetType().Name == "DecalProjector") d.enabled = false;
            if (fotos)
            {
                // closes para conferir que herói e inimigos continuam com a mesma cara depois de aliviar as malhas
                var heroi = gm.player.transform;
                Perto(heroi, new Vector3(-1.1f, 1.5f, 2.4f), 1.15f); yield return Aquecer(); Foto("perto_heroi_frente");
                Perto(heroi, new Vector3(.9f, 1.7f, -2.6f), 1.1f); yield return Aquecer(); Foto("perto_heroi_costas");
                Perto(heroi, new Vector3(.55f, 1.75f, 1.2f), 1.6f); yield return Aquecer(); Foto("perto_heroi_rosto");
                int k = 0;
                foreach (var inimigo in EnemyCombat.Combatants)
                {
                    if (!inimigo || k >= 2) continue;
                    Perto(inimigo.transform, new Vector3(-1.3f, 1.5f, 3.6f), 1f); yield return Aquecer(); Foto("perto_inimigo_" + k++);
                }
            }
            foreach (var p in Pontos)
            {
                float y = Physics.Raycast(new Vector3(p.x, 80, p.z), Vector3.down, out var hit, 200, ~(1 << 2), QueryTriggerInteraction.Ignore) ? hit.point.y : 0;
                cam.transform.SetPositionAndRotation(new Vector3(p.x, y + 2.6f, p.z), Quaternion.Euler(8, p.yaw, 0));
                yield return Aquecer();
                yield return Linha(sb, p.nome, "base");
                if (fotos) Foto(p.nome);
                if (!variantes) continue;
                foreach (var l in luzes) if (l && l.type != LightType.Directional) l.shadows = LightShadows.None;
                yield return Linha(sb, p.nome, "sem_sombra_fogos");
                foreach (var l in luzes) if (l) l.shadows = LightShadows.None;
                yield return Linha(sb, p.nome, "sem_sombras");
                foreach (var l in luzes) if (l) l.shadows = sombras[l];
                foreach (var d in decals) if (d) d.enabled = false;
                yield return Linha(sb, p.nome, "sem_decals");
                foreach (var d in decals) if (d) d.enabled = true;
                foreach (var r in particulas) if (r) r.enabled = false;
                yield return Linha(sb, p.nome, "sem_particulas");
                foreach (var r in particulas) if (r) r.enabled = true;
                foreach (var l in luzes) if (l && l.type != LightType.Directional) l.enabled = false;
                yield return Linha(sb, p.nome, "sem_luz_fogos");
                foreach (var l in luzes) if (l) l.enabled = true;
            }
            foreach (var r in rec) r.Dispose();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/desempenho.txt", sb.ToString());
            UnityEngine.Debug.Log("DESEMPENHO\n" + sb);
            gm.EndRun(false);
        }

        IEnumerator Aquecer() { for (int i = 0; i < 4; i++) { Desenhar(); yield return null; } }

        void Desenhar() { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }

        IEnumerator Linha(StringBuilder sb, string ponto, string variante)
        {
            for (int i = 0; i < 3; i++) { Desenhar(); yield return null; }
            // contadores: valem para o quadro anterior, em que só esta câmera desenhou
            var valores = new long[rec.Length];
            for (int k = 0; k < 4; k++)
            {
                Desenhar(); yield return null;
                for (int i = 0; i < rec.Length; i++) valores[i] = System.Math.Max(valores[i], rec[i].LastValue);
            }
            // tempo: desenhar e esperar a placa terminar (ler um pixel força a sincronização)
            var tempos = new List<double>();
            for (int k = 0; k < 12; k++)
            {
                var sw = Stopwatch.StartNew();
                Desenhar();
                var ativo = RenderTexture.active; RenderTexture.active = rt;
                pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); RenderTexture.active = ativo;
                sw.Stop(); tempos.Add(sw.Elapsed.TotalMilliseconds);
                yield return null;
            }
            tempos.Sort();
            sb.AppendLine($"{ponto};{variante};{string.Join(";", valores)};{tempos[tempos.Count / 2]:0.0}");
        }

        void Perto(Transform alvo, Vector3 local, float altura)
        {
            cam.transform.position = alvo.TransformPoint(local);
            cam.transform.LookAt(alvo.position + Vector3.up * altura);
        }

        void Foto(string nome)
        {
            Directory.CreateDirectory("Capturas/Desempenho");
            var ldr = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = ldr; cam.Render(); cam.targetTexture = null;
            var ativo = RenderTexture.active; RenderTexture.active = ldr;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = ativo;
            File.WriteAllBytes(Path.GetFullPath($"Capturas/Desempenho/{nome}.png"), tex.EncodeToPNG());
            Object.Destroy(tex); RenderTexture.ReleaseTemporary(ldr);
        }
    }
}
