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
        // Pátroclo: modelo do herói com animação procedural sobre o esqueleto dele
        PatrocloAnimador animador;
        PatrocloDesign design;
        Transform espada, lanca, maoGancho;
        RastroDeArma rastro;
        float arremesso, cambalhota, combateAte, chaoAte, sincroniza, pouso, curva, rumoAnterior;
        bool estavaNoChao = true;
        PatrocloAnimacoesSO biblioteca;
        PatrocloTocador tocador;
        PanoDinamico capaViva, raboVivo;
        Transform modeloPatroclo, peito;
        float fasePassos, relogioAnim, ombro, giroModelo, estocadaPeso, estocadaSolta, grito;
        Color corDoRastro = Color.white;
        Transform cabeca, pescoco;
        bool estocadaAlta;
        float porte, porteCorrida, porteRolo;   // arma em posição de porte: peso, quanto de disparada, quanto de rolamento
        public bool UsaClipes => tocador;
        public bool UsaPatroclo => animador;
        public PatrocloAnimador Animador => animador;
        /// <summary>Ponta da arma em uso (o raio de Zeus desce nela).</summary>
        public Vector3 PontaDaArma
        {
            get
            {
                if (lanca && lanca.gameObject.activeInHierarchy) return lanca.TransformPoint(Vector3.forward * 1.35f);
                if (espada && espada.gameObject.activeInHierarchy) return espada.TransformPoint(Vector3.forward * 1.3f);
                if (rightWeapon) return rightWeapon.TransformPoint(Vector3.forward * 1.2f);
                return transform.position + Vector3.up * 2.3f;
            }
        }

        public void Initialize(PlayerCombat combat)
        {
            player = combat; previousPosition = transform.position;
            audioFeedback = GetComponent<CombatAudio>() ?? gameObject.AddComponent<CombatAudio>();
            foreach (string name in new[] { "Corpo", "Crista_do_Elmo" })
            {
                var old = transform.Find(name); if (old) old.gameObject.SetActive(false);
            }
            var prefab = HeroiSO.Prefab;
            if (prefab && prefab.GetComponent<PatrocloDesign>()) { IniciarPatroclo(prefab); return; }
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
            gameObject.AddComponent<DivinePresentation>().Initialize(player, this, null);
        }
        void IniciarPatroclo(GameObject prefab)
        {
            var go = Instantiate(prefab, transform); go.name = "Patroclo";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            design = go.GetComponent<PatrocloDesign>(); design.escudo = PosicaoEscudo.Braco;
            animador = go.AddComponent<PatrocloAnimador>(); animador.Preparar(design);
            var r = animador.Rig;
            modeloPatroclo = go.transform; peito = r.Osso("spine01"); pescoco = r.Osso("neck01"); cabeca = r.Osso("head");
            // clipes humanoides (corpo inteiro) quando a biblioteca existe; senão fica a animação por fórmula
            biblioteca = PatrocloAnimacoesSO.Carregar();
            if (biblioteca) { tocador = go.AddComponent<PatrocloTocador>(); tocador.Preparar(biblioteca.avatar); }
            var esqueleto = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var osso in design.refs.ossos) if (osso) esqueleto[osso.name] = osso;
            if (design.refs.capa) capaViva = PanoDinamico.Criar(design.refs.capa.GetComponent<SkinnedMeshRenderer>(), esqueleto, PanoDinamico.ParaCapa());
            if (design.refs.raboCostas) raboVivo = PanoDinamico.Criar(design.refs.raboCostas.GetComponent<SkinnedMeshRenderer>(), esqueleto, PanoDinamico.ParaRabo());
            PatrocloArmas.Madeira = design.refs.madeira; PatrocloArmas.Bronze = design.refs.lamina;
            espada = r.espada; maoGancho = r.pulsoE;
            lanca = PatrocloArmas.Lanca("Lanca", 2.3f, .95f, .017f, .36f).transform;
            lanca.SetParent(r.pulsoD, false); lanca.localPosition = r.espadaPos; lanca.localRotation = r.espadaLocal;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            // crista e rabos de crina são tufos soltos: o nível de detalhe automático apaga tufos inteiros (duas partes da
            // crista ficam sem nenhum triângulo do 4º nível em diante) e ela some e reaparece conforme a distância da
            // câmera. São malhas pequenas (poucos milhares de triângulos): ficam sempre na versão cheia.
            foreach (var peca in new[] { design.refs.crista, design.refs.raboCostas, design.refs.raboEscudo })
                if (peca && peca.TryGetComponent<SkinnedMeshRenderer>(out var pelos)) pelos.forceMeshLod = 0;
            rastro = gameObject.AddComponent<RastroDeArma>();
            var fill = Joint(transform, "Luz_personagem", new Vector3(-.7f, 2.1f, 1.1f)).gameObject.AddComponent<Light>();
            fill.type = LightType.Point; fill.color = new Color(1, .86f, .74f);
            fill.intensity = 1.4f; fill.range = 3.2f; fill.shadows = LightShadows.None;
            // a câmera fica atrás do herói: uma segunda luz suave nas costas mantém capa, escudo e elmo legíveis à noite
            var costas = Joint(transform, "Luz_costas", new Vector3(.8f, 2.3f, -1.7f)).gameObject.AddComponent<Light>();
            costas.type = LightType.Point; costas.color = new Color(1, .8f, .66f);
            costas.intensity = 1.6f; costas.range = 3.6f; costas.shadows = LightShadows.None;
            player.Events.OnStyleRankChanged += StyleChanged;
            Equip(player.Weapon);
            CombatVisuals.Prewarm(transform.position + Vector3.up * 1.2f); rastro.Aquecer(transform.position + Vector3.up * 1.2f);
            gameObject.AddComponent<DivinePresentation>().Initialize(player, this, go.transform);
        }
        /// <summary>Escolhe os clipes do quadro a partir do estado do combate e avalia a pose.</summary>
        void Animar(in PatrocloAnimador.Estado e, float dt)
        {
            relogioAnim += dt;
            float transicao = .12f, alvoGiro = 0;
            PatrocloAnimacoesSO.Trecho T(string id) => biblioteca.Movimento(id);
            void Trecho(string id, float k) { var t = T(id); if (t != null) tocador.Pedir(id, t.clip, Mathf.Lerp(t.inicio, t.fim, Mathf.Clamp01(k))); }
            void Laco(string id, float tempo, float peso = 1) { var t = T(id); if (t != null && peso > 0) tocador.Pedir(id, t.clip, tempo, peso); }
            var golpe = e.golpe ? biblioteca.Golpe(e.golpe.id) : null;
            if (e.golpe && golpe == null) golpe = biblioteca.Golpe(e.arma == WeaponKind.Spear ? "Lanca_Estocada1" : e.arma == WeaponKind.Shield ? "Escudo_Corte1" : "Corte_1");
            float querOmbro = 0, querGrito = 0;
            float querPorte = 0, querCorrida = 0, querRolo = 0;
            if (e.estado == PlayerState.Dead) { Trecho("morte", e.tempoEstado / 1.6f); transicao = .1f; }
            else if (e.estado == PlayerState.Staggered) { Trecho("dano", e.tempoEstado / .3f); transicao = .05f; }
            else if (e.estado == PlayerState.Dodging && player.Favor.DivineDash && T("bencao_hermes") != null)
            {
                // passo de Hermes: em vez de rolar, o herói dispara num bote baixo com a lâmina à frente
                Trecho("bencao_hermes", e.tempoEstado / Mathf.Max(.1f, player.config.dodgeDuration)); transicao = .04f;
            }
            else if (e.estado == PlayerState.Dodging)
            {
                // rolamento na direção da esquiva: o corpo vira para onde rola
                Trecho("esquiva", e.tempoEstado / Mathf.Max(.1f, player.config.dodgeDuration)); transicao = .05f;
                if (e.esquivaLocal.sqrMagnitude > .01f) alvoGiro = Mathf.Atan2(e.esquivaLocal.x, e.esquivaLocal.z) * Mathf.Rad2Deg;
                querPorte = querRolo = 1;
            }
            else if (e.estado == PlayerState.Casting && Gesto(out var gesto, out float tempoGesto))
            {
                // gesto da bênção: o auge da pose cai junto com o poder
                tocador.Pedir(gesto.id, gesto.clip, tempoGesto); transicao = .08f;
                querGrito = player.Favor.CastGod == God.Ares ? Mathf.Clamp01(e.tempoEstado / .12f) * (1 - Mathf.InverseLerp(.38f, .56f, e.tempoEstado)) : 0;
            }
            else if (e.estado == PlayerState.Rushing) { Laco("investida", relogioAnim * 1.25f); transicao = .08f; querPorte = querCorrida = 1; }
            else if (golpe != null)
            {
                tocador.Pedir(e.golpe.id, golpe.clip, golpe.Tempo(e.golpe, e.tempoGolpe)); transicao = .07f;
                // mira (o bote do clipe sai na direção em que o herói olha) e limite de giro do corpo ao longo do golpe
                alvoGiro = golpe.GiroModelo(golpe.Tempo(e.golpe, e.tempoGolpe));
            }
            else if (!e.noChao)
            {
                if (e.cambalhota > 0) { Trecho("pulo_duplo", 1 - e.cambalhota); transicao = .06f; querRolo = 1; }
                else if (e.velY > 2f) Trecho("pulo", 1 - Mathf.Clamp01(e.velY / 9f));
                else Laco("no_ar", relogioAnim);
                querPorte = 1;
            }
            else
            {
                float v = e.velocidade;
                float lanca = e.arremesso > 0 ? Mathf.Clamp01(e.arremesso * 3) : 0;
                var andar = T("andar"); var correr = T("correr"); var disparar = T("disparar");
                float pParado = 1 - Mathf.InverseLerp(.3f, 1.6f, v);
                float pAndar = Mathf.Min(Mathf.InverseLerp(.3f, 1.6f, v), 1 - Mathf.InverseLerp(1.8f, 4.6f, v));
                float pCorrer = Mathf.Min(Mathf.InverseLerp(1.8f, 4.6f, v), 1 - Mathf.InverseLerp(5.8f, 8f, v));
                float pDisparar = Mathf.InverseLerp(5.8f, 8f, v);
                // passadas sincronizadas: uma fase só, cada clipe no ponto equivalente do seu ciclo
                float ciclo = pAndar * (andar?.clip.length ?? 1) + pCorrer * (correr?.clip.length ?? 1) + pDisparar * (disparar?.clip.length ?? 1);
                float cadencia = pAndar * 1f + pCorrer * 1.05f + pDisparar * 1.1f;
                if (ciclo > .01f) fasePassos = Mathf.Repeat(fasePassos + dt * cadencia / ciclo * (pAndar + pCorrer + pDisparar), 1);
                float resto = 1 - lanca;
                Laco(e.emCombate ? "guarda" : "parado", relogioAnim, pParado * resto);
                if (andar != null) Laco("andar", fasePassos * andar.clip.length, pAndar * resto);
                if (correr != null) Laco("correr", fasePassos * correr.clip.length, pCorrer * resto);
                if (disparar != null) Laco("disparar", fasePassos * disparar.clip.length, pDisparar * resto);
                if (lanca > 0) { var t = T("arremesso"); if (t != null) tocador.Pedir("arremesso", t.clip, Mathf.Lerp(t.inicio, t.fim, 1 - e.arremesso), lanca); transicao = .06f; }
                // assinatura dele: parado e fora de combate, a espada grande descansa no ombro
                if (!e.emCombate && e.arma != WeaponKind.Spear && lanca <= 0) querOmbro = 1 - Mathf.InverseLerp(2.5f, 4.5f, v);
                // andando e correndo a arma vai em posição de porte (os clipes de corrida são de mãos vazias)
                querPorte = (1 - pParado) * resto; querCorrida = pDisparar;
            }
            tocador.Avaliar(dt, transicao);
            giroModelo = Mathf.MoveTowardsAngle(giroModelo, alvoGiro, 1400 * dt);
            modeloPatroclo.localRotation = Quaternion.Euler(0, giroModelo, 0);
            porte = Mathf.MoveTowards(porte, querPorte, dt / .14f); porteRolo = Mathf.MoveTowards(porteRolo, querRolo, dt / .1f);
            porteCorrida = Mathf.MoveTowards(porteCorrida, querCorrida, dt / .2f);
            if (porte > 0) Porte(e.arma == WeaponKind.Spear, e.noChao);
            if (e.arma != WeaponKind.Spear) estocadaPeso = 0;
            else Estocada(e.golpe, e.tempoGolpe, golpe != null && golpe.estocada > 0, golpe != null && golpe.estocada == 2, dt);
            // grito de Ares: peito aberto e cabeça para o céu por cima do clipe
            grito = Mathf.MoveTowards(grito, querGrito, dt / .1f);
            if (grito > 0)
            {
                float g = Mathf.SmoothStep(0, 1, grito);
                if (peito) peito.localRotation *= Quaternion.Euler(-9 * g, 0, 0);
                if (pescoco) pescoco.localRotation *= Quaternion.Euler(-12 * g, 0, 0);
                if (cabeca) cabeca.localRotation *= Quaternion.Euler(-17 * g, 0, 0);
            }
            ombro = Mathf.MoveTowards(ombro, querOmbro, dt / .3f);
            if (ombro > 0 && peito)
                animador.Rig.BracoDireito(peito.TransformPoint(new Vector3(.27f, -.02f, .255f)), peito.TransformDirection(new Vector3(1, -.55f, -.15f)),
                    peito.TransformDirection(new Vector3(.1f, .7f, -.7f)), Mathf.SmoothStep(0, 1, ombro));
            // rede de segurança para qualquer pose (rolamento, quedas, transições): a arma não entra no elmo nem no tronco
            if (e.arma == WeaponKind.Spear) animador.Rig.AfastarArma(lanca, -1.15f, 1.35f, .02f);
            else animador.Rig.AfastarArma(espada, -.17f, 1.17f, .04f);
        }
        /// <summary>
        /// Porte da arma em movimento. Os clipes de andar, correr, pular e rolar são de um boneco de mãos vazias: o punho
        /// sobe junto do peito e a lâmina de 1,2 m atravessava o elmo. Aqui o braço direito leva a arma como quem corre
        /// armado: a espada vai em riste, à frente da cintura, com a ponta para a frente e para cima; a lança vai deitada,
        /// com a ponta para a frente. No rolamento a espada fica ao longo do eixo do giro e a lança ao longo do corpo.
        /// </summary>
        void Porte(bool comLanca, bool noChao)
        {
            var ombroD = animador.Rig.Osso("upperarm01.R"); if (!ombroD || !peito) return;
            // referência em pé: só o giro do tronco conta (meio caminho entre o corpo e o peito), não a inclinação; senão,
            // quando ele se debruça para disparar, a ponta da arma mergulha para o chão. No rolamento vale o peito inteiro
            // (o corpo dá a volta completa).
            Vector3 frentePeito = Vector3.ProjectOnPlane(peito.forward, Vector3.up), frenteCorpo = modeloPatroclo.forward;
            if (frentePeito.sqrMagnitude > .04f) frenteCorpo = Vector3.Slerp(frenteCorpo, frentePeito.normalized, .6f);
            Quaternion quadro = Quaternion.Slerp(Quaternion.LookRotation(frenteCorpo, Vector3.up), peito.rotation, porteRolo);
            Vector3 mao, lamina, polo;
            if (comLanca)
            {
                mao = Vector3.Lerp(new Vector3(.13f, -.44f, .06f), new Vector3(.14f, -.42f, -.02f), porteCorrida);
                lamina = Vector3.Lerp(new Vector3(.07f, .14f, 1), new Vector3(.07f, .06f, 1), porteCorrida);
                polo = new Vector3(.6f, -.3f, -.8f);
                mao = Vector3.Lerp(mao, new Vector3(.27f, -.38f, .08f), porteRolo);
                lamina = Vector3.Slerp(lamina.normalized, new Vector3(.1f, .95f, .2f).normalized, porteRolo);
            }
            else
            {
                // espada em riste: punho à frente da cintura, ponta para a frente e para cima, aberta para fora do elmo
                // (levar a lâmina para trás, arrastada, foi rejeitado: "muito feio")
                mao = Vector3.Lerp(new Vector3(.1f, -.33f, .27f), new Vector3(.12f, -.36f, .24f), porteCorrida);
                lamina = Vector3.Lerp(new Vector3(.22f, .62f, .75f), new Vector3(.26f, .42f, .88f), porteCorrida);
                polo = new Vector3(.6f, -.5f, -.6f);
                mao = Vector3.Lerp(mao, new Vector3(.2f, -.36f, .1f), porteRolo);
                lamina = Vector3.Slerp(lamina.normalized, new Vector3(1, -.15f, -.25f).normalized, porteRolo);
            }
            // balanço da passada: o braço armado ainda marca o ritmo, só que curto
            float passada = noChao ? 1 - porteRolo : 0, fase = fasePassos * 2 * Mathf.PI;
            mao += new Vector3(0, .012f * Mathf.Sin(fase * 2), .035f * Mathf.Sin(fase)) * passada;
            lamina += new Vector3(0, .05f * Mathf.Sin(fase + 1), 0) * passada;
            animador.Rig.BracoDireito(ombroD.position + quadro * mao, quadro * polo, quadro * lamina, Mathf.SmoothStep(0, 1, porte));
        }
        /// <summary>Trecho de clipe e instante do gesto da bênção em andamento.</summary>
        bool Gesto(out PatrocloAnimacoesSO.Trecho gesto, out float tempo)
        {
            God deus = player.Favor.CastGod; tempo = 0;
            gesto = biblioteca.Movimento(DivineLore.Clipe(deus));
            if (gesto == null) return false;
            float t = player.StateTime, auge = Mathf.Max(.05f, DivineLore.Auge(deus)), fim = Mathf.Max(auge + .05f, DivineLore.Trava(deus));
            tempo = t <= auge ? Mathf.Lerp(gesto.inicio, gesto.impacto, t / auge) : Mathf.Lerp(gesto.impacto, gesto.fim, Mathf.Clamp01((t - auge) / (fim - auge)));
            return true;
        }
        /// <summary>
        /// Estocada de lança por cima do clipe: o corpo dá o passo e o giro; o braço recolhe e solta a haste em linha
        /// reta para a frente (baixa, saindo do quadril, ou alta, por cima do escudo, como um hoplita). O braço continua
        /// guiado até o golpe acabar e só então devolve a lança à guarda, para a haste não rodar no ar na recuperação.
        /// </summary>
        void Estocada(ComboSO m, float t, bool ativa, bool alta, float dt)
        {
            if (ativa)
            {
                float hs = Mathf.Max(.02f, m.hitStart), he = Mathf.Max(hs + .02f, m.hitEnd);
                float solta = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hs * .45f, hs, t));
                float volta = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(he + .04f, Mathf.Min(m.duration, he + .3f), t));
                estocadaAlta = alta; estocadaSolta = solta * (1 - volta);
                estocadaPeso = Mathf.MoveTowards(estocadaPeso, 1, dt / Mathf.Max(.03f, hs * .5f));
            }
            else
            {
                estocadaPeso = Mathf.MoveTowards(estocadaPeso, 0, dt / .22f);
                estocadaSolta = Mathf.MoveTowards(estocadaSolta, 0, dt / .15f);
            }
            if (estocadaPeso <= 0) return;
            var ombroD = animador.Rig.Osso("upperarm01.R"); if (!ombroD) return;
            Vector3 s = ombroD.position, f = transform.forward, r = transform.right, u = Vector3.up;
            // a mão fica sempre do lado direito do corpo, longe do rosto: baixa sai do quadril; a alta (só no ar) sai do ombro
            Vector3 recolhida = estocadaAlta ? s + r * .22f + u * .1f - f * .15f : s + r * .16f - u * .4f - f * .2f;
            Vector3 estendida = estocadaAlta ? s + r * .14f - u * .04f + f * .55f : s + r * .12f - u * .22f + f * .54f;
            Vector3 mira = estocadaAlta ? (f - u * .1f).normalized : (f + u * .03f).normalized;
            Vector3 polo = estocadaAlta ? r * .8f + u * .5f - f * .3f : r * .6f - u * .5f - f * .6f;
            animador.Rig.BracoDireito(Vector3.Lerp(recolhida, estendida, estocadaSolta), polo, mira, Mathf.SmoothStep(0, 1, estocadaPeso));
        }
        void EquiparPatroclo(WeaponSO profile)
        {
            bool comLanca = profile.kind == WeaponKind.Spear;
            if (espada) espada.gameObject.SetActive(!comLanca);
            if (lanca) lanca.gameObject.SetActive(comLanca);
            rastro.Limpar(); corDoRastro = profile.color * .5f; rastro.cor = corDoRastro;
            if (profile.kind == WeaponKind.Shield && animador.Rig.escudoBraco)
            { rastro.arma = animador.Rig.escudoBraco; rastro.eixo = Vector3.up; rastro.inicio = -.46f; rastro.fim = .46f; }
            else if (comLanca) { rastro.arma = lanca; rastro.eixo = Vector3.forward; rastro.inicio = .55f; rastro.fim = 1.35f; }
            else { rastro.arma = espada; rastro.eixo = Vector3.forward; rastro.inicio = .22f; rastro.fim = 1.3f; }
        }
        void AtualizarPatroclo()
        {
            float dt = Time.deltaTime;
            Vector3 plano = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up);
            float speed = dt > 0 ? plano.magnitude / dt : 0;
            previousPosition = transform.position;
            if (player.IsGrounded) chaoAte = Time.time + .1f;
            var move = player.CurrentAttack;
            if (move || player.State == PlayerState.Casting) combateAte = Time.time + 3.5f;
            else if (Time.time > combateAte - 3)
                foreach (var enemy in EnemyCombat.Combatants)
                    if (enemy && (enemy.transform.position - transform.position).sqrMagnitude < 100) { combateAte = Time.time + 3.5f; break; }
            bool noChao = Time.time < chaoAte;
            if (noChao && !estavaNoChao) pouso = 1;
            estavaNoChao = noChao; pouso = Mathf.Max(0, pouso - dt / .22f);
            float rumo = transform.eulerAngles.y;
            if (dt > 0) curva = Mathf.Lerp(curva, Mathf.DeltaAngle(rumoAnterior, rumo) / dt, 1 - Mathf.Exp(-12 * dt));
            rumoAnterior = rumo;
            arremesso = Mathf.Max(0, arremesso - dt / .3f); cambalhota = Mathf.Max(0, cambalhota - dt / .36f);
            var e = new PatrocloAnimador.Estado
            {
                velocidade = Mathf.Min(speed, 12), direcaoLocal = speed > .05f ? transform.InverseTransformDirection(plano.normalized) : Vector3.forward,
                noChao = Time.time < chaoAte, velY = player.VerticalSpeed, estado = player.State, tempoEstado = player.StateTime,
                golpe = move, tempoGolpe = player.AttackTime, profundidade = player.ComboDepth,
                arma = player.Weapon ? player.Weapon.kind : WeaponKind.Sword, arremesso = arremesso, cambalhota = cambalhota,
                esquivaLocal = transform.InverseTransformDirection(player.DodgeDirection), emCombate = Time.time < combateAte,
                pouso = pouso, curva = curva
            };
            if (tocador) Animar(e, dt); else animador.Avaliar(e, dt);
            float chao = e.noChao ? transform.position.y : float.NegativeInfinity;
            if (capaViva) { capaViva.chao = chao; capaViva.Passo(dt); }
            if (raboVivo) raboVivo.Passo(dt);
            bool comLanca = e.arma == WeaponKind.Spear;
            if (espada) espada.gameObject.SetActive(!comLanca && !(arremesso > .3f && !move));
            rastro.emitindo = move && player.AttackTime >= move.hitStart * .4f && player.AttackTime <= move.hitEnd + .06f;
            // em fúria (Ares) a lâmina risca fogo
            rastro.cor = player.Favor.FuryRemaining > 0 ? new Color(1.9f, .42f, .1f) : corDoRastro;
            rastro.Passo(dt);
            hookTime -= dt;
            if (hook) { hook.enabled = hookTime > 0 && hookTarget; if (hook.enabled) { hook.SetPosition(0, maoGancho.position); hook.SetPosition(1, hookTarget.position + Vector3.up); } }
            // o painel de design já sabe mostrar favor, nota de estilo e bênção no corpo do herói
            sincroniza -= Time.unscaledDeltaTime;
            if (sincroniza > 0) return;
            sincroniza = .12f;
            float favor = Mathf.Round(player.Favor.Amount / 5f) / 20f;
            var nota = (NotaEstilo)Mathf.Clamp(player.Style.Rank, 0, 6);
            var bencao = player.Favor.Level(player.Favor.Selected) > 0 ? (Bencao)((int)player.Favor.Selected + 1) : Bencao.Nenhuma;
            if (!Mathf.Approximately(design.favorDivino, favor) || design.notaDeEstilo != nota || design.bencao != bencao)
            {
                design.favorDivino = favor; design.notaDeEstilo = nota; design.bencao = bencao; design.Apply();
            }
        }
        static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var joint = new GameObject(name).transform; joint.SetParent(parent, false); joint.localPosition = position; return joint;
        }
        public void Equip(WeaponSO profile)
        {
            if (animador) { if (profile) EquiparPatroclo(profile); return; }
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
        public void Shoot() { shootPose = .18f; arremesso = 1; combateAte = Time.time + 3.5f; audioFeedback?.Shoot(); }
        public void Pull(Transform target)
        {
            hookTarget=target; hookTime=.6f;
            if (!hook)
            {
                hook=new GameObject("Corda_do_arpao").AddComponent<LineRenderer>(); hook.transform.SetParent(transform,false);
                hook.positionCount=2; hook.startWidth=.035f; hook.endWidth=.02f;
                var material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); material.color=new Color(.8f,.55f,.3f);
                hook.sharedMaterial=material; hook.gameObject.AddComponent<OwnedMaterial>().material=material;
            }
        }
        public void Dodge() => audioFeedback?.Dodge();
        public void Jump(bool twice)
        {
            if (!twice) return; flip = .32f; cambalhota = 1;
            CombatVisuals.Burst(transform.position + Vector3.up * .2f, player.Weapon ? player.Weapon.color : Color.cyan, .45f);
        }
        void StyleChanged(int rank)
        {
            if (rank > previousRank) audioFeedback?.Rank(rank); previousRank = rank;
        }
        void LateUpdate()
        {
            if (animador && player) { AtualizarPatroclo(); return; }
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
