using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Cuando una muñeca de las Cloacas (EstatuaAI) alcanza al jugador no lo mata de una: se le tira a la cara como los
// Denizens del Call of Duty (salta desde donde esta y se le agarra a la cabeza: se le ve la cara de frente, encima,
// sacudiendose, y le tapa la vista por los bordes) y empieza un forcejeo al estilo de las "pruebas de habilidad" de
// Dead by Daylight:
// - Cada tanto suena un aviso y aparece un dial con una aguja que gira y una zona marcada: hay que presionar
//   ESPACIO cuando la aguja esta dentro de la zona (al principio de la zona, la parte mas clara, es "perfecto").
//   Si se presiona fuera de la zona, o la aguja da la vuelta entera sin presionar, es un fallo.
// - Con "aciertosParaSoltarse" aciertos se la quita de encima y la muñeca se rompe (si traia la llave, la suelta).
//   Con "fallosParaMorir" fallos, mata al jugador (PantallaMuerte).
// - Mientras dura y durante "segundosDeGracia" despues, no aparecen muñecas nuevas (AparicionMunecas) y las demas
//   no se mueven ni pueden agarrarlo (EstatuaAI): nunca se monta otra al instante.
// Lo empieza PantallaMuerte cuando una EstatuaAI atrapa al jugador; no hace falta ponerlo en la escena.
public class AgarreMuneca : MonoBehaviour
{
    public static AgarreMuneca Actual { get; private set; }
    public static bool Activo => Actual != null;
    // Hasta cuando (Time.time) dura la gracia despues de soltarse
    public static float FinGracia { get; private set; } = -1f;
    // Mientras haya una montada o dure la gracia: ni aparecen nuevas ni se mueve ninguna
    public static bool Bloquea => Activo || Time.time < FinGracia;
    public static EstatuaAI Montada => Actual != null ? Actual.muneca : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { Actual = null; FinGracia = -1f; }

    [Header("Forcejeo")]
    public int aciertosParaSoltarse = 3;
    public int fallosParaMorir = 2;
    [Tooltip("Segundos (al azar) entre una prueba y la siguiente")]
    public Vector2 espera = new Vector2(0.5f, 1.1f);
    [Tooltip("Grados por segundo de la aguja (sube con cada acierto)")]
    public float velocidad = 330f, velocidadPorAcierto = 45f;
    [Tooltip("Grados de la zona buena: la primera prueba y la ultima")]
    public Vector2 zona = new Vector2(62f, 40f);
    [Tooltip("Grados del principio de la zona que cuentan como perfecto")]
    public float perfecto = 12f;
    [Tooltip("Segundos sin muñecas nuevas ni movimiento despues de soltarse (o de morir)")]
    public float segundosDeGracia = 7f;

    [Header("La muñeca en la cara (como un Denizen)")]
    [Tooltip("Donde queda la CABEZA de la muñeca respecto a la camara (un poco abajo del centro, cerca)")]
    public Vector3 cabeza = new Vector3(0f, -0.05f, 0.42f);
    [Tooltip("Giro de la muñeca respecto a la camara: de frente (180) e inclinada hacia el jugador")]
    public Vector3 giroPose = new Vector3(20f, 180f, 0f);
    [Tooltip("Tamaño mientras esta encima (entera no entraria: se veria el pecho metido en la camara)")]
    public float escalaEncima = 0.7f;
    [Tooltip("Segundos del salto hasta la cara")]
    public float salto = 0.32f;
    [Tooltip("Cuanto se oscurecen los bordes de la pantalla (tapa la vista)")]
    [Range(0f, 1f)] public float vistaTapada = 0.65f;

    [Header("Textos")]
    [TextArea] public string alMontarse = "¡Se me subió encima! ¡Tengo que quitármela!";
    [TextArea] public string alSoltarse = "Casi... No puedo dejar que otra me agarre.";
    public string titulo = "¡QUÍTATELA DE ENCIMA!";

    EstatuaAI muneca;
    readonly List<Behaviour> apagados = new List<Behaviour>();
    readonly List<Collider> colliders = new List<Collider>();
    Transform cuerpo, padreOriginal;
    Vector3 escalaOriginal, escalaPose, posPose;
    Quaternion giro;
    Camera cam;
    float empuje;          // + se aleja (acierto), - se acerca (fallo)
    int fallos;
    AudioSource fuente;
    static AudioClip clipAviso, clipBien, clipMal;

    // UI
    GameObject ui;
    CanvasGroup dial;
    RectTransform aguja, zonaR, perfectoR;
    Image zonaImg, perfectoImg, rojo, tapada;
    Image[] marcas;
    TextMeshProUGUI tituloT;

    // Lo llama PantallaMuerte al atrapar una EstatuaAI. false = no se pudo (ya hay otra, o ya esta muerto)
    public static bool Empezar(EstatuaAI m)
    {
        if (m == null || Activo || PantallaMuerte.Muerto || Camera.main == null) return false;
        var go = new GameObject("Agarre de muñeca");
        var a = go.AddComponent<AgarreMuneca>();
        a.muneca = m;
        Actual = a;
        return true;
    }

    void Start()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.spatialBlend = 0f;
        fuente.playOnAwake = false;
        StartCoroutine(Forcejeo());
    }

    void OnDestroy()
    {
        if (Actual == this) Actual = null;
        if (ui != null) Destroy(ui);
    }

    IEnumerator Forcejeo()
    {
        cam = Camera.main;
        Transform jugador = HUD.Jugador();

        // El jugador queda quieto (puede seguir con la linterna)
        Apagar<PlayerMovement>();
        Apagar<MouseLook>();
        Apagar<HeadBob>();
        Apagar<PlayerInteractor>();
        Apagar<PasoBajo>();
        if (jugador != null && jugador.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = Vector3.zero;

        // La muñeca: sin IA ni colisiones, pegada a la camara
        cuerpo = muneca.transform;
        muneca.enabled = false;
        if (cuerpo.TryGetComponent(out NavMeshAgent ag)) ag.enabled = false;
        foreach (Collider c in cuerpo.GetComponentsInChildren<Collider>())
            if (c.enabled && c.GetComponentInParent<LlaveAleatoria>() == null) { c.enabled = false; colliders.Add(c); }
        // Donde tiene la cabeza (el centro del pelo, o lo alto del cuerpo), en su propio espacio
        Vector3 cabezaLocal = CabezaLocal(cuerpo);
        padreOriginal = cuerpo.parent;
        escalaOriginal = cuerpo.localScale;
        cuerpo.SetParent(cam.transform, true);
        Vector3 desde = cuerpo.localPosition;
        Quaternion desdeGiro = cuerpo.localRotation;
        Vector3 desdeEscala = cuerpo.localScale;
        giro = Quaternion.Euler(giroPose);
        escalaPose = desdeEscala * escalaEncima;
        // La raiz queda donde haga falta para que la cabeza (ya achicada y girada) caiga en "cabeza"
        posPose = cabeza - giro * Vector3.Scale(cabezaLocal, escalaPose);

        CrearUI();
        Sonar(Golpe());
        if (!string.IsNullOrEmpty(alMontarse)) Pensamiento.Decir("agarre.montarse", 0.15f, alMontarse);
        // Salta a la cara: en arco, achicandose y girandose hacia el jugador; al llegar, el golpe
        for (float t = 0f; t < 1f && cuerpo != null; t += Time.deltaTime / Mathf.Max(0.05f, salto))
        {
            float k = Jugo.SalidaAtras(Mathf.Clamp01(t), 1.2f);
            cuerpo.localPosition = Vector3.LerpUnclamped(desde, posPose, k) + Vector3.up * (Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.25f);
            cuerpo.localRotation = Quaternion.SlerpUnclamped(desdeGiro, giro, k);
            cuerpo.localScale = Vector3.LerpUnclamped(desdeEscala, escalaPose, Mathf.Clamp01(t * 1.4f));
            tapada.color = new Color(0f, 0f, 0f, vistaTapada * Mathf.Clamp01(t));
            yield return null;
        }
        if (cuerpo != null) cuerpo.localScale = escalaPose;
        Jugo.Sacudir(0.6f);
        Jugo.PatadaFOV(-8f);
        int bien = 0;
        while (cuerpo != null && bien < aciertosParaSoltarse && fallos < fallosParaMorir)
        {
            // Forcejeando un rato hasta la siguiente prueba
            for (float t = Random.Range(espera.x, espera.y); t > 0f && cuerpo != null; t -= Time.deltaTime) { Temblar(); yield return null; }
            if (cuerpo == null) break;

            // La prueba: aviso, y la aguja da una vuelta
            float k = aciertosParaSoltarse > 1 ? bien / (float)(aciertosParaSoltarse - 1) : 0f;
            float ancho = Mathf.Lerp(zona.x, zona.y, k);
            float inicio = Random.Range(110f, 340f - ancho);
            float vel = velocidad + velocidadPorAcierto * bien;
            MostrarDial(inicio, ancho);
            Sonar(Aviso());
            float ang = 0f;
            bool exito = false, perfecta = false, listo = false;
            while (!listo && cuerpo != null)
            {
                bool pausa = Time.timeScale <= 0f || PauseMenu.IsPaused;
                if (!pausa)
                {
                    ang += vel * Time.deltaTime;
                    aguja.localRotation = Quaternion.Euler(0f, 0f, -ang);
                    if (GameInput.Forcejear)
                    {
                        listo = true;
                        exito = ang >= inicio && ang <= inicio + ancho;
                        perfecta = exito && ang <= inicio + perfecto;
                    }
                    else if (ang >= 360f) listo = true;
                }
                Temblar();
                yield return null;
            }
            if (cuerpo == null) break;

            if (exito)
            {
                bien += perfecta ? Mathf.Min(2, aciertosParaSoltarse - bien) : 1;
                empuje = Mathf.Min(empuje + 0.06f, 0.18f);
                Sonar(Bien());
                Jugo.Sacudir(perfecta ? 0.3f : 0.18f);
                Jugo.PatadaFOV(perfecta ? 3f : 1.5f);
            }
            else
            {
                fallos++;
                empuje = Mathf.Max(empuje - 0.08f, -0.16f);
                Sonar(Mal());
                Jugo.Sacudir(0.5f);
                Jugo.PatadaFOV(-4f);
            }
            Marcas(bien);
            yield return Respuesta(exito, perfecta);
        }

        if (cuerpo == null || bien >= aciertosParaSoltarse || PantallaMuerte.Invencible) Soltarse();
        else Matar();
    }

    // La muñeca en la cara: se sacude como si arañara (de lado a lado), cada vez mas con cada fallo, y se acerca o se
    // aleja segun el forcejeo
    void Temblar()
    {
        if (cuerpo == null) return;
        float f = 0.012f + 0.01f * fallos;
        Vector3 ruido = new Vector3(Mathf.PerlinNoise(Time.time * 13f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, Time.time * 11f) - 0.5f, 0f) * 2f * f;
        float arana = Mathf.Sin(Time.time * (9f + 3f * fallos)) * (6f + 4f * fallos);
        cuerpo.localPosition = posPose + Vector3.forward * empuje + ruido;
        cuerpo.localRotation = giro * Quaternion.Euler(ruido.y * 260f, arana * 0.4f, ruido.x * 300f + arana);
        if (tapada != null) tapada.color = new Color(0f, 0f, 0f, Mathf.Clamp01(vistaTapada + 0.08f * fallos - 0.15f * empuje));
    }

    // El centro de la cabeza en el espacio de la muñeca: el del pelo (PeloAleatorio) o, si no tiene, lo alto del cuerpo
    static Vector3 CabezaLocal(Transform m)
    {
        PeloAleatorio pelo = m.GetComponentInChildren<PeloAleatorio>();
        if (pelo != null && pelo.TryGetComponent(out Renderer rp)) return m.InverseTransformPoint(rp.bounds.center);
        bool hay = false;
        Bounds b = default;
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            if (r.GetComponentInParent<LlaveAleatoria>() != null) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return hay ? m.InverseTransformPoint(new Vector3(b.center.x, b.max.y - b.size.y * 0.15f, b.center.z)) : Vector3.up * 0.4f;
    }

    // De vuelta a su tamaño y a su padre (fuera de la camara)
    void Bajarla()
    {
        if (cuerpo == null) return;
        cuerpo.SetParent(padreOriginal, true);
        cuerpo.localScale = escalaOriginal;
    }

    // Se la quita de encima: sale despedida hacia adelante y se rompe (la llave, si la tenia, cae)
    void Soltarse()
    {
        Destroy(ui);
        if (cuerpo != null)
        {
            Bajarla();
            Vector3 fw = cam.transform.forward; fw.y = 0f;
            fw = fw.sqrMagnitude > 0.001f ? fw.normalized : Vector3.forward;
            cuerpo.position = cam.transform.position + fw * 1.1f - Vector3.up * 0.6f;
            Jugo.Sacudir(0.35f);
            MunecaLuz luz = cuerpo.GetComponent<MunecaLuz>();
            if (luz != null) luz.Romper();
            else Destroy(cuerpo.gameObject);
        }
        if (!string.IsNullOrEmpty(alSoltarse)) Pensamiento.Decir("agarre.soltarse", 0.6f, alSoltarse);
        Restaurar();
        FinGracia = Time.time + segundosDeGracia;
        Actual = null;
        Destroy(gameObject, 1f);
    }

    // No pudo: la muñeca queda delante de el (en el piso) y lo mata
    void Matar()
    {
        Destroy(ui);
        Transform jugador = HUD.Jugador();
        Vector3 fw = cam.transform.forward; fw.y = 0f;
        fw = fw.sqrMagnitude > 0.001f ? fw.normalized : Vector3.forward;
        Vector3 pie = (jugador != null ? jugador.position : cam.transform.position) + fw * 0.9f;
        if (Physics.Raycast(pie + Vector3.up * 1.5f, Vector3.down, out RaycastHit h, 4f, ~0, QueryTriggerInteraction.Ignore)) pie = h.point;
        Bajarla();
        NavMeshAgent ag = cuerpo.GetComponent<NavMeshAgent>();
        cuerpo.SetPositionAndRotation(pie + Vector3.up * (ag != null ? ag.baseOffset : 0f), Quaternion.LookRotation(-fw, Vector3.up));
        foreach (Collider c in colliders) if (c != null) c.enabled = true;
        // Con su agente e IA de vuelta (sigue "atrapo"): al reintentar PantallaMuerte la manda a donde aparecio
        if (ag != null) { ag.enabled = true; if (ag.isOnNavMesh) ag.Warp(cuerpo.position); }
        muneca.enabled = true;
        Restaurar();
        FinGracia = Time.time + segundosDeGracia;
        Actual = null;
        if (PantallaMuerte.Instance != null) PantallaMuerte.Instance.Morir(cuerpo);
        Destroy(gameObject, 1f);
    }

    void Apagar<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
            if (b.enabled) { b.enabled = false; apagados.Add(b); }
    }

    void Restaurar()
    {
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
        apagados.Clear();
    }

    void Sonar(AudioClip c) { if (c != null && fuente != null) fuente.PlayOneShot(c); }

    // ------------------------------------------------------------------ UI (generada por codigo)

    void CrearUI()
    {
        ui = new GameObject("UI Agarre");
        var canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350;
        var cs = ui.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 1f;

        rojo = Img(ui.transform, "Rojo", Blanco(), new Color(0.7f, 0f, 0f, 0f));
        var rr = rojo.rectTransform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
        rojo.raycastTarget = false;
        // Los bordes negros: la tiene en la cara y no ve casi nada alrededor
        tapada = Img(ui.transform, "Vista tapada", Vineta(), new Color(0f, 0f, 0f, 0f));
        var rt = tapada.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        tapada.transform.SetAsFirstSibling();

        tituloT = Texto(ui.transform, titulo, 40f, new Vector2(0f, 300f), new Vector2(1000f, 60f));
        tituloT.fontStyle = FontStyles.Bold;

        // El dial (como en Dead by Daylight)
        var raiz = new GameObject("Dial", typeof(RectTransform)).GetComponent<RectTransform>();
        raiz.SetParent(ui.transform, false);
        raiz.anchoredPosition = new Vector2(0f, -40f);
        raiz.sizeDelta = new Vector2(250f, 250f);
        dial = raiz.gameObject.AddComponent<CanvasGroup>();
        dial.alpha = 0f;
        Sprite anillo = Anillo();
        Img(raiz, "Fondo", anillo, new Color(1f, 1f, 1f, 0.28f)).rectTransform.sizeDelta = raiz.sizeDelta;
        zonaImg = Img(raiz, "Zona", anillo, new Color(1f, 1f, 1f, 0.9f));
        zonaR = zonaImg.rectTransform; zonaR.sizeDelta = raiz.sizeDelta;
        Relleno(zonaImg);
        perfectoImg = Img(raiz, "Perfecto", anillo, new Color(1f, 0.85f, 0.35f, 1f));
        perfectoR = perfectoImg.rectTransform; perfectoR.sizeDelta = raiz.sizeDelta * 1.06f;
        Relleno(perfectoImg);
        var ag = Img(raiz, "Aguja", Blanco(), new Color(0.95f, 0.12f, 0.08f));
        aguja = ag.rectTransform;
        aguja.pivot = new Vector2(0.5f, 0f);
        aguja.sizeDelta = new Vector2(6f, 128f);
        aguja.anchoredPosition = Vector2.zero;
        Texto(raiz, GameInput.TeclaForcejear, 26f, Vector2.zero, new Vector2(200f, 40f));

        // Cuantos aciertos van
        marcas = new Image[aciertosParaSoltarse];
        for (int i = 0; i < marcas.Length; i++)
        {
            marcas[i] = Img(ui.transform, "Marca", Blanco(), new Color(1f, 1f, 1f, 0.25f));
            marcas[i].rectTransform.sizeDelta = new Vector2(26f, 26f);
            marcas[i].rectTransform.anchoredPosition = new Vector2((i - (marcas.Length - 1) * 0.5f) * 42f, -210f);
            marcas[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
    }

    void MostrarDial(float inicio, float ancho)
    {
        zonaImg.fillAmount = ancho / 360f;
        zonaR.localRotation = Quaternion.Euler(0f, 0f, -inicio);
        zonaImg.color = new Color(1f, 1f, 1f, 0.9f);
        perfectoImg.fillAmount = Mathf.Min(perfecto, ancho) / 360f;
        perfectoR.localRotation = Quaternion.Euler(0f, 0f, -inicio);
        aguja.localRotation = Quaternion.identity;
        dial.alpha = 1f;
    }

    void Marcas(int bien)
    {
        for (int i = 0; i < marcas.Length; i++) marcas[i].color = i < bien ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(1f, 1f, 1f, 0.25f);
    }

    // La zona se pinta de verde (o rojo) un momento y el dial se apaga
    IEnumerator Respuesta(bool exito, bool perfecta)
    {
        zonaImg.color = exito ? (perfecta ? new Color(1f, 0.85f, 0.35f) : new Color(0.45f, 1f, 0.45f)) : new Color(1f, 0.25f, 0.2f);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            if (!exito) rojo.color = new Color(0.7f, 0f, 0f, 0.45f * (1f - t / 0.35f));
            dial.alpha = 1f - Mathf.Clamp01((t - 0.15f) / 0.2f);
            Temblar();
            yield return null;
        }
        rojo.color = new Color(0.7f, 0f, 0f, 0f);
        dial.alpha = 0f;
    }

    static void Relleno(Image i)
    {
        i.type = Image.Type.Filled;
        i.fillMethod = Image.FillMethod.Radial360;
        i.fillOrigin = (int)Image.Origin360.Top;
        i.fillClockwise = true;
    }

    static Image Img(Transform padre, string nombre, Sprite s, Color c)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var i = go.GetComponent<Image>();
        i.sprite = s;
        i.color = c;
        i.raycastTarget = false;
        return i;
    }

    static TextMeshProUGUI Texto(Transform padre, string s, float tam, Vector2 pos, Vector2 tamano)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = s;
        t.fontSize = tam;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        var r = (RectTransform)go.transform;
        r.anchoredPosition = pos;
        r.sizeDelta = tamano;
        return t;
    }

    static Sprite blanco, anillo;
    static Sprite Blanco()
    {
        if (blanco == null) blanco = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        return blanco;
    }

    // Negro en los bordes y transparente en el centro (se estira a toda la pantalla)
    static Sprite vineta;
    static Sprite Vineta()
    {
        if (vineta != null) return vineta;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f).magnitude / 0.5f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.05f, r));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        vineta = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f));
        return vineta;
    }

    // Un aro blanco con el borde suave
    static Sprite Anillo()
    {
        if (anillo != null) return anillo;
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude / (n / 2f);
                float a = Mathf.Clamp01((r - 0.8f) / 0.02f) * Mathf.Clamp01((0.98f - r) / 0.02f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        anillo = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f));
        return anillo;
    }

    // ------------------------------------------------------------------ Sonidos (generados por codigo)

    static AudioClip Clip(string nombre, float largo, System.Func<float, System.Random, float> f)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * largo);
        var d = new float[n];
        var rnd = new System.Random(nombre.Length * 31);
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)rate, rnd), -1f, 1f);
        var c = AudioClip.Create(nombre, n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    // Aviso de la prueba: un tic agudo y seco
    static AudioClip Aviso() => clipAviso != null ? clipAviso : clipAviso = Clip("Aviso agarre", 0.09f,
        (t, r) => Mathf.Sin(2f * Mathf.PI * 1850f * t) * Mathf.Exp(-t * 55f) * 0.6f);

    // Acierto: un empujon sordo
    static AudioClip Bien() => clipBien != null ? clipBien : clipBien = Clip("Acierto agarre", 0.25f,
        (t, r) => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 70f, t * 4f) * t) * 0.8f + ((float)r.NextDouble() * 2f - 1f) * 0.25f) * Mathf.Exp(-t * 16f));

    // Fallo: un chirrido aspero (la porcelana raspando)
    static AudioClip Mal() => clipMal != null ? clipMal : clipMal = Clip("Fallo agarre", 0.45f,
        (t, r) => (((float)r.NextDouble() * 2f - 1f) * 0.55f + (Mathf.Repeat(t * 520f, 1f) * 2f - 1f) * 0.35f) * Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 200f));

    // Al montarse: golpe grave con ruido
    static AudioClip clipGolpe;
    static AudioClip Golpe() => clipGolpe != null ? clipGolpe : clipGolpe = Clip("Golpe agarre", 0.6f,
        (t, r) => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 45f, Mathf.Clamp01(t * 3f)) * t) + ((float)r.NextDouble() * 2f - 1f) * 0.5f * Mathf.Exp(-t * 12f)) * Mathf.Exp(-t * 5f) * 0.9f);
}
