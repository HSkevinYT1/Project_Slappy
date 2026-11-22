using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// El final del guion: con las 4 zonas completadas, al volver a la estacion los muros empiezan a cerrarse y se abre
// un agujero en el piso (la unica salida). Al tirarse empieza la cinematica final; si lleva las 4 piedras
// coleccionables (PiedraColeccionable), la cinematica secreta (el collar del sotano se completa).
// Las cinematicas son provisionales (texto sobre negro y el collar dibujado): se pueden cambiar por las de verdad.
// Se crea solo al jugar; los textos y tiempos se editan en el objeto "Final del juego" (en Play) o aqui.
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

    [Header("Cinematica final")]
    [TextArea] public string[] lineasFinal = { "...", "SLAPPY", "Fin" };
    [Header("Cinematica secreta (con las 4 piedras)")]
    [TextArea] public string[] lineasSecreto = { "Las cuatro piedras brillan en mi bolsillo...", "El collar está completo.", "SLAPPY", "Final secreto" };
    public Color[] coloresGemas = { new Color(0.35f, 0.1f, 1f), new Color(1f, 0.85f, 0.1f), new Color(0.12f, 0.12f, 0.14f), new Color(1f, 0.08f, 0.08f) };

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
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
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

    Material MaterialMuro()
    {
        GameObject zp = GameObject.Find("Zona_Principal");
        if (zp == null) return null;
        foreach (Renderer r in zp.GetComponentsInChildren<Renderer>())
            if (r.sharedMaterial != null && r.bounds.size.y > 2f) return r.sharedMaterial;
        return null;
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
        AudioListener.volume = 0f;
        yield return new WaitForSeconds(1.2f);
        AudioListener.volume = 1f;

        bool secreto = PiedraColeccionable.Recogidas >= PiedraColeccionable.Total;
        string[] lineas = secreto ? lineasSecreto : lineasFinal;
        if (secreto) yield return Collar(canvas.transform);
        foreach (string l in lineas) yield return Linea(canvas.transform, l, l == "SLAPPY" ? 96f : 40f);
        yield return new WaitForSeconds(1.5f);
        if (PantallaMuerte.Instance != null) PantallaMuerte.Instance.VolverAlInicio();
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

    // El collar de la caja del sotano: la cadena y sus 4 huecos, que se van llenando con las gemas
    IEnumerator Collar(Transform padre)
    {
        RectTransform raiz = HUD.Llenar(padre, "Collar");
        var grupo = raiz.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        Sprite circulo = HUD.Circulo(), anillo = HUD.Circulo(0.25f);
        const int cuentas = 26;
        for (int i = 0; i < cuentas; i++)
        {
            float a = Mathf.Lerp(200f, 340f, i / (float)(cuentas - 1)) * Mathf.Deg2Rad;
            RectTransform c = HUD.Rect(raiz, "Cuenta", new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(a) * 300f, Mathf.Sin(a) * 300f + 260f), Vector2.one * 14f);
            HUD.Imagen(c, new Color(0.7f, 0.62f, 0.4f), circulo);
        }
        var huecos = new List<RectTransform>();
        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.Lerp(235f, 305f, i / 3f) * Mathf.Deg2Rad;
            RectTransform h = HUD.Rect(raiz, "Hueco", new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(a) * 300f, Mathf.Sin(a) * 300f + 260f), Vector2.one * 56f);
            HUD.Imagen(h, new Color(0.75f, 0.66f, 0.42f), anillo);
            huecos.Add(h);
        }
        for (float t = 0f; t < 1f; t += Time.deltaTime) { grupo.alpha = t; yield return null; }
        grupo.alpha = 1f;
        yield return new WaitForSeconds(0.6f);
        for (int i = 0; i < huecos.Count; i++)
        {
            RectTransform g = HUD.Rect(raiz, "Gema", new Vector2(0.5f, 0.5f), new Vector2(Mathf.Lerp(-500f, 500f, i / 3f), -420f), Vector2.one * 40f);
            Image img = HUD.Imagen(g, coloresGemas[i % coloresGemas.Length], circulo);
            Vector2 desde = g.anchoredPosition, hasta = huecos[i].anchoredPosition;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float k = Jugo.SalidaCubica(t / 0.9f);
                g.anchoredPosition = Vector2.Lerp(desde, hasta, k);
                g.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, k);
                yield return null;
            }
            g.anchoredPosition = hasta;
            // Encaja: destello
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) { g.localScale = Vector3.one * (1f + 0.5f * Mathf.Sin(t / 0.3f * Mathf.PI)); yield return null; }
            g.localScale = Vector3.one;
            img.color = Color.Lerp(img.color, Color.white, 0.15f);
            yield return new WaitForSeconds(0.35f);
        }
        yield return new WaitForSeconds(1.2f);
        for (float t = 0f; t < 1f; t += Time.deltaTime) { grupo.alpha = 1f - t; yield return null; }
        Destroy(raiz.gameObject);
    }
}
