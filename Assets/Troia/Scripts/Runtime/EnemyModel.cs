using UnityEngine;

namespace Troia
{
    // Chibi Trojan warriors built with the hero's helpers so both share proportions and shading.
    public static class EnemyModel
    {
        static readonly Color Skin = new Color(.66f, .48f, .38f);
        static readonly Color Leather = new Color(.24f, .14f, .08f);
        static readonly Color Steel = new Color(.62f, .6f, .55f);

        public struct Rig { public Transform root, head, rightArm, leftArm, rightLeg, leftLeg, weaponTip; }

        public static Rig Build(Transform parent, EnemySO data)
        {
            var r = new Rig { root = HeroModel.Joint(parent, "Troiano", Vector3.zero) };
            r.root.localScale = Vector3.one * data.scale;
            bool bronze = data.look != EnemyLook.Archer;
            Torso(r.root, data, bronze);
            r.head = Head(r.root, data);
            r.rightArm = Arm(r.root, 1, data, bronze); r.leftArm = Arm(r.root, -1, data, bronze);
            r.rightLeg = Leg(r.root, 1, data, bronze); r.leftLeg = Leg(r.root, -1, data, bronze);
            if (data.look == EnemyLook.Archer) { Bow(r.leftArm, data); Quiver(r.root); }
            else
            {
                r.weaponTip = Sword(r.rightArm, data.look == EnemyLook.Hector ? 1.25f : 1);
                if (data.look != EnemyLook.Soldier) Shield(r.leftArm, data);
            }
            if (data.look == EnemyLook.Hector) Cape(r.root, data);
            return r;
        }

        static void Metal(Transform t, float metal = .6f)
        {
            var m = t.GetComponent<Renderer>().sharedMaterial; m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", .6f);
        }

        static void Torso(Transform rig, EnemySO d, bool bronze)
        {
            HeroModel.Loft(rig, "Tunica", Vector3.zero, d.tunic,
                new Vector3(.5f, .26f, .19f), new Vector3(.62f, .24f, .17f), new Vector3(.96f, .27f, .18f),
                new Vector3(1.08f, .23f, .15f), new Vector3(1.12f, .14f, .11f));
            var chest = HeroModel.Loft(rig, bronze ? "Couraca" : "Colete_couro", Vector3.zero, bronze ? d.armor : Leather,
                new Vector3(.68f, .255f, .19f), new Vector3(.86f, .295f, .205f), new Vector3(1.0f, .285f, .2f), new Vector3(1.09f, .24f, .16f));
            if (bronze) Metal(chest);
            HeroModel.Part(rig, "Cinto", PrimitiveType.Sphere, new Vector3(0, .66f, 0), new Vector3(.5f, .09f, .36f), Leather);
            // Pteruges: leather strips hanging from the belt.
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-80, 80, i / 6f) * Mathf.Deg2Rad;
                var strip = HeroModel.Part(rig, "Pteruge", PrimitiveType.Cube,
                    new Vector3(Mathf.Sin(a) * .25f, .55f, Mathf.Cos(a) * .18f), new Vector3(.07f, .17f, .025f), i % 2 == 0 ? Leather : d.accent);
                strip.localRotation = Quaternion.Euler(-8, a * Mathf.Rad2Deg, 0);
            }
            HeroModel.Part(rig, "Pescoco", PrimitiveType.Sphere, new Vector3(0, 1.16f, 0), new Vector3(.15f, .16f, .15f), Skin);
            if (bronze)
                for (int side = -1; side <= 1; side += 2)
                    Metal(HeroModel.Part(rig, "Ombreira", PrimitiveType.Sphere, new Vector3(side * .3f, 1.08f, 0), new Vector3(.22f, .12f, .22f), d.armor));
        }

        static Transform Head(Transform rig, EnemySO d)
        {
            var head = HeroModel.Joint(rig, "Cabeca", new Vector3(0, 1.56f, 0));
            HeroModel.Loft(head, "Rosto", Vector3.zero, Skin,
                new Vector3(-.38f, .14f, .14f), new Vector3(-.25f, .27f, .23f), new Vector3(0, .33f, .28f), new Vector3(.21f, .29f, .25f), new Vector3(.34f, .15f, .13f));
            for (int side = -1; side <= 1; side += 2)
            {
                HeroModel.Part(head, "Olho", PrimitiveType.Sphere, new Vector3(side * .12f, -.06f, .27f), new Vector3(.08f, .1f, .05f), new Color(.05f, .04f, .04f));
                HeroModel.Part(head, "Brilho", PrimitiveType.Sphere, new Vector3(side * .12f - .015f, -.04f, .295f), Vector3.one * .02f, Color.white);
                // Angry brows read at gameplay distance and set them apart from the hero.
                HeroModel.Part(head, "Sobrancelha", PrimitiveType.Cube, new Vector3(side * .12f, .005f, .28f), new Vector3(.11f, .025f, .03f), new Color(.15f, .09f, .06f))
                    .localRotation = Quaternion.Euler(0, 0, side * -18);
            }
            HeroModel.Part(head, "Barba", PrimitiveType.Sphere, new Vector3(0, -.27f, .14f), new Vector3(.3f, .16f, .2f), new Color(.17f, .1f, .06f));
            if (d.look == EnemyLook.Archer) PhrygianCap(head, d);
            else Helmet(head, d);
            return head;
        }

        static void Helmet(Transform head, EnemySO d)
        {
            Metal(HeroModel.Loft(head, "Elmo", Vector3.zero, d.armor,
                new Vector3(.02f, .345f, .305f), new Vector3(.15f, .355f, .315f), new Vector3(.28f, .29f, .26f), new Vector3(.37f, .16f, .15f), new Vector3(.4f, .02f, .02f)));
            Metal(HeroModel.Part(head, "Nasal", PrimitiveType.Cube, new Vector3(0, -.04f, .31f), new Vector3(.04f, .16f, .035f), d.armor));
            Metal(HeroModel.Part(head, "Nuca", PrimitiveType.Cube, new Vector3(0, -.12f, -.26f), new Vector3(.5f, .24f, .06f), d.armor));
            for (int side = -1; side <= 1; side += 2)
                Metal(HeroModel.Part(head, "Jugular", PrimitiveType.Cube, new Vector3(side * .31f, -.1f, .1f), new Vector3(.05f, .26f, .22f), d.armor));
            if (d.look == EnemyLook.Soldier) return;
            // Front-to-back horsehair crest; Hector's is taller.
            float height = d.look == EnemyLook.Hector ? .34f : .22f;
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f, a = Mathf.Lerp(-40, 120, t) * Mathf.Deg2Rad;
                var tuft = HeroModel.Part(head, "Crista", PrimitiveType.Cube,
                    new Vector3(0, .4f + Mathf.Cos(a) * .05f + height * .5f * Mathf.Sin(t * Mathf.PI), Mathf.Sin(a) * -.28f + .05f),
                    new Vector3(.06f, height * (1 - Mathf.Abs(t - .4f)), .1f), d.accent);
                tuft.localRotation = Quaternion.Euler(Mathf.Lerp(-30, 60, t), 0, 0);
            }
        }

        static void PhrygianCap(Transform head, EnemySO d)
        {
            HeroModel.Loft(head, "Gorro_frigio", Vector3.zero, d.tunic,
                new Vector3(-.02f, .35f, .31f), new Vector3(.15f, .36f, .32f), new Vector3(.3f, .28f, .27f), new Vector3(.42f, .15f, .16f));
            var tip = HeroModel.Part(head, "Ponta_gorro", PrimitiveType.Sphere, new Vector3(0, .43f, .14f), new Vector3(.17f, .2f, .26f), d.tunic);
            tip.localRotation = Quaternion.Euler(-35, 0, 0);
            HeroModel.Part(head, "Faixa", PrimitiveType.Cube, new Vector3(0, .02f, .3f), new Vector3(.5f, .045f, .04f), d.accent);
            for (int side = -1; side <= 1; side += 2)
                HeroModel.Part(head, "Aba_gorro", PrimitiveType.Cube, new Vector3(side * .32f, -.16f, 0), new Vector3(.05f, .3f, .2f), d.tunic);
        }

        static Transform Arm(Transform rig, int side, EnemySO d, bool bronze)
        {
            var arm = HeroModel.Joint(rig, side > 0 ? "Braco_dir" : "Braco_esq", new Vector3(side * .32f, 1.065f, 0));
            HeroModel.Loft(arm, "Manga", Vector3.zero, d.tunic, new Vector3(-.14f, .085f, .085f), new Vector3(-.05f, .11f, .11f), new Vector3(0, .09f, .09f));
            HeroModel.Part(arm, "Antebraco", PrimitiveType.Capsule, new Vector3(0, -.22f, 0), new Vector3(.13f, .12f, .13f), Skin);
            var bracer = HeroModel.Part(arm, "Bracadeira", PrimitiveType.Cylinder, new Vector3(0, -.27f, 0), new Vector3(.155f, .06f, .155f), bronze ? d.armor : Leather);
            if (bronze) Metal(bracer);
            HeroModel.Part(arm, "Mao", PrimitiveType.Sphere, new Vector3(0, -HeroModel.HandLength, 0), new Vector3(.15f, .15f, .15f), Skin);
            return arm;
        }

        static Transform Leg(Transform rig, int side, EnemySO d, bool bronze)
        {
            var leg = HeroModel.Joint(rig, side > 0 ? "Perna_dir" : "Perna_esq", new Vector3(side * .14f, .57f, 0));
            HeroModel.Part(leg, "Coxa", PrimitiveType.Capsule, new Vector3(0, -.15f, 0), new Vector3(.17f, .16f, .17f), Skin);
            var greave = HeroModel.Loft(leg, bronze ? "Cnemide" : "Perneira", Vector3.zero, bronze ? d.armor : Leather,
                new Vector3(-.47f, .1f, .1f), new Vector3(-.36f, .115f, .12f), new Vector3(-.24f, .1f, .11f));
            if (bronze) Metal(greave);
            HeroModel.Part(leg, "Sandalia", PrimitiveType.Sphere, new Vector3(0, -.5f, .06f), new Vector3(.22f, .13f, .33f), Leather);
            return leg;
        }

        static Transform Sword(Transform arm, float length)
        {
            var root = HeroModel.Joint(arm, "Espada_troiana", Vector3.down * HeroModel.HandLength);
            HeroModel.Blade(root, "Lamina", Vector3.zero, Steel, .02f,
                new Vector2(-.07f, 0), new Vector2(-.09f, .55f * length), new Vector2(0, .95f * length), new Vector2(.09f, .55f * length), new Vector2(.07f, 0));
            Metal(HeroModel.Part(root, "Guarda", PrimitiveType.Cube, Vector3.zero, new Vector3(.26f, .07f, .08f), new Color(.7f, .5f, .2f)));
            HeroModel.Part(root, "Cabo", PrimitiveType.Cube, Vector3.back * .12f, new Vector3(.06f, .07f, .22f), Leather);
            return HeroModel.Joint(root, "Ponta", Vector3.forward * .95f * length);
        }

        static void Shield(Transform arm, EnemySO d)
        {
            var root = HeroModel.Joint(arm, "Escudo", new Vector3(-.14f, -.2f, .08f));
            root.localRotation = Quaternion.Euler(0, -90, 0);
            float size = d.look == EnemyLook.Hector ? .85f : .7f;
            Metal(HeroModel.Part(root, "Face", PrimitiveType.Cylinder, Vector3.zero, new Vector3(size, .03f, size), d.armor).Rotated(90));
            HeroModel.Part(root, "Anel", PrimitiveType.Cylinder, Vector3.forward * .02f, new Vector3(size * .78f, .03f, size * .78f), d.accent).Rotated(90);
            HeroModel.Part(root, "Campo", PrimitiveType.Cylinder, Vector3.forward * .035f, new Vector3(size * .66f, .03f, size * .66f), d.armor).Rotated(90);
            Metal(HeroModel.Part(root, "Umbo", PrimitiveType.Sphere, Vector3.forward * .05f, new Vector3(.16f, .16f, .1f), d.accent));
        }

        static void Bow(Transform arm, EnemySO d)
        {
            var root = HeroModel.Joint(arm, "Arco", Vector3.down * HeroModel.HandLength);
            const int segments = 6;
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Lerp(-55, 55, i / (float)segments) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-55, 55, (i + 1) / (float)segments) * Mathf.Deg2Rad;
                Vector3 p0 = new Vector3(0, Mathf.Cos(a0) * .35f - .25f, Mathf.Sin(a0) * .55f), p1 = new Vector3(0, Mathf.Cos(a1) * .35f - .25f, Mathf.Sin(a1) * .55f);
                var seg = HeroModel.Part(root, "Arco_seg", PrimitiveType.Cylinder, (p0 + p1) * .5f, new Vector3(.035f, (p1 - p0).magnitude * .55f, .035f), Leather);
                seg.localRotation = Quaternion.FromToRotation(Vector3.up, p1 - p0);
            }
            float reach = Mathf.Sin(55 * Mathf.Deg2Rad) * .55f, y = Mathf.Cos(55 * Mathf.Deg2Rad) * .35f - .25f;
            HeroModel.Part(root, "Corda", PrimitiveType.Cube, new Vector3(0, y, 0), new Vector3(.008f, .008f, reach * 2), new Color(.85f, .82f, .7f));
            HeroModel.Part(root, "Empunhadura", PrimitiveType.Cube, new Vector3(0, .1f, 0), new Vector3(.05f, .05f, .12f), d.accent);
        }

        static void Quiver(Transform rig)
        {
            var root = HeroModel.Joint(rig, "Aljava", new Vector3(.12f, .95f, -.24f));
            root.localRotation = Quaternion.Euler(0, 0, -25);
            HeroModel.Part(root, "Tubo", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.13f, .22f, .13f), Leather);
            for (int i = 0; i < 4; i++)
                HeroModel.Part(root, "Pena", PrimitiveType.Cube, new Vector3((i - 1.5f) * .03f, .27f, (i % 2) * .03f), new Vector3(.02f, .1f, .04f), new Color(.9f, .88f, .8f));
        }

        static void Cape(Transform rig, EnemySO d)
        {
            var cape = HeroModel.Joint(rig, "Capa", new Vector3(0, 1.1f, -.17f));
            cape.localRotation = Quaternion.Euler(12, 0, 0);
            HeroModel.Loft(cape, "Pano", Vector3.zero, d.tunic,
                new Vector3(-.95f, .34f, .05f), new Vector3(-.6f, .3f, .05f), new Vector3(-.25f, .26f, .05f), new Vector3(0, .2f, .04f));
            for (int side = -1; side <= 1; side += 2)
                Metal(HeroModel.Part(rig, "Broche", PrimitiveType.Sphere, new Vector3(side * .2f, 1.1f, .17f), Vector3.one * .07f, d.accent), .8f);
        }

        static Transform Rotated(this Transform t, float x) { t.localRotation = Quaternion.Euler(x, 0, 0); return t; }
    }
}
