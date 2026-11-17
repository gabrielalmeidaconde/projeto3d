using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Biblioteca de animação do Pátroclo: o Avatar humanoide dele, os clipes de locomoção por nome e, para cada golpe
    /// (id do ComboSO), o trecho de clipe que o representa. Fica em Resources/Troia/Animacoes.
    /// </summary>
    public class PatrocloAnimacoesSO : ScriptableObject
    {
        [System.Serializable]
        public class Trecho
        {
            public string id;
            public AnimationClip clip;
            [Tooltip("Início, instante do impacto e fim dentro do clipe (s).")]
            public float inicio, impacto, fim;
            [Tooltip("Estocada de lança com o braço guiado por IK: 0 não, 1 baixa, 2 alta.")]
            public int estocada;
            [Tooltip("Graus que o bote do clipe sai fora da frente do herói (o jogo gira o modelo para compensar).")]
            public float giro;

            /// <summary>Instante do clipe para um golpe em andamento: o impacto do clipe cai exatamente em hitStart.</summary>
            public float Tempo(ComboSO golpe, float tempoGolpe)
            {
                float hs = Mathf.Max(.01f, golpe.hitStart), d = Mathf.Max(hs + .01f, golpe.duration);
                return tempoGolpe <= hs ? Mathf.Lerp(inicio, impacto, tempoGolpe / hs) : Mathf.Lerp(impacto, fim, Mathf.Clamp01((tempoGolpe - hs) / (d - hs)));
            }
        }
        public Avatar avatar;
        public List<Trecho> golpes = new List<Trecho>();
        public List<Trecho> movimentos = new List<Trecho>();
        Dictionary<string, Trecho> porGolpe, porMovimento;

        public Trecho Golpe(string id)
        {
            if (porGolpe == null) { porGolpe = new Dictionary<string, Trecho>(); foreach (var t in golpes) if (t != null && t.clip) porGolpe[t.id] = t; }
            return id != null && porGolpe.TryGetValue(id, out var r) ? r : null;
        }
        public Trecho Movimento(string id)
        {
            if (porMovimento == null) { porMovimento = new Dictionary<string, Trecho>(); foreach (var t in movimentos) if (t != null && t.clip) porMovimento[t.id] = t; }
            return porMovimento.TryGetValue(id, out var r) ? r : null;
        }
        void OnEnable() { porGolpe = null; porMovimento = null; }

        static PatrocloAnimacoesSO cache; static bool procurou;
        public static PatrocloAnimacoesSO Carregar()
        {
            if (!procurou) { cache = Resources.Load<PatrocloAnimacoesSO>("Troia/Animacoes"); procurou = true; }
            return cache && cache.avatar ? cache : null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpar() { cache = null; procurou = false; }
    }
}
