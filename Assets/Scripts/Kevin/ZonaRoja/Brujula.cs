using UnityEngine;

// Brujula de la Zona Roja (inspirada en la de PEAK): no apunta al norte, sino a donde dice la ultima nota leida de
// la mision del faro (MisionFaro.DestinoBrujula): la nota 1 lleva a la nota 2, la 2 a la 3, la 3 a la gasolina, y
// despues a la luz del faro y a la salida. Sin ninguna nota leida gira sin rumbo. No tiene nada que ver con el enemigo.
// - En la mano el dial se gira hacia el jugador; manteniendo clic derecho (Mirar) se la sube delante de la cara.
// - La aguja tiene inercia (resorte): al girar se pasa, vuelve y tiembla hasta asentarse.
// - En su pedestal se ve bien aunque la zona este a oscuras: brilla el contorno y la alumbra una lucecita tibia
//   (MisionFaro no la apaga) hasta que se agarra por primera vez.
// La coloca Tools > Zona Roja > Colocar brujula (BrujulaSetup).
[RequireComponent(typeof(Grabbable))]
public class Brujula : MonoBehaviour
{
    [Tooltip("Lo que gira (la aguja), alrededor de su eje Y local. Vacio = el hijo \"Aguja\"")]
    public Transform aguja;

    [Header("Aguja")]
    public float rigidez = 38f;
    public float amortiguacion = 5f;
    [Tooltip("Al girar el jugador: 1 = la aguja queda fija en el mundo (sin temblor), 0 = gira con la brujula y despues se corrige (se pasa y tiembla)")]
    public float inercia = 0.6f;

    [Header("En el pedestal")]
    [Tooltip("Tamaño (1 = el del setup, que era muy chica para verla)")]
    public float tamano = 1.5f;
    [Tooltip("Lucecita que la alumbra hasta agarrarla")]
    public Color colorLuz = new Color(1f, 0.82f, 0.55f);
    public float intensidadLuz = 1.4f;
    public float alcanceLuz = 2.2f;
    [Tooltip("Metros a los que el protagonista la nota")]
    public float distanciaVer = 6f;

    [Header("En la mano")]
    [Tooltip("Grados que se inclina el dial hacia el jugador (0 = horizontal)")]
    public float inclinacion = 45f;
    [Tooltip("Al mirarla (clic derecho): donde queda respecto a la camara")]
    public Vector3 mirando = new Vector3(0f, -0.1f, 0.28f);
    public float inclinacionMirando = 68f;
    public float transicion = 0.25f;

    [Header("Textos")]
    [TextArea] public string alVer = "¿Una brújula?";
    [TextArea] public string alAgarrar = "Una brújula... pero no apunta al norte. Apunta a otra cosa.";
    [TextArea] public string alApuntar = "La aguja se quedó quieta. Me está marcando hacia dónde ir.";
    public string ayuda = "Mantén clic derecho para mirar la brújula";

    Grabbable grab;
    PlayerInteractor jugador;
    float angulo, velocidad, yawAntes;
    float k;
    bool moviendo, usado, dicha, agarrada, teniaRumbo;
    Vector3 manoPos;
    Light luz;
    Highlightable brillo;
    float proximoVer;

    void Awake()
    {
        grab = GetComponent<Grabbable>();
        if (aguja == null) aguja = transform.Find("Aguja");
        yawAntes = transform.eulerAngles.y;
        if (tamano > 0f && !Mathf.Approximately(tamano, 1f)) transform.localScale *= tamano;

        // La lucecita del pedestal (hija de la brujula: MisionFaro.Oscuridad no apaga las luces de la brujula)
        var go = new GameObject("Luz brujula");
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position + Vector3.up * 0.35f;
        luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = colorLuz;
        luz.intensity = intensidadLuz;
        luz.range = alcanceLuz;
        luz.shadows = LightShadows.None;
        brillo = GetComponent<Highlightable>();
    }

    void Start()
    {
        // Brilla en el pedestal (Grabbable apaga el contorno al agarrarla)
        if (brillo != null && !agarrada) brillo.Show();
    }

    // Guardada en el inventario mientras se miraba: vuelve a la mano
    void OnDisable()
    {
        if (moviendo && transform.parent != null) transform.localPosition = manoPos;
        moviendo = false;
        k = 0f;
    }

    bool EnMano()
    {
        if (jugador == null) jugador = FindFirstObjectByType<PlayerInteractor>();
        return jugador != null && jugador.Held == grab;
    }

    void LateUpdate()
    {
        bool enMano = EnMano();
        Camera cam = Camera.main;
        if (enMano && !agarrada)
        {
            // Ya no hace falta la lucecita
            agarrada = true;
            if (luz != null) luz.gameObject.SetActive(false);
        }
        if (enMano && !dicha) { dicha = true; Pensamiento.Decir("brujula.agarrar", 0.4f, alAgarrar); }
        if (!agarrada) Ver(cam);

        Pose(enMano, cam);
        Aguja(enMano);
    }

    // En el pedestal: el protagonista la nota al acercarse y verla
    void Ver(Camera cam)
    {
        if (cam == null || Time.time < proximoVer || string.IsNullOrEmpty(alVer) || Pensamiento.YaDicho("brujula.ver")) return;
        proximoVer = Time.time + 0.3f;
        Vector3 d = transform.position - cam.transform.position;
        if (d.sqrMagnitude > distanciaVer * distanciaVer) return;
        Vector3 v = cam.WorldToViewportPoint(transform.position);
        if (v.z <= 0f || v.x < 0.15f || v.x > 0.85f || v.y < 0.15f || v.y > 0.85f) return;
        Pensamiento.Decir("brujula.ver", 0.1f, alVer);
    }

    // En la mano: el dial mira al jugador; con clic derecho se sube delante de la cara
    void Pose(bool enMano, Camera cam)
    {
        Transform padre = transform.parent;
        if (!enMano || cam == null || padre == null)
        {
            if (moviendo && padre != null) transform.localPosition = manoPos;
            moviendo = false;
            k = 0f;
            return;
        }

        bool bloqueado = PauseMenu.IsPaused || MenuPrincipal.Abierto || Time.timeScale <= 0f || HUD.EnCinematica
                         || (DialogueManager.Instance != null && DialogueManager.Instance.IsActive);
        bool quiere = !bloqueado && GameInput.LookThrough;
        if (quiere && k <= 0f && !moviendo)
        {
            manoPos = transform.localPosition;   // donde la dejo el Grabbable, para devolverla
            moviendo = true;
            usado = true;
        }
        k = Mathf.MoveTowards(k, quiere ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, transicion));
        float s = Mathf.SmoothStep(0f, 1f, k);

        Quaternion yaw = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        transform.rotation = yaw * Quaternion.Euler(-Mathf.Lerp(inclinacion, inclinacionMirando, s), 0f, 0f);

        if (moviendo)
        {
            Vector3 desde = padre.TransformPoint(manoPos);
            Vector3 hasta = cam.transform.TransformPoint(mirando);
            transform.position = Vector3.Lerp(desde, hasta, s);
            if (k <= 0f) { transform.localPosition = manoPos; moviendo = false; }
        }
    }

    void Aguja(bool enMano)
    {
        if (aguja == null) return;
        float dt = Mathf.Max(0.0001f, Time.deltaTime);

        // Angulo (en el suelo) entre el frente de la brujula y el objetivo
        Vector3 frente = transform.forward; frente.y = 0f;
        if (frente.sqrMagnitude < 0.0001f) { frente = -transform.up; frente.y = 0f; }
        MisionFaro m = MisionFaro.Instance;
        Vector3 destino = default;
        bool hay = m != null && m.isActiveAndEnabled && m.DestinoBrujula(out destino);
        Vector3 hacia = hay ? destino - transform.position : Vector3.zero;
        hacia.y = 0f;
        hay &= hacia.sqrMagnitude > 0.01f;
        float deseado = hay
            ? Vector3.SignedAngle(frente, hacia, Vector3.up)
            : angulo + 35f + Mathf.Sin(Time.time * 0.6f) * 50f;   // sin rumbo: gira despacio

        // La primera vez que una nota le da rumbo (con la brujula en la mano), el protagonista lo nota
        if (hay && !teniaRumbo && enMano && !string.IsNullOrEmpty(alApuntar)) Pensamiento.Decir("brujula.apunta", 1.2f, alApuntar);
        if (enMano) teniaRumbo = hay;

        // Inercia: si el jugador gira, la aguja se queda atras y despues lo alcanza
        float yaw = transform.eulerAngles.y;
        float giro = Mathf.DeltaAngle(yawAntes, yaw);
        yawAntes = yaw;
        angulo -= giro * Mathf.Clamp01(inercia);

        // Resorte amortiguado hacia el angulo deseado
        float error = Mathf.DeltaAngle(angulo, deseado);
        velocidad += (error * rigidez - velocidad * amortiguacion) * dt;
        angulo = Mathf.Repeat(angulo + velocidad * dt + 180f, 360f) - 180f;
        aguja.localRotation = Quaternion.Euler(0f, angulo, 0f);
    }

    void OnGUI()
    {
        if (usado || string.IsNullOrEmpty(ayuda) || MenuPrincipal.Abierto || PauseMenu.IsPaused || PanelDebug.HUDOculto || !EnMano()) return;
        var estilo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.025f * HUD.Escala) };
        estilo.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(0f, Screen.height * 0.85f, Screen.width, Screen.height * 0.06f), ayuda, estilo);
    }
}
