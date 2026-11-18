using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    // Procedural animation for EnemyCombat, driven only by its state and timers.
    public class EnemyPresentation : MonoBehaviour
    {
        static readonly Color WarningColor = new Color(1, .18f, .055f);
        EnemyCombat enemy;
        EnemyModel.Rig rig;
        TrailRenderer trail;
        Material merged;
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        readonly List<Material> materials = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();
        Vector3 previousPosition;
        float gait, strike, flash, tumble, lean;
        bool tinted;
        const float StrikeTime = .22f;

        public void Initialize(EnemyCombat owner)
        {
            enemy = owner; previousPosition = transform.position;
            rig = EnemyModel.Build(transform, owner.Data);
            // one mesh per animated joint and a single material: ~6 draw calls per enemy instead of ~50
            merged = ModeloFundido.Fundir(rig.root, rig.head, rig.rightArm, rig.leftArm, rig.rightLeg, rig.leftLeg);
            if (!merged)
                foreach (var r in rig.root.GetComponentsInChildren<Renderer>())
                {
                    materials.Add(r.sharedMaterial); baseColors.Add(r.sharedMaterial.color);
                }
            if (rig.weaponTip)
            {
                trail = rig.weaponTip.gameObject.AddComponent<TrailRenderer>();
                trail.time = .12f; trail.startWidth = .18f; trail.endWidth = 0; trail.minVertexDistance = .04f;
                trail.startColor = new Color(1, .35f, .2f, .9f); trail.endColor = new Color(1, .35f, .2f, 0);
                var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = new Color(1, .4f, .25f) };
                trail.sharedMaterial = material; rig.weaponTip.gameObject.AddComponent<OwnedMaterial>().material = material;
                trail.emitting = false;
            }
            owner.Health.Damaged += Hurt;
        }

        public void Strike() => strike = StrikeTime;
        void Hurt(DamageInfo info) => flash = .09f;

        void LateUpdate()
        {
            if (!enemy || !rig.root) return;
            float dt = Time.deltaTime;
            float speed = dt > 0 ? Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude / dt : 0;
            previousPosition = transform.position;
            strike = Mathf.Max(0, strike - dt); flash = Mathf.Max(0, flash - dt);

            var state = enemy.State;
            bool grounded = state != EnemyState.Airborne && state != EnemyState.Pulled;
            gait += Mathf.Min(speed, 8) * dt * 3.2f;
            float walk = grounded ? Mathf.Sin(gait) * Mathf.Clamp01(speed / 3) * 34 : 0;

            Quaternion rightLeg = Quaternion.Euler(grounded ? walk : -40, 0, 0);
            Quaternion leftLeg = Quaternion.Euler(grounded ? -walk : 30, 0, 0);
            Quaternion rightArm = Quaternion.Euler(-20 - walk * .4f, 0, -12);
            Quaternion leftArm = Quaternion.Euler(walk * .5f - 10, 0, 12);
            float targetLean = 0, bob = Mathf.Abs(Mathf.Sin(gait)) * .03f * Mathf.Clamp01(speed / 3);
            bool ranged = enemy.Data.ranged;

            if (state == EnemyState.Windup)
            {
                float t = enemy.WindupDuration > 0 ? 1 - Mathf.Clamp01(enemy.StateTime / enemy.WindupDuration) : 1;
                float e = Mathf.SmoothStep(0, 1, t);
                if (ranged)
                {
                    leftArm = Quaternion.Euler(-88, 8, 8);
                    rightArm = Quaternion.Euler(-88, Mathf.Lerp(-5, -60, e), -10);
                }
                else
                {
                    rightArm = Quaternion.Euler(Mathf.Lerp(-20, -155, e), 0, -18);
                    leftArm = Quaternion.Euler(Mathf.Lerp(-10, -55, e), 0, 25);
                    targetLean = -10 * e;
                }
                // Small shake right before the hit sells the telegraph.
                if (t > .75f) bob += Mathf.Sin(Time.time * 70) * .012f;
            }
            else if (strike > 0 || state == EnemyState.Recovery)
            {
                float s = 1 - strike / StrikeTime;
                if (ranged) { leftArm = Quaternion.Euler(-88, 8, 8); rightArm = Quaternion.Euler(-80, Mathf.Lerp(-60, 10, s), -10); }
                else
                {
                    float swing = Mathf.SmoothStep(0, 1, s);
                    rightArm = Quaternion.Euler(Mathf.Lerp(-155, 55, swing), 0, -18);
                    leftArm = Quaternion.Euler(-10, 0, 20);
                    targetLean = Mathf.Lerp(-10, 14, swing);
                }
            }
            else if (state == EnemyState.Staggered)
            {
                targetLean = -20; rightArm = Quaternion.Euler(-35, 0, -45); leftArm = Quaternion.Euler(-35, 0, 45);
            }
            else if (state == EnemyState.Pulled)
            {
                targetLean = 25; rightArm = Quaternion.Euler(-100, 0, -20); leftArm = Quaternion.Euler(-100, 0, 20);
            }
            if (state == EnemyState.Airborne)
            {
                tumble += dt * 420; rightArm = Quaternion.Euler(-140, 0, -50); leftArm = Quaternion.Euler(-140, 0, 50);
            }
            else tumble = Mathf.MoveTowards(tumble % 360, 0, dt * 900);

            float k = 1 - Mathf.Exp(-18 * dt);
            rig.rightArm.localRotation = Quaternion.Slerp(rig.rightArm.localRotation, rightArm, strike > 0 ? 1 : k);
            rig.leftArm.localRotation = Quaternion.Slerp(rig.leftArm.localRotation, leftArm, k);
            rig.rightLeg.localRotation = Quaternion.Slerp(rig.rightLeg.localRotation, rightLeg, k);
            rig.leftLeg.localRotation = Quaternion.Slerp(rig.leftLeg.localRotation, leftLeg, k);
            lean = Mathf.Lerp(lean, targetLean, k);
            rig.root.localRotation = Quaternion.Euler(lean - tumble, 0, 0);
            rig.root.localPosition = Vector3.up * bob;
            if (rig.head) rig.head.localRotation = Quaternion.Euler(state == EnemyState.Windup ? 8 : 0, 0, 0);
            if (trail) trail.emitting = strike > 0 && !ranged;
            Tint(state);
        }

        void Tint(EnemyState state)
        {
            float pulse = state == EnemyState.Windup ? .35f + .25f * Mathf.Sin(Time.time * 22) : 0;
            if (pulse <= 0 && flash <= 0 && !tinted) return;
            tinted = pulse > 0 || flash > 0;
            if (merged)
            {
                // same result as the per-part lerps below, folded into one colour + coverage for the shader
                float f = flash > 0 ? .75f : 0, cover = 1 - (1 - pulse) * (1 - f);
                Color mix = cover > 0 ? (WarningColor * (pulse * (1 - f)) + Color.white * f) / cover : Color.white;
                merged.SetColor(FlashId, new Color(mix.r, mix.g, mix.b, cover));
                return;
            }
            for (int i = 0; i < materials.Count; i++)
            {
                if (!materials[i]) continue;
                Color c = Color.Lerp(baseColors[i], WarningColor, pulse);
                materials[i].color = flash > 0 ? Color.Lerp(c, Color.white, .75f) : c;
            }
        }

        void OnDestroy() { if (enemy && enemy.Health) enemy.Health.Damaged -= Hurt; }
    }
}
