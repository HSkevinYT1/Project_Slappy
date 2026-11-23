using UnityEngine;

// Brujula de la Zona Roja (inspirada en la de PEAK): no apunta al norte, sino a lo siguiente de la mision del
// faro (MisionFaro): el diario, la nota sin leer mas cercana, el bidon, la luz del faro y al final la salida.
// - En la mano el dial se gira hacia el jugador; manteniendo clic derecho (Mirar) se la sube delante de la cara.
// - La aguja tiene inercia (resorte): al girar se pasa, vuelve y tiembla hasta asentarse.
// - Si el enemigo (JefeIndigoAI) esta cerca, la aguja se vuelve loca. Fuera del faro da vueltas sin rumbo.
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

    [Header("Enemigo cerca")]
    public float distanciaInterferencia = 14f;

    [Header("En la mano")]
    [Tooltip("Grados que se inclina el dial hacia el jugador (0 = horizontal)")]
    public float inclinacion = 45f;
    [Tooltip("Al mirarla (clic derecho): donde queda respecto a la camara")]
    public Vector3 mirando = new Vector3(0f, -0.1f, 0.28f);
    public float inclinacionMirando = 68f;
    public float transicion = 0.25f;

    [Header("Textos")]
    [TextArea] public string alAgarrar = "Una brújula... pero no apunta al norte. Apunta a otra cosa.";
    [TextArea] public string alEnloquecer = "La aguja se volvió loca... algo está cerca.";
    public string ayuda = "Mantén clic derecho para mirar la brújula";

    Grabbable grab;
    PlayerInteractor jugador;
    float angulo, velocidad, yawAntes;
    float k;
    bool moviendo, usado, dicha;
    Vector3 manoPos;
    float loca, buscarEnemigo;
    JefeIndigoAI[] enemigos = new JefeIndigoAI[0];

    void Awake()
    {
        grab = GetComponent<Grabbable>();
        if (aguja == null) aguja = transform.Find("Aguja");
        yawAntes = transform.eulerAngles.y;
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
        if (enMano && !dicha) { dicha = true; Pensamiento.Decir("brujula.agarrar", 0.4f, alAgarrar); }

        Pose(enMano, cam);
        Aguja();
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

        bool bloqueado = PauseMenu.IsPaused || MenuPrincipal.Abierto || Time.timeScale <= 0f
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

    void Aguja()
    {
        if (aguja == null) return;
        float dt = Mathf.Max(0.0001f, Time.deltaTime);

        // Angulo (en el suelo) entre el frente de la brujula y el objetivo
        Vector3 frente = transform.forward; frente.y = 0f;
        if (frente.sqrMagnitude < 0.0001f) { frente = -transform.up; frente.y = 0f; }
        Vector3 hacia = Objetivo(out bool hay) - transform.position; hacia.y = 0f;
        float deseado = hay && hacia.sqrMagnitude > 0.01f
            ? Vector3.SignedAngle(frente, hacia, Vector3.up)
            : angulo + 35f + Mathf.Sin(Time.time * 0.6f) * 50f;   // sin rumbo: gira despacio

        // Enemigo cerca: la aguja enloquece
        float antes = loca;
        loca = Mathf.MoveTowards(loca, Interferencia(), dt * 1.5f);
        if (loca > 0f)
            deseado += (Mathf.PerlinNoise(Time.time * 2.5f, 3.7f) - 0.5f) * 900f * loca + Mathf.Sin(Time.time * 11f) * 40f * loca;
        if (antes < 0.35f && loca >= 0.35f && EnMano()) Pensamiento.Decir("brujula.loca", 0.2f, alEnloquecer);

        // Inercia: si el jugador gira, la aguja se queda atras y despues lo alcanza
        float yaw = transform.eulerAngles.y;
        float giro = Mathf.DeltaAngle(yawAntes, yaw);
        yawAntes = yaw;
        angulo -= giro * Mathf.Clamp01(inercia);

        // Resorte amortiguado hacia el angulo deseado (temblor extra cuando esta loca)
        float error = Mathf.DeltaAngle(angulo, deseado);
        velocidad += (error * rigidez - velocidad * amortiguacion) * dt;
        if (loca > 0f) velocidad += (Mathf.PerlinNoise(Time.time * 17f, 0.4f) - 0.5f) * 3000f * loca * dt;
        angulo = Mathf.Repeat(angulo + velocidad * dt + 180f, 360f) - 180f;
        aguja.localRotation = Quaternion.Euler(0f, angulo, 0f);
    }

    // Lo siguiente de la mision del faro
    Vector3 Objetivo(out bool hay)
    {
        hay = true;
        MisionFaro m = MisionFaro.Instance;
        if (m == null || !m.isActiveAndEnabled) { hay = false; return transform.position; }
        if (!m.DiarioLeido) return m.diario.transform.position;
        if (m.NotasLeidas < m.TotalNotas)
        {
            Transform mejor = null; float d = float.MaxValue;
            foreach (Lectura l in m.notas)
            {
                if (l == null || l.Leida || !l.gameObject.activeInHierarchy) continue;
                float dd = (l.transform.position - transform.position).sqrMagnitude;
                if (dd < d) { d = dd; mejor = l.transform; }
            }
            if (mejor != null) return mejor.position;
        }
        if (!m.Relleno)
        {
            if (!m.TieneBidon && m.bidon != null && m.bidon.gameObject.activeInHierarchy) return m.bidon.transform.position;
            if (m.deposito != null) return m.deposito.transform.position;
        }
        if (m.muroEntrada != null) return m.muroEntrada.transform.position;
        hay = false;
        return transform.position;
    }

    // 0 = nada, 1 = el enemigo esta encima
    float Interferencia()
    {
        buscarEnemigo -= Time.deltaTime;
        if (buscarEnemigo <= 0f)
        {
            buscarEnemigo = 0.5f;
            enemigos = FindObjectsByType<JefeIndigoAI>(FindObjectsSortMode.None);
        }
        float mas = 0f;
        foreach (JefeIndigoAI e in enemigos)
        {
            if (e == null || !e.isActiveAndEnabled) continue;
            float d = Vector3.Distance(e.transform.position, transform.position);
            if (d < distanciaInterferencia) mas = Mathf.Max(mas, 1f - d / distanciaInterferencia);
        }
        return mas;
    }

    void OnGUI()
    {
        if (usado || string.IsNullOrEmpty(ayuda) || MenuPrincipal.Abierto || PauseMenu.IsPaused || PanelDebug.HUDOculto || !EnMano()) return;
        var estilo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.025f * HUD.Escala) };
        estilo.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(0f, Screen.height * 0.85f, Screen.width, Screen.height * 0.06f), ayuda, estilo);
    }
}
