using System;
using UnityEngine;

namespace Troia
{
    // Instance events avoid leaked subscriptions between runs and allow multiple combatants.
    public class CombatEvents : MonoBehaviour
    {
        public event Action<DamageInfo, Health> OnComboHit, OnFinisher, OnLaunch;
        public event Action OnDodge;
        public event Action<int> OnStyleRankChanged;
        public void Hit(DamageInfo info, Health target)
        {
            OnComboHit?.Invoke(info, target);
            if (info.finisher) OnFinisher?.Invoke(info, target);
            if (info.launch > 0) OnLaunch?.Invoke(info, target);
        }
        public void Dodge() => OnDodge?.Invoke();
        public void RankChanged(int rank) => OnStyleRankChanged?.Invoke(rank);
    }}
