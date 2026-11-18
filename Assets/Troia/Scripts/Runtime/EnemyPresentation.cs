using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    // Apresentação do EnemyCombat, guiada só pelo estado e pelos tempos dele.
    // Com corpo humanoide (EnemyTroiano) toca os clipes da biblioteca do Pátroclo; sem ele, anima o modelo chibi.
    public class EnemyPresentation : MonoBehaviour
    {
        static readonly Color WarningColor = new Color(1, .18f, .055f);
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        EnemyCombat enemy;
        Vector3 previousPosition;
        float strike, flash, speed;
        bool tinted;
        const float StrikeTime = .22f;

        // chibi
        EnemyModel.Rig rig;
        TrailRenderer trail;
        readonly List<Material> materials = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();
        float gait, tumble, lean;

        // troiano
        Transform modelo, dardo;
        PatrocloAnimacoesSO biblioteca;
        PatrocloTocador tocador;
        Renderer[] renderers;
        MaterialPropertyBlock bloco;
        float relogio, fasePassos, tempoMorte;

        /// <summary>O inimigo tem animação de morte: o EnemyCombat espera ela antes de destruir o objeto.</summary>
        public bool TemMorte => tocador && biblioteca && biblioteca.Movimento("morte") != null;

        public void Initialize(EnemyCombat owner)
        {
            enemy = owner; previousPosition = transform.position;
            if (EnemyTroiano.Disponivel(owner.Data)) IniciarTroiano(owner.Data); else IniciarChibi(owner.Data);
            owner.Health.Damaged += Hurt;
        }

        void IniciarChibi(EnemySO data)
        {
            rig = EnemyModel.Build(transform, data);
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
        }

        void IniciarTroiano(EnemySO data)
        {
            // o equipamento é montado na pose de ligação, antes do primeiro quadro de animação
            var m = EnemyTroiano.Criar(transform, data);
            modelo = m.raiz; dardo = m.dardo;
            biblioteca = PatrocloAnimacoesSO.Carregar();
            tocador = modelo.gameObject.AddComponent<PatrocloTocador>(); tocador.Preparar(m.animador.avatar);
            renderers = modelo.GetComponentsInChildren<Renderer>(true);
            bloco = new MaterialPropertyBlock();
            AnimarTroiano(0); tocador.Firmar();
        }

        public void Strike() => strike = StrikeTime;
        void Hurt(DamageInfo info) => flash = .09f;

        // um golpe só por inimigo: nada dos combos do herói
        string ClipeGolpe() => enemy.Data.look == EnemyLook.Hector ? "Pesado" : "Corte_1";

        void LateUpdate()
        {
            if (!enemy) return;
            float dt = Time.deltaTime;
            speed = dt > 0 ? Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude / dt : 0;
            previousPosition = transform.position;
            strike = Mathf.Max(0, strike - dt); flash = Mathf.Max(0, flash - dt);
            if (tocador) { AnimarTroiano(dt); TintTroiano(enemy.State); }
            else if (rig.root) { AnimarChibi(dt); Tint(enemy.State); }
        }

        void AnimarTroiano(float dt)
        {
            relogio += dt;
            var state = enemy.State;
            bool ranged = enemy.Data.ranged;
            float transicao = .12f;
            PatrocloAnimacoesSO.Trecho Movimento(string id) => biblioteca.Movimento(id);
            void Trecho(PatrocloAnimacoesSO.Trecho t, string chave, float k) { if (t != null) tocador.Pedir(chave, t.clip, Mathf.Lerp(t.inicio, t.fim, Mathf.Clamp01(k))); }
            void Laco(string id, float tempo, float peso = 1) { var t = Movimento(id); if (t != null && peso > 0) tocador.Pedir(id, t.clip, tempo, peso); }

            if (state != EnemyState.Dead) tempoMorte = 0;
            if (dardo) dardo.gameObject.SetActive(state != EnemyState.Recovery);

            if (state == EnemyState.Dead)
            {
                tempoMorte += dt; Trecho(Movimento("morte"), "morte", tempoMorte / 1.6f); transicao = .1f;
            }
            else if (state == EnemyState.Airborne) { Laco("no_ar", relogio); transicao = .08f; }
            else if (state == EnemyState.Staggered || state == EnemyState.Pulled)
            {
                Trecho(Movimento("dano"), "dano", 1 - Mathf.Clamp01(enemy.StateTime / .4f)); transicao = .05f;
            }
            else if (state == EnemyState.Windup || state == EnemyState.Recovery)
            {
                var golpe = ranged ? Movimento("arremesso") : biblioteca.Golpe(ClipeGolpe());
                if (golpe != null)
                {
                    // o impacto do clipe cai no fim da preparação, quando o EnemyCombat aplica o golpe
                    float impacto = golpe.impacto > golpe.inicio ? golpe.impacto : Mathf.Lerp(golpe.inicio, golpe.fim, .45f);
                    if (state == EnemyState.Windup)
                    {
                        float k = enemy.WindupDuration > 0 ? 1 - Mathf.Clamp01(enemy.StateTime / enemy.WindupDuration) : 1;
                        tocador.Pedir("golpe", golpe.clip, Mathf.Lerp(golpe.inicio, impacto, Mathf.SmoothStep(0, 1, k)));
                        transicao = .1f;
                    }
                    else
                    {
                        // depois do impacto o clipe segue no tempo real; acabado, volta à guarda
                        float t = impacto + Mathf.Max(0, enemy.Data.recovery - enemy.StateTime);
                        if (t < golpe.fim) { tocador.Pedir("golpe", golpe.clip, t); transicao = .05f; }
                        else Laco("guarda", relogio);
                    }
                }
                else Laco("guarda", relogio);
            }
            else
            {
                // passadas sincronizadas como no herói: uma fase só, cada clipe no ponto equivalente do ciclo
                float v = speed / Mathf.Max(.5f, enemy.Data.scale);
                var andar = Movimento("andar"); var correr = Movimento("correr");
                float pParado = 1 - Mathf.InverseLerp(.3f, 1.4f, v);
                float pAndar = Mathf.Min(Mathf.InverseLerp(.3f, 1.4f, v), 1 - Mathf.InverseLerp(2f, 4f, v));
                float pCorrer = Mathf.InverseLerp(2f, 4f, v);
                float ciclo = pAndar * (andar?.clip.length ?? 1) + pCorrer * (correr?.clip.length ?? 1);
                if (ciclo > .01f) fasePassos = Mathf.Repeat(fasePassos + dt / ciclo * (pAndar + pCorrer), 1);
                Laco("guarda", relogio, pParado);
                if (andar != null) Laco("andar", fasePassos * andar.clip.length, pAndar);
                if (correr != null) Laco("correr", fasePassos * correr.clip.length, pCorrer);
            }
            tocador.Avaliar(dt, transicao);
        }

        void TintTroiano(EnemyState state)
        {
            float pulse = state == EnemyState.Windup ? .35f + .25f * Mathf.Sin(Time.time * 22) : 0;
            if (pulse <= 0 && flash <= 0)
            {
                if (!tinted) return;
                tinted = false;
                foreach (var r in renderers) if (r) r.SetPropertyBlock(null);
                return;
            }
            tinted = true;
            Color brilho = flash > 0 ? Color.white * .8f : WarningColor * (pulse * 1.6f);
            bloco.SetColor(EmissionId, brilho);
            foreach (var r in renderers) if (r) r.SetPropertyBlock(bloco);
        }

        void AnimarChibi(float dt)
        {
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
        }

        void Tint(EnemyState state)
        {
            float pulse = state == EnemyState.Windup ? .35f + .25f * Mathf.Sin(Time.time * 22) : 0;
            if (pulse <= 0 && flash <= 0 && !tinted) return;
            tinted = pulse > 0 || flash > 0;
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
