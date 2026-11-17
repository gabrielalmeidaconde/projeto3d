using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>
    /// Monta o Avatar humanoide do Pátroclo (esqueleto do MakeHuman) para ele poder tocar clipes humanoides
    /// por retargeting. O esqueleto está em pose A; o Unity precisa da pose T, então ela é construída aqui.
    /// </summary>
    public static class PatrocloAvatar
    {
        public const string Caminho = "Assets/Troia/Personagens/Patroclo/Patroclo_Avatar.asset";

        // osso humano do Unity -> osso do MakeHuman
        static readonly (string humano, string osso)[] Mapa =
        {
            ("Hips", "root"), ("Spine", "spine04"), ("Chest", "spine02"), ("UpperChest", "spine01"), ("Neck", "neck01"), ("Head", "head"),
            ("LeftShoulder", "clavicle.L"), ("LeftUpperArm", "upperarm01.L"), ("LeftLowerArm", "lowerarm01.L"), ("LeftHand", "wrist.L"),
            ("RightShoulder", "clavicle.R"), ("RightUpperArm", "upperarm01.R"), ("RightLowerArm", "lowerarm01.R"), ("RightHand", "wrist.R"),
            ("LeftUpperLeg", "upperleg01.L"), ("LeftLowerLeg", "lowerleg01.L"), ("LeftFoot", "foot.L"),
            ("RightUpperLeg", "upperleg01.R"), ("RightLowerLeg", "lowerleg01.R"), ("RightFoot", "foot.R"),
        };

        [MenuItem("Troia/Personagem/Gerar Avatar humanoide", priority = 46)]
        public static void Gerar()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PatrocloJogo.Prefab));
            go.name = "Patroclo";
            try
            {
                var avatar = Construir(go);
                if (!avatar || !avatar.isValid || !avatar.isHuman) { Debug.LogError("Avatar do Pátroclo inválido"); return; }
                var antigo = AssetDatabase.LoadAssetAtPath<Avatar>(Caminho);
                if (antigo) AssetDatabase.DeleteAsset(Caminho);
                AssetDatabase.CreateAsset(avatar, Caminho); AssetDatabase.SaveAssets();
                Debug.Log($"Avatar do Pátroclo gerado: humano={avatar.isHuman} válido={avatar.isValid}");
            }
            finally { Object.DestroyImmediate(go); }
        }

        public static Avatar Carregar() => AssetDatabase.LoadAssetAtPath<Avatar>(Caminho);

        /// <summary>Põe a instância em pose T (a partir do repouso em A) e constrói o Avatar.</summary>
        public static Avatar Construir(GameObject go)
        {
            var design = go.GetComponent<PatrocloDesign>(); design.pose = PosePatroclo.Repouso; design.Apply(); design.pose = PosePatroclo.Livre;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var o = new Dictionary<string, Transform>();
            foreach (var b in design.refs.ossos) if (b) o[b.name] = b;
            // dedos esticados: a pose T fica limpa (eles não entram no mapa humano; o jogo fecha as mãos por conta própria)
            if (design.refs.dedos != null) foreach (var d in design.refs.dedos) if (d) d.localRotation = Quaternion.identity;
            PoseT(o);

            var esqueleto = new List<SkeletonBone> { Osso(go.transform) };
            foreach (var t in design.refs.raiz.GetComponentsInChildren<Transform>(true))
                if (o.TryGetValue(t.name, out var mesmo) && mesmo == t) esqueleto.Add(Osso(t));
            var humanos = new List<HumanBone>();
            foreach (var (humano, osso) in Mapa)
            {
                if (!o.ContainsKey(osso)) { Debug.LogError("Osso ausente: " + osso); return null; }
                humanos.Add(new HumanBone { humanName = humano, boneName = osso, limit = new HumanLimit { useDefaultValues = true } });
            }
            var desc = new HumanDescription
            {
                human = humanos.ToArray(), skeleton = esqueleto.ToArray(),
                upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                armStretch = .05f, legStretch = .05f, feetSpacing = 0, hasTranslationDoF = false
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(go, desc);
            avatar.name = "Patroclo_Avatar";
            return avatar;
        }
        static SkeletonBone Osso(Transform t) => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale };

        static void Alinhar(Transform osso, Vector3 de, Vector3 para)
        {
            if (de.sqrMagnitude < 1e-8f) return;
            osso.rotation = Quaternion.FromToRotation(de, para) * osso.rotation;
        }
        public static void PoseT(Dictionary<string, Transform> o)
        {
            foreach (string s in new[] { "L", "R" })
            {
                float sx = s == "L" ? -1 : 1;
                Transform braco = o["upperarm01." + s], ante = o["lowerarm01." + s], pulso = o["wrist." + s];
                // levantar o braço de lado (abdução) deixa a palma para baixo e a dobra do cotovelo para a frente
                Alinhar(braco, ante.position - braco.position, Vector3.right * sx);
                Alinhar(ante, pulso.position - ante.position, Vector3.right * sx);
                Vector3 nos = Vector3.zero; int n = 0;
                for (int f = 2; f <= 5; f++) if (o.TryGetValue($"finger{f}-1.{s}", out var k)) { nos += k.position; n++; }
                if (n > 0 && o.ContainsKey($"finger2-1.{s}") && o.ContainsKey($"finger5-1.{s}"))
                {
                    Vector3 dedos = nos / n - pulso.position, polegar = o[$"finger2-1.{s}"].position - o[$"finger5-1.{s}"].position;
                    Quaternion atual = Quaternion.LookRotation(dedos, polegar), alvo = Quaternion.LookRotation(Vector3.right * sx, Vector3.forward);
                    pulso.rotation = alvo * Quaternion.Inverse(atual) * pulso.rotation;
                }
                Transform coxa = o["upperleg01." + s], canela = o["lowerleg01." + s], pe = o["foot." + s];
                Alinhar(coxa, canela.position - coxa.position, Vector3.down);
                Alinhar(canela, pe.position - canela.position, Vector3.down);
                Vector3 ponta = Vector3.zero; n = 0;
                for (int f = 1; f <= 5; f++) if (o.TryGetValue($"toe{f}-1.{s}", out var k)) { ponta += k.position; n++; }
                if (n > 0) { Vector3 frente = ponta / n - pe.position; frente.y = 0; Alinhar(pe, frente, Vector3.forward); }
            }
        }

        // ------------------------------------------------------------------ conferência
        /// <summary>Fotografa a pose T construída e algumas poses por músculos, para conferir o mapeamento.</summary>
        public static void Conferir()
        {
            var avatar = Carregar();
            if (!avatar) { Debug.LogError("Gere o avatar antes"); return; }
            var est = new PatrocloEstudio("frente", 480);
            var go = est.modelo;
            var o = new Dictionary<string, Transform>();
            foreach (var b in est.design.refs.ossos) if (b) o[b.name] = b;
            est.design.pose = PosePatroclo.Repouso; est.design.Apply(); est.design.pose = PosePatroclo.Livre;
            est.Folha(4, 2);
            est.Celula(0, 0);                                   // repouso em A
            PoseT(o); est.Celula(1, 0);                         // pose T construída
            var anim = go.AddComponent<Animator>(); anim.avatar = avatar;
            var mao = new HumanPoseHandler(avatar, go.transform);
            var pose = new HumanPose();
            mao.GetHumanPose(ref pose);
            var sb = new System.Text.StringBuilder("musculos na pose T (deveriam ser ~0 nos braços e pernas):\n");
            for (int i = 0; i < pose.muscles.Length; i++) if (Mathf.Abs(pose.muscles[i]) > .08f) sb.AppendLine($"  {HumanTrait.MuscleName[i]} = {pose.muscles[i]:0.00}");
            sb.AppendLine($"corpo {pose.bodyPosition} rot {pose.bodyRotation.eulerAngles}");
            Debug.Log(sb.ToString());
            Vector3 corpo = pose.bodyPosition; Quaternion giro = pose.bodyRotation;
            void Posar(System.Action<float[]> f)
            {
                var p = new HumanPose { bodyPosition = corpo, bodyRotation = giro, muscles = new float[HumanTrait.MuscleCount] };
                f(p.muscles); mao.SetHumanPose(ref p);
            }
            int M(string nome) { int i = System.Array.IndexOf(HumanTrait.MuscleName, nome); if (i < 0) Debug.LogError("músculo? " + nome); return Mathf.Max(i, 0); }
            Posar(m => { }); est.Celula(2, 0);                  // todos os músculos em zero
            Posar(m => { m[M("Left Arm Down-Up")] = -.9f; m[M("Right Arm Down-Up")] = -.9f; }); est.Celula(3, 0);
            Posar(m => { m[M("Right Arm Down-Up")] = .2f; m[M("Right Arm Front-Back")] = .8f; m[M("Right Forearm Stretch")] = -.6f; }); est.Celula(0, 1);
            Posar(m => { m[M("Spine Twist Left-Right")] = .8f; m[M("Chest Twist Left-Right")] = .8f; m[M("Head Nod Down-Up")] = -.5f; }); est.Celula(1, 1);
            Posar(m => { m[M("Left Upper Leg Front-Back")] = -.7f; m[M("Left Lower Leg Stretch")] = -.5f; m[M("Right Upper Leg Front-Back")] = .4f; }); est.Celula(2, 1);
            Posar(m => { m[M("Spine Front-Back")] = -.8f; m[M("Chest Front-Back")] = -.8f; m[M("Left Shoulder Down-Up")] = .8f; m[M("Left Arm Down-Up")] = .8f; }); est.Celula(3, 1);
            est.Salvar("Capturas/Golpes/avatar_conferencia.jpg");
            est.Fechar();
        }
    }
}
