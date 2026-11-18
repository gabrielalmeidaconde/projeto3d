using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Troia
{
    // Victory/defeat summary and credits, built with the HUD's own helpers.
    public partial class GameplayHUD
    {
        static readonly Color Defeat = new Color(.9f, .32f, .28f);
        static readonly string[] EndRows =
            { "ANDARES CONCLUÍDOS", "ELIMINAÇÕES", "TEMPO DE PARTIDA", "TEMPO RESTANTE", "MAIOR SEQUÊNCIA", "MELHOR ESTILO" };
        const string Credits =
            "<b>ILÍADA  /  NOITE DA INVASÃO</b>\nInspirado na Ilíada, de Homero\n\n" +
            "<color=#FFAD40>EQUIPE</color>\nGabriel Almeida Conde\nYuri Henrique\nLuísa Carvalho Farah\n\n" +
            "Projeto de disciplina  /  Insper, 2026\n\n" +
            "<color=#FFAD40>RECURSOS DE TERCEIROS</color>\nCorpo e esqueleto: MakeHuman (CC0)\nTexturas escaneadas: Poly Haven (CC0)";

        GameObject endPanel, creditsPanel;
        Text endTitle, endSubtitle, gradeText, gradeCaption, kleosText;
        readonly Text[] endLabels = new Text[EndRows.Length], endValues = new Text[EndRows.Length];
        Button endRetryButton, endCheckpointButton;
        float endShownAt;

        void BuildEndScreen()
        {
            var end = Panel(root, "Fim", new Vector2(.5f, .5f), new Vector2(-400, 280), new Vector2(800, 560), Ink);
            endPanel = end.gameObject;
            endTitle = Label(end, "", 40, Gold, new Rect(24, 22, 752, 56), TextAnchor.MiddleCenter);
            endTitle.fontStyle = FontStyle.Bold;
            endSubtitle = Label(end, "", 15, Muted, new Rect(24, 80, 752, 26), TextAnchor.MiddleCenter);
            for (int i = 0; i < EndRows.Length; i++)
            {
                float y = 136 + i * 42;
                endLabels[i] = Label(end, EndRows[i], 14, Muted, new Rect(48, y, 250, 30), TextAnchor.MiddleLeft);
                endValues[i] = Label(end, "", 22, Color.white, new Rect(298, y, 160, 30), TextAnchor.MiddleRight);
            }
            var gradeBox = Panel(end, "Nota", Vector2.up, new Vector2(500, -136), new Vector2(252, 246), new Color(.09f, .11f, .16f));
            Label(gradeBox, "NOTA DA JORNADA", 12, Muted, new Rect(0, 12, 252, 20), TextAnchor.MiddleCenter);
            gradeText = Label(gradeBox, "", 110, RankColors[0], new Rect(0, 30, 252, 130), TextAnchor.MiddleCenter);
            gradeText.fontStyle = FontStyle.Bold;
            gradeText.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, .7f);
            gradeCaption = Label(gradeBox, "", 15, Gold, new Rect(0, 160, 252, 24), TextAnchor.MiddleCenter);
            Label(gradeBox, "KLEOS  /  GLÓRIA", 11, Muted, new Rect(0, 192, 252, 18), TextAnchor.MiddleCenter);
            kleosText = Label(gradeBox, "", 22, Color.white, new Rect(0, 210, 252, 30), TextAnchor.MiddleCenter);
            endRetryButton = Button(end, "JOGAR NOVAMENTE  [R]", new Rect(48, 420, 340, 44), () => manager.Restart(false));
            endCheckpointButton = Button(end, "RETOMAR CHECKPOINT", new Rect(412, 420, 340, 44), () => manager.Restart(true));
            Button(end, "CRÉDITOS", new Rect(48, 476, 704, 44), () => ShowCredits(true));
            endPanel.SetActive(false);

            var credits = Panel(root, "Creditos", new Vector2(.5f, .5f), new Vector2(-400, 280), new Vector2(800, 560), Ink);
            creditsPanel = credits.gameObject;
            Label(credits, "CRÉDITOS", 32, Gold, new Rect(24, 24, 752, 48), TextAnchor.MiddleCenter);
            Label(credits, Credits, 16, Color.white, new Rect(64, 90, 672, 380), TextAnchor.UpperCenter);
            Button(credits, "VOLTAR", new Rect(230, 490, 340, 44), () => ShowCredits(false));
            creditsPanel.SetActive(false);
        }

        void ShowEnd()
        {
            bool won = manager.State == RunState.Won;
            endTitle.text = won ? "TROIA CAIU" : "FIM DA JORNADA";
            endTitle.color = won ? Gold : Defeat;
            endSubtitle.text = manager.Message;
            endCheckpointButton.gameObject.SetActive(!won && manager.HasCheckpoint);
            endShownAt = Time.unscaledTime;
            UpdateEndScreen();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(endRetryButton.gameObject);
        }

        void ShowCredits(bool show)
        {
            creditsPanel.SetActive(show); endPanel.SetActive(!show);
            if (!show && EventSystem.current) EventSystem.current.SetSelectedGameObject(endRetryButton.gameObject);
        }

        void UpdateEndScreen()
        {
            if (!endPanel || !endPanel.activeSelf) return;
            var style = manager.player.Style;
            float elapsed = Time.unscaledTime - endShownAt;
            // Rows count up one after another, then the grade lands.
            for (int i = 0; i < EndRows.Length; i++)
            {
                float t = Mathf.Clamp01((elapsed - .25f - i * .18f) / .5f);
                endLabels[i].color = new Color(Muted.r, Muted.g, Muted.b, t);
                Color value = i == EndRows.Length - 1 ? RankColors[style.PeakRank] : Color.white;
                value.a = t; endValues[i].color = value;
                endValues[i].text = RowValue(i, t, style);
            }
            float g = Mathf.Clamp01((elapsed - 1.6f) / .35f);
            int grade = manager.FinalGrade;
            gradeText.text = g > 0 ? StyleMeter.Labels[grade] : "";
            gradeText.color = RankColors[grade];
            gradeText.transform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1, g);
            gradeCaption.text = g > 0 ? StyleMeter.Titles[grade] : "";
            kleosText.text = Mathf.RoundToInt(manager.Kleos * Mathf.Clamp01((elapsed - .25f) / 1.6f)).ToString("N0");
        }

        string RowValue(int row, float t, StyleMeter style)
        {
            switch (row)
            {
                case 0: return $"{Mathf.RoundToInt(manager.FloorsCleared * t)} / {manager.config.floors.Length}";
                case 1: return Mathf.RoundToInt(manager.Kills * t).ToString();
                case 2: return Clock(manager.RunTime * t);
                case 3: return Clock(manager.RemainingTime * t);
                case 4: return $"{Mathf.RoundToInt(manager.BestChain * t)} hits";
                default: return t > 0 ? StyleMeter.Labels[style.PeakRank] : "";
            }
        }

        static string Clock(float seconds) => $"{Mathf.FloorToInt(seconds / 60):00}:{Mathf.FloorToInt(seconds % 60):00}";
    }
}