using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>Estúdio simples para fotografar o Pátroclo em lote: cena vazia, chão, sol, câmera e uma folha de células.</summary>
    public class PatrocloEstudio
    {
        public Camera cam;
        public RenderTexture rt;
        public GameObject heroi, modelo;
        public PatrocloDesign design;
        public int celula;
        Texture2D folha;
        int colunas, linhas;

        public PatrocloEstudio(string vista, int celula = 400, bool comArmas = true)
        {
            this.celula = celula;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.42f, .42f, .46f);
            var sol = new GameObject("Sol").AddComponent<Light>(); sol.type = LightType.Directional; sol.intensity = 1.5f;
            sol.transform.rotation = Quaternion.Euler(42, vista == "tras" ? 20 : 205, 0); sol.shadows = LightShadows.Soft;
            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.transform.localScale = Vector3.one * 3;
            chao.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.32f, .32f, .33f) };
            cam = new GameObject("Camera").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.2f, .21f, .23f);
            cam.fieldOfView = 30;
            Mirar(vista);
            heroi = new GameObject("Heroi");
            modelo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PatrocloJogo.Prefab), heroi.transform);
            modelo.name = "Patroclo";
            design = modelo.GetComponent<PatrocloDesign>(); design.escudo = PosicaoEscudo.Braco; design.Apply();
            // várias poses no mesmo quadro do editor: a pele precisa ser recalculada a cada render
            foreach (var smr in modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
            rt = new RenderTexture(celula, celula, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render(); cam.Render();
        }
        public void Mirar(string vista)
        {
            Vector3 olho = vista == "lado" ? new Vector3(4.6f, 1.4f, .3f) : vista == "tras" ? new Vector3(-1.2f, 2.3f, -4.6f)
                : vista == "jogo" ? new Vector3(0f, 2.9f, -5.6f) : new Vector3(2.1f, 1.6f, 4.1f);
            cam.transform.position = olho; cam.transform.LookAt(new Vector3(0, 1.0f, .25f));
        }
        public void Folha(int colunas, int linhas)
        {
            this.colunas = colunas; this.linhas = linhas;
            folha = new Texture2D(celula * colunas, celula * linhas, TextureFormat.RGB24, false);
            var cinza = new Color32[celula * colunas * celula * linhas];
            for (int i = 0; i < cinza.Length; i++) cinza[i] = new Color32(40, 42, 46, 255);
            folha.SetPixels32(cinza);
        }
        public void Celula(int coluna, int linha)
        {
            cam.Render();
            var antes = RenderTexture.active; RenderTexture.active = rt;
            folha.ReadPixels(new Rect(0, 0, celula, celula), coluna * celula, (linhas - 1 - linha) * celula); RenderTexture.active = antes;
        }
        public void Salvar(string caminho)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(caminho));
            File.WriteAllBytes(caminho, folha.EncodeToJPG(88));
            Object.DestroyImmediate(folha); folha = null;
        }
        public void Fechar() { cam.targetTexture = null; rt.Release(); }

        public static string[] Args()
        {
            var args = System.Environment.GetCommandLineArgs();
            int sep = System.Array.IndexOf(args, "--");
            if (sep < 0) return new string[0];
            var r = new List<string>(); for (int i = sep + 1; i < args.Length; i++) r.Add(args[i]); return r.ToArray();
        }
    }
}
