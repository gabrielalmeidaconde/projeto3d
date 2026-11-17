using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Troia
{
    /// <summary>
    /// Toca clipes humanoides no Pátroclo com o tempo controlado pelo jogo (para o golpe casar com a janela de acerto
    /// e congelar junto na parada do impacto). A cada quadro o jogo diz quais clipes quer, em que instante e com que
    /// peso; o tocador faz a transição suave entre eles e avalia a pose na hora.
    /// </summary>
    public class PatrocloTocador : MonoBehaviour
    {
        class Faixa { public string chave; public AnimationClip clip; public AnimationClipPlayable tocavel; public float peso, alvo; public int entrada; }
        PlayableGraph grafo;
        AnimationMixerPlayable mistura;
        readonly List<Faixa> faixas = new List<Faixa>();
        const int Entradas = 8;
        public Animator Animador { get; private set; }

        public void Preparar(Avatar avatar)
        {
            Animador = GetComponent<Animator>(); if (!Animador) Animador = gameObject.AddComponent<Animator>();
            Animador.avatar = avatar; Animador.applyRootMotion = false; Animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Animador.updateMode = AnimatorUpdateMode.Normal;
            grafo = PlayableGraph.Create("Patroclo"); grafo.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mistura = AnimationMixerPlayable.Create(grafo, Entradas);
            var saida = AnimationPlayableOutput.Create(grafo, "Pose", Animador); saida.SetSourcePlayable(mistura);
            grafo.Play();
        }

        /// <summary>Pede um clipe neste quadro. 'chave' distingue usos diferentes do mesmo clipe (trechos de golpes distintos).</summary>
        public void Pedir(string chave, AnimationClip clip, float tempo, float peso = 1)
        {
            if (!clip || !grafo.IsValid()) return;
            Faixa f = null;
            foreach (var x in faixas) if (x.chave == chave && x.clip == clip) { f = x; break; }
            if (f == null)
            {
                int livre = -1;
                for (int i = 0; i < Entradas; i++) { bool usada = false; foreach (var x in faixas) if (x.entrada == i) usada = true; if (!usada) { livre = i; break; } }
                if (livre < 0)
                {
                    // sem vaga: sai a faixa mais fraca que não foi pedida
                    Faixa fraca = null; foreach (var x in faixas) if (x.alvo <= 0 && (fraca == null || x.peso < fraca.peso)) fraca = x;
                    if (fraca == null) return;
                    livre = fraca.entrada; Remover(fraca);
                }
                f = new Faixa { chave = chave, clip = clip, entrada = livre, tocavel = AnimationClipPlayable.Create(grafo, clip) };
                f.tocavel.SetApplyFootIK(false); f.tocavel.SetSpeed(0);
                grafo.Connect(f.tocavel, 0, mistura, livre);
                faixas.Add(f);
            }
            f.alvo = Mathf.Max(f.alvo, peso);
            float t = clip.isLooping ? Mathf.Repeat(tempo, clip.length) : Mathf.Clamp(tempo, 0, clip.length - .001f);
            f.tocavel.SetTime(t);
        }
        void Remover(Faixa f)
        {
            mistura.DisconnectInput(f.entrada); f.tocavel.Destroy(); faixas.Remove(f);
        }

        /// <summary>Fecha o quadro: aproxima os pesos dos pedidos e avalia a pose. 'transicao' em segundos.</summary>
        public void Avaliar(float dt, float transicao)
        {
            if (!grafo.IsValid()) return;
            float passo = transicao <= 0 || dt <= 0 ? (dt <= 0 ? 0 : 1) : dt / transicao;
            float soma = 0;
            for (int i = faixas.Count - 1; i >= 0; i--)
            {
                var f = faixas[i];
                f.peso = Mathf.MoveTowards(f.peso, f.alvo, passo);
                if (f.peso <= 0 && f.alvo <= 0) { Remover(f); continue; }
                soma += f.peso;
            }
            foreach (var f in faixas) { mistura.SetInputWeight(f.entrada, soma > 1e-4f ? f.peso / soma : 0); f.alvo = 0; }
            grafo.Evaluate(0);
        }
        /// <summary>Entra direto na pose pedida, sem transição (início, reaparecimento).</summary>
        public void Firmar() { foreach (var f in faixas) f.peso = f.alvo; }

        void OnDestroy() { if (grafo.IsValid()) grafo.Destroy(); }
    }
}
