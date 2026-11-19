using UnityEditor;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>
    /// Faixas da trilha em camadas (Resources/Troia/Musica): Vorbis na memória, carregadas junto com a cena e sem
    /// carregamento em segundo plano, para todas começarem no mesmo instante e seguirem juntas no loop.
    /// </summary>
    public class ImportarMusica : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Troia/Musica/")) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = false;
            imp.loadInBackground = false;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = .7f;
            s.preloadAudioData = true;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.defaultSampleSettings = s;
        }
    }
}
