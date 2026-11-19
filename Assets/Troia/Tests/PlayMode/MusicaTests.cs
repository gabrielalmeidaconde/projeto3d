using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Troia.Tests
{
    /// <summary>A trilha em camadas acompanha a nota de estilo e as oito faixas seguem juntas.</summary>
    public class MusicaTests
    {
        const string Map = "Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity";

        [UnityTest]
        public IEnumerator CamadasSeguemANotaDeEstilo()
        {
            if (!GameManager.Instance) yield return SceneManager.LoadSceneAsync(Map);
            yield return null;
            var gm = GameManager.Instance; gm.player.readInput = false;
            var musica = gm.GetComponent<MusicaDinamica>();
            Assert.That(musica, Is.Not.Null, "o GameManager deve criar a música");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(musica.Pronta, Is.True, "as oito faixas devem carregar de Resources/Troia/Musica");
            int amostras = musica.Fonte(0).clip.samples;
            for (int i = 0; i < MusicaDinamica.Camadas; i++)
            {
                Assert.That(musica.Fonte(i).clip.samples, Is.EqualTo(amostras), $"camada {i} com outro comprimento sairia de sincronia");
                Assert.That(musica.Fonte(i).loop, Is.True);
            }
            Assert.That(amostras / (double)musica.Fonte(0).clip.frequency, Is.EqualTo(64 * MusicaDinamica.Batida).Within(.01),
                "16 compassos a 150 BPM");

            Assert.That(musica.CamadasAlvo, Is.EqualTo(1), "fora de combate fica só a base");
            gm.BeginTraining();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(gm.FloorActive, Is.True);
            Assert.That(musica.CamadasAlvo, Is.EqualTo(2), "nota D: base e tambores");

            var estilo = gm.player.Style;
            int golpe = 0;
            while (estilo.Rank < 6 && golpe < 400)
            {
                estilo.RegisterHit("teste." + golpe, golpe % 3 == 0, golpe % 2 == 0 ? "xifos" : "lanca", false, false, 0, 10, "teste");
                golpe++;
            }
            Assert.That(estilo.Rank, Is.EqualTo(6), "chegar a SSS");
            yield return null;
            Assert.That(musica.CamadasAlvo, Is.EqualTo(MusicaDinamica.Camadas), "SSS liga todas as camadas");
            yield return new WaitForSecondsRealtime((float)MusicaDinamica.Batida + .3f);
            for (int i = 0; i < MusicaDinamica.Camadas; i++)
                Assert.That(musica.Nivel(i), Is.GreaterThan(.9f), $"camada {i} deveria estar tocando em SSS");

            estilo.TakeHit();
            yield return null;
            Assert.That(musica.CamadasAlvo, Is.EqualTo(2 + estilo.Rank), "apanhar derruba a nota e as camadas de cima");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(musica.Nivel(MusicaDinamica.Camadas - 1), Is.LessThan(.05f), "a camada SSS sai depois de apanhar");

            // sincronia: todas no mesmo ponto da faixa (quando há dispositivo de áudio)
            int t0 = musica.Fonte(0).timeSamples;
            for (int i = 1; i < MusicaDinamica.Camadas; i++)
                Assert.That(Mathf.Abs(musica.Fonte(i).timeSamples - t0), Is.LessThan(2048), $"camada {i} fora de sincronia");
            Debug.Log($"MUSICA ok; posição da faixa {t0} amostras; relógio de áudio {AudioSettings.dspTime:0.00} s");
            gm.EndRun(false);
        }
    }
}
