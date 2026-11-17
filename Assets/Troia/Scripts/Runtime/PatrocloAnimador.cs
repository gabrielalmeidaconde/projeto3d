using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Animação procedural do Pátroclo: gera uma <see cref="PoseAlvo"/> por quadro a partir do estado do combate
    /// (parado, correndo, no ar, esquiva, golpes por arma) e manda o rig resolver. Não usa clipes.
    /// </summary>
    public class PatrocloAnimador : MonoBehaviour
    {
        public struct Estado
        {
            public float velocidade;          // m/s no plano
            public Vector3 direcaoLocal;      // direção do movimento no espaço do personagem
            public bool noChao;
            public float velY;
            public PlayerState estado;
            public float tempoEstado;
            public ComboSO golpe;
            public float tempoGolpe;
            public int profundidade;          // passo do combo (alternância dos cortes)
            public WeaponKind arma;
            public float arremesso;           // 1 -> 0 depois de lançar um dardo
            public float cambalhota;          // 1 -> 0 no pulo duplo
            public Vector3 esquivaLocal;
            public bool emCombate;
            public float pouso;               // 1 -> 0 logo depois de tocar o chão
            public float curva;               // graus por segundo (inclina o corpo nas curvas)
        }

        public PatrocloRig Rig { get; private set; }
        public PoseAlvo Atual => atual;
        PoseAlvo atual;
        bool temAtual;
        float passo, respiro;

        public void Preparar(PatrocloDesign design)
        {
            // dedos fechados nas duas mãos; daqui em diante o painel não mexe mais nos ossos
            design.pose = PosePatroclo.Combate; design.Apply();
            design.pose = PosePatroclo.Livre;
            Rig = new PatrocloRig(design);
        }

        public void Avaliar(in Estado e, float dt)
        {
            if (Rig == null || !Rig.Valido) return;
            respiro += dt;
            PoseAlvo alvo = Montar(e, dt, out float rapidez);
            if (!temAtual || dt <= 0) { if (!temAtual) atual = alvo; temAtual = true; }
            else atual = PoseAlvo.Lerp(atual, alvo, 1 - Mathf.Exp(-rapidez * dt));
            // o giro do corpo inteiro não pode ser suavizado através da volta completa
            atual.giro = alvo.giro; atual.cambalhota = alvo.cambalhota;
            Rig.Resolver(atual);
        }

        // ------------------------------------------------------------------ utilidades
        static Vector3 D(float azimute, float elevacao)
        {
            float a = azimute * Mathf.Deg2Rad, el = elevacao * Mathf.Deg2Rad, c = Mathf.Cos(el);
            return new Vector3(Mathf.Sin(a) * c, Mathf.Sin(el), Mathf.Cos(a) * c);
        }
        static readonly Vector3 OmbroD = new Vector3(.223f, 1.47f, .02f), OmbroE = new Vector3(-.223f, 1.47f, .02f);
        static readonly Vector3 Pivo = new Vector3(0, 1.02f, 0);
        const float Tornozelo = .075f;

        /// <summary>Posição no espaço do personagem de um ponto preso ao tronco (acompanha quadril e peito).</summary>
        static Vector3 NoTronco(in PoseAlvo p, Vector3 local)
        {
            Quaternion t = Quaternion.Euler(p.quadrilRot) * Quaternion.Euler(p.peitoRot);
            return p.quadril + Pivo + t * (local - Pivo);
        }
        static Vector3 DirTronco(in PoseAlvo p, Vector3 dir) => Quaternion.Euler(p.quadrilRot) * Quaternion.Euler(p.peitoRot) * dir;

        /// <summary>Braço direito: mão a 'alcance' do ombro na direção (az, el) do tronco; lâmina em (laz, lel).</summary>
        static void BracoD(ref PoseAlvo p, float az, float el, float alcance, float laz, float lel)
        {
            p.maoD = NoTronco(p, OmbroD + D(az, el) * alcance);
            p.lamina = DirTronco(p, D(laz, lel));
            p.cotoveloD = DirTronco(p, new Vector3(.75f, -.45f, -.5f));
        }
        static void BracoE(ref PoseAlvo p, float az, float el, float alcance, float eaz, float eel)
        {
            p.maoE = NoTronco(p, OmbroE + D(az, el) * alcance);
            p.escudo = DirTronco(p, D(eaz, eel));
            p.cotoveloE = DirTronco(p, new Vector3(-.75f, -.5f, -.45f));
        }
        static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        static float Saida(float x, float p = 2.2f) => 1 - Mathf.Pow(1 - Mathf.Clamp01(x), p);

        // ------------------------------------------------------------------ poses de base
        PoseAlvo Guarda(in Estado e)
        {
            var p = new PoseAlvo();
            float r = Mathf.Sin(respiro * 1.7f);
            bool lanca = e.arma == WeaponKind.Spear;
            if (e.emCombate)
            {
                // de lado, escudo à frente, arma pronta
                p.quadril = new Vector3(0, -.075f + r * .004f, 0);
                p.quadrilRot = new Vector3(3, 20, 0); p.peitoRot = new Vector3(5 + r * .8f, 10, 0); p.cabecaRot = new Vector3(-2, -28, 0);
                p.peE = new Vector3(-.15f, Tornozelo, .26f); p.peD = new Vector3(.19f, Tornozelo, -.2f);
                p.peGiroE = 4; p.peGiroD = 48;
                p.joelhoE = new Vector3(-.15f, 0, 1); p.joelhoD = new Vector3(.7f, 0, .7f);
                if (lanca) BracoD(ref p, 35, -52, .36f, -12, 14);
                else BracoD(ref p, 30, -58, .4f, -22, 42);
                BracoE(ref p, -20, -22, .36f, -18, 2);
            }
            else
            {
                // descansado: espada grande apoiada no ombro
                p.quadril = new Vector3(.012f, -.012f + r * .004f, 0);
                p.quadrilRot = new Vector3(0, 6, 0); p.peitoRot = new Vector3(r * 1f, 4, 0); p.cabecaRot = new Vector3(-2, -8, 0);
                p.peE = new Vector3(-.13f, Tornozelo, .1f); p.peD = new Vector3(.15f, Tornozelo, -.07f);
                p.peGiroE = -4; p.peGiroD = 24;
                p.joelhoE = new Vector3(-.2f, 0, 1); p.joelhoD = new Vector3(.4f, 0, 1);
                if (lanca) { BracoD(ref p, 35, -70, .44f, 0, 80); }
                else
                {
                    p.maoD = NoTronco(p, new Vector3(.27f, 1.36f, .21f));
                    p.lamina = DirTronco(p, new Vector3(.1f, .7f, -.7f));
                    p.cotoveloD = DirTronco(p, new Vector3(1, -.55f, -.15f));
                }
                BracoE(ref p, -25, -62, .44f, -55, 0);
            }
            return p;
        }

        PoseAlvo Correr(in Estado e, float dt)
        {
            float v = e.velocidade;
            float amp = Mathf.Min(.26f + .055f * v, .64f);
            passo += dt * 2 * Mathf.PI * v / (4 * amp);
            float corrida = Mathf.InverseLerp(2.5f, 7.5f, v);
            Vector3 dir = e.direcaoLocal.sqrMagnitude > .01f ? e.direcaoLocal.normalized : Vector3.forward;
            float s = Mathf.Sin(passo), c = Mathf.Cos(passo);
            var p = new PoseAlvo();
            // dois impulsos por ciclo: o corpo sobe no meio de cada passada e desce no apoio
            float voo = Mathf.Abs(s);
            p.quadril = new Vector3(0, -.035f - .03f * corrida + voo * (.03f + .06f * corrida), 0) + dir * (.03f * corrida);
            float incl = Mathf.Lerp(6, 21, corrida);
            float tomba = Mathf.Clamp(e.curva * .035f, -14, 14);
            p.quadrilRot = new Vector3(incl * dir.z, 10 * c * dir.z, -incl * dir.x - 3 * s - tomba);
            p.peitoRot = new Vector3(5 * corrida * dir.z, -20 * c * dir.z, -tomba * .4f);
            p.cabecaRot = new Vector3(-incl * .7f * dir.z, 9 * c * dir.z, tomba * .5f);
            float alto = .13f + .22f * corrida;
            Pe(out p.peD, out p.peInclD, .12f, passo, amp, alto, corrida, dir);
            Pe(out p.peE, out p.peInclE, -.12f, passo + Mathf.PI, amp, alto, corrida, dir);
            p.peGiroD = 6; p.peGiroE = -6;
            p.joelhoD = new Vector3(.12f, 0, 1) + dir * .6f; p.joelhoE = new Vector3(-.12f, 0, 1) + dir * .6f;
            bool lanca = e.arma == WeaponKind.Spear;
            if (lanca)
            {
                // lança nivelada ao lado do corpo, apontada para a frente
                BracoD(ref p, 30 + 8 * c, -60 - 4 * corrida - 5 * s, .42f, -4, 4 - 6 * corrida);
            }
            else
            {
                // andando: espada no ombro; correndo: braço bombeia e a lâmina vai baixa, arrastada para trás
                float az = Mathf.Lerp(18, 50, corrida) - 22 * c * corrida, el = Mathf.Lerp(-22, -62, corrida) + 8 * c * corrida;
                BracoD(ref p, az, el, Mathf.Lerp(.3f, .44f, corrida), Mathf.Lerp(172, 166, corrida), Mathf.Lerp(46, -30, corrida) + 8 * c * corrida);
                p.cotoveloD = DirTronco(p, new Vector3(1, -.4f, -.5f));
            }
            // o braço do escudo bombeia em oposição à perna, com o escudo de lado para não tampar o corpo
            BracoE(ref p, -24 + 26 * c * corrida, -44 + 12 * corrida - 8 * c * corrida, .36f, -40 + 16 * corrida, 4);
            return p;
        }
        static void Pe(out Vector3 pos, out float incl, float x, float fase, float amp, float alto, float corrida, Vector3 dir)
        {
            float avanco = Mathf.Cos(fase) * amp, balanco = Mathf.Max(0, -Mathf.Sin(fase));
            float atras = Mathf.Clamp01(-avanco / amp), afrente = Mathf.Clamp01(avanco / amp);
            // no balanço o pé sobe; logo que sai do chão ele ainda chuta para trás (calcanhar alto)
            float y = Tornozelo + balanco * alto + balanco * atras * (.1f + .2f * corrida) + (1 - balanco) * atras * atras * .07f;
            pos = Vector3.right * x + dir * avanco + Vector3.up * y;
            float frente = Mathf.Sign(dir.z + .001f);
            incl = (balanco * atras * (40 + 30 * corrida) + (1 - balanco) * atras * 24 - balanco * afrente * 26) * frente;
        }

        PoseAlvo NoAr(in Estado e)
        {
            var p = Guarda(e);
            float sobe = Mathf.Clamp(e.velY / 9f, -1, 1);
            p.quadril = new Vector3(0, .02f, 0);
            p.quadrilRot = new Vector3(8 - 10 * sobe, 8, 0); p.peitoRot = new Vector3(4, 6, 0); p.cabecaRot = new Vector3(4 * sobe, -10, 0);
            // subindo: joelho da frente alto; caindo: pernas buscando o chão
            float a = Mathf.InverseLerp(-1, 1, sobe);
            p.peE = Vector3.Lerp(new Vector3(-.13f, .12f, .12f), new Vector3(-.13f, .46f, .26f), a);
            p.peD = Vector3.Lerp(new Vector3(.16f, .16f, -.14f), new Vector3(.15f, .3f, -.34f), a);
            p.peInclE = Mathf.Lerp(-12, 25, a); p.peInclD = Mathf.Lerp(18, 50, a);
            bool lanca = e.arma == WeaponKind.Spear;
            if (lanca) BracoD(ref p, 40, -30, .44f, 0, 20);
            else BracoD(ref p, 70, Mathf.Lerp(-20, -50, a), .48f, 150, Mathf.Lerp(40, -10, a));
            BracoE(ref p, -45, -25 + 20 * (1 - a), .42f, -30, 0);
            return p;
        }

        PoseAlvo Esquiva(in Estado e, float duracao)
        {
            var p = Guarda(e);
            Vector3 d = e.esquivaLocal.sqrMagnitude > .01f ? e.esquivaLocal.normalized : Vector3.forward;
            float t = Mathf.Clamp01(e.tempoEstado / Mathf.Max(.05f, duracao));
            float baixo = Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI);
            p.quadril = new Vector3(0, -.1f - .26f * baixo, 0) + d * (.1f * baixo);
            p.quadrilRot = new Vector3(26 * d.z * baixo + 6, 14 * d.x, -30 * d.x * baixo);
            p.peitoRot = new Vector3(14 * baixo, 0, -10 * d.x * baixo); p.cabecaRot = new Vector3(-24 * baixo * Mathf.Abs(d.z), 0, 0);
            Vector3 lado = new Vector3(d.z, 0, -d.x);
            p.peE = new Vector3(-.15f, Tornozelo, 0) + d * (.42f - .1f * t); p.peD = new Vector3(.15f, Tornozelo + .05f * baixo, 0) - d * (.48f + .1f * t);
            p.peInclD = 30 * baixo;
            p.joelhoE = new Vector3(-.2f, 0, 1) + d; p.joelhoD = new Vector3(.2f, 0, 1) + d * .3f;
            BracoD(ref p, 75, -45, .5f, 160, -12);
            BracoE(ref p, -12, -28, .3f, 0, 8);
            return p;
        }

        PoseAlvo Atordoado(in Estado e)
        {
            var p = Guarda(e);
            float t = Mathf.Clamp01(e.tempoEstado / .3f), k = Mathf.Sin(Mathf.Sqrt(t) * Mathf.PI);
            p.quadril += new Vector3(0, -.06f * k, -.1f * k);
            p.quadrilRot = new Vector3(-12 * k, 10, 4 * k); p.peitoRot = new Vector3(-20 * k, -14 * k, 6 * k); p.cabecaRot = new Vector3(16 * k, 12 * k, 0);
            p.peD += new Vector3(0, 0, -.16f * k);
            BracoD(ref p, 70, -40 + 20 * k, .46f, 120, 10);
            BracoE(ref p, -60 * k - 20, -25, .4f, -50, 10);
            return p;
        }

        PoseAlvo Investida(in Estado e)
        {
            // avanço (E): corpo deitado para a frente, escudo abrindo caminho, arma recolhida para o golpe
            var p = Guarda(e);
            float c = Mathf.Cos(e.tempoEstado * 34);
            p.quadril = new Vector3(0, -.2f, .08f);
            p.quadrilRot = new Vector3(30, 14, 0); p.peitoRot = new Vector3(10, 10, 0); p.cabecaRot = new Vector3(-34, -20, 0);
            p.peE = new Vector3(-.13f, Tornozelo + .05f * Mathf.Max(0, c), .5f * c); p.peD = new Vector3(.13f, Tornozelo + .05f * Mathf.Max(0, -c), -.5f * c);
            p.peInclD = 30; p.peInclE = 10;
            if (e.arma == WeaponKind.Spear) BracoD(ref p, 40, -40, .4f, -6, 2);
            else BracoD(ref p, 70, -45, .5f, 170, -18);
            BracoE(ref p, 0, -10, .4f, 0, 6);
            return p;
        }

        PoseAlvo Arremesso(in Estado e, PoseAlvo p)
        {
            // dardo: braço direito de trás para a frente por cima do ombro
            float k = 1 - e.arremesso;            // 0 no instante do lançamento
            float solto = Saida(k * 2.2f);
            p.peitoRot = new Vector3(Mathf.Lerp(-4, 14, solto), Mathf.Lerp(34, -30, solto), 0);
            p.quadrilRot.y = Mathf.Lerp(16, -6, solto);
            p.cabecaRot = new Vector3(-4, Mathf.Lerp(-40, 26, solto), 0);
            BracoD(ref p, Mathf.Lerp(60, -8, solto), Mathf.Lerp(40, -4, solto), Mathf.Lerp(.34f, .55f, solto), 0, Mathf.Lerp(12, -8, solto));
            p.cotoveloD = DirTronco(p, new Vector3(1, .1f, -.5f));
            BracoE(ref p, -30, -30, .36f, -20, 0);
            return p;
        }

        PoseAlvo Morto(in Estado e)
        {
            var p = Guarda(e);
            float t = Suave(e.tempoEstado / .7f);
            p.tombo = 86 * t; p.quadril = new Vector3(0, -.05f * t, 0);
            p.peitoRot = new Vector3(-8 * t, 0, 0); p.cabecaRot = new Vector3(-14 * t, 20 * t, 0);
            p.peE = Vector3.Lerp(p.peE, new Vector3(-.2f, Tornozelo, .18f), t); p.peD = Vector3.Lerp(p.peD, new Vector3(.22f, Tornozelo + .16f * t, .1f), t);
            BracoD(ref p, 85, -30, .5f, 90, -10);
            BracoE(ref p, -85, -35, .48f, -60, 20);
            return p;
        }

        // ------------------------------------------------------------------ golpes
        /// <summary>
        /// Tempos do golpe: preparo (até pouco antes de hitStart), golpe (até hitEnd), sustentação e volta à guarda.
        /// </summary>
        static void Fases(ComboSO m, float t, out float preparo, out float golpe, out float volta, out float depois)
        {
            float t0 = m.hitStart * .5f;
            preparo = Suave(t / Mathf.Max(.015f, t0));
            golpe = t <= t0 ? 0 : Saida(Mathf.InverseLerp(t0, Mathf.Lerp(m.hitEnd, m.hitStart, .25f), t));
            depois = Mathf.InverseLerp(m.hitEnd, m.duration, t);
            volta = Suave(Mathf.InverseLerp(.5f, 1, depois));
        }

        PoseAlvo Golpe(in Estado e, PoseAlvo guarda)
        {
            var m = e.golpe;
            Fases(m, e.tempoGolpe, out float preparo, out float k, out float volta, out float depois);
            k += .07f * Saida(depois * 2.5f) * (e.tempoGolpe >= m.hitEnd ? 1 : 0);   // a arma ainda escorre um pouco depois do impacto
            bool aereo = !e.noChao && !m.slam;
            PoseAlvo p;
            if (e.arma == WeaponKind.Shield) p = GolpeEscudo(m, e, k);
            else if (e.arma == WeaponKind.Spear) p = GolpeLanca(m, e, k);
            else p = GolpeEspada(m, e, k);
            if (aereo || (m.slam && !e.noChao)) PernasNoAr(ref p, k);
            p = PoseAlvo.Lerp(guarda, p, preparo);
            return PoseAlvo.Lerp(p, guarda, volta);
        }
        static void PernasNoAr(ref PoseAlvo p, float k)
        {
            p.quadril.y = Mathf.Max(p.quadril.y, -.02f);
            p.peE = new Vector3(-.13f, Mathf.Lerp(.44f, .2f, k), Mathf.Lerp(.24f, .05f, k));
            p.peD = new Vector3(.15f, Mathf.Lerp(.26f, .34f, k), Mathf.Lerp(-.2f, -.36f, k));
            p.peInclE = 20; p.peInclD = 45;
        }
        static void Base(ref PoseAlvo p, float frente, float baixo, float abertura)
        {
            // pés firmes: esquerdo à frente, direito atrás, quadril entre eles
            p.quadril = new Vector3(0, -baixo, frente);
            p.peE = new Vector3(-.16f, Tornozelo, .22f + abertura * .5f + frente * .5f);
            p.peD = new Vector3(.19f, Tornozelo, -.2f - abertura * .5f + frente * .2f);
            p.peGiroE = 2; p.peGiroD = 42; p.peInclD = 8 + 30 * Mathf.Clamp01(frente * 4);
            p.joelhoE = new Vector3(-.15f, 0, 1); p.joelhoD = new Vector3(.6f, 0, .8f);
        }

        PoseAlvo GolpeEspada(ComboSO m, in Estado e, float k)
        {
            var p = new PoseAlvo();
            float forca = m.finisher ? 1.25f : m.input != AttackInput.Light ? 1.12f : 1f;
            switch (m.motion)
            {
                case AttackMotion.Thrust:
                {
                    Base(ref p, Mathf.Lerp(-.08f, .24f, k), Mathf.Lerp(.06f, .12f, k), Mathf.Lerp(.1f, .5f, k));
                    // estocada: a perna direita dá o passo junto com o ombro; a de trás estica
                    p.peD = new Vector3(.11f, Tornozelo + Mathf.Sin(Mathf.Clamp01(k * 1.5f) * Mathf.PI) * .07f, Mathf.Lerp(-.22f, .6f, k));
                    p.peE = new Vector3(-.13f, Tornozelo, Mathf.Lerp(.14f, -.36f, k));
                    p.peGiroD = 4; p.peGiroE = -28; p.peInclE = 30 * k; p.joelhoD = new Vector3(.08f, 0, 1); p.joelhoE = new Vector3(-.3f, 0, 1);
                    p.quadrilRot = new Vector3(Mathf.Lerp(4, 16, k), Mathf.Lerp(34, -34, k), 0);
                    p.peitoRot = new Vector3(Mathf.Lerp(0, 12, k), Mathf.Lerp(26, -30, k), 0);
                    p.cabecaRot = new Vector3(-8 * k, Mathf.Lerp(-55, 55, k), 0);
                    BracoD(ref p, Mathf.Lerp(70, 6, k), Mathf.Lerp(-45, -8, k), Mathf.Lerp(.24f, .56f, k), Mathf.Lerp(40, 62, k), Mathf.Lerp(8, -2, k));
                    p.lamina = D(Mathf.Lerp(8, 0, k), Mathf.Lerp(8, -3, k));
                    BracoE(ref p, -30, -30, Mathf.Lerp(.4f, .3f, k), -30, 4);
                    break;
                }
                case AttackMotion.Overhead:
                {
                    Base(ref p, Mathf.Lerp(-.06f, .16f, k), Mathf.Lerp(-.03f, .26f * forca, k), Mathf.Lerp(.05f, .42f, k));
                    p.quadrilRot = new Vector3(Mathf.Lerp(-8, 24, k), Mathf.Lerp(20, -8, k), 0);
                    p.peitoRot = new Vector3(Mathf.Lerp(-18, 30, k), Mathf.Lerp(16, -14, k), 0);
                    p.cabecaRot = new Vector3(Mathf.Lerp(14, -40, k), Mathf.Lerp(-30, 18, k), 0);
                    float el = Mathf.Lerp(132, -42, k);
                    BracoD(ref p, Mathf.Lerp(18, -6, k), Mathf.Lerp(72, -38, k), Mathf.Lerp(.42f, .54f, k), 0, 0);
                    p.lamina = D(Mathf.Lerp(10, -6, k), el);
                    p.cotoveloD = new Vector3(1, .2f, -.2f);
                    BracoE(ref p, -35, Mathf.Lerp(-10, -45, k), .38f, -30, 10);
                    break;
                }
                case AttackMotion.Uppercut:
                {
                    Base(ref p, Mathf.Lerp(-.04f, .12f, k), Mathf.Lerp(.3f, -.05f, k), .3f);
                    p.quadrilRot = new Vector3(Mathf.Lerp(22, -8, k), Mathf.Lerp(-30, 26, k), 0);
                    p.peitoRot = new Vector3(Mathf.Lerp(18, -16, k), Mathf.Lerp(-34, 30, k), 0);
                    p.cabecaRot = new Vector3(Mathf.Lerp(-30, 16, k), Mathf.Lerp(50, -40, k), 0);
                    BracoD(ref p, Mathf.Lerp(-28, 22, k), Mathf.Lerp(-66, 62, k), Mathf.Lerp(.44f, .54f, k), 0, 0);
                    p.lamina = D(Mathf.Lerp(-55, 20, k), Mathf.Lerp(-58, 108, k));
                    p.cotoveloD = new Vector3(.8f, -.3f, -.5f);
                    BracoE(ref p, -40, Mathf.Lerp(-20, -50, k), .4f, -40, 0);
                    p.peInclD = 30 * k; p.peInclE = 14 * k;
                    break;
                }
                case AttackMotion.Spin:
                {
                    float voltas = Mathf.Max(1, m.hitCount);
                    p.giro = -Mathf.Repeat(360 * voltas * Mathf.Clamp01(k), 360);
                    Base(ref p, .04f, .2f, .3f);
                    p.quadrilRot = new Vector3(10, -10, 0); p.peitoRot = new Vector3(8, -24, 0); p.cabecaRot = new Vector3(-8, -30, 0);
                    BracoD(ref p, 84, -14, .54f, 96, -6);
                    p.cotoveloD = DirTronco(p, new Vector3(.3f, -.4f, -1));
                    BracoE(ref p, -70, -30, .4f, -80, 0);
                    p.peE = new Vector3(-.12f, Tornozelo, .06f); p.peD = new Vector3(.34f, Tornozelo + .05f, -.12f); p.peInclD = 20;
                    break;
                }
                default:
                {
                    // cortes alternados: ímpar vem da direita para a esquerda, par volta de revés
                    bool reves = (e.profundidade & 1) == 0;
                    float s = reves ? -1 : 1;
                    float sobe = m.finisher ? 1 : 0;
                    Base(ref p, Mathf.Lerp(-.05f, .15f, k), Mathf.Lerp(.08f, .16f * forca, k), Mathf.Lerp(.1f, .34f, k));
                    p.peE.y += Mathf.Sin(Mathf.Clamp01(k * 1.7f) * Mathf.PI) * .05f;
                    float tronco = Mathf.Lerp(52, -48, k) * s * forca;
                    p.quadrilRot = new Vector3(8, tronco * .45f, -4 * s * (k - .5f));
                    p.peitoRot = new Vector3(8, tronco * .55f, 0);
                    p.cabecaRot = new Vector3(-6, -tronco * .8f, 0);
                    // a lâmina varre 250 graus e passa pela frente no instante do impacto
                    float la = reves ? Mathf.Lerp(-142, 104, k) : Mathf.Lerp(150, -100, k);
                    float le = reves ? Mathf.Lerp(-18, 16 + 26 * sobe, k) : Mathf.Lerp(26 + 30 * sobe, -14 - 22 * sobe, k);
                    float az = reves ? Mathf.Lerp(-58, 78, k) : Mathf.Lerp(82, -52, k);
                    float el = reves ? Mathf.Lerp(-34, -6, k) : Mathf.Lerp(4 + 22 * sobe, -34, k);
                    BracoD(ref p, az, el, .54f * Mathf.Lerp(.84f, 1, Mathf.Sin(k * Mathf.PI)), 0, 0);
                    p.lamina = D(la, le);
                    p.cotoveloD = DirTronco(p, new Vector3(.7f, -.6f, -.4f));
                    BracoE(ref p, -34, -28, .38f, -26, 4);
                    break;
                }
            }
            return p;
        }

        PoseAlvo GolpeLanca(ComboSO m, in Estado e, float k)
        {
            var p = new PoseAlvo();
            switch (m.motion)
            {
                case AttackMotion.Slash:
                case AttackMotion.Spin:
                {
                    // varredura com a haste: o corpo todo gira e a ponta risca um círculo baixo
                    float voltas = m.motion == AttackMotion.Spin ? Mathf.Max(1, m.hitCount) : 0;
                    bool reves = (e.profundidade & 1) == 0 && voltas == 0;
                    float s = reves ? -1 : 1;
                    p.giro = -Mathf.Repeat(360 * voltas * Mathf.Clamp01(k), 360);
                    Base(ref p, Mathf.Lerp(-.04f, .12f, k), .2f, .36f);
                    float tronco = voltas > 0 ? -18 : Mathf.Lerp(50, -46, k) * s;
                    p.quadrilRot = new Vector3(10, tronco * .45f, 0); p.peitoRot = new Vector3(8, tronco * .55f, 0); p.cabecaRot = new Vector3(-6, -tronco * .8f, 0);
                    float la = voltas > 0 ? 88 : reves ? Mathf.Lerp(-130, 96, k) : Mathf.Lerp(138, -92, k);
                    BracoD(ref p, voltas > 0 ? 80 : (reves ? Mathf.Lerp(-40, 70, k) : Mathf.Lerp(74, -36, k)), -30, .5f, 0, 0);
                    p.lamina = D(la, voltas > 0 ? -6 : Mathf.Lerp(8, -12, k));
                    BracoE(ref p, -36, -28, .38f, -30, 4);
                    break;
                }
                case AttackMotion.Uppercut:
                {
                    Base(ref p, Mathf.Lerp(-.02f, .14f, k), Mathf.Lerp(.3f, -.04f, k), .34f);
                    p.quadrilRot = new Vector3(Mathf.Lerp(22, -6, k), Mathf.Lerp(-20, 20, k), 0);
                    p.peitoRot = new Vector3(Mathf.Lerp(16, -14, k), Mathf.Lerp(-24, 24, k), 0);
                    p.cabecaRot = new Vector3(Mathf.Lerp(-28, 14, k), 0, 0);
                    BracoD(ref p, Mathf.Lerp(20, 12, k), Mathf.Lerp(-70, 30, k), Mathf.Lerp(.4f, .54f, k), 0, 0);
                    p.lamina = D(0, Mathf.Lerp(-34, 78, k));
                    BracoE(ref p, -40, -30, .4f, -40, 0);
                    break;
                }
                case AttackMotion.Overhead:
                {
                    // lança erguida acima do ombro e cravada para baixo
                    Base(ref p, Mathf.Lerp(-.06f, .2f, k), Mathf.Lerp(-.02f, .26f, k), Mathf.Lerp(.1f, .46f, k));
                    p.quadrilRot = new Vector3(Mathf.Lerp(-6, 24, k), Mathf.Lerp(30, -20, k), 0);
                    p.peitoRot = new Vector3(Mathf.Lerp(-14, 26, k), Mathf.Lerp(24, -22, k), 0);
                    p.cabecaRot = new Vector3(Mathf.Lerp(10, -36, k), Mathf.Lerp(-45, 36, k), 0);
                    BracoD(ref p, Mathf.Lerp(40, 4, k), Mathf.Lerp(58, -22, k), Mathf.Lerp(.36f, .56f, k), 0, 0);
                    p.lamina = D(Mathf.Lerp(6, 0, k), Mathf.Lerp(-10, -38, k));
                    p.cotoveloD = new Vector3(1, .3f, -.4f);
                    BracoE(ref p, -30, -34, .36f, -24, 6);
                    break;
                }
                default:
                {
                    // estocada da falange: por cima do escudo, passo longo com a perna esquerda
                    bool alta = (e.profundidade & 1) == 0;
                    Base(ref p, Mathf.Lerp(-.1f, .3f, k), Mathf.Lerp(.1f, .22f, k), Mathf.Lerp(.1f, .56f, k));
                    p.quadrilRot = new Vector3(Mathf.Lerp(2, 16, k), Mathf.Lerp(40, -24, k), 0);
                    p.peitoRot = new Vector3(Mathf.Lerp(0, 10, k), Mathf.Lerp(30, -30, k), 0);
                    p.cabecaRot = new Vector3(-8 * k, Mathf.Lerp(-64, 50, k), 0);
                    if (alta) BracoD(ref p, Mathf.Lerp(64, 4, k), Mathf.Lerp(26, 2, k), Mathf.Lerp(.26f, .56f, k), 0, 0);
                    else BracoD(ref p, Mathf.Lerp(66, 8, k), Mathf.Lerp(-48, -14, k), Mathf.Lerp(.26f, .56f, k), 0, 0);
                    p.lamina = D(Mathf.Lerp(4, 0, k), alta ? Mathf.Lerp(-2, -10, k) : Mathf.Lerp(6, 2, k));
                    if (alta) p.cotoveloD = new Vector3(1, .25f, -.5f);
                    BracoE(ref p, -18, -26, .34f, -12, 4);
                    break;
                }
            }
            return p;
        }

        PoseAlvo GolpeEscudo(ComboSO m, in Estado e, float k)
        {
            var p = new PoseAlvo();
            // a espada fica recolhida atrás; quem bate é o braço do escudo
            switch (m.motion)
            {
                case AttackMotion.Thrust:
                {
                    Base(ref p, Mathf.Lerp(-.1f, .3f, k), Mathf.Lerp(.12f, .22f, k), Mathf.Lerp(.1f, .52f, k));
                    p.quadrilRot = new Vector3(Mathf.Lerp(2, 18, k), Mathf.Lerp(-30, 38, k), 0);
                    p.peitoRot = new Vector3(Mathf.Lerp(0, 12, k), Mathf.Lerp(-26, 30, k), 0);
                    p.cabecaRot = new Vector3(-8 * k, Mathf.Lerp(50, -60, k), 0);
                    BracoE(ref p, Mathf.Lerp(-50, -4, k), Mathf.Lerp(-30, -6, k), Mathf.Lerp(.24f, .54f, k), 0, 0);
                    p.escudo = D(0, 4);
                    BracoD(ref p, 70, -50, .46f, 150, -10);
                    break;
                }
                case AttackMotion.Overhead:
                {
                    // borda do escudo descendo como um machado
                    Base(ref p, Mathf.Lerp(-.06f, .16f, k), Mathf.Lerp(-.03f, .3f, k), Mathf.Lerp(.05f, .42f, k));
                    p.quadrilRot = new Vector3(Mathf.Lerp(-8, 26, k), Mathf.Lerp(-16, 12, k), 0);
                    p.peitoRot = new Vector3(Mathf.Lerp(-18, 30, k), Mathf.Lerp(-14, 12, k), 0);
                    p.cabecaRot = new Vector3(Mathf.Lerp(14, -42, k), 0, 0);
                    BracoE(ref p, Mathf.Lerp(-16, 2, k), Mathf.Lerp(74, -36, k), Mathf.Lerp(.44f, .54f, k), 0, 0);
                    p.escudo = D(0, Mathf.Lerp(60, -50, k));
                    p.cotoveloE = new Vector3(-1, .2f, -.2f);
                    BracoD(ref p, 60, Mathf.Lerp(30, -50, k), .46f, 140, Mathf.Lerp(40, -10, k));
                    break;
                }
                case AttackMotion.Uppercut:
                {
                    Base(ref p, Mathf.Lerp(-.04f, .14f, k), Mathf.Lerp(.32f, -.05f, k), .3f);
                    p.quadrilRot = new Vector3(Mathf.Lerp(24, -8, k), Mathf.Lerp(26, -22, k), 0);
                    p.peitoRot = new Vector3(Mathf.Lerp(18, -16, k), Mathf.Lerp(24, -22, k), 0);
                    p.cabecaRot = new Vector3(Mathf.Lerp(-30, 16, k), 0, 0);
                    BracoE(ref p, Mathf.Lerp(-10, -14, k), Mathf.Lerp(-64, 56, k), Mathf.Lerp(.42f, .54f, k), 0, 0);
                    p.escudo = D(0, Mathf.Lerp(-20, 60, k));
                    BracoD(ref p, 70, -50, .46f, 150, -10);
                    p.peInclD = 30 * k; p.peInclE = 14 * k;
                    break;
                }
                case AttackMotion.Spin:
                {
                    float voltas = Mathf.Max(1, m.hitCount);
                    p.giro = Mathf.Repeat(360 * voltas * Mathf.Clamp01(k), 360);
                    Base(ref p, .04f, .2f, .3f);
                    p.quadrilRot = new Vector3(10, 10, 0); p.peitoRot = new Vector3(8, 24, 0); p.cabecaRot = new Vector3(-8, 30, 0);
                    BracoE(ref p, -86, -12, .54f, -90, 0);
                    p.cotoveloE = DirTronco(p, new Vector3(-.3f, -.4f, -1));
                    BracoD(ref p, 74, -30, .5f, 100, -8);
                    p.peD = new Vector3(.12f, Tornozelo, .06f); p.peE = new Vector3(-.34f, Tornozelo + .05f, -.12f); p.peInclE = 20;
                    break;
                }
                default:
                {
                    // gancho com a borda: ímpar da esquerda para a direita, par volta com as costas do escudo
                    bool reves = (e.profundidade & 1) == 0;
                    float s = reves ? -1 : 1;
                    Base(ref p, Mathf.Lerp(-.05f, .16f, k), Mathf.Lerp(.1f, .2f, k), Mathf.Lerp(.1f, .34f, k));
                    float tronco = Mathf.Lerp(-50, 52, k) * s;
                    p.quadrilRot = new Vector3(8, tronco * .45f, 0); p.peitoRot = new Vector3(8, tronco * .55f, 0); p.cabecaRot = new Vector3(-6, -tronco * .8f, 0);
                    BracoE(ref p, reves ? Mathf.Lerp(50, -78, k) : Mathf.Lerp(-84, 46, k), -14, .52f * Mathf.Lerp(.8f, 1, Mathf.Sin(k * Mathf.PI)), 0, 0);
                    p.escudo = D(reves ? Mathf.Lerp(40, -70, k) : Mathf.Lerp(-70, 50, k), 20);
                    BracoD(ref p, 72, -48, .46f, 150, -10);
                    break;
                }
            }
            return p;
        }

        // ------------------------------------------------------------------ montagem
        PoseAlvo Montar(in Estado e, float dt, out float rapidez)
        {
            rapidez = 16;
            PoseAlvo p;
            if (e.estado == PlayerState.Dead) { rapidez = 30; return Morto(e); }
            if (e.estado == PlayerState.Staggered) { rapidez = 34; return Atordoado(e); }
            if (e.estado == PlayerState.Dodging) { rapidez = 30; p = Esquiva(e, .32f); }
            else if (e.estado == PlayerState.Rushing) { rapidez = 26; p = Investida(e); }
            else if (e.golpe)
            {
                rapidez = 46;
                PoseAlvo guarda = e.noChao ? Guarda(e) : NoAr(e);
                p = Golpe(e, guarda);
            }
            else if (!e.noChao) { rapidez = 18; p = NoAr(e); }
            else if (e.velocidade > .35f)
            {
                // o ciclo da corrida já é suave; suavizar demais encolhe a passada
                rapidez = 45;
                PoseAlvo corre = Correr(e, dt);
                p = PoseAlvo.Lerp(Guarda(e), corre, Mathf.InverseLerp(.35f, 1.6f, e.velocidade));
            }
            else p = Guarda(e);
            if (e.arremesso > 0 && !e.golpe) { rapidez = 40; p = Arremesso(e, p); }
            if (e.pouso > 0 && !e.golpe && e.estado == PlayerState.Locomotion)
            {
                // amortece a queda: quadril afunda e o tronco fecha por um instante
                float a = Mathf.Sin(Mathf.Sqrt(1 - e.pouso) * Mathf.PI) * e.pouso;
                p.quadril.y -= .2f * a; p.quadrilRot.x += 14 * a; p.peitoRot.x += 8 * a;
            }
            if (e.cambalhota > 0) { p.cambalhota = (1 - e.cambalhota) * 360; p.peE.y += .28f; p.peD.y += .2f; p.quadril.y += .05f; }
            return p;
        }
    }
}
