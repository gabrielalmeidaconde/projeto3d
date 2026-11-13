using System;
using UnityEngine;

namespace Troia
{
    [CreateAssetMenu(menuName = "Troia/Andar")]
    public class WaveSO : ScriptableObject
    {
        public int floor = 1, arena = 1;
        public EnemyGroup[] enemies;
        public float healthMultiplier = 1, damageMultiplier = 1;
        [Tooltip("Indices dos LevelMarker EnemySpawn; vazio usa todos os pontos da arena.")]
        public int[] spawnPoints;
    }

}
