using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Menu de pausa (Esc). Si no tiene un pausePanel asignado, construye el suyo por codigo con las utilidades de HUD:
// Continuar, volumen, sensibilidad del mouse, Volver al inicio y Salir, mas la lista de controles.
// El HUD lo crea solo si la escena no tiene uno.
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Tooltip("Objeto raiz del menu: se activa al pausar y se oculta al continuar. Vacio = se construye por codigo.")]
    public GameObject pausePanel;

    const string ClaveVolumen = "Opciones.Volumen";
    const string ClaveSensibilidad = "Opciones.Sensibilidad";

    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    // scripts del jugador que se apagan mientras esta en pausa
    private readonly List<Behaviour> disabledWhilePaused = new List<Behaviour>();

    // UI construida por codigo
    private Canvas canvas;
    private CanvasGroup grupo;
    private Button botonContinuar;
    private float alfa;

    // Jugo del menu (todo en tiempo real: el juego esta congelado)
    private readonly List<JugoUI> jugos = new List<JugoUI>();
    private TextMeshProUGUI titulo;
    private RectTransform tituloRect;
    private Vector2 tituloBase;
    private float tAbrir = 99f, tPagina = 99f, proximoParpadeo, finParpadeo;
    private GameObject paginaAnimada;
    private readonly Dictionary<GameObject, Vector2> basePaginas = new Dictionary<GameObject, Vector2>();

    void Awake()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(ClaveVolumen, 1f);
        if (pausePanel == null) CrearUI();
    }

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        IsPaused = false;
        AplicarSensibilidad(PlayerPrefs.GetFloat(ClaveSensibilidad, 1f));
    }

    void Update()
    {
        if (GameInput.Pause && !HUD.Oculto && !PantallaMuerte.Muerto)
        {
            if (IsPaused) Resume();
            else Pause();
        }

        // Aparece y desaparece suave (en tiempo real: el juego esta congelado)
        if (grupo != null)
        {
            alfa = Mathf.MoveTowards(alfa, IsPaused ? 1f : 0f, Time.unscaledDeltaTime * (IsPaused ? 6f : 9f));
            grupo.alpha = IsPaused ? Jugo.SalidaCubica(alfa) : alfa * alfa;
            if (!IsPaused && alfa <= 0f && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
            if (canvas.gameObject.activeSelf) AnimarJugo(Time.unscaledDeltaTime);
        }
    }

    // Titulo que cae y rebota al abrir, con algun parpadeo de cartel que falla; paginas que entran deslizando
    void AnimarJugo(float dt)
    {
        tAbrir += dt;
        tPagina += dt;
        if (tituloRect != null)
        {
            float k = Jugo.SalidaAtras(tAbrir / 0.5f, 2.2f);
            tituloRect.anchoredPosition = tituloBase + new Vector2(0f, Mathf.LerpUnclamped(46f, 0f, k));
            tituloRect.localScale = Vector3.one * Mathf.LerpUnclamped(1.18f, 1f, k);
        }
        if (titulo != null)
        {
            float ahora = Time.unscaledTime;
            if (ahora > proximoParpadeo)
            {
                finParpadeo = ahora + Random.Range(0.08f, 0.22f);
                proximoParpadeo = ahora + Random.Range(2.5f, 6f);
            }
            titulo.alpha = ahora < finParpadeo ? (Mathf.PerlinNoise(ahora * 40f, 0.3f) > 0.5f ? 1f : 0.25f) : 1f;
        }
        if (paginaAnimada != null && paginaAnimada.activeSelf && basePaginas.TryGetValue(paginaAnimada, out Vector2 b))
        {
            float k = Jugo.SalidaCubica(tPagina / 0.28f);
            ((RectTransform)paginaAnimada.transform).anchoredPosition = b + new Vector2(Mathf.Lerp(50f, 0f, k), 0f);
            paginaAnimada.GetComponent<CanvasGroup>().alpha = k;
        }
    }

    void OnDestroy()
    {
        // por si se cambia de escena estando en pausa
        if (IsPaused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            IsPaused = false;
        }
        if (canvas != null) Destroy(canvas.gameObject);
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // la camara, la linterna y las interacciones leen el input directamente,
        // asi que se apagan para que no reaccionen mientras el menu esta abierto
        disabledWhilePaused.Clear();
        DisableAll<MouseLook>();
        DisableAll<Flashlight>();
        DisableAll<PlayerInteractor>();
        DisableAll<DialogueManager>();
        DisableAll<GraspObject>();

        if (pausePanel != null) pausePanel.SetActive(true);
        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);
            MostrarPagina(null);
            grupo.interactable = true;
            grupo.blocksRaycasts = true;
            // Entrada: el titulo cae y las opciones llegan una detras de otra
            tAbrir = 0f;
            proximoParpadeo = Time.unscaledTime + Random.Range(1.5f, 3f);
            for (int i = 0; i < jugos.Count; i++) jugos[i].Entrar(0.06f + i * 0.045f);
            PantallaMuerte.AsegurarEventSystem();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(botonContinuar.gameObject);
        }
    }

    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (grupo != null)
        {
            grupo.interactable = false;
            grupo.blocksRaycasts = false;
        }

        foreach (Behaviour b in disabledWhilePaused)
        {
            if (b != null) b.enabled = true;
        }
        disabledWhilePaused.Clear();

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;

        AudioListener.pause = false;
        Time.timeScale = 1f;
        IsPaused = false;
    }

    // Recarga la escena (lo mismo que "Volver al inicio" de la pantalla de muerte)
    public void VolverAlInicio()
    {
        Resume();
        if (PantallaMuerte.Instance != null) PantallaMuerte.Instance.VolverAlInicio();
        else UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void DisableAll<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (!b.enabled) continue;
            b.enabled = false;
            disabledWhilePaused.Add(b);
        }
    }

    // ------------------------------------------------------------------ Opciones

    // La sensibilidad se guarda como multiplicador sobre la que trae cada MouseLook en la escena
    readonly Dictionary<MouseLook, float> sensibilidadBase = new Dictionary<MouseLook, float>();

    void AplicarSensibilidad(float factor)
    {
        foreach (MouseLook m in FindObjectsByType<MouseLook>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!sensibilidadBase.TryGetValue(m, out float b)) { b = m.mouseSensitivity; sensibilidadBase[m] = b; }
            m.mouseSensitivity = b * factor;
        }
    }

    // ------------------------------------------------------------------ UI
    // Mismo estilo que el menu principal: fuente Frisky, opciones de texto a la izquierda sobre la escena
    // oscurecida, sin cajas. Opciones y Controles se abren al lado de la lista.

    static readonly Color Crema = new Color(0.92f, 0.9f, 0.85f);
    static readonly Color Rojo = new Color(0.85f, 0.1f, 0.1f);

    GameObject paginaOpciones, paginaControles;
    GameObject sombraPagina;
    Button botonOpciones, botonControles;

    void CrearUI()
    {
        canvas = HUD.CrearCanvas("HUD Pausa", 150);
        canvas.transform.SetParent(transform, false);
        grupo = canvas.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        TMP_FontAsset fuente = FuenteDelMenu();

        // La escena se sigue viendo detras; la mitad izquierda un poco mas oscura para leer
        Image fondo = HUD.Imagen(HUD.Llenar(canvas.transform, "Fondo"), new Color(0f, 0f, 0f, 0.4f));
        fondo.raycastTarget = true;
        HUD.Imagen(HUD.Rect(canvas.transform, "Sombra", Vector2.zero, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero), new Color(0f, 0f, 0f, 0.4f));
        // Con una pagina abierta se oscurece tambien la mitad derecha (si no, se pierde contra la escena)
        sombraPagina = HUD.Rect(canvas.transform, "Sombra Pagina", new Vector2(0.5f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        HUD.Imagen((RectTransform)sombraPagina.transform, new Color(0f, 0f, 0f, 0.5f));

        tituloRect = ArribaIzq(canvas.transform, "Titulo", new Vector2(90f, -60f), new Vector2(600f, 120f));
        tituloRect.pivot = new Vector2(0f, 0.5f);   // crece desde la izquierda (donde empieza la palabra)
        tituloRect.anchoredPosition = new Vector2(90f, -120f);
        tituloBase = tituloRect.anchoredPosition;
        titulo = HUD.Texto(tituloRect, "Texto", "Pausa", 96f, TextAlignmentOptions.MidlineLeft, Rojo);
        if (fuente) titulo.font = fuente;

        // Lista de opciones
        string[] nombres = { "Continuar", "Opciones", "Controles", "Volver al inicio", "Salir" };
        UnityEngine.Events.UnityAction[] acciones =
        {
            Resume,
            () => MostrarPagina(paginaOpciones),
            () => MostrarPagina(paginaControles),
            VolverAlInicio,
            Quit,
        };
        for (int i = 0; i < nombres.Length; i++)
        {
            Button b = Opcion(canvas.transform, nombres[i], new Vector2(96f, -220f - i * 66f), fuente, acciones[i]);
            jugos.Add(b.GetComponent<JugoUI>());
            if (i == 0) botonContinuar = b;
            if (i == 1) botonOpciones = b;
            if (i == 2) botonControles = b;
        }

        // Pagina de opciones
        paginaOpciones = ArribaIzq(canvas.transform, "Pagina Opciones", new Vector2(700f, -230f), new Vector2(480f, 330f)).gameObject;
        Deslizador((RectTransform)paginaOpciones.transform, "Volumen", 0f, fuente, PlayerPrefs.GetFloat(ClaveVolumen, 1f), 0f, 1f, v =>
        {
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(ClaveVolumen, v);
        });
        Deslizador((RectTransform)paginaOpciones.transform, "Sensibilidad", -110f, fuente, PlayerPrefs.GetFloat(ClaveSensibilidad, 1f), 0.25f, 2.5f, v =>
        {
            AplicarSensibilidad(v);
            PlayerPrefs.SetFloat(ClaveSensibilidad, v);
        });
        // Accesibilidad: cuanto se mueve la camara con el jugo (temblor, golpes de vision, cabeceos). 0 = nada
        Deslizador((RectTransform)paginaOpciones.transform, "Movimiento de camara", -220f, fuente, Jugo.Movimiento, 0f, 1f, v =>
        {
            Jugo.Movimiento = v;
            Jugo.Sacudir(0.35f * v);   // muestra como se siente (se ve al cerrar el menu)
        });

        // Pagina de controles: dos columnas de texto (las teclas salen de Assets/InputSystem_Actions)
        paginaControles = ArribaIzq(canvas.transform, "Pagina Controles", new Vector2(700f, -230f), new Vector2(480f, 420f)).gameObject;
        string[,] filas =
        {
            { "WASD", "Moverse" },
            { "Shift", "Correr" },
            { Tecla("Interactuar"), "Interactuar / agarrar" },
            { Tecla("Soltar"), "Soltar objeto" },
            { "1  2  3", "Inventario" },
            { Tecla("Linterna"), "Linterna" },
            { Tecla("Reloj"), "Mirar el reloj" },
            { Tecla("Pausa"), "Pausa" },
        };
        for (int i = 0; i < filas.GetLength(0); i++)
        {
            float y = -i * 48f;
            TextMeshProUGUI tecla = HUD.Texto(ArribaIzq(paginaControles.transform, "Tecla " + i, new Vector2(0f, y), new Vector2(140f, 44f)), "Texto", filas[i, 0], 30f, TextAlignmentOptions.MidlineLeft, Rojo);
            TextMeshProUGUI accion = HUD.Texto(ArribaIzq(paginaControles.transform, "Accion " + i, new Vector2(160f, y), new Vector2(340f, 44f)), "Texto", filas[i, 1], 28f, TextAlignmentOptions.MidlineLeft, Crema);
            if (fuente) { tecla.font = fuente; accion.font = fuente; }
        }

        foreach (GameObject p in new[] { paginaOpciones, paginaControles })
        {
            basePaginas[p] = ((RectTransform)p.transform).anchoredPosition;
            p.AddComponent<CanvasGroup>();
        }
        MostrarPagina(null);
        canvas.gameObject.SetActive(false);
    }

    // Abre una pagina al lado de la lista (otra vez la misma la cierra)
    void MostrarPagina(GameObject pagina)
    {
        bool abrir = pagina != null && !pagina.activeSelf;
        paginaOpciones.SetActive(abrir && pagina == paginaOpciones);
        paginaControles.SetActive(abrir && pagina == paginaControles);
        sombraPagina.SetActive(abrir);
        // La pagina entra deslizando desde la derecha
        if (abrir)
        {
            paginaAnimada = pagina;
            tPagina = 0f;
            AnimarJugo(0f);
        }
        // La opcion abierta queda en rojo
        Marcar(botonOpciones, paginaOpciones.activeSelf);
        Marcar(botonControles, paginaControles.activeSelf);
    }

    static void Marcar(Button b, bool activa)
    {
        ColorBlock c = b.colors;
        c.normalColor = activa ? Rojo : Crema;
        c.selectedColor = c.normalColor;
        b.colors = c;
    }

    // La fuente del titulo del menu principal (Frisky); si no hay menu, queda la de TMP
    static TMP_FontAsset FuenteDelMenu()
    {
        MenuPrincipal menu = FindFirstObjectByType<MenuPrincipal>(FindObjectsInactive.Include);
        TMP_Text t = menu != null && menu.panel != null ? menu.panel.GetComponentInChildren<TMP_Text>(true) : null;
        return t != null ? t.font : null;
    }

    static RectTransform ArribaIzq(Transform padre, string nombre, Vector2 pos, Vector2 tam)
        => HUD.Rect(padre, nombre, new Vector2(0f, 1f), pos, tam);

    // Opcion de solo texto: crema normal, roja al pasar el mouse (como los botones del menu principal)
    static Button Opcion(Transform padre, string texto, Vector2 pos, TMP_FontAsset fuente, UnityEngine.Events.UnityAction accion)
    {
        RectTransform r = ArribaIzq(padre, texto, pos, new Vector2(420f, 58f));
        HUD.Imagen(r, Color.clear).raycastTarget = true;   // zona de clic
        TextMeshProUGUI t = HUD.Texto(r, "Texto", texto, 48f, TextAlignmentOptions.MidlineLeft, Color.white);
        if (fuente) t.font = fuente;
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = t;
        ColorBlock c = b.colors;
        c.normalColor = Crema;
        c.highlightedColor = Rojo;
        c.pressedColor = new Color(0.5f, 0.05f, 0.05f);
        c.selectedColor = Crema;
        c.fadeDuration = 0.06f;
        b.colors = c;
        b.onClick.AddListener(accion);

        // Jugo: el texto se corre y crece al pasar el mouse, con una rayita roja que aparece a su izquierda
        RectTransform marca = HUD.Rect(r, "Marca", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-4f, -2f), new Vector2(14f, 4f));
        HUD.Imagen(marca, Rojo);
        marca.gameObject.SetActive(false);
        t.rectTransform.pivot = new Vector2(0f, 0.5f);   // escala desde donde empieza la palabra
        t.rectTransform.anchoredPosition = new Vector2(-t.rectTransform.sizeDelta.x * 0.5f, 0f);
        var jugo = r.gameObject.AddComponent<JugoUI>();
        jugo.visual = t.rectTransform;
        jugo.marca = marca;
        return b;
    }

    static string Tecla(string accion)
    {
        string t = GameInput.KeyName(accion);
        return string.IsNullOrEmpty(t) ? accion : t.ToUpperInvariant();
    }

    // Nombre arriba y una linea fina con un mango chico (Slider de Unity armado por codigo)
    static Slider Deslizador(RectTransform padre, string etiqueta, float y, TMP_FontAsset fuente, float valor, float min, float max, UnityEngine.Events.UnityAction<float> alCambiar)
    {
        RectTransform r = ArribaIzq(padre, etiqueta, new Vector2(0f, y), new Vector2(420f, 84f));
        TextMeshProUGUI et = HUD.Texto(HUD.Rect(r, "Etiqueta", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 44f)),
            "Texto", etiqueta, 34f, TextAlignmentOptions.MidlineLeft, Crema);
        if (fuente) et.font = fuente;

        // Zona de arrastre alta (facil de agarrar) con la linea visible fina en el medio
        RectTransform barra = HUD.Rect(r, "Barra", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(-12f, 28f));
        HUD.Imagen(barra, Color.clear).raycastTarget = true;
        HUD.Imagen(HUD.Rect(barra, "Linea", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 4f)), new Color(1f, 1f, 1f, 0.35f));
        RectTransform area = HUD.Rect(barra, "Relleno Area", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 4f));
        RectTransform relleno = HUD.Llenar(area, "Relleno");
        HUD.Imagen(relleno, Rojo);
        RectTransform areaMango = HUD.Llenar(barra, "Mango Area");
        RectTransform mango = HUD.Rect(areaMango, "Mango", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 28f));
        Image mangoImg = HUD.Imagen(mango, Crema);

        var s = r.gameObject.AddComponent<Slider>();
        s.fillRect = relleno;
        s.handleRect = mango;
        s.targetGraphic = mangoImg;
        ColorBlock c = s.colors;
        c.highlightedColor = Rojo;
        c.selectedColor = Color.white;
        s.colors = c;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min;
        s.maxValue = max;
        s.value = Mathf.Clamp(valor, min, max);
        s.onValueChanged.AddListener(alCambiar);

        // Jugo: el mango crece al pasar el mouse o arrastrarlo
        var jugo = r.gameObject.AddComponent<JugoUI>();
        jugo.visual = mango;
        jugo.moverPosicion = false;
        jugo.escalaHover = 1.5f;
        return s;
    }
}
