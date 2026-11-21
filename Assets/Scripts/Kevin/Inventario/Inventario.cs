using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Inventario de 3 objetos estilo Valorant. Va en la Main Camera junto al PlayerInteractor (el HUD lo agrega solo).
// - Al agarrar algo (Grabbable > PickUp) va a la ranura seleccionada si esta vacia, si no a la primera libre.
//   Con las 3 llenas, se suelta el que esta en la mano y el nuevo ocupa su lugar.
// - 1, 2, 3 o la rueda del mouse cambian de objeto: el de la mano se guarda (se oculta) y sale el elegido.
// - Z suelta el de la mano (como antes).
// - La barra (abajo a la derecha) muestra las ranuras con su tecla, un icono y el nombre; la seleccionada
//   resalta. La linterna muestra un icono de bateria (placeholder) con su porcentaje.
[DisallowMultipleComponent]
public class Inventario : MonoBehaviour
{
    public const int Ranuras = 3;

    public static Inventario Instance { get; private set; }

    [Tooltip("Iconos opcionales para la ranura de la linterna (si no, se dibuja un placeholder)")]
    public Sprite iconoLinterna;

    readonly Grabbable[] objetos = new Grabbable[Ranuras];
    public int Actual { get; private set; }
    public Grabbable this[int i] => objetos[i];

    PlayerInteractor interactor;

    // UI
    Canvas canvas;
    CanvasGroup grupo;
    readonly Fila[] filas = new Fila[Ranuras];
    TextMeshProUGUI ayuda;
    float animCambio;
    // Pulso de la pila de la linterna al recargarla (1 = recien, baja a 0)
    float pulsoBateria;

    // Lo llama Bateria al recogerla: la pila del inventario crece, se pasa y se asienta, y brilla
    public void PulsoBateria() => pulsoBateria = 1f;

    class Fila
    {
        public RectTransform raiz;
        public CanvasGroup grupo;
        public Image fondo, acento, iconoMarco, icono;
        public TextMeshProUGUI tecla, nombre, letra, porcentaje;
        public RectTransform bloqueTexto, bateria;
        public Image bateriaRelleno;
        public float resalte, ocupado, y;
    }

    void Awake()
    {
        Instance = this;
        interactor = GetComponent<PlayerInteractor>();
        CrearUI();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void Update()
    {
        Limpiar();

        bool puedeCambiar = interactor != null && interactor.enabled && !PauseMenu.IsPaused && !PantallaMuerte.Muerto && !HUD.Oculto;
        if (puedeCambiar)
        {
            int r = GameInput.SlotPressed;
            if (r >= 0) Seleccionar(r);
            int rueda = GameInput.ScrollStep;
            if (rueda != 0) Seleccionar((Actual + rueda + Ranuras) % Ranuras);
        }

        ActualizarUI();
    }

    // ------------------------------------------------------------------ Logica

    public int IndiceDe(Grabbable g)
    {
        for (int i = 0; i < Ranuras; i++) if (objetos[i] == g && g != null) return i;
        return -1;
    }

    public bool Lleno
    {
        get { for (int i = 0; i < Ranuras; i++) if (objetos[i] == null) return false; return true; }
    }

    // Lo llama PlayerInteractor.Hold: el objeto entra al inventario y queda en la mano
    public void Recoger(Grabbable obj)
    {
        if (obj == null || interactor == null) return;
        int ya = IndiceDe(obj);
        if (ya >= 0) { Seleccionar(ya); return; }

        int ranura = objetos[Actual] == null ? Actual : PrimeraLibre();
        if (ranura < 0)
        {
            // Lleno: se suelta lo de la mano y el nuevo ocupa su ranura
            interactor.Drop();
            ranura = Actual;
        }

        if (ranura != Actual) Guardar();
        Actual = ranura;
        objetos[ranura] = obj;
        obj.gameObject.SetActive(true);
        interactor.HoldDirect(obj);
        animCambio = 1f;
        // Agarrar algo: la cabeza baja un poquito hacia la mano
        Jugo.PatadaFOV(-1f);
        Jugo.Cabeceo(0.015f);
    }

    // Lo llaman PlayerInteractor.Drop y Give: el objeto ya no esta en el inventario
    public void Quitar(Grabbable obj)
    {
        int i = IndiceDe(obj);
        if (i >= 0) objetos[i] = null;
    }

    public void Seleccionar(int i)
    {
        if (i < 0 || i >= Ranuras || interactor == null) return;
        if (i == Actual && interactor.Held == objetos[i]) return;
        Guardar();
        Actual = i;
        Grabbable g = objetos[i];
        if (g != null)
        {
            g.gameObject.SetActive(true);
            interactor.SetHeld(g);
        }
        animCambio = 1f;
    }

    // Guarda (oculta) lo que hay en la mano; sigue en su ranura
    void Guardar()
    {
        Grabbable g = interactor.Held;
        if (g == null) return;
        if (IndiceDe(g) < 0) { interactor.Drop(); return; }
        Flashlight f = g.GetComponentInChildren<Flashlight>(true);
        if (f != null && f.lightObject != null) f.lightObject.SetActive(false);
        g.gameObject.SetActive(false);
        interactor.SetHeld(null);
    }

    int PrimeraLibre()
    {
        for (int i = 0; i < Ranuras; i++) if (objetos[i] == null) return i;
        return -1;
    }

    // Objetos destruidos o que ya no estan con el jugador (entregados, soltados por otro script)
    void Limpiar()
    {
        for (int i = 0; i < Ranuras; i++)
        {
            Grabbable g = objetos[i];
            if (g == null) { objetos[i] = null; continue; }
            if (!g.transform.IsChildOf(transform.root))
            {
                objetos[i] = null;
                if (interactor != null && interactor.Held == g) interactor.SetHeld(null);
            }
        }
        // Si algo se puso en la mano sin pasar por el inventario, se agrega
        if (interactor != null && interactor.Held != null && IndiceDe(interactor.Held) < 0)
        {
            Grabbable h = interactor.Held;
            int libre = objetos[Actual] == null ? Actual : PrimeraLibre();
            if (libre >= 0) { objetos[libre] = h; Actual = libre; }
        }
    }

    public static string NombreDe(Grabbable g)
    {
        if (g == null) return "";
        ObjetoInventario oi = g.GetComponent<ObjetoInventario>();
        if (oi != null && !string.IsNullOrEmpty(oi.nombre)) return oi.nombre;
        if (g.GetComponentInChildren<Flashlight>(true) != null) return "Linterna";
        string n = g.name.Replace("(Clone)", "");
        int p = n.IndexOf('(');
        if (p > 0) n = n.Substring(0, p);
        n = n.Trim();
        return n.Length > 0 ? char.ToUpper(n[0]) + n.Substring(1) : n;
    }

    // ------------------------------------------------------------------ UI
    // Compacta, alineada a la derecha (como Valorant): de derecha a izquierda van la tecla, el icono y el texto.
    // Las ranuras vacias quedan como una linea finita con su numero; la seleccionada tiene fondo y una barra roja.

    const float Ancho = 280f, AltoLleno = 56f, AltoVacio = 30f, Separacion = 6f, Margen = 28f;
    // Altura del centro de la lista respecto al centro de la pantalla (negativo = mas abajo)
    const float AlturaLista = -60f;
    const float AltoTotal = 3f * AltoLleno + 2f * Separacion;

    void CrearUI()
    {
        canvas = HUD.CrearCanvas("HUD Inventario", 40);
        grupo = canvas.gameObject.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        // A la derecha, a media altura (un poco por debajo del centro para no tapar la mira)
        RectTransform lista = HUD.Rect(canvas.transform, "Ranuras", new Vector2(1f, 0.5f), new Vector2(-Margen, AlturaLista), new Vector2(Ancho, AltoTotal));

        for (int i = 0; i < Ranuras; i++)
        {
            var f = new Fila();
            f.raiz = HUD.Rect(lista, "Ranura " + (i + 1), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(Ancho, AltoVacio));
            f.grupo = f.raiz.gameObject.AddComponent<CanvasGroup>();

            // Fondo que se desvanece hacia la izquierda (dos tramos) y barra roja a la derecha
            f.fondo = HUD.Imagen(HUD.Rect(f.raiz, "Fondo", new Vector2(0.35f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero), new Color(0f, 0f, 0f, 0.5f));
            HUD.Imagen(HUD.Rect(f.fondo.rectTransform, "Degradado", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(70f, 0f)), new Color(0f, 0f, 0f, 0.25f));
            f.acento = HUD.Imagen(HUD.Rect(f.raiz, "Acento", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(3f, 0f)), new Color(1f, 0.3f, 0.3f));

            // Tecla
            RectTransform teclaR = HUD.Rect(f.raiz, "Tecla", new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(22f, 22f));
            HUD.Borde(teclaR, new Color(1f, 1f, 1f, 0.45f), 1.5f);
            f.tecla = HUD.Texto(teclaR, "Numero", (i + 1).ToString(), 17f, TextAlignmentOptions.Center, Color.white);

            // Icono (placeholder: marco con la inicial, o el sprite del objeto)
            RectTransform iconoR = HUD.Rect(f.raiz, "Icono", new Vector2(1f, 0.5f), new Vector2(-44f, 0f), new Vector2(40f, 40f));
            f.iconoMarco = HUD.Imagen(iconoR, new Color(1f, 1f, 1f, 0.08f));
            f.icono = HUD.Imagen(HUD.Llenar(iconoR, "Sprite", 4f), Color.white);
            f.icono.preserveAspect = true;
            f.letra = HUD.Texto(iconoR, "Inicial", "", 24f, TextAlignmentOptions.Center, Color.white);

            // Texto a la izquierda del icono: nombre arriba, bateria abajo (solo linterna)
            f.bloqueTexto = HUD.Rect(f.raiz, "Texto", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-96f, 0f), new Vector2(-104f, -8f));
            RectTransform nombreR = HUD.Rect(f.bloqueTexto, "Nombre", new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            f.nombre = HUD.Texto(nombreR, "Texto", "", 20f, TextAlignmentOptions.BottomRight, Color.white);
            f.nombre.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            f.nombre.characterSpacing = 6f;
            f.nombre.overflowMode = TextOverflowModes.Ellipsis;

            f.bateria = HUD.Rect(f.bloqueTexto, "Bateria", new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -2f), new Vector2(90f, 0f));
            RectTransform pctR = HUD.Rect(f.bateria, "Porcentaje", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(-30f, 0f));
            f.porcentaje = HUD.Texto(pctR, "Texto", "100%", 15f, TextAlignmentOptions.MidlineRight, Color.white);
            RectTransform pila = HUD.Rect(f.bateria, "Pila", new Vector2(1f, 0.5f), new Vector2(-3f, 0f), new Vector2(22f, 11f));
            HUD.Borde(pila, new Color(1f, 1f, 1f, 0.75f), 1.5f);
            HUD.Imagen(HUD.Rect(f.bateria, "Polo", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(2.5f, 5f)), new Color(1f, 1f, 1f, 0.75f));
            RectTransform carga = HUD.Llenar(pila, "Carga", 3f);
            f.bateriaRelleno = HUD.Imagen(carga, Color.green, Blanco());
            f.bateriaRelleno.type = Image.Type.Filled;
            f.bateriaRelleno.fillMethod = Image.FillMethod.Horizontal;

            filas[i] = f;
        }

        RectTransform ayudaR = HUD.Rect(canvas.transform, "Ayuda", new Vector2(1f, 0.5f), new Vector2(-Margen, 0f), new Vector2(Ancho, 18f));
        ayuda = HUD.Texto(ayudaR, "Texto", "", 15f, TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.4f));
    }

    static Sprite blanco;
    static Sprite Blanco()
    {
        if (blanco != null) return blanco;
        Texture2D t = Texture2D.whiteTexture;
        blanco = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
        return blanco;
    }

    void ActualizarUI()
    {
        if (grupo == null) return;
        bool alguno = false;
        for (int i = 0; i < Ranuras; i++) if (objetos[i] != null) alguno = true;
        float dt = Time.unscaledDeltaTime;
        float alfaObjetivo = HUD.Oculto || PantallaMuerte.Muerto || !alguno ? 0f : 1f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, alfaObjetivo, dt * 4f);
        animCambio = Mathf.MoveTowards(animCambio, 0f, dt * 4f);
        pulsoBateria = Mathf.MoveTowards(pulsoBateria, 0f, dt * 2f);

        // Se apilan de abajo hacia arriba (la 3 abajo, la 1 arriba), centrados en la lista
        float total = 0f;
        for (int i = 0; i < Ranuras; i++) total += Mathf.Lerp(AltoVacio, AltoLleno, filas[i].ocupado) + (i > 0 ? Separacion : 0f);
        float y = (AltoTotal - total) * 0.5f;
        for (int i = Ranuras - 1; i >= 0; i--)
        {
            Fila f = filas[i];
            Grabbable g = objetos[i];
            bool sel = i == Actual;
            f.resalte = Mathf.MoveTowards(f.resalte, sel ? 1f : 0f, dt * 8f);
            f.ocupado = Mathf.MoveTowards(f.ocupado, g != null ? 1f : 0f, dt * 8f);
            float k = f.resalte, o = f.ocupado;

            float alto = Mathf.Lerp(AltoVacio, AltoLleno, o);
            f.raiz.sizeDelta = new Vector2(Ancho, alto);
            f.raiz.anchoredPosition = new Vector2(-10f * k * (1f - animCambio * 0.5f), y);
            y += alto + Separacion;

            f.grupo.alpha = Mathf.Lerp(0.35f, Mathf.Lerp(0.6f, 1f, k), o);
            f.fondo.color = new Color(0f, 0f, 0f, 0.55f * k);
            f.fondo.transform.GetChild(0).GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f * k);
            f.acento.color = new Color(1f, 0.3f, 0.3f, k);
            f.iconoMarco.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.03f, 0.1f, o));
            f.iconoMarco.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(20f, 40f, o);

            string nombre = g != null ? NombreDe(g) : "";
            f.nombre.text = nombre;
            f.bloqueTexto.gameObject.SetActive(g != null);

            ObjetoInventario oi = g != null ? g.GetComponent<ObjetoInventario>() : null;
            Flashlight linterna = g != null ? g.GetComponentInChildren<Flashlight>(true) : null;
            Sprite sprite = oi != null && oi.icono != null ? oi.icono : (linterna != null ? iconoLinterna : null);
            f.icono.enabled = sprite != null;
            f.icono.sprite = sprite;
            f.letra.text = g == null || sprite != null ? "" : (oi != null && !string.IsNullOrEmpty(oi.inicial) ? oi.inicial : nombre.Substring(0, Mathf.Min(1, nombre.Length)).ToUpper());

            // El nombre se centra verticalmente si no hay linea de bateria
            f.bateria.gameObject.SetActive(linterna != null);
            RectTransform nr = (RectTransform)f.nombre.transform.parent;
            nr.anchorMin = new Vector2(0f, linterna != null ? 0.5f : 0f);
            f.nombre.alignment = linterna != null ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.MidlineRight;

            if (linterna != null)
            {
                float p = linterna.BatteryPercent;
                Color c = p > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(0.45f, 1f, 0.45f), (p - 0.5f) * 2f)
                                   : Color.Lerp(new Color(1f, 0.2f, 0.18f), new Color(1f, 0.85f, 0.2f), p * 2f);
                if (p < 0.15f) c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
                // Recarga: crece de golpe, se pasa y se asienta, con un destello blanco que se apaga
                float pulso = pulsoBateria > 0f ? Mathf.Lerp(1.5f, 1f, Jugo.SalidaAtras(1f - pulsoBateria, 3f)) : 1f;
                f.bateria.localScale = Vector3.one * pulso;
                c = Color.Lerp(c, Color.white, pulsoBateria * pulsoBateria);
                f.bateriaRelleno.color = c;
                f.bateriaRelleno.fillAmount = p;
                f.porcentaje.text = Mathf.CeilToInt(p * 100f) + "%";
                f.porcentaje.color = new Color(c.r, c.g, c.b, 0.9f);
            }
        }

        // Ayuda chiquita encima de la lista
        ((RectTransform)ayuda.transform.parent).anchoredPosition = new Vector2(-Margen, AlturaLista - AltoTotal * 0.5f + y + 11f);
        Grabbable mano = interactor != null ? interactor.Held : null;
        ayuda.text = mano != null ? "[" + GameInput.KeyName("Soltar") + "] soltar" : "";
    }
}
