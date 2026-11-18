using UnityEngine;

namespace Troia
{
    /// <summary>Sons das bênçãos, sintetizados na hora (o projeto não tem arquivos de áudio): um timbre para cada deus.</summary>
    public class DivineAudio : MonoBehaviour
    {
        const int Taxa = 22050;
        AudioSource fonte;
        readonly AudioClip[] poderes = new AudioClip[6];
        AudioClip aparo, carga;

        void Awake()
        {
            // fonte própria: a do CombatAudio muda de tom a cada golpe
            fonte = gameObject.AddComponent<AudioSource>();
            fonte.playOnAwake = false; fonte.spatialBlend = 0; fonte.volume = .5f;
        }
        public void Poder(God deus, float volume = 1)
        {
            int i = (int)deus;
            if (!poderes[i]) poderes[i] = Criar(deus);
            Tocar(poderes[i], 1, (deus == God.Zeus || deus == God.Poseidon ? 1f : .8f) * volume);
        }
        public void Carga(God deus)
        {
            if (!carga) carga = Clip("Carga", .4f, (t, n) => Mathf.Sin(6.2832f * (180 + 900 * t * t) * t) * .35f * Mathf.SmoothStep(0, 1, t / .4f) + n.Ruido() * .12f * t);
            Tocar(carga, deus == God.Poseidon ? .6f : 1.15f, .5f);
        }
        public void Aparo()
        {
            if (!aparo) aparo = Clip("Aparo", .7f, (t, n) => Sino(t, 392, .5f) + n.Ruido() * .5f * Mathf.Exp(-t * 90));
            Tocar(aparo, 1.25f, .8f);
        }
        void Tocar(AudioClip clip, float tom, float volume)
        {
            if (!fonte || !clip || !isActiveAndEnabled) return;
            fonte.pitch = tom; fonte.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------ síntese
        class Estado
        {
            readonly System.Random sorteio = new System.Random(7);
            public float baixo, banda, faixaBaixa;
            public float Ruido() => (float)sorteio.NextDouble() * 2 - 1;
            /// <summary>Ruído passado por um filtro passa-baixa de um polo (corte em Hz).</summary>
            public float Grave(float corte)
            {
                float a = 1 - Mathf.Exp(-6.2832f * corte / Taxa);
                baixo += a * (Ruido() - baixo); return baixo;
            }
            /// <summary>Ruído por um filtro passa-faixa ressonante (para silvos de vento).</summary>
            public float Faixa(float centro, float q)
            {
                float f = 2 * Mathf.Sin(Mathf.PI * Mathf.Min(centro, Taxa * .2f) / Taxa);
                faixaBaixa += f * banda; float alto = Ruido() - faixaBaixa - q * banda; banda += f * alto;
                return banda;
            }
        }
        static AudioClip Clip(string nome, float duracao, System.Func<float, Estado, float> onda)
        {
            int n = Mathf.CeilToInt(duracao * Taxa);
            var dados = new float[n]; var estado = new Estado();
            float pico = .001f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                // some nas pontas para não estalar
                float borda = Mathf.Clamp01(t / .004f) * Mathf.Clamp01((duracao - t) / .03f);
                dados[i] = onda(t, estado) * borda; pico = Mathf.Max(pico, Mathf.Abs(dados[i]));
            }
            float ganho = .92f / pico;
            for (int i = 0; i < n; i++) dados[i] *= ganho;
            var clip = AudioClip.Create(nome, n, 1, Taxa, false); clip.SetData(dados, 0); return clip;
        }
        // parciais desafinados de um disco de bronze
        static float Sino(float t, float f, float cauda)
        {
            return Mathf.Sin(6.2832f * f * t) * Mathf.Exp(-t / cauda) * .5f
                 + Mathf.Sin(6.2832f * f * 2.76f * t) * Mathf.Exp(-t / (cauda * .6f)) * .32f
                 + Mathf.Sin(6.2832f * f * 5.4f * t) * Mathf.Exp(-t / (cauda * .32f)) * .2f
                 + Mathf.Sin(6.2832f * f * 8.93f * t) * Mathf.Exp(-t / (cauda * .18f)) * .12f;
        }
        static float Corda(float t, float f, float inicio)
        {
            float s = t - inicio; if (s < 0) return 0;
            // corda dedilhada: fundamental e harmônicos que morrem mais rápido quanto mais agudos
            float v = 0;
            for (int h = 1; h <= 6; h++) v += Mathf.Sin(6.2832f * f * h * s) / h * Mathf.Exp(-s * (2.2f + h * 1.4f));
            return v * Mathf.Clamp01(s / .003f);
        }
        static AudioClip Criar(God deus)
        {
            switch (deus)
            {
                case God.Zeus:
                    // estalo seco, estouro e o trovão rolando atrás
                    return Clip("Trovao", 1.9f, (t, n) =>
                        n.Ruido() * Mathf.Exp(-t * 34) * 1.1f
                        + n.Grave(260 + 900 * Mathf.Exp(-t * 6)) * 3.2f * Mathf.Exp(-t * 2.1f) * (.75f + .25f * Mathf.Sin(t * 41) * Mathf.Sin(t * 13))
                        + Mathf.Sin(6.2832f * (58 - 12 * t) * t) * .7f * Mathf.Exp(-t * 3.2f)
                        + (t > .17f ? n.Ruido() * .55f * Mathf.Exp(-(t - .17f) * 28) : 0));
                case God.Ares:
                    // trompa de guerra: quinta grave, áspera, com o sopro por cima
                    return Clip("Trompa", 1.3f, (t, n) =>
                    {
                        float env = Mathf.SmoothStep(0, 1, t / .07f) * Mathf.Exp(-Mathf.Max(0, t - .35f) * 2.6f);
                        float vib = 1 + .012f * Mathf.Sin(t * 34);
                        float f = 87.3f * vib * (1 + .06f * Mathf.Exp(-t * 12));
                        float som = 0;
                        for (int h = 1; h <= 9; h++) som += Mathf.Sin(6.2832f * f * h * t) / h * (h % 2 == 0 ? .6f : 1);
                        for (int h = 1; h <= 6; h++) som += Mathf.Sin(6.2832f * f * 1.5f * h * t) / h * .55f;
                        return (som * .45f + n.Grave(700) * 1.6f * Mathf.Exp(-t * 5)) * env + n.Ruido() * .5f * Mathf.Exp(-t * 26);
                    });
                case God.Apollo:
                    // lira: arpejo maior subindo
                    return Clip("Lira", 1.8f, (t, n) =>
                        Corda(t, 440, 0) * .5f + Corda(t, 554.4f, .09f) * .45f + Corda(t, 659.3f, .18f) * .45f + Corda(t, 880, .28f) * .5f
                        + Corda(t, 1318.5f, .4f) * .3f + Mathf.Sin(6.2832f * 220 * t) * .12f * Mathf.Exp(-t * 2.2f));
                case God.Hermes:
                    // rajada: silvo que sobe e um brilho agudo no fim
                    return Clip("Rajada", .6f, (t, n) =>
                        n.Faixa(500 + 3600 * t * t * 2.2f, .35f) * 1.2f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .5f))
                        + Mathf.Sin(6.2832f * (1400 + 2200 * t) * t) * .14f * Mathf.Exp(-Mathf.Abs(t - .22f) * 14));
                case God.Athena:
                    // escudo de bronze batido
                    return Clip("Bronze", 1.6f, (t, n) => Sino(t, 196, .75f) + Sino(t, 293.7f, .5f) * .5f + n.Ruido() * .7f * Mathf.Exp(-t * 60));
                default:
                    // onda quebrando: ruído que abre e fecha, pancada grave embaixo
                    return Clip("Vaga", 1.9f, (t, n) =>
                    {
                        float env = Mathf.SmoothStep(0, 1, t / .1f) * Mathf.Exp(-Mathf.Max(0, t - .2f) * 2.3f);
                        return n.Grave(350 + 2600 * Mathf.Exp(-t * 2.4f)) * 3.4f * env
                             + n.Ruido() * .16f * Mathf.Exp(-Mathf.Max(0, t - .25f) * 3.5f) * Mathf.SmoothStep(0, 1, t / .2f)
                             + Mathf.Sin(6.2832f * (62 - 10 * t) * t) * .75f * Mathf.Exp(-t * 4);
                    });
            }
        }
        void OnDestroy()
        {
            foreach (var c in poderes) if (c) Destroy(c);
            if (aparo) Destroy(aparo); if (carga) Destroy(carga);
        }
    }
}
