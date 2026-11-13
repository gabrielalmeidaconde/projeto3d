using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    // Rounded chibi geometry with a large expressive face and separately animated coat tails.
    public static class HeroModel
    {
        public static readonly Color Coat = new Color(.28f, .012f, .025f);
        public static readonly Color Leather = new Color(.035f, .028f, .035f);
        public static readonly Color Silver = new Color(.54f, .57f, .65f);
        static readonly Color Skin = new Color(.70f, .56f, .51f);
        static Shader cartoonShader;
        public static Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 pos, Vector3 size, Color color, float metal = 0)
        {
            var t = CombatVisuals.Part(parent, name, shape, pos, size, color);
            Finish(t.GetComponent<Renderer>().sharedMaterial, metal); return t;
        }
        static void Finish(Material m, float metal)
        {
            if (!cartoonShader) cartoonShader = Resources.Load<Shader>("Troia/HeroCartoon");
            if (cartoonShader) { Color color=m.color; m.shader=cartoonShader; m.color=color; }
            m.SetFloat("_Parallax", 0); m.SetFloat("_Smoothness", metal > 0 ? .65f : .28f); m.SetFloat("_Metallic", metal);
        }
        public static Transform Joint(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = pos; return t;
        }
        // Each ring describes height, radius across the body and radius front-to-back.
        public static Transform Loft(Transform parent, string name, Vector3 pos, Color color, params Vector3[] rings)
        {
            const int sides = 12;
            var vertices = new List<Vector3>(); var indices = new List<int>();
            foreach (var ring in rings)
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2 / sides;
                    vertices.Add(new Vector3(Mathf.Cos(a) * ring.y, ring.x, Mathf.Sin(a) * ring.z));
                }
            for (int r = 0; r < rings.Length - 1; r++)
                for (int j = 0; j < sides; j++)
                {
                    int a = r * sides + j, b = r * sides + (j + 1) % sides;
                    indices.AddRange(new[] { a, a + sides, b, b, a + sides, b + sides });
                }
            for (int j = 1; j < sides - 1; j++)
            {
                indices.AddRange(new[] { 0, j, j + 1 });
                int end = (rings.Length - 1) * sides; indices.AddRange(new[] { end, end + j + 1, end + j });
            }
            return MeshPart(parent, name, pos, color, vertices.ToArray(), indices.ToArray());
        }
        public static Transform Blade(Transform parent, string name, Vector3 pos, Color color, float thickness, params Vector2[] outline)
        {
            // Convex polygons; use several pieces for a curved or concave blade.
            int n = outline.Length; var v = new Vector3[n * 2]; var t = new List<int>();
            for (int i = 0; i < n; i++) { v[i] = new Vector3(outline[i].x, -thickness, outline[i].y); v[i + n] = new Vector3(outline[i].x, thickness, outline[i].y); }
            for (int i = 1; i < n - 1; i++) t.AddRange(new[] { 0, i + 1, i, n, n + i, n + i + 1 });
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; t.AddRange(new[] { i, j, i + n, j, j + n, i + n }); }
            var result = MeshPart(parent, name, pos, color, v, t.ToArray()); Finish(result.GetComponent<Renderer>().sharedMaterial, .65f); return result;
        }
        static Transform MeshPart(Transform parent, string name, Vector3 pos, Color color, Vector3[] vertices, int[] triangles)
        {
            var root = Joint(parent, name, pos); root.gameObject.layer = 2;
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Troia/Lit Sujo")) { color = color }; Finish(material, 0);
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            root.gameObject.AddComponent<OwnedMaterial>().material = material;
            root.gameObject.AddComponent<OwnedMesh>().mesh = mesh; return root;
        }
        public const float HandLength = .37f;
        public static void Body(Transform rig, out Transform rightArm, out Transform leftArm, out Transform rightLeg, out Transform leftLeg, out Transform[] tails)
        {
            // Two-and-a-half-head silhouette: large face, short limbs and a flared little coat.
            Loft(rig, "Casaco_chibi", Vector3.zero, Coat,
                new Vector3(.54f,.23f,.15f), new Vector3(.69f,.24f,.17f),
                new Vector3(.96f,.28f,.18f), new Vector3(1.08f,.24f,.15f), new Vector3(1.12f,.15f,.11f));
            Part(rig,"Colete",PrimitiveType.Sphere,new Vector3(0,.88f,.13f),new Vector3(.29f,.43f,.15f),Leather);
            for(int side=-1;side<=1;side+=2)
            {
                var lapel=Part(rig,"Lapela_angulosa",PrimitiveType.Cube,new Vector3(side*.125f,1.01f,.19f),new Vector3(.09f,.32f,.04f),new Color(.45f,.018f,.035f));
                lapel.localRotation=Quaternion.Euler(0,0,side*-24);
                Part(rig,"Gola",PrimitiveType.Sphere,new Vector3(side*.14f,1.14f,-.025f),new Vector3(.16f,.18f,.22f),Coat);
                for(int i=0;i<2;i++) Part(rig,"Botao",PrimitiveType.Sphere,new Vector3(side*.09f,.79f+i*.105f,.216f),Vector3.one*.035f,Silver,.35f);
            }
            Part(rig,"Cinto",PrimitiveType.Sphere,new Vector3(0,.65f,0),new Vector3(.48f,.11f,.34f),Leather);
            Part(rig,"Fivela",PrimitiveType.Cube,new Vector3(0,.65f,.185f),new Vector3(.11f,.075f,.035f),Silver,.4f);
            Part(rig,"Pescoco",PrimitiveType.Sphere,new Vector3(0,1.16f,0),new Vector3(.15f,.16f,.15f),Skin);
            int firstHeadPart=rig.childCount;
            Loft(rig,"Rosto_anguloso",Vector3.zero,Skin,new Vector3(1.18f,.14f,.14f),new Vector3(1.31f,.27f,.23f),new Vector3(1.56f,.35f,.29f),new Vector3(1.77f,.30f,.25f),new Vector3(1.90f,.15f,.13f));
            for(int side=-1;side<=1;side+=2)
            {
                Part(rig,"Orelha",PrimitiveType.Sphere,new Vector3(side*.35f,1.49f,0),new Vector3(.11f,.17f,.1f),Skin);
                Part(rig,"Olho_branco",PrimitiveType.Sphere,new Vector3(side*.145f,1.51f,.31f),new Vector3(.15f,.088f,.06f),new Color(.96f,.97f,1));
                Part(rig,"Iris_azul",PrimitiveType.Sphere,new Vector3(side*.137f,1.51f,.35f),new Vector3(.075f,.066f,.028f),new Color(.58f,.055f,.045f));
                Part(rig,"Pupila",PrimitiveType.Sphere,new Vector3(side*.137f,1.509f,.366f),new Vector3(.032f,.049f,.018f),new Color(.025f,.035f,.065f));
                Part(rig,"Brilho_olhar",PrimitiveType.Sphere,new Vector3(side*.137f-.015f,1.525f,.379f),new Vector3(.012f,.014f,.012f),Color.white);
                Part(rig,"Palpebra",PrimitiveType.Cube,new Vector3(side*.145f,1.547f,.356f),new Vector3(.157f,.026f,.055f),new Color(.025f,.018f,.028f)).localRotation=Quaternion.Euler(0,0,side*12);
                Part(rig,"Sobrancelha",PrimitiveType.Capsule,new Vector3(side*.145f,1.633f,.315f),new Vector3(.027f,.083f,.025f),new Color(.32f,.33f,.39f)).localRotation=Quaternion.Euler(0,0,side*104);
            }
            Part(rig,"Nariz",PrimitiveType.Sphere,new Vector3(0,1.425f,.35f),new Vector3(.065f,.055f,.065f),Skin);
            Part(rig,"Boca_firme",PrimitiveType.Sphere,new Vector3(.018f,1.342f,.263f),new Vector3(.105f,.025f,.018f),new Color(.28f,.11f,.12f)).localRotation=Quaternion.Euler(0,0,-5);
            Part(rig,"Cabelo_nuca",PrimitiveType.Sphere,new Vector3(0,1.62f,-.11f),new Vector3(.77f,.69f,.57f),Silver);
            Loft(rig,"Cabelo_coroa",new Vector3(0,0,-.035f),Silver,
                new Vector3(1.65f,.32f,.265f),new Vector3(1.79f,.382f,.32f),
                new Vector3(1.96f,.245f,.215f),new Vector3(2.035f,.035f,.035f));
            // Broad, separated locks give the fringe a readable cartoon shape rather than fine strands.
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++)
                {
                    float x=side*(.075f+i*.095f), tip=1.64f-i*.06f;
                    var lockRoot=Joint(rig,"Mecha_"+side+"_"+i,new Vector3(x,1.82f,.315f-i*.02f));
                    Loft(lockRoot,"Mecha_esculpida",Vector3.zero,new Color(.67f,.69f,.75f),
                        new Vector3(tip-1.82f,.006f,.012f),new Vector3(-.035f,.075f,.065f),new Vector3(.105f,.065f,.055f),new Vector3(.15f,.015f,.015f));
                    lockRoot.localRotation=Quaternion.Euler(-10,0,side*(14+i*4));
                }
                var sideburn=Joint(rig,"Mecha_lateral",new Vector3(side*.325f,1.69f,-.015f));
                Loft(sideburn,"Ponta_lateral",Vector3.zero,Silver,new Vector3(-.29f,.005f,.01f),new Vector3(-.05f,.08f,.13f),new Vector3(.12f,.07f,.12f));
                sideburn.localRotation=Quaternion.Euler(-8,0,side*10);
            }
            Part(rig,"Cicatriz",PrimitiveType.Cube,new Vector3(-.225f,1.39f,.245f),new Vector3(.018f,.105f,.018f),new Color(.33f,.055f,.065f)).localRotation=Quaternion.Euler(0,0,-24);
            var head=Joint(rig,"Cabeca_edge",new Vector3(0,1.56f,0));
            for(int i=rig.childCount-2;i>=firstHeadPart;i--)
            {
                var part=rig.GetChild(i); Vector3 position=part.localPosition;
                part.SetParent(head,false); part.localPosition=position-head.localPosition;
            }
            head.localScale=new Vector3(.84f,.9f,.88f);
            rightArm=Arm(rig,1); leftArm=Arm(rig,-1); rightLeg=Leg(rig,1); leftLeg=Leg(rig,-1);
            tails=new Transform[4];
            for(int i=0;i<4;i++)
            {
                float side=i%2==0?-1:1; bool back=i<2;
                var tail=Joint(rig,"Aba_casaco_"+i,new Vector3(side*.17f,.62f,back?-.12f:.08f));
                Loft(tail,"Aba_curva",Vector3.zero,Coat,new Vector3(-.38f,.125f,.04f),new Vector3(-.31f,.15f,.045f),new Vector3(-.12f,.13f,.05f),new Vector3(0,.105f,.04f));
                tail.localRotation=Quaternion.Euler(back?12:-10,0,side*8); tails[i]=tail;
            }
            Pistol(rig,new Vector3(.28f,.57f,.03f),false).localScale=Vector3.one*.75f;
            Pistol(rig,new Vector3(-.28f,.57f,.03f),true).localScale=Vector3.one*.75f;
        }
        static Transform Arm(Transform rig,int side)
        {
            var arm=Joint(rig,"Braco_"+side,new Vector3(side*.32f,1.065f,0));
            Loft(arm,"Manga_couro",Vector3.zero,Coat,new Vector3(-.29f,.075f,.08f),new Vector3(-.19f,.105f,.11f),new Vector3(-.06f,.12f,.12f),new Vector3(0,.09f,.09f));
            Part(arm,"Punho",PrimitiveType.Sphere,new Vector3(0,-.285f,0),new Vector3(.18f,.095f,.18f),new Color(.38f,.018f,.035f));
            Part(arm,"Luva",PrimitiveType.Sphere,new Vector3(0,-HandLength,0),new Vector3(.18f,.19f,.18f),Leather);
            Part(arm,"Bracelete",PrimitiveType.Cube,new Vector3(0,-.285f,0),new Vector3(.19f,.055f,.19f),Leather);
            Part(arm,"Botao_manga",PrimitiveType.Sphere,new Vector3(0,-.28f,.09f),Vector3.one*.035f,Silver,.3f); return arm;
        }
        static Transform Leg(Transform rig,int side)
        {
            var leg=Joint(rig,"Perna_"+side,new Vector3(side*.14f,.57f,0));
            Part(leg,"Calca_curta",PrimitiveType.Capsule,new Vector3(0,-.18f,0),new Vector3(.205f,.19f,.22f),Leather);
            Loft(leg,"Botinha",Vector3.zero,new Color(.075f,.065f,.09f),new Vector3(-.5f,.115f,.12f),new Vector3(-.42f,.115f,.12f),new Vector3(-.29f,.105f,.105f));
            Part(leg,"Bota_arredondada",PrimitiveType.Sphere,new Vector3(0,-.485f,.07f),new Vector3(.255f,.18f,.37f),Leather);
            Part(leg,"Sola",PrimitiveType.Sphere,new Vector3(0,-.55f,.065f),new Vector3(.26f,.065f,.375f),new Color(.02f,.025f,.04f));
            Part(leg,"Fivela_bota",PrimitiveType.Cube,new Vector3(0,-.34f,.11f),new Vector3(.095f,.045f,.022f),Silver,.25f); return leg;
        }
        public static Transform Pistol(Transform parent,Vector3 pos,bool dark)
        {
            var root=Joint(parent,dark?"Pistola_negra":"Pistola_prata",pos); Color c=dark?new Color(.07f,.075f,.085f):Silver;
            Part(root,"Ferrolho",PrimitiveType.Cube,new Vector3(0,0,.1f),new Vector3(.06f,.08f,.33f),c,.7f);
            Part(root,"Cano",PrimitiveType.Cylinder,new Vector3(0,-.01f,.27f),new Vector3(.043f,.035f,.043f),Leather,.5f).localRotation=Quaternion.Euler(90,0,0);
            Part(root,"Empunhadura",PrimitiveType.Cube,new Vector3(0,-.085f,-.025f),new Vector3(.055f,.14f,.075f),Leather).localRotation=Quaternion.Euler(15,0,0); return root;
        }
    }
    public class OwnedMesh : MonoBehaviour
    {
        public Mesh mesh;
        void OnDestroy() { if(mesh) Destroy(mesh); }
    }
}
