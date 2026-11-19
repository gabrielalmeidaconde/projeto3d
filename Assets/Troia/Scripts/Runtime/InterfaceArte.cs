using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Troia
{
    /// <summary>
    /// Arte da interface: fontes (Cinzel para títulos e números, Marcellus para texto), sprites de bronze e ouro
    /// gerados por Ferramentas/interface/gerar_interface.py e as cores do tema. Sem os arquivos, cai no padrão do Unity.
    /// </summary>
    public static class InterfaceArte
    {
        public static readonly Color Ouro = new Color(.96f, .79f, .44f);
        public static readonly Color OuroClaro = new Color(1f, .93f, .74f);
        public static readonly Color Bronze = new Color(.6f, .4f, .19f);
        public static readonly Color Pergaminho = new Color(.94f, .9f, .82f);
        public static readonly Color Apagado = new Color(.67f, .62f, .55f);
        public static readonly Color Sangue = new Color(.78f, .1f, .08f);

        /// <summary>Escurece (k &lt; 1) ou clareia o tom sem mexer na transparência.</summary>
        public static Color Tom(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        static Font titulo, pesado, texto;
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        /// <summary>Cinzel Bold: títulos, nomes, rótulos.</summary>
        public static Font Titulo => titulo ? titulo : titulo = Fonte("Cinzel-Bold");
        /// <summary>Cinzel Black: nota de estilo, números grandes.</summary>
        public static Font Pesado => pesado ? pesado : pesado = Fonte("Cinzel-Black");
        /// <summary>Marcellus: frases e descrições.</summary>
        public static Font Texto => texto ? texto : texto = Fonte("Marcellus-Regular");

        static Font Fonte(string nome)
        {
            var f = Resources.Load<Font>("Troia/UI/Fontes/" + nome);
            return f ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static Sprite Sprite(string nome)
        {
            if (sprites.TryGetValue(nome, out var s) && s) return s;
            s = Resources.Load<Sprite>("Troia/UI/" + nome);
            sprites[nome] = s;
            return s;
        }

        public static Sprite IconeArma(WeaponKind tipo)
        {
            switch (tipo)
            {
                case WeaponKind.Spear:
                case WeaponKind.Scythe: return Sprite("Arma_Lanca");
                case WeaponKind.Shield:
                case WeaponKind.Axe: return Sprite("Arma_Escudo");
                default: return Sprite("Arma_Xifos");
            }
        }
    }

    /// <summary>Gradiente vertical em cada letra (ouro claro em cima, bronze embaixo). Vem antes da sombra no objeto.</summary>
    public class GradienteTexto : BaseMeshEffect
    {
        public Color topo = Color.white, baixo = new Color(.75f, .55f, .3f);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var v = new UIVertex();
            int n = vh.currentVertCount;
            // o Text gera um quadrado por letra: 0 e 1 em cima, 2 e 3 embaixo
            for (int i = 0; i < n; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                int k = i % 4;
                Color c = k < 2 ? topo : baixo;
                Color32 o = v.color;
                v.color = new Color32((byte)(o.r * c.r), (byte)(o.g * c.g), (byte)(o.b * c.b), (byte)(o.a * c.a));
                vh.SetUIVertex(v, i);
            }
        }

        public void Definir(Color cima, Color embaixo)
        {
            if (topo == cima && baixo == embaixo) return;
            topo = cima; baixo = embaixo;
            if (graphic) graphic.SetVerticesDirty();
        }
    }

    /// <summary>
    /// Realce de botão e de carta: acende a faixa dourada, clareia o texto, mostra os losangos e cresce um pouco.
    /// O mouse por cima seleciona o botão, para teclado, controle e mouse andarem juntos.
    /// </summary>
    public class BotaoTroia : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Graphic realce;
        public Text rotulo;
        public RectTransform marcaEsquerda, marcaDireita;
        public Color corNormal = InterfaceArte.Pergaminho, corAcesa = InterfaceArte.OuroClaro, corDesligada = new Color(.45f, .42f, .38f);
        public float escalaAcesa = 1.03f, alfaRealce = 1;
        Selectable alvo;
        bool selecionado, mouse;
        float t;

        void Awake() => alvo = GetComponent<Selectable>();
        public void OnSelect(BaseEventData e) => selecionado = true;
        public void OnDeselect(BaseEventData e) => selecionado = false;
        public void OnPointerEnter(PointerEventData e)
        {
            mouse = true;
            if (alvo && alvo.interactable && EventSystem.current) EventSystem.current.SetSelectedGameObject(gameObject);
        }
        public void OnPointerExit(PointerEventData e) => mouse = false;
        void OnDisable() { selecionado = mouse = false; t = 0; Aplicar(); }

        void Update()
        {
            bool ativo = !alvo || alvo.interactable;
            bool aceso = ativo && (selecionado || mouse || (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject));
            t = Mathf.MoveTowards(t, aceso ? 1 : 0, Time.unscaledDeltaTime * 7);
            Aplicar();
        }

        void Aplicar()
        {
            bool ativo = !alvo || alvo.interactable;
            float e = t * t * (3 - 2 * t);
            if (realce) { var c = realce.color; c.a = e * alfaRealce; realce.color = c; }
            if (rotulo)
            {
                rotulo.color = ativo ? Color.Lerp(corNormal, corAcesa, e) : corDesligada;
                if (marcaEsquerda && marcaDireita)
                {
                    float meia = Mathf.Min(rotulo.preferredWidth, rotulo.rectTransform.rect.width) * .5f + 20;
                    marcaEsquerda.anchoredPosition = new Vector2(-meia - 6 * (1 - e), 0);
                    marcaDireita.anchoredPosition = new Vector2(meia + 6 * (1 - e), 0);
                    marcaEsquerda.localScale = marcaDireita.localScale = Vector3.one * e;
                }
            }
            transform.localScale = Vector3.one * Mathf.Lerp(1, escalaAcesa, e);
        }
    }
}
