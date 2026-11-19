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
            [Tooltip("Para onde o peito aponta (graus, relativo à frente do clipe) ao longo do trecho, de início a fim; vazio = sem limite.")]
            public float[] corpo;
            [Tooltip("Giro de propósito (Fúria, Roda): sem limite; depois do impacto a volta continua até o herói terminar de frente.")]
            public bool livre;

            /// <summary>Limite de quanto o tronco pode virar para longe do alvo durante o golpe.</summary>
            public const float LimiteCorpo = 55;

            /// <summary>
            /// Giro do modelo para este instante do clipe: a mira e, se o clipe virar o corpo mais que LimiteCorpo para
            /// longe do alvo, o que passar disso é desfeito (o tronco ainda torce, mas o herói não termina de costas).
            /// </summary>
            public float GiroModelo(float tempoClipe)
            {
                float r = -giro;
                if (corpo == null || corpo.Length < 2 || fim <= inicio) return r;
                if (livre)
                {
                    // completa a volta no sentido em que o corpo gira: termina num múltiplo de 360°, de frente para o alvo
                    float e = corpo[corpo.Length - 1] + r;
                    float perto = Mathf.Round(e / 360) * 360;
                    float sentido = e - Amostra(Mathf.Clamp01((impacto - inicio) / (fim - inicio)));
                    float alvoFinal = Mathf.Abs(e - perto) < 60 ? perto : (sentido < 0 ? Mathf.Floor(e / 360) : Mathf.Ceil(e / 360)) * 360;
                    float k = Mathf.Clamp01((tempoClipe - impacto) / Mathf.Max(.01f, fim - impacto));
                    return r + (alvoFinal - e) * k * k * (3 - 2 * k);
                }
                float mundo = Amostra(Mathf.Clamp01((tempoClipe - inicio) / (fim - inicio))) + r;
                if (mundo > LimiteCorpo) r -= mundo - LimiteCorpo;
                else if (mundo < -LimiteCorpo) r -= mundo + LimiteCorpo;
                return r;
            }

            float Amostra(float fracao)
            {
                float u = fracao * (corpo.Length - 1);
                int i = Mathf.Min((int)u, corpo.Length - 2);
                return Mathf.Lerp(corpo[i], corpo[i + 1], u - i);
            }

            /// <summary>
            /// Instante do clipe para um golpe em andamento: o impacto do clipe cai exatamente em hitStart. A preparação
            /// começa lenta e acelera até o impacto (antecipação e chicotada) e o acompanhamento sai rápido e assenta,
            /// em vez de tocar tudo em velocidade constante.
            /// Queda em área: o golpe fica preso em hitStart enquanto o herói cai e salta para hitEnd ao pousar; aqui
            /// 'impacto' é o auge (arma erguida), mantido na queda, e o golpe para baixo toca a partir do pouso.
            /// </summary>
            public float Tempo(ComboSO golpe, float tempoGolpe)
            {
                float hs = Mathf.Max(.01f, golpe.hitStart), d = Mathf.Max(hs + .01f, golpe.duration);
                if (golpe.slam)
                {
                    float he = Mathf.Clamp(golpe.hitEnd, hs, d - .01f);
                    if (tempoGolpe < he - .001f) { float u = Mathf.Clamp01(tempoGolpe / hs); return Mathf.Lerp(inicio, impacto, 1 - (1 - u) * (1 - u)); }
                    float v = Mathf.Clamp01((tempoGolpe - he) / (d - he));
                    return Mathf.Lerp(impacto, fim, 1 - Mathf.Pow(1 - v, 2.4f));
                }
                if (tempoGolpe <= hs) return Mathf.Lerp(inicio, impacto, Mathf.Pow(Mathf.Clamp01(tempoGolpe / hs), 1.45f));
                float w = Mathf.Clamp01((tempoGolpe - hs) / (d - hs));
                return Mathf.Lerp(impacto, fim, 1 - (1 - w) * (1 - w));
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
