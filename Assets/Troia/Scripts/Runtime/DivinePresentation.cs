using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Liga as regras das bênçãos (<see cref="DivineFavor"/>) ao que se vê e ouve: dispara os efeitos de cada deus,
    /// mantém as auras enquanto os poderes duram e pede os trancos de câmera. Não decide nada de combate.
    /// </summary>
    public class DivinePresentation : MonoBehaviour
    {
        PlayerCombat player;
        DivineFavor favor;
        PlayerPresentation corpo;
        DivineAudio som;
        AuraDivina furia, egide;
        System.Action<Vector3> impactoNaEgide;
        SkinnedMeshRenderer[] peles;
        MeshFilter[] rigidos;
        float fantasmaAte, proximoFantasma;
        int quadroDoSom = -1;

        public void Initialize(PlayerCombat combat, PlayerPresentation presentation, Transform modelo)
        {
            player = combat; favor = combat.Favor; corpo = presentation;
            som = GetComponent<DivineAudio>() ?? gameObject.AddComponent<DivineAudio>();
            if (modelo)
            {
                peles = modelo.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                rigidos = modelo.GetComponentsInChildren<MeshFilter>(false);
            }
            favor.CastStarted += Conjurar; favor.Unleashed += Liberar; favor.DivineHit += Acerto;
            favor.Proc += Passiva; favor.Blocked += Bloqueio; favor.Parried += Aparo;
            player.Events.OnComboHit += Golpe;
            DivineVfx.Aquecer(transform.position + Vector3.up * 1.2f);
        }
        Vector3 Ponta() => corpo ? corpo.PontaDaArma : transform.position + Vector3.up * 2.2f;

        void Conjurar(God deus, int nivel)
        {
            float atraso = DivineLore.Atraso(deus);
            if (deus == God.Zeus) { DivineVfx.ZeusCarga(transform, Ponta, atraso); som.Carga(deus); }
            else if (deus == God.Poseidon) { DivineVfx.PoseidonCarga(transform, atraso); som.Carga(deus); }
        }
        void Liberar(God deus, int nivel, Vector3 centro, float raio)
        {
            som.Poder(deus);
            switch (deus)
            {
                case God.Zeus:
                    DivineVfx.ZeusGolpe(centro, raio, transform.position + transform.forward * 1.5f);
                    player.Kick(.55f, 1f); GameManager.Instance?.HitStop(.07f); break;
                case God.Ares:
                    DivineVfx.AresErupcao(transform); player.Kick(.4f, .7f); break;
                case God.Apollo:
                    DivineVfx.ApoloLuz(transform); player.Kick(.08f, .25f); break;
                case God.Hermes:
                    DivineVfx.HermesVento(transform, transform.forward, player.config.dodgeDuration);
                    fantasmaAte = Time.time + player.config.dodgeDuration; proximoFantasma = 0;
                    player.Kick(.12f, .6f); break;
                case God.Athena:
                    DivineVfx.AtenaSurgir(transform); player.Kick(.2f, .35f); break;
                case God.Poseidon:
                    DivineVfx.PoseidonMare(centro, raio, transform.forward);
                    player.Kick(.5f, .9f); GameManager.Instance?.HitStop(.06f); break;
            }
        }
        void Acerto(God deus, EnemyCombat inimigo, bool passivo)
        {
            if (!inimigo) return;
            Vector3 meio = inimigo.transform.position + Vector3.up * 1.1f;
            float escala = passivo ? .75f : 1;
            switch (deus)
            {
                case God.Zeus: DivineVfx.ZeusAlvo(inimigo.transform, escala); break;
                case God.Hermes: DivineVfx.HermesCorte(meio); break;
                case God.Poseidon: DivineVfx.PoseidonGeiser(inimigo.transform.position, escala); break;
                default: DivineVfx.Toque(deus, meio); break;
            }
            // o poder ativo já tem o som dele; o passivo toca uma versão baixa, uma vez por quadro
            if (passivo && Time.frameCount != quadroDoSom) { quadroDoSom = Time.frameCount; som.Poder(deus, .4f); }
        }
        void Passiva(God deus, Vector3 ponto, float valor)
        {
            switch (deus)
            {
                // o raio do finalizador e o jato do lançador aparecem no acerto de área que vem em seguida (ver Acerto)
                case God.Apollo: DivineVfx.ApoloCentelha(transform); break;
                case God.Hermes: if (!favor.DivineDash) DivineVfx.HermesPassos(transform, valor); break;
            }
        }
        void Bloqueio(DamageInfo info)
        {
            Vector3 paraFora = -info.direction; paraFora.y = 0;
            if (paraFora.sqrMagnitude < .01f && info.source) paraFora = Vector3.ProjectOnPlane(info.source.transform.position - transform.position, Vector3.up);
            if (paraFora.sqrMagnitude < .01f) paraFora = transform.forward;
            impactoNaEgide?.Invoke(paraFora + Vector3.up * .15f);
            DivineVfx.AtenaAparo(transform.position + Vector3.up * 1.15f + paraFora.normalized * 1.55f, paraFora);
            som.Aparo(); player.Kick(.14f, .2f);
        }
        void Aparo(DamageInfo info)
        {
            Vector3 paraFora = -info.direction; paraFora.y = 0;
            if (paraFora.sqrMagnitude < .01f) paraFora = transform.forward;
            DivineVfx.AtenaAparo(transform.position + Vector3.up * 1.15f + paraFora.normalized * .9f, paraFora);
            som.Aparo(); player.Kick(.22f, .5f);
        }
        void Golpe(DamageInfo info, Health alvo)
        {
            // em fúria, cada golpe arranca brasas do inimigo
            if (favor.FuryRemaining > 0 && alvo && !info.divine) DivineVfx.AresGolpe(alvo.transform.position + Vector3.up * 1.1f, info.direction);
        }
        void Update()
        {
            if (!favor) return;
            // auras acompanham a duração real dos poderes (inclusive quando a partida reinicia e eles são zerados)
            bool emFuria = favor.FuryRemaining > 0;
            if (emFuria && !furia) furia = DivineVfx.AresAura(transform);
            else if (!emFuria && furia) { furia.Encerrar(); furia = null; }
            bool comEgide = favor.ShieldRemaining > 0;
            if (comEgide && !egide) egide = DivineVfx.AtenaEgide(transform, out impactoNaEgide);
            else if (!comEgide && egide)
            {
                DivineVfx.AtenaQuebrar(transform.position); egide.Encerrar(); egide = null; impactoNaEgide = null;
            }
            if (Time.time < fantasmaAte && Time.time >= proximoFantasma && player.State == PlayerState.Dodging)
            {
                proximoFantasma = Time.time + .07f;
                DivineVfx.Fantasma(peles, rigidos, DivineLore.Cor(God.Hermes) * .45f, .34f);
            }
        }
        void OnDestroy()
        {
            if (favor)
            {
                favor.CastStarted -= Conjurar; favor.Unleashed -= Liberar; favor.DivineHit -= Acerto;
                favor.Proc -= Passiva; favor.Blocked -= Bloqueio; favor.Parried -= Aparo;
            }
            if (player && player.Events) player.Events.OnComboHit -= Golpe;
        }
    }
}
