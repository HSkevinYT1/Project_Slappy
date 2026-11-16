using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reloj de pulsera del jugador. Con la tecla del reloj (C) se levanta la muneca y se ve en pantalla.
// - Normal: marca la hora del juego.
// - Con una entidad cerca (JefeIndigoAI o EstatuaAI) se vuelve loco: las agujas giran cada vez mas rapido,
//   la esfera tiembla, el cristal se pone rojo y suena un tic-tac acelerado. Cuanto mas cerca, peor.
// Se desbloquea en el tutorial de la presentacion del primer enemigo (o con "Desbloqueado al inicio").
public class RelojJugador : MonoBehaviour
{
    public static RelojJugador Instance { get; private set; }

    public bool desbloqueadoAlInicio = false;

    [Header("Hora normal")]
    [Range(0f, 24f)] public float horaInicial = 23.2f;
    [Tooltip("Minutos del juego por segundo real")]
    public float minutosPorSegundo = 1f;

    [Header("Detector de entidad")]
    [Tooltip("A esta distancia (m) empieza a notarse")]
    public float distanciaMaxima = 28f;
    [Tooltip("A esta distancia (m) o menos esta al maximo")]
    public float distanciaMinima = 3f;
    public float giroMinimo = 40f;
    public float giroMaximo = 1400f;
    [Range(0f, 1f)] public float volumenTic = 0.6f;

    public bool Desbloqueado { get; private set; }
    public bool Visible { get; private set; }
    // 0 = ninguna entidad cerca, 1 = encima
    public float Cercania { get; private set; }

    // Visual
    Canvas canvas;
    RectTransform reloj, esfera, aguHora, aguMinuto;
    Image brilloRojo, cristal;
    TextMeshProUGUI digital, tutorialTexto, pista;
    CanvasGroup tutorialGrupo, pistaGrupo;
    float subida, hora, anguloHora, anguloMinuto, sentido = 1f, proximoCambio, proximoTic, tutorialAlfa, pistaTiempo;
    AudioSource audioTic;
    AudioClip tic, tac;
    bool alterna;
    string tutorialMensaje;

    readonly List<Transform> entidades = new List<Transform>();
    float proximaBusqueda;
    Transform jugador;

    void Awake()
    {
        Instance = this;
        Desbloqueado = desbloqueadoAlInicio;
        hora = horaInicial;
        CrearUI();
        audioTic = gameObject.AddComponent<AudioSource>();
        audioTic.playOnAwake = false;
        audioTic.spatialBlend = 0f;
        audioTic.ignoreListenerPause = false;
        tic = HUD.Clic("Tic", 3200f, 0.035f, 0.45f);
        tac = HUD.Clic("Tac", 2400f, 0.04f, 0.45f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    public void Desbloquear() => Desbloqueado = true;

    public void Mostrar(bool mostrar)
    {
        if (mostrar && !Desbloqueado) return;
        Visible = mostrar;
    }

    // Cartel de tutorial (abajo al centro). Vacio = se oculta
    public void Tutorial(string mensaje) => tutorialMensaje = mensaje;

    // Texto corto debajo del reloj durante unos segundos
    public void Pista(string mensaje, float segundos)
    {
        pista.text = mensaje;
        pistaTiempo = segundos;
    }

    void Update()
    {
        bool bloqueado = PauseMenu.IsPaused || PantallaMuerte.Muerto || HUD.Oculto;
        if (!bloqueado && Desbloqueado && GameInput.Watch) Visible = !Visible;
        if (PantallaMuerte.Muerto || HUD.Oculto) Visible = false;

        BuscarEntidades();
        Cercania = CalcularCercania();

        float dt = Time.deltaTime;
        subida = Mathf.MoveTowards(subida, Visible ? 1f : 0f, dt * 3.5f);
        hora = (hora + dt * minutosPorSegundo / 60f) % 24f;

        Agujas(dt);
        Animar(dt);
        Sonido();
    }

    // ------------------------------------------------------------------ Entidades

    void BuscarEntidades()
    {
        if (Time.time < proximaBusqueda) return;
        proximaBusqueda = Time.time + 1.5f;
        entidades.Clear();
        foreach (JefeIndigoAI e in FindObjectsByType<JefeIndigoAI>(FindObjectsSortMode.None)) entidades.Add(e.transform);
        foreach (EstatuaAI e in FindObjectsByType<EstatuaAI>(FindObjectsSortMode.None)) entidades.Add(e.transform);
        if (jugador == null) jugador = HUD.Jugador();
    }

    float CalcularCercania()
    {
        if (jugador == null) return 0f;
        float menor = float.MaxValue;
        foreach (Transform e in entidades)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(jugador.position, e.position);
            if (d < menor) menor = d;
        }
        if (menor == float.MaxValue) return 0f;
        return Mathf.Clamp01(Mathf.InverseLerp(distanciaMaxima, distanciaMinima, menor));
    }

    // ------------------------------------------------------------------ Agujas y animacion

    void Agujas(float dt)
    {
        float k = Cercania;
        float minutos = hora * 60f;
        float normalMin = -(minutos % 60f) * 6f;
        float normalHora = -((hora % 12f) * 30f);

        if (k <= 0.001f)
        {
            // Vuelven solas a la hora correcta
            anguloMinuto = Mathf.LerpAngle(anguloMinuto, normalMin, 1f - Mathf.Exp(-6f * dt));
            anguloHora = Mathf.LerpAngle(anguloHora, normalHora, 1f - Mathf.Exp(-6f * dt));
        }
        else
        {
            // Giro cada vez mas rapido y, muy cerca, cambios de sentido al azar
            float velocidad = Mathf.Lerp(giroMinimo, giroMaximo, k * k);
            if (Time.time > proximoCambio)
            {
                proximoCambio = Time.time + Random.Range(0.3f, 1.4f) * Mathf.Lerp(3f, 0.4f, k);
                if (k > 0.45f && Random.value < 0.5f) sentido = -sentido;
            }
            float tiron = k > 0.6f ? (Mathf.PerlinNoise(Time.time * 9f, 1.3f) - 0.5f) * 120f * k : 0f;
            anguloMinuto += (-velocidad * sentido) * dt + tiron * dt * 10f;
            anguloHora += (velocidad * 0.37f * sentido) * dt;
        }
        aguMinuto.localRotation = Quaternion.Euler(0f, 0f, anguloMinuto);
        aguHora.localRotation = Quaternion.Euler(0f, 0f, anguloHora);

        if (k > 0.05f)
        {
            // La pantallita digital tambien falla
            string basura = "0123456789:-#?";
            char[] c = new char[5];
            for (int i = 0; i < 5; i++) c[i] = Random.value < k ? basura[Random.Range(0, basura.Length)] : (i == 2 ? ':' : '-');
            digital.text = new string(c);
            digital.color = Color.Lerp(new Color(0.55f, 1f, 0.6f), new Color(1f, 0.25f, 0.2f), k);
        }
        else
        {
            int h = Mathf.FloorToInt(hora), m = Mathf.FloorToInt(minutos % 60f);
            digital.text = h.ToString("00") + (Mathf.FloorToInt(Time.time * 2f) % 2 == 0 ? ":" : " ") + m.ToString("00");
            digital.color = new Color(0.55f, 1f, 0.6f);
        }
    }

    void Animar(float dt)
    {
        float e = subida * subida * (3f - 2f * subida);
        float k = Cercania;
        // Sube desde abajo a la izquierda, con un pequeno giro como si se levantara la muneca
        Vector2 temblor = k > 0.01f && Visible
            ? new Vector2(Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 33f) - 0.5f) * (22f * k * k)
            : Vector2.zero;
        reloj.anchoredPosition = new Vector2(Mathf.Lerp(120f, 300f, e), Mathf.Lerp(-420f, 250f, e)) + temblor;
        reloj.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(35f, 8f, e) + temblor.x * 0.4f);
        reloj.gameObject.SetActive(subida > 0.001f);

        brilloRojo.color = new Color(1f, 0.1f, 0.05f, 0.55f * k * (0.6f + 0.4f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 22f, k))));
        cristal.color = Color.Lerp(new Color(0.8f, 0.9f, 1f, 0.08f), new Color(1f, 0.2f, 0.1f, 0.22f), k);

        // Tutorial
        bool hayTutorial = !string.IsNullOrEmpty(tutorialMensaje);
        if (hayTutorial) tutorialTexto.text = tutorialMensaje;
        tutorialAlfa = Mathf.MoveTowards(tutorialAlfa, hayTutorial ? 1f : 0f, dt * 3f);
        tutorialGrupo.alpha = tutorialAlfa * (0.75f + 0.25f * Mathf.Sin(Time.time * 4f));

        pistaTiempo -= dt;
        pistaGrupo.alpha = Mathf.MoveTowards(pistaGrupo.alpha, pistaTiempo > 0f ? 1f : 0f, dt * 3f);
    }

    void Sonido()
    {
        if (!Visible || subida < 0.5f || Time.timeScale == 0f) return;
        float k = Cercania;
        float intervalo = k > 0.01f ? Mathf.Lerp(0.35f, 0.045f, k) : 1f;
        if (Time.time < proximoTic) return;
        proximoTic = Time.time + intervalo * (k > 0.5f ? Random.Range(0.6f, 1.4f) : 1f);
        alterna = !alterna;
        audioTic.pitch = 1f + k * Random.Range(-0.15f, 0.3f);
        audioTic.PlayOneShot(alterna ? tic : tac, volumenTic * (0.35f + 0.65f * Mathf.Max(0.3f, k)));
    }

    // ------------------------------------------------------------------ UI

    void CrearUI()
    {
        canvas = HUD.CrearCanvas("HUD Reloj", 45);
        var g = canvas.gameObject.AddComponent<CanvasGroup>();
        g.interactable = false;
        g.blocksRaycasts = false;

        reloj = HUD.Rect(canvas.transform, "Reloj", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(300f, -420f), new Vector2(330f, 330f));

        // Correa (arriba y abajo de la caja)
        Color cuero = new Color(0.16f, 0.11f, 0.08f, 1f);
        HUD.Imagen(HUD.Rect(reloj, "Correa Arriba", new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(170f, 260f)), cuero);
        HUD.Imagen(HUD.Rect(reloj, "Correa Abajo", new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(170f, 260f)), cuero);
        for (int i = 0; i < 3; i++)
            HUD.Imagen(HUD.Rect(reloj, "Agujero", new Vector2(0.5f, 0.5f), new Vector2(0f, -200f - i * 36f), new Vector2(12f, 12f)), new Color(0.05f, 0.03f, 0.02f), HUD.Circulo());

        // Caja metalica, esfera y brillo
        brilloRojo = HUD.Imagen(HUD.Rect(reloj, "Brillo", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 460f)), Color.clear, HUD.Circulo());
        HUD.Imagen(HUD.Rect(reloj, "Caja", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 330f)), new Color(0.55f, 0.53f, 0.5f), HUD.Circulo());
        HUD.Imagen(HUD.Rect(reloj, "Bisel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f)), new Color(0.22f, 0.21f, 0.2f), HUD.Circulo());
        HUD.Imagen(HUD.Rect(reloj, "Corona", new Vector2(0.5f, 0.5f), new Vector2(168f, 0f), new Vector2(22f, 40f)), new Color(0.5f, 0.48f, 0.45f));
        esfera = HUD.Rect(reloj, "Esfera", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270f, 270f));
        HUD.Imagen(esfera, new Color(0.9f, 0.86f, 0.74f), HUD.Circulo());

        // Marcas de las horas
        for (int i = 0; i < 60; i++)
        {
            bool hora = i % 5 == 0;
            RectTransform pivote = HUD.Rect(esfera, "Marca", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            pivote.localRotation = Quaternion.Euler(0f, 0f, -i * 6f);
            HUD.Imagen(HUD.Rect(pivote, "Raya", new Vector2(0.5f, 0.5f), new Vector2(0f, hora ? 112f : 120f), new Vector2(hora ? 6f : 2f, hora ? 22f : 9f)), new Color(0.12f, 0.1f, 0.08f));
        }
        string[] numeros = { "12", "3", "6", "9" };
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            RectTransform n = HUD.Rect(esfera, "Numero", new Vector2(0.5f, 0.5f), new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 82f, new Vector2(60f, 40f));
            HUD.Texto(n, "Texto", numeros[i], 30f, TextAlignmentOptions.Center, new Color(0.12f, 0.1f, 0.08f)).fontStyle = FontStyles.Bold;
        }

        // Pantallita digital
        RectTransform lcd = HUD.Rect(esfera, "Digital", new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(92f, 30f));
        HUD.Imagen(lcd, new Color(0.05f, 0.08f, 0.06f));
        digital = HUD.Texto(lcd, "Texto", "00:00", 22f, TextAlignmentOptions.Center, new Color(0.55f, 1f, 0.6f));

        // Agujas (pivote en el centro)
        aguHora = HUD.Rect(esfera, "Aguja Hora", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        HUD.Imagen(HUD.Rect(aguHora, "Barra", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(10f, 72f)), new Color(0.08f, 0.07f, 0.06f));
        aguMinuto = HUD.Rect(esfera, "Aguja Minuto", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        HUD.Imagen(HUD.Rect(aguMinuto, "Barra", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(6f, 108f)), new Color(0.08f, 0.07f, 0.06f));
        HUD.Imagen(HUD.Rect(esfera, "Centro", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f)), new Color(0.6f, 0.15f, 0.1f), HUD.Circulo());
        cristal = HUD.Imagen(HUD.Rect(reloj, "Cristal", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270f, 270f)), new Color(0.8f, 0.9f, 1f, 0.08f), HUD.Circulo());
        // Reflejo
        RectTransform reflejo = HUD.Rect(reloj, "Reflejo", new Vector2(0.5f, 0.5f), new Vector2(-50f, 60f), new Vector2(90f, 40f));
        reflejo.localRotation = Quaternion.Euler(0f, 0f, 35f);
        HUD.Imagen(reflejo, new Color(1f, 1f, 1f, 0.12f), HUD.Circulo());

        // Pista al lado del reloj
        RectTransform pistaR = HUD.Rect(reloj, "Pista", new Vector2(0.5f, 0.5f), new Vector2(430f, -20f), new Vector2(460f, 90f));
        pistaGrupo = pistaR.gameObject.AddComponent<CanvasGroup>();
        pistaGrupo.alpha = 0f;
        pista = HUD.Texto(pistaR, "Texto", "", 28f, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.8f));
        pista.fontStyle = FontStyles.Italic;
        pista.textWrappingMode = TextWrappingModes.Normal;

        // Cartel de tutorial (abajo al centro)
        RectTransform tut = HUD.Rect(canvas.transform, "Tutorial Reloj", new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(760f, 74f));
        tutorialGrupo = tut.gameObject.AddComponent<CanvasGroup>();
        tutorialGrupo.alpha = 0f;
        HUD.Imagen(tut, new Color(0f, 0f, 0f, 0.65f));
        HUD.Borde(tut, new Color(1f, 0.35f, 0.3f, 0.7f), 2f);
        tutorialTexto = HUD.Texto(tut, "Texto", "", 30f, TextAlignmentOptions.Center, Color.white);
    }
}
