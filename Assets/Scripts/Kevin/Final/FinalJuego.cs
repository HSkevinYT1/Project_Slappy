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
    [Tooltip("Antes de la ultima imagen: pantalla negra con este texto (escrito letra a letra, como el final de Papers, Please)")]
    [TextArea(3, 6)] public string textoFinal = "No fuiste capaz de conseguir ni siquiera el muñeco.\n\nBueno, nos las arreglaremos con tu energía vital.";
    [TextArea] public string[] lineasFinal = { "SLAPPY", "Fin" };
    [Header("Final secreto (con las 4 piedras)")]
    public string[] rendersSecreto = { "Prim_Render", "CuartRend", "QuintoRend" };
    [TextArea(3, 6)] public string textoSecreto = "No solo fuiste capaz de vencer los peligros, sino que la energía de las gemas está en tu poder.\n\nNos rendimos ante ti \"Mi lord\".";
    [TextArea] public string[] lineasSecreto = { "SLAPPY", "Final secreto" };

    [Header("Pelicula vieja (en color: grano, rayas, parpadeo, viñeta, tono calido)")]
    public bool peliculaVieja = true;
    [Range(0f, 1f)] public float grano = 0.11f;
    [Range(0f, 1f)] public float parpadeo = 0.06f;
    [Range(0f, 1f)] public float vineta = 0.75f;
    public Color tonoCalido = new Color(1f, 0.78f, 0.5f, 0.07f);
    [Tooltip("Letras por segundo del texto")]
    public float letrasPorSegundo = 26f;
    public Color colorTexto = new Color(0.93f, 0.87f, 0.74f);

    public bool Empezado { get; private set; }
    public bool AgujeroAbierto { get; private set; }

    ProgresoCaminos progreso;
    Bounds hub;
    bool hayHub;
    // Los colliders del piso de la estacion: el agujero va SOLO sobre ellos (antes caia sobre el techo de una cabina)
    readonly List<Collider> pisos = new List<Collider>();
    Material materialPiso;
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
                pisos.Add(c);
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

    // Lo mas cerca del centro de la estacion donde quepa el agujero: EN EL PISO (lo primero que toca el rayo tiene que
    // ser el piso de la estacion, no el techo de una cabina ni un mueble) y sin nada encima en todo su circulo
    Vector3 BuscarSitioAgujero()
    {
        Vector3 c = new Vector3(hub.center.x, hub.max.y, hub.center.z);
        for (float r = 0f; r < Mathf.Min(hub.extents.x, hub.extents.z) - 3f; r += 1.5f)
            for (int i = 0; i < (r == 0f ? 1 : 12); i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!PisoEn(p, out RaycastHit h)) continue;
                Vector3 suelo = h.point;
                // Todo el borde del agujero tambien sobre el piso (no a medias sobre un escalon)
                bool entero = true;
                for (int j = 0; j < 8 && entero; j++)
                {
                    float b = j * Mathf.PI / 4f;
                    if (!PisoEn(suelo + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * radioAgujero, out RaycastHit hb) || Mathf.Abs(hb.point.y - suelo.y) > 0.15f) entero = false;
                }
                if (!entero) continue;
                if (Physics.CheckCapsule(suelo + Vector3.up * (radioAgujero + 0.3f), suelo + Vector3.up * 2.2f, radioAgujero + 0.2f,
                                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                Renderer rp = h.collider.GetComponent<Renderer>();
                if (rp == null) rp = h.collider.GetComponentInParent<Renderer>();
                if (rp != null) materialPiso = rp.sharedMaterial;
                return suelo;
            }
        Debug.LogWarning("[Final] No encontré un sitio libre en el piso para el agujero: lo pongo en el centro.", this);
        PisoEn(c, out RaycastHit hc);
        return hc.collider != null ? hc.point : c;
    }

    // Lo primero que hay debajo de ese punto es el piso de la estacion (y no algo encima de el)
    bool PisoEn(Vector3 p, out RaycastHit piso)
    {
        piso = default;
        // Desde 3 m (por debajo del techo de la estacion, por encima de las cabinas y los muebles)
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, hub.max.y + 3f, p.z), Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return false;
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (jugador != null && h.collider.transform.IsChildOf(jugador.root)) continue;
            if (!pisos.Contains(h.collider)) return false;
            piso = h;
            return true;
        }
        return false;
    }

    // El piso se hunde: primero se raja (grietas negras que crecen desde el centro, con crujidos), despues el trozo de
    // piso se parte en baldosas que caen al hueco, una tras otra, y queda el agujero negro (pegado al piso)
    IEnumerator AbrirAgujero()
    {
        yield return new WaitForSeconds(3f);
        var negro = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.black };
        if (negro.HasProperty("_BaseColor")) negro.SetColor("_BaseColor", Color.black);
        var sonido = gameObject.AddComponent<AudioSource>();
        sonido.spatialBlend = 0f;
        sonido.playOnAwake = false;

        // ---- Grietas
        var grietas = new List<(Transform t, float largo)>();
        int nGrietas = 9;
        for (int i = 0; i < nGrietas; i++)
        {
            float ang = (i + Random.Range(-0.3f, 0.3f)) * 360f / nGrietas;
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Grieta";
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(transform, true);
            g.transform.SetPositionAndRotation(agujero + Vector3.up * 0.006f, Quaternion.Euler(0f, ang, 0f));
            g.GetComponent<Renderer>().sharedMaterial = negro;
            grietas.Add((g.transform, radioAgujero * Random.Range(1.05f, 1.6f)));
        }
        Jugo.Sacudir(0.35f);
        for (float t = 0f; t < 1.8f; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / 1.8f);
            foreach (var g in grietas)
            {
                float l = g.largo * Jugo.SalidaCubica(k);
                // Cada grieta sale del centro hacia afuera (el cubo se estira hacia adelante)
                g.t.localScale = new Vector3(0.035f + 0.03f * k, 0.004f, l);
                g.t.position = agujero + Vector3.up * 0.006f + g.t.forward * (l * 0.5f);
            }
            if (Random.value < Time.deltaTime * 5f) { sonido.pitch = Random.Range(0.6f, 0.9f); sonido.PlayOneShot(PedazoMuneca.Crujido(), 0.9f); Jugo.Sacudir(0.12f); }
            yield return null;
        }

        // ---- El hueco (abajo de las baldosas) y su borde
        var borde = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        borde.name = "Agujero (borde)";
        Destroy(borde.GetComponent<Collider>());
        borde.transform.SetParent(transform, true);
        borde.transform.position = agujero + Vector3.up * 0.004f;
        borde.transform.localScale = new Vector3(radioAgujero * 2.25f, 0.002f, radioAgujero * 2.25f);
        borde.GetComponent<Renderer>().sharedMaterial = new Material(negro) { color = new Color(0.07f, 0.05f, 0.04f) };
        var hueco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hueco.name = "Agujero";
        Destroy(hueco.GetComponent<Collider>());
        hueco.transform.SetParent(transform, true);
        hueco.transform.position = agujero + Vector3.up * 0.007f;
        hueco.transform.localScale = new Vector3(radioAgujero * 2f, 0.002f, radioAgujero * 2f);
        hueco.GetComponent<Renderer>().sharedMaterial = negro;

        // ---- Las baldosas: el trozo de piso que tapa el hueco (con el material del piso), y caen
        Material mp = materialPiso != null ? materialPiso : new Material(negro) { color = new Color(0.25f, 0.24f, 0.22f) };
        const float lado = 0.55f;
        var baldosas = new List<Rigidbody>();
        int n = Mathf.CeilToInt(radioAgujero / lado);
        for (int x = -n; x <= n; x++)
            for (int z = -n; z <= n; z++)
            {
                Vector3 off = new Vector3(x * lado, 0f, z * lado);
                if (off.magnitude > radioAgujero - lado * 0.3f) continue;
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Baldosa";
                b.layer = 2;
                b.transform.SetParent(transform, true);
                // La cara de arriba apenas sobre el hueco (lo tapa); el resto, metido en el piso
                b.transform.SetPositionAndRotation(agujero + off + Vector3.up * (0.012f - 0.05f), Quaternion.Euler(0f, Random.Range(-4f, 4f), 0f));
                b.transform.localScale = new Vector3(lado * 0.97f, 0.1f, lado * 0.97f);
                b.GetComponent<Renderer>().sharedMaterial = mp;
                Collider cb = b.GetComponent<Collider>();
                foreach (Collider p in pisos) if (p != null) Physics.IgnoreCollision(cb, p, true);
                if (jugador != null) foreach (Collider cj in jugador.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(cb, cj, true);
                var rb = b.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.mass = 8f;
                baldosas.Add(rb);
            }
        // Del centro hacia afuera, cada una un poco despues de la anterior
        baldosas.Sort((a, c) => Plano(a.position - agujero).CompareTo(Plano(c.position - agujero)));
        foreach (var g in grietas) if (g.t != null) Destroy(g.t.gameObject);
        sonido.pitch = 0.7f;
        sonido.PlayOneShot(Derrumbe(), 1f);
        Jugo.Sacudir(0.55f);
        Jugo.PatadaFOV(-3f);
        for (int i = 0; i < baldosas.Count; i++)
        {
            Rigidbody rb = baldosas[i];
            rb.isKinematic = false;
            rb.useGravity = true;
            Vector3 haciaCentro = agujero - rb.position; haciaCentro.y = 0f;
            rb.linearVelocity = Vector3.down * Random.Range(0.5f, 1.5f) + haciaCentro * 0.4f;
            rb.angularVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(-1f, 1f), Random.Range(-3f, 3f));
            Destroy(rb.gameObject, 3f);
            if (i % 3 == 2) { Jugo.Sacudir(0.08f); yield return new WaitForSeconds(0.05f); }
        }
        yield return new WaitForSeconds(0.6f);
        AgujeroAbierto = true;
        Pensamiento.Decir("final.agujero", 0.4f, alAbrirAgujero);
        while (!cayendo)
        {
            if (jugador != null && new Vector2(jugador.position.x - agujero.x, jugador.position.z - agujero.z).magnitude < radioAgujero * 0.8f)
                StartCoroutine(Caer());
            yield return null;
        }
    }

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    // Derrumbe: un golpe grave y largo con ruido que se va apagando (el piso que se viene abajo)
    static AudioClip derrumbe;
    static AudioClip Derrumbe()
    {
        if (derrumbe != null) return derrumbe;
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * 2.2f);
        var d = new float[n];
        var rnd = new System.Random(21);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            lp += ((float)rnd.NextDouble() * 2f - 1f - lp) * 0.06f;
            float golpe = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 32f, Mathf.Clamp01(t * 1.5f)) * t) * Mathf.Exp(-t * 2.2f);
            float rocas = lp * 2.2f * Mathf.Exp(-t * 1.4f) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 18f, 0.3f));
            d[i] = Mathf.Clamp((golpe * 0.8f + rocas) * 0.85f, -1f, 1f);
        }
        derrumbe = AudioClip.Create("Derrumbe", n, 1, rate, false);
        derrumbe.SetData(d, 0);
        return derrumbe;
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

    // Lo que el jugador no puede usar durante el final. Se apaga en cada cuadro (LateUpdate): al cerrar el menu debug
    // (ver los finales desde ahi) se volvian a prender, y al mover el mouse salia "interactuar" con lo que tenia delante
    readonly List<Behaviour> bloqueados = new List<Behaviour>();

    void Bloquear()
    {
        bloqueados.Clear();
        foreach (PlayerMovement b in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) bloqueados.Add(b);
        foreach (MouseLook b in FindObjectsByType<MouseLook>(FindObjectsSortMode.None)) bloqueados.Add(b);
        foreach (HeadBob b in FindObjectsByType<HeadBob>(FindObjectsSortMode.None)) bloqueados.Add(b);
        foreach (PlayerInteractor b in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None)) bloqueados.Add(b);
        foreach (PauseMenu b in FindObjectsByType<PauseMenu>(FindObjectsSortMode.None)) bloqueados.Add(b);
        foreach (Inventario b in FindObjectsByType<Inventario>(FindObjectsSortMode.None)) bloqueados.Add(b);
        foreach (Behaviour b in bloqueados) if (b != null) b.enabled = false;
    }

    void LateUpdate()
    {
        if (!cayendo) return;
        foreach (Behaviour b in bloqueados) if (b != null && b.enabled) b.enabled = false;
    }

    IEnumerator Caer()
    {
        cayendo = true;
        Bloquear();
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
        yield return Cinematica(canvas, PiedraColeccionable.Recogidas >= PiedraColeccionable.Total);
    }

    // PanelDebug: ver uno de los dos finales ya (sin muros ni agujero). Al terminar vuelve al inicio, como el de verdad
    public void VerFinal(bool secreto)
    {
        if (cayendo) return;
        cayendo = true;
        Empezado = true;
        StopAllCoroutines();
        Bloquear();
        foreach (JefeIndigoAI e in FindObjectsByType<JefeIndigoAI>(FindObjectsSortMode.None)) e.enabled = false;
        foreach (EstatuaAI e in FindObjectsByType<EstatuaAI>(FindObjectsSortMode.None)) e.enabled = false;
        Canvas canvas = HUD.CrearCanvas("Final", 900);
        HUD.Imagen(HUD.Llenar(canvas.transform, "Negro"), Color.black);
        StartCoroutine(Cinematica(canvas, secreto));
    }

    // La cinematica de un final: se callan todos los sonidos, suena la cancion, las imagenes (con el texto antes de la
    // ultima) y las lineas, y se vuelve al inicio
    IEnumerator Cinematica(Canvas canvas, bool secreto)
    {
        float volumen = AudioListener.volume;   // el volumen que eligio el jugador en el menu
        AudioListener.volume = 0f;
        yield return new WaitForSeconds(1.2f);
        // Se callan todos los sonidos del juego (aunque algun script los vuelva a dar Play) y suena la cancion de Slappy
        foreach (AudioSource a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) a.mute = true;
        AudioListener.volume = volumen;
        AudioSource musica = Cancion();
        // Todo como una pelicula vieja (en color): grano, rayas, parpadeo, viñeta, tono calido y el proyector
        if (peliculaVieja) StartCoroutine(Pelicula());

        yield return Imagenes(canvas.transform, secreto ? rendersSecreto : rendersFinal, secreto ? textoSecreto : textoFinal);
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
    // Antes de la ULTIMA: pantalla negra con el texto del final (textoAntesDeLaUltima)
    IEnumerator Imagenes(Transform padre, string[] nombres, string textoAntesDeLaUltima = null)
    {
        for (int i = 0; i < nombres.Length; i++)
        {
            string n = nombres[i];
            if (i == nombres.Length - 1 && !string.IsNullOrEmpty(textoAntesDeLaUltima)) yield return TextoEscrito(padre, textoAntesDeLaUltima);
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
            float proximoSalto = 0f;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                img.color = new Color(1f, 1f, 1f, Mathf.Min(Mathf.Clamp01(t / fundido), Mathf.Clamp01((total - t) / fundido)));
                r.localScale = Vector3.one * Mathf.Lerp(1f, zoomImagen, t / total);
                // El cuadro tiembla un poquito, como en un proyector viejo
                if (peliculaVieja && t >= proximoSalto)
                {
                    proximoSalto = t + Random.Range(0.04f, 0.12f);
                    r.anchoredPosition = Random.value < 0.08f ? new Vector2(Random.Range(-3f, 3f), Random.Range(-7f, 7f))
                                                              : new Vector2(Random.Range(-1f, 1f), Random.Range(-1.5f, 1.5f));
                }
                yield return null;
            }
            Destroy(r.gameObject);
            yield return new WaitForSeconds(0.4f);
        }
    }

    // ------------------------------------------------------------------ Texto del final (como el de Papers, Please)

    // Pantalla negra; el texto se escribe letra a letra (con su tic de maquina de escribir), queda un rato y se funde
    IEnumerator TextoEscrito(Transform padre, string texto)
    {
        yield return new WaitForSeconds(0.6f);
        TextMeshProUGUI t = HUD.Texto(padre, "Texto del final", texto, 44f, TextAlignmentOptions.Center, colorTexto);
        var r = (RectTransform)t.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(320f, 160f); r.offsetMax = new Vector2(-320f, -160f);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.lineSpacing = 14f;
        t.characterSpacing = 3f;
        t.maxVisibleCharacters = 0;
        t.ForceMeshUpdate();
        int total = t.textInfo.characterCount;

        var tecla = gameObject.AddComponent<AudioSource>();
        tecla.spatialBlend = 0f;
        tecla.playOnAwake = false;
        tecla.ignoreListenerPause = true;
        AudioClip tic = Tecla();
        float acumulado = 0f;
        int visibles = 0;
        while (visibles < total)
        {
            acumulado += Time.deltaTime * letrasPorSegundo;
            while (acumulado >= 1f && visibles < total)
            {
                acumulado -= 1f;
                char c = t.textInfo.characterInfo[visibles].character;
                visibles++;
                if (!char.IsWhiteSpace(c)) { tecla.pitch = Random.Range(0.92f, 1.08f); tecla.PlayOneShot(tic, 0.35f); }
                // Pausa corta despues de un punto o una coma (como quien escribe)
                if (c == '.' || c == ',') acumulado -= letrasPorSegundo * (c == '.' ? 0.35f : 0.12f);
            }
            t.maxVisibleCharacters = visibles;
            yield return null;
        }
        yield return new WaitForSeconds(Mathf.Clamp(texto.Length * 0.045f, 3f, 7f));
        for (float x = 0f; x < 1.4f; x += Time.deltaTime) { t.alpha = 1f - x / 1.4f; yield return null; }
        Destroy(t.gameObject);
        Destroy(tecla);
        yield return new WaitForSeconds(0.6f);
    }

    // Tic de maquina de escribir: un golpecito seco
    static AudioClip tecla;
    static AudioClip Tecla()
    {
        if (tecla != null) return tecla;
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * 0.06f);
        var d = new float[n];
        var rnd = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            d[i] = (((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 120f) * 0.8f + Mathf.Sin(2f * Mathf.PI * 1300f * t) * Mathf.Exp(-t * 90f) * 0.4f);
        }
        tecla = AudioClip.Create("Tecla", n, 1, rate, false);
        tecla.SetData(d, 0);
        return tecla;
    }

    // ------------------------------------------------------------------ Pelicula vieja (en color)

    // Encima de todo: tono calido, parpadeo de brillo, viñeta, grano que se mueve, rayas y motas que aparecen y se van,
    // y el traqueteo bajito del proyector. Dura hasta que se vuelve al inicio
    IEnumerator Pelicula()
    {
        Canvas c = HUD.CrearCanvas("Pelicula vieja", 905);
        var g = c.gameObject.AddComponent<CanvasGroup>();
        g.interactable = false; g.blocksRaycasts = false;
        Image tono = HUD.Imagen(HUD.Llenar(c.transform, "Tono"), tonoCalido);
        Image luz = HUD.Imagen(HUD.Llenar(c.transform, "Parpadeo"), new Color(0f, 0f, 0f, 0f));
        RawImage vin = Raw(c.transform, "Viñeta", Vineta(), new Color(0f, 0f, 0f, vineta));
        RawImage gr = Raw(c.transform, "Grano", Ruido(), new Color(1f, 1f, 1f, grano));
        foreach (Graphic x in new Graphic[] { tono, luz, vin, gr }) x.raycastTarget = false;
        var rayas = new Image[3];
        for (int i = 0; i < rayas.Length; i++)
        {
            RectTransform rr = HUD.Rect(c.transform, "Raya", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 2200f));
            rayas[i] = HUD.Imagen(rr, new Color(1f, 0.96f, 0.85f, 0f));
            rayas[i].raycastTarget = false;
        }
        var motas = new Image[4];
        for (int i = 0; i < motas.Length; i++)
        {
            RectTransform rm = HUD.Rect(c.transform, "Mota", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            motas[i] = HUD.Imagen(rm, new Color(0f, 0f, 0f, 0f));
            motas[i].raycastTarget = false;
        }

        var proyector = gameObject.AddComponent<AudioSource>();
        proyector.clip = Proyector();
        proyector.loop = true;
        proyector.spatialBlend = 0f;
        proyector.volume = 0.12f;
        proyector.ignoreListenerPause = true;
        proyector.Play();

        float[] rayaHasta = new float[rayas.Length];
        float[] motaHasta = new float[motas.Length];
        while (true)
        {
            float t = Time.unscaledTime;
            gr.uvRect = new Rect(Random.value, Random.value, 2.5f, 2.5f * Screen.height / Mathf.Max(1f, Screen.width));
            luz.color = new Color(0f, 0f, 0f, parpadeo * (0.5f + Mathf.PerlinNoise(t * 9f, 0.2f)) + (Random.value < 0.015f ? 0.12f : 0f));
            for (int i = 0; i < rayas.Length; i++)
            {
                if (t >= rayaHasta[i])
                {
                    bool ver = Random.value < 0.35f;
                    rayaHasta[i] = t + Random.Range(0.06f, ver ? 0.5f : 1.4f);
                    rayas[i].rectTransform.anchoredPosition = new Vector2(Random.Range(-900f, 900f), 0f);
                    rayas[i].rectTransform.sizeDelta = new Vector2(Random.Range(1f, 3f), 2200f);
                    rayas[i].color = new Color(1f, 0.96f, 0.85f, ver ? Random.Range(0.08f, 0.22f) : 0f);
                }
                else rayas[i].rectTransform.anchoredPosition += new Vector2(Random.Range(-1.5f, 1.5f), 0f);
            }
            for (int i = 0; i < motas.Length; i++)
            {
                if (t < motaHasta[i]) continue;
                bool ver = Random.value < 0.3f;
                motaHasta[i] = t + Random.Range(0.04f, 0.12f);
                float s = Random.Range(3f, 11f);
                motas[i].rectTransform.anchoredPosition = new Vector2(Random.Range(-900f, 900f), Random.Range(-500f, 500f));
                motas[i].rectTransform.sizeDelta = new Vector2(s, s * Random.Range(0.6f, 1.6f));
                motas[i].color = ver ? (Random.value < 0.7f ? new Color(0f, 0f, 0f, 0.5f) : new Color(1f, 0.95f, 0.85f, 0.35f)) : Color.clear;
            }
            yield return null;
        }
    }

    static RawImage Raw(Transform padre, string nombre, Texture tex, Color color)
    {
        RectTransform r = HUD.Llenar(padre, nombre);
        var img = r.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.color = color;
        return img;
    }

    // Grano: ruido en escala de grises (se repite y se mueve al azar cada cuadro)
    static Texture2D ruido;
    static Texture2D Ruido()
    {
        if (ruido != null) return ruido;
        const int n = 256;
        ruido = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color32[n * n];
        var rnd = new System.Random(3);
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)rnd.Next(0, 256);
            px[i] = new Color32(v, v, v, (byte)rnd.Next(60, 256));
        }
        ruido.SetPixels32(px);
        ruido.Apply();
        return ruido;
    }

    // Negro en los bordes, transparente en el centro
    static Texture2D vinetaTex;
    static Texture2D Vineta()
    {
        if (vinetaTex != null) return vinetaTex;
        const int n = 128;
        vinetaTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f).magnitude / 0.5f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.15f, r));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        vinetaTex.SetPixels32(px);
        vinetaTex.Apply();
        return vinetaTex;
    }

    // Traqueteo del proyector: golpecitos a 24 cuadros por segundo con un zumbido grave, en bucle
    static AudioClip proyectorClip;
    static AudioClip Proyector()
    {
        if (proyectorClip != null) return proyectorClip;
        const int rate = 44100;
        int n = rate * 2;
        var d = new float[n];
        var rnd = new System.Random(9);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float fase = (t * 24f) % 1f;
            float golpe = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-fase * 40f) * 0.6f;
            float zumbido = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.15f + ((float)rnd.NextDouble() * 2f - 1f) * 0.05f;
            d[i] = Mathf.Clamp(golpe + zumbido, -1f, 1f);
        }
        proyectorClip = AudioClip.Create("Proyector", n, 1, rate, false);
        proyectorClip.SetData(d, 0);
        return proyectorClip;
    }
}
