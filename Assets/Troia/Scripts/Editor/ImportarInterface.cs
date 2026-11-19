using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>
    /// Imagens da interface (Resources/Troia/UI), geradas por Ferramentas/interface/gerar_interface.py: viram sprites
    /// sem compressão nem mipmaps; o sufixo _bN no nome é a borda do 9-slice (painéis, cartas, barras, teclas).
    /// </summary>
    public class ImportarInterface : AssetPostprocessor
    {
        static readonly Regex Borda = new Regex(@"_b(\d+)$");

        void OnPreprocessTexture()
        {
            string caminho = assetPath.Replace('\\', '/');
            if (!caminho.Contains("/Resources/Troia/UI/")) return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.sRGBTexture = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 1024;
            string nome = System.IO.Path.GetFileNameWithoutExtension(caminho);
            imp.wrapMode = nome == "Greca" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var config = new TextureImporterSettings();
            imp.ReadTextureSettings(config);
            config.spriteMeshType = SpriteMeshType.FullRect;
            config.spritePixelsPerUnit = 100;
            imp.SetTextureSettings(config);
            var m = Borda.Match(nome);
            int b = m.Success ? int.Parse(m.Groups[1].Value) : 0;
            imp.spriteBorder = new Vector4(b, b, b, b);
        }
    }
}
