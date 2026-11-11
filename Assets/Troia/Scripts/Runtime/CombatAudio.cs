using UnityEngine;

namespace Troia
{
    [RequireComponent(typeof(AudioSource))]
    public class CombatAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip swing, impact, shot, dash, rank;
        void Awake()
        {
            source = GetComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = .15f; source.volume = .24f;
            swing = Create("Whoosh", .13f, 180, true);
            impact = Create("Impact", .12f, 82, true);
            shot = Create("Throw", .09f, 760, false);
            dash = Create("Dash", .18f, 320, true);
            rank = Create("Style", .3f, 523, false);
        }
        static AudioClip Create(string name, float duration, float frequency, bool noise)
        {
            const int rate = 22050; int size = Mathf.CeilToInt(duration * rate);
            var data = new float[size]; var random = new System.Random(41);
            for (int i = 0; i < size; i++)
            {
                float t = i / (float)rate, envelope = Mathf.Sin(Mathf.PI * i / size) * Mathf.Exp(-t * 16);
                float tone = Mathf.Sin(2 * Mathf.PI * frequency * t * (1 - t));
                data[i] = envelope * (noise ? .55f * ((float)random.NextDouble() * 2 - 1) + .45f * tone : tone + .25f * Mathf.Sin(2 * Mathf.PI * frequency * 1.5f * t));
            }
            var clip = AudioClip.Create(name, size, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void Play(AudioClip clip, float pitch, float volume)
        {
            if (!source || !isActiveAndEnabled) return;
            source.pitch = pitch; source.PlayOneShot(clip, volume);
        }
        public void Swing(WeaponKind kind) => Play(swing, kind == WeaponKind.Spear ? .8f : kind == WeaponKind.TwinBlades ? 1.4f : 1, .5f);
        public void Impact(bool heavy) => Play(impact, heavy ? .65f : 1.1f, heavy ? 1 : .65f);
        public void Shoot() => Play(shot, 1.1f, .45f);
        public void Dodge() => Play(dash, 1.25f, .5f);
        public void Rank(int level) => Play(rank, 1 + level * .08f, .6f);
        void OnDestroy()
        {
            foreach (var clip in new[] { swing, impact, shot, dash, rank }) if (clip) Destroy(clip);
        }
    }
}
