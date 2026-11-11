using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    // Authored low-poly geometry: tapered sections, separate garment layers and movable coat tails.
    public static class HeroModel
    {
        public static readonly Color Coat = new Color(.62f, .025f, .04f);
        public static readonly Color Leather = new Color(.035f, .028f, .035f);
        public static readonly Color Silver = new Color(.65f, .68f, .73f);
        static readonly Color Skin = new Color(.72f, .55f, .46f);
        public static Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 pos, Vector3 size, Color color, float metal = 0)
        {
            var t = CombatVisuals.Part(parent, name, shape, pos, size, color);
            Finish(t.GetComponent<Renderer>().sharedMaterial, metal); return t;
        }
        static void Finish(Material m, float metal)
        {
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
        public static void Body(Transform rig, out Transform rightArm, out Transform leftArm, out Transform rightLeg, out Transform leftLeg, out Transform[] tails)
        {
            Loft(rig, "Colete_couro", Vector3.zero, Leather, new Vector3(.83f,.18f,.12f),new Vector3(1.03f,.20f,.13f),new Vector3(1.38f,.29f,.155f),new Vector3(1.49f,.25f,.12f));
            Loft(rig, "Casaco_costas", new Vector3(0,0,-.055f), Coat, new Vector3(.87f,.22f,.11f),new Vector3(1.05f,.22f,.13f),new Vector3(1.39f,.31f,.145f),new Vector3(1.48f,.26f,.10f));
            Part(rig,"Peitoral",PrimitiveType.Cube,new Vector3(0,1.26f,.145f),new Vector3(.23f,.36f,.035f),Leather);
            for(int side=-1;side<=1;side+=2)
            {
                var lapel = Part(rig,"Lapela",PrimitiveType.Cube,new Vector3(side*.15f,1.35f,.15f),new Vector3(.105f,.36f,.045f),new Color(.5f,.025f,.04f)); lapel.localRotation=Quaternion.Euler(0,0,side*-22);
                Part(rig,"Gola_alta",PrimitiveType.Cube,new Vector3(side*.105f,1.52f,-.005f),new Vector3(.085f,.18f,.19f),Coat).localRotation=Quaternion.Euler(-12,0,side*-15);
                for(int i=0;i<3;i++) Part(rig,"Fecho_prateado",PrimitiveType.Cube,new Vector3(side*.10f,1.13f+i*.09f,.173f),new Vector3(.03f,.025f,.018f),Silver,.8f);
            }
            Part(rig,"Cinto",PrimitiveType.Cube,new Vector3(0,.94f,0),new Vector3(.47f,.065f,.29f),Leather);
            Part(rig,"Fivela",PrimitiveType.Cube,new Vector3(.035f,.94f,.16f),new Vector3(.095f,.07f,.025f),Silver,.85f);
            Loft(rig,"Pescoco",Vector3.zero,Skin,new Vector3(1.47f,.075f,.07f),new Vector3(1.63f,.07f,.065f));
            Loft(rig,"Rosto_esculpido",new Vector3(0,0,.018f),Skin,new Vector3(1.59f,.055f,.065f),new Vector3(1.64f,.095f,.085f),new Vector3(1.75f,.12f,.102f),new Vector3(1.85f,.105f,.09f),new Vector3(1.89f,.05f,.05f));
            Part(rig,"Nariz",PrimitiveType.Cube,new Vector3(0,1.722f,.121f),new Vector3(.033f,.055f,.035f),Skin).localRotation=Quaternion.Euler(-12,0,0);
            Part(rig,"Boca",PrimitiveType.Cube,new Vector3(0,1.666f,.108f),new Vector3(.06f,.009f,.009f),new Color(.3f,.17f,.16f));
            for(int side=-1;side<=1;side+=2)
            {
                Part(rig,"Olho",PrimitiveType.Cube,new Vector3(side*.05f,1.76f,.111f),new Vector3(.038f,.013f,.012f),new Color(.56f,.7f,.76f));
                Part(rig,"Sobrancelha",PrimitiveType.Cube,new Vector3(side*.05f,1.783f,.112f),new Vector3(.045f,.01f,.014f),Silver).localRotation=Quaternion.Euler(0,0,side*8);
            }
            Loft(rig,"Cabelo_prateado",new Vector3(0,0,-.02f),Silver,new Vector3(1.765f,.125f,.1f),new Vector3(1.86f,.134f,.105f),new Vector3(1.94f,.085f,.07f),new Vector3(1.95f,.01f,.01f));
            for(int i=0;i<9;i++)
            {
                float x=(i-4)*.028f;
                float tip=1.735f+(.12f-Mathf.Abs(x))*.65f;
                var strand=Loft(rig,"Mecha_frontal",new Vector3(x,1.84f,.092f-Mathf.Abs(x)*.25f),new Color(.79f,.8f,.82f),new Vector3(tip-1.84f,.004f,.005f),new Vector3(0,.025f,.024f),new Vector3(.08f,.015f,.015f));
                strand.localRotation=Quaternion.Euler(0,0,Mathf.Sign(x)*10);
            }
            rightArm=Arm(rig,1); leftArm=Arm(rig,-1); rightLeg=Leg(rig,1); leftLeg=Leg(rig,-1);
            tails=new Transform[4];
            for(int i=0;i<4;i++)
            {
                float side=i%2==0?-1:1; bool back=i<2;
                var tail=Joint(rig,"Aba_casaco_"+i,new Vector3(side*.17f,.94f,back?-.12f:.08f));
                Loft(tail,"Couro_vermelho",Vector3.zero,Coat,new Vector3(-.65f,.15f,.035f),new Vector3(-.55f,.155f,.04f),new Vector3(-.25f,.13f,.04f),new Vector3(0,.105f,.04f));
                tail.localRotation=Quaternion.Euler(back?12:-10,0,side*8); tails[i]=tail;
            }
            Pistol(rig,new Vector3(.255f,.84f,.03f),false); Pistol(rig,new Vector3(-.255f,.84f,.03f),true);
        }
        static Transform Arm(Transform rig,int side)
        {
            var arm=Joint(rig,"Braco_"+side,new Vector3(side*.32f,1.44f,0));
            Loft(arm,"Manga",Vector3.zero,Coat,new Vector3(-.5f,.065f,.065f),new Vector3(-.28f,.075f,.085f),new Vector3(-.1f,.10f,.10f),new Vector3(0,.10f,.09f));
            Loft(arm,"Punho_luva",Vector3.zero,Leather,new Vector3(-.61f,.06f,.07f),new Vector3(-.5f,.065f,.065f),new Vector3(-.44f,.07f,.07f));
            Part(arm,"Fivela_manga",PrimitiveType.Cube,new Vector3(0,-.455f,.068f),new Vector3(.09f,.025f,.015f),Silver,.75f); return arm;
        }
        static Transform Leg(Transform rig,int side)
        {
            var leg=Joint(rig,"Perna_"+side,new Vector3(side*.125f,.86f,0));
            Loft(leg,"Calca",Vector3.zero,Leather,new Vector3(-.68f,.068f,.075f),new Vector3(-.43f,.075f,.085f),new Vector3(-.34f,.09f,.10f),new Vector3(-.05f,.11f,.12f),new Vector3(0,.105f,.11f));
            Loft(leg,"Bota",Vector3.zero,new Color(.06f,.045f,.05f),new Vector3(-.80f,.085f,.1f),new Vector3(-.72f,.08f,.085f),new Vector3(-.4f,.083f,.085f));
            Loft(leg,"Pe_bota",new Vector3(0,-.86f,.055f),Leather,new Vector3(0,.07f,.13f),new Vector3(.035f,.085f,.155f),new Vector3(.1f,.075f,.12f),new Vector3(.12f,.05f,.08f));
            Loft(leg,"Sola",new Vector3(0,-.875f,.055f),new Color(.025f,.025f,.025f),new Vector3(0,.075f,.14f),new Vector3(.02f,.088f,.16f),new Vector3(.035f,.08f,.15f)); return leg;
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
