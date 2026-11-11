using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Troia
{
    public enum MarkerKind
    {
        PlayerSpawn,
        EnemySpawn,
        CombatArena,
        Objective,
        Checkpoint,
        Barrier
    }

    /// <summary>
    /// Marcador de level design para o modo de arenas com ondas (estilo "palácio de
    /// ondas"): início do jogador, arenas, pontos de surgimento de inimigos, barreiras
    /// que trancam a arena durante as ondas, objetivos e checkpoints. Por enquanto só
    /// desenha gizmos; o sistema de ondas/combate vai ler estes marcadores depois.
    /// </summary>
    public class LevelMarker : MonoBehaviour
    {
        public MarkerKind kind;
        public string label;
        [TextArea(2, 5)] public string notes;
        [Tooltip("Ordem na progressão da fase (arenas, objetivos e checkpoints).")]
        public int order;
        [Tooltip("Tamanho da área (usado pelas arenas).")]
        public Vector3 size = new Vector3(10f, 6f, 10f);
        [Tooltip("Arena à qual este marcador pertence (spawns e barreiras).")]
        public int arena;
        [Tooltip("Arenas: quantidade de ondas sugerida.")]
        public int waves;
        [Tooltip("Barreiras: true = bloqueia a entrada; false = bloqueia a saída (abre ao limpar as ondas).")]
        public bool isEntrance;

        void OnDrawGizmos()
        {
            Color c;
            switch (kind)
            {
                case MarkerKind.PlayerSpawn: c = new Color(0.2f, 1f, 0.3f); break;
                case MarkerKind.EnemySpawn: c = new Color(1f, 0.25f, 0.2f); break;
                case MarkerKind.CombatArena: c = new Color(1f, 0.6f, 0.1f); break;
                case MarkerKind.Objective: c = new Color(1f, 0.9f, 0.2f); break;
                case MarkerKind.Barrier: c = new Color(1f, 0.35f, 0.05f); break;
                default: c = new Color(0.3f, 0.7f, 1f); break;
            }

            Gizmos.color = c;
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            switch (kind)
            {
                case MarkerKind.CombatArena:
                    Gizmos.DrawWireCube(Vector3.up * size.y * 0.5f, size);
                    Gizmos.color = new Color(c.r, c.g, c.b, 0.06f);
                    Gizmos.DrawCube(Vector3.up * size.y * 0.5f, size);
                    break;
                case MarkerKind.PlayerSpawn:
                    Gizmos.DrawWireSphere(Vector3.up * 0.9f, 0.5f);
                    Gizmos.DrawLine(Vector3.up * 0.9f, Vector3.up * 0.9f + Vector3.forward * 1.5f);
                    break;
                case MarkerKind.EnemySpawn:
                    Gizmos.DrawWireSphere(Vector3.up * 0.9f, 0.6f);
                    Gizmos.DrawLine(Vector3.zero, Vector3.up * 1.8f);
                    break;
                case MarkerKind.Objective:
                    Gizmos.DrawWireCube(Vector3.up * 1.5f, new Vector3(1.5f, 3f, 1.5f));
                    Gizmos.DrawLine(Vector3.zero, Vector3.up * 6f);
                    break;
                case MarkerKind.Barrier:
                    Gizmos.DrawWireCube(Vector3.up * size.y * 0.5f, size);
                    Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
                    Gizmos.DrawCube(Vector3.up * size.y * 0.5f, size);
                    break;
                default:
                    Gizmos.DrawWireCube(Vector3.up * 1f, new Vector3(3f, 2f, 0.3f));
                    break;
            }
            Gizmos.matrix = old;

#if UNITY_EDITOR
            if (kind != MarkerKind.EnemySpawn && !string.IsNullOrEmpty(label))
            {
                float h = kind == MarkerKind.CombatArena ? size.y + 1f : 3f;
                Handles.color = c;
                Handles.Label(transform.position + Vector3.up * h, label);
            }
#endif
        }
    }
}
