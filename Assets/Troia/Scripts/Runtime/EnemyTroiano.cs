using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    // Troianos simples: corpo low-poly da Quaternius (humanoide, ~14k triângulos) com equipamento leve preso aos ossos.
    // O Pátroclo é só a referência de proporção e paleta; os troianos não usam a panóplia grega dele.
    public static class EnemyTroiano
    {
        public struct Modelo { public Transform raiz; public Animator animador; public Transform dardo; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly Color Pele = new Color(.62f, .45f, .34f), Couro = new Color(.28f, .17f, .09f), Ferro = new Color(.6f, .58f, .55f);
        // Materiais e malhas são de todos os troianos; o brilho de cada um vai por MaterialPropertyBlock.
        static readonly Dictionary<Color, Material> materiais = new Dictionary<Color, Material>();
        static readonly Dictionary<string, Mesh> malhas = new Dictionary<string, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpar() { materiais.Clear(); malhas.Clear(); }

        // Referencial da montagem (pose de ligação do boneco), válido durante Criar.
        static Vector3 cima, frente, lado;
        static Quaternion giro;
        static float escala;

        public static bool Disponivel(EnemySO data)
        {
            if (!data || !data.corpo || !PatrocloAnimacoesSO.Carregar()) return false;
            var animador = data.corpo.GetComponent<Animator>();
            return animador && animador.avatar && animador.avatar.isHuman;
        }

        public static Modelo Criar(Transform pai, EnemySO data)
        {
            var go = Object.Instantiate(data.corpo, pai);
            go.name = "Troiano_" + data.name;
            var raiz = go.transform;
            raiz.localPosition = Vector3.zero; raiz.localRotation = Quaternion.identity; raiz.localScale = Vector3.one * data.scale;
            var m = new Modelo { raiz = raiz, animador = go.GetComponent<Animator>() };
            m.animador.applyRootMotion = false;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Mat(Pele, 0);
                r.sharedMaterials = mats;
            }
            m.dardo = Vestir(m, data);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            return m;
        }

        static Transform Vestir(Modelo m, EnemySO e)
        {
            var a = m.animador;
            Transform quadril = a.GetBoneTransform(HumanBodyBones.Hips);
            Transform peito = a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest);
            Transform cabeca = a.GetBoneTransform(HumanBodyBones.Head), maoD = a.GetBoneTransform(HumanBodyBones.RightHand);
            Transform anteE = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), maoE = a.GetBoneTransform(HumanBodyBones.LeftHand);
            if (!quadril || !peito || !cabeca || !maoD) return null;

            // Antes de animar, o boneco da Quaternius olha para -Z: a frente vem do pé para os dedos.
            cima = m.raiz.up; escala = m.raiz.lossyScale.y;
            var pe = a.GetBoneTransform(HumanBodyBones.RightFoot); var dedos = a.GetBoneTransform(HumanBodyBones.RightToes);
            frente = pe && dedos ? Vector3.ProjectOnPlane(dedos.position - pe.position, cima).normalized : -m.raiz.forward;
            if (frente.sqrMagnitude < .5f) frente = -m.raiz.forward;
            lado = Vector3.Cross(cima, frente); giro = Quaternion.LookRotation(frente, cima);

            bool heitor = e.look == EnemyLook.Hector, oficial = heitor || e.look == EnemyLook.Captain, arqueiro = e.look == EnemyLook.Archer;
            Color escuro = Tom(e.tunic, .7f), couraca = arqueiro ? Couro : e.armor;
            float metal = arqueiro ? 0 : .6f;

            // túnica curta, couraça lisa e cinto com tiras de couro
            Loft(quadril, "Saia", Em(quadril, 0), e.tunic, 0, new Vector3(-.25f, .2f, .17f), new Vector3(-.12f, .19f, .155f), new Vector3(.04f, .175f, .14f));
            Loft(peito, arqueiro ? "Colete" : "Couraca", Em(peito, 0), couraca, metal,
                new Vector3(-.38f, .17f, .14f), new Vector3(-.2f, .185f, .15f), new Vector3(-.02f, .2f, .155f), new Vector3(.1f, .185f, .13f), new Vector3(.16f, .1f, .08f));
            Loft(quadril, "Cinto", Em(quadril, 0), Couro, 0, new Vector3(.03f, .187f, .152f), new Vector3(.09f, .187f, .152f));
            for (int i = 0; i < 5; i++)
                Peca(quadril, PrimitiveType.Cube, Em(quadril, -.07f, .165f, (i - 2) * .075f), giro * Quaternion.Euler(-8, 0, 0), new Vector3(.06f, .14f, .015f), i % 2 == 0 ? Couro : escuro);
            if (!arqueiro)
                foreach (var osso in new[] { HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftLowerLeg })
                {
                    var canela = a.GetBoneTransform(osso);
                    if (canela) Loft(canela, "Cnemide", Em(canela, 0), e.armor, metal, new Vector3(-.4f, .058f, .062f), new Vector3(-.22f, .066f, .072f), new Vector3(-.04f, .055f, .06f));
                }

            if (arqueiro)
            {
                // gorro frígio de pano
                Loft(cabeca, "Gorro", Em(cabeca, 0), e.tunic, 0, new Vector3(.02f, .13f, .145f), new Vector3(.13f, .14f, .155f), new Vector3(.22f, .115f, .125f), new Vector3(.29f, .05f, .06f));
                Peca(cabeca, PrimitiveType.Sphere, Em(cabeca, .29f, .05f), giro * Quaternion.Euler(-35, 0, 0), new Vector3(.09f, .1f, .15f), e.tunic);
                for (int s = -1; s <= 1; s += 2) Peca(cabeca, PrimitiveType.Cube, Em(cabeca, -.04f, -.01f, s * .135f), giro, new Vector3(.025f, .16f, .12f), e.tunic);
            }
            else
            {
                // elmo frígio: calota alta curvada para a frente e abas nas faces (sem a máscara fechada do coríntio)
                Loft(cabeca, "Elmo", Em(cabeca, 0), e.armor, .7f, new Vector3(.03f, .135f, .15f), new Vector3(.12f, .142f, .157f), new Vector3(.21f, .12f, .13f), new Vector3(.27f, .065f, .075f), new Vector3(.3f, .01f, .01f));
                Peca(cabeca, PrimitiveType.Sphere, Em(cabeca, .27f, .06f), giro * Quaternion.Euler(-40, 0, 0), new Vector3(.08f, .09f, .14f), e.armor, .7f);
                for (int s = -1; s <= 1; s += 2) Peca(cabeca, PrimitiveType.Cube, Em(cabeca, -.05f, .04f, s * .13f), giro * Quaternion.Euler(0, -s * 12, 0), new Vector3(.02f, .14f, .12f), e.armor, .7f);
                Peca(cabeca, PrimitiveType.Cube, Em(cabeca, -.02f, -.13f), giro, new Vector3(.22f, .1f, .02f), e.armor, .7f);
                if (oficial)
                {
                    float alto = heitor ? .16f : .1f;
                    for (int i = 0; i < 7; i++)
                    {
                        float t = i / 6f, ang = Mathf.Lerp(-30, 120, t) * Mathf.Deg2Rad;
                        Peca(cabeca, PrimitiveType.Cube, Em(cabeca, .28f + Mathf.Cos(ang) * .03f + alto * .5f * Mathf.Sin(t * Mathf.PI), .02f - Mathf.Sin(ang) * .14f),
                            giro * Quaternion.Euler(Mathf.Lerp(-30, 60, t), 0, 0), new Vector3(.03f, alto * (1 - Mathf.Abs(t - .4f)), .06f), e.accent);
                    }
                }
            }

            if (oficial && anteE)
            {
                // escudo redondo no antebraço; na pose T o dorso da mão aponta para cima, que vira a face do escudo
                Vector3 meio = maoE ? Vector3.Lerp(anteE.position, maoE.position, .5f) : anteE.position;
                float d = heitor ? .78f : .66f;
                Peca(anteE, PrimitiveType.Cylinder, meio + cima * .05f * escala, giro, new Vector3(d, .02f, d), e.armor, .6f);
                Peca(anteE, PrimitiveType.Cylinder, meio + cima * .062f * escala, giro, new Vector3(d * .8f, .02f, d * .8f), e.accent);
                Peca(anteE, PrimitiveType.Cylinder, meio + cima * .074f * escala, giro, new Vector3(d * .62f, .02f, d * .62f), e.armor, .6f);
                Peca(anteE, PrimitiveType.Sphere, meio + cima * .085f * escala, giro, new Vector3(.12f, .07f, .12f), e.accent, .6f);
            }
            if (heitor) Peca(peito, PrimitiveType.Cube, Em(peito, -.42f, -.17f), giro * Quaternion.Euler(8, 0, 0), new Vector3(.42f, 1f, .02f), escuro);

            // pega medida nos clipes de espada da Quaternius: a lâmina sai do punho fechado, do mínimo para o indicador
            Vector3 punho = maoD.TransformPoint(new Vector3(0, .067f, .003f));
            Vector3 lamina = maoD.TransformDirection(new Vector3(.006f, .168f, .986f)).normalized;
            if (!arqueiro)
            {
                float comp = heitor ? .95f : .75f;
                var rot = Quaternion.LookRotation(lamina, maoD.up);
                Peca(maoD, PrimitiveType.Cube, punho + lamina * (.08f + comp * .5f) * escala, rot, new Vector3(.055f, .012f, comp), Ferro, .8f);
                Peca(maoD, PrimitiveType.Cube, punho + lamina * .07f * escala, rot, new Vector3(.2f, .035f, .04f), oficial ? e.accent : Couro, .5f);
                Peca(maoD, PrimitiveType.Cube, punho - lamina * .02f * escala, rot, new Vector3(.035f, .035f, .14f), Couro);
                return null;
            }
            // dardo: pego a 35% do comprimento, ponta para o lado do polegar invertido (arremesso por cima)
            var dardo = new GameObject("Dardo").transform;
            dardo.SetPositionAndRotation(punho, Quaternion.LookRotation(-lamina, maoD.up));
            Peca(dardo, PrimitiveType.Cylinder, punho - lamina * .35f * escala, Quaternion.LookRotation(-lamina, maoD.up) * Quaternion.Euler(90, 0, 0), new Vector3(.025f, .75f, .025f), Couro);
            Peca(dardo, PrimitiveType.Cube, punho - lamina * 1.12f * escala, Quaternion.LookRotation(-lamina, maoD.up), new Vector3(.04f, .02f, .16f), Ferro, .8f);
            dardo.SetParent(maoD, true);
            return dardo;
        }

        static Vector3 Em(Transform osso, float y, float z = 0, float x = 0) => osso.position + (cima * y + frente * z + lado * x) * escala;

        static Transform Peca(Transform pai, PrimitiveType forma, Vector3 pos, Quaternion rot, Vector3 tam, Color cor, float metal = 0)
        {
            var go = GameObject.CreatePrimitive(forma); go.name = forma.ToString();
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Mat(cor, metal);
            return Prender(go.transform, pai, pos, rot, tam);
        }

        static Transform Loft(Transform osso, string nome, Vector3 pos, Color cor, float metal, params Vector3[] aneis)
        {
            var chave = new System.Text.StringBuilder(nome);
            foreach (var a in aneis) chave.Append('|').Append(a.x.ToString("F3")).Append(';').Append(a.y.ToString("F3")).Append(';').Append(a.z.ToString("F3"));
            if (!malhas.TryGetValue(chave.ToString(), out var malha) || !malha) malhas[chave.ToString()] = malha = MalhaLoft(nome, aneis);
            var go = new GameObject(nome, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = malha;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(cor, metal);
            return Prender(go.transform, osso, pos, giro, Vector3.one);
        }

        // Monta no espaço do mundo e prende ao osso mantendo a pose: não depende dos eixos de cada osso.
        static Transform Prender(Transform t, Transform pai, Vector3 pos, Quaternion rot, Vector3 tam)
        {
            t.SetPositionAndRotation(pos, rot); t.localScale = tam * escala; t.SetParent(pai, true); return t;
        }

        // Cada anel: altura, raio de lado a lado e raio de frente a trás.
        static Mesh MalhaLoft(string nome, Vector3[] aneis)
        {
            const int lados = 10;
            var v = new List<Vector3>(); var tri = new List<int>();
            foreach (var anel in aneis)
                for (int j = 0; j < lados; j++)
                {
                    float ang = j * Mathf.PI * 2 / lados;
                    v.Add(new Vector3(Mathf.Cos(ang) * anel.y, anel.x, Mathf.Sin(ang) * anel.z));
                }
            for (int r = 0; r < aneis.Length - 1; r++)
                for (int j = 0; j < lados; j++)
                {
                    int a = r * lados + j, b = r * lados + (j + 1) % lados;
                    tri.AddRange(new[] { a, a + lados, b, b, a + lados, b + lados });
                }
            int fim = (aneis.Length - 1) * lados;
            for (int j = 1; j < lados - 1; j++) { tri.AddRange(new[] { 0, j, j + 1 }); tri.AddRange(new[] { fim, fim + j + 1, fim + j }); }
            var m = new Mesh { name = nome }; m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Material Mat(Color cor, float metal)
        {
            var chave = new Color(cor.r, cor.g, cor.b, metal);   // o alfa da chave guarda o metal
            if (materiais.TryGetValue(chave, out var m) && m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Troiano_" + ColorUtility.ToHtmlStringRGB(cor) };
            m.SetColor(BaseColorId, cor); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", metal > 0 ? .55f : .25f);
            m.EnableKeyword("_EMISSION"); m.SetColor(EmissionId, Color.black);
            return materiais[chave] = m;
        }

        static Color Tom(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1);
    }
}
