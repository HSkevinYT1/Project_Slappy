using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla de muerte. Cuando un enemigo (JefeIndigoAI o EstatuaAI) atrapa al jugador:
// el jugador se congela, la camara se gira hacia el enemigo con un golpe rojo y temblor, todo se va a negro
// y aparece "HAS MUERTO" con dos opciones:
// - Reintentar: vuelve al ultimo punto de control (al entrar a cada zona, ya un poco adentro y sin nadie
//   persiguiendolo) y el enemigo que lo atrapo se va lejos.
// - Volver al inicio: recarga la escena.
// El HUD la crea sola; no hace falta ponerla en la escena.
public class PantallaMuerte : MonoBehaviour
{
    public static bool Muerto { get; private set; }
    public static PantallaMuerte Instance { get; private set; }

    public string titulo = "HAS MUERTO";
    public string[] frases =
    {
        "Algo te encontro en la oscuridad.",
        "No debiste quedarte quieto.",
        "El reloj intento avisarte.",
        "Te estaba esperando.",
    };
    [Tooltip("Vacio = un golpe generado por codigo")]
    public AudioClip sonidoMuerte;

    [Header("Puntos de control")]
    [Tooltip("Metros que hay que estar adentro de una zona para guardar el punto de control")]
    public float metrosDentroZona = 6f;

    // Ultimo punto de control
    Vector3 puntoPos;
    float puntoYaw;
    bool hayPunto;
    ZonaVolumen zonaGuardada;
    bool fueraGuardado;
    float proximoChequeo;

    // UI
    Canvas canvas;
    Image rojo, negro;
    CanvasGroup panel;
    TextMeshProUGUI tituloTexto, fraseTexto;
    Button botonReintentar;

    readonly List<Behaviour> apagados = new List<Behaviour>();
    readonly HashSet<Object> suscritos = new HashSet<Object>();
    Transform jugador;
    Rigidbody cuerpo;
    Camera camara;
    Vector3 camPosLocal;
    Quaternion camRotLocal;
    Transform asesino;
    bool reintentando;

    void Awake()
    {
        Instance = this;
        Muerto = false;
        CrearUI();
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; Muerto = false; }
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void Start() => Suscribir();

    void Suscribir()
    {
        foreach (JefeIndigoAI e in FindObjectsByType<JefeIndigoAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (suscritos.Add(e)) { JefeIndigoAI x = e; e.onPlayerCaught.AddListener(() => Morir(x.transform)); }
        foreach (EstatuaAI e in FindObjectsByType<EstatuaAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (suscritos.Add(e)) { EstatuaAI x = e; e.onPlayerCaught.AddListener(() => Morir(x.transform)); }
    }

    void Update()
    {
        if (jugador == null)
        {
            jugador = HUD.Jugador();
            if (jugador != null) cuerpo = jugador.GetComponent<Rigidbody>();
        }
        if (Time.time >= proximoChequeo)
        {
            proximoChequeo = Time.time + 0.5f;
            Suscribir();
            if (!Muerto && !HUD.Oculto && jugador != null) PuntoDeControl();
        }
    }

    // ------------------------------------------------------------------ Puntos de control

    void PuntoDeControl()
    {
        // Primero: donde empieza el juego
        if (!hayPunto) { Guardar(); return; }
        if (!Seguro()) return;

        VolumenZonas vz = FindFirstObjectByType<VolumenZonas>();
        ZonaVolumen zona = vz != null ? vz.ZonaActual : null;
        if (zona == null)
        {
            // De vuelta en la zona principal / tienda
            if (!fueraGuardado) { Guardar(); fueraGuardado = true; zonaGuardada = null; }
            return;
        }
        fueraGuardado = false;
        if (zona == zonaGuardada) return;

        // Ya bien adentro de la zona nueva (no en la boca, para no quedar del otro lado de una reja)
        Bounds b = zona.GetComponent<BoxCollider>().bounds;
        Vector3 p = jugador.position;
        float dentro = Mathf.Min(p.x - b.min.x, b.max.x - p.x, p.z - b.min.z, b.max.z - p.z);
        if (dentro < metrosDentroZona) return;
        Guardar();
        zonaGuardada = zona;
    }

    bool Seguro()
    {
        if (Escondite.JugadorEscondido) return false;
        foreach (JefeIndigoAI e in FindObjectsByType<JefeIndigoAI>(FindObjectsSortMode.None))
            if (e.CurrentState == JefeIndigoAI.State.Chase && Vector3.Distance(e.transform.position, jugador.position) < 25f) return false;
        // En el piso
        return Physics.Raycast(jugador.position + Vector3.up * 0.2f, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore);
    }

    public void Guardar()
    {
        if (jugador == null) return;
        puntoPos = cuerpo != null ? cuerpo.position : jugador.position;
        puntoYaw = jugador.eulerAngles.y;
        hayPunto = true;
    }

    // ------------------------------------------------------------------ Muerte

    public void Morir(Transform enemigo)
    {
        if (Muerto || reintentando) return;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador != null) cuerpo = jugador.GetComponent<Rigidbody>(); }
        StartCoroutine(Secuencia(enemigo));
    }

    void Apagar<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
            if (b.enabled) { b.enabled = false; apagados.Add(b); }
    }

    IEnumerator Secuencia(Transform enemigo)
    {
        Muerto = true;
        asesino = enemigo;
        if (PauseMenu.IsPaused) FindFirstObjectByType<PauseMenu>()?.Resume();

        apagados.Clear();
        Apagar<PlayerMovement>();
        Apagar<MouseLook>();
        Apagar<HeadBob>();
        Apagar<PlayerInteractor>();
        Apagar<PauseMenu>();
        Apagar<PasoBajo>();
        if (cuerpo != null) cuerpo.linearVelocity = Vector3.zero;

        camara = Camera.main;
        Transform camT = camara != null ? camara.transform : null;
        if (camT != null) { camPosLocal = camT.localPosition; camRotLocal = camT.localRotation; }

        Golpe();

        // Se gira hacia el enemigo de golpe, con un destello rojo y temblor
        Quaternion desde = camT != null ? camT.rotation : Quaternion.identity;
        Vector3 cabeza = enemigo != null ? enemigo.position + Vector3.up * 1.2f : Vector3.zero;
        for (float t = 0f; t < 1.1f; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / 0.3f);
            k = 1f - (1f - k) * (1f - k);
            if (camT != null && enemigo != null)
            {
                Quaternion hacia = Quaternion.LookRotation(cabeza - camT.position);
                float f = Mathf.Clamp01(1f - t / 1.1f);
                Vector3 sacudida = new Vector3(Mathf.PerlinNoise(t * 40f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 37f) - 0.5f, 0f) * 0.12f * f;
                camT.rotation = Quaternion.Slerp(desde, hacia, k) * Quaternion.Euler(sacudida.y * 40f, sacudida.x * 40f, (Mathf.PerlinNoise(t * 25f, 3f) - 0.5f) * 14f * f);
            }
            rojo.color = new Color(0.7f, 0f, 0f, Mathf.Lerp(0.75f, 0.35f, t / 1.1f));
            yield return null;
        }

        // A negro
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
        {
            negro.color = new Color(0f, 0f, 0f, t * t);
            rojo.color = new Color(0.7f, 0f, 0f, 0.35f * (1f - t));
            yield return null;
        }
        negro.color = Color.black;
        rojo.color = Color.clear;

        // Cartel
        fraseTexto.text = frases != null && frases.Length > 0 ? frases[Random.Range(0, frases.Length)] : "";
        tituloTexto.text = titulo;
        panel.gameObject.SetActive(true);
        panel.interactable = true;
        panel.blocksRaycasts = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AsegurarEventSystem();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(botonReintentar.gameObject);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
        {
            panel.alpha = t;
            tituloTexto.characterSpacing = Mathf.Lerp(40f, 14f, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        panel.alpha = 1f;
    }

    // Boton Reintentar
    public void Reintentar()
    {
        if (!Muerto || reintentando) return;
        StartCoroutine(Reaparecer());
    }

    IEnumerator Reaparecer()
    {
        reintentando = true;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.4f) { panel.alpha = t; yield return null; }
        panel.alpha = 0f;
        panel.gameObject.SetActive(false);

        // Jugador al punto de control
        if (jugador != null && hayPunto)
        {
            Quaternion rot = Quaternion.Euler(0f, puntoYaw, 0f);
            if (cuerpo != null)
            {
                cuerpo.linearVelocity = Vector3.zero;
                cuerpo.angularVelocity = Vector3.zero;
                cuerpo.position = puntoPos;
                cuerpo.rotation = rot;
            }
            jugador.SetPositionAndRotation(puntoPos, rot);
            Physics.SyncTransforms();
        }
        if (camara != null)
        {
            camara.transform.localPosition = camPosLocal;
            camara.transform.localRotation = Quaternion.identity;
        }

        // Los enemigos que lo atraparon (o que lo perseguian cerca) se van lejos
        foreach (JefeIndigoAI e in FindObjectsByType<JefeIndigoAI>(FindObjectsSortMode.None))
            if (e.CurrentState != JefeIndigoAI.State.Patrol || e.transform == asesino || Vector3.Distance(e.transform.position, puntoPos) < 20f)
                e.Reiniciar(puntoPos);
        foreach (EstatuaAI e in FindObjectsByType<EstatuaAI>(FindObjectsSortMode.None))
            if (e.CurrentState == EstatuaAI.State.Caught) e.Reiniciar();

        yield return new WaitForFixedUpdate();
        yield return null;

        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
        apagados.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Muerto = false;

        for (float t = 1f; t > 0f; t -= Time.deltaTime / 1.2f) { negro.color = new Color(0f, 0f, 0f, t); yield return null; }
        negro.color = Color.clear;
        reintentando = false;
    }

    // Boton Volver al inicio
    public void VolverAlInicio()
    {
        Muerto = false;
        Time.timeScale = 1f;
        Scene escena = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(escena.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(escena.name);
#endif
    }

    void Golpe()
    {
        // Si el enemigo que atrapo tiene su propio screamer, suena ese
        JefeIndigoAI jefe = asesino != null ? asesino.GetComponentInParent<JefeIndigoAI>() : null;
        AudioClip clip = jefe != null && jefe.sonidoScreamer != null ? jefe.sonidoScreamer : sonidoMuerte;
        if (clip == null)
        {
            // Golpe grave con ruido y un pitido agudo que se apaga
            int n = 44100 * 2;
            var d = new float[n];
            var rnd = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / 44100f;
                float golpe = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 38f, Mathf.Clamp01(t * 3f)) * t) * Mathf.Exp(-t * 3.5f);
                float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 9f) * 0.6f;
                float pitido = Mathf.Sin(2f * Mathf.PI * 2900f * t) * 0.08f * Mathf.Exp(-t * 1.2f) * Mathf.Clamp01(t * 8f);
                d[i] = Mathf.Clamp((golpe + ruido + pitido) * 0.9f, -1f, 1f);
            }
            clip = AudioClip.Create("Muerte", n, 1, 44100, false);
            clip.SetData(d, 0);
            sonidoMuerte = clip;
        }
        var go = new GameObject("Sonido Muerte");
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.spatialBlend = 0f;
        a.priority = 0;
        a.Play();
        Destroy(go, clip.length + 0.2f);
    }

    static void AsegurarEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    // ------------------------------------------------------------------ UI

    void CrearUI()
    {
        canvas = HUD.CrearCanvas("HUD Muerte", 200);
        rojo = HUD.Imagen(HUD.Llenar(canvas.transform, "Rojo"), Color.clear);
        negro = HUD.Imagen(HUD.Llenar(canvas.transform, "Negro"), Color.clear);

        RectTransform p = HUD.Llenar(canvas.transform, "Panel");
        panel = p.gameObject.AddComponent<CanvasGroup>();
        panel.alpha = 0f;
        // Vineta roja suave
        HUD.Imagen(HUD.Rect(p, "Halo", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 700f)), new Color(0.35f, 0f, 0f, 0.35f), HUD.Circulo());

        RectTransform t = HUD.Rect(p, "Titulo", new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1200f, 160f));
        tituloTexto = HUD.Texto(t, "Texto", titulo, 120f, TextAlignmentOptions.Center, new Color(0.85f, 0.08f, 0.06f));
        tituloTexto.fontStyle = FontStyles.Bold;
        tituloTexto.characterSpacing = 14f;

        RectTransform f = HUD.Rect(p, "Frase", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1000f, 50f));
        fraseTexto = HUD.Texto(f, "Texto", "", 32f, TextAlignmentOptions.Center, new Color(0.85f, 0.8f, 0.78f));
        fraseTexto.fontStyle = FontStyles.Italic;

        HUD.Imagen(HUD.Rect(p, "Linea", new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(520f, 2f)), new Color(0.85f, 0.1f, 0.08f, 0.6f));

        botonReintentar = HUD.Boton(p, "Reintentar", "Reintentar", new Vector2(-170f, -120f), new Vector2(300f, 70f), Reintentar);
        HUD.Boton(p, "Volver al inicio", "Volver al inicio", new Vector2(170f, -120f), new Vector2(300f, 70f), VolverAlInicio);

        panel.interactable = false;
        panel.blocksRaycasts = false;
        p.gameObject.SetActive(false);
    }
}
