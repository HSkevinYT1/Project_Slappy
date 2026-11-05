using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Genera lianas / venas indigo sobre las paredes (cubos) del laberinto.
//
// - Las lianas se crean al iniciar, siguiendo la forma de cada cubo de los contenedores
//   indicados (por defecto "Paredes" y "Paredes Elevadas"), asi que si cambias o
//   eliminas cubos, las lianas se adaptan solas.
// - Siempre estan encendidas las lianas de la ruta marcada (bloques "p1", "p1 (1)"...
//   hasta la zona segura). Sin ruta marcada, se usa el camino mas corto desde el jugador.
// - Las demas lianas se encienden solo cuando el jugador pasa cerca (y se quedan encendidas).
//   Llegar a la zona segura NO enciende todas.
// - Las encendidas pulsan como una onda seno que avanza por la ruta hacia la zona segura.
//
// Uso: poner este componente en el objeto "Laberinto" y asignar "Zona Segura".
// Tambien existe el menu Tools > Lianas > Configurar Laberinto que lo hace solo.
public class LianasLaberinto : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Contenedores cuyos cubos llevaran lianas (ej: Paredes, Paredes Elevadas).")]
    public List<Transform> contenedoresConLianas = new List<Transform>();

    [Tooltip("Contenedores cuyos cubos bloquean el paso al calcular el camino (ej: Paredes, Paredes Elevadas, Bloques Azules).")]
    public List<Transform> contenedoresObstaculos = new List<Transform>();

    [Tooltip("Piso del laberinto: define el area de la cuadricula del camino.")]
    public Transform piso;

    [Tooltip("Techo del laberinto (opcional): las lianas no pasan de aqui.")]
    public Transform techo;

    [Tooltip("Destino del camino. Si tiene Collider, toda su area cuenta como zona segura.")]
    public Transform zonaSegura;

    [Tooltip("Jugador. Si se deja vacio se busca por el tag indicado.")]
    public Transform jugador;
    public string tagJugador = "Player";

    [Tooltip("Opcional: punto desde el que se calcula el camino cuando el jugador no esta dentro del laberinto (ej: la entrada).")]
    public Transform puntoInicio;

    [Tooltip("Material con el shader Custom/LianasIndigo.")]
    public Material materialLianas;

    [Header("Camino")]
    [Tooltip("Tamano de cada celda de la cuadricula (metros).")]
    [Range(0.2f, 1.5f)] public float tamanoCelda = 0.5f;
    [Tooltip("Radio del jugador: margen que se deja alrededor de las paredes.")]
    [Range(0f, 1.5f)] public float radioJugador = 0.35f;
    [Tooltip("Que tan lejos del camino (caminando) puede estar una liana para encenderse.")]
    [Range(0.5f, 6f)] public float distanciaCamino = 2.25f;
    [Tooltip("Cada cuanto se revisa si el jugador cambio de posicion (segundos).")]
    [Range(0.05f, 2f)] public float intervaloRecalculo = 0.3f;

    [Header("Ruta marcada (p1 -> p1 (1) -> ... -> zona segura)")]
    [Tooltip("Si hay puntos, las lianas encendidas son las de ESTA ruta (en orden) y no el camino mas corto.")]
    public List<Transform> puntosRuta = new List<Transform>();
    [Tooltip("Copia de la ruta en coordenadas del Laberinto: sigue funcionando aunque se borren los puntos.")]
    [SerializeField] List<Vector3> rutaGuardada = new List<Vector3>();

    [Header("Descubrir lianas")]
    [Tooltip("Las lianas a esta distancia (caminando) del jugador se encienden y se quedan encendidas.")]
    [Range(0f, 10f)] public float radioDescubrir = 3f;

    [Header("Zona segura")]
    [Tooltip("Retraso por metro de la ola que enciende todas las lianas al llegar a la zona segura.")]
    [Range(0f, 0.2f)] public float retrasoPorMetro = 0.04f;

    [Header("Forma de las lianas")]
    public int semilla = 1234;
    [Tooltip("Ancho de cada tramo de pared que se enciende/apaga como una unidad.")]
    [Range(0.75f, 6f)] public float anchoTramo = 2.5f;
    [Tooltip("Venas serpenteantes por metro de pared.")]
    [Range(0.1f, 3f)] public float densidadVenas = 0.6f;
    [Tooltip("0 = las venas suben casi rectas, 1 = avanzan casi de lado.")]
    [Range(0f, 1f)] public float tendenciaHorizontal = 0.7f;
    [Tooltip("Largo de cada vena serpenteante (metros).")]
    [Range(0.5f, 8f)] public float largoVenas = 3.5f;
    [Tooltip("Venas horizontales que recorren cada pared de punta a punta.")]
    [Range(0, 5)] public int venasHorizontales = 2;
    [Range(0.005f, 0.15f)] public float grosor = 0.045f;
    [Range(0, 4)] public int ramasMaximas = 2;
    [Tooltip("Agrega una raiz horizontal en la base de cada pared.")]
    public bool raizEnLaBase = true;

    [Tooltip("Contenedores cuyas lianas van en vertical (hacia arriba o hacia abajo), ej: Paredes Elevadas (rosadas).")]
    public List<Transform> contenedoresVerticales = new List<Transform>();
    [Tooltip("Material propio de las lianas verticales (bloques rosas). Si esta vacio se usa Resources/LianasVerticales.")]
    public Material materialLianasVerticales;
    [Tooltip("Lianas verticales por metro en los bloques rosas.")]
    [Range(0.5f, 8f)] public float densidadVerticales = 3f;

    [Header("Lianas en el piso")]
    public bool lianasEnPiso = true;
    [Tooltip("El piso se divide en baldosas de este tamano; cada una se enciende/apaga como unidad.")]
    [Range(1f, 8f)] public float tamanoBaldosaPiso = 3f;
    [Range(0, 6)] public int venasPorBaldosa = 2;
    [Range(0.005f, 0.15f)] public float grosorPiso = 0.035f;
    [Range(3, 8)] public int ladosTubo = 5;

    const string NombreObjetoGenerado = "Lianas (generadas)";
    static readonly int IdTiempo = Shader.PropertyToID("_TiempoLianas");
    static readonly int IdFade = Shader.PropertyToID("_DuracionFade");

    // ---------- Datos internos ----------

    struct Caja
    {
        public Transform t;
        public Vector3 c, ax, ay, az; // centro y ejes (mundo)
        public float hx, hy, hz;       // medias medidas (mundo)
    }

    class Tramo
    {
        public int vIni, vCant;
        public int celda = -1;     // celda caminable frente al tramo
        public float distSegura;   // metros hasta la zona segura
        public float prev, next, tCambio;
    }

    // Cuadricula
    Vector2 origen;
    int nx, nz;
    float pisoY, techoY;
    bool[] caminable;
    int[] distSegura;      // pasos hasta la zona segura (-1 = inalcanzable)
    int[] marcaCerca;      // celdas cerca del camino (con marca de recalculo)
    int marcaActual;
    readonly List<int> camino = new List<int>();
    float[] distRuta;      // metros que faltan por la ruta marcada (-1 = lejos de la ruta)
    bool[] descubierta;    // celdas por las que ya paso (cerca) el jugador
    bool hayRuta;

    // Malla
    readonly List<Tramo> tramos = new List<Tramo>();
    Vector4[] datos;
    Mesh malla;
    MeshRenderer rendererLianas;
    MaterialPropertyBlock bloque;

    bool todasEncendidas;
    int ultimaCeldaJugador = int.MinValue;
    float temporizador;
    bool listo;

    public bool TodasEncendidas => todasEncendidas;

    // ---------------------------------------------------------------------

    void Reset()
    {
        AutoConfigurar();
    }

    // Busca automaticamente los hijos tipicos del laberinto.
    public void AutoConfigurar()
    {
        Transform paredes = transform.Find("Paredes");
        Transform elevadas = transform.Find("Paredes Elevadas");
        Transform bloques = transform.Find("Bloques Azules");

        contenedoresConLianas.Clear();
        if (paredes) contenedoresConLianas.Add(paredes);
        if (elevadas) contenedoresConLianas.Add(elevadas);

        // Las paredes elevadas (rosadas) NO bloquean: el jugador pasa por debajo.
        contenedoresObstaculos.Clear();
        if (paredes) contenedoresObstaculos.Add(paredes);
        if (bloques) contenedoresObstaculos.Add(bloques);

        contenedoresVerticales.Clear();
        if (elevadas) contenedoresVerticales.Add(elevadas);

        if (!piso) piso = transform.Find("Piso");
        if (!techo) techo = transform.Find("techo");
        if (!techo) techo = transform.Find("Techo");

        if (!zonaSegura)
        {
            ZonaSeguraLianas z = GetComponentInChildren<ZonaSeguraLianas>(true);
            if (z) zonaSegura = z.transform;
        }
        if (!zonaSegura)
        {
            // Objeto colocado a mano con nombre tipo "zona segura".
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.Replace("'", "").Replace("_", " ").Trim().ToLowerInvariant();
                if (n.Contains("zona segura")) { zonaSegura = t; break; }
            }
        }

        if (puntosRuta.Count == 0) BuscarPuntosRuta();

#if UNITY_EDITOR
        if (!materialLianas)
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("LianasIndigo t:Material"))
            {
                materialLianas = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (materialLianas) break;
            }
        }
#endif
    }

    // Busca los bloques "p1", "p1 (1)", "p1 (2)"... dentro del Laberinto, en orden.
    public void BuscarPuntosRuta()
    {
        var encontrados = new List<KeyValuePair<int, Transform>>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            var m = System.Text.RegularExpressions.Regex.Match(t.name.Trim(), @"^p1(?:\s*\((\d+)\))?$");
            if (!m.Success) continue;
            int orden = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
            encontrados.Add(new KeyValuePair<int, Transform>(orden, t));
        }
        encontrados.Sort((a, b) => a.Key.CompareTo(b.Key));
        puntosRuta.Clear();
        foreach (var e in encontrados) puntosRuta.Add(e.Value);
    }

    // Ruta en el mundo: puntos (o su copia guardada) + la zona segura al final.
    List<Vector3> RutaMundo()
    {
        var ruta = new List<Vector3>();
        bool hayPuntos = false;
        foreach (Transform t in puntosRuta) if (t) { ruta.Add(t.position); hayPuntos = true; }
        if (hayPuntos)
        {
            if (zonaSegura) ruta.Add(CentroZonaSegura());
        }
        else
        {
            foreach (Vector3 p in rutaGuardada) ruta.Add(transform.TransformPoint(p));
        }
        return ruta;
    }

    Vector3 CentroZonaSegura()
    {
        return LimitesDe(zonaSegura, out Bounds b) ? b.center : zonaSegura.position;
    }

    // Destino final: la zona segura, o el ultimo punto de la ruta guardada si se borro.
    bool PosicionZonaSegura(out Vector3 p)
    {
        if (zonaSegura) { p = CentroZonaSegura(); return true; }
        if (rutaGuardada.Count > 0) { p = transform.TransformPoint(rutaGuardada[rutaGuardada.Count - 1]); return true; }
        p = default;
        return false;
    }

    // Guarda la ruta en coordenadas locales (para cuando se borren los bloques).
    void ActualizarRutaGuardada()
    {
        bool hayPuntos = false;
        foreach (Transform t in puntosRuta) if (t) { hayPuntos = true; break; }
        if (!hayPuntos) return;

        var nueva = new List<Vector3>();
        foreach (Vector3 p in RutaMundo()) nueva.Add(transform.InverseTransformPoint(p));

        bool igual = nueva.Count == rutaGuardada.Count;
        for (int i = 0; igual && i < nueva.Count; i++) igual = (nueva[i] - rutaGuardada[i]).sqrMagnitude < 1e-6f;
        if (igual) return;

        rutaGuardada = nueva;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // Si la escena tiene un "Laberinto" sin este componente, se agrega solo al dar Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstalarAutomaticamente()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        InstalarEnEscena();
    }

    static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => InstalarEnEscena();

    static void InstalarEnEscena()
    {
        GameObject lab = GameObject.Find("Laberinto");
        if (lab && !lab.GetComponent<LianasLaberinto>())
        {
            lab.AddComponent<LianasLaberinto>();
            Debug.Log("[LianasLaberinto] Componente agregado automaticamente al Laberinto.", lab);
        }
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        // Vista previa en el editor (no se guarda en la escena).
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += PrevisualizarSiSigueActivo;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        // Se borra un frame despues: destruir objetos dentro de OnDisable puede fallar.
        GameObject go = ObtenerObjetoGenerado(false);
        Mesh m = malla;
        malla = null;
        listo = false;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (go) DestroyImmediate(go);
            if (m) DestroyImmediate(m);
        };
#endif
    }

#if UNITY_EDITOR
    void PrevisualizarSiSigueActivo()
    {
        if (this && isActiveAndEnabled && !Application.isPlaying) Previsualizar();
    }
#endif

    void Start()
    {
        if (contenedoresConLianas.Count == 0) AutoConfigurar();
        if (!materialLianas) materialLianas = Resources.Load<Material>("LianasIndigo");

        if (!jugador && !string.IsNullOrEmpty(tagJugador))
        {
            GameObject j = GameObject.FindGameObjectWithTag(tagJugador);
            if (j) jugador = j.transform;
        }

        if (puntosRuta.Count == 0 && rutaGuardada.Count == 0) BuscarPuntosRuta();
        ActualizarRutaGuardada();

        Construir(false);

        if (!hayRuta && !PosicionZonaSegura(out _))
            Debug.LogWarning("[LianasLaberinto] No hay ruta ni zona segura: solo se encenderan las lianas por donde pase el jugador.", this);

        ActualizarCamino(true);
    }

    void Update()
    {
        if (!listo) return;

        if (bloque == null) bloque = new MaterialPropertyBlock();
        rendererLianas.GetPropertyBlock(bloque);
        bloque.SetFloat(IdTiempo, Time.time);
        rendererLianas.SetPropertyBlock(bloque);

        if (todasEncendidas) return;

        // Llegar a la zona segura ya NO enciende todo: fuera de la ruta, las lianas
        // solo se encienden por donde pasa el jugador.
        temporizador -= Time.deltaTime;
        if (temporizador <= 0f)
        {
            temporizador = intervaloRecalculo;
            ActualizarCamino(false);
        }
    }

    // ---------------------------------------------------------------------
    // API publica

    // Enciende todas las lianas del laberinto. Ya no se llama sola al llegar a la zona segura;
    // queda disponible por si algun evento del juego lo necesita.
    public void ActivarTodas()
    {
        if (!listo || todasEncendidas) return;
        todasEncendidas = true;

        float ahora = Time.time;
        bool cambio = false;
        foreach (Tramo t in tramos)
            cambio |= CambiarEstado(t, 1f, ahora + t.distSegura * retrasoPorMetro);

        if (cambio) malla.SetUVs(1, datos);
    }

    // Vuelve al modo "solo el camino" (por si el jugador reinicia el nivel, etc).
    public void VolverAModoCamino()
    {
        todasEncendidas = false;
        ultimaCeldaJugador = int.MinValue;
        ActualizarCamino(true);
    }

    // ---------------------------------------------------------------------
    // Construccion

    void Construir(bool todoEncendido)
    {
        listo = false;
        tramos.Clear();

        List<Caja> obstaculos = ObtenerCajas(contenedoresObstaculos);
        List<Caja> conLianas = ObtenerCajas(contenedoresConLianas);
        if (conLianas.Count == 0)
        {
            Debug.LogWarning("[LianasLaberinto] No se encontraron cubos para las lianas.", this);
            return;
        }

        ConstruirCuadricula(obstaculos, conLianas);
        CalcularDistanciasZonaSegura();
        CalcularRuta();
        ConstruirMalla(conLianas, obstaculos, todoEncendido);
        listo = malla != null;
    }

    List<Caja> ObtenerCajas(List<Transform> contenedores)
    {
        var lista = new List<Caja>();
        var vistos = new HashSet<Transform>();
        foreach (Transform cont in contenedores)
        {
            if (!cont) continue;
            foreach (Transform t in cont.GetComponentsInChildren<Transform>(false))
            {
                if (t == cont || !vistos.Add(t)) continue;

                Vector3 centro, tam;
                BoxCollider bc = t.GetComponent<BoxCollider>();
                if (bc) { centro = bc.center; tam = bc.size; }
                else
                {
                    MeshFilter mf = t.GetComponent<MeshFilter>();
                    if (!mf || !mf.sharedMesh) continue;
                    centro = mf.sharedMesh.bounds.center;
                    tam = mf.sharedMesh.bounds.size;
                }

                Vector3 ls = t.lossyScale;
                lista.Add(new Caja
                {
                    t = t,
                    c = t.TransformPoint(centro),
                    ax = t.right,
                    ay = t.up,
                    az = t.forward,
                    hx = Mathf.Abs(ls.x * tam.x) * 0.5f,
                    hy = Mathf.Abs(ls.y * tam.y) * 0.5f,
                    hz = Mathf.Abs(ls.z * tam.z) * 0.5f,
                });
            }
        }
        return lista;
    }

    static bool Contiene(in Caja b, Vector3 p, float margen)
    {
        Vector3 d = p - b.c;
        return Mathf.Abs(Vector3.Dot(d, b.ax)) <= b.hx + margen
            && Mathf.Abs(Vector3.Dot(d, b.ay)) <= b.hy + margen
            && Mathf.Abs(Vector3.Dot(d, b.az)) <= b.hz + margen;
    }

    static Bounds LimitesMundo(in Caja b)
    {
        Vector3 e = new Vector3(
            Mathf.Abs(b.ax.x) * b.hx + Mathf.Abs(b.ay.x) * b.hy + Mathf.Abs(b.az.x) * b.hz,
            Mathf.Abs(b.ax.y) * b.hx + Mathf.Abs(b.ay.y) * b.hy + Mathf.Abs(b.az.y) * b.hz,
            Mathf.Abs(b.ax.z) * b.hx + Mathf.Abs(b.ay.z) * b.hy + Mathf.Abs(b.az.z) * b.hz);
        return new Bounds(b.c, e * 2f);
    }

    static bool LimitesDe(Transform t, out Bounds b)
    {
        b = default;
        if (!t) return false;
        Collider col = t.GetComponent<Collider>();
        if (col) { b = col.bounds; return true; }
        Renderer r = t.GetComponent<Renderer>();
        if (r) { b = r.bounds; return true; }
        return false;
    }

    void ConstruirCuadricula(List<Caja> obstaculos, List<Caja> conLianas)
    {
        Bounds area;
        if (!LimitesDe(piso, out area))
        {
            // Sin piso: usar el area que ocupan las paredes.
            area = LimitesMundo(conLianas[0]);
            foreach (Caja c in conLianas) area.Encapsulate(LimitesMundo(c));
            pisoY = area.min.y;
        }
        else pisoY = area.max.y;

        Bounds limTecho;
        techoY = LimitesDe(techo, out limTecho) ? limTecho.min.y : pisoY + 4f;

        origen = new Vector2(area.min.x, area.min.z);
        nx = Mathf.Max(1, Mathf.CeilToInt(area.size.x / tamanoCelda));
        nz = Mathf.Max(1, Mathf.CeilToInt(area.size.z / tamanoCelda));
        caminable = new bool[nx * nz];
        for (int i = 0; i < caminable.Length; i++) caminable[i] = true;

        // Franja de altura del cuerpo del jugador: lo que la cruce bloquea el paso.
        float yBajo = pisoY + 0.15f, yAlto = pisoY + 1.7f;

        foreach (Caja b in obstaculos)
        {
            Bounds lb = LimitesMundo(b);
            if (lb.max.y < yBajo || lb.min.y > yAlto) continue;

            int x0 = Mathf.Max(0, Mathf.FloorToInt((lb.min.x - radioJugador - origen.x) / tamanoCelda));
            int x1 = Mathf.Min(nx - 1, Mathf.FloorToInt((lb.max.x + radioJugador - origen.x) / tamanoCelda));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((lb.min.z - radioJugador - origen.y) / tamanoCelda));
            int z1 = Mathf.Min(nz - 1, Mathf.FloorToInt((lb.max.z + radioJugador - origen.y) / tamanoCelda));

            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                Vector3 p = CentroCelda(x, z);
                p.y = b.c.y; // solo importa la huella en XZ
                if (Contiene(b, p, radioJugador)) caminable[x + z * nx] = false;
            }
        }

        distSegura = new int[nx * nz];
        marcaCerca = new int[nx * nz];
    }

    Vector3 CentroCelda(int x, int z)
    {
        return new Vector3(origen.x + (x + 0.5f) * tamanoCelda, pisoY, origen.y + (z + 0.5f) * tamanoCelda);
    }

    Vector3 CentroCelda(int i) => CentroCelda(i % nx, i / nx);

    int IndiceCelda(Vector3 p)
    {
        int x = Mathf.FloorToInt((p.x - origen.x) / tamanoCelda);
        int z = Mathf.FloorToInt((p.z - origen.y) / tamanoCelda);
        if (x < 0 || z < 0 || x >= nx || z >= nz) return -1;
        return x + z * nx;
    }

    // Celda caminable mas cercana a p (buscando hasta 'radio' celdas). -1 si no hay.
    int CeldaCaminable(Vector3 p, int radio)
    {
        int cx = Mathf.FloorToInt((p.x - origen.x) / tamanoCelda);
        int cz = Mathf.FloorToInt((p.z - origen.y) / tamanoCelda);
        int mejor = -1;
        float mejorD = float.MaxValue;
        for (int dz = -radio; dz <= radio; dz++)
        for (int dx = -radio; dx <= radio; dx++)
        {
            int x = cx + dx, z = cz + dz;
            if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
            int i = x + z * nx;
            if (!caminable[i]) continue;
            float d = dx * dx + dz * dz;
            if (d < mejorD) { mejorD = d; mejor = i; }
        }
        return mejor;
    }

    static readonly int[] VecX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] VecZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

    bool Vecino(int i, int k, out int j)
    {
        int x = i % nx, z = i / nx;
        int x2 = x + VecX[k], z2 = z + VecZ[k];
        j = -1;
        if (x2 < 0 || z2 < 0 || x2 >= nx || z2 >= nz) return false;
        j = x2 + z2 * nx;
        if (!caminable[j]) return false;
        // Diagonales: no cortar esquinas de paredes.
        if (k >= 4 && (!caminable[x2 + z * nx] || !caminable[x + z2 * nx])) return false;
        return true;
    }

    // BFS desde la zona segura: distancia (en pasos) de cada celda caminable.
    void CalcularDistanciasZonaSegura()
    {
        for (int i = 0; i < distSegura.Length; i++) distSegura[i] = -1;
        if (!PosicionZonaSegura(out Vector3 posSegura)) return;

        var cola = new Queue<int>();
        Bounds bz;
        if (zonaSegura && LimitesDe(zonaSegura, out bz))
        {
            for (int i = 0; i < caminable.Length; i++)
            {
                if (!caminable[i]) continue;
                Vector3 c = CentroCelda(i);
                if (c.x >= bz.min.x && c.x <= bz.max.x && c.z >= bz.min.z && c.z <= bz.max.z)
                {
                    distSegura[i] = 0;
                    cola.Enqueue(i);
                }
            }
        }

        if (cola.Count == 0)
        {
            int c = CeldaCaminable(posSegura, 8);
            if (c < 0)
            {
                Debug.LogWarning("[LianasLaberinto] La zona segura no esta sobre una parte caminable del laberinto.", this);
                return;
            }
            distSegura[c] = 0;
            cola.Enqueue(c);
        }

        while (cola.Count > 0)
        {
            int i = cola.Dequeue();
            for (int k = 0; k < 8; k++)
            {
                if (Vecino(i, k, out int j) && distSegura[j] < 0)
                {
                    distSegura[j] = distSegura[i] + 1;
                    cola.Enqueue(j);
                }
            }
        }
    }

    // Fase de la onda: por la ruta marcada si la liana esta cerca de ella
    // (asi la onda corre a lo largo de la ruta), si no, por el camino mas corto.
    float DistanciaSeguraEn(Vector3 p, float porDefecto)
    {
        int i = IndiceCelda(p);
        if (i >= 0 && distRuta != null && distRuta[i] >= 0f) return distRuta[i];
        if (i >= 0 && distSegura[i] >= 0) return distSegura[i] * tamanoCelda;
        return porDefecto;
    }

    // Recorre la ruta marcada punto a punto (por los pasillos, sin atravesar paredes)
    // y marca las celdas cercanas con los metros que faltan hasta la zona segura.
    void CalcularRuta()
    {
        distRuta = new float[nx * nz];
        descubierta = new bool[nx * nz];
        for (int i = 0; i < distRuta.Length; i++) distRuta[i] = -1f;
        camino.Clear();
        hayRuta = false;

        List<Vector3> puntos = RutaMundo();
        if (puntos.Count < 2) return;

        var celdasRuta = new List<int>();
        int anterior = CeldaCaminable(puntos[0], 6);
        if (anterior < 0)
        {
            Debug.LogWarning("[LianasLaberinto] El primer punto de la ruta (p1) no esta sobre una parte caminable.", this);
            return;
        }
        celdasRuta.Add(anterior);
        for (int k = 1; k < puntos.Count; k++)
        {
            int siguiente = CeldaCaminable(puntos[k], 6);
            if (siguiente < 0)
            {
                Debug.LogWarning($"[LianasLaberinto] El punto {k} de la ruta no esta sobre una parte caminable; se salta.", this);
                continue;
            }
            List<int> tramo = CaminoEntre(anterior, siguiente);
            if (tramo == null)
            {
                Debug.LogWarning($"[LianasLaberinto] No hay paso caminando entre los puntos {k - 1} y {k} de la ruta.", this);
                continue;
            }
            for (int i = 1; i < tramo.Count; i++) celdasRuta.Add(tramo[i]);
            anterior = siguiente;
        }
        if (celdasRuta.Count < 2) return;

        // Metros que faltan desde cada celda de la ruta hasta el final.
        var falta = new float[celdasRuta.Count];
        for (int i = celdasRuta.Count - 2; i >= 0; i--)
            falta[i] = falta[i + 1] + Vector3.Distance(CentroCelda(celdasRuta[i]), CentroCelda(celdasRuta[i + 1]));

        // Expandir a las celdas cercanas (caminando), heredando la distancia de la ruta.
        int radio = Mathf.CeilToInt(distanciaCamino / tamanoCelda);
        var pasos = new int[nx * nz];
        var cola = new Queue<int>();
        for (int i = 0; i < celdasRuta.Count; i++)
        {
            int c = celdasRuta[i];
            if (distRuta[c] >= 0f && distRuta[c] <= falta[i]) continue;
            distRuta[c] = falta[i];
            pasos[c] = 0;
            cola.Enqueue(c);
        }
        while (cola.Count > 0)
        {
            int i = cola.Dequeue();
            if (pasos[i] >= radio) continue;
            for (int k = 0; k < 4; k++)
            {
                if (Vecino(i, k, out int j) && distRuta[j] < 0f)
                {
                    distRuta[j] = distRuta[i];
                    pasos[j] = pasos[i] + 1;
                    cola.Enqueue(j);
                }
            }
        }

        camino.AddRange(celdasRuta);
        hayRuta = true;
    }

    // Camino mas corto (BFS) entre dos celdas caminables.
    List<int> CaminoEntre(int desde, int hasta)
    {
        if (desde == hasta) return new List<int> { desde };
        var previo = new Dictionary<int, int> { [desde] = desde };
        var cola = new Queue<int>();
        cola.Enqueue(desde);
        while (cola.Count > 0)
        {
            int i = cola.Dequeue();
            if (i == hasta) break;
            for (int k = 0; k < 8; k++)
            {
                if (Vecino(i, k, out int j) && !previo.ContainsKey(j))
                {
                    previo[j] = i;
                    cola.Enqueue(j);
                }
            }
        }
        if (!previo.ContainsKey(hasta)) return null;
        var lista = new List<int>();
        for (int c = hasta; c != desde; c = previo[c]) lista.Add(c);
        lista.Add(desde);
        lista.Reverse();
        return lista;
    }

    float DistanciaEuclidea(Vector3 p)
    {
        Vector3 destino = PosicionZonaSegura(out Vector3 ps) ? ps : transform.position;
        p.y = destino.y;
        return Vector3.Distance(p, destino);
    }

    // ---------------------------------------------------------------------
    // Malla de lianas

    readonly List<Vector3> vPos = new List<Vector3>();
    readonly List<Vector3> vNor = new List<Vector3>();
    readonly List<Vector4> vDat = new List<Vector4>();
    readonly List<int> vTri = new List<int>();   // submalla 0: paredes y piso
    readonly List<int> vTriV = new List<int>();  // submalla 1: bloques rosas (material propio)
    List<int> trisActual;
    readonly List<Vector3> puntos = new List<Vector3>();
    readonly List<float> radios = new List<float>();
    Transform destinoMalla;

    void ConstruirMalla(List<Caja> conLianas, List<Caja> obstaculos, bool todoEncendido)
    {
        destinoMalla = ObtenerObjetoGenerado(true).transform;
        vPos.Clear(); vNor.Clear(); vDat.Clear(); vTri.Clear(); vTriV.Clear();
        trisActual = vTri;

        var rnd = new System.Random(semilla);
        float inicial = todoEncendido ? 1f : 0f;

        var verticales = new HashSet<Transform>();
        foreach (Transform cont in contenedoresVerticales)
            if (cont) foreach (Transform t in cont.GetComponentsInChildren<Transform>(true)) verticales.Add(t);
        var sinHorizontales = new VenaHorizontal[0];

        for (int ci = 0; ci < conLianas.Count; ci++)
        {
            Caja b = conLianas[ci];
            Modo modo = verticales.Contains(b.t) ? Modo.Vertical : Modo.Pared;
            for (int cara = 0; cara < 4; cara++)
            {
                // Cara vertical: esquina inferior O, direccion horizontal R, normal N.
                Vector3 N, R; float ancho;
                switch (cara)
                {
                    case 0: N = b.ax; R = b.az; ancho = b.hz * 2f; break;
                    case 1: N = -b.ax; R = -b.az; ancho = b.hz * 2f; break;
                    case 2: N = b.az; R = -b.ax; ancho = b.hx * 2f; break;
                    default: N = -b.az; R = b.ax; ancho = b.hx * 2f; break;
                }
                float prof = cara < 2 ? b.hx : b.hz;
                Vector3 U = b.ay;
                Vector3 O = b.c + N * prof - R * (ancho * 0.5f) - U * b.hy;

                float vIni = Mathf.Max(0f, pisoY - O.y);
                float vFin = Mathf.Min(b.hy * 2f, techoY - O.y);
                if (vFin - vIni < 0.2f || ancho < 0.05f) continue;

                int n = Mathf.Max(1, Mathf.CeilToInt(ancho / anchoTramo));
                float anchoT = ancho / n;

                // Venas horizontales de toda la cara: mismos parametros en todos sus tramos
                // para que se vean continuas de punta a punta de la pared.
                int nh = Mathf.Max(0, venasHorizontales);
                var horizontales = new VenaHorizontal[nh + (raizEnLaBase ? 1 : 0)];
                for (int h = 0; h < nh; h++)
                {
                    float f = (h + Aleatorio(rnd, 0.25f, 0.75f)) / nh;
                    horizontales[h] = new VenaHorizontal
                    {
                        altura = vIni + (vFin - vIni) * Mathf.Lerp(0.15f, 0.9f, f),
                        amp = Aleatorio(rnd, 0.08f, 0.3f),
                        frec = Aleatorio(rnd, 0.9f, 2.2f),
                        fase = Aleatorio(rnd, 0f, Mathf.PI * 2f),
                        radio = grosor * Aleatorio(rnd, 0.45f, 0.7f),
                    };
                }
                if (raizEnLaBase)
                {
                    horizontales[nh] = new VenaHorizontal
                    {
                        altura = vIni + 0.07f, amp = 0.05f, frec = 2.3f,
                        fase = Aleatorio(rnd, 0f, Mathf.PI * 2f), radio = grosor * 0.8f,
                    };
                }

                for (int k = 0; k < n; k++)
                {
                    float u0 = k * anchoT, u1 = u0 + anchoT;
                    float uMed = (u0 + u1) * 0.5f;

                    // Saltar tramos tapados por otra pared o fuera del piso.
                    Vector3 prueba = O + R * uMed + U * (vIni + Mathf.Min(1f, (vFin - vIni) * 0.5f)) + N * 0.08f;
                    if (Tapado(prueba, b.t, conLianas, obstaculos)) continue;
                    if (IndiceCelda(prueba) < 0) continue;

                    var tramo = new Tramo { vIni = vPos.Count, prev = inicial, next = inicial, tCambio = -100f };

                    Vector3 frente = O + R * uMed + N * (radioJugador + tamanoCelda);
                    tramo.celda = CeldaCaminable(frente, 3);
                    tramo.distSegura = tramo.celda >= 0 && distSegura[tramo.celda] >= 0
                        ? distSegura[tramo.celda] * tamanoCelda
                        : DistanciaEuclidea(frente);

                    GenerarTramo(rnd, tramo, O, R, U, N, ancho, u0, u1, vIni, vFin,
                                 modo == Modo.Vertical ? sinHorizontales : horizontales, modo, grosor);

                    tramo.vCant = vPos.Count - tramo.vIni;
                    if (tramo.vCant > 0) tramos.Add(tramo);
                }
            }
        }

        // Venas sobre el piso, por baldosas (solo donde se puede caminar).
        if (lianasEnPiso && venasPorBaldosa > 0)
        {
            float lado = tamanoBaldosaPiso;
            float anchoX = nx * tamanoCelda, anchoZ = nz * tamanoCelda;
            int bx = Mathf.Max(1, Mathf.CeilToInt(anchoX / lado));
            int bz = Mathf.Max(1, Mathf.CeilToInt(anchoZ / lado));
            for (int z = 0; z < bz; z++)
            for (int x = 0; x < bx; x++)
            {
                Vector3 O = new Vector3(origen.x + x * lado, pisoY, origen.y + z * lado);
                Vector3 centro = O + new Vector3(lado * 0.5f, 0f, lado * 0.5f);
                int celda = CeldaCaminable(centro, Mathf.CeilToInt(lado * 0.5f / tamanoCelda));
                if (celda < 0) continue;

                var tramo = new Tramo { vIni = vPos.Count, prev = inicial, next = inicial, tCambio = -100f, celda = celda };
                tramo.distSegura = distSegura[celda] >= 0 ? distSegura[celda] * tamanoCelda : DistanciaEuclidea(centro);

                GenerarTramo(rnd, tramo, O, Vector3.right, Vector3.forward, Vector3.up,
                             lado, 0f, lado, 0f, lado, sinHorizontales, Modo.Piso, grosorPiso);

                tramo.vCant = vPos.Count - tramo.vIni;
                if (tramo.vCant > 0) tramos.Add(tramo);
            }
        }

        if (malla == null)
        {
            malla = new Mesh { name = "Lianas Laberinto" };
            malla.hideFlags = HideFlags.DontSave;
        }
        malla.Clear();
        malla.indexFormat = vPos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        malla.SetVertices(vPos);
        malla.SetNormals(vNor);
        malla.SetUVs(1, vDat);
        malla.subMeshCount = 2;
        malla.SetTriangles(vTri, 0);
        malla.SetTriangles(vTriV, 1);
        malla.RecalculateBounds();
        datos = vDat.ToArray();

        if (!materialLianasVerticales) materialLianasVerticales = Resources.Load<Material>("LianasVerticales");

        destinoMalla.GetComponent<MeshFilter>().sharedMesh = malla;
        rendererLianas = destinoMalla.GetComponent<MeshRenderer>();
        rendererLianas.sharedMaterials = new[] { materialLianas, materialLianasVerticales ? materialLianasVerticales : materialLianas };
        rendererLianas.shadowCastingMode = ShadowCastingMode.Off;
        rendererLianas.receiveShadows = false;

        if (!materialLianas)
            Debug.LogWarning("[LianasLaberinto] Falta asignar el material de las lianas (shader Custom/LianasIndigo).", this);
    }

    static bool Tapado(Vector3 p, Transform propio, List<Caja> a, List<Caja> b)
    {
        foreach (Caja c in a) if (c.t != propio && Contiene(c, p, 0f)) return true;
        foreach (Caja c in b) if (c.t != propio && Contiene(c, p, 0f)) return true;
        return false;
    }

    float Aleatorio(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);

    struct VenaHorizontal
    {
        public float altura, amp, frec, fase, radio;
    }

    enum Modo { Pared, Vertical, Piso }

    void GenerarTramo(System.Random rnd, Tramo tramo, Vector3 O, Vector3 R, Vector3 U, Vector3 N,
                      float ancho, float u0, float u1, float vIni, float vFin, VenaHorizontal[] horizontales,
                      Modo modo, float grosorBase)
    {
        float alto = vFin - vIni;
        float margen = Mathf.Min(0.03f, ancho * 0.25f);
        const float paso = 0.15f;

        // Pared: venas que salen de la base y avanzan en diagonal / de lado.
        // Vertical: venas rectas que suben desde abajo o cuelgan desde arriba (paredes rosadas).
        // Piso: venas que se extienden en cualquier direccion sobre el suelo.
        int venas = modo == Modo.Piso ? venasPorBaldosa
            : modo == Modo.Vertical ? Mathf.Max(2, Mathf.RoundToInt((u1 - u0) * densidadVerticales))
            : Mathf.Max(1, Mathf.RoundToInt((u1 - u0) * densidadVenas));
        trisActual = modo == Modo.Vertical ? vTriV : vTri;

        for (int s = 0; s < venas; s++)
        {
            float u = Mathf.Lerp(u0, u1, (s + Aleatorio(rnd, 0.2f, 0.8f)) / venas);
            float v, ang, angBase = 0f, largo, ruido;
            bool limitarAngulo;

            switch (modo)
            {
                case Modo.Vertical:
                {
                    bool desdeArriba = rnd.NextDouble() < 0.5;
                    v = desdeArriba ? vFin - 0.05f : vIni + 0.05f;
                    angBase = desdeArriba ? -Mathf.PI * 0.5f : Mathf.PI * 0.5f;
                    ang = angBase + Aleatorio(rnd, -0.15f, 0.15f);
                    largo = alto * Aleatorio(rnd, 0.5f, 1f);
                    ruido = 0.07f;
                    limitarAngulo = true;
                    break;
                }
                case Modo.Piso:
                    v = Aleatorio(rnd, vIni, vFin);
                    ang = Aleatorio(rnd, 0f, Mathf.PI * 2f);
                    largo = Aleatorio(rnd, 0.6f, 1f) * largoVenas;
                    ruido = 0.22f;
                    limitarAngulo = false;
                    break;
                default:
                {
                    v = vIni + Aleatorio(rnd, 0f, alto * 0.35f);
                    float signo = rnd.NextDouble() < 0.5 ? -1f : 1f;
                    // Angulo: 0 = horizontal, PI/2 = vertical.
                    float inclinacion = Mathf.Lerp(1.35f, 0.25f, tendenciaHorizontal) + Aleatorio(rnd, -0.2f, 0.2f);
                    ang = signo > 0 ? inclinacion : Mathf.PI - inclinacion;
                    largo = Aleatorio(rnd, 0.6f, 1f) * largoVenas;
                    ruido = 0.18f;
                    limitarAngulo = false;
                    break;
                }
            }

            float vaiven = Aleatorio(rnd, 0.6f, 1.2f);
            float fase = Aleatorio(rnd, 0f, Mathf.PI * 2f);

            puntos.Clear(); radios.Clear();
            int pasos = Mathf.Max(3, Mathf.CeilToInt(largo / paso));
            for (int i = 0; i <= pasos; i++)
            {
                float f = i / (float)pasos;
                float r = Mathf.Lerp(grosorBase, grosorBase * 0.2f, f);
                Vector3 p = PuntoCara(O, R, U, N, u, v, r);

                // En el piso, la vena se corta al llegar a una pared.
                if (modo == Modo.Piso)
                {
                    int c = IndiceCelda(p);
                    if (c < 0 || !caminable[c]) break;
                }

                puntos.Add(p);
                radios.Add(r);

                // Giro suave tipo "vaiven" + ruido, y rebote en los bordes.
                ang += Mathf.Sin(i * 0.35f + fase) * 0.12f * vaiven * (modo == Modo.Vertical ? 0.4f : 1f)
                     + Aleatorio(rnd, -ruido, ruido);
                if (limitarAngulo) ang = Mathf.Clamp(ang, angBase - 0.4f, angBase + 0.4f);
                else if (modo == Modo.Pared) ang = Mathf.Clamp(ang, -0.35f, Mathf.PI + 0.35f);

                float nu = u + Mathf.Cos(ang) * paso;
                float nv = v + Mathf.Sin(ang) * paso;
                bool fueraV = nv < vIni + 0.05f || nv > vFin - 0.05f;
                if (modo == Modo.Vertical && fueraV) break; // llego al otro extremo
                if (nu < margen || nu > ancho - margen) { ang = Mathf.PI - ang; nu = Mathf.Clamp(nu, margen, ancho - margen); }
                if (fueraV) { ang = -ang; nv = Mathf.Clamp(nv, vIni + 0.05f, vFin - 0.05f); }
                u = nu; v = nv;
            }
            if (puntos.Count < 3) continue;
            pasos = puntos.Count - 1;
            var tronco = new List<Vector3>(puntos);
            var radiosTronco = new List<float>(radios);
            AgregarTubo(tramo, N);

            // Ramas: hacia los lados en paredes, en diagonal en el piso y paredes rosadas.
            int ramas = rnd.Next(1, ramasMaximas + 1);
            for (int rr = 0; rr < ramas; rr++)
            {
                int idx = Mathf.Clamp(Mathf.RoundToInt(Aleatorio(rnd, 0.15f, 0.8f) * pasos), 1, pasos - 1);
                float ub = Vector3.Dot(tronco[idx] - O, R);
                float vb = Vector3.Dot(tronco[idx] - O, U);
                float sb = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float ab;
                if (modo == Modo.Pared)
                    ab = (sb > 0 ? 0f : Mathf.PI) + sb * Aleatorio(rnd, -0.25f, 0.45f);
                else
                {
                    Vector3 d = tronco[idx + 1] - tronco[idx];
                    float angTronco = Mathf.Atan2(Vector3.Dot(d, U), Vector3.Dot(d, R));
                    ab = angTronco + sb * Aleatorio(rnd, modo == Modo.Vertical ? 0.25f : 0.5f, modo == Modo.Vertical ? 0.6f : 1.1f);
                }
                float lb = Aleatorio(rnd, 0.5f, 1.6f);
                float r0 = radiosTronco[idx] * 0.75f;

                puntos.Clear(); radios.Clear();
                int pr = Mathf.Max(3, Mathf.CeilToInt(lb / paso));
                for (int i = 0; i <= pr; i++)
                {
                    float f = i / (float)pr;
                    float r = Mathf.Lerp(r0, 0.006f, f);
                    Vector3 p = PuntoCara(O, R, U, N, ub, vb, r);
                    if (modo == Modo.Piso)
                    {
                        int c = IndiceCelda(p);
                        if (c < 0 || !caminable[c]) break;
                    }
                    puntos.Add(p);
                    radios.Add(r);
                    ab += Aleatorio(rnd, modo == Modo.Vertical ? -0.1f : -0.25f, modo == Modo.Vertical ? 0.1f : 0.25f);
                    ub = Mathf.Clamp(ub + Mathf.Cos(ab) * paso, margen, ancho - margen);
                    vb = Mathf.Clamp(vb + Mathf.Sin(ab) * paso, vIni + 0.05f, vFin - 0.05f);
                }
                AgregarTubo(tramo, N);
            }
        }

        // Venas horizontales (y la raiz de la base) que cruzan el tramo de lado a lado.
        float ua = Mathf.Max(margen, u0 - 0.02f), ubFin = Mathf.Min(ancho - margen, u1 + 0.02f);
        foreach (VenaHorizontal h in horizontales)
        {
            puntos.Clear(); radios.Clear();
            int pasos = Mathf.Max(2, Mathf.CeilToInt((ubFin - ua) / paso));
            for (int i = 0; i <= pasos; i++)
            {
                float uh = Mathf.Lerp(ua, ubFin, i / (float)pasos);
                float vh = h.altura + h.amp * Mathf.Sin(uh * h.frec + h.fase)
                                    + h.amp * 0.4f * Mathf.Sin(uh * h.frec * 2.7f + h.fase * 1.3f);
                vh = Mathf.Clamp(vh, vIni + 0.04f, vFin - 0.04f);
                puntos.Add(PuntoCara(O, R, U, N, uh, vh, h.radio));
                radios.Add(h.radio);
            }
            AgregarTubo(tramo, N);
        }
    }

    // Punto sobre la cara de la pared, medio incrustado para que parezca pegado.
    static Vector3 PuntoCara(Vector3 O, Vector3 R, Vector3 U, Vector3 N, float u, float v, float radio)
    {
        return O + R * u + U * v + N * (radio * 0.5f + 0.004f);
    }

    // Convierte 'puntos'/'radios' en un tubo y lo agrega a la malla.
    void AgregarTubo(Tramo tramo, Vector3 N)
    {
        int n = puntos.Count;
        if (n < 2) return;
        int lados = Mathf.Clamp(ladosTubo, 3, 8);
        int baseIdx = vPos.Count;
        float ahora = tramo.tCambio;

        for (int i = 0; i < n; i++)
        {
            Vector3 tan = (puntos[Mathf.Min(n - 1, i + 1)] - puntos[Mathf.Max(0, i - 1)]).normalized;
            Vector3 lado = Vector3.Cross(tan, N);
            if (lado.sqrMagnitude < 1e-6f) lado = Vector3.Cross(tan, Vector3.up);
            lado.Normalize();
            Vector3 nor = Vector3.Cross(lado, tan).normalized;

            // Fase de la onda = distancia caminando hasta la zona segura frente a este punto.
            Vector3 frente = puntos[i] + N * (radioJugador + tamanoCelda);
            float fase = DistanciaSeguraEn(frente, tramo.distSegura);
            var dato = new Vector4(fase, tramo.prev, tramo.next, ahora);

            for (int a = 0; a < lados; a++)
            {
                float ang = a / (float)lados * Mathf.PI * 2f;
                Vector3 dir = Mathf.Cos(ang) * nor + Mathf.Sin(ang) * lado;
                vPos.Add(destinoMalla.InverseTransformPoint(puntos[i] + dir * radios[i]));
                vNor.Add(destinoMalla.InverseTransformDirection(dir).normalized);
                vDat.Add(dato);
            }
        }

        for (int i = 0; i < n - 1; i++)
        {
            for (int a = 0; a < lados; a++)
            {
                int a2 = (a + 1) % lados;
                int p00 = baseIdx + i * lados + a;
                int p01 = baseIdx + i * lados + a2;
                int p10 = baseIdx + (i + 1) * lados + a;
                int p11 = baseIdx + (i + 1) * lados + a2;
                trisActual.Add(p00); trisActual.Add(p01); trisActual.Add(p10);
                trisActual.Add(p01); trisActual.Add(p11); trisActual.Add(p10);
            }
        }
    }

    GameObject ObtenerObjetoGenerado(bool crear)
    {
        Transform t = transform.Find(NombreObjetoGenerado);
        if (t) return t.gameObject;
        if (!crear) return null;

        var go = new GameObject(NombreObjetoGenerado, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        if (!Application.isPlaying) go.hideFlags = HideFlags.DontSave;
        return go;
    }

    // ---------------------------------------------------------------------
    // Camino y estados

    bool avisoMostrado;

    // Posicion real del jugador. El objeto con tag puede no ser el que se mueve
    // (ej: rig de VR, o un jugador desactivado), asi que si hace falta se usa la camara.
    bool PosicionJugador(out Vector3 pos)
    {
        if ((!jugador || !jugador.gameObject.activeInHierarchy) && !string.IsNullOrEmpty(tagJugador))
        {
            foreach (GameObject j in GameObject.FindGameObjectsWithTag(tagJugador))
                if (j.activeInHierarchy) { jugador = j.transform; break; }
        }
        if (jugador && jugador.gameObject.activeInHierarchy)
        {
            Camera cam = Camera.main;
            // Si la camara esta lejos del objeto con tag, manda la camara (es lo que ve el jugador).
            pos = cam && (cam.transform.position - jugador.position).sqrMagnitude > 9f
                ? cam.transform.position : jugador.position;
            return true;
        }
        if (Camera.main) { pos = Camera.main.transform.position; return true; }
        pos = default;
        return false;
    }

    // Con ruta marcada: encendidas = la ruta + todo lo que el jugador ya tuvo cerca.
    void ActualizarRutaYDescubrir(bool forzar)
    {
        int celda = -1;
        if (PosicionJugador(out Vector3 pos) && IndiceCelda(pos) >= 0)
            celda = CeldaCaminable(pos, 2);

        if (!forzar && celda == ultimaCeldaJugador) return;
        ultimaCeldaJugador = celda;

        // Marcar como descubierto lo que esta a "radioDescubrir" metros caminando del jugador.
        if (celda >= 0 && radioDescubrir > 0f)
        {
            marcaActual++;
            int radio = Mathf.CeilToInt(radioDescubrir / tamanoCelda);
            var cola = new Queue<(int c, int d)>();
            cola.Enqueue((celda, 0));
            marcaCerca[celda] = marcaActual;
            while (cola.Count > 0)
            {
                var (i, d) = cola.Dequeue();
                descubierta[i] = true;
                if (d >= radio) continue;
                for (int k = 0; k < 4; k++)
                {
                    if (Vecino(i, k, out int j) && marcaCerca[j] != marcaActual)
                    {
                        marcaCerca[j] = marcaActual;
                        cola.Enqueue((j, d + 1));
                    }
                }
            }
        }

        float ahora = Time.time;
        bool cambio = false;
        foreach (Tramo t in tramos)
        {
            bool encender = t.celda >= 0 && (distRuta[t.celda] >= 0f || descubierta[t.celda]);
            cambio |= CambiarEstado(t, encender ? 1f : 0f, ahora);
        }
        if (cambio) malla.SetUVs(1, datos);
    }

    void ActualizarCamino(bool forzar)
    {
        if (!listo || todasEncendidas) return;
        if (hayRuta || !PosicionZonaSegura(out _)) { ActualizarRutaYDescubrir(forzar); return; }

        int celda = -1;
        if (PosicionJugador(out Vector3 posJugador))
        {
            if (IndiceCelda(posJugador) >= 0)
                celda = CeldaCaminable(posJugador, 4);
            else if (!puntoInicio)
            {
                // Fuera del laberinto (ej: recien llega por la cabina): el camino empieza
                // en el punto del laberinto mas cercano al jugador.
                Vector3 p = posJugador;
                p.x = Mathf.Clamp(p.x, origen.x + tamanoCelda * 0.5f, origen.x + (nx - 0.5f) * tamanoCelda);
                p.z = Mathf.Clamp(p.z, origen.y + tamanoCelda * 0.5f, origen.y + (nz - 0.5f) * tamanoCelda);
                celda = CeldaCaminable(p, 10);
            }
        }
        if (celda < 0 && puntoInicio)
            celda = CeldaCaminable(puntoInicio.position, 4);

        if (!avisoMostrado)
        {
            avisoMostrado = true;
            if (celda < 0)
                Debug.LogWarning("[LianasLaberinto] No se pudo ubicar al jugador en el laberinto: no se enciende ningun camino.", this);
            else if (distSegura[celda] < 0)
                Debug.LogWarning("[LianasLaberinto] No hay camino caminable desde el jugador hasta la zona segura.", this);
        }

        if (!forzar && celda == ultimaCeldaJugador) return;
        ultimaCeldaJugador = celda;

        // Recorrer el campo de distancias cuesta abajo hasta la zona segura.
        camino.Clear();
        if (celda >= 0 && distSegura[celda] >= 0)
        {
            int actual = celda;
            camino.Add(actual);
            int seguridad = nx * nz;
            while (distSegura[actual] > 0 && seguridad-- > 0)
            {
                int siguiente = -1;
                for (int k = 0; k < 8; k++) // primero rectos, luego diagonales
                {
                    if (Vecino(actual, k, out int j) && distSegura[j] == distSegura[actual] - 1)
                    {
                        siguiente = j;
                        break;
                    }
                }
                if (siguiente < 0) break;
                actual = siguiente;
                camino.Add(actual);
            }
        }

        // Marcar celdas cercanas al camino (caminando, para no atravesar paredes).
        marcaActual++;
        int radio = Mathf.CeilToInt(distanciaCamino / tamanoCelda);
        var cola = new Queue<int>();
        var pasos = new Dictionary<int, int>();
        foreach (int c in camino)
        {
            if (marcaCerca[c] == marcaActual) continue;
            marcaCerca[c] = marcaActual;
            pasos[c] = 0;
            cola.Enqueue(c);
        }
        while (cola.Count > 0)
        {
            int i = cola.Dequeue();
            int d = pasos[i];
            if (d >= radio) continue;
            for (int k = 0; k < 4; k++)
            {
                if (Vecino(i, k, out int j) && marcaCerca[j] != marcaActual)
                {
                    marcaCerca[j] = marcaActual;
                    pasos[j] = d + 1;
                    cola.Enqueue(j);
                }
            }
        }

        float ahora = Time.time;
        bool cambio = false;
        foreach (Tramo t in tramos)
        {
            float objetivo = t.celda >= 0 && marcaCerca[t.celda] == marcaActual ? 1f : 0f;
            cambio |= CambiarEstado(t, objetivo, ahora);
        }
        if (cambio) malla.SetUVs(1, datos);
    }

    bool CambiarEstado(Tramo t, float objetivo, float momento)
    {
        if (Mathf.Approximately(t.next, objetivo)) return false;

        float ahora = Time.time;
        t.prev = ValorActual(t, ahora);
        t.next = objetivo;
        t.tCambio = momento;

        for (int i = t.vIni; i < t.vIni + t.vCant; i++)
        {
            Vector4 d = datos[i];
            d.y = t.prev; d.z = t.next; d.w = t.tCambio;
            datos[i] = d;
        }
        return true;
    }

    // Debe coincidir con el calculo del shader.
    float ValorActual(Tramo t, float ahora)
    {
        float fade = materialLianas && materialLianas.HasProperty(IdFade) ? materialLianas.GetFloat(IdFade) : 0.8f;
        float x = Mathf.Clamp01((ahora - t.tCambio) / Mathf.Max(0.01f, fade));
        x = x * x * (3f - 2f * x);
        return Mathf.Lerp(t.prev, t.next, x);
    }

    // ---------------------------------------------------------------------
    // Editor

    // Para herramientas de editor que mueven/cambian el laberinto.
    public void RegenerarVistaPrevia()
    {
        if (!Application.isPlaying && isActiveAndEnabled) Previsualizar();
    }

    [ContextMenu("Previsualizar lianas (editor)")]
    void Previsualizar()
    {
        if (contenedoresConLianas.Count == 0) AutoConfigurar();
        if (!materialLianas) materialLianas = Resources.Load<Material>("LianasIndigo");
        if (puntosRuta.Count == 0) BuscarPuntosRuta();
        ActualizarRutaGuardada();
        Construir(true);

        // Con ruta marcada, la vista previa muestra encendida solo la ruta (como al empezar a jugar).
        if (listo && hayRuta)
        {
            foreach (Tramo t in tramos)
            {
                float v = t.celda >= 0 && distRuta[t.celda] >= 0f ? 1f : 0f;
                t.prev = t.next = v;
                for (int i = t.vIni; i < t.vIni + t.vCant; i++) { Vector4 d = datos[i]; d.y = v; d.z = v; datos[i] = d; }
            }
            malla.SetUVs(1, datos);
        }
        if (listo)
            Debug.Log($"[LianasLaberinto] {tramos.Count} tramos, {vPos.Count} vertices. Ruta marcada: " +
                      (hayRuta ? $"{puntosRuta.Count} puntos, {camino.Count} celdas, {(camino.Count > 1 ? distRuta[camino[0]] : 0f):0} m" : "no"), this);
    }

    [ContextMenu("Borrar previsualizacion")]
    void BorrarPrevisualizacion()
    {
        GameObject go = ObtenerObjetoGenerado(false);
        if (go) DestroyImmediate(go);
        if (malla) DestroyImmediate(malla);
        malla = null;
        listo = false;
    }

    void OnDestroy()
    {
        if (malla)
        {
            if (Application.isPlaying) Destroy(malla);
            else DestroyImmediate(malla);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (camino.Count == 0 || caminable == null) return;
        Gizmos.color = new Color(0.45f, 0.2f, 1f);
        for (int i = 1; i < camino.Count; i++)
            Gizmos.DrawLine(CentroCelda(camino[i - 1]) + Vector3.up * 0.1f, CentroCelda(camino[i]) + Vector3.up * 0.1f);
    }
}
