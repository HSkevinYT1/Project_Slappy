using System.Collections;
using UnityEngine;

// La Zona Roja segun el guion:
// 1. Al entrar (la parte de arriba de un faro): "Ahora un faro, ya veo." y la entrada desaparece (aparece una pared).
// 2. En el piso 1 hay un diario: al leerlo se sabe que faltan 3 notas, que empiezan a brillar en el bosque.
// 3. Las notas llevan hasta el bidon de gasolina (brilla cuando ya se leyeron las 3).
// 4. Con el bidon se rellena el faro (en la luz, arriba, junto a la entrada): la entrada vuelve a abrirse y la zona
//    queda completada (ProgresoCaminos). Al volver a la estacion empieza el final (FinalJuego).
// La luz del faro alumbra siempre, a toda potencia y girando: es la unica luz de la zona (el bidon no la cambia).
// Lo arma Tools > Guion > Novedades (NovedadesGuionSetup). Todo se puede mover y los textos se editan aqui.
public class MisionFaro : MonoBehaviour
{
    public static MisionFaro Instance { get; private set; }

    [Header("Piezas")]
    public Transform centroFaro;
    [Tooltip("Pared que tapa la entrada (escondida al principio)")]
    public GameObject muroEntrada;
    public Lectura diario;
    public Lectura[] notas;
    public Grabbable bidon;
    [Tooltip("La luz del faro: recibe el bidon")]
    public ReceptorObjeto deposito;
    public Light foco;
    [Tooltip("Lo que gira cuando la luz esta prendida (la base y la lente)")]
    public Transform giratoria;
    public Renderer lente;
    [Tooltip("Tramo de ProgresoCaminos que se completa")]
    public string zona = "Zona Roja";
    [Tooltip("Metros desde el centro del faro a los que ya se considera que entro (el faro mide ~8 de radio)")]
    public float radioDentro = 5.5f;

    [Header("Luz del faro")]
    [Tooltip("Grados por segundo que gira la luz")]
    public float velocidadGiro = 35f;
    [Tooltip("Grados que el haz apunta hacia abajo (el faro esta a ~25 m del suelo: horizontal no toca el bosque)")]
    public float inclinacionHaz = 10f;
    [Tooltip("Apertura minima del haz y su alcance minimo")]
    public float anguloHaz = 40f, alcanceHaz = 180f;

    [Header("Pensamientos")]
    [TextArea] public string alEntrar = "Ahora un faro, ya veo.";
    [TextArea] public string alDesaparecer = "Por qué no me sorprende.";
    [TextArea] public string alVerDiario = "¿Y eso?";
    [TextArea] public string alVerNota = "Entonces ahora tengo que hallar las notas, ¿no?";
    [TextArea] public string alAgarrarBidon = "Esto era lo que buscaba.";
    [TextArea] public string alAbrirse = "Al fin. ¿Ahora qué?";

    public bool Entro { get; private set; }
    public bool DiarioLeido => diario == null || diario.Leida;
    public int NotasLeidas { get { int n = 0; if (notas != null) foreach (Lectura l in notas) if (l != null && l.Leida) n++; return n; } }
    public int TotalNotas => notas != null ? notas.Length : 0;
    public bool TieneBidon => bidon != null && Inventario.Instance != null && Inventario.Instance.IndiceDe(bidon) >= 0;
    // Ya se vacio el bidon en el faro (la salida esta abierta)
    public bool Relleno { get; private set; }

    Transform jugador;
    float proxima;
    bool vioNota;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    // Si la escena quedo a medias (el setup se corto antes de conectar la luz y el bidon), se buscan por nombre
    void Completar()
    {
        Transform raiz = transform.root;
        if (foco == null)
            foreach (Light l in raiz.GetComponentsInChildren<Light>(true))
                if (l.type == LightType.Spot && l.name.StartsWith("Foco")) { foco = l; break; }
        if (giratoria == null && foco != null) giratoria = foco.transform.parent;
        if (lente == null && giratoria != null) { Transform t = giratoria.Find("Lente (apagada)"); if (t != null) lente = t.GetComponent<Renderer>(); }
        if (bidon == null) { Transform b = raiz.Find("Bidon de gasolina"); if (b != null) bidon = b.GetComponent<Grabbable>(); }
        if (bidon != null && bidon.TryGetComponent(out Interactable ib))
        {
            if (ib.onInteract == null) ib.onInteract = new UnityEngine.Events.UnityEvent<PlayerInteractor>();
            if (ib.onInteract.GetPersistentEventCount() == 0) ib.onInteract.AddListener(bidon.PickUp);
        }
        if (deposito == null && giratoria != null)
        {
            deposito = giratoria.GetComponent<ReceptorObjeto>();
            if (deposito == null) deposito = giratoria.gameObject.AddComponent<ReceptorObjeto>();
        }
        if (deposito != null && deposito.alRecibir == null) deposito.alRecibir = new UnityEngine.Events.UnityEvent<Grabbable>();
        if (foco == null) Debug.LogWarning("[Faro] No encontré la luz del faro (Foco): la Zona Roja va a quedar a oscuras.", this);
    }

    void Start()
    {
        Completar();
        if (muroEntrada != null) muroEntrada.SetActive(false);
        Brillar(notas, false);
        Interactuable(notas, false);
        if (bidon != null)
        {
            if (bidon.TryGetComponent(out Highlightable h)) h.Hide();
            if (bidon.TryGetComponent(out Interactable i)) i.enabled = false;
        }
        if (diario != null) diario.alLeer.AddListener(AlLeerDiario);
        if (notas != null) foreach (Lectura l in notas) if (l != null) l.alLeer.AddListener(AlLeerNota);
        if (deposito != null)
        {
            deposito.objetoEsperado = bidon;
            deposito.prompt = "Rellenar el faro";
            deposito.ocultarAlRecibir = true;
            deposito.alRecibir.AddListener(_ => StartCoroutine(Rellenar()));
        }
        // La luz alumbra siempre, a toda potencia (es la unica luz de la zona)
        if (foco != null)
        {
            foco.enabled = true;
            // El haz barre el bosque (no solo el horizonte)
            foco.transform.localRotation *= Quaternion.Euler(inclinacionHaz, 0f, 0f);
            if (foco.type == LightType.Spot) foco.spotAngle = Mathf.Max(foco.spotAngle, anguloHaz);
            foco.range = Mathf.Max(foco.range, alcanceHaz);
            // Sombras duras: la niebla las mira muchas veces por pixel (las suaves son varias veces mas caras)
            if (foco.shadows == LightShadows.Soft) foco.shadows = LightShadows.Hard;
            Lente(1f);
        }
    }

    void Update()
    {
        if (Time.time < proxima) return;
        proxima = Time.time + 0.15f;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }

        if (!Entro && centroFaro != null && Plano(jugador.position - centroFaro.position) < radioDentro
            && Mathf.Abs(jugador.position.y - centroFaro.position.y) < 4f)
            StartCoroutine(Entrada());

        if (diario != null && !diario.Leida && Cerca(diario.transform.position, 4f)) Pensamiento.Decir("faro.diario", 0.2f, alVerDiario);

        if (!vioNota && DiarioLeido && notas != null)
            foreach (Lectura l in notas)
                if (l != null && !l.Leida && Cerca(l.transform.position, 14f) && Visible(l.transform.position))
                {
                    vioNota = true;
                    Pensamiento.Decir("faro.nota", 0.3f, alVerNota);
                    break;
                }

        if (TieneBidon) Pensamiento.Decir("faro.bidon", 0.2f, alAgarrarBidon);
    }

    // Entro al faro: la entrada desaparece detras de el
    IEnumerator Entrada()
    {
        Entro = true;
        Pensamiento.Decir("faro.entrar", 0.3f, alEntrar);
        yield return new WaitForSeconds(2.4f);
        if (muroEntrada != null)
        {
            float[] parpadeo = { 0.06f, 0.08f, 0.05f, 0.12f, 0.05f };
            for (int i = 0; i < parpadeo.Length; i++)
            {
                muroEntrada.SetActive(i % 2 == 0);
                yield return new WaitForSeconds(parpadeo[i]);
            }
            muroEntrada.SetActive(true);
            Jugo.Sacudir(0.25f);
        }
        Pensamiento.Decir("faro.desaparece", 0.5f, alDesaparecer);
    }

    void AlLeerDiario()
    {
        Brillar(notas, true);
        Interactuable(notas, true);
    }

    void AlLeerNota()
    {
        if (NotasLeidas < TotalNotas || bidon == null) return;
        if (bidon.TryGetComponent(out Highlightable h)) h.Show();
        if (bidon.TryGetComponent(out Interactable i)) i.enabled = true;
    }

    // Se vacia el bidon en el faro: la entrada vuelve a abrirse (la luz no cambia: alumbra siempre)
    IEnumerator Rellenar()
    {
        if (Relleno) yield break;
        Relleno = true;
        yield return new WaitForSeconds(0.8f);
        Jugo.Sacudir(0.2f);
        yield return new WaitForSeconds(1f);
        if (muroEntrada != null) muroEntrada.SetActive(false);
        Pensamiento.Decir("faro.abierto", 0.3f, alAbrirse);
        ProgresoCaminos p = ProgresoCaminos.Instance;
        if (p != null && p.TramoActual != null && p.TramoActual.nombre == zona) p.CompletarZonaActual();
    }

    void LateUpdate()
    {
        if (giratoria != null) giratoria.Rotate(0f, velocidadGiro * Time.deltaTime, 0f, Space.World);
        Oscuridad();
    }

    // ------------------------------------------------------------------ Oscuridad

    [Header("Oscuridad")]
    [Tooltip("En la Zona Roja la unica luz es la del faro: sin luz ambiente, cielo negro y las demas luces de la zona apagadas")]
    public bool oscuridad = true;
    public float segundosOscurecer = 1.5f;
    [Tooltip("Luz ambiente que queda (muy tenue): 0 = negro total entre las pasadas del haz")]
    public Color colorNoche = new Color(0.03f, 0.035f, 0.05f);
    [Tooltip("Pasos de la niebla en esta zona (en las demas, los del material: 12). Menos = mas rapido")]
    [Range(4, 12)] public int pasosNiebla = 7;

    FogVolume niebla;
    MaterialPropertyBlock bloqueNiebla;
    float proximoRepaso;

    VolumenZonas volumen;
    float oscuro;
    bool guardado;
    UnityEngine.Rendering.SphericalHarmonicsL2 probe;
    float reflejos;
    Camera camOscura;
    CameraClearFlags fondoAntes;
    Color colorAntes;
    readonly System.Collections.Generic.List<Light> apagadas = new System.Collections.Generic.List<Light>();

    void Oscuridad()
    {
        if (volumen == null) volumen = FindFirstObjectByType<VolumenZonas>();
        ZonaVolumen actual = volumen != null ? volumen.ZonaActual : null;
        bool dentro = oscuridad && actual != null && actual.name == "Trigger " + zona;
        float antes = oscuro;
        oscuro = Mathf.MoveTowards(oscuro, dentro ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, segundosOscurecer));
        if (oscuro <= 0f && antes <= 0f) return;

        if (!guardado)
        {
            guardado = true;
            probe = RenderSettings.ambientProbe;
            reflejos = RenderSettings.reflectionIntensity;
            camOscura = Camera.main;
            if (camOscura != null) { fondoAntes = camOscura.clearFlags; colorAntes = camOscura.backgroundColor; }
            niebla = volumen != null ? volumen.niebla : null;
            proximoRepaso = 0f;
        }
        // Todas las luces de la escena menos la del faro y las del jugador (la linterna): las de las otras zonas
        // (Backrooms, estacion...) no se ven desde aqui, pero igual se dibujan sus sombras cada frame y la niebla
        // las calcula en cada paso. Se repasa cada tanto por si algun script prende una (bombillos que titilan)
        if (oscuro > 0f && dentro && Time.time >= proximoRepaso)
        {
            proximoRepaso = Time.time + 1f;
            Transform jug = jugador != null ? jugador.root : null;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l.enabled) continue;
                if (foco != null && (l == foco || l.transform.IsChildOf(foco.transform))) continue;
                if (jug != null && l.transform.IsChildOf(jug)) continue;
                if (camOscura != null && l.transform.IsChildOf(camOscura.transform.root)) continue;
                l.enabled = false;
                if (!apagadas.Contains(l)) apagadas.Add(l);
            }
        }
        // Niebla mas barata aqui (la isla es enorme y casi toda la pantalla tiene niebla)
        if (niebla != null && niebla.TryGetComponent(out Renderer rn))
        {
            if (bloqueNiebla == null) bloqueNiebla = new MaterialPropertyBlock();
            rn.GetPropertyBlock(bloqueNiebla);
            float normal = rn.sharedMaterial != null && rn.sharedMaterial.HasProperty("_Steps") ? rn.sharedMaterial.GetFloat("_Steps") : 12f;
            bloqueNiebla.SetFloat("_Steps", oscuro > 0f ? Mathf.Min(normal, pasosNiebla) : normal);
            rn.SetPropertyBlock(bloqueNiebla);
        }

        // Sin luz ambiente, salvo un fondo nocturno muy tenue (para que el bosque no sea negro puro entre pasadas del haz)
        var sh = probe * (1f - oscuro);
        sh.AddAmbientLight(colorNoche * oscuro);
        RenderSettings.ambientProbe = sh;
        RenderSettings.reflectionIntensity = reflejos * (1f - oscuro);
        if (camOscura != null)
        {
            camOscura.clearFlags = oscuro > 0.5f ? CameraClearFlags.SolidColor : fondoAntes;
            camOscura.backgroundColor = oscuro > 0.5f ? Color.black : colorAntes;
        }
        if (oscuro <= 0f) Restaurar();
    }

    void Restaurar()
    {
        if (!guardado) return;
        guardado = false;
        RenderSettings.ambientProbe = probe;
        RenderSettings.reflectionIntensity = reflejos;
        if (camOscura != null) { camOscura.clearFlags = fondoAntes; camOscura.backgroundColor = colorAntes; }
        foreach (Light l in apagadas) if (l != null) l.enabled = true;
        apagadas.Clear();
        if (niebla != null && niebla.TryGetComponent(out Renderer rn) && rn.sharedMaterial != null && rn.sharedMaterial.HasProperty("_Steps"))
        {
            if (bloqueNiebla == null) bloqueNiebla = new MaterialPropertyBlock();
            rn.GetPropertyBlock(bloqueNiebla);
            bloqueNiebla.SetFloat("_Steps", rn.sharedMaterial.GetFloat("_Steps"));
            rn.SetPropertyBlock(bloqueNiebla);
        }
    }

    void OnDisable() => Restaurar();

    MaterialPropertyBlock bloque;

    void Lente(float k)
    {
        if (lente == null) return;
        if (bloque == null) bloque = new MaterialPropertyBlock();
        lente.GetPropertyBlock(bloque);
        bloque.SetColor("_EmissionColor", new Color(4f, 3.6f, 2.6f) * k);
        bloque.SetColor("_BaseColor", Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(1f, 0.97f, 0.85f), k));
        lente.SetPropertyBlock(bloque);
    }

    static void Brillar(Lectura[] ls, bool si)
    {
        if (ls == null) return;
        foreach (Lectura l in ls)
            if (l != null && l.TryGetComponent(out Highlightable h)) { if (si) h.Show(); else h.Hide(); }
    }

    static void Interactuable(Lectura[] ls, bool si)
    {
        if (ls == null) return;
        foreach (Lectura l in ls) if (l != null && l.TryGetComponent(out Interactable i)) i.enabled = si;
    }

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    bool Cerca(Vector3 p, float d) => jugador != null && (jugador.position - p).sqrMagnitude < d * d;

    bool Visible(Vector3 p)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(p);
        if (v.z <= 0f || v.x < 0.1f || v.x > 0.9f || v.y < 0.1f || v.y > 0.9f) return false;
        // Hasta un poco antes del punto (si no, choca con la propia nota)
        return !Physics.Linecast(cam.transform.position, Vector3.MoveTowards(p, cam.transform.position, 0.6f),
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
}
