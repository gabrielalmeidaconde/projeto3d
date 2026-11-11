using System;
using UnityEngine;

namespace Troia
{
    [CreateAssetMenu(menuName = "Troia/Gameplay")]
    public class GameplayConfig : ScriptableObject
    {
        public float startingTime = 180, secondsPerKill = 3, inputBuffer = .2f;
        public float moveSpeed = 5.5f, sprintSpeed = 8.5f, jumpSpeed = 9, gravity = 25;
        public float dodgeSpeed = 14, dodgeDuration = .32f, dodgeCooldown = .55f;
        public float heavyHold = .35f, playerHealth = 120;
        public ComboSO light, heavy, launcher, airLight, slam;
        public int combatVersion;
        public WeaponSO[] weapons;
        public float doubleJumpSpeed = 8.5f;
        public float approachSpeed = 26, approachRange = 18, approachCooldown = .75f;
        public float lockRange = 24;
        public WaveSO[] floors;
        public BlessingSO[] blessings;
    }

}
