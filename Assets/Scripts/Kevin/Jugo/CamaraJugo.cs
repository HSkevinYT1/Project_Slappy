using UnityEngine;

// Mueve la camara del jugador para el "jugo" (ver Jugo): temblor por trauma, golpes de campo de vision,
// cabeceos, un poco mas de campo de vision al correr y una hundida al aterrizar de una caida.
// Solo toca la camara (nunca el cuerpo) y lo hace SUMANDO un desplazamiento en LateUpdate que se quita al
// principio del frame siguiente, asi no pelea con MouseLook, HeadBob, el reloj ni las cinematicas.
// HUD lo agrega solo a la camara del jugador (la del MouseLook).
[DefaultExecutionOrder(-500)]
[RequireComponent(typeof(Camera))]
public class CamaraJugo : MonoBehaviour
{
    public static CamaraJugo Instance { get; private set; }

    [Header("Temblor (trauma)")]
    [Tooltip("Cuanto trauma se pierde por segundo (1.5 = un trauma de 1 dura unos 0.7 s)")]
    public float decaimiento = 1.5f;
    [Tooltip("Giro maximo con trauma 1 (grados: arriba/abajo, izquierda/derecha, inclinacion)")]
    public Vector3 giroMaximo = new Vector3(2.4f, 2.4f, 3.2f);
    [Tooltip("Desplazamiento maximo con trauma 1 (metros)")]
    public float desplazamientoMaximo = 0.025f;
    public float frecuencia = 22f;

    [Header("Correr")]
    [Tooltip("Grados de campo de vision que se suman corriendo")]
    public float fovCorrer = 6f;
    public float suavidadFov = 5f;

    [Header("Caidas")]
    [Tooltip("Velocidad de caida (m/s) a partir de la cual se nota el aterrizaje")]
    public float caidaMinima = 3.5f;
    [Tooltip("Cuanto baja la camara (m) en el aterrizaje mas fuerte")]
    public float hundidaMaxima = 0.16f;

    [Header("Suavizado")]
    [Tooltip("El cuerpo del jugador se mueve y gira con la fisica (50 veces por segundo) y sin interpolacion: la camara " +
             "avanzaba a saltitos. Con esto la camara (solo lo visual) se interpola entre los dos ultimos pasos de fisica.")]
    public bool suavizarMovimiento = true;

    float trauma, tiempo;
    float fovRun, fovGolpe, fovGolpeVel, cabeceo, cabeceoVel;
    Vector3 posAplicada;
    Quaternion rotAplicada = Quaternion.identity;
    // Giro del suavizado: se aplica por delante (en el espacio del cuerpo)
    Quaternion rotPrevia = Quaternion.identity;
    float fovAplicado;

    // Pose del cuerpo en el paso de fisica anterior
    Vector3 posFisicaAnterior;
    Quaternion rotFisicaAnterior = Quaternion.identity;
    bool hayPoseAnterior;

    Camera cam;
    Rigidbody rb;
    PlayerMovement movimiento;
    Collider cuerpo;
    float tiempoEnAire, caidaMaxima;
    bool agotadoAntes;
    static readonly RaycastHit[] hits = new RaycastHit[8];

    public float Trauma => trauma;

    void Awake()
    {
        cam = GetComponent<Camera>();
        rb = GetComponentInParent<Rigidbody>();
        movimiento = GetComponentInParent<PlayerMovement>();
        if (movimiento != null)
            foreach (Collider c in movimiento.GetComponents<Collider>()) if (!c.isTrigger) { cuerpo = c; break; }
    }

    void OnEnable() => Instance = this;

    void OnDisable()
    {
        Deshacer();
        if (Instance == this) Instance = null;
    }

    public void AgregarTrauma(float t) => trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, t));
    // Salto instantaneo que vuelve con resorte (se pasa un poco y se asienta)
    public void AgregarPatadaFOV(float grados) => fovGolpe = Mathf.Clamp(fovGolpe + grados, -15f, 15f);
    // Empujon hacia abajo con velocidad: la camara baja suave y vuelve
    public void AgregarCabeceo(float metros) => cabeceoVel += Mathf.Max(0f, metros) * 14f;

    // Antes que los demas scripts de la camara: se quita lo que se sumo el frame anterior
    void Update() => Deshacer();

    void Deshacer()
    {
        transform.localPosition -= posAplicada;
        transform.localRotation = Quaternion.Inverse(rotPrevia) * transform.localRotation * Quaternion.Inverse(rotAplicada);
        if (cam != null) cam.fieldOfView -= fovAplicado;
        posAplicada = Vector3.zero;
        rotAplicada = Quaternion.identity;
        rotPrevia = Quaternion.identity;
        fovAplicado = 0f;
    }

    // Antes que los demas FixedUpdate: guarda donde quedo el cuerpo en el paso de fisica anterior
    void FixedUpdate()
    {
        if (rb == null) return;
        posFisicaAnterior = rb.position;
        rotFisicaAnterior = rb.rotation;
        hayPoseAnterior = true;
    }

    // Desplazamiento y giro (en el espacio del cuerpo) que llevan la camara a donde estaria el cuerpo interpolado
    bool Suavizado(out Vector3 pos, out Quaternion giro)
    {
        pos = Vector3.zero;
        giro = Quaternion.identity;
        if (!suavizarMovimiento || !hayPoseAnterior || rb == null || rb.isKinematic || rb.interpolation != RigidbodyInterpolation.None) return false;
        Transform cuerpoT = transform.parent;
        if (cuerpoT != rb.transform) return false;
        Vector3 posActual = cuerpoT.position;
        Quaternion rotActual = cuerpoT.rotation;
        // Teletransporte (o algo que movio el cuerpo a mano): no se interpola ese salto
        if ((posActual - posFisicaAnterior).sqrMagnitude > 1f || Quaternion.Angle(rotActual, rotFisicaAnterior) > 45f) return false;

        float alfa = Mathf.Clamp01((Time.time - Time.fixedTime) / Mathf.Max(0.0001f, Time.fixedDeltaTime));
        Vector3 posVisual = Vector3.Lerp(posFisicaAnterior, posActual, alfa);
        Quaternion rotVisual = Quaternion.Slerp(rotFisicaAnterior, rotActual, alfa);

        // Donde quedaria la camara con el cuerpo en la pose interpolada
        Vector3 local = transform.localPosition;
        Vector3 mundo = posVisual + rotVisual * Vector3.Scale(cuerpoT.lossyScale, local);
        pos = cuerpoT.InverseTransformPoint(mundo) - local;
        giro = Quaternion.Inverse(rotActual) * rotVisual;
        return true;
    }

    void LateUpdate()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        bool activo = cam != null && cam.enabled && !PauseMenu.IsPaused && !PantallaMuerte.Muerto && Time.timeScale > 0f;

        if (activo) { DetectarAterrizaje(); DetectarAgotado(); }

        // En tiempo real: sigue decayendo aunque el tiempo del juego vaya lento
        trauma = Mathf.Max(0f, trauma - decaimiento * dt);
        bool corriendo = movimiento != null && movimiento.Corriendo && rb != null && new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).sqrMagnitude > 1f;
        fovRun = Mathf.Lerp(fovRun, corriendo ? fovCorrer : 0f, 1f - Mathf.Exp(-suavidadFov * dt));
        Jugo.Resorte(ref fovGolpe, ref fovGolpeVel, 160f, 15f, dt);
        Jugo.Resorte(ref cabeceo, ref cabeceoVel, 140f, 16f, dt);
        if (!activo) return;

        float m = Jugo.Movimiento;
        tiempo += dt * frecuencia;
        float s = trauma * trauma * m;
        rotAplicada = Quaternion.Euler(giroMaximo.x * s * Ruido(0f), giroMaximo.y * s * Ruido(1f), giroMaximo.z * s * Ruido(2f));
        posAplicada = new Vector3(Ruido(3f), Ruido(4f), 0f) * (desplazamientoMaximo * s) + Vector3.down * (cabeceo * m);
        fovAplicado = (fovRun + fovGolpe) * m;
        if (Suavizado(out Vector3 posSuave, out Quaternion giroSuave))
        {
            posAplicada += posSuave;
            rotPrevia = giroSuave;
        }

        transform.localPosition += posAplicada;
        transform.localRotation = rotPrevia * transform.localRotation * rotAplicada;
        cam.fieldOfView += fovAplicado;
    }

    // Ruido suave (no al azar cada frame, que "zumba"): -1..1
    float Ruido(float canal) => (Mathf.PerlinNoise(tiempo, canal * 7.31f + 0.5f) - 0.5f) * 2f;

    void DetectarAterrizaje()
    {
        if (rb == null) return;
        if (!EnSuelo())
        {
            tiempoEnAire += Time.deltaTime;
            caidaMaxima = Mathf.Max(caidaMaxima, -rb.linearVelocity.y);
            return;
        }
        if (tiempoEnAire > 0.18f && caidaMaxima > caidaMinima)
        {
            float k = Mathf.Clamp01((caidaMaxima - caidaMinima) / 8f);
            AgregarCabeceo(hundidaMaxima * Mathf.Lerp(0.3f, 1f, k));
            AgregarTrauma(Mathf.Lerp(0.12f, 0.5f, k));
        }
        tiempoEnAire = 0f;
        caidaMaxima = 0f;
    }

    // Al quedarse sin aire corriendo: un tironcito de cabeza
    void DetectarAgotado()
    {
        bool agotado = movimiento != null && movimiento.Agotado;
        if (agotado && !agotadoAntes)
        {
            AgregarTrauma(0.12f);
            AgregarPatadaFOV(-1.5f);
            AgregarCabeceo(0.04f);
        }
        agotadoAntes = agotado;
    }

    bool EnSuelo()
    {
        if (cuerpo == null) return Mathf.Abs(rb.linearVelocity.y) < 1f;
        Bounds b = cuerpo.bounds;
        int n = Physics.RaycastNonAlloc(b.center, Vector3.down, hits, b.extents.y + 0.3f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (!hits[i].collider.transform.IsChildOf(cuerpo.transform)) return true;
        return false;
    }
}
