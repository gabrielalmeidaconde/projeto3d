using UnityEngine;

namespace Troia
{
    /// <summary>Aponta para o prefab do herói. Fica em Resources/Troia/Heroi para o jogo carregar sem depender da cena.</summary>
    public class HeroiSO : ScriptableObject
    {
        public GameObject prefab;
        static HeroiSO cache; static bool procurou;
        public static GameObject Prefab
        {
            get
            {
                if (!procurou) { cache = Resources.Load<HeroiSO>("Troia/Heroi"); procurou = true; }
                return cache ? cache.prefab : null;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpar() { cache = null; procurou = false; }
    }
}
