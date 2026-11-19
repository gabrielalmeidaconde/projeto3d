using UnityEngine;

namespace Troia
{
    public static class CombatVisuals
    {
        public static Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name; go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider) { collider.enabled = false; Object.Destroy(collider); }
            var shader = Shader.Find("Troia/Lit Sujo") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.AddComponent<OwnedMaterial>().material = material;
            return go.transform;
        }
        public static Transform Weapon(Transform actor, Color color)
        {
            var pivot = new GameObject("Braco_Arma").transform;
            pivot.SetParent(actor, false); pivot.localPosition = new Vector3(.5f, 1.2f, .15f);
            Part(pivot, "Espada", PrimitiveType.Cube, new Vector3(0, 0, .6f), new Vector3(.12f, .07f, 1.3f), color);
            Part(pivot, "Guarda", PrimitiveType.Cube, Vector3.zero, new Vector3(.4f, .12f, .13f), new Color(.7f, .45f, .12f));
            return pivot;
        }
        /// <summary>Faíscas esticadas na direção do golpe, com um clarão curto.</summary>
        /// <summary>Desenha uma faísca apagada no começo da fase, para o shader não compilar no meio do primeiro golpe.</summary>
        public static void Prewarm(Vector3 position)
        {
            Spark(position, Vector3.up, Color.black, false, true); Burst(position, Color.black, .05f);
        }
        public static void Spark(Vector3 position, Vector3 direction, Color color, bool heavy, bool silent = false)
        {
            var root = new GameObject("Faiscas") { layer = 2 }; root.transform.position = position;
            if (direction.sqrMagnitude > .01f) root.transform.rotation = Quaternion.LookRotation(direction + Vector3.up * .25f);
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, heavy ? .4f : .26f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(heavy ? 6 : 4, heavy ? 16 : 10); main.startSize = new ParticleSystem.MinMaxCurve(.025f, heavy ? .07f : .045f);
            Color quente = Color.Lerp(color, new Color(1, .9f, .6f), .6f);
            main.startColor = quente; main.loop = false; main.duration = .3f; main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = heavy ? 42 : 30; shape.radius = .05f;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 1; renderer.velocityScale = .07f;
            var material = MaterialParticula(quente * 2.2f, true);
            renderer.sharedMaterial = material; root.AddComponent<OwnedMaterial>().material = material;
            var light = root.AddComponent<Light>(); light.type = LightType.Point; light.color = quente; light.range = heavy ? 6 : 3.5f;
            light.intensity = silent ? 0 : heavy ? 9 : 4; light.shadows = LightShadows.None;
            if (silent) { material.color = new Color(0, 0, 0, .01f); main.startColor = new Color(0, 0, 0, .01f); }
            root.AddComponent<Clarao>();
            ps.Play(); ps.Emit(heavy ? 34 : 16); Object.Destroy(root, .7f);
        }
        static Texture2D ponto;

        /// <summary>Ponto redondo e macio, gerado uma vez: faíscas e estilhaços deixam de ser quadrados chapados.</summary>
        static Texture2D Ponto()
        {
            if (ponto) return ponto;
            const int n = 32;
            ponto = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Ponto_macio", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n * 2 - 1, dy = (y + .5f) / n * 2 - 1;
                    float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * (3 - 2 * a) * 255));
                }
            ponto.SetPixels32(px); ponto.Apply();
            return ponto;
        }

        /// <summary>Material de partícula transparente com o ponto macio (aditivo para faíscas, mistura comum para estilhaços).</summary>
        public static Material MaterialParticula(Color cor, bool aditivo)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", Ponto()); m.color = cor;
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", aditivo ? 2 : 0); m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(aditivo ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        public static void Burst(Vector3 position, Color color, float radius = .35f)
        {
            var root = new GameObject("Impacto"); root.transform.position = position;
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = .22f; main.startSpeed = 4; main.startSize = .1f;
            main.startColor = color; main.loop = false; main.duration = .25f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            var material = MaterialParticula(color, false);
            renderer.sharedMaterial = material;
            root.AddComponent<OwnedMaterial>().material = material;
            ps.Play(); ps.Emit(18); Object.Destroy(root, .6f);
        }
    }
    public class Clarao : MonoBehaviour
    {
        Light luz; float inicial;
        void Awake() { luz = GetComponent<Light>(); inicial = luz ? luz.intensity : 0; }
        void Update() { if (luz) luz.intensity = Mathf.MoveTowards(luz.intensity, 0, inicial * Time.unscaledDeltaTime / .1f); }
    }
    public class OwnedMaterial : MonoBehaviour
    {
        public Material material;
        void OnDestroy() { if (material) Destroy(material); }
    }
}
