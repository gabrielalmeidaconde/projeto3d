using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    public class StyleMeter : MonoBehaviour
    {
        public static readonly string[] Labels = { "D", "C", "B", "A", "S", "SS", "SSS" };
        static readonly float[] Thresholds = { 0, 35, 85, 150, 235, 340, 470 };
        readonly Queue<string> recent = new Queue<string>();
        public float Score { get; private set; }
        public int Rank { get; private set; }
        public float Multiplier => 1 + Rank * .25f;
        public float AverageRank => samples > 0 ? rankSum / samples : Rank;
        float idle, rankSum, samples;
        CombatEvents events;
        void Awake() => events = GetComponent<CombatEvents>();
        public void RegisterHit(string id, bool finisher)
        {
            int repeats = 0;
            foreach (var old in recent) if (old == id) repeats++;
            recent.Enqueue(id);
            if (recent.Count > 8) recent.Dequeue();
            SetScore(Score + (finisher ? 26 : 15) / (1f + repeats));
            idle = 0;
        }
        public void TakeHit()
        {
            SetScore(Thresholds[Mathf.Max(0, Rank - 2)]);
            recent.Clear(); idle = 0;
        }
        public void Tick(float dt)
        {
            idle += dt;
            if (idle > 2.5f) SetScore(Score - dt * 12);
            rankSum += Rank * dt; samples += dt;
        }
        void SetScore(float value)
        {
            Score = Mathf.Clamp(value, 0, 600);
            int rank = 0;
            for (int i = 1; i < Thresholds.Length; i++) if (Score >= Thresholds[i]) rank = i;
            if (rank == Rank) return;
            Rank = rank; events?.RankChanged(rank);
        }
        public void ResetMeter() { Score = idle = rankSum = samples = 0; Rank = 0; recent.Clear(); }
    }
}
