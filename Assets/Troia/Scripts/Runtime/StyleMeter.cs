using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    public class StyleMeter : MonoBehaviour
    {
        public static readonly string[] Labels = { "D", "C", "B", "A", "S", "SS", "SSS" };
        public static readonly string[] Titles = { "DESPERTO", "CORTANTE", "BRUTAL", "AVASSALADOR", "SELVAGEM", "SUBLIME", "LENDARIO" };
        static readonly float[] Thresholds = { 0, 35, 85, 150, 235, 340, 470 };
        readonly Queue<string> recent = new Queue<string>();
        readonly HashSet<int> credited = new HashSet<int>();
        readonly Queue<int> actionHistory = new Queue<int>();
        public float Score { get; private set; }
        public int Rank { get; private set; }
        public float Multiplier => 1 + Rank * .25f;
        public float AverageRank => samples > 0 ? rankSum / samples : Rank;
        public int PeakRank { get; private set; }
        public int ChainHits { get; private set; }
        public float ChainDamage { get; private set; }
        public float LastGain { get; private set; }
        public float Freshness { get; private set; } = 1;
        public string LastMove { get; private set; }
        public float RankProgress => Rank == 6 ? Mathf.Clamp01((Score - Thresholds[6]) / 130) :
            Mathf.InverseLerp(Thresholds[Rank], Thresholds[Rank + 1], Score);
        public float RankFlash { get; private set; }
        public bool ComboActive => ChainHits > 0 && sinceHit < 4;
        float idle, sinceHit, rankSum, samples;
        string lastWeapon;
        CombatEvents events;
        void Awake() => events = GetComponent<CombatEvents>();
        public void RegisterHit(string id, bool finisher)
            => RegisterHit(id, finisher, "", false, false, 0, 0, id);

        public void RegisterHit(string id, bool finisher, string weapon, bool aerial, bool projectile, int sequence, float damage, string title)
        {
            if (sinceHit >= 4) { ChainHits = 0; ChainDamage = 0; }
            ChainHits++; ChainDamage += Mathf.Max(0, damage); sinceHit = 0;
            // Multi-hit moves, dual projectiles and crowds count as hits, but one action earns style once.
            if (sequence != 0)
            {
                if (!credited.Add(sequence)) return;
                actionHistory.Enqueue(sequence);
                if (actionHistory.Count > 128) credited.Remove(actionHistory.Dequeue());
            }
            int repeats = 0;
            foreach (var old in recent) if (old == id) repeats++;
            recent.Enqueue(id);
            if (recent.Count > 10) recent.Dequeue();
            Freshness = repeats >= 3 ? 0 : 1f / (1 + repeats * repeats);
            float variety = !string.IsNullOrEmpty(lastWeapon) && lastWeapon != weapon && idle < 4 ? 1.4f : 1;
            LastGain = (finisher ? 26 : 15) * Freshness * variety * (aerial ? 1.2f : 1) * (projectile ? .35f : 1);
            LastMove = string.IsNullOrEmpty(title) ? id : title;
            lastWeapon = weapon;
            SetScore(Score + LastGain);
            if (LastGain > 0) idle = 0;
        }
        public void TakeHit()
        {
            SetScore(Thresholds[Mathf.Max(0, Rank - 2)]);
            idle = 0; ChainHits = 0; ChainDamage = 0; sinceHit = 4;
        }
        public void Tick(float dt)
        {
            idle += dt; sinceHit += dt;
            RankFlash = Mathf.Max(0, RankFlash - dt * 2);
            if (idle > 2.5f) SetScore(Score - dt * (10 + Rank * 2));
            if (sinceHit >= 4) { ChainHits = 0; ChainDamage = 0; }
            rankSum += Rank * dt; samples += dt;
        }
        void SetScore(float value)
        {
            Score = Mathf.Clamp(value, 0, 600);
            int rank = 0;
            for (int i = 1; i < Thresholds.Length; i++) if (Score >= Thresholds[i]) rank = i;
            if (rank == Rank) return;
            if (rank > Rank) RankFlash = 1;
            Rank = rank; PeakRank = Mathf.Max(PeakRank, rank); events?.RankChanged(rank);
        }
        public void ResetMeter()
        {
            Score = idle = rankSum = samples = sinceHit = ChainDamage = LastGain = RankFlash = 0;
            Rank = PeakRank = ChainHits = 0; lastWeapon = LastMove = ""; Freshness = 1;
            recent.Clear(); credited.Clear(); actionHistory.Clear();
        }
    }
}
