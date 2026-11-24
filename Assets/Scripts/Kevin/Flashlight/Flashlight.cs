using System.Collections;
using UnityEngine;

// Linterna. Jugo: al prenderse parpadea un instante (como una bombita que agarra), con poca pila falla de vez en
// cuando, y al quedarse sin pila se apaga a los tirones en vez de cortarse en seco.
public class Flashlight : MonoBehaviour
{
    public GameObject lightObject;
    public AudioClip lightSound;
    private AudioSource source;

    public float battery = 100f;
    public float maxBattery = 100f;
    // Energia que gasta por segundo encendida (0.67 = 150 s de luz con la pila llena; antes 2 = 50 s)
    public float drainPerSecond = 0.6666667f;

    [Header("Parpadeos")]
    [Tooltip("Por debajo de esta carga (0..1) la luz falla de vez en cuando")]
    [Range(0f, 1f)] public float cargaBaja = 0.15f;

    public float BatteryPercent => maxBattery > 0f ? Mathf.Clamp01(battery / maxBattery) : 0f;

    private Grabbable grab;
    private PlayerInteractor interactor;
    private Light[] luces;
    private float[] intensidades;
    private Coroutine parpadeo;
    private bool apagandose;
    private float proximaFalla;

    void Start()
    {
        source = GetComponent<AudioSource>();
        grab = GetComponentInParent<Grabbable>();
        interactor = FindFirstObjectByType<PlayerInteractor>();
        if (lightObject != null)
        {
            luces = lightObject.GetComponentsInChildren<Light>(true);
            intensidades = new float[luces.Length];
            for (int i = 0; i < luces.Length; i++) intensidades[i] = luces[i].intensity;
        }
    }

    void Update()
    {
        // Solo se puede prender/apagar si el jugador la tiene en la mano (y no en medio de una cinematica)
        if (GameInput.Flashlight && IsHeld() && !apagandose && !HUD.EnCinematica)
        {
            LightManager();
        }

        RecargarEnEstacion(Time.deltaTime);

        // Descargar bateria
        if (lightObject.activeSelf && !apagandose && !Lectura.Abierta)   // leyendo una nota o el diario no se gasta
        {
            battery -= drainPerSecond * Time.deltaTime;

            if (battery <= 0)
            {
                battery = 0;
                Parpadear(Apagarse());
            }
            else if (BatteryPercent < cargaBaja && Time.time > proximaFalla && parpadeo == null)
            {
                // Con poca pila: un fallo corto cada tanto (mas seguido cuanto menos carga queda)
                proximaFalla = Time.time + Random.Range(1.5f, 5f) * Mathf.Lerp(0.4f, 1f, BatteryPercent / Mathf.Max(0.01f, cargaBaja));
                Parpadear(Falla());
            }
        }
    }

    // La llaman las pilas (Bateria.Recoger)
    public void Recharge(float amount)
    {
        battery = Mathf.Min(maxBattery, battery + amount);
        proximaFalla = Time.time + 2f;
    }

    [Header("Recarga en la estacion")]
    [Tooltip("Carga por segundo mientras el jugador esta en la Zona Principal (la estacion). 0 = no se recarga sola")]
    public float recargaEstacion = 25f;
    [Tooltip("Metros por encima/debajo del piso de la estacion que siguen contando como estacion")]
    public float alturaEstacion = 3.5f;
    static Bounds estacion;
    static int estacionBuscada;   // 0 = sin buscar, 1 = hay, 2 = no hay
    static float reintentarEstacion;
    static UnityEngine.SceneManagement.Scene escenaEstacion;
    Transform jugadorEstacion;

    // En la Zona Principal (la estacion) se recarga sola. La llama su Update y, mientras esta guardada en el
    // inventario (objeto inactivo, sin Update), el Inventario
    public void RecargarEnEstacion(float dt)
    {
        if (battery >= maxBattery || !EnEstacion()) return;
        battery = Mathf.MoveTowards(battery, maxBattery, recargaEstacion * dt);
        proximaFalla = Time.time + 2f;
    }

    // El jugador esta en la estacion: dentro del piso de la Zona Principal y fuera de cualquier zona (igual que
    // MusicaZonaPrincipal). La tienda, el sotano y los pasillos no cuentan
    bool EnEstacion()
    {
        if (recargaEstacion <= 0f || MenuPrincipal.Abierto) return false;
        // (si no aparece, p. ej. porque la estacion esta apagada, se vuelve a buscar cada tanto)
        if (estacionBuscada == 0 || escenaEstacion != gameObject.scene || (estacionBuscada == 2 && Time.unscaledTime >= reintentarEstacion))
        {
            escenaEstacion = gameObject.scene;
            estacionBuscada = 2;
            reintentarEstacion = Time.unscaledTime + 3f;
            GameObject zp = GameObject.Find("Zona_Principal");
            Transform piso = zp != null ? zp.transform.Find("piso") : null;
            if (piso != null)
                foreach (Collider c in piso.GetComponentsInChildren<Collider>())
                {
                    if (c.isTrigger) continue;
                    if (estacionBuscada == 2) { estacion = c.bounds; estacionBuscada = 1; } else estacion.Encapsulate(c.bounds);
                }
        }
        if (estacionBuscada != 1) return false;
        if (volumenZonas == null) volumenZonas = FindFirstObjectByType<VolumenZonas>();
        if (volumenZonas != null && volumenZonas.ZonaActual != null) return false;
        if (jugadorEstacion == null) { jugadorEstacion = HUD.Jugador(); if (jugadorEstacion == null) return false; }
        Vector3 p = jugadorEstacion.position;
        return p.x >= estacion.min.x && p.x <= estacion.max.x && p.z >= estacion.min.z && p.z <= estacion.max.z &&
               Mathf.Abs(p.y - estacion.max.y) <= alturaEstacion;
    }

    [Header("Por zona")]
    [Tooltip("Fuerza de la linterna en los Backrooms (ya estan iluminados: a toda potencia se ve todo quemado)")]
    [Range(0.1f, 1f)] public float potenciaBackrooms = 0.4f;
    float factorZona = 1f;
    VolumenZonas volumenZonas;

    float FactorDeseado()
    {
        if (volumenZonas == null) volumenZonas = FindFirstObjectByType<VolumenZonas>();
        ZonaVolumen z = volumenZonas != null ? volumenZonas.ZonaActual : null;
        return z != null && z.name == "Trigger Backrooms" ? potenciaBackrooms : 1f;
    }

    // La linterna que el jugador tiene en la mano (o null) y si esta alumbrando (para MunecaLuz)
    public static Flashlight EnMano { get; private set; }
    public bool Encendida => lightObject != null && lightObject.activeInHierarchy && !apagandose && IsHeld();
    // El haz (la primera luz spot)
    public Light Haz
    {
        get
        {
            if (luces != null) foreach (Light l in luces) if (l != null && l.type == LightType.Spot) return l;
            return lightObject != null ? lightObject.GetComponentInChildren<Light>() : null;
        }
    }

    void LateUpdate()
    {
        if (IsHeld()) EnMano = this;
        else if (EnMano == this) EnMano = null;
        // Menos fuerte en las zonas ya iluminadas (los Backrooms)
        factorZona = Mathf.MoveTowards(factorZona, FactorDeseado(), Time.deltaTime * 1.5f);
        // Menos fuerte cuando alumbra algo de cerca
        float objetivo = FactorCerca();
        factorCerca = Mathf.Lerp(factorCerca, objetivo, 1f - Mathf.Exp(-(objetivo < factorCerca ? 14f : 6f) * Time.deltaTime));
        if (parpadeo == null && !apagandose) Nivel(1f);
    }

    [Header("De cerca")]
    [Tooltip("A menos de esta distancia (m) de lo que alumbra, el haz baja su fuerza: la luz crece con 1/distancia^2 y " +
             "de cerca las paredes y los objetos (palancas, pilas, el tablero...) se veian quemados, blancos")]
    public float distanciaCerca = 4f;
    [Tooltip("Fuerza minima del haz pegado a algo (0..1)")]
    [Range(0.01f, 1f)] public float minimoCerca = 0.05f;
    [Tooltip("Que tan rapido baja al acercarse (2 = el brillo de cerca queda igual que a 'distanciaCerca'; menos = algo mas brillante de cerca)")]
    [Range(1f, 2f)] public float curvaCerca = 1.6f;
    float factorCerca = 1f;
    readonly RaycastHit[] golpes = new RaycastHit[8];

    // Distancia a lo que tiene delante el haz (el centro y un poco a los lados) -> cuanto bajar la fuerza
    float FactorCerca()
    {
        Light haz = Haz;
        if (haz == null || distanciaCerca <= 0.01f || !haz.isActiveAndEnabled) return 1f;
        Transform t = haz.transform;
        float d = Distancia(t.position, t.forward);
        // A los lados cuentan menos (asi una pared al borde del haz no lo apaga todo)
        for (int k = 0; k < 4; k++)
        {
            Vector3 eje = k == 0 ? t.up : k == 1 ? -t.up : k == 2 ? t.right : -t.right;
            d = Mathf.Min(d, Distancia(t.position, Quaternion.AngleAxis(14f, eje) * t.forward) * 1.5f);
        }
        if (d >= distanciaCerca) return 1f;
        return Mathf.Max(minimoCerca, Mathf.Pow(d / distanciaCerca, curvaCerca));
    }

    float Distancia(Vector3 desde, Vector3 dir)
    {
        int n = Physics.RaycastNonAlloc(desde, dir, golpes, distanciaCerca, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float d = distanciaCerca;
        Transform propio = transform.root;
        Transform jugador = interactor != null ? interactor.transform.root : null;
        for (int i = 0; i < n; i++)
        {
            Transform c = golpes[i].collider.transform;
            if (c.IsChildOf(propio) || (jugador != null && c.IsChildOf(jugador))) continue;   // la propia linterna y el jugador
            d = Mathf.Min(d, golpes[i].distance);
        }
        return d;
    }


    bool IsHeld()
    {
        // Si la linterna no se puede agarrar, se asume que siempre esta en la mano
        if (grab == null) return true;
        return interactor != null && interactor.Held == grab;
    }

    void LightManager()
    {
        if (lightObject.activeSelf)
        {
            Detener();
            lightObject.SetActive(false);
        }
        else
        {
            if (battery > 0)
            {
                lightObject.SetActive(true);
                Parpadear(Encenderse());
            }
        }

        if (source != null)
        {
            if (lightSound != null)
            {
                source.PlayOneShot(lightSound);
            }
        }
    }

    // ------------------------------------------------------------------ Parpadeos

    void Parpadear(IEnumerator rutina)
    {
        if (luces == null || luces.Length == 0 || !isActiveAndEnabled) { if (rutina != null && battery <= 0f) lightObject.SetActive(false); return; }
        Detener();
        parpadeo = StartCoroutine(rutina);
    }

    void Detener()
    {
        if (parpadeo != null) StopCoroutine(parpadeo);
        parpadeo = null;
        apagandose = false;
        Nivel(1f);
    }

    void Nivel(float k)
    {
        if (luces == null) return;
        // La de cerca solo baja el haz (la luz chica de las manos no)
        for (int i = 0; i < luces.Length; i++)
            if (luces[i] != null) luces[i].intensity = intensidades[i] * k * factorZona * (luces[i].type == LightType.Spot ? factorCerca : 1f);
    }

    // Prende con un par de titileos rapidos
    IEnumerator Encenderse()
    {
        float[] niveles = { 0.15f, 1f, 0.35f, 1.1f, 1f };
        float[] tiempos = { 0.03f, 0.04f, 0.035f, 0.05f, 0f };
        for (int i = 0; i < niveles.Length; i++)
        {
            Nivel(niveles[i]);
            if (tiempos[i] > 0f) yield return new WaitForSeconds(tiempos[i]);
        }
        Nivel(1f);
        parpadeo = null;
    }

    // Fallo corto con poca pila
    IEnumerator Falla()
    {
        int veces = Random.Range(1, 4);
        for (int i = 0; i < veces; i++)
        {
            Nivel(Random.Range(0f, 0.25f));
            yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
            Nivel(Random.Range(0.6f, 1f));
            yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
        }
        Nivel(1f);
        parpadeo = null;
    }

    // Sin pila: unos tirones cada vez mas debiles y se apaga
    IEnumerator Apagarse()
    {
        apagandose = true;
        float[] niveles = { 0.3f, 0.8f, 0.1f, 0.5f, 0.05f, 0.25f, 0f };
        foreach (float n in niveles)
        {
            Nivel(n);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.14f));
        }
        lightObject.SetActive(false);
        apagandose = false;
        Nivel(1f);
        parpadeo = null;
    }

    // Al guardarla en el inventario (se desactiva) se cortan los parpadeos y vuelve a su brillo normal
    void OnDisable()
    {
        if (EnMano == this) EnMano = null;
        bool sinPila = apagandose;
        parpadeo = null;
        apagandose = false;
        Nivel(1f);
        if (sinPila && lightObject != null) lightObject.SetActive(false);
    }
}
