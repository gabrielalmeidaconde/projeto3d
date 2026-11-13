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
        Transform[] coatTails;
        Transform rightPistol, leftPistol, hookTarget;
        LineRenderer hook;
        float hookTime;
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
            HeroModel.Body(rig, out rightArm, out leftArm, out rightLeg, out leftLeg, out coatTails);
            // Soft character fill keeps the dark leather and red coat readable in the night scene.
            var fill = Joint(rig, "Luz_personagem", new Vector3(-.7f, 2.1f, 1.1f)).gameObject.AddComponent<Light>();
            fill.type = LightType.Point; fill.color = new Color(1, .83f, .73f);
            fill.intensity = 1.1f; fill.range = 3; fill.shadows = LightShadows.None;
            rightPistol = HeroModel.Pistol(rightArm, Vector3.down * HeroModel.HandLength, false);
            leftPistol = HeroModel.Pistol(leftArm, Vector3.down * HeroModel.HandLength, true);
            rightPistol.localScale = leftPistol.localScale = Vector3.one * .8f;
            rightPistol.gameObject.SetActive(false); leftPistol.gameObject.SetActive(false);
            player.Events.OnStyleRankChanged += StyleChanged;
            Equip(player.Weapon);
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
            var root = Joint(arm, profile.displayName, Vector3.down * HeroModel.HandLength);
            root.localScale = Vector3.one * .85f;
            float length = profile.kind == WeaponKind.Spear ? 2.4f : profile.kind == WeaponKind.TwinBlades ? .85f : 1.25f;
            Color metal = Color.Lerp(profile.color, Color.white, .6f);
            if (profile.kind == WeaponKind.Scythe || profile.kind == WeaponKind.Axe)
            {
                length = profile.kind == WeaponKind.Scythe ? 1.9f : 1.35f;
                HeroModel.Part(root, "Haste", PrimitiveType.Cylinder, new Vector3(0, 0, .35f), new Vector3(.065f, length * .5f, .065f), HeroModel.Leather).localRotation = Quaternion.Euler(90,0,0);
                for (int i=0;i<4;i++) HeroModel.Part(root,"Anel",PrimitiveType.Cylinder,new Vector3(0,0,-.15f+i*.17f),new Vector3(.085f,.025f,.085f),HeroModel.Silver,.8f).localRotation=Quaternion.Euler(90,0,0);
                if (profile.kind == WeaponKind.Scythe)
                {
                    HeroModel.Blade(root,"Ceifadora_base",new Vector3(0,0,1.25f),metal,.025f,new Vector2(-.06f,-.12f),new Vector2(-.11f,.22f),new Vector2(.48f,.15f),new Vector2(.54f,-.02f));
                    HeroModel.Blade(root,"Ceifadora_curva",new Vector3(0,0,1.25f),metal,.02f,new Vector2(.48f,.15f),new Vector2(.88f,-.16f),new Vector2(1.05f,-.65f),new Vector2(.54f,-.02f));
                }
                else
                {
                    HeroModel.Blade(root,"Machado_cabeca",new Vector3(0,0,1),new Color(.38f,.4f,.45f),.07f,new Vector2(-.16f,-.22f),new Vector2(-.16f,.22f),new Vector2(.46f,.37f),new Vector2(.46f,-.38f));
                    HeroModel.Blade(root,"Machado_gume",new Vector3(0,0,1),metal,.025f,new Vector2(.46f,-.38f),new Vector2(.46f,.37f),new Vector2(.68f,.29f),new Vector2(.68f,-.31f));
                    HeroModel.Part(root,"Contrapeso",PrimitiveType.Cube,new Vector3(-.22f,0,1),new Vector3(.22f,.17f,.24f),HeroModel.Silver,.7f);
                }
            }
            else
            {
                HeroModel.Blade(root, "Lamina_forjada", Vector3.zero, metal, .023f, new Vector2(-.085f,0), new Vector2(-.075f,1.02f), new Vector2(0,1.34f), new Vector2(.075f,1.02f), new Vector2(.085f,0));
                HeroModel.Part(root,"Nervura",PrimitiveType.Cube,new Vector3(0,.025f,.49f),new Vector3(.022f,.018f,.95f),HeroModel.Silver,.8f);
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
        public void Shoot() { shootPose = .18f; audioFeedback?.Shoot(); }
        public void Pull(Transform target)
        {
            hookTarget=target; hookTime=.6f;
            if (!hook)
            {
                hook=new GameObject("Corrente_do_machado").AddComponent<LineRenderer>(); hook.transform.SetParent(transform,false);
                hook.positionCount=2; hook.startWidth=.035f; hook.endWidth=.02f;
                var material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); material.color=new Color(.8f,.55f,.3f);
                hook.sharedMaterial=material; hook.gameObject.AddComponent<OwnedMaterial>().material=material;
            }
        }
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
                    case AttackMotion.Uppercut: pose = Quaternion.Euler(Mathf.Lerp(65, -120, strike), -20, 15); break;
                    case AttackMotion.Overhead: pose = Quaternion.Euler(Mathf.Lerp(-145, 55, strike), 0, 22); break;
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
            if (shootPose > 0) { leftArm.localRotation = Quaternion.Euler(-85, -5, 8); if (!move) rightArm.localRotation=Quaternion.Euler(-85,5,-8); }
            leftPistol.gameObject.SetActive(shootPose>0); rightPistol.gameObject.SetActive(shootPose>0 && !move);
            if(rightWeapon) rightWeapon.gameObject.SetActive(!(shootPose>0 && !move));
            for(int i=0;i<coatTails.Length;i++)
            {
                float side=i%2==0?-1:1;
                float sway=Mathf.Sin(gait+(i%2)*Mathf.PI)*Mathf.Min(speed,8)*1.5f;
                coatTails[i].localRotation=Quaternion.Slerp(coatTails[i].localRotation,Quaternion.Euler((i<2?12:-10)+Mathf.Min(speed,10)*2+sway,spin*.025f,side*(8+Mathf.Min(speed,10))),1-Mathf.Exp(-10*dt));
            }
            hookTime-=dt;
            if(hook) { hook.enabled=hookTime>0 && hookTarget; if(hook.enabled) { hook.SetPosition(0,leftArm.position-Vector3.up*.35f); hook.SetPosition(1,hookTarget.position+Vector3.up); } }
            rig.localRotation = Quaternion.Euler(player.State == PlayerState.Rushing ? 18 : 0, spin, flip > 0 ? (1 - flip / .32f) * 360 : 0);
        }
        void OnDestroy() { if (player && player.Events) player.Events.OnStyleRankChanged -= StyleChanged; }
    }
}
