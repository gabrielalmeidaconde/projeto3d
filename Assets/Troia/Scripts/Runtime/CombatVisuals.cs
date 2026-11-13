using UnityEngine;

namespace Troia
{
    public static class CombatVisuals
    {
        public static Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name; go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider) { collider.enabled = false; Object.Destroy(collider); }
            var shader = Shader.Find("Troia/Lit Sujo") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.AddComponent<OwnedMaterial>().material = material;
            return go.transform;
        }
        public static Transform Weapon(Transform actor, Color color)
        {
            var pivot = new GameObject("Braco_Arma").transform;
            pivot.SetParent(actor, false); pivot.localPosition = new Vector3(.5f, 1.2f, .15f);
            Part(pivot, "Espada", PrimitiveType.Cube, new Vector3(0, 0, .6f), new Vector3(.12f, .07f, 1.3f), color);
            Part(pivot, "Guarda", PrimitiveType.Cube, Vector3.zero, new Vector3(.4f, .12f, .13f), new Color(.7f, .45f, .12f));
            return pivot;
        }
        public static void Burst(Vector3 position, Color color, float radius = .35f)
        {
            var root = new GameObject("Impacto"); root.transform.position = position;
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = .22f; main.startSpeed = 4; main.startSize = .1f;
            main.startColor = color; main.loop = false; main.duration = .25f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.color = color; renderer.sharedMaterial = material;
            root.AddComponent<OwnedMaterial>().material = material;
            ps.Play(); ps.Emit(18); Object.Destroy(root, .6f);
        }
    }
    public class OwnedMaterial : MonoBehaviour
    {
        public Material material;
        void OnDestroy() { if (material) Destroy(material); }
    }
}
