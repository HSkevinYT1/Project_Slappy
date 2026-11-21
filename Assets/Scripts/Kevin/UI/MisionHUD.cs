using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cartel de mision arriba a la izquierda (temporal): una etiqueta "MISION" y el objetivo actual.
// Uso: MisionHUD.Mostrar("Busca la llave");  MisionHUD.Ocultar();
// Al cambiar de objetivo el cartel parpadea un momento para que se note. Se esconde con el menu principal.
public class MisionHUD : MonoBehaviour
{
    public static MisionHUD Instance { get; private set; }

    static readonly Color Acento = new Color(1f, 0.78f, 0.25f);

    CanvasGroup grupo;
    TextMeshProUGUI objetivo;
    Image fondo;
    bool visible;
    float destello;
    RectTransform panel;
    Vector2 posBase;
    float entrada = 1f;

    public static void Mostrar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) { Ocultar(); return; }
        if (Instance == null) new GameObject("HUD Mision").AddComponent<MisionHUD>();
        Instance.Poner(texto);
    }

    public static void Ocultar()
    {
        if (Instance != null) Instance.visible = false;
    }

    public static string Actual => Instance != null && Instance.visible ? Instance.objetivo.text : null;

    void Awake()
    {
        Instance = this;
        Canvas canvas = HUD.CrearCanvas("HUD Mision", 55);
        canvas.transform.SetParent(transform, false);

        // Panel anclado arriba a la izquierda; el ancho se ajusta al texto
        panel = HUD.Rect(canvas.transform, "Mision", new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(420f, 78f));
        posBase = panel.anchoredPosition;
        grupo = panel.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;
        fondo = HUD.Imagen(panel, new Color(0.05f, 0.05f, 0.06f, 0.72f));
        var ajuste = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        ajuste.padding = new RectOffset(26, 22, 10, 12);
        ajuste.childControlWidth = ajuste.childControlHeight = true;
        ajuste.childForceExpandWidth = ajuste.childForceExpandHeight = false;
        var tam = panel.gameObject.AddComponent<ContentSizeFitter>();
        tam.horizontalFit = tam.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Barra de color a la izquierda
        var barra = HUD.Rect(panel, "Barra", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 0f));
        HUD.Imagen(barra, Acento);
        barra.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var columna = new GameObject("Textos", typeof(RectTransform));
        columna.transform.SetParent(panel, false);
        var v = columna.AddComponent<VerticalLayoutGroup>();
        v.spacing = 2f;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;

        TextMeshProUGUI etiqueta = Linea(columna.transform, "Etiqueta", "MISION", 18f, Acento);
        etiqueta.fontStyle = FontStyles.Bold;
        etiqueta.characterSpacing = 8f;
        objetivo = Linea(columna.transform, "Objetivo", "", 30f, new Color(0.96f, 0.94f, 0.9f));
    }

    static TextMeshProUGUI Linea(Transform padre, string nombre, string texto, float tamano, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = TextAlignmentOptions.Left;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.outlineWidth = 0.12f;
        t.outlineColor = new Color32(0, 0, 0, 180);
        return t;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Poner(string texto)
    {
        if (visible && objetivo.text == texto) return;
        objetivo.text = texto;
        visible = true;
        destello = 1f;
        entrada = 0f;
    }

    void Update()
    {
        bool mostrar = visible && !HUD.Oculto && !PantallaMuerte.Muerto;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, mostrar ? 1f : 0f, Time.unscaledDeltaTime * 3f);

        // Objetivo nuevo: entra desde la izquierda, se pasa un poco y se asienta
        entrada = Mathf.MoveTowards(entrada, 1f, Time.unscaledDeltaTime / 0.45f);
        panel.anchoredPosition = posBase + new Vector2(Mathf.LerpUnclamped(-90f, 0f, Jugo.SalidaAtras(entrada, 2.2f)), 0f);

        // Destello al cambiar de objetivo
        destello = Mathf.MoveTowards(destello, 0f, Time.unscaledDeltaTime * 1.2f);
        float d = Mathf.Sin(destello * Mathf.PI * 3f) * destello;
        fondo.color = Color.Lerp(new Color(0.05f, 0.05f, 0.06f, 0.72f), new Color(0.35f, 0.26f, 0.06f, 0.85f), Mathf.Abs(d));
    }
}
