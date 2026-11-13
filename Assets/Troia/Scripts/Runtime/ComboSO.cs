using System;
using UnityEngine;

namespace Troia
{
    [CreateAssetMenu(menuName = "Troia/Combo")]
    public class ComboSO : ScriptableObject
    {
        public string id, animationState;
        public string displayName;
        [TextArea] public string command;
        public AttackMotion motion;
        public AttackInput input;
        public float damage = 14, duration = .5f, hitStart = .12f, hitEnd = .25f;
        public float comboOpen = .26f, comboClose = .48f;
        [Min(0)] public float cooldown;
        [Min(0)] public float minimumRecovery = .02f;
        public float range = 2.8f, angle = 110, knockback = 1, launch, lunge = 1;
        public bool aerial, finisher, slam;
        [Min(1)] public int hitCount = 1;
        public ComboSO next, delayedNext, heavyNext;
        public float delayedThreshold = .43f;
    }

}
