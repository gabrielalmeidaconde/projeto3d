using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Alvos de uma pose do Pátroclo, no espaço do personagem (X = direita, Y = cima, Z = frente, origem nos pés).
    /// O rig resolve os ossos a partir disto: tronco por rotação, braços e pernas por IK.
    /// </summary>
    public struct PoseAlvo
    {
        public Vector3 quadril;      // deslocamento do quadril em relação ao repouso
        public Vector3 quadrilRot;   // euler (graus): X inclina para a frente, Y gira, Z tomba
        public Vector3 peitoRot;     // euler somado ao longo da coluna
        public Vector3 cabecaRot;    // euler somado ao longo do pescoço
        public Vector3 maoD, maoE;   // pulsos
        public Vector3 cotoveloD, cotoveloE; // para onde o cotovelo aponta
        public Vector3 lamina;       // direção da arma da mão direita (ponta)
        public Vector3 escudo;       // para onde a face do escudo aponta (zero = solto)
        public Vector3 peD, peE;     // tornozelos
        public Vector3 joelhoD, joelhoE;
        public float peGiroD, peGiroE, peInclD, peInclE;
        public float giro, cambalhota, tombo; // corpo inteiro: giro em Y, rotação em X pelo quadril, queda de costas

        public static PoseAlvo Lerp(in PoseAlvo a, in PoseAlvo b, float t)
        {
            t = Mathf.Clamp01(t);
            PoseAlvo r;
            r.quadril = Vector3.Lerp(a.quadril, b.quadril, t);
            r.quadrilRot = Vector3.Lerp(a.quadrilRot, b.quadrilRot, t);
            r.peitoRot = Vector3.Lerp(a.peitoRot, b.peitoRot, t);
            r.cabecaRot = Vector3.Lerp(a.cabecaRot, b.cabecaRot, t);
            r.maoD = Vector3.Lerp(a.maoD, b.maoD, t); r.maoE = Vector3.Lerp(a.maoE, b.maoE, t);
            r.cotoveloD = Vector3.Lerp(a.cotoveloD, b.cotoveloD, t); r.cotoveloE = Vector3.Lerp(a.cotoveloE, b.cotoveloE, t);
            r.lamina = Dir(a.lamina, b.lamina, t);
            r.escudo = a.escudo == Vector3.zero || b.escudo == Vector3.zero ? Vector3.Lerp(a.escudo, b.escudo, t) : Dir(a.escudo, b.escudo, t);
            r.peD = Vector3.Lerp(a.peD, b.peD, t); r.peE = Vector3.Lerp(a.peE, b.peE, t);
            r.joelhoD = Vector3.Lerp(a.joelhoD, b.joelhoD, t); r.joelhoE = Vector3.Lerp(a.joelhoE, b.joelhoE, t);
            r.peGiroD = Mathf.Lerp(a.peGiroD, b.peGiroD, t); r.peGiroE = Mathf.Lerp(a.peGiroE, b.peGiroE, t);
            r.peInclD = Mathf.Lerp(a.peInclD, b.peInclD, t); r.peInclE = Mathf.Lerp(a.peInclE, b.peInclE, t);
            r.giro = Mathf.Lerp(a.giro, b.giro, t); r.cambalhota = Mathf.Lerp(a.cambalhota, b.cambalhota, t);
            r.tombo = Mathf.Lerp(a.tombo, b.tombo, t);
            return r;
        }
        static Vector3 Dir(Vector3 a, Vector3 b, float t)
        {
            if (a == Vector3.zero) return b; if (b == Vector3.zero) return a;
            return Vector3.Slerp(a.normalized, b.normalized, t);
        }
    }

    /// <summary>Resolve uma <see cref="PoseAlvo"/> nos ossos do Pátroclo (esqueleto do MakeHuman).</summary>
    public class PatrocloRig
    {
        public Transform modelo, raiz, espada, pulsoD, pulsoE, escudoBraco;
        public Quaternion espadaLocal;
        public Vector3 espadaPos;
        Vector3 raizPos;
        readonly Dictionary<string, Transform> osso = new Dictionary<string, Transform>();
        Transform[] corpo;
        Transform[] coluna, pescoco;
        static readonly float[] PesoColuna = { .14f, .2f, .22f, .2f, .24f };
        static readonly float[] PesoPescoco = { .25f, .15f, .15f, .45f };
        Transform bracoD, bracoE, anteD, anteE, ante2D, ante2E, coxaD, coxaE, canelaD, canelaE, peD, peE, claviculaD, claviculaE;
        public bool Valido => raiz && bracoD && coxaD && pulsoD;

        public PatrocloRig(PatrocloDesign design)
        {
            modelo = design.transform;
            var refs = design.refs;
            raiz = refs.raiz; raizPos = refs.posicaoRaiz;
            var lista = new List<Transform>();
            foreach (var b in refs.ossos)
            {
                if (!b) continue;
                osso[b.name] = b;
                if (!b.name.StartsWith("finger") && !b.name.StartsWith("metacarpal")) lista.Add(b);
            }
            corpo = lista.ToArray();
            coluna = new[] { O("spine05"), O("spine04"), O("spine03"), O("spine02"), O("spine01") };
            pescoco = new[] { O("neck01"), O("neck02"), O("neck03"), O("head") };
            bracoD = O("upperarm01.R"); bracoE = O("upperarm01.L"); anteD = O("lowerarm01.R"); anteE = O("lowerarm01.L");
            ante2D = O("lowerarm02.R"); ante2E = O("lowerarm02.L"); pulsoD = O("wrist.R"); pulsoE = O("wrist.L");
            coxaD = O("upperleg01.R"); coxaE = O("upperleg01.L"); canelaD = O("lowerleg01.R"); canelaE = O("lowerleg01.L");
            peD = O("foot.R"); peE = O("foot.L"); claviculaD = O("clavicle.R"); claviculaE = O("clavicle.L");
            if (pulsoD) espada = pulsoD.Find("Espada");
            if (espada) { espadaLocal = espada.localRotation; espadaPos = espada.localPosition; }
            escudoBraco = refs.escudoBraco ? refs.escudoBraco.transform : null;
            PrepararObstaculos(refs);
        }
        Transform O(string nome) => osso.TryGetValue(nome, out var t) ? t : null;

        public void Resolver(in PoseAlvo p)
        {
            if (!Valido) return;
            // corpo inteiro: giro em Y e cambalhota em X em torno do quadril
            Quaternion inteiro = Quaternion.Euler(p.cambalhota - p.tombo, p.giro, 0);
            Vector3 pivo = new Vector3(0, Mathf.Lerp(1f, .12f, Mathf.Clamp01(p.tombo / 90f)), 0);
            modelo.localRotation = inteiro; modelo.localPosition = pivo - inteiro * pivo;

            for (int i = 0; i < corpo.Length; i++) corpo[i].localRotation = Quaternion.identity;
            raiz.localPosition = raizPos + p.quadril;
            raiz.localRotation = Quaternion.Euler(p.quadrilRot);
            for (int i = 0; i < coluna.Length; i++) if (coluna[i]) coluna[i].localRotation = Quaternion.Euler(p.peitoRot * PesoColuna[i]);
            for (int i = 0; i < pescoco.Length; i++) if (pescoco[i]) pescoco[i].localRotation = Quaternion.Euler(p.cabecaRot * PesoPescoco[i]);

            Matrix4x4 m = modelo.localToWorldMatrix;
            Quaternion mr = modelo.rotation;
            // ombros acompanham um pouco os braços altos
            Ombro(claviculaD, bracoD, m.MultiplyPoint3x4(p.maoD), mr, 1);
            Ombro(claviculaE, bracoE, m.MultiplyPoint3x4(p.maoE), mr, -1);
            Dois(bracoD, anteD, pulsoD, m.MultiplyPoint3x4(p.maoD), mr * Seguro(p.cotoveloD, new Vector3(.6f, -.5f, -.6f)));
            Dois(bracoE, anteE, pulsoE, m.MultiplyPoint3x4(p.maoE), mr * Seguro(p.cotoveloE, new Vector3(-.6f, -.5f, -.6f)));

            // mão direita: alinha a arma
            if (espada && p.lamina != Vector3.zero)
            {
                Vector3 ponta = (mr * p.lamina).normalized;
                Vector3 ante = (pulsoD.position - anteD.position).normalized;
                Vector3 cima = Vector3.ProjectOnPlane(ante, ponta);
                Vector3 reserva = Vector3.ProjectOnPlane(mr * new Vector3(-.3f, -1f, .2f), ponta);
                cima = (cima + reserva.normalized * .35f).normalized;
                Quaternion alvo = Quaternion.LookRotation(ponta, cima) * Quaternion.Inverse(espadaLocal);
                pulsoD.rotation = alvo;
                // metade da torção vai para o antebraço, para a pele do pulso não enrolar
                if (ante2D)
                {
                    Vector3 eixo = pulsoD.localPosition.normalized;
                    Quaternion torcao = Torcao(pulsoD.localRotation, eixo);
                    ante2D.localRotation = Quaternion.Slerp(Quaternion.identity, torcao, .5f);
                    pulsoD.rotation = alvo;
                }
            }
            // braço esquerdo: gira o antebraço para a face do escudo apontar para o alvo
            if (escudoBraco && p.escudo != Vector3.zero && anteE)
            {
                Vector3 eixo = (pulsoE.position - anteE.position).normalized;
                Vector3 atual = Vector3.ProjectOnPlane(escudoBraco.forward, eixo);
                Vector3 quer = Vector3.ProjectOnPlane(mr * p.escudo, eixo);
                if (atual.sqrMagnitude > 1e-4f && quer.sqrMagnitude > 1e-4f)
                {
                    float ang = Mathf.Clamp(Vector3.SignedAngle(atual, quer, eixo), -75, 75);
                    anteE.rotation = Quaternion.AngleAxis(ang, eixo) * anteE.rotation;
                }
            }

            Dois(coxaD, canelaD, peD, m.MultiplyPoint3x4(p.peD), mr * Seguro(p.joelhoD, new Vector3(.25f, 0, 1)));
            Dois(coxaE, canelaE, peE, m.MultiplyPoint3x4(p.peE), mr * Seguro(p.joelhoE, new Vector3(-.25f, 0, 1)));
            peD.rotation = mr * Quaternion.Euler(p.peInclD, p.peGiroD, 0);
            peE.rotation = mr * Quaternion.Euler(p.peInclE, p.peGiroE, 0);
        }
        /// <summary>
        /// Por cima de uma pose já animada: leva a mão direita a um ponto (mundo) com a arma apontando para 'lamina',
        /// misturando com a pose atual pelo peso. Usado para apoiar a espada no ombro quando ele está parado.
        /// </summary>
        public void BracoDireito(Vector3 mao, Vector3 polo, Vector3 lamina, float peso)
        {
            if (!Valido || peso <= 0 || !espada) return;
            Quaternion b0 = bracoD.localRotation, a0 = anteD.localRotation, p0 = pulsoD.localRotation, t0 = ante2D ? ante2D.localRotation : Quaternion.identity;
            Dois(bracoD, anteD, pulsoD, mao, polo);
            Vector3 ante = (pulsoD.position - anteD.position).normalized;
            Vector3 cima = (Vector3.ProjectOnPlane(ante, lamina).normalized + Vector3.ProjectOnPlane(new Vector3(-.3f, -1f, .2f), lamina).normalized * .35f).normalized;
            pulsoD.rotation = Quaternion.LookRotation(lamina.normalized, cima) * Quaternion.Inverse(espadaLocal);
            if (peso >= 1) return;
            bracoD.localRotation = Quaternion.Slerp(b0, bracoD.localRotation, peso);
            anteD.localRotation = Quaternion.Slerp(a0, anteD.localRotation, peso);
            if (ante2D) ante2D.localRotation = Quaternion.Slerp(t0, ante2D.localRotation, peso);
            pulsoD.localRotation = Quaternion.Slerp(p0, pulsoD.localRotation, peso);
        }
        public Transform Osso(string nome) => O(nome);

        // ------------------------------------------------------------------ a arma não atravessa a cabeça nem o tronco
        // Cada obstáculo é uma cápsula de raio 1 num espaço local achatado pelos semi-eixos (vira elipsoide quando
        // as duas pontas coincidem). 'punho' é o trecho da arma junto da mão que não conta (a mão pode encostar no corpo).
        struct Obstaculo { public Transform osso, ate, visivel; public Vector3 a, b, eixos; public float punho, folga; }
        readonly List<Obstaculo> obstaculos = new List<Obstaculo>();

        void PrepararObstaculos(PatrocloDesign.Referencias refs)
        {
            var cabeca = O("head");
            if (cabeca)
            {
                var casco = refs.elmo ? refs.elmo.transform.Find("Elmo_Casco") : null;
                if (casco && Caixa(casco.GetComponent<SkinnedMeshRenderer>(), cabeca, out var c))
                {
                    // casco: cápsula em pé, redonda no plano horizontal
                    float r = (c.extents.x + c.extents.z) * .5f, meia = Mathf.Max(0, c.extents.y - r);
                    obstaculos.Add(new Obstaculo { osso = cabeca, visivel = casco, a = c.center - Vector3.up * meia, b = c.center + Vector3.up * meia,
                        eixos = new Vector3(c.extents.x, r, c.extents.z), folga = .025f });
                }
                else obstaculos.Add(new Obstaculo { osso = cabeca, a = new Vector3(0, .09f, .02f), b = new Vector3(0, .09f, .02f), eixos = new Vector3(.1f, .13f, .12f), folga = .02f });
                // crista: leque fino no plano do meio da cabeça; o elipsoide inscrito na caixa acompanha o arco dela
                if (refs.crista && Caixa(refs.crista.GetComponent<SkinnedMeshRenderer>(), cabeca, out var k))
                    obstaculos.Add(new Obstaculo { osso = cabeca, visivel = refs.crista.transform, a = k.center, b = k.center,
                        eixos = new Vector3(Mathf.Max(.03f, k.extents.x), k.extents.y, k.extents.z), folga = .015f });
            }
            // tronco: quadril-cintura e cintura-peito (mais largo do que fundo)
            foreach (var (de, ate, raio) in new[] { ("root", "spine03", .135f), ("spine03", "spine01", .15f) })
                if (O(de) && O(ate))
                    obstaculos.Add(new Obstaculo { osso = O(de), ate = O(ate), eixos = new Vector3(raio * 1.25f, raio, raio), punho = .25f, folga = .01f });
        }
        /// <summary>Caixa dos vértices de uma malha presos a um osso, no espaço desse osso (pose em que a malha foi presa).</summary>
        static bool Caixa(SkinnedMeshRenderer smr, Transform osso, out Bounds caixa)
        {
            caixa = new Bounds();
            if (!smr || !smr.sharedMesh || !smr.sharedMesh.isReadable) return false;
            int i = System.Array.IndexOf(smr.bones, osso); if (i < 0) return false;
            var malha = smr.sharedMesh; var bind = malha.bindposes[i]; var bw = malha.boneWeights; var v = malha.vertices;
            bool tem = false;
            for (int n = 0; n < v.Length; n++)
            {
                var w = bw[n];
                float peso = (w.boneIndex0 == i ? w.weight0 : 0) + (w.boneIndex1 == i ? w.weight1 : 0) + (w.boneIndex2 == i ? w.weight2 : 0) + (w.boneIndex3 == i ? w.weight3 : 0);
                if (peso <= .5f) continue;
                Vector3 p = bind.MultiplyPoint3x4(v[n]);
                if (!tem) { caixa = new Bounds(p, Vector3.zero); tem = true; } else caixa.Encapsulate(p);
            }
            return tem;
        }

        /// <summary>
        /// Por cima da pose pronta: se a arma da mão direita (haste em +Z de 'arma', de z0 a z1) entra no elmo, na crista
        /// ou no tronco, gira o pulso (e, se a própria mão está perto demais, recua a mão) só o bastante para ela passar
        /// por fora. Não faz nada quando a arma já está livre, então não muda os golpes que não tocam no corpo.
        /// </summary>
        public void AfastarArma(Transform arma, float z0, float z1, float grossura)
        {
            if (!Valido || !arma || !arma.gameObject.activeInHierarchy) return;
            for (int volta = 0; volta < 2; volta++)
            {
                bool mexeu = false;
                foreach (var o in obstaculos)
                {
                    if (o.visivel && !o.visivel.gameObject.activeInHierarchy) continue;
                    if (o.punho <= 0) mexeu |= Afastar(arma, z0, z1, grossura, o);
                    else
                    {
                        if (z1 > o.punho) mexeu |= Afastar(arma, Mathf.Max(z0, o.punho), z1, grossura, o);
                        if (z0 < -o.punho) mexeu |= Afastar(arma, z0, Mathf.Min(z1, -o.punho), grossura, o);
                    }
                }
                if (!mexeu) break;
            }
        }
        bool Afastar(Transform arma, float z0, float z1, float grossura, in Obstaculo o)
        {
            Vector3 inv = new Vector3(1 / o.eixos.x, 1 / o.eixos.y, 1 / o.eixos.z);
            Vector3 A = arma.TransformPoint(0, 0, z0), B = arma.TransformPoint(0, 0, z1);
            Vector3 a = Vector3.Scale(o.osso.InverseTransformPoint(A), inv), b = Vector3.Scale(o.osso.InverseTransformPoint(B), inv);
            Vector3 c0 = Vector3.Scale(o.a, inv), c1 = Vector3.Scale(o.ate ? o.osso.InverseTransformPoint(o.ate.position) : o.b, inv);
            float dist = Segmentos(a, b, c0, c1, out float u, out Vector3 naArma, out Vector3 noEixo);
            float media = (o.eixos.x + o.eixos.y + o.eixos.z) / 3f;
            float entra = 1 + (grossura + o.folga) / media - dist;
            if (entra <= 0) return false;
            Vector3 n = naArma - noEixo;
            if (dist < 1e-4f)
            {
                // a arma passa pelo meio: sai para o lado em que a mão está
                n = Vector3.Scale(o.osso.InverseTransformPoint(pulsoD.position), inv) - noEixo;
                n = Vector3.ProjectOnPlane(n, b - a);
                if (n.sqrMagnitude < 1e-6f) n = Vector3.right;
            }
            Vector3 empurra = o.osso.TransformVector(Vector3.Scale(n.normalized * entra, o.eixos));
            Vector3 ponto = Vector3.Lerp(A, B, u), alavanca = ponto - pulsoD.position;
            // longe da mão basta girar o pulso; junto da mão é a mão que tem de sair
            float gira = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, .3f, alavanca.magnitude));
            if (gira > 0) pulsoD.rotation = Quaternion.FromToRotation(alavanca, alavanca + empurra * gira) * pulsoD.rotation;
            if (gira < 1)
            {
                Quaternion giro = pulsoD.rotation;
                Dois(bracoD, anteD, pulsoD, pulsoD.position + empurra * (1 - gira), anteD.position - bracoD.position);
                pulsoD.rotation = giro;
            }
            return true;
        }
        /// <summary>Menor distância entre dois segmentos; 'u' é a posição ao longo do primeiro (0..1).</summary>
        static float Segmentos(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float u, out Vector3 em1, out Vector3 em2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = d1.sqrMagnitude, e = d2.sqrMagnitude, f = Vector3.Dot(d2, r), t;
            if (a <= 1e-9f && e <= 1e-9f) { u = 0; t = 0; }
            else if (a <= 1e-9f) { u = 0; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-9f) { t = 0; u = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), den = a * e - b * b;
                    u = den > 1e-9f ? Mathf.Clamp01((b * f - c * e) / den) : 0;
                    t = (b * u + f) / e;
                    if (t < 0) { t = 0; u = Mathf.Clamp01(-c / a); }
                    else if (t > 1) { t = 1; u = Mathf.Clamp01((b - c) / a); }
                }
            }
            em1 = p1 + d1 * u; em2 = p2 + d2 * t;
            return Vector3.Distance(em1, em2);
        }
        static Vector3 Seguro(Vector3 v, Vector3 padrao) => v.sqrMagnitude < 1e-4f ? padrao : v;

        static void Ombro(Transform clavicula, Transform braco, Vector3 alvo, Quaternion mr, float lado)
        {
            if (!clavicula || !braco) return;
            Vector3 d = Quaternion.Inverse(mr) * (alvo - braco.position);
            float sobe = Mathf.Clamp(d.y / .5f, -.3f, 1f) * 16f;
            float frente = Mathf.Clamp(d.z / .5f, -1f, 1f) * 12f;
            clavicula.localRotation = Quaternion.Euler(0, -frente * lado, sobe * lado);
        }

        /// <summary>IK de dois ossos: põe a ponta no alvo, com o meio (cotovelo/joelho) puxado para o polo.</summary>
        static void Dois(Transform cima, Transform meio, Transform ponta, Vector3 alvo, Vector3 polo)
        {
            if (!cima || !meio || !ponta) return;
            Vector3 a = cima.position, b = meio.position, c = ponta.position;
            float la = (b - a).magnitude, lb = (c - b).magnitude;
            Vector3 at = alvo - a;
            float d = Mathf.Clamp(at.magnitude, Mathf.Abs(la - lb) + .02f, (la + lb) * .995f);
            Vector3 dir = at.sqrMagnitude > 1e-6f ? at.normalized : Vector3.down;
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2 * la * d), -1, 1);
            Vector3 lateral = Vector3.ProjectOnPlane(polo, dir);
            if (lateral.sqrMagnitude < 1e-5f) lateral = Vector3.ProjectOnPlane(Vector3.up, dir);
            lateral.Normalize();
            Vector3 e = a + dir * (la * cosA) + lateral * (la * Mathf.Sqrt(1 - cosA * cosA));
            cima.rotation = Quaternion.FromToRotation(b - a, e - a) * cima.rotation;
            b = meio.position; c = ponta.position;
            meio.rotation = Quaternion.FromToRotation(c - b, a + dir * d - b) * meio.rotation;
        }
        static Quaternion Torcao(Quaternion q, Vector3 eixo)
        {
            Vector3 r = new Vector3(q.x, q.y, q.z);
            Vector3 p = Vector3.Project(r, eixo);
            var t = new Quaternion(p.x, p.y, p.z, q.w);
            float n = Mathf.Sqrt(t.x * t.x + t.y * t.y + t.z * t.z + t.w * t.w);
            return n < 1e-5f ? Quaternion.identity : new Quaternion(t.x / n, t.y / n, t.z / n, t.w / n);
        }
    }
}
