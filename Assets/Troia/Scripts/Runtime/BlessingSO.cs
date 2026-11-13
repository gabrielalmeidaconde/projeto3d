using System;
using UnityEngine;

namespace Troia
{
    [CreateAssetMenu(menuName = "Troia/Bencao")]
    public class BlessingSO : ScriptableObject
    {
        public God god;
        public string title;
        [TextArea] public string description;
        public float cost = 40, cooldown = 8;
    }

}
