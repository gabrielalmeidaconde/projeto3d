using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Trilha de combate em camadas, como no Devil May Cry: as oito faixas de Resources/Troia/Musica tocam juntas,
    /// em sincronia, e cada nota de estilo liga mais uma (D tambores, C baixo e darbuka, B guitarras, A bateria e
    /// cordas, S aulos e metais, SS coro, SSS solo de guitarra). Camada nova entra na próxima batida; ao apanhar,
    /// as que sobram saem em pouco mais de um segundo. Fora de combate fica só a base; pausa e escolha de bênção
    /// abafam a música. As faixas são geradas por Ferramentas/musica/gerar_musica.py.
    /// </summary>
    public class MusicaDinamica : MonoBehaviour
    {
        public const int Camadas = 8;
        /// <summary>150 BPM, a mesma do gerador.</summary>
        public const double Batida = 60.0 / 150;
        [Range(0, 1)] public float volume = .6f;
        /// <summary>Volume geral da música (para um menu de opções).</summary>
        public static float VolumeGeral = 1;

        readonly AudioSource[] fontes = new AudioSource[Camadas];
        readonly AudioLowPassFilter[] filtros = new AudioLowPassFilter[Camadas];
        readonly float[] nivel = new float[Camadas];
        readonly double[] entrada = new double[Camadas];
        readonly bool[] ligada = new bool[Camadas];
        double inicio, dspAnterior;
        float mestre, corteAtual = 22000, semRelogio;
        bool pronta;

        /// <summary>Quantas camadas a situação pede agora (0 = silêncio, 1 = só a base, 8 = SSS).</summary>
        public int CamadasAlvo { get; private set; }
        public float Nivel(int camada) => nivel[camada];
        public AudioSource Fonte(int camada) => fontes[camada];
        public bool Pronta => pronta;

        void Start()
        {
            for (int i = 0; i < Camadas; i++)
            {
                var clip = Resources.Load<AudioClip>("Troia/Musica/Camada_" + i);
                if (!clip) { Debug.LogWarning("[Troia] faixa de música não encontrada: Camada_" + i); enabled = false; return; }
                var go = new GameObject("Musica_Camada_" + i);
                go.transform.SetParent(transform, false);
                var f = go.AddComponent<AudioSource>();
                f.clip = clip; f.loop = true; f.playOnAwake = false; f.spatialBlend = 0; f.volume = 0;
                f.priority = 0; f.bypassReverbZones = true; f.dopplerLevel = 0;
                fontes[i] = f;
                filtros[i] = go.AddComponent<AudioLowPassFilter>();
                filtros[i].cutoffFrequency = corteAtual;
            }
            // todas no mesmo instante do relógio de áudio: como têm o mesmo comprimento, seguem juntas no loop
            inicio = AudioSettings.dspTime + .25;
            foreach (var f in fontes) f.PlayScheduled(inicio);
            dspAnterior = AudioSettings.dspTime;
            pronta = true;
        }

        int Alvo(out float teto, out float corte)
        {
            teto = 1; corte = 22000;
            var gm = GameManager.Instance;
            if (!gm || !gm.player) return 0;
            switch (gm.State)
            {
                case RunState.Won:
                case RunState.Lost:
                    return 0;
                case RunState.Paused:
                    teto = .5f; corte = 650;
                    return Mathf.Max(1, CamadasAlvo);
                case RunState.ChoosingBlessing:
                    teto = .8f; corte = 1400;
                    return 1;
            }
            // em combate: D liga a base e os tambores; cada nota acima soma uma camada
            return gm.FloorActive ? Mathf.Clamp(2 + gm.player.Style.Rank, 2, Camadas) : 1;
        }

        void Update()
        {
            if (!pronta) return;
            float dt = Time.unscaledDeltaTime;
            CamadasAlvo = Alvo(out float teto, out float corte);
            double agora = AudioSettings.dspTime;
            // sem dispositivo de áudio (testes em lote) o relógio do áudio não anda: as camadas entram na hora
            if (agora > dspAnterior) { dspAnterior = agora; semRelogio = 0; } else semRelogio += dt;
            bool semAudio = semRelogio > .5f;
            double batidas = System.Math.Ceiling((agora - inicio) / Batida);
            double proxima = inicio + System.Math.Max(0, batidas) * Batida;
            for (int i = 0; i < Camadas; i++)
            {
                bool liga = i < CamadasAlvo;
                if (liga && !ligada[i]) entrada[i] = proxima;
                ligada[i] = liga;
                float destino = liga && (semAudio || agora >= entrada[i]) ? 1 : 0;
                // entra rápido, no tempo; sai devagar (e mais devagar ainda quando o combate acaba)
                float duracao = destino > nivel[i] ? (i == 0 ? 1.5f : .1f) : (CamadasAlvo <= 1 ? 2.8f : 1.2f);
                nivel[i] = Mathf.MoveTowards(nivel[i], destino, dt / duracao);
            }
            mestre = Mathf.MoveTowards(mestre, teto, dt / .4f);
            corteAtual = Mathf.Exp(Mathf.MoveTowards(Mathf.Log(corteAtual), Mathf.Log(corte), dt * 6));
            float geral = volume * VolumeGeral * mestre;
            for (int i = 0; i < Camadas; i++)
            {
                float n = nivel[i];
                fontes[i].volume = geral * n * n * (3 - 2 * n);
                filtros[i].cutoffFrequency = corteAtual;
            }
        }
    }
}
