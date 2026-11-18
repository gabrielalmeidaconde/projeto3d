using UnityEngine;

namespace Troia
{
    public enum EnemyLook { Soldier, Archer, Captain, Hector }

    // One asset per enemy type: combat numbers and appearance live here instead of in EnemyCombat.
    [CreateAssetMenu(menuName = "Troia/Inimigo")]
    public class EnemySO : ScriptableObject
    {
        public EnemyKind kind;
        public string displayName = "SOLDADO";
        [Header("Vida e movimento")]
        public float maxHealth = 55;
        public float moveSpeed = 3.4f;
        [Tooltip("Distancia em que o NavMeshAgent para de se aproximar.")]
        public float stoppingDistance = 1.65f;
        [Tooltip("Raio em que o inimigo circula o jogador enquanto espera vez de atacar (0 = nao circula).")]
        public float circleRadius = 2.65f;
        [Header("Ataque")]
        [Tooltip("Distancia em que comeca a preparar o golpe.")]
        public float attackRange = 2.15f;
        [Tooltip("Alcance real do golpe e raio do aviso no chao.")]
        public float hitReach = 2.5f;
        [Range(10, 180)] public float hitArc = 65;
        public float damage = 10;
        public float knockback = 4;
        public float windup = .65f;
        public float recovery = 1.1f;
        [Tooltip("Dispara flechas em vez de golpe corpo a corpo.")]
        public bool ranged;
        public float projectileSpeed = 16;
        [Tooltip("Ignora stagger de golpes leves, nao pode ser puxado fora de stagger e acerta golpes pesados.")]
        public bool armored;
        [Header("Visual")]
        public EnemyLook look = EnemyLook.Soldier;
        [Tooltip("Corpo humanoide (o Mannequin da Quaternius em UAL1_Standard.fbx). Vazio = modelo chibi.")]
        public GameObject corpo;
        [Range(.7f, 1.6f)] public float scale = 1;
        public Color tunic = new Color(.55f, .25f, .18f);
        public Color armor = new Color(.62f, .45f, .2f);
        public Color accent = new Color(.6f, .08f, .08f);

        public static EnemySO Get(EnemyKind kind, GameplayConfig config = null)
        {
            config ??= GameManager.Instance ? GameManager.Instance.config : null;
            config ??= Resources.Load<GameplayConfig>("Troia/Gameplay");
            if (config && config.enemyTypes != null)
                foreach (var type in config.enemyTypes) if (type && type.kind == kind) return type;
            // Cached so a project without the assets does not create one instance per spawn.
            if (!fallback.TryGetValue(kind, out var cached) || !cached) fallback[kind] = cached = EnemyDefaults.Create(kind);
            return cached;
        }
        static readonly System.Collections.Generic.Dictionary<EnemyKind, EnemySO> fallback = new System.Collections.Generic.Dictionary<EnemyKind, EnemySO>();
    }

    public static class EnemyDefaults
    {
        public static EnemySO[] CreateAll()
        {
            var all = new EnemySO[4];
            for (int i = 0; i < all.Length; i++) all[i] = Create((EnemyKind)i);
            return all;
        }
        // Values match the original hardcoded tuning.
        public static EnemySO Create(EnemyKind kind)
        {
            var e = ScriptableObject.CreateInstance<EnemySO>(); e.kind = kind;
            switch (kind)
            {
                case EnemyKind.Archer:
                    e.name = "Arqueiro"; e.displayName = "ARQUEIRO"; e.look = EnemyLook.Archer;
                    e.maxHealth = 38; e.stoppingDistance = 9; e.circleRadius = 0; e.attackRange = 13; e.hitReach = 13;
                    e.windup = .95f; e.ranged = true; e.knockback = 2; e.scale = .95f;
                    e.tunic = new Color(.2f, .45f, .3f); e.armor = new Color(.45f, .3f, .16f); e.accent = new Color(.75f, .55f, .2f);
                    break;
                case EnemyKind.Captain:
                    e.name = "Capitao"; e.displayName = "CAPITÃO"; e.look = EnemyLook.Captain; e.armored = true;
                    e.maxHealth = 220; e.damage = 18; e.recovery = .85f; e.scale = 1.04f;
                    e.tunic = new Color(.5f, .12f, .1f); e.armor = new Color(.72f, .55f, .2f); e.accent = new Color(.85f, .1f, .08f);
                    break;
                case EnemyKind.Hector:
                    e.name = "Heitor"; e.displayName = "HEITOR"; e.look = EnemyLook.Hector; e.armored = true;
                    e.maxHealth = 650; e.damage = 24; e.attackRange = 3; e.hitReach = 3.5f; e.windup = .8f; e.recovery = .85f; e.scale = 1.12f;
                    e.tunic = new Color(.42f, .05f, .12f); e.armor = new Color(.85f, .66f, .25f); e.accent = new Color(.95f, .78f, .3f);
                    break;
                default:
                    e.name = "Soldado"; e.displayName = "SOLDADO";
                    break;
            }
            return e;
        }
    }
}
