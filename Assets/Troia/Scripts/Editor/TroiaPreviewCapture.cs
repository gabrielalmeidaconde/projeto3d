using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Troia.EditorTools
{
    /// <summary>Renderiza vistas da Fase 1 em PNG (pasta Capturas/ na raiz do projeto).</summary>
    public static class TroiaPreviewCapture
    {
        struct Shot
        {
            public string Name;
            public Vector3 Pos, Target;
            public float Fov;
        }

        static Shot S(string n, Vector3 p, Vector3 t, float fov = 55f) => new Shot { Name = n, Pos = p, Target = t, Fov = fov };

        [MenuItem("Troia/Capturar Prévias do Mapa", priority = 20)]
        static void CaptureMenu() => Capture(Path.Combine(Directory.GetCurrentDirectory(), "Capturas"));

        public static void CaptureFromCommandLine()
        {
            string dir = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-capturaDir") dir = args[i + 1];
            Capture(dir ?? Path.Combine(Directory.GetCurrentDirectory(), "Capturas"));
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene(TroiaAssets.ScenePath, OpenSceneMode.Single);
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                ps.Simulate(3f, false, true, true);
            var mainCam = Camera.main;
            if (mainCam != null) mainCam.enabled = false;

            var go = new GameObject("CapturaCam");
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 2000f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            const int W = 1600, H = 900;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;

            var shots = new[]
            {
                S("01_cidade_vista_sul", new Vector3(0f, 95f, -250f), new Vector3(0f, 5f, -20f), 50f),
                S("02_arena1_agora_cavalo", new Vector3(-38f, 7f, -58f), new Vector3(-55f, 6f, -40f)),
                S("03_arena2_mercado", new Vector3(-25f, 5f, -98f), new Vector3(-44f, 2f, -82f)),
                S("04_arena3_portas_ceias", new Vector3(0f, 6f, -104f), new Vector3(0f, 6f, -133f)),
                S("05_avenida", new Vector3(0f, 2.5f, -98f), new Vector3(0f, 5f, 0f)),
                S("06_arena4_praca_fonte", new Vector3(10f, 6f, -17f), new Vector3(-4f, 1f, -38f)),
                S("07_rampa", new Vector3(0f, 3f, 8f), new Vector3(0f, 12f, 55f)),
                S("08_arena5_patio_cidadela", new Vector3(12f, 12f, 62f), new Vector3(-15f, 13f, 95f)),
                S("09_arena6_altar_zeus", new Vector3(6f, 12.5f, 89f), new Vector3(17f, 9f, 106f)),
                S("10_exercito_grego", new Vector3(20f, 10f, -215f), new Vector3(0f, 5f, -140f)),
                S("11_vista_da_cidadela", new Vector3(0f, 30f, 120f), new Vector3(0f, 0f, -60f)),
                S("15_detalhe_chao_agora", new Vector3(-62f, 1.7f, -57f), new Vector3(-62f, 0f, -51f)),
                S("17_cavalo_lado_iluminado", new Vector3(-68f, 8f, -27f), new Vector3(-55f, 7f, -40f)),
                S("18_cavalo_cabeca", new Vector3(-55.5f, 13.6f, -55f), new Vector3(-53f, 12.3f, -47.3f), 45f),
                S("16_detalhe_fachadas_avenida", new Vector3(-1.5f, 1.8f, -62f), new Vector3(-6f, 2.6f, -54f)),
            };
            foreach (var s in shots)
            {
                cam.orthographic = false;
                cam.fieldOfView = s.Fov;
                cam.transform.position = s.Pos;
                cam.transform.LookAt(s.Target);
                Render(cam, rt, Path.Combine(dir, s.Name + ".png"));
            }

            // planta vista de cima, com luz "de serviço" para ler o layout
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            if (RenderSettings.sun != null) RenderSettings.sun.intensity = 1.6f;
            data.renderPostProcessing = false;
            cam.orthographic = true;
            cam.orthographicSize = 150f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 300f, -5f), Quaternion.Euler(90f, 0f, 0f));
            Render(cam, rt, Path.Combine(dir, "12_planta_topo.png"));
            cam.orthographicSize = 50f;
            cam.transform.position = new Vector3(-30f, 300f, -80f);
            Render(cam, rt, Path.Combine(dir, "13_planta_cidade_baixa.png"));
            cam.transform.position = new Vector3(5f, 300f, 85f);
            Render(cam, rt, Path.Combine(dir, "14_planta_cidadela.png"));

            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(go);
            Debug.Log($"[Troia] Capturas salvas em {dir}");
        }

        static void Render(Camera cam, RenderTexture rt, string path)
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
