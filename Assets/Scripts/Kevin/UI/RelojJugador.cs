using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Reloj de pulsera del jugador: el modelo 3D "reloj de pulsera" (Assets/Models/Player/reloj) va en la muneca
// izquierda de los brazos (objeto "Reloj 3D" bajo el hueso Forearm.L, lo coloca Tools > Jugador > Reloj 3D en la muneca).
// Con la tecla del reloj (C) se levanta el brazo izquierdo y se mira el reloj.
// - Normal: las agujas marcan la hora del juego.
// - Con una entidad cerca (JefeIndigoAI o EstatuaAI) DE LA MISMA AREA (ZonaVolumen) que el jugador se vuelve loco
//   (las de otras zonas no cuentan aunque esten cerca a traves de una pared): las agujas giran cada vez mas rapido,
//   el brazo tiembla, la esfera se pone roja y brilla, y suena un tic-tac acelerado. Cuanto mas cerca, peor.
// Se desbloquea en el tutorial de la presentacion del primer enemigo (o con "Desbloqueado al inicio").
public class RelojJugador : MonoBehaviour
{
    public static RelojJugador Instance { get; private set; }

    // Nombres dentro del modelo (los pone el setup del editor)
    public const string NombreReloj = "Reloj 3D";
    public const string NombreAgujaHora = "Aguja Hora";
    public const string NombreAgujaMinuto = "Aguja Minuto";
    public const string NombreEsfera = "Esfera";
    public const string NombreBrillo = "Brillo";
    public const string HuesoBrazo = "LbowControl.L";

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
    [Tooltip("Tic-tac del reloj (en bucle mientras se mira). Vacio = tic-tac generado por codigo")]
    public AudioClip sonidoManecillas;
    [Tooltip("Cuanto se acelera el tic-tac con la entidad encima (2.5 = dos veces y media)")]
    public float aceleracionManecillas = 2.5f;
    [Tooltip("Guardado (sin mirarlo) el tic-tac se sigue oyendo, muy bajo y apagado, como dentro de la manga (0 = no se oye)")]
    [Range(0f, 1f)] public float volumenGuardado = 0.12f;

    [Header("Brazo al mirar el reloj")]
    [Tooltip("Donde queda el reloj, en espacio de la camara (x derecha, y arriba, z adelante)")]
    public Vector3 posicionMirar = new Vector3(-0.03f, -0.05f, 0.25f);
    [Tooltip("Inclinacion del antebrazo (grados, positivo = la mano mas arriba)")]
    public float inclinacionBrazo = 12f;
    [Tooltip("Cuanto se inclina la esfera hacia arriba (grados)")]
    public float inclinacionEsfera = 10f;
    public float velocidadSubida = 3.2f;

    public bool Desbloqueado { get; private set; }
    public bool Visible { get; private set; }
    // 0 = ninguna entidad cerca, 1 = encima
    public float Cercania { get; private set; }

    // Modelo
    Transform camara, brazo, reloj, aguHora, aguMinuto;
    Camera camJugador, camConCapa;
    int mascaraAntes;
    Renderer esfera;
    Light brillo;
    Vector3 brazoPos;
    Quaternion brazoRot;
    MaterialPropertyBlock bloque;
    Color esferaColor = new Color(0.9f, 0.86f, 0.74f);
    float proximaBusquedaModelo;

    // UI (solo textos)
    Canvas canvas;
    TextMeshProUGUI tutorialTexto, pista;
    CanvasGroup tutorialGrupo, pistaGrupo;
    float subida, hora, anguloHora, anguloMinuto, sentido = 1f, proximoCambio, proximoTic, tutorialAlfa, pistaTiempo;
    AudioSource audioTic, audioManecillas;
    AudioClip tic, tac;
    bool alterna;
    string tutorialMensaje;

    readonly List<Transform> entidades = new List<Transform>();
    readonly List<ZonaVolumen> zonasJugador = new List<ZonaVolumen>();
    ZonaVolumen[] zonas = new ZonaVolumen[0];
    float proximaBusqueda;
    Transform jugador;

    void Awake()
    {
        Instance = this;
        Desbloqueado = desbloqueadoAlInicio;
        hora = horaInicial;
        CrearUI();
        audioTic = FuenteReloj().AddComponent<AudioSource>();
        audioTic.playOnAwake = false;
        audioTic.spatialBlend = 0f;
        audioTic.ignoreListenerPause = false;
        tic = HUD.Clic("Tic", 3200f, 0.035f, 0.45f);
        tac = HUD.Clic("Tac", 2400f, 0.04f, 0.45f);
        bloque = new MaterialPropertyBlock();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
        if (brazo != null) { brazo.localPosition = brazoPos; brazo.localRotation = brazoRot; }
    }

    public void Desbloquear() => Desbloqueado = true;

    public void Mostrar(bool mostrar)
    {
        if (mostrar && !Desbloqueado) return;
        Visible = mostrar;
    }

    // Cartel de tutorial (abajo al centro). Vacio = se oculta
    public void Tutorial(string mensaje) => tutorialMensaje = mensaje;

    // Texto corto al lado del reloj durante unos segundos
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
        subida = Mathf.MoveTowards(subida, Visible ? 1f : 0f, dt * velocidadSubida);
        hora = (hora + dt * minutosPorSegundo / 60f) % 24f;

        Agujas(dt);
        Textos(dt);
        Sonido();
    }

    // Despues de que la camara se movio (MouseLook, HeadBob, cinematicas): el brazo sube con la vista
    void LateUpdate()
    {
        BuscarModelo();
        if (brazo == null) return;
        brazo.localPosition = brazoPos;
        brazo.localRotation = brazoRot;
        if (subida <= 0.0001f || !brazo.gameObject.activeInHierarchy) { QuitarCapa(); return; }

        // Si una cinematica dibuja con otra camara (p. ej. la presentacion del enemigo), el reloj se pone
        // delante de esa camara y esa camara dibuja tambien la capa de los brazos
        Camera vista = CamaraVista();
        float zoom = 1f;
        if (vista != camJugador)
        {
            PonerCapa(vista);
            zoom = Mathf.Tan(camJugador.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(vista.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }
        else QuitarCapa();

        float e = subida * subida * (3f - 2f * subida);
        float k = Cercania;
        Vector3 temblor = k > 0.01f && Visible
            ? new Vector3(Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 33f) - 0.5f, 0f) * (0.02f * k * k)
            : Vector3.zero;
        PoseMirar(brazo, reloj, vista.transform, (posicionMirar + temblor) * zoom, inclinacionBrazo + temblor.x * 300f, inclinacionEsfera, e);
    }

    // La camara que esta dibujando: la del jugador o, si esta apagada, la de la cinematica (la de mayor profundidad)
    Camera CamaraVista()
    {
        if (camJugador.isActiveAndEnabled) return camJugador;
        Camera mejor = null;
        foreach (Camera c in Camera.allCameras)
        {
            if (c == camJugador || c.targetTexture != null) continue;
            var d = c.GetUniversalAdditionalCameraData();
            if (d != null && d.renderType == CameraRenderType.Overlay) continue;
            if (mejor == null || c.depth > mejor.depth) mejor = c;
        }
        return mejor != null ? mejor : camJugador;
    }

    void PonerCapa(Camera c)
    {
        if (camConCapa == c) return;
        QuitarCapa();
        int capa = esfera != null ? esfera.gameObject.layer : reloj.gameObject.layer;
        mascaraAntes = c.cullingMask;
        c.cullingMask |= 1 << capa;
        camConCapa = c;
    }

    void QuitarCapa()
    {
        if (camConCapa != null) camConCapa.cullingMask = mascaraAntes;
        camConCapa = null;
    }

    // Lleva el brazo (rigido, desde su pose actual) a la pose de mirar el reloj, mezclado por e (0..1).
    // Publico y estatico para poder previsualizarlo desde el editor.
    public static void PoseMirar(Transform brazo, Transform reloj, Transform camara, Vector3 posicion, float inclinacionBrazo, float inclinacionEsfera, float e)
    {
        // Pose actual (reposo) del reloj
        Vector3 pReloj = reloj.position;
        Quaternion qReloj = reloj.rotation;
        // Pose objetivo: el reloj delante de la camara, la esfera (+Y del modelo) mirando a los ojos y
        // el antebrazo (+X del modelo, del codo a la mano) cruzando hacia la derecha
        Vector3 objetivo = camara.TransformPoint(posicion);
        Vector3 normal = Quaternion.AngleAxis(-inclinacionEsfera, camara.right) * (camara.position - objetivo).normalized;
        Vector3 antebrazo = Quaternion.AngleAxis(-inclinacionBrazo, camara.forward) * camara.right;
        antebrazo = (antebrazo - normal * Vector3.Dot(antebrazo, normal)).normalized;
        Quaternion qObjetivo = Quaternion.LookRotation(Vector3.Cross(antebrazo, normal), normal);

        Quaternion delta = qObjetivo * Quaternion.Inverse(qReloj);
        Vector3 posFinal = objetivo + delta * (brazo.position - pReloj);
        Quaternion rotFinal = delta * brazo.rotation;

        // Sube desde abajo con un pequeno arco
        Vector3 arco = -camara.up * (Mathf.Sin(e * Mathf.PI) * 0.03f);
        brazo.SetPositionAndRotation(Vector3.Lerp(brazo.position, posFinal, e) + arco, Quaternion.Slerp(brazo.rotation, rotFinal, e));
    }

    // ------------------------------------------------------------------ Modelo

    void BuscarModelo()
    {
        if (reloj != null || Time.unscaledTime < proximaBusquedaModelo) return;
        proximaBusquedaModelo = Time.unscaledTime + 1f;
        Transform j = jugador != null ? jugador : HUD.Jugador();
        if (j == null) return;
        foreach (Transform t in j.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != NombreReloj) continue;
            reloj = t;
            break;
        }
        if (reloj == null) return;
        for (Transform p = reloj.parent; p != null; p = p.parent)
        {
            if (p.name == HuesoBrazo) brazo = p;
            Camera c = p.GetComponent<Camera>();
            if (c != null) { camara = p; camJugador = c; break; }
        }
        if (brazo == null || camara == null) { reloj = null; brazo = null; return; }
        brazoPos = brazo.localPosition;
        brazoRot = brazo.localRotation;
        aguHora = BuscarHijo(reloj, NombreAgujaHora);
        aguMinuto = BuscarHijo(reloj, NombreAgujaMinuto);
        Transform es = BuscarHijo(reloj, NombreEsfera);
        esfera = es != null ? es.GetComponent<Renderer>() : null;
        if (esfera != null && esfera.sharedMaterial != null && esfera.sharedMaterial.HasProperty("_BaseColor"))
            esferaColor = esfera.sharedMaterial.GetColor("_BaseColor");
        Transform b = BuscarHijo(reloj, NombreBrillo);
        brillo = b != null ? b.GetComponent<Light>() : null;
    }

    static Transform BuscarHijo(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }

    // ------------------------------------------------------------------ Entidades

    void BuscarEntidades()
    {
        if (Time.time < proximaBusqueda) return;
        proximaBusqueda = Time.time + 1.5f;
        entidades.Clear();
        foreach (JefeIndigoAI e in FindObjectsByType<JefeIndigoAI>(FindObjectsSortMode.None)) entidades.Add(e.transform);
        foreach (EstatuaAI e in FindObjectsByType<EstatuaAI>(FindObjectsSortMode.None)) entidades.Add(e.transform);
        zonas = FindObjectsByType<ZonaVolumen>(FindObjectsSortMode.None);
        if (jugador == null) jugador = HUD.Jugador();
    }

    float CalcularCercania()
    {
        if (jugador == null) return 0f;
        // Solo cuentan las entidades del area (ZonaVolumen) en la que esta el jugador
        zonasJugador.Clear();
        foreach (ZonaVolumen z in zonas) if (z != null && z.isActiveAndEnabled && z.JugadorDentro) zonasJugador.Add(z);
        if (zonasJugador.Count == 0) return 0f;
        float menor = float.MaxValue;
        foreach (Transform e in entidades)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            bool mismaZona = false;
            foreach (ZonaVolumen z in zonasJugador) if (z.Contiene(e.position)) { mismaZona = true; break; }
            if (!mismaZona) continue;
            float d = Vector3.Distance(jugador.position, e.position);
            if (d < menor) menor = d;
        }
        if (menor == float.MaxValue) return 0f;
        return Mathf.Clamp01(Mathf.InverseLerp(distanciaMaxima, distanciaMinima, menor));
    }

    // ------------------------------------------------------------------ Agujas y esfera

    void Agujas(float dt)
    {
        float k = Cercania;
        float minutos = hora * 60f;
        // Angulo en sentido horario desde las 12
        float normalMin = (minutos % 60f) * 6f;
        float normalHora = (hora % 12f) * 30f;

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
            anguloMinuto += (velocidad * sentido) * dt + tiron * dt * 10f;
            anguloHora -= (velocidad * 0.37f * sentido) * dt;
        }

        // En el modelo las agujas apuntan a las 6 (-Z); giran sobre el eje de la esfera (+Y), horario visto de frente
        if (aguMinuto != null) aguMinuto.localRotation = Quaternion.Euler(0f, 180f + anguloMinuto, 0f);
        if (aguHora != null) aguHora.localRotation = Quaternion.Euler(0f, 180f + anguloHora, 0f);

        // La esfera se pone roja y brilla, con pulso
        float pulso = k * (0.6f + 0.4f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 22f, k)));
        if (esfera != null)
        {
            if (bloque == null) bloque = new MaterialPropertyBlock();
            esfera.GetPropertyBlock(bloque);
            // Brillo suave: con mucha emision el bloom tapa las agujas
            bloque.SetColor("_BaseColor", Color.Lerp(esferaColor, new Color(0.7f, 0.2f, 0.16f), k * 0.85f));
            bloque.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.03f) * (0.35f * pulso));
            esfera.SetPropertyBlock(bloque);
        }
        if (brillo != null)
        {
            brillo.enabled = pulso > 0.01f && subida > 0.01f;
            brillo.intensity = pulso * 0.2f;
        }
    }

    void Textos(float dt)
    {
        // Tutorial
        bool hayTutorial = !string.IsNullOrEmpty(tutorialMensaje);
        if (hayTutorial) tutorialTexto.text = tutorialMensaje;
        tutorialAlfa = Mathf.MoveTowards(tutorialAlfa, hayTutorial ? 1f : 0f, dt * 3f);
        tutorialGrupo.alpha = tutorialAlfa * (0.75f + 0.25f * Mathf.Sin(Time.time * 4f));

        pistaTiempo -= dt;
        pistaGrupo.alpha = Mathf.MoveTowards(pistaGrupo.alpha, pistaTiempo > 0f && subida > 0.6f ? 1f : 0f, dt * 3f);
    }

    // Cuanto se oye segun lo levantado que esta: guardado muy bajo (y apagado por el filtro), mirandolo normal
    float FactorVolumen() => Mathf.Lerp(volumenGuardado, 1f, Mathf.Clamp01(subida * 1.5f));

    // Guardado suena apagado (sin agudos), como tapado por la manga; levantado, claro
    void Filtro()
    {
        if (filtroManga == null)
        {
            filtroManga = FuenteReloj().GetComponent<AudioLowPassFilter>();
            if (filtroManga == null) filtroManga = FuenteReloj().AddComponent<AudioLowPassFilter>();
        }
        filtroManga.cutoffFrequency = Mathf.Lerp(1400f, 22000f, Mathf.Clamp01(subida * 1.5f));
    }

    AudioLowPassFilter filtroManga;
    GameObject fuenteReloj;

    // Objeto propio para el sonido del reloj (el filtro de "manga" solo le afecta a el)
    GameObject FuenteReloj()
    {
        if (fuenteReloj == null)
        {
            fuenteReloj = new GameObject("Sonido Reloj");
            fuenteReloj.transform.SetParent(transform, false);
        }
        return fuenteReloj;
    }

    void Sonido()
    {
        bool puede = Desbloqueado && Time.timeScale > 0f && !PantallaMuerte.Muerto && !HUD.Oculto && (volumenGuardado > 0f || Visible);
        if (puede) Filtro();
        if (sonidoManecillas != null) { SonidoManecillas(puede); return; }
        if (!puede || (!Visible && volumenGuardado <= 0f)) return;
        float k = Cercania;
        float intervalo = k > 0.01f ? Mathf.Lerp(0.35f, 0.045f, k) : 1f;
        if (Time.time < proximoTic) return;
        proximoTic = Time.time + intervalo * (k > 0.5f ? Random.Range(0.6f, 1.4f) : 1f);
        alterna = !alterna;
        audioTic.pitch = 1f + k * Random.Range(-0.15f, 0.3f);
        audioTic.PlayOneShot(alterna ? tic : tac, volumenTic * (0.35f + 0.65f * Mathf.Max(0.3f, k)) * FactorVolumen());
    }

    // "reloj manecillas" en bucle mientras el reloj esta levantado: con una entidad cerca se acelera y tiembla
    void SonidoManecillas(bool puede)
    {
        if (audioManecillas == null)
        {
            audioManecillas = FuenteReloj().AddComponent<AudioSource>();
            audioManecillas.playOnAwake = false;
            audioManecillas.loop = true;
            audioManecillas.spatialBlend = 0f;
            audioManecillas.clip = sonidoManecillas;
        }
        bool sonar = puede && (subida > 0.05f || volumenGuardado > 0f);
        if (!sonar)
        {
            if (audioManecillas.isPlaying) audioManecillas.Pause();
            return;
        }
        if (!audioManecillas.isPlaying)
        {
            // Cada vez desde un punto distinto del audio, para que no suene siempre igual
            if (audioManecillas.time <= 0f || Random.value < 0.5f) audioManecillas.time = Random.Range(0f, Mathf.Max(0f, sonidoManecillas.length - 5f));
            audioManecillas.Play();
        }
        float k = Cercania;
        float temblor = k > 0.5f ? (Mathf.PerlinNoise(Time.time * 6f, 4.2f) - 0.5f) * 0.6f * k : 0f;
        audioManecillas.pitch = Mathf.Lerp(1f, Mathf.Max(1f, aceleracionManecillas), k * k) + temblor;
        audioManecillas.volume = volumenTic * FactorVolumen() * (0.6f + 0.4f * k);
    }

    // ------------------------------------------------------------------ UI (solo textos; el reloj es el modelo 3D)

    void CrearUI()
    {
        canvas = HUD.CrearCanvas("HUD Reloj", 45);
        var g = canvas.gameObject.AddComponent<CanvasGroup>();
        g.interactable = false;
        g.blocksRaycasts = false;

        // Pista a la derecha del reloj
        RectTransform pistaR = HUD.Rect(canvas.transform, "Pista", new Vector2(0.5f, 0.5f), new Vector2(420f, -40f), new Vector2(520f, 110f));
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
