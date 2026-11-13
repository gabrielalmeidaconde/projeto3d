using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    /// <summary>Livre = o painel não mexe nos ossos (para posar à mão).</summary>
    public enum PosePatroclo { Repouso, Neutra, Combate, Livre }
    public enum NotaEstilo { D, C, B, A, S, SS, SSS }
    public enum Bencao { Nenhuma, Zeus, Ares, Apolo, Hermes, Atena, Poseidon }
    public enum PosicaoEscudo { Costas, Braco }

    /// <summary>
    /// Painel de design do Pátroclo. Mexa nos campos do Inspector e veja o personagem mudar
    /// na hora, no editor: paleta (tinge os materiais dele), peças, pose e prévias do jogo
    /// (favor divino, nota de estilo e marcas das bênçãos). Não tem lógica de combate.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PatrocloDesign : MonoBehaviour
    {
        [Header("Paleta (tinge as texturas dos materiais)")]
        public Color bronze = new Color32(0xA2, 0x6A, 0x3A, 0xFF);
        public Color bronzeEscuro = new Color32(0x5E, 0x3A, 0x1E, 0xFF);
        public Color ouro = new Color32(0xF2, 0xC6, 0x5C, 0xFF);
        public Color lamina = new Color32(0xD8, 0xAA, 0x5F, 0xFF);
        [Tooltip("Crista e rabo de crina do elmo.")]
        public Color carmesim = new Color32(0xB0, 0x1C, 0x22, 0xFF);
        [Tooltip("Capa de lã.")]
        public Color carmesimEscuro = new Color32(0xA2, 0x1E, 0x22, 0xFF);
        [Tooltip("Linho do corselete (linotórax), das epômides e da saia.")]
        public Color linho = new Color32(0xD6, 0xC6, 0xA2, 0xFF);
        public Color couro = new Color32(0xF2, 0xE6, 0xD9, 0xFF);
        [Tooltip("Tinge a textura da pele (que já tem cor): quase branco = tom natural bronzeado.")]
        public Color pele = new Color32(0xE4, 0xD6, 0xCC, 0xFF);
        [Tooltip("Tinge a textura do cabelo (que é quase preta): cores claras dão castanho escuro.")]
        public Color cabelo = new Color32(0xE0, 0xA8, 0x7C, 0xFF);
        public Color emblema = new Color32(0x6E, 0x10, 0x15, 0xFF);

        [Header("Peças")]
        [Tooltip("Desligue para ver o corpo e a musculatura sem couraça, ombreiras, braçadeiras, cnêmides e ptéruges.")]
        public bool armadura = true;
        public bool elmo = true;
        public bool crista = true;
        public bool capa = true;
        public PosicaoEscudo escudo = PosicaoEscudo.Costas;

        [Header("Desgaste de batalha")]
        [Tooltip("Lama e poeira subindo dos pés, pó nas frestas da armadura e manchas no tecido e na pele.")]
        [Range(0f, 1f)] public float sujeira = 0.7f;
        [Tooltip("Respingos e manchas de sangue seco (mais na ponta da lâmina).")]
        [Range(0f, 1f)] public float sangue = 0.2f;

        [Header("Pose")]
        public PosePatroclo pose = PosePatroclo.Neutra;

        [Header("Prévia do jogo")]
        [Tooltip("As gravações em ouro da couraça acendem de baixo para cima conforme o favor enche.")]
        [Range(0f, 1f)] public float favorDivino = 0f;
        public NotaEstilo notaDeEstilo = NotaEstilo.D;
        public Bencao bencao = Bencao.Nenhuma;

        [System.Serializable]
        public class Referencias
        {
            public Material bronze, bronzeEscuro, ouro, lamina, carmesim, carmesimEscuro, linho, couro, pele, cabelo, emblema, madeira;
            public GameObject elmo, crista, raboCostas, raboEscudo, capa, escudoCostas, escudoBraco, chamaCrista, cabeloSolto;
            public Renderer[] gravacoes1, gravacoes2, gravacoes3, crista_r, lamina_r, emblema_r, ombreiras_r, marcas_r;
            public GameObject[] marcasZeus, marcasAres, marcasApolo, marcasHermes, marcasAtena, marcasPoseidon;
            [Tooltip("Peças escondidas quando 'armadura' está desligada.")]
            public GameObject[] armaduraCorpo;
            [Tooltip("Mostradas só com a armadura desligada: corpo inteiro e saia com a oclusão própria.")]
            public GameObject[] semArmadura;
            public Transform[] ossos;
            public Transform raiz;
            public Vector3 posicaoRaiz;
            [Tooltip("Ossos dos dedos, com o eixo de dobra (no repouso) e o ângulo com a mão fechada.")]
            public Transform[] dedos;
            public Vector3[] eixosDedos;
            public float[] dobraDedos;
            public bool[] dedoDaMaoDireita;
        }

        [HideInInspector] public Referencias refs = new Referencias();

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // os shaders Troia/Personagem e Troia/Pele usam estes dois campos do Lit para o desgaste
        static readonly int DirtId = Shader.PropertyToID("_OcclusionStrength");
        static readonly int BloodId = Shader.PropertyToID("_Parallax");
        MaterialPropertyBlock _mpb;

        // Rotações locais (graus) por pose, com os nomes de ossos do MakeHuman.
        // No repouso (pose A) todos os ossos têm rotação zero e eixos iguais aos do mundo:
        // X = direita do personagem, Y = cima, Z = frente.
        static readonly Dictionary<string, Vector3> PoseNeutra = new Dictionary<string, Vector3>
        {
            // braço direito: espada grande apoiada no ombro (cotovelo aberto para o lado, mão à frente do ombro)
            { "upperarm01.R", new Vector3(30.6f, 3.5f, -5.6f) }, { "lowerarm01.R", new Vector3(-42.6f, -66.5f, 34.2f) },
            { "lowerarm02.R", new Vector3(-4.2f, 4.5f, -6.5f) }, { "wrist.R", new Vector3(-31.6f, -34.1f, -16.4f) },
            { "upperarm01.L", new Vector3(0f, 0f, 33f) }, { "lowerarm01.L", new Vector3(0f, 0f, 0f) },
            { "upperleg01.R", new Vector3(0f, 0f, -2.5f) }, { "upperleg01.L", new Vector3(0f, 0f, 2.5f) },
            { "foot.R", new Vector3(0f, 0f, 2.5f) }, { "foot.L", new Vector3(0f, 0f, -2.5f) },
            { "neck01", new Vector3(-2f, 4f, 0f) }, { "head", new Vector3(-2f, 4f, 0f) },
        };

        static readonly Dictionary<string, Vector3> PoseCombate = new Dictionary<string, Vector3>
        {
            { "spine05", new Vector3(0f, 12f, 0f) }, { "spine03", new Vector3(6f, 8f, 0f) }, { "spine01", new Vector3(4f, 6f, 0f) },
            { "neck01", new Vector3(0f, -10f, 0f) }, { "head", new Vector3(-4f, -10f, 0f) },
            { "upperarm01.L", new Vector3(-55f, 25f, 40f) }, { "lowerarm01.L", new Vector3(0f, -70f, 0f) },
            { "upperarm01.R", new Vector3(-20f, -30f, 5f) }, { "lowerarm01.R", new Vector3(-30f, 0f, 0f) }, { "wrist.R", new Vector3(0f, 0f, 0f) },
            { "upperleg01.L", new Vector3(-30f, -8f, 4f) }, { "lowerleg01.L", new Vector3(32f, 0f, 0f) }, { "foot.L", new Vector3(-4f, 0f, 0f) },
            { "upperleg01.R", new Vector3(16f, 12f, -6f) }, { "lowerleg01.R", new Vector3(22f, 0f, 0f) }, { "foot.R", new Vector3(-20f, 0f, 0f) },
        };

        void OnEnable() => Apply();

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= DelayedApply;
            UnityEditor.EditorApplication.delayCall += DelayedApply;
#else
            Apply();
#endif
        }

        void DelayedApply()
        {
            if (this != null) Apply();
        }

        /// <summary>Aplica paleta, peças, pose e prévias. Chamado automaticamente ao mexer no Inspector.</summary>
        public void Apply()
        {
            if (refs == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            SetColor(refs.bronze, bronze);
            SetColor(refs.bronzeEscuro, bronzeEscuro);
            SetColor(refs.ouro, ouro);
            SetColor(refs.lamina, lamina);
            SetColor(refs.carmesim, carmesim);
            SetColor(refs.carmesimEscuro, carmesimEscuro);
            SetColor(refs.linho, linho);
            SetColor(refs.couro, couro);
            SetColor(refs.pele, pele);
            SetColor(refs.cabelo, cabelo);
            SetColor(refs.emblema, emblema);

            // desgaste: o ouro suja menos; a lâmina é limpa com frequência, mas é onde fica o sangue
            SetWear(refs.bronze, 1f, 1f);
            SetWear(refs.bronzeEscuro, 1f, 1f);
            SetWear(refs.ouro, 0.55f, 0.8f);
            SetWear(refs.lamina, 0.45f, 2.2f);
            SetWear(refs.carmesim, 0.7f, 0.6f);
            SetWear(refs.carmesimEscuro, 1f, 0.8f);
            SetWear(refs.linho, 1f, 1f);
            SetWear(refs.couro, 1f, 0.8f);
            SetWear(refs.pele, 0.8f, 0.9f);
            SetWear(refs.emblema, 0.8f, 0.8f);
            SetWear(refs.madeira, 1f, 0.6f);

            bool costas = escudo == PosicaoEscudo.Costas;
            Show(refs.elmo, elmo);
            Show(refs.cabeloSolto, !elmo);
            Show(refs.crista, elmo && crista);
            Show(refs.raboEscudo, elmo && crista && costas);
            Show(refs.raboCostas, elmo && crista && !costas);
            Show(refs.capa, capa);
            if (refs.armaduraCorpo != null)
                foreach (var g in refs.armaduraCorpo) Show(g, armadura);
            if (refs.semArmadura != null)
                foreach (var g in refs.semArmadura) Show(g, !armadura);
            Show(refs.escudoCostas, costas);
            Show(refs.escudoBraco, !costas);

            ApplyPose();

            // favor divino e nota de estilo
            int rank = (int)notaDeEstilo;
            var gold = new Color(1f, 0.7f, 0.25f);
            bool s = rank >= (int)NotaEstilo.S;
            Emit(refs.gravacoes1, gold * (s ? 4f : Mathf.InverseLerp(0.02f, 0.33f, favorDivino) * 2.6f));
            Emit(refs.gravacoes2, gold * (s ? 4f : Mathf.InverseLerp(0.36f, 0.66f, favorDivino) * 2.6f));
            Emit(refs.gravacoes3, gold * (s ? 4f : Mathf.InverseLerp(0.69f, 1f, favorDivino) * 2.6f));
            Emit(refs.crista_r, rank >= (int)NotaEstilo.B ? new Color(1f, 0.16f, 0.08f) * (rank >= (int)NotaEstilo.SSS ? 3.2f : 1.3f) : Color.black);
            Emit(refs.emblema_r, rank >= (int)NotaEstilo.SS ? new Color(1f, 0.32f, 0.12f) * 3f : Color.black);
            Show(refs.chamaCrista, rank >= (int)NotaEstilo.SSS && elmo && crista);

            Color blade = Color.black;
            if (rank >= (int)NotaEstilo.SSS) blade = gold * 2.5f;
            else if (rank >= (int)NotaEstilo.A) blade = new Color(1f, 0.82f, 0.55f) * 0.7f;

            // bênçãos
            Color mark = MarkColor(bencao);
            SetMarks(refs.marcasZeus, bencao == Bencao.Zeus);
            SetMarks(refs.marcasAres, bencao == Bencao.Ares && armadura);
            SetMarks(refs.marcasApolo, bencao == Bencao.Apolo && elmo);
            SetMarks(refs.marcasHermes, bencao == Bencao.Hermes && armadura);
            SetMarks(refs.marcasAtena, bencao == Bencao.Atena);
            SetMarks(refs.marcasPoseidon, bencao == Bencao.Poseidon);
            Emit(refs.marcas_r, mark);
            if (bencao == Bencao.Zeus || bencao == Bencao.Poseidon) blade = mark * 0.06f + blade * 0.25f;
            Emit(refs.lamina_r, blade);
            Emit(refs.ombreiras_r, bencao == Bencao.Ares ? mark * 0.8f : Color.black);
        }

        static Color MarkColor(Bencao b)
        {
            switch (b)
            {
                case Bencao.Zeus: return new Color(0.55f, 0.82f, 1f) * 6f;
                case Bencao.Ares: return new Color(1f, 0.29f, 0.18f) * 3f;
                case Bencao.Apolo: return new Color(1f, 0.82f, 0.36f) * 3f;
                case Bencao.Hermes: return new Color(0.49f, 0.92f, 0.78f) * 3f;
                case Bencao.Atena: return new Color(0.78f, 0.82f, 1f) * 3f;
                case Bencao.Poseidon: return new Color(0.17f, 0.71f, 0.78f) * 4f;
                default: return Color.black;
            }
        }

        void ApplyPose()
        {
            if (refs.ossos == null || pose == PosePatroclo.Livre) return;
            Dictionary<string, Vector3> table = pose == PosePatroclo.Neutra ? PoseNeutra : pose == PosePatroclo.Combate ? PoseCombate : null;
            foreach (var bone in refs.ossos)
            {
                if (bone == null) continue;
                Vector3 e = Vector3.zero;
                if (table != null) table.TryGetValue(bone.name, out e);
                bone.localRotation = Quaternion.Euler(e);
            }
            if (refs.raiz != null)
                refs.raiz.localPosition = refs.posicaoRaiz + (pose == PosePatroclo.Combate ? new Vector3(0f, -0.075f, 0f) : Vector3.zero);

            // mãos: a direita segura a espada; a esquerda fecha na alça do escudo em combate
            float right = 1f;
            float left = pose == PosePatroclo.Combate && escudo == PosicaoEscudo.Braco ? 1f : pose == PosePatroclo.Repouso ? 0.15f : 0.35f;
            if (refs.dedos == null || refs.eixosDedos == null || refs.dobraDedos == null || refs.dedoDaMaoDireita == null) return;
            for (int i = 0; i < refs.dedos.Length; i++)
            {
                if (refs.dedos[i] == null) continue;
                float k = refs.dedoDaMaoDireita[i] ? right : left;
                refs.dedos[i].localRotation = Quaternion.AngleAxis(refs.dobraDedos[i] * k, refs.eixosDedos[i]);
            }
        }

        void SetWear(Material m, float dirtScale, float bloodScale)
        {
            if (m == null) return;
            float d = sujeira * dirtScale, b = sangue * bloodScale;
            if (m.HasProperty(DirtId) && !Mathf.Approximately(m.GetFloat(DirtId), d)) m.SetFloat(DirtId, d);
            if (m.HasProperty(BloodId) && !Mathf.Approximately(m.GetFloat(BloodId), b)) m.SetFloat(BloodId, b);
        }

        static void SetColor(Material m, Color c)
        {
            if (m != null && m.GetColor(BaseColorId) != c) m.SetColor(BaseColorId, c);
        }

        static void Show(GameObject go, bool v)
        {
            if (go != null && go.activeSelf != v) go.SetActive(v);
        }

        static void SetMarks(GameObject[] gos, bool v)
        {
            if (gos == null) return;
            foreach (var g in gos) Show(g, v);
        }

        void Emit(Renderer[] rs, Color c)
        {
            if (rs == null) return;
            foreach (var r in rs)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
