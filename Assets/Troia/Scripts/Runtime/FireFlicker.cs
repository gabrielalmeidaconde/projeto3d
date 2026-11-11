using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Faz a luz de um incêndio / braseiro / tocha tremular (intensidade e alcance)
    /// usando ruído Perlin, para que cada fogo pareça vivo e diferente dos outros.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FireFlicker : MonoBehaviour
    {
        public float baseIntensity = 4f;
        public float intensityVariation = 1.2f;
        public float baseRange = 14f;
        public float rangeVariation = 1f;
        [Tooltip("Velocidade da oscilação.")]
        public float speed = 7f;

        Light _light;
        float _seed;

        void Awake()
        {
            _light = GetComponent<Light>();
            _seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time * speed;
            float n = Mathf.PerlinNoise(t, _seed) * 0.7f + Mathf.PerlinNoise(t * 2.7f, _seed + 13.1f) * 0.3f;
            float k = (n - 0.5f) * 2f;
            _light.intensity = Mathf.Max(0f, baseIntensity + k * intensityVariation);
            _light.range = Mathf.Max(0.1f, baseRange + k * rangeVariation);
        }
    }
}
