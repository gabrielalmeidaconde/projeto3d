using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Troia
{
    // Screen-space camera Canvas renders with the game camera, including off-screen QA captures.
    public class GameplayHUD : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public string DisplayedRank => rank ? rank.text : "";
        public bool MoveListVisible => moveList && moveList.activeSelf;
        static readonly Color Ink = new Color(.035f, .045f, .07f, .94f);
        static readonly Color Muted = new Color(.63f, .68f, .76f);
        static readonly Color Gold = new Color(1, .68f, .25f);
        static readonly Color[] RankColors = { new Color(.65f,.7f,.78f), new Color(.35f,.8f,.8f), new Color(.3f,.78f,1),
            new Color(.55f,.5f,1), new Color(1,.7f,.2f), new Color(1,.4f,.16f), new Color(1,.22f,.45f) };
        Font font;
        RectTransform root, gameplayRoot, targetMarker;
        GameObject modalBackdrop;
        Text healthText, favorText, floorText, clock, message, rank, rankTitle, styleDetail, chain, moveName, weaponText, weaponDetail, cooldowns, targetName;
        Image healthBar, favorBar, styleBar, targetBar;
        GameObject choicePanel, pausePanel, moveList;
        Text pauseTitle, results, moves;
        Button continueButton, checkpointButton;
        readonly Button[] blessingButtons = new Button[3];
        readonly Text[] blessingTitles = new Text[3], blessingDescriptions = new Text[3];
        RunState previousState = (RunState)(-1);
        bool showMoves;
        GameManager manager;
        // bênçãos: painel do deus selecionado, faixa com os seis deuses, nome do poder ao conjurar e clarão de tela
        DivineFavor favor;
        Image flash, aura, blessingIcon, blessingBar;
        Text blessingName, blessingState, banner;
        RectTransform chipMarker;
        readonly Image[] godChips = new Image[6];
        readonly Image[] cardIcons = new Image[3];
        readonly Text[] blessingPowers = new Text[3];
        Button trainingBlessing;
        float bannerAge = 9, flashAlpha, auraAlpha;
        Color flashColor = Color.white, auraColor = Color.white;
        public string DisplayedBlessing => blessingName ? blessingName.text : "";

        void Start()
        {
            manager = GetComponent<GameManager>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("HUD_Iliada", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas = canvasObject.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera; Canvas.worldCamera = Camera.main;
            Canvas.planeDistance = .5f; Canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            root = canvasObject.GetComponent<RectTransform>();
            var layer = new GameObject("Gameplay", typeof(RectTransform)); layer.transform.SetParent(root, false);
            gameplayRoot = layer.GetComponent<RectTransform>();
            gameplayRoot.anchorMin = Vector2.zero; gameplayRoot.anchorMax = Vector2.one;
            gameplayRoot.offsetMin = gameplayRoot.offsetMax = Vector2.zero;
            if (!FindFirstObjectByType<EventSystem>())
            {
                var eventObject = new GameObject("Gameplay_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
                eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            // por baixo dos painéis: moldura colorida enquanto um poder dura e clarão quando ele cai
            aura = Cover(gameplayRoot, "Aura_divina"); aura.sprite = DivineLore.Vinheta(); aura.color = Color.clear;
            flash = Cover(gameplayRoot, "Clarao_divino"); flash.color = Color.clear;
            BuildStatus(); BuildStyle(); BuildArsenal(); BuildBlessing();
            var shade = Panel(root, "Fundo_Modal", Vector2.zero, Vector2.zero, Vector2.zero, new Color(.01f, .015f, .03f, .76f));
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().raycastTarget = true; modalBackdrop = shade.gameObject; modalBackdrop.SetActive(false);
            BuildModals();
            var footer = Panel(gameplayRoot, "Atalhos", new Vector2(.5f, 0), new Vector2(-500, 40), new Vector2(1000, 26), new Color(.025f, .035f, .055f, .86f));
            Label(footer, "WASD mover   ESPAÇO pulo duplo   CTRL esquiva   E avanço/arpão   R dardos   F foco   1/2/3 armas   Q bênção   TAB troca de deus   H golpes   F2 treino", 12, Color.white, new Rect(10, 4, 980, 22), TextAnchor.MiddleCenter);
            var marker = Panel(gameplayRoot, "Alvo", Vector2.zero, Vector2.zero, new Vector2(110, 80), Color.clear);
            targetMarker = marker; targetMarker.pivot = new Vector2(.5f, .5f);
            Label(marker, "[    ]", 36, Gold, new Rect(0, 0, 110, 48), TextAnchor.MiddleCenter);
            targetName = Label(marker, "", 12, Color.white, new Rect(-30, 49, 170, 20), TextAnchor.MiddleCenter);
            targetBar = Bar(marker, new Rect(20, 72, 70, 4), Gold);
            banner = Label(gameplayRoot, "", 34, Color.white, new Rect(0, 0, 760, 52), TextAnchor.MiddleCenter);
            Place(banner.rectTransform, new Vector2(.5f, 1), new Vector2(-380, -168), new Vector2(760, 52));
            banner.rectTransform.pivot = new Vector2(.5f, .5f); banner.rectTransform.anchoredPosition = new Vector2(0, -194);
            banner.fontStyle = FontStyle.Bold; banner.color = Color.clear;
            var sombra = banner.gameObject.AddComponent<Outline>(); sombra.effectColor = new Color(0, 0, 0, .85f); sombra.effectDistance = new Vector2(2, -2);
            favor = manager.player.Favor;
            favor.CastStarted += OnCast; favor.Unleashed += OnUnleashed;
        }
        void OnCast(God god, int level) { bannerAge = 0; banner.text = DivineLore.Poder(god); banner.color = Color.Lerp(DivineLore.Cor(god), Color.white, .35f); }
        void OnUnleashed(God god, int level, Vector3 center, float radius)
        {
            flashColor = DivineLore.Claro(god);
            flashAlpha = Mathf.Max(flashAlpha, god == God.Zeus ? .26f : god == God.Poseidon || god == God.Ares ? .16f : .1f);
        }
        void OnDestroy() { if (favor) { favor.CastStarted -= OnCast; favor.Unleashed -= OnUnleashed; } }
        void BuildBlessing()
        {
            var panel = Panel(gameplayRoot, "Bencao", new Vector2(1, 0), new Vector2(-374, 166), new Vector2(350, 132), Ink);
            Label(panel, "BÊNÇÃO  /  Q usar  ·  TAB trocar", 11, Muted, new Rect(16, 10, 320, 20));
            blessingIcon = Icon(panel, new Rect(16, 33, 46, 46), DivineLore.Simbolo(God.Zeus), Color.white);
            blessingName = Label(panel, "", 21, Gold, new Rect(72, 29, 266, 30));
            blessingName.fontStyle = FontStyle.Bold;
            blessingState = Label(panel, "", 12, Color.white, new Rect(72, 59, 266, 18));
            blessingBar = Bar(panel, new Rect(72, 81, 262, 4), Gold);
            for (int i = 0; i < 6; i++)
                godChips[i] = Icon(panel, new Rect(16 + i * 32, 95, 24, 24), DivineLore.Simbolo((God)i), DivineLore.Cor((God)i));
            chipMarker = Panel(panel, "Selecionado", Vector2.up, new Vector2(16, -122), new Vector2(24, 2), Color.white);
            Label(panel, "4 a 9: escolha direta", 10, Muted, new Rect(206, 100, 130, 18), TextAnchor.MiddleRight);
        }
        void UpdateBlessing(PlayerCombat p)
        {
            var f = p.Favor; God god = f.Selected; int level = f.Level(god);
            bool owned = level > 0;
            Color color = DivineLore.Cor(god);
            blessingIcon.sprite = DivineLore.Simbolo(god); blessingIcon.color = owned ? color : new Color(1, 1, 1, .14f);
            blessingName.text = owned ? DivineLore.Poder(god) : "SEM BÊNÇÃO";
            blessingName.color = owned ? Color.Lerp(color, Color.white, .25f) : Muted;
            float cooldown = f.CooldownRemaining, cost = f.SelectedCost;
            bool ready = owned && cooldown <= 0 && f.Amount >= cost && !f.Casting;
            if (!owned) blessingState.text = "Vença 5 andares para receber a primeira.";
            else if (god == God.Ares && f.FuryRemaining > 0) blessingState.text = $"Nv.{level}  ·  FÚRIA ATIVA  {f.FuryRemaining:0.0}s";
            else if (god == God.Athena && f.ShieldRemaining > 0) blessingState.text = $"Nv.{level}  ·  ÉGIDE ATIVA  {f.ShieldRemaining:0.0}s";
            else if (cooldown > 0) blessingState.text = $"Nv.{level}  ·  RECARGA  {cooldown:0.0}s";
            else if (f.Amount < cost) blessingState.text = $"Nv.{level}  ·  FAVOR  {f.Amount:0} / {cost:0}";
            else blessingState.text = $"Nv.{level}  ·  PRONTA  ·  {DivineLore.Resumo(god)}";
            blessingState.color = ready ? Color.white : Muted;
            SetBar(blessingBar, owned ? 1 - f.CooldownFraction : 0);
            blessingBar.color = ready ? color : new Color(color.r, color.g, color.b, .45f);
            for (int i = 0; i < 6; i++)
            {
                bool has = f.Level((God)i) > 0, selected = owned && i == (int)god;
                Color c = DivineLore.Cor((God)i); c.a = selected ? 1 : has ? .42f : .1f;
                godChips[i].color = c; godChips[i].rectTransform.localScale = Vector3.one * (selected ? 1.18f : 1);
            }
            chipMarker.gameObject.SetActive(owned);
            chipMarker.anchoredPosition = new Vector2(16 + (int)god * 32, -122); chipMarker.GetComponent<Image>().color = color;
            // nome do poder: entra grande, assenta e some
            bannerAge += Time.unscaledDeltaTime;
            float show = Mathf.Clamp01(bannerAge / .12f) * (1 - Mathf.Clamp01((bannerAge - .9f) / .45f));
            var bc = banner.color; bc.a = show; banner.color = bc;
            banner.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1, Mathf.Clamp01(bannerAge / .16f));
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0, Time.unscaledDeltaTime * 2.4f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
            // moldura na cor do poder que está durando
            float want = 0; Color tint = auraColor;
            if (f.ShieldRemaining > 0) { want = .26f * Mathf.Clamp01(f.ShieldRemaining / .5f); tint = DivineLore.Cor(God.Athena); }
            else if (f.FuryRemaining > 0) { want = (.3f + .06f * Mathf.Sin(Time.unscaledTime * 6)) * Mathf.Clamp01(f.FuryRemaining / .5f); tint = DivineLore.Cor(God.Ares); }
            else if (f.BoostRemaining > 0) { want = .16f * Mathf.Clamp01(f.BoostRemaining / .4f); tint = DivineLore.Cor(God.Hermes); }
            auraAlpha = Mathf.MoveTowards(auraAlpha, want, Time.unscaledDeltaTime * 2.5f); auraColor = tint;
            aura.color = new Color(tint.r, tint.g, tint.b, auraAlpha);
        }
        Image Cover(Transform parent, string name)
        {
            var rect = Panel(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, Color.clear);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect.GetComponent<Image>();
        }
        Image Icon(Transform parent, Rect bounds, Sprite sprite, Color color)
        {
            var rect = Panel(parent, "Simbolo", Vector2.up, new Vector2(bounds.x + bounds.width * .5f, -bounds.y - bounds.height * .5f), bounds.size, color);
            rect.pivot = new Vector2(.5f, .5f);
            var image = rect.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; return image;
        }
        void BuildStatus()
        {
            var status = Panel(gameplayRoot, "Estado", Vector2.up, new Vector2(24, -24), new Vector2(304, 114), Ink);
            Label(status, "ILÍADA  /  NOITE DA INVASÃO", 12, Gold, new Rect(16, 10, 280, 20));
            healthBar = Bar(status, new Rect(16, 40, 272, 9), new Color(.37f, .85f, .57f));
            healthText = Label(status, "", 12, Color.white, new Rect(16, 51, 272, 18));
            favorBar = Bar(status, new Rect(16, 77, 272, 5), new Color(.3f, .73f, 1));
            favorText = Label(status, "", 11, Muted, new Rect(16, 87, 272, 20));
            var run = Panel(gameplayRoot, "Andar", new Vector2(.5f, 1), new Vector2(-130, -24), new Vector2(260, 78), Ink);
            floorText = Label(run, "", 13, Gold, new Rect(10, 8, 240, 20), TextAnchor.MiddleCenter);
            clock = Label(run, "", 31, Color.white, new Rect(10, 29, 240, 40), TextAnchor.MiddleCenter);
            message = Label(gameplayRoot, "", 13, Color.white, new Rect(0, 0, 650, 44), TextAnchor.MiddleCenter);
            Place(message.rectTransform, new Vector2(.5f, 1), new Vector2(-325, -110), new Vector2(650, 44));
        }
        void BuildStyle()
        {
            var panel = Panel(gameplayRoot, "Estilo", Vector2.one, new Vector2(-292, -24), new Vector2(268, 306), Ink);
            Label(panel, "ESTILO DE COMBATE", 11, Muted, new Rect(16, 12, 236, 20), TextAnchor.MiddleRight);
            rank = Label(panel, "D", 96, RankColors[0], new Rect(4, 29, 248, 118), TextAnchor.MiddleRight);
            rank.fontStyle = FontStyle.Bold;
            rank.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, .7f);
            rankTitle = Label(panel, "", 16, Gold, new Rect(16, 145, 236, 25), TextAnchor.MiddleRight);
            styleBar = Bar(panel, new Rect(16, 183, 236, 5), Gold);
            styleDetail = Label(panel, "", 12, Muted, new Rect(16, 196, 236, 23), TextAnchor.MiddleRight);
            chain = Label(panel, "", 22, Color.white, new Rect(16, 227, 236, 32), TextAnchor.MiddleRight);
            moveName = Label(panel, "", 12, Muted, new Rect(16, 266, 236, 28), TextAnchor.MiddleRight);
        }
        void BuildArsenal()
        {
            var panel = Panel(gameplayRoot, "Arsenal", Vector2.zero, new Vector2(24, 166), new Vector2(350, 132), Ink);
            Label(panel, "ARSENAL  /  1 · 2 · 3", 11, Muted, new Rect(16, 10, 320, 20));
            weaponText = Label(panel, "", 24, Gold, new Rect(16, 30, 320, 32));
            weaponText.fontStyle = FontStyle.Bold;
            weaponDetail = Label(panel, "", 12, Color.white, new Rect(16, 64, 320, 22));
            cooldowns = Label(panel, "", 11, Muted, new Rect(16, 87, 320, 40));
        }
        void BuildModals()
        {
            var choice = Panel(root, "Bencaos", new Vector2(.5f, .5f), new Vector2(-465, 190), new Vector2(930, 380), Ink);
            choicePanel = choice.gameObject;
            Label(choice, "ESCOLHA UMA BÊNÇÃO", 28, Gold, new Rect(24, 20, 882, 44), TextAnchor.MiddleCenter);
            Label(choice, "1 / 2 / 3  ou  direcional + A", 13, Muted, new Rect(24, 68, 882, 24), TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var card = Panel(choice, "Deus_" + i, Vector2.up, new Vector2(24 + i * 298, -116), new Vector2(286, 232), new Color(.09f, .11f, .16f));
                cardIcons[i] = Icon(card, new Rect(16, 14, 46, 46), DivineLore.Simbolo(God.Zeus), Color.white);
                blessingTitles[i] = Label(card, "", 20, Gold, new Rect(72, 10, 200, 28));
                blessingTitles[i].fontStyle = FontStyle.Bold;
                blessingPowers[i] = Label(card, "", 12, Muted, new Rect(72, 39, 200, 20));
                blessingDescriptions[i] = Label(card, "", 14, Color.white, new Rect(16, 72, 254, 100));
                blessingButtons[i] = Button(card, "Escolher", new Rect(16, 178, 254, 38), () => manager.ChooseBlessing(index));
            }
            choicePanel.SetActive(false);
            var pause = Panel(root, "Pausa", new Vector2(.5f, .5f), new Vector2(-280, 286), new Vector2(560, 572), Ink);
            pausePanel = pause.gameObject;
            pauseTitle = Label(pause, "", 28, Gold, new Rect(24, 20, 512, 48), TextAnchor.MiddleCenter);
            results = Label(pause, "", 15, Muted, new Rect(32, 78, 496, 75), TextAnchor.MiddleCenter);
            continueButton = Button(pause, "CONTINUAR", new Rect(48, 168, 464, 42), () => manager.TogglePause());
            Button(pause, "LISTA DE GOLPES  [H]", new Rect(48, 220, 464, 42), ShowMoveList);
            Button(pause, "TREINO LIVRE  [F2]", new Rect(48, 272, 464, 42), () => manager.BeginTraining());
            trainingBlessing = Button(pause, "TROCAR BÊNÇÃO", new Rect(48, 324, 464, 42), () => { manager.player.Favor.Cycle(); RefreshModals(); });
            Button(pause, "REINICIAR PARTIDA", new Rect(48, 376, 464, 42), () => manager.Restart(false));
            checkpointButton = Button(pause, "RETOMAR CHECKPOINT", new Rect(48, 428, 464, 42), () => manager.Restart(true));
            Label(pause, "No treino: alvos resistentes, tempo livre, favor ilimitado e todas as bênçãos.\nQ usa a bênção, TAB troca de deus, 4 a 9 escolhem direto.", 12, Muted, new Rect(32, 488, 496, 52), TextAnchor.MiddleCenter);
            pausePanel.SetActive(false);
            var list = Panel(root, "Golpes", new Vector2(.5f, .5f), new Vector2(-470, 270), new Vector2(940, 540), Ink);
            moveList = list.gameObject;
            Label(list, "ARSENAL / LISTA DE GOLPES", 25, Gold, new Rect(24, 18, 892, 44), TextAnchor.MiddleCenter);
            moves = Label(list, "", 15, Color.white, new Rect(32, 76, 876, 384));
            Button(list, "VOLTAR  [H]", new Rect(290, 466, 360, 42), () => { showMoves = false; RefreshModals(); });
            moveList.SetActive(false);
        }
        void Update()
        {
            if (!manager || !manager.player || !Canvas) return;
            if (Keyboard.current?.hKey.wasPressedThisFrame ?? false)
            {
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
            var p = manager.player; var style = p.Style;
            SetBar(healthBar, p.Health.Current / p.Health.maximum);
            healthText.text = $"VIDA  {p.Health.Current:0} / {p.Health.maximum:0}";
            SetBar(favorBar, p.Favor.Amount / 100);
            favorText.text = $"FAVOR DIVINO  {p.Favor.Amount:0} / 100";
            floorText.text = manager.TrainingMode ? "TREINO LIVRE" : $"ANDAR {manager.Floor:00} / {manager.config.floors.Length}";
            clock.text = manager.TrainingMode ? "SEM LIMITE" : $"{Mathf.FloorToInt(manager.RemainingTime / 60):00}:{Mathf.FloorToInt(manager.RemainingTime % 60):00}";
            clock.color = manager.RemainingTime < 30 && !manager.TrainingMode ? new Color(1,.3f,.3f) : Color.white;
            message.text = manager.Message;
            if (manager.IsPlaying && !manager.FloorActive && !manager.TrainingMode)
                message.text += $"  /  {Vector3.Distance(p.transform.position, manager.Destination):0} m";
            rank.text = StyleMeter.Labels[style.Rank]; rank.color = RankColors[style.Rank];
            rank.transform.localScale = Vector3.one * (1 + style.RankFlash * .08f);
            rankTitle.text = StyleMeter.Titles[style.Rank]; rankTitle.color = rank.color;
            SetBar(styleBar, style.RankProgress); styleBar.color = rank.color;
            styleDetail.text = style.Freshness == 0 && style.ChainHits > 0 ? "REPITIÇÃO / VARIE OS GOLPES" : $"FAVOR x{style.Multiplier:0.00}    ESTILO +{style.LastGain:0}";
            chain.text = style.ChainHits > 0 ? $"{style.ChainHits} HITS   /   {style.ChainDamage:0}" : "CONECTE SEUS ATAQUES";
            chain.fontSize = style.ChainHits > 0 ? 22 : 13;
            moveName.text = style.LastMove ?? "";
            if (p.Weapon)
            {
                weaponText.text = p.Weapon.displayName; weaponText.color = p.Weapon.color;
                weaponDetail.text = p.CurrentAttack ? (string.IsNullOrEmpty(p.CurrentAttack.displayName) ? p.CurrentAttack.id : p.CurrentAttack.displayName) :
                    p.Weapon.kind == WeaponKind.Spear ? "CONTROLE / AVANÇO" : p.Weapon.kind == WeaponKind.Shield ? "IMPACTO / ARPÃO" : "PRECISÃO / ENCADEAMENTO";
                cooldowns.text = p.PerfectDodgeFlash > 0 ? "ESQUIVA PERFEITA / +FAVOR" : p.PendingWeapon >= 0 ? "TROCA PREPARADA / FINALIZANDO IMPACTO" :
                    $"E {(p.Weapon.pullsEnemy ? "ARPÃO" : "AVANÇAR")} {(p.ApproachCooldown > 0 ? p.ApproachCooldown.ToString("0.0") + "s" : "OK")} / R {(p.ShotCooldown > .3f ? "ESFRIANDO" : "DARDOS")}\n" +
                    (p.RecoveryRemaining > 0 ? $"RECUPERAÇÃO {p.RecoveryRemaining:0.0}s" : p.MoveCooldown(p.Weapon.heavy) > 0 ? $"PESADO {p.MoveCooldown(p.Weapon.heavy):0.0}s" : "PESADO PRONTO");
            }
            if (previousState != manager.State) { if (manager.State != RunState.Paused) showMoves = false; RefreshModals(); previousState = manager.State; }
            UpdateTarget(p); UpdateBlessing(p);
        }
        void UpdateTarget(PlayerCombat player)
        {
            bool visible = player.Targeting.IsLocked && player.cameraTransform && manager.IsPlaying;
            if (!visible) { targetMarker.gameObject.SetActive(false); return; }
            var target = player.Targeting.LockedTarget;
            Vector3 point = player.cameraTransform.GetComponent<Camera>().WorldToViewportPoint(target.transform.position + Vector3.up * 1.3f);
            targetMarker.gameObject.SetActive(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1);
            targetMarker.anchoredPosition = new Vector2(point.x * root.rect.width, point.y * root.rect.height);
            targetName.text = target.Data ? target.Data.displayName : target.kind.ToString().ToUpper();
            SetBar(targetBar, target.Health.Current / target.Health.maximum);
        }
        public void ShowMoveList()
        {
            if (!manager) return;
            if (manager.IsPlaying) manager.TogglePause();
            if (manager.State != RunState.Paused) return;
            showMoves = true; RefreshModals();
        }
        void RefreshModals()
        {
            if (!manager || !moveList) return;
            previousState = manager.State;
            choicePanel.SetActive(manager.State == RunState.ChoosingBlessing);
            bool pause = manager.State == RunState.Paused || manager.State == RunState.Won || manager.State == RunState.Lost;
            gameplayRoot.gameObject.SetActive(!pause && manager.State != RunState.ChoosingBlessing);
            modalBackdrop.SetActive(pause || manager.State == RunState.ChoosingBlessing);
            pausePanel.SetActive(pause && !showMoves); moveList.SetActive(showMoves && manager.State == RunState.Paused);
            if (choicePanel.activeSelf)
            {
                for (int i = 0; i < manager.Choices.Length; i++)
                {
                    var b = manager.Choices[i];
                    Color color = DivineLore.Cor(b.god);
                    cardIcons[i].sprite = DivineLore.Simbolo(b.god); cardIcons[i].color = color;
                    blessingTitles[i].text = $"{i + 1}. {b.title.ToUpperInvariant()}  /  Nv.{manager.player.Favor.Level(b.god) + 1}";
                    blessingTitles[i].color = Color.Lerp(color, Color.white, .2f);
                    blessingPowers[i].text = DivineLore.Poder(b.god);
                    blessingDescriptions[i].text = b.description + $"\nCusto: {b.cost:0} favor  ·  recarga {b.cooldown:0}s";
                }
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(blessingButtons[0].gameObject);
            }
            if (pause)
            {
                pauseTitle.text = manager.State == RunState.Paused ? "PAUSADO" : manager.State == RunState.Won ? "TROIA CAIU" : "FIM DA JORNADA";
                results.text = $"ANDAR {manager.Floor}   /   {manager.Kills} ELIMINAÇÕES   /   {manager.RemainingTime:0}s\nMELHOR ESTILO: {StyleMeter.Labels[manager.player.Style.PeakRank]}";
                continueButton.gameObject.SetActive(manager.State == RunState.Paused);
                checkpointButton.interactable = manager.HasCheckpoint;
                var selected = manager.player.Favor.Selected;
                bool canSwap = manager.TrainingMode && manager.State == RunState.Paused;
                trainingBlessing.interactable = canSwap;
                var swapLabel = trainingBlessing.GetComponentInChildren<Text>();
                swapLabel.text = canSwap ? $"BÊNÇÃO: {DivineLore.Poder(selected)}   »   TROCAR  [TAB]" : "TROCAR BÊNÇÃO  (no treino livre)";
                swapLabel.color = canSwap ? Color.Lerp(DivineLore.Cor(selected), Color.white, .3f) : Muted;
                if (EventSystem.current && pausePanel.activeSelf) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
            }
            if (moveList.activeSelf) BuildMoveText();
        }
        void BuildMoveText()
        {
            moves.text = "L = LEVE / P = PESADO     |     TROQUE 1 / 2 / 3 DURANTE O COMBO\n\n";
            foreach (var weapon in manager.config.weapons)
                moves.text += $"{weapon.displayName}\n{weapon.light.command}   /   P P\n{weapon.description}\n\n";
            moves.text += "C / SEGURAR P: lançador / ESPAÇO duas vezes: pulo duplo\nE: avanço (espada/lança) ou arpão (escudo); blindados exigem quebra de guarda.\nR: dardos, 3 arremessos e 0,95s para esfriar. F: foco / T: próximo alvo.\nCTRL cancela; recargas persistem. Alterne ACERTOS; repetir não pontua.\nSofrer dano derruba dois níveis de estilo. Pesados e quedas exigem recuperação.\nQ: bênção do deus selecionado. TAB troca de deus (SHIFT+TAB volta); 4 a 9 escolhem direto.";
        }
        RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); Place(rect, anchor, position, size);
            go.GetComponent<Image>().color = color; go.GetComponent<Image>().raycastTarget = false; return rect;
        }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        Text Label(Transform parent, string text, int size, Color color, Rect bounds, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>(); label.font = font; label.fontSize = size; label.color = color; label.text = text;
            label.alignment = alignment; label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            Place(label.rectTransform, Vector2.up, new Vector2(bounds.x, -bounds.y), new Vector2(bounds.width, bounds.height)); return label;
        }
        Image Bar(Transform parent, Rect bounds, Color color)
        {
            var background = Panel(parent, "Barra", Vector2.up, new Vector2(bounds.x, -bounds.y), bounds.size, new Color(1,1,1,.13f));
            var fill = Panel(background, "Preenchimento", Vector2.zero, Vector2.zero, bounds.size, color);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero; return fill.GetComponent<Image>();
        }
        static void SetBar(Image image, float value) { if (image) image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1); }
        Button Button(Transform parent, string text, Rect bounds, UnityEngine.Events.UnityAction action)
        {
            var panel = Panel(parent, text, Vector2.up, new Vector2(bounds.x, -bounds.y), bounds.size, new Color(.15f,.18f,.24f));
            panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<Image>();
            panel.GetComponent<Image>().color = Color.white;
            var colors = button.colors; colors.normalColor = new Color(.15f,.18f,.24f);
            colors.highlightedColor = new Color(.46f,.3f,.1f); colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.65f,.4f,.12f); colors.disabledColor = new Color(.07f,.08f,.1f,.6f); button.colors = colors;
            button.onClick.AddListener(action);
            Label(panel, text, 14, Color.white, new Rect(4, 4, bounds.width - 8, bounds.height - 8), TextAnchor.MiddleCenter);
            return button;
        }
    }
}
