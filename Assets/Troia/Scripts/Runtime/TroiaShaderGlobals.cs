using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Publica os parâmetros globais do shader "Troia/Lit Sujo": textura de sujeira,
    /// mapa de altura do chão (para sujar a base das paredes) e intensidades.
    /// Funciona no editor e no jogo; ajuste os valores no Inspector para ver na hora.
    /// </summary>
    [ExecuteAlways]
    public class TroiaShaderGlobals : MonoBehaviour
    {
        public Texture2D grimeTexture;
        public Texture2D groundHeightTexture;
        [Tooltip("Canto mínimo (x, z) do mapa de altura, em metros.")]
        public Vector2 groundMin = new Vector2(-120f, -150f);
        [Tooltip("Tamanho (x, z) coberto pelo mapa de altura, em metros.")]
        public Vector2 groundSize = new Vector2(240f, 300f);
        public float groundMinY = -10f;
        public float groundRangeY = 30f;

        [Header("Sujeira")]
        [Range(0f, 2f)] public float strength = 1.15f;
        [Tooltip("Altura da faixa de lama na base das paredes (m).")]
        [Range(0f, 3f)] public float baseHeight = 1.5f;
        [Range(0f, 1f)] public float toneVariation = 0.8f;
        [Range(0f, 1f)] public float streaks = 0.7f;
        public Color grimeColor = new Color(0.3f, 0.24f, 0.18f);

        static readonly int GrimeTex = Shader.PropertyToID("_TroiaGrimeTex");
        static readonly int GroundTex = Shader.PropertyToID("_TroiaGroundTex");
        static readonly int GroundRect = Shader.PropertyToID("_TroiaGroundRect");
        static readonly int GroundRange = Shader.PropertyToID("_TroiaGroundRange");
        static readonly int GrimeParams = Shader.PropertyToID("_TroiaGrimeParams");
        static readonly int GrimeColor = Shader.PropertyToID("_TroiaGrimeColor");

        void OnEnable() => Apply();
        void OnValidate() => Apply();

#if UNITY_EDITOR
        void Update()
        {
            if (!Application.isPlaying) Apply();
        }
#endif

        void OnDisable()
        {
            Shader.SetGlobalVector(GrimeParams, Vector4.zero);
        }

        public void Apply()
        {
            if (grimeTexture == null || groundHeightTexture == null)
            {
                Shader.SetGlobalVector(GrimeParams, Vector4.zero);
                return;
            }
            Shader.SetGlobalTexture(GrimeTex, grimeTexture);
            Shader.SetGlobalTexture(GroundTex, groundHeightTexture);
            Shader.SetGlobalVector(GroundRect, new Vector4(groundMin.x, groundMin.y, 1f / Mathf.Max(groundSize.x, 1f), 1f / Mathf.Max(groundSize.y, 1f)));
            Shader.SetGlobalVector(GroundRange, new Vector4(groundMinY, groundRangeY, 0f, 0f));
            Shader.SetGlobalVector(GrimeParams, new Vector4(baseHeight, strength, toneVariation, streaks));
            Shader.SetGlobalColor(GrimeColor, grimeColor);
        }
    }
}
