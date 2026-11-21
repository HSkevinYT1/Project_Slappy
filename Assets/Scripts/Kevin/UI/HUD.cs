using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD del jugador: se crea solo al cargar una escena que tenga jugador (PlayerInteractor), sin tocar la escena.
// - Inventario (en la Main Camera, junto al PlayerInteractor) con su barra estilo Valorant.
// - Reloj de pulsera (RelojJugador) y pantalla de muerte (PantallaMuerte).
// - Agranda la UI de los Canvas de la escena (Escala).
// Tambien tiene las utilidades para construir UI por codigo (textos, paneles, circulos).
public static class HUD
{
    // Cuanto mas grande se ve la UI (1 = como estaba). Afecta a los Canvas con "Scale With Screen Size",
    // al texto de interaccion y a todo el HUD nuevo.
    public static float Escala = 1.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlCargar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        Preparar();
    }

    static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Preparar();

    static void Preparar()
    {
        PlayerInteractor interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (interactor == null) return;

        if (!interactor.GetComponent<Inventario>()) interactor.gameObject.AddComponent<Inventario>();

        // Jugo de camara (temblor, golpes de FOV, correr, aterrizajes) en la camara del jugador
        MouseLook vista = Object.FindFirstObjectByType<MouseLook>(FindObjectsInactive.Include);
        if (vista != null && vista.GetComponent<Camera>() && !vista.GetComponent<CamaraJugo>()) vista.gameObject.AddComponent<CamaraJugo>();

        if (!Object.FindFirstObjectByType<RelojJugador>(FindObjectsInactive.Include) || !Object.FindFirstObjectByType<PantallaMuerte>(FindObjectsInactive.Include))
        {
            var go = new GameObject("HUD Jugador");
            if (!Object.FindFirstObjectByType<RelojJugador>(FindObjectsInactive.Include)) go.AddComponent<RelojJugador>();
            if (!Object.FindFirstObjectByType<PantallaMuerte>(FindObjectsInactive.Include)) go.AddComponent<PantallaMuerte>();
        }
        // Barra de estamina y menu de pausa (si la escena no trae uno propio, se construye por codigo)
        if (!Object.FindFirstObjectByType<EstaminaUI>(FindObjectsInactive.Include)) new GameObject("HUD Estamina").AddComponent<EstaminaUI>();
        if (!Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include)) new GameObject("Menu de Pausa").AddComponent<PauseMenu>();

        // UI mas grande: los Canvas que escalan con la pantalla usan una resolucion de referencia mas chica
        foreach (CanvasScaler cs in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cs.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || cs.GetComponent<HUDEscalado>()) continue;
            if (cs.GetComponentInParent<MenuPrincipal>(true)) continue; // el menu principal queda como se diseno
            cs.referenceResolution /= Escala;
            cs.gameObject.AddComponent<HUDEscalado>();
        }
    }

    // Jugador (el objeto con el Rigidbody que se mueve)
    public static Transform Jugador()
    {
        PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        return pm != null ? pm.transform : null;
    }

    // Hay algo que deberia esconder el HUD (menu principal abierto)
    public static bool Oculto => MenuPrincipal.Abierto;

    // Mensaje corto arriba al centro durante unos segundos (ej: "Algo se abrio a lo lejos...")
    public static void Aviso(string texto, float segundos)
    {
        if (AvisoHUD.Instance == null) new GameObject("HUD Aviso").AddComponent<AvisoHUD>();
        AvisoHUD.Instance.Mostrar(texto, segundos);
    }

    // ------------------------------------------------------------------ Construccion de UI

    public static Canvas CrearCanvas(string nombre, int orden, Transform padre = null)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        if (padre != null) go.transform.SetParent(padre, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f) / Escala;
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<HUDEscalado>();
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Rect(Transform padre, string nombre, Vector2 anclaMin, Vector2 anclaMax, Vector2 pivote, Vector2 pos, Vector2 tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = anclaMin;
        r.anchorMax = anclaMax;
        r.pivot = pivote;
        r.anchoredPosition = pos;
        r.sizeDelta = tam;
        return r;
    }

    // Rect anclado a un punto (ancla y pivote iguales)
    public static RectTransform Rect(Transform padre, string nombre, Vector2 ancla, Vector2 pos, Vector2 tam)
        => Rect(padre, nombre, ancla, ancla, ancla, pos, tam);

    // Rect que llena a su padre (con margen)
    public static RectTransform Llenar(Transform padre, string nombre, float margen = 0f)
    {
        RectTransform r = Rect(padre, nombre, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        r.offsetMin = new Vector2(margen, margen);
        r.offsetMax = new Vector2(-margen, -margen);
        return r;
    }

    public static Image Imagen(RectTransform r, Color color, Sprite sprite = null)
    {
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI Texto(Transform padre, string nombre, string texto, float tamano, TextAlignmentOptions alineacion, Color color)
    {
        RectTransform r = Llenar(padre, nombre);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = color;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    public static Button Boton(Transform padre, string nombre, string texto, Vector2 pos, Vector2 tam, UnityEngine.Events.UnityAction accion)
    {
        RectTransform r = Rect(padre, nombre, new Vector2(0.5f, 0.5f), pos, tam);
        Image fondo = Imagen(r, new Color(0.08f, 0.08f, 0.09f, 0.85f));
        fondo.raycastTarget = true;
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = fondo;
        ColorBlock c = b.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1f, 0.55f, 0.55f);
        c.pressedColor = new Color(0.7f, 0.2f, 0.2f);
        c.selectedColor = c.highlightedColor;
        c.fadeDuration = 0.08f;
        b.colors = c;
        b.onClick.AddListener(accion);
        Borde(r, new Color(1f, 1f, 1f, 0.18f), 2f);
        Texto(r, "Texto", texto, 30f, TextAlignmentOptions.Center, new Color(0.95f, 0.93f, 0.9f));
        return b;
    }

    // Marco fino alrededor de un rect (4 barras)
    public static void Borde(RectTransform r, Color color, float grosor)
    {
        Imagen(Rect(r, "Borde Arriba", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, grosor)), color);
        Imagen(Rect(r, "Borde Abajo", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, grosor)), color);
        Imagen(Rect(r, "Borde Izq", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(grosor, 0f)), color);
        Imagen(Rect(r, "Borde Der", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(grosor, 0f)), color);
    }

    static readonly Dictionary<int, Sprite> circulos = new Dictionary<int, Sprite>();

    // Circulo (disco) o anillo suavizado generado por codigo. grosorAnillo 0 = disco lleno; 0..1 = fraccion del radio
    public static Sprite Circulo(float grosorAnillo = 0f)
    {
        int clave = Mathf.RoundToInt(grosorAnillo * 1000f);
        if (circulos.TryGetValue(clave, out Sprite s) && s != null) return s;
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        float rExt = n * 0.5f - 1f, rInt = grosorAnillo > 0f ? rExt * (1f - grosorAnillo) : -1f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(rExt - d + 0.5f);
                if (rInt > 0f) a *= Mathf.Clamp01(d - rInt + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        circulos[clave] = s;
        return s;
    }

    static Sprite vineta;

    // Vineta: transparente en el centro y negra hacia los bordes (para pantallas de muerte, pausa...)
    public static Sprite Vineta()
    {
        if (vineta != null) return vineta;
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        vineta = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return vineta;
    }

    static Texture2D ruido;

    // Textura de ruido (grano de pelicula). Usar en un RawImage moviendo su uvRect cada frame
    public static Texture2D Ruido()
    {
        if (ruido != null) return ruido;
        const int n = 128;
        ruido = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color32[n * n];
        var rnd = new System.Random(11);
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)rnd.Next(0, 256);
            px[i] = new Color32(v, v, v, 255);
        }
        ruido.SetPixels32(px);
        ruido.Apply();
        return ruido;
    }

    // Sonido corto generado por codigo (tic del reloj, golpe...)
    public static AudioClip Clic(string nombre, float frecuencia, float duracion, float ruido)
    {
        int muestras = Mathf.Max(1, Mathf.RoundToInt(44100 * duracion));
        var datos = new float[muestras];
        var rnd = new System.Random(nombre.GetHashCode());
        for (int i = 0; i < muestras; i++)
        {
            float t = i / 44100f;
            float env = Mathf.Exp(-t / (duracion * 0.25f));
            float tono = Mathf.Sin(2f * Mathf.PI * frecuencia * t);
            float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
            datos[i] = (tono * (1f - ruido) + n * ruido) * env * 0.8f;
        }
        AudioClip c = AudioClip.Create(nombre, muestras, 1, 44100, false);
        c.SetData(datos, 0);
        return c;
    }
}

// Marca un CanvasScaler que ya se agrando (para no agrandarlo dos veces)
public class HUDEscalado : MonoBehaviour { }

// Muestra los avisos de HUD.Aviso
public class AvisoHUD : MonoBehaviour
{
    public static AvisoHUD Instance { get; private set; }
    Canvas canvas;
    CanvasGroup grupo;
    TextMeshProUGUI texto;
    float restante;

    void Awake()
    {
        Instance = this;
        canvas = HUD.CrearCanvas("HUD Avisos", 60);
        canvas.transform.SetParent(transform, false);
        RectTransform r = HUD.Rect(canvas.transform, "Aviso", new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1200f, 60f));
        grupo = r.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;
        texto = HUD.Texto(r, "Texto", "", 36f, TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.85f));
        texto.fontStyle = FontStyles.Italic;
        texto.outlineWidth = 0.15f;
        texto.outlineColor = new Color32(0, 0, 0, 200);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Mostrar(string t, float segundos)
    {
        texto.text = t;
        restante = segundos;
    }

    void Update()
    {
        restante -= Time.deltaTime;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, restante > 0f && !HUD.Oculto ? 1f : 0f, Time.deltaTime * 2.5f);
    }
}
