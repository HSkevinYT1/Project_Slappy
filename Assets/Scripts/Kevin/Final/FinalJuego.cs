using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// El final del guion: con las 4 zonas completadas, al volver a la estacion los muros empiezan a cerrarse y se abre
// un agujero en el piso (la unica salida). Al tirarse empieza la cinematica final: una serie de imagenes
// (Assets/Resources/FinalRenders). Final normal: Prim, Seg y Terc (lo atrapan). Si lleva las 4 piedras
// coleccionables (PiedraColeccionable), el final secreto: Prim, Cuart y Quinto (las gemas lo salvan y sale).
// En los dos suena la cancion de Slappy (Resources/FinalRenders/slappy) y se callan los demas sonidos.
// Se crea solo al jugar; las imagenes, textos y tiempos se editan en el objeto "Final del juego" (en Play) o aqui.
public class FinalJuego : MonoBehaviour
{
    [Header("Muros y agujero")]
    [Tooltip("Segundos que tardan los muros en cerrarse")]
    public float duracionCierre = 28f;
    [Tooltip("Hasta donde llegan los muros (metros desde el agujero)")]
    public float distanciaFinal = 3.6f;
    public float alturaMuros = 7f;
    public float radioAgujero = 1.9f;
    [TextArea] public string alTemblar = "¿Y ahora qué está pasando?";
    [TextArea] public string alAbrirAgujero = "No me queda otra... tengo que tirarme.";

    const string CarpetaRenders = "FinalRenders";   // Assets/Resources/FinalRenders

    [Header("Cinematica (imagenes de Resources/FinalRenders, sin extension)")]
    [Tooltip("Segundos que cada imagen queda a la vista (sin contar los fundidos)")]
    public float segundosPorImagen = 5f;
    public float fundido = 1.2f;
    [Tooltip("Cuanto se acerca la camara a cada imagen mientras se ve (1 = nada)")]
    public float zoomImagen = 1.06f;
    [Tooltip("Cancion de los dos finales (Resources/FinalRenders, sin extension)")]
    public string cancion = "slappy";
    [Range(0f, 1f)] public float volumenCancion = 0.85f;

    [Header("Final normal")]
    public string[] rendersFinal = { "Prim_Render", "SegRend", "TercRend" };
    [TextArea] public string[] lineasFinal = { "SLAPPY", "Fin" };
    [Header("Final secreto (con las 4 piedras)")]
    public string[] rendersSecreto = { "Prim_Render", "CuartRend", "QuintoRend" };
    [TextArea] public string[] lineasSecreto = { "SLAPPY", "Final secreto" };

    public bool Empezado { get; private set; }
    public bool AgujeroAbierto { get; private set; }

    ProgresoCaminos progreso;
    Bounds hub;
    bool hayHub;
    Vector3 agujero;
    Transform jugador;
    float proxima;
    bool cayendo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlCargar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        Crear();
    }

    static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Crear();

    static void Crear()
    {
        if (FindFirstObjectByType<FinalJuego>(FindObjectsInactive.Include)) return;
        if (!FindFirstObjectByType<ProgresoCaminos>(FindObjectsInactive.Include)) return;
        new GameObject("Final del juego").AddComponent<FinalJuego>();
    }

    void Start()
    {
        progreso = FindFirstObjectByType<ProgresoCaminos>(FindObjectsInactive.Include);
        // La estacion: el piso de la Zona Principal
        GameObject zp = GameObject.Find("Zona_Principal");
        Transform piso = zp != null ? zp.transform.Find("piso") : null;
        if (piso != null)
            foreach (Collider c in piso.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!hayHub) { hub = c.bounds; hayHub = true; } else hub.Encapsulate(c.bounds);
            }
        if (!hayHub) Debug.LogWarning("[Final] No encontré el piso de la estación (Zona_Principal/piso).", this);
    }

    void Update()
    {
        if (Empezado || !hayHub || progreso == null || progreso.tramos == null || Time.time < proxima) return;
        proxima = Time.time + 0.25f;
        if (progreso.zonaActual < progreso.tramos.Length || PantallaMuerte.Muerto) return;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }
        // Ya en la estacion (no en el pasillo): empieza
        Vector3 p = jugador.position;
        if (p.x > hub.min.x + 3f && p.x < hub.max.x - 3f && p.z > hub.min.z + 3f && p.z < hub.max.z - 3f && Mathf.Abs(p.y - hub.max.y) < 3.5f)
            StartCoroutine(Cierre());
    }

    // ------------------------------------------------------------------ Muros y agujero

    IEnumerator Cierre()
    {
        Empezado = true;
        agujero = BuscarSitioAgujero();
        Pensamiento.Decir("final.temblor", 0.6f, alTemblar);

        // Cuatro muros desde los bordes de la estacion hacia el agujero
        Material mat = MaterialMuro();
        var muros = new List<(Transform t, Vector3 desde, Vector3 hasta)>();
        float y = hub.max.y + alturaMuros * 0.5f;
        float largoX = hub.size.x + 2f, largoZ = hub.size.z + 2f;
        void Muro(string n, Vector3 desde, Vector3 hasta, Vector3 tam)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(transform, true);
            go.transform.position = desde;
            go.transform.localScale = tam;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            muros.Add((go.transform, desde, hasta));
        }
        Muro("Muro Norte", new Vector3(agujero.x, y, hub.max.z + 0.5f), new Vector3(agujero.x, y, agujero.z + distanciaFinal + 0.5f), new Vector3(largoX * 2f, alturaMuros, 1f));
        Muro("Muro Sur", new Vector3(agujero.x, y, hub.min.z - 0.5f), new Vector3(agujero.x, y, agujero.z - distanciaFinal - 0.5f), new Vector3(largoX * 2f, alturaMuros, 1f));
        Muro("Muro Este", new Vector3(hub.max.x + 0.5f, y, agujero.z), new Vector3(agujero.x + distanciaFinal + 0.5f, y, agujero.z), new Vector3(1f, alturaMuros, largoZ * 2f));
        Muro("Muro Oeste", new Vector3(hub.min.x - 0.5f, y, agujero.z), new Vector3(agujero.x - distanciaFinal - 0.5f, y, agujero.z), new Vector3(1f, alturaMuros, largoZ * 2f));

        StartCoroutine(AbrirAgujero());
        float proximoTemblor = 0f;
        for (float t = 0f; t < duracionCierre; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duracionCierre);
            foreach (var m in muros) m.t.GetComponent<Rigidbody>().MovePosition(Vector3.Lerp(m.desde, m.hasta, k));
            if (t >= proximoTemblor) { Jugo.Sacudir(0.12f); proximoTemblor = t + Random.Range(0.8f, 1.6f); }
            yield return null;
        }
    }

    // Lo mas cerca del centro de la estacion donde quepa el agujero (sin nada encima)
    Vector3 BuscarSitioAgujero()
    {
        Vector3 c = new Vector3(hub.center.x, hub.max.y, hub.center.z);
        for (float r = 0f; r < Mathf.Min(hub.extents.x, hub.extents.z) - 3f; r += 1.5f)
            for (int i = 0; i < (r == 0f ? 1 : 12); i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit h, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                Vector3 suelo = h.point;
                if (Physics.CheckCapsule(suelo + Vector3.up * (radioAgujero + 0.3f), suelo + Vector3.up * 2.2f, radioAgujero + 0.2f,
                                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                return suelo;
            }
        return c;
    }

    IEnumerator AbrirAgujero()
    {
        yield return new WaitForSeconds(3f);
        var negro = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.black };
        if (negro.HasProperty("_BaseColor")) negro.SetColor("_BaseColor", Color.black);
        var borde = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        borde.name = "Agujero (borde)";
        Destroy(borde.GetComponent<Collider>());
        borde.transform.SetParent(transform, true);
        borde.transform.position = agujero + Vector3.up * 0.012f;
        borde.GetComponent<Renderer>().sharedMaterial = new Material(negro) { color = new Color(0.08f, 0.05f, 0.04f) };
        var hueco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hueco.name = "Agujero";
        Destroy(hueco.GetComponent<Collider>());
        hueco.transform.SetParent(transform, true);
        hueco.transform.position = agujero + Vector3.up * 0.02f;
        hueco.GetComponent<Renderer>().sharedMaterial = negro;
        Jugo.Sacudir(0.4f);
        for (float t = 0f; t < 2.5f; t += Time.deltaTime)
        {
            float k = Jugo.SalidaCubica(t / 2.5f);
            borde.transform.localScale = new Vector3(radioAgujero * 2.3f * k, 0.01f, radioAgujero * 2.3f * k);
            hueco.transform.localScale = new Vector3(radioAgujero * 2f * k, 0.01f, radioAgujero * 2f * k);
            yield return null;
        }
        AgujeroAbierto = true;
        Pensamiento.Decir("final.agujero", 0.4f, alAbrirAgujero);
        while (!cayendo)
        {
            if (jugador != null && new Vector2(jugador.position.x - agujero.x, jugador.position.z - agujero.z).magnitude < radioAgujero * 0.8f)
                StartCoroutine(Caer());
            yield return null;
        }
    }

    // Muros de sombra negra: el mismo humo que el "Humo Plano" de las entradas de los pasillos (Humo_Fondo,
    // shader Slappy/HumoNegro opaco). Para entonces las sombras ya se abrieron y estan desactivadas: se buscan inactivas.
    Material MaterialMuro()
    {
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (r.name == "Humo Plano" && r.sharedMaterial != null) return r.sharedMaterial;
        // Sin "Humo Plano" en la escena: humo negro con los mismos valores, o negro liso si falta el shader
        Shader sh = Shader.Find("Slappy/HumoNegro");
        if (sh != null)
        {
            var m = new Material(sh) { renderQueue = 2450 };
            m.SetColor("_Color", new Color(0.002f, 0.002f, 0.0025f));
            m.SetColor("_ColorClaro", new Color(0.03f, 0.03f, 0.032f));
            m.SetFloat("_Densidad", 2f); m.SetFloat("_Nucleo", 1f); m.SetFloat("_Escala", 0.8f);
            m.SetFloat("_Remolino", 0.6f); m.SetFloat("_VelRemolino", 0.3f); m.SetFloat("_Estirar", 2.2f);
            m.SetFloat("_Capas", 0.6f); m.SetFloat("_Contraste", 2.4f); m.SetVector("_Velocidad", new Vector4(0f, 0.6f, 0f, 0f));
            m.SetFloat("_Bordes", 0f); m.SetFloat("_ZWrite", 1f);
            return m;
        }
        var negro = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.black };
        if (negro.HasProperty("_BaseColor")) negro.SetColor("_BaseColor", Color.black);
        return negro;
    }

    // ------------------------------------------------------------------ Caida y cinematica

    IEnumerator Caer()
    {
        cayendo = true;
        foreach (MonoBehaviour b in new MonoBehaviour[] { FindFirstObjectByType<PlayerMovement>(), FindFirstObjectByType<MouseLook>(),
                                                          FindFirstObjectByType<HeadBob>(), FindFirstObjectByType<PlayerInteractor>(), FindFirstObjectByType<PauseMenu>() })
            if (b != null) b.enabled = false;
        foreach (JefeIndigoAI e in FindObjectsByType<JefeIndigoAI>(FindObjectsSortMode.None)) e.enabled = false;
        foreach (EstatuaAI e in FindObjectsByType<EstatuaAI>(FindObjectsSortMode.None)) e.enabled = false;

        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
        foreach (Collider c in jugador.GetComponentsInChildren<Collider>()) c.enabled = false;
        Camera cam = Camera.main;

        // Pantalla
        Canvas canvas = HUD.CrearCanvas("Final", 900);
        Image negro = HUD.Imagen(HUD.Llenar(canvas.transform, "Negro"), Color.black);
        negro.color = new Color(0f, 0f, 0f, 0f);

        // Cae por el agujero (cada vez mas rapido) mientras se va todo a negro
        Vector3 desde = new Vector3(agujero.x, jugador.position.y, agujero.z);
        float v = 0f;
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            v += 14f * Time.deltaTime;
            desde += Vector3.down * v * Time.deltaTime;
            jugador.position = desde;
            if (cam != null) cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, Quaternion.Euler(-70f, 0f, 0f), Time.deltaTime * 2f);
            negro.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / 1.6f));
            yield return null;
        }
        negro.color = Color.black;
        float volumen = AudioListener.volume;   // el volumen que eligio el jugador en el menu
        AudioListener.volume = 0f;
        yield return new WaitForSeconds(1.2f);
        // Se callan todos los sonidos del juego (aunque algun script los vuelva a dar Play) y suena la cancion de Slappy
        foreach (AudioSource a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) a.mute = true;
        AudioListener.volume = volumen;
        AudioSource musica = Cancion();

        bool secreto = PiedraColeccionable.Recogidas >= PiedraColeccionable.Total;
        yield return Imagenes(canvas.transform, secreto ? rendersSecreto : rendersFinal);
        foreach (string l in secreto ? lineasSecreto : lineasFinal) yield return Linea(canvas.transform, l, l == "SLAPPY" ? 96f : 40f);
        // La cancion se apaga mientras se vuelve al inicio
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            if (musica != null) musica.volume = volumenCancion * (1f - t / 1.5f);
            yield return null;
        }
        if (PantallaMuerte.Instance != null) PantallaMuerte.Instance.VolverAlInicio();
    }

    AudioSource Cancion()
    {
        AudioClip clip = Resources.Load<AudioClip>(CarpetaRenders + "/" + cancion);
        if (clip == null) { Debug.LogWarning($"[Final] No encontré la canción Resources/{CarpetaRenders}/{cancion}.", this); return null; }
        var a = gameObject.AddComponent<AudioSource>();
        a.clip = clip;
        a.spatialBlend = 0f;
        a.volume = volumenCancion;
        a.loop = false;
        a.ignoreListenerPause = true;
        a.Play();
        return a;
    }

    IEnumerator Linea(Transform padre, string texto, float tam)
    {
        TextMeshProUGUI t = HUD.Texto(padre, "Linea", texto, tam, TextAlignmentOptions.Center, new Color(0.92f, 0.9f, 0.86f, 0f));
        var r = (RectTransform)t.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        if (tam > 60f) t.fontStyle = FontStyles.Bold;
        float dura = Mathf.Clamp(texto.Length * 0.06f, 1.6f, 4f);
        for (float x = 0f; x < 0.8f; x += Time.deltaTime) { t.alpha = x / 0.8f; yield return null; }
        t.alpha = 1f;
        yield return new WaitForSeconds(dura);
        for (float x = 0f; x < 0.8f; x += Time.deltaTime) { t.alpha = 1f - x / 0.8f; yield return null; }
        Destroy(t.gameObject);
        yield return new WaitForSeconds(0.3f);
    }

    // Las imagenes de la cinematica, una tras otra: aparece desde negro, se acerca despacio y se vuelve a negro.
    // Se ajustan a la pantalla sin deformarse (bandas negras si la proporcion no coincide).
    IEnumerator Imagenes(Transform padre, string[] nombres)
    {
        foreach (string n in nombres)
        {
            Texture2D tex = Resources.Load<Texture2D>(CarpetaRenders + "/" + n);
            if (tex == null) { Debug.LogWarning($"[Final] No encontré la imagen Resources/{CarpetaRenders}/{n}.", this); continue; }
            RectTransform r = HUD.Llenar(padre, n);
            var ajuste = r.gameObject.AddComponent<AspectRatioFitter>();
            ajuste.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            ajuste.aspectRatio = tex.width / (float)tex.height;
            var img = r.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            float total = segundosPorImagen + fundido * 2f;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                img.color = new Color(1f, 1f, 1f, Mathf.Min(Mathf.Clamp01(t / fundido), Mathf.Clamp01((total - t) / fundido)));
                r.localScale = Vector3.one * Mathf.Lerp(1f, zoomImagen, t / total);
                yield return null;
            }
            Destroy(r.gameObject);
            yield return new WaitForSeconds(0.4f);
        }
    }
}
