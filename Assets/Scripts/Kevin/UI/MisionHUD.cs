using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Indicador de lo que toca hacer, arriba a la izquierda: solo el objetivo, chico (ej. "Ve al sótano").
//   MisionHUD.Mostrar("Baja las palancas (2/4)");   MisionHUD.Ocultar();
// Lo maneja Objetivos (segun lo que pasa en el juego). Animaciones en tiempo real:
// - Objetivo nuevo: el anterior se tacha, sube y se desvanece; el nuevo entra desde la izquierda y el rombo hace "pop".
// - Solo cambia el contador (2/4 -> 3/4): el texto late, sin tachar.
// (Admite una segunda linea opcional, pero Objetivos no la usa.)
// Se esconde con el menu principal, la pantalla de muerte y el menu de pausa.
public class MisionHUD : MonoBehaviour
{
    public static MisionHUD Instance { get; private set; }

    static readonly Color Acento = new Color(1f, 0.8f, 0.32f);
    static readonly Color ColorTexto = new Color(0.97f, 0.95f, 0.9f);
    static readonly Color ColorSub = new Color(0.82f, 0.8f, 0.76f, 0.85f);
    const float AltoLinea = 28f;
    const float X = 28f, Y = -24f;

    class Linea
    {
        public RectTransform raiz;
        public CanvasGroup grupo;
        public TextMeshProUGUI texto, sub;
        public RectTransform rombo, tachado, fondo;
    }

    Linea actual, saliente;
    string textoActual, subActual;
    bool visible;
    float tEntrada = 99f, tSalida = 99f, tLatido = 99f, alfaGeneral;

    public static string Actual => Instance != null && Instance.visible ? Instance.textoActual : null;

    public static void Mostrar(string texto, string sub = null)
    {
        if (string.IsNullOrEmpty(texto)) { Ocultar(); return; }
        if (Instance == null) new GameObject("HUD Objetivo").AddComponent<MisionHUD>();
        Instance.Poner(texto, sub);
    }

    public static void Ocultar()
    {
        if (Instance == null || !Instance.visible) return;
        Instance.Sacar(true);
        Instance.visible = false;
        Instance.textoActual = null;
    }

    void Awake()
    {
        Instance = this;
        Canvas canvas = HUD.CrearCanvas("HUD Objetivo", 55);
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        TMP_FontAsset fuente = null;   // la fuente normal del HUD (la misma del inventario)
        actual = CrearLinea(canvas.transform, "Objetivo", fuente);
        saliente = CrearLinea(canvas.transform, "Objetivo anterior", fuente);
        actual.grupo.alpha = 0f;
        saliente.grupo.alpha = 0f;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    Linea CrearLinea(Transform padre, string nombre, TMP_FontAsset fuente)
    {
        var l = new Linea();
        l.raiz = HUD.Rect(padre, nombre, new Vector2(0f, 1f), new Vector2(X, Y), new Vector2(700f, 60f));
        l.grupo = l.raiz.gameObject.AddComponent<CanvasGroup>();
        l.grupo.blocksRaycasts = false;
        l.grupo.interactable = false;

        // Sombra suave detras (degradado que se apaga hacia la derecha) para leer sobre cualquier fondo
        l.fondo = HUD.Rect(l.raiz, "Fondo", new Vector2(0f, 1f), new Vector2(-X, 10f), new Vector2(400f, 48f));
        var img = HUD.Imagen(l.fondo, Color.white, Degradado());
        img.type = Image.Type.Simple;

        // Rombo
        l.rombo = HUD.Rect(l.raiz, "Rombo", new Vector2(0f, 1f), new Vector2(3f, -AltoLinea * 0.5f - 1f), new Vector2(7f, 7f));
        l.rombo.pivot = new Vector2(0.5f, 0.5f);
        l.rombo.localRotation = Quaternion.Euler(0f, 0f, 45f);
        HUD.Imagen(l.rombo, Acento);

        // Texto principal y linea de abajo
        RectTransform rt = HUD.Rect(l.raiz, "Texto", new Vector2(0f, 1f), new Vector2(16f, 0f), new Vector2(680f, AltoLinea));
        l.texto = rt.gameObject.AddComponent<TextMeshProUGUI>();
        Estilo(l.texto, fuente, 22f, ColorTexto);
        RectTransform rs = HUD.Rect(l.raiz, "Sub", new Vector2(0f, 1f), new Vector2(17f, -AltoLinea + 2f), new Vector2(680f, 22f));
        l.sub = rs.gameObject.AddComponent<TextMeshProUGUI>();
        Estilo(l.sub, null, 16f, ColorSub);
        l.sub.fontStyle = FontStyles.Italic;

        // Tachado (al completarse)
        l.tachado = HUD.Rect(l.raiz, "Tachado", new Vector2(0f, 1f), new Vector2(15f, -AltoLinea * 0.5f), new Vector2(0f, 2f));
        l.tachado.pivot = new Vector2(0f, 0.5f);
        HUD.Imagen(l.tachado, Acento);
        return l;
    }

    static void Estilo(TextMeshProUGUI t, TMP_FontAsset fuente, float tam, Color color)
    {
        if (fuente != null) t.font = fuente;
        t.fontSize = tam;
        t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.outlineWidth = 0.1f;
        t.outlineColor = new Color32(0, 0, 0, 150);
        t.characterSpacing = 1f;
    }

    static Sprite degradado;
    static Sprite Degradado()
    {
        if (degradado != null) return degradado;
        const int w = 128, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f), v = y / (h - 1f);
                float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.45f, 1f, u)) * Mathf.Sin(v * Mathf.PI);
                px[y * w + x] = new Color32(0, 0, 0, (byte)(a * 150f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        degradado = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 100f);
        return degradado;
    }

    // Quita el numero entre parentesis: "Baja las palancas (2/4)" -> "Baja las palancas"
    static string Base(string t)
    {
        if (string.IsNullOrEmpty(t)) return t;
        int i = t.LastIndexOf('(');
        return i > 0 ? t.Substring(0, i).TrimEnd() : t;
    }

    void Poner(string texto, string sub)
    {
        sub = string.IsNullOrEmpty(sub) ? null : sub;
        if (visible && texto == textoActual && sub == subActual) return;

        if (visible && Base(texto) == Base(textoActual))
        {
            // Mismo objetivo, otro contador o pista: late
            tLatido = 0f;
        }
        else
        {
            if (visible) Sacar(true);
            tEntrada = 0f;
        }
        textoActual = texto;
        subActual = sub;
        visible = true;
        Aplicar(actual, texto, sub);
    }

    // Copia la linea actual a la saliente y la anima afuera (tachada si se cumplio)
    void Sacar(bool tachar)
    {
        Aplicar(saliente, actual.texto.text, actual.sub.text);
        saliente.grupo.alpha = actual.grupo.alpha;
        saliente.tachado.gameObject.SetActive(tachar);
        tSalida = 0f;
        actual.grupo.alpha = 0f;
    }

    void Aplicar(Linea l, string texto, string sub)
    {
        l.texto.text = texto;
        l.sub.text = sub ?? "";
        l.sub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        float ancho = Mathf.Max(l.texto.preferredWidth, string.IsNullOrEmpty(sub) ? 0f : l.sub.preferredWidth);
        l.fondo.sizeDelta = new Vector2(ancho + 110f, string.IsNullOrEmpty(sub) ? 48f : 68f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool oculto = HUD.Oculto || PantallaMuerte.Muerto || PauseMenu.IsPaused;
        alfaGeneral = Mathf.MoveTowards(alfaGeneral, oculto ? 0f : 1f, dt * 4f);

        // Entrada del objetivo nuevo (espera un poco a que salga el anterior)
        tEntrada += dt;
        float e = Mathf.Clamp01((tEntrada - (tSalida < 0.5f ? 0.35f : 0f)) / 0.45f);
        if (visible)
        {
            actual.grupo.alpha = Jugo.SalidaCubica(e) * alfaGeneral;
            actual.raiz.anchoredPosition = new Vector2(X + Mathf.LerpUnclamped(-50f, 0f, Jugo.SalidaAtras(e, 1.8f)), Y);
            float pop = e < 1f ? Mathf.LerpUnclamped(1.9f, 1f, Jugo.SalidaAtras(e, 3f)) : 1f;
            tLatido += dt;
            float latido = tLatido < 0.4f ? 1f + 0.12f * Mathf.Sin(tLatido / 0.4f * Mathf.PI) : 1f;
            actual.rombo.localScale = Vector3.one * pop * latido;
            actual.texto.rectTransform.localScale = Vector3.one * latido;
            actual.tachado.gameObject.SetActive(false);
        }
        else actual.grupo.alpha = Mathf.MoveTowards(actual.grupo.alpha, 0f, dt * 4f);

        // Salida del anterior: se tacha, sube y se desvanece
        tSalida += dt;
        if (tSalida < 1f)
        {
            float tach = Mathf.Clamp01(tSalida / 0.3f);
            saliente.tachado.sizeDelta = new Vector2((saliente.texto.preferredWidth + 4f) * Jugo.SalidaCubica(tach), 2f);
            float s = Mathf.Clamp01((tSalida - 0.3f) / 0.45f);
            saliente.grupo.alpha = (1f - Jugo.EntradaCubica(s)) * alfaGeneral;
            saliente.raiz.anchoredPosition = new Vector2(X, Y + Mathf.Lerp(0f, 24f, Jugo.SalidaCubica(s)));
            saliente.texto.color = Color.Lerp(ColorTexto, new Color(0.75f, 0.72f, 0.65f), tach);
        }
        else saliente.grupo.alpha = 0f;
    }
}
