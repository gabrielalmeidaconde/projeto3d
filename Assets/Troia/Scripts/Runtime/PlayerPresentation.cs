using UnityEngine;

namespace Troia
{
    public class PlayerPresentation : MonoBehaviour
    {
        PlayerCombat player;
        Transform rig, rightArm, leftArm, rightLeg, leftLeg, rightWeapon, leftWeapon;
        CombatAudio audioFeedback;
        TrailRenderer rightTrail, leftTrail;
        float gait, flip, shootPose;
        int previousRank;
        Vector3 previousPosition;

        public void Initialize(PlayerCombat combat)
        {
            player = combat; previousPosition = transform.position;
            audioFeedback = GetComponent<CombatAudio>() ?? gameObject.AddComponent<CombatAudio>();
            foreach (string name in new[] { "Corpo", "Crista_do_Elmo" })
            {
                var old = transform.Find(name); if (old) old.gameObject.SetActive(false);
            }
            rig = Joint(transform, "Guerreiro", Vector3.zero);
            Color bronze = new Color(.56f, .36f, .16f), cloth = new Color(.4f, .07f, .08f), skin = new Color(.67f, .44f, .29f);
            CombatVisuals.Part(rig, "Couraca", PrimitiveType.Cube, new Vector3(0, 1.22f, 0), new Vector3(.64f, .68f, .35f), bronze);
            CombatVisuals.Part(rig, "Cinto", PrimitiveType.Cube, new Vector3(0, .91f, 0), new Vector3(.69f, .12f, .4f), new Color(.85f, .65f, .25f));
            CombatVisuals.Part(rig, "Tunica", PrimitiveType.Cube, new Vector3(0, .72f, 0), new Vector3(.59f, .35f, .34f), cloth);
            CombatVisuals.Part(rig, "Elmo", PrimitiveType.Sphere, new Vector3(0, 1.73f, 0), new Vector3(.43f, .46f, .43f), bronze);
            CombatVisuals.Part(rig, "Rosto", PrimitiveType.Cube, new Vector3(0, 1.68f, .18f), new Vector3(.25f, .23f, .11f), skin);
            CombatVisuals.Part(rig, "Crista", PrimitiveType.Cube, new Vector3(0, 2.02f, -.04f), new Vector3(.08f, .25f, .49f), cloth);
            rightArm = Limb("Braco_D", new Vector3(.41f, 1.47f, 0), .5f, skin);
            leftArm = Limb("Braco_E", new Vector3(-.41f, 1.47f, 0), .5f, skin);
            rightLeg = Limb("Perna_D", new Vector3(.19f, .65f, 0), .58f, bronze);
            leftLeg = Limb("Perna_E", new Vector3(-.19f, .65f, 0), .58f, bronze);
            player.Events.OnStyleRankChanged += StyleChanged;
            Equip(player.Weapon);
        }
        Transform Limb(string name, Vector3 position, float length, Color color)
        {
            var pivot = Joint(rig, name, position);
            CombatVisuals.Part(pivot, name + "_malha", PrimitiveType.Capsule, Vector3.down * length * .5f, new Vector3(.18f, length * .5f, .2f), color);
            return pivot;
        }
        static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var joint = new GameObject(name).transform; joint.SetParent(parent, false); joint.localPosition = position; return joint;
        }
        public void Equip(WeaponSO profile)
        {
            if (!rig || !profile) return;
            if (rightWeapon) { rightWeapon.gameObject.SetActive(false); Destroy(rightWeapon.gameObject); }
            if (leftWeapon) { leftWeapon.gameObject.SetActive(false); Destroy(leftWeapon.gameObject); }
            rightWeapon = CreateWeapon(rightArm, profile, out rightTrail);
            leftWeapon = null; leftTrail = null;
            if (profile.kind == WeaponKind.TwinBlades) leftWeapon = CreateWeapon(leftArm, profile, out leftTrail);
        }
        Transform CreateWeapon(Transform arm, WeaponSO profile, out TrailRenderer trail)
        {
            var root = Joint(arm, profile.displayName, Vector3.down * .5f);
            float length = profile.kind == WeaponKind.Spear ? 2.4f : profile.kind == WeaponKind.TwinBlades ? .85f : 1.25f;
            Color metal = Color.Lerp(profile.color, Color.white, .6f);
            if (profile.kind == WeaponKind.Spear)
            {
                CombatVisuals.Part(root, "Haste", PrimitiveType.Cube, new Vector3(0, 0, .45f), new Vector3(.065f, .065f, 2.3f), new Color(.27f, .13f, .07f));
                var tip = CombatVisuals.Part(root, "Ponta", PrimitiveType.Cube, new Vector3(0, 0, 1.7f), new Vector3(.16f, .035f, .47f), metal);
                tip.localRotation = Quaternion.Euler(0, 0, 45);
            }
            else
            {
                CombatVisuals.Part(root, "Lamina", PrimitiveType.Cube, new Vector3(0, 0, length * .5f), new Vector3(profile.kind == WeaponKind.TwinBlades ? .16f : .13f, .045f, length), metal);
                CombatVisuals.Part(root, "Guarda", PrimitiveType.Cube, Vector3.zero, new Vector3(.34f, .09f, .12f), profile.color);
                CombatVisuals.Part(root, "Punho", PrimitiveType.Cube, Vector3.back * .15f, new Vector3(.08f, .09f, .3f), new Color(.25f, .1f, .06f));
            }
            var endpoint = Joint(root, "Rastro", Vector3.forward * (profile.kind == WeaponKind.Spear ? 1.85f : length));
            trail = endpoint.gameObject.AddComponent<TrailRenderer>(); trail.time = .14f;
            trail.startWidth = profile.kind == WeaponKind.Spear ? .12f : .2f; trail.endWidth = 0;
            trail.minVertexDistance = .04f; trail.startColor = profile.color; trail.endColor = new Color(profile.color.r, profile.color.g, profile.color.b, 0);
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); material.color = profile.color;
            trail.sharedMaterial = material; endpoint.gameObject.AddComponent<OwnedMaterial>().material = material; trail.emitting = false;
            return root;
        }
        public void Attack() { if (player.Weapon) audioFeedback.Swing(player.Weapon.kind); }
        public void Impact(bool heavy) => audioFeedback?.Impact(heavy);
        public void Shoot() { shootPose = .15f; audioFeedback?.Shoot(); }
        public void Dodge() => audioFeedback?.Dodge();
        public void Jump(bool twice)
        {
            if (!twice) return; flip = .32f;
            CombatVisuals.Burst(transform.position + Vector3.up * .2f, player.Weapon ? player.Weapon.color : Color.cyan, .45f);
        }
        void StyleChanged(int rank)
        {
            if (rank > previousRank) audioFeedback?.Rank(rank); previousRank = rank;
        }
        void LateUpdate()
        {
            if (!rig || !player) return;
            float dt = Time.deltaTime;
            float speed = dt > 0 ? Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude / dt : 0;
            previousPosition = transform.position; gait += Mathf.Min(speed, 10) * dt * 2.8f;
            float walk = Mathf.Sin(gait) * Mathf.Clamp01(speed / 4) * 32;
            rightLeg.localRotation = Quaternion.Euler(player.IsGrounded ? walk : -35, 0, 0);
            leftLeg.localRotation = Quaternion.Euler(player.IsGrounded ? -walk : 25, 0, 0);
            rightArm.localRotation = Quaternion.Euler(-walk * .65f - 8, 0, -10);
            leftArm.localRotation = Quaternion.Euler(walk * .65f - 8, 0, 10);
            float spin = 0;
            var move = player.CurrentAttack;
            bool active = move && player.AttackTime >= move.hitStart * .65f && player.AttackTime <= move.hitEnd + .03f;
            if (rightTrail) rightTrail.emitting = active;
            if (leftTrail) leftTrail.emitting = active;
            if (move)
            {
                float t = Mathf.Clamp01(player.AttackTime / move.duration);
                float strike = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(move.hitStart * .55f, move.hitEnd, player.AttackTime));
                float settle = Mathf.InverseLerp(move.hitEnd, move.duration, player.AttackTime);
                Quaternion pose;
                switch (move.motion)
                {
                    case AttackMotion.Thrust: pose = Quaternion.Euler(-65 * Mathf.Sin(strike * Mathf.PI), 0, -20); break;
                    case AttackMotion.Uppercut: pose = Quaternion.Euler(Mathf.Lerp(65, -120, strike), -20, -35); break;
                    case AttackMotion.Overhead: pose = Quaternion.Euler(Mathf.Lerp(-145, 55, strike), 0, -20); break;
                    case AttackMotion.Spin: spin = strike * 360; pose = Quaternion.Euler(-65, -45, -75); break;
                    default: pose = Quaternion.Euler(-55, Mathf.Lerp(-105, 90, strike), -55); break;
                }
                rightArm.localRotation = Quaternion.Slerp(pose, Quaternion.Euler(-8, 0, -10), settle);
                if (player.Weapon && player.Weapon.kind == WeaponKind.TwinBlades)
                    leftArm.localRotation = Quaternion.Slerp(Quaternion.Euler(-50, Mathf.Lerp(100, -95, strike), 55), Quaternion.Euler(-8, 0, 10), settle);
                rig.localPosition = Vector3.up * (player.IsGrounded ? Mathf.Sin(t * Mathf.PI) * .04f : 0);
            }
            else rig.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(gait)) * .025f * Mathf.Clamp01(speed));
            flip = Mathf.Max(0, flip - dt); shootPose = Mathf.Max(0, shootPose - dt);
            if (shootPose > 0) leftArm.localRotation = Quaternion.Euler(-75, -10, 12);
            rig.localRotation = Quaternion.Euler(player.State == PlayerState.Rushing ? 18 : 0, spin, flip > 0 ? (1 - flip / .32f) * 360 : 0);
        }
        void OnDestroy() { if (player && player.Events) player.Events.OnStyleRankChanged -= StyleChanged; }
    }
}
