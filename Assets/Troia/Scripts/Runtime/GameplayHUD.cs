using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using A = Troia.InterfaceArte;

namespace Troia
{
    /// <summary>
    /// HUD e menus no tema da Ilíada: placas de pedra com filetes de ouro, greca, louros e medalhões (arte em
    /// Resources/Troia/UI, fontes Cinzel e Marcellus). O Canvas é de sobreposição (Screen Space - Overlay): o URP o
    /// desenha depois do pós-processamento, então as cores não pegam a gradação noturna nem o bloom, e nada do mundo
    /// passa na frente. Para capturas fora da tela (camera.Render não inclui sobreposição) use Fotografar.
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public string DisplayedRank => rank ? rank.text : "";
        public bool MoveListVisible => moveList && moveList.activeSelf;
        public string DisplayedBlessing => blessingName ? blessingName.text : "";

        // pedra, bronze, prata, ouro, fogo, sangue e o brilho dos deuses
        static readonly Color[] RankColors = {
            new Color(.72f, .7f, .66f), new Color(.86f, .58f, .32f), new Color(.8f, .86f, .95f), new Color(1f, .8f, .34f),
            new Color(1f, .52f, .18f), new Color(1f, .26f, .18f), new Color(.86f, .6f, 1f) };

        class Barra
        {
            public Image cheio, rastro;
            public float valor = 1, mostrado = 1, espera;
        }

        GameManager manager;
        RectTransform root, gameplayRoot, targetMarker, estiloRaiz;
        GameObject modalBackdrop;
        // estado, andar e mensagem
        Text healthText, favorText, floorText, clock, message;
        Barra vida, favorBarra, estiloBarra, alvoBarra;
        Image mensagemDivisor;
        // estilo
        Text rank, rankTitle, styleDetail, chain, chainLabel, moveName;
        Image rankGlow, louroE, louroD;
        GradienteTexto rankGradiente;
        int ultimoRank, ultimosGolpes;
        float socoRank, socoGolpes, idadeMensagem = 99;
        string ultimaMensagem;
        // arsenal
        Text weaponText, weaponDetail;
        readonly Image[] armaIcones = new Image[3];
        readonly RectTransform[] armaSlots = new RectTransform[3];
        readonly Text[] estadoTeclas = new Text[3], estadoTextos = new Text[3];
        Image armaBrilho;
        // bênção
        DivineFavor favor;
        Image flash, aura, blessingIcon, blessingRing, chipMarker;
        Text blessingName, blessingState, banner;
        Image bannerDivisor;
        readonly Image[] godChips = new Image[6];
        float bannerAge = 9, flashAlpha, auraAlpha;
        Color flashColor = Color.white, auraColor = Color.white;
        // alvo
        Image mira;
        Text targetName;
        // modais
        GameObject choicePanel, pausePanel, moveList, controlsPanel;
        CanvasGroup modalAtual;
        float modalIdade;
        Text pauseTitle, pauseSub, pauseRodape;
        GradienteTexto pauseGradiente;
        readonly Text[] estatValores = new Text[4];
        Button continueButton, checkpointButton, trainingBlessing;
        readonly Button[] blessingButtons = new Button[3];
        readonly Text[] blessingTitles = new Text[3], blessingDescriptions = new Text[3], blessingPowers = new Text[3], blessingGods = new Text[3], blessingCosts = new Text[3];
        readonly Image[] cardIcons = new Image[3], cardGlows = new Image[3];
        readonly Text[] golpeNomes = new Text[3], golpeComandos = new Text[3], golpeDescricoes = new Text[3];
        readonly Image[] golpeIcones = new Image[3];
        RunState previousState = (RunState)(-1);
        bool showMoves, showControls;
        public bool ControlsVisible => controlsPanel && controlsPanel.activeSelf;

        // ================================================================== montagem
        void Start()
        {
            manager = GetComponent<GameManager>();
            var canvasObject = new GameObject("HUD_Iliada", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas = canvasObject.GetComponent<Canvas>();
            canvasObject.layer = CamadaUI;
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            root = canvasObject.GetComponent<RectTransform>();
            gameplayRoot = No(root, "Gameplay", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            // refaz o contêiner sempre que o Canvas muda de tamanho (resolução, troca de modo nas capturas)
            canvasObject.AddComponent<EscalaContida>().Definir(gameplayRoot, EscalaHUD);
            if (!FindFirstObjectByType<EventSystem>())
            {
                var eventObject = new GameObject("Gameplay_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
                eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            // por baixo de tudo: moldura colorida enquanto um poder dura e clarão quando ele cai
            aura = Cobrir(gameplayRoot, "Aura_divina", DivineLore.Vinheta());
            flash = Cobrir(gameplayRoot, "Clarao_divino", null);
            BuildStatus(); BuildFloor(); BuildStyle(); BuildArsenal(); BuildBlessing(); BuildTarget(); BuildBanner();
            var fundo = Cobrir(root, "Fundo_Modal", A.Sprite("Vinheta"));
            fundo.color = Color.white; fundo.raycastTarget = true;
            modalBackdrop = fundo.gameObject; modalBackdrop.SetActive(false);
            BuildModals();
            favor = manager.player.Favor;
            favor.CastStarted += OnCast; favor.Unleashed += OnUnleashed;
        }

        /// <summary>Tamanho do HUD de jogo em relação à tela de referência (1280x720); os menus não mudam.</summary>
        public const float EscalaHUD = .7f;

        void AjustarEscala() => EscalaContida.Aplicar(gameplayRoot, root, EscalaHUD);

        void OnCast(God god, int level)
        {
            bannerAge = 0; banner.text = DivineLore.Poder(god);
            var cor = DivineLore.Cor(god);
            banner.GetComponent<GradienteTexto>().Definir(Color.Lerp(cor, Color.white, .75f), Color.Lerp(cor, Color.white, .1f));
            bannerDivisor.color = Color.Lerp(cor, Color.white, .3f);
        }
        void OnUnleashed(God god, int level, Vector3 center, float radius)
        {
            flashColor = DivineLore.Claro(god);
            flashAlpha = Mathf.Max(flashAlpha, god == God.Zeus ? .26f : god == God.Poseidon || god == God.Ares ? .16f : .1f);
        }
        void OnDestroy() { if (favor) { favor.CastStarted -= OnCast; favor.Unleashed -= OnUnleashed; } }

        void BuildStatus()
        {
            var placa = Img(gameplayRoot, "Estado", A.Sprite("Painel_b36"), Color.white, Vector2.up, Vector2.up, new Vector2(22, -20), new Vector2(344, 100));
            placa.pixelsPerUnitMultiplier = 1.5f;
            Txt(placa.transform, "PÁTROCLO", A.Titulo, 17, Color.white, TextAnchor.MiddleLeft, Vector2.up, Vector2.up, new Vector2(22, -12), new Vector2(180, 24), A.OuroClaro, A.Tom(A.Ouro, .85f));
            Txt(placa.transform, "NOITE DA INVASÃO", A.Texto, 11, A.Apagado, TextAnchor.MiddleRight, Vector2.one, Vector2.one, new Vector2(-22, -14), new Vector2(150, 20));
            Txt(placa.transform, "VIDA", A.Titulo, 11, A.Apagado, TextAnchor.MiddleLeft, Vector2.up, Vector2.up, new Vector2(22, -42), new Vector2(50, 18));
            vida = NovaBarra(placa.transform, Vector2.up, Vector2.up, new Vector2(70, -40), new Vector2(252, 22), A.Sangue);
            healthText = Txt(vida.cheio.transform.parent.parent, "", A.Titulo, 12, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(240, 18));
            Txt(placa.transform, "FAVOR", A.Titulo, 11, A.Apagado, TextAnchor.MiddleLeft, Vector2.up, Vector2.up, new Vector2(22, -69), new Vector2(50, 18));
            favorBarra = NovaBarra(placa.transform, Vector2.up, Vector2.up, new Vector2(70, -68), new Vector2(252, 16), new Color(1f, .74f, .26f));
            favorText = Txt(favorBarra.cheio.transform.parent.parent, "", A.Titulo, 9, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(240, 14));
        }

        void BuildFloor()
        {
            var topo = No(gameplayRoot, "Andar", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -12), new Vector2(300, 84));
            louro(topo, "Louro_E", new Vector2(-122, -42), new Vector2(40, 80), false).color = new Color(1, 1, 1, .9f);
            louro(topo, "Louro_D", new Vector2(122, -42), new Vector2(40, 80), true).color = new Color(1, 1, 1, .9f);
            var placa = Img(topo, "Placa", A.Sprite("Painel_b36"), Color.white, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(200, 82));
            placa.pixelsPerUnitMultiplier = 1.7f;
            floorText = Txt(placa.transform, "", A.Titulo, 12, A.Ouro, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -11), new Vector2(180, 18));
            clock = Txt(placa.transform, "", A.Pesado, 30, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 10), new Vector2(190, 44), Color.white, new Color(.82f, .76f, .66f));
            message = Txt(gameplayRoot, "", A.Texto, 16, A.Pergaminho, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -104), new Vector2(720, 26));
            mensagemDivisor = Img(gameplayRoot, "Divisor_mensagem", A.Sprite("Divisor"), new Color(1, 1, 1, .85f), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -130), new Vector2(460, 18));
        }

        void BuildStyle()
        {
            estiloRaiz = No(gameplayRoot, "Estilo", Vector2.one, Vector2.one, new Vector2(-14, -12), new Vector2(300, 300));
            var fundo = Img(estiloRaiz, "Fundo", A.Sprite("Degrade"), Color.white, Vector2.one, Vector2.one, new Vector2(14, 12), new Vector2(420, 312));
            fundo.type = Image.Type.Simple;
            Txt(estiloRaiz, "ESTILO", A.Titulo, 12, A.Apagado, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -4), new Vector2(200, 18));
            var greca = Img(estiloRaiz, "Greca", A.Sprite("Greca"), new Color(1, 1, 1, .75f), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(144, 10), Image.Type.Tiled);
            greca.pixelsPerUnitMultiplier = 1.6f;
            rankGlow = Img(estiloRaiz, "Brilho", A.Sprite("Brilho"), Color.clear, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -92), new Vector2(230, 170));
            louroE = louro(estiloRaiz, "Louro_E", new Vector2(-112, -92), new Vector2(54, 108), false, new Vector2(.5f, 1));
            louroD = louro(estiloRaiz, "Louro_D", new Vector2(112, -92), new Vector2(54, 108), true, new Vector2(.5f, 1));
            rank = Txt(estiloRaiz, "D", A.Pesado, 86, RankColors[0], TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -90), new Vector2(260, 110), Color.white, new Color(.55f, .5f, .45f), 3);
            rankGradiente = rank.GetComponent<GradienteTexto>();
            rankTitle = Txt(estiloRaiz, "", A.Titulo, 17, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -144), new Vector2(290, 24));
            estiloBarra = NovaBarra(estiloRaiz, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -172), new Vector2(220, 14), A.Ouro);
            styleDetail = Txt(estiloRaiz, "", A.Texto, 12, A.Apagado, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -190), new Vector2(290, 18));
            chain = Txt(estiloRaiz, "", A.Pesado, 32, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -228), new Vector2(290, 44), Color.white, new Color(.84f, .78f, .68f), 2);
            chainLabel = Txt(estiloRaiz, "", A.Titulo, 12, A.Ouro, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -250), new Vector2(290, 18));
            moveName = Txt(estiloRaiz, "", A.Texto, 14, A.OuroClaro, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -270), new Vector2(290, 22));
        }

        void BuildArsenal()
        {
            var placa = Img(gameplayRoot, "Arsenal", A.Sprite("Painel_b36"), Color.white, Vector2.zero, Vector2.zero, new Vector2(22, 22), new Vector2(392, 132));
            placa.pixelsPerUnitMultiplier = 1.5f;
            var p = placa.transform;
            weaponText = Txt(p, "", A.Titulo, 22, Color.white, TextAnchor.MiddleLeft, Vector2.up, Vector2.up, new Vector2(22, -10), new Vector2(260, 30), A.OuroClaro, A.Tom(A.Ouro, .8f));
            Txt(p, "ARSENAL", A.Titulo, 11, A.Apagado, TextAnchor.MiddleRight, Vector2.one, Vector2.one, new Vector2(-22, -16), new Vector2(110, 18));
            weaponDetail = Txt(p, "", A.Texto, 13, A.Pergaminho, TextAnchor.MiddleLeft, Vector2.up, Vector2.up, new Vector2(22, -40), new Vector2(350, 20));
            armaBrilho = Img(p, "Brilho_arma", A.Sprite("Brilho"), new Color(1, .8f, .4f, .45f), Vector2.zero, new Vector2(.5f, .5f), new Vector2(49, 44), new Vector2(96, 96));
            for (int i = 0; i < 3; i++)
            {
                var slot = Img(p, "Arma_" + (i + 1), A.Sprite("Placa_b20"), Color.white, Vector2.zero, new Vector2(.5f, .5f), new Vector2(49 + i * 62, 44), new Vector2(54, 54));
                slot.pixelsPerUnitMultiplier = 1.6f;
                armaSlots[i] = slot.rectTransform;
                armaIcones[i] = Img(slot.transform, "Icone", null, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(42, 42));
                armaIcones[i].preserveAspect = true;
                Tecla(slot.transform, (i + 1).ToString(), new Vector2(1, 0), new Vector2(-2, 2), 21);
            }
            string[] teclas = { "E", "R", "P" };
            for (int j = 0; j < 3; j++)
            {
                float y = 70 - j * 21;
                var t = Tecla(p, teclas[j], Vector2.zero, new Vector2(222, y), 19);
                estadoTeclas[j] = t;
                estadoTextos[j] = Txt(p, "", A.Texto, 13, A.Pergaminho, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(0, .5f), new Vector2(240, y), new Vector2(140, 18));
            }
        }

        void BuildBlessing()
        {
            var placa = Img(gameplayRoot, "Bencao", A.Sprite("Painel_b36"), Color.white, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 22), new Vector2(392, 132));
            placa.pixelsPerUnitMultiplier = 1.5f;
            var p = placa.transform;
            var medalhao = Img(p, "Medalhao", A.Sprite("Medalhao"), Color.white, new Vector2(1, .5f), new Vector2(.5f, .5f), new Vector2(-64, 0), new Vector2(108, 108));
            blessingRing = Img(medalhao.transform, "Recarga", A.Sprite("Anel"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(104, 104));
            blessingRing.type = Image.Type.Filled; blessingRing.fillMethod = Image.FillMethod.Radial360;
            blessingRing.fillOrigin = (int)Image.Origin360.Top; blessingRing.fillClockwise = true;
            blessingIcon = Img(medalhao.transform, "Simbolo", DivineLore.Simbolo(God.Zeus), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(56, 56));
            blessingIcon.preserveAspect = true;
            Txt(p, "BÊNÇÃO", A.Titulo, 11, A.Apagado, TextAnchor.MiddleLeft, Vector2.up, Vector2.up, new Vector2(22, -14), new Vector2(90, 18));
            Txt(p, "Q usar  ·  TAB trocar", A.Texto, 11, A.Apagado, TextAnchor.MiddleRight, Vector2.up, Vector2.up, new Vector2(110, -14), new Vector2(150, 18));
            blessingName = Txt(p, "", A.Titulo, 19, Color.white, TextAnchor.MiddleLeft, Vector2.up, Vector2.up, new Vector2(22, -34), new Vector2(250, 26), A.OuroClaro, A.Ouro);
            blessingState = Txt(p, "", A.Texto, 12, A.Pergaminho, TextAnchor.UpperLeft, Vector2.up, Vector2.up, new Vector2(22, -60), new Vector2(246, 34));
            chipMarker = Img(p, "Selecionado", A.Sprite("Anel"), Color.white, Vector2.zero, new Vector2(.5f, .5f), new Vector2(36, 22), new Vector2(32, 32));
            for (int i = 0; i < 6; i++)
            {
                var moeda = Img(p, "Deus_" + i, A.Sprite("Disco"), new Color(.09f, .07f, .05f, .95f), Vector2.zero, new Vector2(.5f, .5f), new Vector2(36 + i * 38, 22), new Vector2(26, 26));
                godChips[i] = Img(moeda.transform, "Simbolo", DivineLore.Simbolo((God)i), DivineLore.Cor((God)i), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(19, 19));
                godChips[i].preserveAspect = true;
            }
        }

        void BuildTarget()
        {
            targetMarker = No(gameplayRoot, "Alvo", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(120, 120));
            mira = Img(targetMarker, "Mira", A.Sprite("Alvo"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(86, 86));
            targetName = Txt(targetMarker, "", A.Titulo, 13, A.Pergaminho, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(.5f, 1), new Vector2(0, -46), new Vector2(240, 20));
            alvoBarra = NovaBarra(targetMarker, new Vector2(.5f, .5f), new Vector2(.5f, 1), new Vector2(0, -66), new Vector2(112, 12), A.Sangue);
        }

        void BuildBanner()
        {
            banner = Txt(gameplayRoot, "", A.Pesado, 42, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -196), new Vector2(940, 64), Color.white, A.Ouro, 3);
            banner.color = Color.clear;
            bannerDivisor = Img(gameplayRoot, "Divisor_poder", A.Sprite("Divisor"), Color.clear, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -232), new Vector2(480, 20));
        }

        void BuildModals()
        {
            // ------------------------------------------------ escolha de bênção
            var escolha = No(root, "Bencaos", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(1000, 560));
            choicePanel = escolha.gameObject; escolha.gameObject.AddComponent<CanvasGroup>();
            Txt(escolha, "OS DEUSES OFERECEM SEU FAVOR", A.Pesado, 34, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -2), new Vector2(980, 48), A.OuroClaro, A.Tom(A.Ouro, .8f), 2);
            Txt(escolha, "Escolha com 1, 2 ou 3  ·  ou com o direcional e A", A.Texto, 15, A.Apagado, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -50), new Vector2(900, 22));
            Img(escolha, "Divisor", A.Sprite("Divisor"), Color.white, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -76), new Vector2(560, 20));
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var carta = Img(escolha, "Carta_" + i, A.Sprite("Carta_b60"), Color.white, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-320 + i * 320, -108), new Vector2(296, 432));
                carta.raycastTarget = true; carta.pixelsPerUnitMultiplier = 1.25f;
                var botao = carta.gameObject.AddComponent<Button>();
                botao.targetGraphic = carta;
                var cores = botao.colors; cores.normalColor = new Color(.8f, .8f, .8f); cores.highlightedColor = cores.selectedColor = Color.white;
                cores.pressedColor = new Color(1, .95f, .82f); cores.fadeDuration = .12f; botao.colors = cores;
                botao.onClick.AddListener(() => manager.ChooseBlessing(index));
                blessingButtons[i] = botao;
                var c = carta.transform;
                cardGlows[i] = Img(c, "Brilho", A.Sprite("Brilho"), Color.clear, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -96), new Vector2(300, 240));
                var realce = carta.gameObject.AddComponent<BotaoTroia>();
                realce.realce = cardGlows[i]; realce.alfaRealce = .75f; realce.escalaAcesa = 1.04f;
                var medalhao = Img(c, "Medalhao", A.Sprite("Medalhao"), Color.white, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -96), new Vector2(118, 118));
                cardIcons[i] = Img(medalhao.transform, "Simbolo", DivineLore.Simbolo(God.Zeus), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(68, 68));
                cardIcons[i].preserveAspect = true;
                Tecla(c, (i + 1).ToString(), Vector2.up, new Vector2(30, -30), 26);
                blessingGods[i] = Txt(c, "", A.Titulo, 12, A.Apagado, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -164), new Vector2(260, 18));
                blessingTitles[i] = Txt(c, "", A.Titulo, 21, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -184), new Vector2(262, 54), Color.white, new Color(.8f, .75f, .65f));
                blessingPowers[i] = Txt(c, "", A.Texto, 14, A.Ouro, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -238), new Vector2(262, 24));
                Img(c, "Divisor", A.Sprite("Divisor"), new Color(1, 1, 1, .8f), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -264), new Vector2(220, 14));
                blessingDescriptions[i] = Txt(c, "", A.Texto, 14, A.Pergaminho, TextAnchor.UpperCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -284), new Vector2(246, 100));
                blessingCosts[i] = Txt(c, "", A.Texto, 12, A.Apagado, TextAnchor.MiddleCenter, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 26), new Vector2(250, 18));
            }
            choicePanel.SetActive(false);

            // ------------------------------------------------ pausa, vitória e derrota
            var pausa = No(root, "Pausa", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(640, 620));
            pausePanel = pausa.gameObject; pausa.gameObject.AddComponent<CanvasGroup>();
            var placaPausa = Img(pausa, "Placa", A.Sprite("Painel_b36"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -6), new Vector2(660, 640));
            placaPausa.pixelsPerUnitMultiplier = 1.1f;
            louro(pausa, "Louro_E", new Vector2(-250, -52), new Vector2(48, 96), false, new Vector2(.5f, 1));
            louro(pausa, "Louro_D", new Vector2(250, -52), new Vector2(48, 96), true, new Vector2(.5f, 1));
            pauseTitle = Txt(pausa, "", A.Pesado, 50, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(460, 70), A.OuroClaro, A.Tom(A.Ouro, .75f), 3);
            pauseGradiente = pauseTitle.GetComponent<GradienteTexto>();
            pauseSub = Txt(pausa, "", A.Texto, 15, A.Apagado, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -86), new Vector2(600, 22));
            var greca = Img(pausa, "Greca", A.Sprite("Greca"), Color.white, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -116), new Vector2(432, 12), Image.Type.Tiled);
            greca.pixelsPerUnitMultiplier = 1.35f;
            string[] rotulos = { "ANDAR", "ELIMINAÇÕES", "TEMPO", "MELHOR ESTILO" };
            for (int i = 0; i < 4; i++)
            {
                float x = -225 + i * 150;
                estatValores[i] = Txt(pausa, "", A.Titulo, 26, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(x, -138), new Vector2(150, 34), Color.white, new Color(.82f, .76f, .66f));
                Txt(pausa, rotulos[i], A.Texto, 11, A.Apagado, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(x, -172), new Vector2(150, 16));
            }
            float y = -200;
            continueButton = Botao(pausa, "CONTINUAR", y, () => manager.TogglePause()); y -= 43;
            Botao(pausa, "LISTA DE GOLPES  [H]", y, ShowMoveList); y -= 43;
            Botao(pausa, "CONTROLES", y, MostrarControles); y -= 43;
            Botao(pausa, "TREINO LIVRE  [F2]", y, () => manager.BeginTraining()); y -= 43;
            trainingBlessing = Botao(pausa, "TROCAR BÊNÇÃO", y, () => { manager.player.Favor.Cycle(); RefreshModals(); }); y -= 43;
            Botao(pausa, "REINICIAR PARTIDA", y, () => manager.Restart(false)); y -= 43;
            checkpointButton = Botao(pausa, "RETOMAR CHECKPOINT", y, () => manager.Restart(true)); y -= 48;
            Img(pausa, "Divisor", A.Sprite("Divisor"), new Color(1, 1, 1, .8f), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, y), new Vector2(420, 18));
            pauseRodape = Txt(pausa, "No treino: alvos resistentes, tempo livre, favor sem fim e todas as bênçãos.\nQ usa a bênção, TAB troca de deus, 4 a 9 escolhem direto.", A.Texto, 13, A.Apagado,
                TextAnchor.UpperCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, y - 22), new Vector2(580, 44));
            pausePanel.SetActive(false);

            // ------------------------------------------------ lista de golpes (gerada da árvore de combos de cada arma)
            var lista = Img(root, "Golpes", A.Sprite("Painel_b36"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1120, 640));
            moveList = lista.gameObject; lista.gameObject.AddComponent<CanvasGroup>();
            var l = lista.transform;
            Txt(l, "ARSENAL DE PÁTROCLO", A.Pesado, 32, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(900, 46), A.OuroClaro, A.Tom(A.Ouro, .8f), 2);
            var g2 = Img(l, "Greca", A.Sprite("Greca"), Color.white, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -72), new Vector2(480, 12), Image.Type.Tiled);
            g2.pixelsPerUnitMultiplier = 1.35f;
            Txt(l, "L = leve   ·   P = pesado   ·   C = lançador   ·   E = avanço   ·   troque 1 / 2 / 3 no meio do combo e a sequência continua", A.Texto, 14, A.Apagado,
                TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -92), new Vector2(1060, 20));
            for (int i = 0; i < 3; i++)
            {
                float x = -356 + i * 356;
                var coluna = Img(l, "Arma_" + i, A.Sprite("Placa_b20"), new Color(1, 1, 1, .85f), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(x, -116), new Vector2(340, 440));
                coluna.pixelsPerUnitMultiplier = 1.4f;
                var co = coluna.transform;
                golpeIcones[i] = Img(co, "Icone", null, Color.white, Vector2.up, new Vector2(.5f, .5f), new Vector2(38, -34), new Vector2(42, 42));
                golpeIcones[i].preserveAspect = true;
                golpeNomes[i] = Txt(co, "", A.Titulo, 21, Color.white, TextAnchor.MiddleLeft, Vector2.up, Vector2.up, new Vector2(68, -12), new Vector2(256, 28), Color.white, new Color(.8f, .75f, .65f));
                golpeDescricoes[i] = Txt(co, "", A.Texto, 12, A.Apagado, TextAnchor.UpperLeft, Vector2.up, Vector2.up, new Vector2(68, -40), new Vector2(256, 32));
                golpeComandos[i] = Txt(co, "", A.Texto, 14, A.Pergaminho, TextAnchor.UpperLeft, Vector2.up, Vector2.up, new Vector2(20, -84), new Vector2(304, 350));
                golpeComandos[i].supportRichText = true; golpeComandos[i].lineSpacing = 1.12f;
            }
            Txt(l, "Repetir o mesmo golpe não pontua  ·  apanhar derruba dois níveis de estilo  ·  pesados e quedas exigem recuperação", A.Texto, 13, A.Apagado,
                TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -566), new Vector2(1060, 18));
            Botao(l, "VOLTAR  [H]", -588, () => { showMoves = false; RefreshModals(); });
            moveList.SetActive(false);

            // ------------------------------------------------ controles
            var ctrl = Img(root, "Controles", A.Sprite("Painel_b36"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1000, 640));
            controlsPanel = ctrl.gameObject; ctrl.gameObject.AddComponent<CanvasGroup>();
            var cp = ctrl.transform;
            Txt(cp, "CONTROLES", A.Pesado, 32, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(900, 46), A.OuroClaro, A.Tom(A.Ouro, .8f), 2);
            var g3 = Img(cp, "Greca", A.Sprite("Greca"), Color.white, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -72), new Vector2(400, 12), Image.Type.Tiled);
            g3.pixelsPerUnitMultiplier = 1.35f;
            float[] colunas = { -300, 30, 300 };
            string[] cabecalhos = { "AÇÃO", "TECLADO E MOUSE", "CONTROLE" };
            for (int c = 0; c < 3; c++)
                Txt(cp, cabecalhos[c], A.Titulo, 13, A.Ouro, c == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(colunas[c], -96), new Vector2(c == 0 ? 300 : 260, 18));
            for (int r = 0; r < Controles.Length / 3; r++)
            {
                float yy = -120 - r * 25;
                if (r % 2 == 0) Img(cp, "Faixa", null, new Color(1, .9f, .7f, .045f), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, yy), new Vector2(900, 24));
                Txt(cp, Controles[r * 3], A.Texto, 15, A.Pergaminho, TextAnchor.MiddleLeft, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(colunas[0], yy), new Vector2(300, 24));
                Txt(cp, Controles[r * 3 + 1], A.Titulo, 14, A.OuroClaro, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(colunas[1], yy), new Vector2(300, 24));
                Txt(cp, Controles[r * 3 + 2], A.Titulo, 14, A.OuroClaro, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(colunas[2], yy), new Vector2(260, 24));
            }
            Txt(cp, "Botões do controle no padrão Xbox (A, B, X, Y); no PlayStation: A = cruz, B = círculo, X = quadrado, Y = triângulo.", A.Texto, 12, A.Apagado,
                TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -566), new Vector2(940, 18));
            Botao(cp, "VOLTAR", -588, () => { showControls = false; RefreshModals(); });
            controlsPanel.SetActive(false);
        }

        // ação, teclado e mouse, controle (o que PlayerCombat, DivineFavor e o HUD de fato leem)
        static readonly string[] Controles = {
            "Mover", "W  A  S  D", "Analógico esquerdo",
            "Câmera  ·  aproximar", "Mouse  ·  roda", "Analógico direito",
            "Correr", "Shift", "Apertar analógico esquerdo",
            "Pulo e pulo duplo", "Espaço", "A",
            "Esquiva  ·  cancela o golpe", "Ctrl", "B",
            "Ataque leve (L)", "Botão esquerdo  ·  J", "X",
            "Ataque pesado (P)", "Botão direito  ·  K", "Y",
            "Lançador", "C  ·  segurar P", "Segurar Y",
            "Avanço  ·  arpão (escudo)", "E", "RB",
            "Dardos", "R (segurar)", "RT",
            "Foco no alvo", "F  ·  botão do meio", "LT",
            "Próximo alvo", "T", "Apertar analógico direito",
            "Trocar de arma", "1  2  3", "Direcional esquerda / direita",
            "Usar bênção", "Q", "LB",
            "Trocar de deus", "Tab ou G (Shift volta)  ·  4 a 9", "Direcional para baixo",
            "Pausa", "Esc", "Start",
            "Lista de golpes  ·  treino livre", "H  ·  F2", "Pausa > menu",
        };

        const int CamadaUI = 5;

        // ================================================================== peças
        static RectTransform No(Transform pai, string nome, Vector2 ancora, Vector2 pivo, Vector2 pos, Vector2 tamanho)
        {
            var go = new GameObject(nome, typeof(RectTransform)) { layer = CamadaUI };
            go.transform.SetParent(pai, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = ancora; r.pivot = pivo; r.anchoredPosition = pos; r.sizeDelta = tamanho;
            return r;
        }

        static RectTransform Esticar(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        static Image Img(Transform pai, string nome, Sprite sprite, Color cor, Vector2 ancora, Vector2 pivo, Vector2 pos, Vector2 tamanho, Image.Type tipo = Image.Type.Simple)
        {
            var img = No(pai, nome, ancora, pivo, pos, tamanho).gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = cor; img.raycastTarget = false;
            img.type = tipo == Image.Type.Simple && sprite && sprite.border != Vector4.zero ? Image.Type.Sliced : tipo;
            return img;
        }

        static Image Cobrir(Transform pai, string nome, Sprite sprite)
        {
            var img = Img(pai, nome, sprite, Color.clear, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            img.type = Image.Type.Simple;
            Esticar(img.rectTransform);
            return img;
        }

        /// <summary>Texto com sombra; com cores de topo e base ganha gradiente por letra.</summary>
        static Text Txt(Transform pai, string texto, Font fonte, int tamanho, Color cor, TextAnchor alinhamento, Vector2 ancora, Vector2 pivo, Vector2 pos, Vector2 caixa,
            Color? topo = null, Color? baixo = null, float sombra = 1.5f)
        {
            var t = No(pai, "Texto", ancora, pivo, pos, caixa).gameObject.AddComponent<Text>();
            t.font = fonte; t.fontSize = tamanho; t.color = cor; t.text = texto; t.alignment = alinhamento;
            t.raycastTarget = false; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            if (topo.HasValue)
            {
                var g = t.gameObject.AddComponent<GradienteTexto>();
                g.topo = topo.Value; g.baixo = baixo ?? topo.Value;
            }
            if (sombra > 0)
            {
                var s = t.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, .8f); s.effectDistance = new Vector2(sombra, -sombra);
            }
            return t;
        }

        static Image louro(Transform pai, string nome, Vector2 pos, Vector2 tamanho, bool espelhar, Vector2? ancora = null)
        {
            var img = Img(pai, nome, A.Sprite("Louro"), Color.white, ancora ?? new Vector2(.5f, 1), new Vector2(.5f, .5f), pos, tamanho);
            img.preserveAspect = true;
            if (espelhar) img.rectTransform.localScale = new Vector3(-1, 1, 1);
            return img;
        }

        /// <summary>Tecla desenhada (moldura de ouro) com o texto dentro; devolve o texto.</summary>
        static Text Tecla(Transform pai, string texto, Vector2 ancora, Vector2 pos, float tamanho)
        {
            var img = Img(pai, "Tecla_" + texto, A.Sprite("Tecla_b10"), Color.white, ancora, new Vector2(.5f, .5f), pos, new Vector2(tamanho, tamanho));
            img.pixelsPerUnitMultiplier = 20f / tamanho;
            var t = Txt(img.transform, texto, A.Titulo, Mathf.RoundToInt(tamanho * .62f), A.OuroClaro, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(200, tamanho), null, null, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        static Barra NovaBarra(Transform pai, Vector2 ancora, Vector2 pivo, Vector2 pos, Vector2 tamanho, Color cor)
        {
            var moldura = Img(pai, "Barra", A.Sprite("Barra_Moldura_b9"), Color.white, ancora, pivo, pos, tamanho);
            moldura.pixelsPerUnitMultiplier = Mathf.Max(1, 20f / tamanho.y);
            float folga = Mathf.Clamp(tamanho.y * .2f, 2, 4);
            var area = No(moldura.transform, "Area", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(folga + 2, folga); area.offsetMax = new Vector2(-folga - 2, -folga);
            var b = new Barra();
            b.rastro = Img(area, "Rastro", A.Sprite("Barra_Preenchimento"), new Color(1, .62f, .32f, .6f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Esticar(b.rastro.rectTransform);
            b.cheio = Img(area, "Cheio", A.Sprite("Barra_Preenchimento"), cor, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Esticar(b.cheio.rectTransform);
            return b;
        }

        /// <summary>Barra cheia na hora; o rastro claro espera um instante e desce devagar (mostra o dano levado).</summary>
        static void AtualizarBarra(Barra b, float valor, float dt)
        {
            valor = Mathf.Clamp01(valor);
            if (valor < b.valor - .0001f) b.espera = .45f;
            b.valor = valor;
            if (b.espera > 0) b.espera -= dt; else b.mostrado = Mathf.MoveTowards(b.mostrado, valor, dt * .8f);
            if (valor > b.mostrado) b.mostrado = valor;
            b.cheio.rectTransform.anchorMax = new Vector2(valor, 1);
            b.rastro.rectTransform.anchorMax = new Vector2(b.mostrado, 1);
        }

        Button Botao(Transform pai, string texto, float y, UnityEngine.Events.UnityAction acao)
        {
            var fundo = Img(pai, texto, A.Sprite("Realce"), new Color(1, 1, 1, 0), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, y), new Vector2(440, 40));
            fundo.raycastTarget = true;
            var botao = fundo.gameObject.AddComponent<Button>();
            botao.transition = Selectable.Transition.None;
            botao.onClick.AddListener(acao);
            var rotulo = Txt(fundo.transform, texto, A.Titulo, 19, A.Pergaminho, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(420, 34));
            var visual = fundo.gameObject.AddComponent<BotaoTroia>();
            visual.realce = fundo; visual.rotulo = rotulo; visual.alfaRealce = .7f;
            visual.marcaEsquerda = Img(fundo.transform, "Losango_E", A.Sprite("Losango"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(14, 14)).rectTransform;
            visual.marcaDireita = Img(fundo.transform, "Losango_D", A.Sprite("Losango"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(14, 14)).rectTransform;
            return botao;
        }

        // ================================================================== a cada quadro
        void Update()
        {
            if (!manager || !manager.player || !Canvas) return;
            if (Keyboard.current?.hKey.wasPressedThisFrame ?? false)
            {
                showControls = false;
                if (showMoves) showMoves = false;
                else if (manager.IsPlaying || manager.State == RunState.Paused)
                {
                    if (manager.IsPlaying) manager.TogglePause();
                    showMoves = true;
                }
                RefreshModals();
            }
            if ((Keyboard.current?.f2Key.wasPressedThisFrame ?? false) && manager.State != RunState.ChoosingBlessing) manager.BeginTraining();
            // com o jogo pausado no treino, TAB continua trocando de deus (o botão da pausa mostra qual está escolhido)
            if (manager.State == RunState.Paused && manager.TrainingMode && (Keyboard.current?.tabKey.wasPressedThisFrame ?? false))
            { manager.player.Favor.Cycle(Keyboard.current.leftShiftKey.isPressed ? -1 : 1); RefreshModals(); }
        }

        void LateUpdate()
        {
            if (!manager || !manager.player || !Canvas) return;
            AjustarEscala();
            float dt = Time.unscaledDeltaTime, tempo = Time.unscaledTime;
            var p = manager.player; var style = p.Style;
            // ------------------------------------------------ vida e favor
            float fracaoVida = p.Health.Current / p.Health.maximum;
            AtualizarBarra(vida, fracaoVida, dt);
            vida.cheio.color = fracaoVida < .3f ? Color.Lerp(A.Sangue, new Color(1, .35f, .25f), .5f + .5f * Mathf.Sin(tempo * 8)) : A.Sangue;
            healthText.text = $"{p.Health.Current:0}  /  {p.Health.maximum:0}";
            AtualizarBarra(favorBarra, p.Favor.Amount / 100, dt);
            favorText.text = $"{p.Favor.Amount:0}  /  100";
            // ------------------------------------------------ andar, tempo e objetivo
            floorText.text = manager.TrainingMode ? "TREINO LIVRE" : $"ANDAR  {manager.Floor:00}  ·  {manager.config.floors.Length}";
            clock.text = manager.TrainingMode ? "SEM LIMITE" : $"{Mathf.FloorToInt(manager.RemainingTime / 60):00}:{Mathf.FloorToInt(manager.RemainingTime % 60):00}";
            clock.fontSize = manager.TrainingMode ? 20 : 30;
            bool urgente = manager.RemainingTime < 30 && !manager.TrainingMode;
            clock.color = urgente ? Color.Lerp(new Color(1, .3f, .25f), Color.white, .25f + .25f * Mathf.Sin(tempo * 9)) : Color.white;
            string texto = manager.Message;
            if (manager.IsPlaying && !manager.FloorActive && !manager.TrainingMode)
                texto += $"   ·   {Vector3.Distance(p.transform.position, manager.Destination):0} m";
            // na luta a mensagem só aparece por alguns segundos quando muda (o andar já está no medalhão)
            if (manager.Message != ultimaMensagem) { ultimaMensagem = manager.Message; idadeMensagem = 0; }
            idadeMensagem += dt;
            float alfaMensagem = string.IsNullOrEmpty(manager.Message) ? 0 : !manager.FloorActive ? 1 : 1 - Mathf.Clamp01((idadeMensagem - 3.5f) / .6f);
            message.text = texto;
            message.color = new Color(A.Pergaminho.r, A.Pergaminho.g, A.Pergaminho.b, alfaMensagem);
            mensagemDivisor.color = new Color(1, 1, 1, .85f * alfaMensagem);
            // ------------------------------------------------ estilo
            int nota = style.Rank;
            Color corNota = RankColors[nota];
            if (nota > ultimoRank) socoRank = 1;
            ultimoRank = nota;
            socoRank = Mathf.MoveTowards(socoRank, 0, dt * 3.5f);
            rank.text = StyleMeter.Labels[nota]; rank.color = corNota;
            rankGradiente.Definir(Color.Lerp(corNota, Color.white, .55f), Color.Lerp(corNota, Color.black, .35f));
            float pulso = nota >= 5 ? .03f * Mathf.Sin(tempo * (nota == 6 ? 9 : 6)) : 0;
            rank.transform.localScale = Vector3.one * (1 + socoRank * socoRank * .35f + style.RankFlash * .06f + pulso);
            float brilhoNota = .05f + nota * .025f + socoRank * .3f + (nota == 6 ? .06f + .04f * Mathf.Sin(tempo * 7) : 0);
            rankGlow.color = new Color(corNota.r, corNota.g, corNota.b, brilhoNota);
            // a coroa de louros aparece a partir de A
            float coroa = Mathf.Clamp01((nota - 2.5f) * 2);
            var corLouro = Color.Lerp(Color.white, corNota, .35f); corLouro.a = coroa;
            louroE.color = louroD.color = corLouro;
            rankTitle.text = StyleMeter.Titles[nota]; rankTitle.color = Color.Lerp(corNota, Color.white, .2f);
            AtualizarBarra(estiloBarra, style.RankProgress, dt);
            estiloBarra.cheio.color = corNota;
            bool repetindo = style.Freshness == 0 && style.ChainHits > 0;
            styleDetail.text = repetindo ? "REPETIÇÃO  ·  VARIE OS GOLPES" : $"FAVOR ×{style.Multiplier:0.00}     ESTILO +{style.LastGain:0}";
            styleDetail.color = repetindo ? new Color(1, .45f, .35f) : A.Apagado;
            if (style.ChainHits > ultimosGolpes) socoGolpes = 1;
            ultimosGolpes = style.ChainHits;
            socoGolpes = Mathf.MoveTowards(socoGolpes, 0, dt * 6);
            if (style.ChainHits > 0)
            {
                chain.text = style.ChainHits.ToString();
                chain.transform.localScale = Vector3.one * (1 + socoGolpes * .25f);
                chainLabel.text = $"GOLPES  ·  {style.ChainDamage:0} DE DANO";
                chainLabel.color = A.Ouro;
            }
            else
            {
                chain.text = "";
                chainLabel.text = "CONECTE SEUS ATAQUES";
                chainLabel.color = A.Apagado;
            }
            moveName.text = style.LastMove ?? "";
            // ------------------------------------------------ arsenal
            if (p.Weapon)
            {
                weaponText.text = p.Weapon.displayName.ToUpperInvariant();
                weaponText.color = Color.Lerp(p.Weapon.color, Color.white, .35f);
                if (p.PerfectDodgeFlash > 0) { weaponDetail.text = "ESQUIVA PERFEITA  ·  +FAVOR"; weaponDetail.color = A.OuroClaro; }
                else if (p.PendingWeapon >= 0) { weaponDetail.text = "Troca preparada: terminando o golpe"; weaponDetail.color = A.OuroClaro; }
                else
                {
                    weaponDetail.text = p.CurrentAttack ? (string.IsNullOrEmpty(p.CurrentAttack.displayName) ? p.CurrentAttack.id : p.CurrentAttack.displayName) :
                        p.Weapon.kind == WeaponKind.Spear ? "Controle e avanço" : p.Weapon.kind == WeaponKind.Shield ? "Impacto e arpão" : "Precisão e encadeamento";
                    weaponDetail.color = A.Pergaminho;
                }
                var armas = manager.config.weapons;
                for (int i = 0; i < 3; i++)
                {
                    bool existe = armas != null && i < armas.Length && armas[i];
                    armaSlots[i].gameObject.SetActive(existe);
                    if (!existe) continue;
                    bool atual = i == p.WeaponIndex, preparada = i == p.PendingWeapon;
                    armaIcones[i].sprite = A.IconeArma(armas[i].kind);
                    armaIcones[i].color = atual ? Color.Lerp(armas[i].color, Color.white, .45f) : preparada ? new Color(1, 1, 1, .8f) : new Color(.55f, .52f, .48f, .7f);
                    float alvo = atual ? 1.12f : 1;
                    float s = Mathf.MoveTowards(armaSlots[i].localScale.x, alvo, dt * 2);
                    armaSlots[i].localScale = Vector3.one * s;
                }
                if (p.WeaponIndex < 3)
                {
                    var destino = armaSlots[p.WeaponIndex].anchoredPosition;
                    armaBrilho.rectTransform.anchoredPosition = Vector2.Lerp(armaBrilho.rectTransform.anchoredPosition, destino, 1 - Mathf.Exp(-18 * dt));
                    var cb = Color.Lerp(p.Weapon.color, Color.white, .3f); cb.a = .5f; armaBrilho.color = cb;
                }
                bool avanco = p.ApproachCooldown <= 0, dardos = p.ShotCooldown <= .3f;
                float pesado = p.MoveCooldown(p.Weapon.heavy);
                estadoTextos[0].text = $"{(p.Weapon.pullsEnemy ? "Arpão" : "Avanço")}  {(avanco ? "pronto" : p.ApproachCooldown.ToString("0.0") + " s")}";
                estadoTextos[1].text = dardos ? "Dardos  prontos" : "Dardos  esfriando";
                estadoTextos[2].text = p.RecoveryRemaining > 0 ? $"Recuperação  {p.RecoveryRemaining:0.0} s" : pesado > 0 ? $"Pesado  {pesado:0.0} s" : "Pesado  pronto";
                bool[] prontos = { avanco, dardos, p.RecoveryRemaining <= 0 && pesado <= 0 };
                for (int j = 0; j < 3; j++)
                {
                    estadoTextos[j].color = prontos[j] ? A.Pergaminho : A.Apagado;
                    estadoTeclas[j].color = prontos[j] ? A.OuroClaro : new Color(.5f, .46f, .4f);
                }
            }
            if (previousState != manager.State) { if (manager.State != RunState.Paused) showMoves = showControls = false; RefreshModals(); previousState = manager.State; }
            UpdateTarget(p, dt);
            UpdateBlessing(p, dt);
            // ------------------------------------------------ entrada dos menus
            if (modalAtual)
            {
                modalIdade += dt;
                float e = Mathf.Clamp01(modalIdade / .22f); e = 1 - (1 - e) * (1 - e);
                modalAtual.alpha = e;
                modalAtual.transform.localScale = Vector3.one * Mathf.Lerp(.96f, 1, e);
            }
        }

        void UpdateBlessing(PlayerCombat p, float dt)
        {
            var f = p.Favor; God god = f.Selected; int level = f.Level(god);
            bool owned = level > 0;
            Color color = DivineLore.Cor(god);
            blessingIcon.sprite = DivineLore.Simbolo(god); blessingIcon.color = owned ? Color.Lerp(color, Color.white, .2f) : new Color(1, 1, 1, .14f);
            blessingName.text = owned ? DivineLore.Poder(god) : "SEM BÊNÇÃO";
            blessingName.color = owned ? Color.Lerp(color, Color.white, .3f) : A.Apagado;
            float cooldown = f.CooldownRemaining, cost = f.SelectedCost;
            bool ready = owned && cooldown <= 0 && f.Amount >= cost && !f.Casting;
            if (!owned) blessingState.text = "Vença 5 andares para receber a primeira.";
            else if (god == God.Ares && f.FuryRemaining > 0) blessingState.text = $"Nível {level}  ·  FÚRIA ATIVA  {f.FuryRemaining:0.0} s";
            else if (god == God.Athena && f.ShieldRemaining > 0) blessingState.text = $"Nível {level}  ·  ÉGIDE ATIVA  {f.ShieldRemaining:0.0} s";
            else if (cooldown > 0) blessingState.text = $"Nível {level}  ·  recarga  {cooldown:0.0} s";
            else if (f.Amount < cost) blessingState.text = $"Nível {level}  ·  favor  {f.Amount:0} / {cost:0}";
            else blessingState.text = $"Nível {level}  ·  PRONTA\n{DivineLore.Resumo(god)}";
            blessingState.color = ready ? A.Pergaminho : A.Apagado;
            blessingRing.fillAmount = owned ? 1 - f.CooldownFraction : 0;
            float pulso = ready ? .85f + .15f * Mathf.Sin(Time.unscaledTime * 5) : .45f;
            blessingRing.color = new Color(color.r, color.g, color.b, pulso);
            for (int i = 0; i < 6; i++)
            {
                bool has = f.Level((God)i) > 0, selected = owned && i == (int)god;
                Color c = DivineLore.Cor((God)i); c.a = selected ? 1 : has ? .55f : .14f;
                godChips[i].color = c;
                godChips[i].transform.parent.localScale = Vector3.one * (selected ? 1.15f : 1);
            }
            chipMarker.enabled = owned;
            chipMarker.rectTransform.anchoredPosition = new Vector2(36 + (int)god * 38, 22);
            chipMarker.color = Color.Lerp(color, Color.white, .3f);
            // nome do poder: entra grande, assenta e some
            bannerAge += dt;
            float show = Mathf.Clamp01(bannerAge / .12f) * (1 - Mathf.Clamp01((bannerAge - .9f) / .45f));
            banner.color = new Color(1, 1, 1, show);
            banner.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1, Mathf.Clamp01(bannerAge / .16f));
            var cd = bannerDivisor.color; cd.a = show; bannerDivisor.color = cd;
            bannerDivisor.rectTransform.localScale = new Vector3(Mathf.Clamp01(bannerAge / .25f), 1, 1);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0, dt * 2.4f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
            // moldura na cor do poder que está durando
            float want = 0; Color tint = auraColor;
            if (f.ShieldRemaining > 0) { want = .26f * Mathf.Clamp01(f.ShieldRemaining / .5f); tint = DivineLore.Cor(God.Athena); }
            else if (f.FuryRemaining > 0) { want = (.3f + .06f * Mathf.Sin(Time.unscaledTime * 6)) * Mathf.Clamp01(f.FuryRemaining / .5f); tint = DivineLore.Cor(God.Ares); }
            else if (f.BoostRemaining > 0) { want = .16f * Mathf.Clamp01(f.BoostRemaining / .4f); tint = DivineLore.Cor(God.Hermes); }
            auraAlpha = Mathf.MoveTowards(auraAlpha, want, dt * 2.5f); auraColor = tint;
            aura.color = new Color(tint.r, tint.g, tint.b, auraAlpha);
        }

        void UpdateTarget(PlayerCombat player, float dt)
        {
            bool visible = player.Targeting.IsLocked && player.cameraTransform && manager.IsPlaying;
            if (!visible) { targetMarker.gameObject.SetActive(false); return; }
            var target = player.Targeting.LockedTarget;
            Vector3 point = player.cameraTransform.GetComponent<Camera>().WorldToViewportPoint(target.transform.position + Vector3.up * 1.3f);
            bool naTela = point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1;
            if (naTela && !targetMarker.gameObject.activeSelf) mira.rectTransform.localScale = Vector3.one * 1.6f;
            targetMarker.gameObject.SetActive(naTela);
            targetMarker.anchoredPosition = new Vector2(point.x * gameplayRoot.rect.width, point.y * gameplayRoot.rect.height);
            // a mira fecha sobre o alvo ao travar e depois respira devagar
            float s = Mathf.MoveTowards(mira.rectTransform.localScale.x, 1 + .04f * Mathf.Sin(Time.unscaledTime * 4), dt * 4);
            mira.rectTransform.localScale = Vector3.one * s;
            mira.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 1.3f) * 6);
            targetName.text = (target.Data ? target.Data.displayName : target.kind.ToString()).ToUpperInvariant();
            AtualizarBarra(alvoBarra, target.Health.Current / target.Health.maximum, dt);
        }

        /// <summary>
        /// Cena pela câmera e a interface por cima, em destino: a imagem da cena vai para o fundo do Canvas e uma câmera
        /// temporária desenha os dois juntos. Leva um quadro (o Canvas precisa se refazer no modo câmera); a cena é
        /// desenhada antes da espera. Quem chama deve ler o destino logo que a rotina terminar, no mesmo quadro.
        /// </summary>
        public System.Collections.IEnumerator Fotografar(Camera camera, RenderTexture destino)
        {
            var cena = RenderTexture.GetTemporary(destino.width, destino.height, 24, destino.format);
            var alvo = camera.targetTexture;
            camera.targetTexture = cena; camera.Render(); camera.targetTexture = alvo;
            if (!Canvas) { Graphics.Blit(cena, destino); RenderTexture.ReleaseTemporary(cena); yield break; }
            var fundo = Esticar(No(Canvas.transform, "Captura_Cena", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            fundo.SetAsFirstSibling();
            var imagem = fundo.gameObject.AddComponent<RawImage>(); imagem.texture = cena; imagem.raycastTarget = false;
            var fotografo = new GameObject("Captura_Interface").AddComponent<Camera>();
            fotografo.enabled = false; fotografo.cullingMask = 1 << CamadaUI;
            fotografo.clearFlags = CameraClearFlags.SolidColor; fotografo.backgroundColor = Color.black; fotografo.targetTexture = destino;
            Canvas.renderMode = RenderMode.ScreenSpaceCamera; Canvas.worldCamera = fotografo; Canvas.planeDistance = 1;
            yield return null;
            Canvas.ForceUpdateCanvases();
            AjustarEscala(); Canvas.ForceUpdateCanvases();
            fotografo.Render();
            // ler um pixel obriga a placa a terminar o desenho antes de o Canvas voltar à sobreposição
            // (trocar o modo antes descarta a geometria da interface e a imagem sai preta)
            var ativo = RenderTexture.active; RenderTexture.active = destino;
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); RenderTexture.active = ativo; Destroy(pixel);
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.worldCamera = null;
            // destruição adiada; leia o destino neste mesmo quadro (no seguinte o conteúdo pode se perder)
            Destroy(fundo.gameObject); Destroy(fotografo.gameObject);
            RenderTexture.ReleaseTemporary(cena);
        }

        public void ShowMoveList()
        {
            if (!manager) return;
            if (manager.IsPlaying) manager.TogglePause();
            if (manager.State != RunState.Paused) return;
            showControls = false; showMoves = true; RefreshModals();
        }

        public void MostrarControles()
        {
            if (!manager) return;
            if (manager.IsPlaying) manager.TogglePause();
            if (manager.State != RunState.Paused) return;
            showMoves = false; showControls = true; RefreshModals();
        }

        void RefreshModals()
        {
            if (!manager || !moveList) return;
            previousState = manager.State;
            bool escolha = manager.State == RunState.ChoosingBlessing;
            bool pause = manager.State == RunState.Paused || manager.State == RunState.Won || manager.State == RunState.Lost;
            var antes = modalAtual;
            choicePanel.SetActive(escolha);
            gameplayRoot.gameObject.SetActive(!pause && !escolha);
            modalBackdrop.SetActive(pause || escolha);
            pausePanel.SetActive(pause && !showMoves && !showControls);
            moveList.SetActive(showMoves && manager.State == RunState.Paused);
            controlsPanel.SetActive(showControls && !showMoves && manager.State == RunState.Paused);
            modalAtual = escolha ? choicePanel.GetComponent<CanvasGroup>() : moveList.activeSelf ? moveList.GetComponent<CanvasGroup>() :
                controlsPanel.activeSelf ? controlsPanel.GetComponent<CanvasGroup>() :
                pausePanel.activeSelf ? pausePanel.GetComponent<CanvasGroup>() : null;
            if (modalAtual && modalAtual != antes) { modalIdade = 0; modalAtual.alpha = 0; }
            if (escolha)
            {
                for (int i = 0; i < manager.Choices.Length && i < 3; i++)
                {
                    var b = manager.Choices[i];
                    Color color = DivineLore.Cor(b.god);
                    cardIcons[i].sprite = DivineLore.Simbolo(b.god); cardIcons[i].color = Color.Lerp(color, Color.white, .15f);
                    cardGlows[i].color = new Color(color.r, color.g, color.b, 0);
                    blessingGods[i].text = $"{DivineLore.Nome(b.god)}   ·   NÍVEL {manager.player.Favor.Level(b.god) + 1}";
                    blessingTitles[i].text = DivineLore.Poder(b.god);
                    blessingTitles[i].color = Color.Lerp(color, Color.white, .45f);
                    blessingPowers[i].text = DivineLore.Resumo(b.god);
                    blessingPowers[i].color = Color.Lerp(color, Color.white, .25f);
                    blessingDescriptions[i].text = b.description;
                    blessingCosts[i].text = $"Custo {b.cost:0} de favor   ·   recarga {b.cooldown:0} s";
                }
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(blessingButtons[0].gameObject);
            }
            if (pause)
            {
                bool venceu = manager.State == RunState.Won, perdeu = manager.State == RunState.Lost;
                pauseTitle.text = venceu ? "TROIA CAIU" : perdeu ? "FIM DA JORNADA" : "PAUSA";
                if (perdeu) pauseGradiente.Definir(new Color(1, .55f, .45f), new Color(.6f, .08f, .06f));
                else pauseGradiente.Definir(A.OuroClaro, A.Tom(A.Ouro, .75f));
                pauseSub.text = venceu ? "A cidade de Príamo arde. Seu nome será cantado pelos aedos." :
                    perdeu ? "Até os heróis caem diante das muralhas. Tente outra vez." :
                    manager.TrainingMode ? "Treino livre: os deuses observam." : $"Andar {manager.Floor} de {manager.config.floors.Length}  ·  a batalha espera.";
                estatValores[0].text = manager.Floor.ToString("00");
                estatValores[1].text = manager.Kills.ToString();
                estatValores[2].text = $"{Mathf.FloorToInt(manager.RemainingTime / 60):00}:{Mathf.FloorToInt(manager.RemainingTime % 60):00}";
                int pico = manager.player.Style.PeakRank;
                estatValores[3].text = StyleMeter.Labels[pico];
                estatValores[3].GetComponent<GradienteTexto>().Definir(Color.Lerp(RankColors[pico], Color.white, .5f), RankColors[pico]);
                continueButton.gameObject.SetActive(manager.State == RunState.Paused);
                checkpointButton.interactable = manager.HasCheckpoint;
                var selected = manager.player.Favor.Selected;
                bool canSwap = manager.TrainingMode && manager.State == RunState.Paused;
                trainingBlessing.interactable = canSwap;
                var visual = trainingBlessing.GetComponent<BotaoTroia>();
                visual.rotulo.text = canSwap ? $"BÊNÇÃO: {DivineLore.Poder(selected)}   ·   TROCAR [TAB]" : "TROCAR BÊNÇÃO  (no treino livre)";
                visual.corNormal = canSwap ? Color.Lerp(DivineLore.Cor(selected), Color.white, .45f) : A.Pergaminho;
                pauseRodape.enabled = manager.State == RunState.Paused;
                if (EventSystem.current && pausePanel.activeSelf)
                    EventSystem.current.SetSelectedGameObject(continueButton.gameObject.activeSelf ? continueButton.gameObject : checkpointButton.interactable ? checkpointButton.gameObject : null);
            }
            if (moveList.activeSelf) BuildMoveText();
        }

        void BuildMoveText()
        {
            var armas = manager.config.weapons;
            for (int i = 0; i < 3; i++)
            {
                bool existe = armas != null && i < armas.Length && armas[i];
                golpeNomes[i].transform.parent.gameObject.SetActive(existe);
                if (!existe) continue;
                var w = armas[i];
                golpeIcones[i].sprite = A.IconeArma(w.kind);
                golpeIcones[i].color = Color.Lerp(w.color, Color.white, .4f);
                golpeNomes[i].text = $"{i + 1}  ·  {w.displayName.ToUpperInvariant()}";
                golpeNomes[i].color = Color.Lerp(w.color, Color.white, .35f);
                golpeDescricoes[i].text = w.description;
                golpeComandos[i].text = ListaDeCombos(w);
            }
        }

        /// <summary>
        /// Todos os combos de uma arma, lidos da árvore (L = next, P = heavyNext): cada caminho que termina, cada
        /// finalizador e o pesado de abertura. Avanço, lançador e queda aparecem como golpes únicos.
        /// </summary>
        public static string ListaDeCombos(WeaponSO w)
        {
            var chao = new System.Collections.Generic.List<(string cmd, string nome)>();
            var ar = new System.Collections.Generic.List<(string cmd, string nome)>();
            var caminho = new System.Collections.Generic.HashSet<ComboSO>();
            void Ramo(ComboSO m, string cmd, System.Collections.Generic.List<(string, string)> saida)
            {
                if (!m || !caminho.Add(m)) return;
                bool leve = m.next && !caminho.Contains(m.next), pesado = m.heavyNext && !caminho.Contains(m.heavyNext);
                if ((!leve && !pesado) || m.finisher || cmd == "P") saida.Add((cmd, m.displayName));
                if (leve) Ramo(m.next, cmd + "  L", saida);
                if (pesado) Ramo(m.heavyNext, cmd + "  P", saida);
                caminho.Remove(m);
            }
            Ramo(w.light, "L", chao);
            Ramo(w.heavy, "P", chao);
            Ramo(w.airLight, "L", ar);
            int Passos(string c) => c.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
            chao.Sort((a, b) => a.cmd[0] != b.cmd[0] ? a.cmd[0].CompareTo(b.cmd[0]) : Passos(a.cmd).CompareTo(Passos(b.cmd)));
            ar.Sort((a, b) => Passos(a.cmd).CompareTo(Passos(b.cmd)));
            var sb = new System.Text.StringBuilder();
            void Linha(string cmd, string nome) => sb.Append("<color=#F6DB96>").Append(cmd).Append("</color>     ").Append(nome).Append('\n');
            foreach (var (cmd, nome) in chao) Linha(cmd, nome);
            if (w.launcher) Linha("C", w.launcher.displayName);
            if (w.dash) Linha("E", w.dash.displayName + "  (continua com L)");
            sb.Append("<color=#A99D8C>NO AR</color>\n");
            foreach (var (cmd, nome) in ar) Linha(cmd, nome);
            if (w.slam) Linha("P", w.slam.displayName);
            if (w.airDash) Linha("E", w.airDash.displayName);
            return sb.ToString().TrimEnd('\n');
        }
    }

    /// <summary>
    /// Mantém um contêiner cobrindo o Canvas inteiro, mas desenhado em escala menor: os filhos presos aos cantos
    /// continuam nos cantos e encolhem por igual. Recalcula quando o Canvas muda de tamanho.
    /// </summary>
    public class EscalaContida : UIBehaviour
    {
        RectTransform alvo;
        float escala = 1;

        public void Definir(RectTransform contido, float fator) { alvo = contido; escala = fator; Aplicar(alvo, (RectTransform)transform, escala); }

        public static void Aplicar(RectTransform contido, RectTransform pai, float fator)
        {
            if (!contido || !pai) return;
            contido.anchorMin = contido.anchorMax = contido.pivot = new Vector2(.5f, .5f);
            contido.anchoredPosition = Vector2.zero;
            contido.localScale = Vector3.one * fator;
            contido.sizeDelta = pai.rect.size / fator;
        }

        protected override void OnRectTransformDimensionsChange() => Aplicar(alvo, (RectTransform)transform, escala);
    }
}
