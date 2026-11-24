using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Los Backrooms segun el guion:
// 1. Al entrar (unos metros adentro) el jugador se gira y ve como la puerta por la que vino desaparece
//    (aparece pared en su lugar): "Es en serio... otra vez."
// 2. En la sala de los focos (cerca del pasillo de la entrada) hay un cadaver y, sentado al lado, el gato de sombra
//    lamiendose la pata (su animacion Idle). En cuanto el jugador lo tiene a la vista empieza la cinematica: la camara
//    se gira hacia el gato, "¡¿QUE MIERDA ES ESO?!", el gato se levanta y se va caminando por la otra puerta. Cuando
//    sale de la vista se lo teletransporta a una habitacion lejana y desde ahi sale a buscar al jugador (su IA sigue sola).
// 3. Al entrar a la habitacion nota la palanca y la piedra sobre el cadaver: "¿Y esto?"
// Las palancas, el boton y el portal los lleva PuzzlePalancas. La muerte especial del gato: MuerteGato.
// Lo arma Tools > Backrooms > Rehacer pasillos (BackroomsPasillosSetup + BackroomsPalancasSetup).
public class BackroomsGuion : MonoBehaviour
{
    public static BackroomsGuion Instance { get; private set; }

    [Header("Entrada")]
    [Tooltip("La entrada de los Backrooms (su eje azul apunta hacia afuera)")]
    public Transform entrada;
    [Tooltip("Pared que aparece donde estaba la puerta (empieza escondida)")]
    public GameObject muroEntrada;
    [Tooltip("Metros hacia adentro que tiene que avanzar el jugador para que pase")]
    public float metrosEntrada = 4f;
    [TextArea] public string alEntrar = "Es en serio... otra vez.";
    [Tooltip("Suena mientras la puerta se desvanece (el muro parpadea)")]
    public AudioClip sonidoGlitch;
    [Range(0f, 1f)] public float volumenGlitch = 0.8f;

    [Header("La habitacion del cadaver")]
    public BoxCollider habitacion;
    public JefeIndigoAI gato;
    [Tooltip("Donde esta el gato al principio: sentado al lado del cadaver, lamiendose la pata (su Idle)")]
    public Transform puntoGato;
    [Tooltip("Por donde se va de la habitacion (fuera de la vista desde la entrada de la sala)")]
    public Transform huidaGato;
    [Tooltip("Por donde se va si el jugador llega por el otro lado (opcional): se usa la salida mas lejos del jugador")]
    public Transform huidaGatoAlterna;
    [Tooltip("Habitacion lejana donde se lo teletransporta para que salga a buscar al jugador")]
    public Transform reaparicionGato;
    [Tooltip("Grito de otra persona (opcional) y desde donde suena (al reaparecer el gato)")]
    public AudioClip grito;
    public Transform origenGrito;
    [Range(0f, 1f)] public float volumenGrito = 1f;
    [Tooltip("A esta distancia (m) o menos, con el gato a la vista (sin nada en medio), empieza la cinematica")]
    public float distanciaVerGato = 11f;
    [TextArea] public string alVerGato = "¡¿QUÉ MIERDA ES ESO?!";
    [TextArea] public string alEntrarHabitacion = "¿Y esto?";

    [Header("Cinematica del gato")]
    [Tooltip("Segundos mirandolo lamerse la pata antes de que se vaya")]
    public float segundosMirando = 2.4f;
    [Tooltip("Velocidad (m/s) a la que se va caminando de la habitacion")]
    public float velocidadIrse = 2.1f;
    [Tooltip("Maximo de segundos siguiendolo con la mirada mientras se va")]
    public float maxSegundosIrse = 6f;
    [Tooltip("Metros de error al ir a buscar al jugador cuando reaparece (no va directo)")]
    public float errorPersecucion = 3f;
    [Tooltip("Puerta de la sala por la que llega el jugador (centro, a ras del piso; su eje azul apunta hacia dentro). " +
             "Con ella el jugador se asoma por el marco: mitad pared y mitad gato, y cuando el gato se va, 3/4 pared")]
    public Transform puertaSala;
    public float anchoPuertaSala = 1.3f;
    [Tooltip("A que distancia (m) de la pared se pone la camara al asomarse")]
    public float distanciaAsomarse = 0.55f;
    [Tooltip("Cuanto se inclina la cabeza al asomarse (grados)")]
    public float inclinacionAsomarse = 7f;
    [Tooltip("Suspiro rapido cuando el gato se empieza a mover (y el jugador se esconde mas)")]
    public AudioClip suspiro;
    [Range(0f, 1f)] public float volumenSuspiro = 0.85f;

    [Header("Muerte del gato")]
    [Tooltip("Sonido cuando el gato salta sobre el jugador (MuerteGato): suena solo lo que dura el salto")]
    public AudioClip sonidoKill;
    [Tooltip("Segundo del audio desde el que empieza (se salta el silencio del principio)")]
    public float inicioKill;

    public bool Entro { get; private set; }
    public bool GatoSuelto { get; private set; }
    public bool EnCinematica { get; private set; }

    ZonaVolumen zona;
    Transform jugador;
    Camera cam;
    bool gatoVisto, habitacionDicha;
    float proxima;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        foreach (ZonaVolumen z in FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (z.name == "Trigger Backrooms") zona = z;
        if (muroEntrada != null) muroEntrada.SetActive(false);
        if (gato != null)
        {
            MuerteGato muerte = gato.GetComponent<MuerteGato>();
            if (!muerte) muerte = gato.gameObject.AddComponent<MuerteGato>();
            if (muerte.sonidoKill == null) { muerte.sonidoKill = sonidoKill; muerte.inicioKill = inicioKill; }
            StartCoroutine(PonerGato());
        }
    }

    // El gato espera sentado al lado del cadaver (sin IA, quieto: se ve su Idle, lamiendose la pata) hasta la cinematica
    IEnumerator PonerGato()
    {
        yield return null;   // despues del Start de su IA
        gato.enabled = false;
        NavMeshAgent a = gato.GetComponent<NavMeshAgent>();
        if (a != null && puntoGato != null && NavMesh.SamplePosition(puntoGato.position, out NavMeshHit h, 3f, NavMesh.AllAreas))
        {
            a.updatePosition = true;
            a.Warp(h.position);
            a.isStopped = true;
            a.ResetPath();
            a.updateRotation = false;   // que no se gire solo: mira hacia donde dice puntoGato
        }
        if (puntoGato != null) gato.transform.rotation = Quaternion.Euler(0f, puntoGato.eulerAngles.y, 0f);
    }

    void Update()
    {
        if (Time.time < proxima) return;
        proxima = Time.time + 0.1f;
        // Sin la cinematica el gato NO persigue: si algo le prende la IA antes de tiempo, se le vuelve a apagar
        if (gato != null && !GatoSuelto && !EnCinematica && gato.enabled)
        {
            gato.enabled = false;
            if (gato.TryGetComponent(out NavMeshAgent ag) && ag.isOnNavMesh) { ag.isStopped = true; ag.ResetPath(); }
        }
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }
        if (cam == null) cam = Camera.main;
        if (PantallaMuerte.Muerto) return;

        // 1. Entrada
        if (!Entro && entrada != null && zona != null && zona.JugadorDentro)
        {
            float adentro = -Vector3.Dot(jugador.position - entrada.position, entrada.forward);
            if (adentro >= metrosEntrada) { Entro = true; StartCoroutine(CinematicaEntrada()); }
        }

        // 2. El gato: en cuanto lo tiene a la vista (o entra a la sala), la cinematica
        bool enHabitacion = habitacion != null && habitacion.bounds.Contains(jugador.position + Vector3.up * 0.5f);
        if (!gatoVisto && !EnCinematica && gato != null && (enHabitacion || GatoALaVista()))
        {
            gatoVisto = true;
            StartCoroutine(CinematicaGato());
        }

        // (Sin la cinematica el gato NO persigue: se queda lamiendose la pata al lado del cadaver, con la IA apagada.
        //  La sala del cadaver es el unico paso del sector de la entrada al resto, asi que no se la puede saltar)

        // 2b. El gato acecha (solo despues de la cinematica)
        Acechar();

        // 3. La palanca y la piedra
        if (GatoSuelto && !habitacionDicha && enHabitacion)
        {
            habitacionDicha = true;
            Pensamiento.Decir("backrooms.habitacion", 0.4f, alEntrarHabitacion);
        }
    }

    // El centro del cuerpo del gato (es chico: esta cerca del piso, no a la altura del agente)
    Vector3 CuerpoGato()
    {
        NavMeshAgent ag = gato.GetComponent<NavMeshAgent>();
        return gato.transform.position + Vector3.up * (0.3f - (ag != null ? ag.baseOffset : 0f));
    }

    // Nada en medio entre la camara y el punto (ni paredes ni puertas; el gato y el jugador no cuentan)
    bool SinNadaEnMedio(Vector3 p)
    {
        if (cam == null) return false;
        Vector3 o = cam.transform.position;
        foreach (RaycastHit h in Physics.RaycastAll(o, (p - o).normalized, Vector3.Distance(o, p), ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(gato.transform) || h.collider.transform.IsChildOf(jugador)) continue;
            return false;
        }
        return true;
    }

    bool LoVe(Vector3 p) => SinNadaEnMedio(p) || SinNadaEnMedio(p + Vector3.up * 0.25f);

    // El gato esta a la vista: cerca y sin nada en medio (no hace falta que lo este mirando: la cinematica gira la camara)
    bool GatoALaVista()
    {
        if (cam == null) return false;
        Vector3 p = CuerpoGato();
        if ((p - cam.transform.position).sqrMagnitude > distanciaVerGato * distanciaVerGato) return false;
        return LoVe(p);
    }

    // Gira al jugador hacia el punto: el cuerpo en horizontal y la camara arriba/abajo (k = cuanto se acerca este frame)
    void MirarHacia(Vector3 punto, float k, Rigidbody rb, Transform ct)
    {
        Vector3 dir = punto - jugador.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion actual = rb != null ? rb.rotation : jugador.rotation;
            Quaternion r = Quaternion.Slerp(actual, Quaternion.LookRotation(dir.normalized), k);
            if (rb != null) rb.MoveRotation(r); else jugador.rotation = r;
        }
        if (ct != null)
        {
            Vector3 d = punto - ct.position;
            float pitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -60f, 60f);
            ct.localRotation = Quaternion.Slerp(ct.localRotation, Quaternion.Euler(pitch, 0f, 0f), k);
        }
    }

    static float Suave(float rapidez) => 1f - Mathf.Exp(-rapidez * Time.deltaTime);

    // El jugador ve al gato lamiendose la pata al lado del cadaver. Si llega por la puerta de la sala, se ASOMA por el
    // marco: la mitad de la imagen es la pared y la otra mitad el gato; cuando el gato se empieza a mover se esconde mas
    // (3/4 pared, 1/4 viendo como se va) con un suspiro. El gato se va por la otra puerta y, ya fuera de la vista, se lo
    // teletransporta a la habitacion lejana para que salga a buscarlo
    IEnumerator CinematicaGato()
    {
        EnCinematica = true;
        NavMeshAgent agent = gato.GetComponent<NavMeshAgent>();

        var apagados = new List<Behaviour>();
        void Apagar(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; apagados.Add(b); } }
        Apagar(jugador.GetComponent<PlayerMovement>());
        foreach (MouseLook m in jugador.GetComponentsInChildren<MouseLook>()) Apagar(m);
        foreach (HeadBob h in jugador.GetComponentsInChildren<HeadBob>()) Apagar(h);
        foreach (PlayerInteractor p in jugador.GetComponentsInChildren<PlayerInteractor>()) Apagar(p);
        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        bool cinematico = rb != null && rb.isKinematic;
        if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }   // se lo mueve a mano
        Transform ct = cam != null ? cam.transform : null;
        Quaternion camIni = ct != null ? ct.localRotation : Quaternion.identity;

        // Por donde se va el gato: la salida mas lejos del jugador
        Transform salida = huidaGato;
        if (huidaGatoAlterna != null && (salida == null ||
            (huidaGatoAlterna.position - jugador.position).sqrMagnitude > (salida.position - jugador.position).sqrMagnitude))
            salida = huidaGatoAlterna;

        Marco marco = default;
        bool asomarse = ct != null && cam != null && PrepararMarco(salida, ref marco);

        // 1. Se gira hacia el gato (asomandose por el marco si llego por la puerta)
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            if (asomarse) Asomarse(marco, marco.mitad, false, Suave(4.5f), rb, ct);
            else MirarHacia(CuerpoGato(), Suave(5f), rb, ct);
            yield return null;
        }
        Jugo.Sacudir(0.12f);

        // 2. Lo mira lamerse la pata (su Idle)
        bool dicho = false;
        for (float t = 0f; t < segundosMirando; t += Time.deltaTime)
        {
            if (!dicho && t > 0.35f) { dicho = true; Pensamiento.Decir("backrooms.gato", 0f, alVerGato); }
            if (asomarse) Asomarse(marco, marco.mitad, false, Suave(5f), rb, ct);
            else MirarHacia(CuerpoGato(), Suave(6f), rb, ct);
            yield return null;
        }
        if (!dicho) Pensamiento.Decir("backrooms.gato", 0f, alVerGato);

        // 3. Se levanta y se va caminando; el jugador se esconde mas (suspiro) y lo sigue con la mirada hasta que sale
        if (agent != null && agent.isOnNavMesh && salida != null)
        {
            Sonar(suspiro, volumenSuspiro);
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.speed = velocidadIrse;
            agent.SetDestination(salida.position);
            float sinVerlo = 0f;
            for (float t = 0f; t < maxSegundosIrse; t += Time.deltaTime)
            {
                Vector3 p = CuerpoGato();
                bool ve = LoVe(p);
                if (asomarse) Asomarse(marco, marco.cuarto, true, Suave(3.5f), rb, ct);
                else if (ve) MirarHacia(p, Suave(4f), rb, ct);
                sinVerlo = ve ? 0f : sinVerlo + Time.deltaTime;
                if (sinVerlo > 0.35f) break;   // ya salio de la vista
                if (t > 0.5f && !agent.pathPending && agent.remainingDistance < 0.4f) break;
                yield return null;
            }
        }
        yield return new WaitForSeconds(0.3f);

        // 4. Fuera de la vista: a la habitacion lejana, y a buscar al jugador
        yield return Reaparecer();

        // 5. La camara vuelve a su altura de antes (la de MouseLook), sin inclinarse, y se devuelven los controles
        if (ct != null)
        {
            Quaternion desde = ct.localRotation;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { ct.localRotation = Quaternion.Slerp(desde, camIni, Mathf.SmoothStep(0f, 1f, t / 0.4f)); yield return null; }
            ct.localRotation = camIni;
        }
        if (rb != null && !cinematico) rb.isKinematic = false;
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
        EnCinematica = false;
        if (PantallaMuerte.Instance != null && !PantallaMuerte.Muerto) PantallaMuerte.Instance.Guardar();
    }

    // ------------------------------------------------------------------ Asomarse por el marco de la puerta

    struct Marco
    {
        public Vector3 jamba;      // la esquina del marco que tapa (del lado de afuera), a ras del piso
        public Vector3 adentro;    // hacia dentro de la sala (perpendicular a la pared)
        public Vector3 hueco;      // desde la jamba hacia el centro de la puerta (el lado que se ve)
        public float lado;         // +1 si lo que se ve queda a la derecha de la imagen, -1 si a la izquierda
        public float mitad, cuarto;   // separacion (grados) gato-jamba para que el gato quede en el centro de lo que se ve
        public float giroCuarto;   // cuanto se gira la vista hacia la pared para que la jamba quede a 1/4 del borde
    }

    // Solo si el jugador esta afuera de la sala, cerca de la puerta. Se elige la jamba que deja a la vista el camino del
    // gato hacia su salida (asi lo puede ver irse)
    bool PrepararMarco(Transform salida, ref Marco m)
    {
        if (puertaSala == null) return false;
        Vector3 adentro = puertaSala.forward; adentro.y = 0f; adentro.Normalize();
        Vector3 rel = jugador.position - puertaSala.position; rel.y = 0f;
        if (Vector3.Dot(rel, adentro) > -0.05f || rel.magnitude > 6f) return false;   // ya esta adentro o lejos
        Vector3 der = Vector3.Cross(Vector3.up, adentro);
        Vector3 objetivo = salida != null ? salida.position : CuerpoGato();
        float mejor = float.MinValue;
        foreach (float s in new[] { 1f, -1f })
        {
            Vector3 j = puertaSala.position + der * (s * anchoPuertaSala * 0.5f) - adentro * 0.1f;
            Vector3 hueco = -der * s;
            float puntos = Vector3.Dot(objetivo - j, hueco);
            if (puntos > mejor) { mejor = puntos; m.jamba = new Vector3(j.x, 0f, j.z); m.hueco = hueco; }
        }
        m.adentro = adentro;
        m.lado = Mathf.Sign(Vector3.SignedAngle(adentro, m.hueco, Vector3.up));
        // Del campo de vision horizontal de la camara: donde cae cada fraccion de la imagen
        float tanH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
        float a50 = Mathf.Atan(0.5f * tanH) * Mathf.Rad2Deg, a75 = Mathf.Atan(0.75f * tanH) * Mathf.Rad2Deg;
        m.mitad = a50;           // jamba en el centro, gato en el centro de la mitad que se ve (a 1/4 del borde)
        m.cuarto = a75 - a50;    // jamba a 1/4 del borde, gato en el centro de ese cuarto (a 1/8 del borde)
        m.giroCuarto = a50;
        return true;
    }

    // Angulo horizontal (grados, + hacia la derecha) de un punto visto desde c, respecto a "adentro"
    static float Angulo(Vector3 c, Vector3 punto, Vector3 adentro)
    {
        Vector3 d = punto - c; d.y = 0f;
        return Vector3.SignedAngle(adentro, d, Vector3.up);
    }

    // Lleva la camara a donde, a "distanciaAsomarse" de la pared, el gato queda a "separacion" grados de la jamba
    // (del lado que se ve), con la vista apuntando a la jamba (o girada hacia la pared en el tramo de 3/4)
    void Asomarse(Marco m, float separacion, bool escondido, float k, Rigidbody rb, Transform ct)
    {
        Vector3 gatoP = CuerpoGato();
        Vector3 Posicion(float u) => m.jamba - m.adentro * distanciaAsomarse + m.hueco * u;
        float Sep(float u) { Vector3 c = Posicion(u); return m.lado * (Angulo(c, gatoP, m.adentro) - Angulo(c, m.jamba, m.adentro)); }
        // Sep crece al pararse mas frente al hueco: se busca el punto exacto (biseccion)
        float lo = -1.3f, hi = 0.9f;
        float u;
        if (Sep(lo) >= separacion) u = lo;
        else if (Sep(hi) <= separacion) u = hi;
        else
        {
            for (int i = 0; i < 24; i++) { float mid = (lo + hi) * 0.5f; if (Sep(mid) < separacion) lo = mid; else hi = mid; }
            u = (lo + hi) * 0.5f;
        }
        Vector3 objetivo = Posicion(u);

        // Mueve el cuerpo para que la camara quede ahi (en horizontal)
        Vector3 camPos = ct.position;
        Vector3 delta = new Vector3(objetivo.x - camPos.x, 0f, objetivo.z - camPos.z) * k;
        Vector3 cuerpo = (rb != null ? rb.position : jugador.position) + delta;
        if (rb != null) rb.MovePosition(cuerpo); else jugador.position = cuerpo;

        // Vista: hacia la jamba (mitad y mitad) o girada hacia la pared (3/4 pared)
        Vector3 haciaJamba = m.jamba - (camPos + delta); haciaJamba.y = 0f;
        if (haciaJamba.sqrMagnitude < 1e-4f) return;
        float giro = escondido ? -m.lado * m.giroCuarto : 0f;
        Quaternion yaw = Quaternion.AngleAxis(giro, Vector3.up) * Quaternion.LookRotation(haciaJamba.normalized);
        Quaternion actual = rb != null ? rb.rotation : jugador.rotation;
        Quaternion r = Quaternion.Slerp(actual, yaw, k);
        if (rb != null) rb.MoveRotation(r); else jugador.rotation = r;

        // Arriba/abajo hacia el gato, y la cabeza inclinada hacia el lado por el que se asoma
        Vector3 d = gatoP - ct.position;
        float pitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -40f, 40f);
        float roll = -m.lado * inclinacionAsomarse * (escondido ? 0.6f : 1f);
        ct.localRotation = Quaternion.Slerp(ct.localRotation, Quaternion.Euler(pitch, 0f, roll), k);
    }

    // Sonido 2D corto (si se da "duracion", se corta con un fundido al llegar)
    void Sonar(AudioClip clip, float volumen, float desde = 0f, float duracion = -1f)
    {
        if (clip == null) return;
        var go = new GameObject("Sonido " + clip.name);
        var a = go.AddComponent<AudioSource>();
        a.clip = clip; a.volume = volumen; a.spatialBlend = 0f; a.playOnAwake = false;
        a.time = Mathf.Clamp(desde, 0f, Mathf.Max(0f, clip.length - 0.05f));
        a.Play();
        float vida = duracion > 0f ? duracion : clip.length - a.time;
        if (duracion > 0f) StartCoroutine(Fundir(a, duracion));
        Destroy(go, vida + 0.3f);
    }

    static IEnumerator Fundir(AudioSource a, float duracion)
    {
        float fundido = Mathf.Min(0.25f, duracion * 0.4f);
        yield return new WaitForSeconds(duracion - fundido);
        float v0 = a != null ? a.volume : 0f;
        for (float t = 0f; t < fundido && a != null; t += Time.deltaTime) { a.volume = v0 * (1f - t / fundido); yield return null; }
        if (a != null) a.Stop();
    }

    // Lo teletransporta a la habitacion lejana (si el jugador anda justo por ahi, a la otra punta de los Backrooms),
    // le devuelve su IA y lo manda a buscar al jugador
    IEnumerator Reaparecer()
    {
        NavMeshAgent agent = gato.GetComponent<NavMeshAgent>();
        Vector3 destino = reaparicionGato != null ? reaparicionGato.position : (huidaGato != null ? huidaGato.position : gato.transform.position);
        if (agent != null)
        {
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (jugador != null && (destino - jugador.position).sqrMagnitude < 18f * 18f) gato.MandarLejos(jugador.position);
            else if (NavMesh.SamplePosition(destino, out NavMeshHit h, 3f, NavMesh.AllAreas)) agent.Warp(h.position);
            if (agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = false; }
        }
        GatoSuelto = true;
        gato.enabled = true;   // desde aqui patrulla, oye y persigue solo
        Grito(gato.transform.position);
        yield return null;
        // Va a la zona donde esta el jugador (con algo de error); si lo ve o lo oye, lo persigue
        Vector3 error = Random.insideUnitSphere * errorPersecucion; error.y = 0f;
        if (jugador != null) gato.Investigar(jugador.position + error);
        proximoAcecho = Time.time + Random.Range(acechoCada.x, acechoCada.y);
    }

    void Grito(Vector3 donde)
    {
        if (grito == null) return;
        Jugo.Sacudir(0.15f);
        var go = new GameObject("Grito");
        go.transform.position = donde;
        var a = go.AddComponent<AudioSource>();
        a.clip = grito; a.volume = volumenGrito; a.spatialBlend = 0.7f; a.minDistance = 8f; a.maxDistance = 80f;
        a.rolloffMode = AudioRolloffMode.Linear; a.Play();
        Destroy(go, grito.length + 0.2f);
    }

    [Header("Acecho del gato")]
    [Tooltip("Cada cuantos segundos (min, max) el gato, si esta patrullando, va a buscar al jugador cerca de donde esta")]
    public Vector2 acechoCada = new Vector2(18f, 32f);
    [Tooltip("Metros de error: no va directo a donde esta el jugador, sino a la zona")]
    public float errorAcecho = 7f;
    float proximoAcecho = float.MaxValue;

    // El gato no se queda dando vueltas lejos: cada tanto va a rondar donde anda el jugador
    void Acechar()
    {
        if (!GatoSuelto || gato == null || !gato.enabled || Time.time < proximoAcecho || PantallaMuerte.Muerto) return;
        proximoAcecho = Time.time + Random.Range(acechoCada.x, acechoCada.y);
        if (PuzzlePalancas.Instance != null && PuzzlePalancas.Instance.PortalAbierto) return;
        Vector3 error = Random.insideUnitSphere * errorAcecho; error.y = 0f;
        gato.Investigar(jugador.position + error);
    }

    // El jugador se gira hacia la entrada y ve como la puerta desaparece
    IEnumerator CinematicaEntrada()
    {
        EnCinematica = true;
        var apagados = new List<Behaviour>();
        void Apagar(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; apagados.Add(b); } }
        Apagar(jugador.GetComponent<PlayerMovement>());
        foreach (MouseLook m in jugador.GetComponentsInChildren<MouseLook>()) Apagar(m);
        foreach (HeadBob h in jugador.GetComponentsInChildren<HeadBob>()) Apagar(h);
        foreach (PlayerInteractor p in jugador.GetComponentsInChildren<PlayerInteractor>()) Apagar(p);
        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        // Se da vuelta hacia la puerta
        Vector3 dir = entrada.position - jugador.position; dir.y = 0f;
        Quaternion desde = jugador.rotation, hasta = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir.normalized) : desde;
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            Quaternion r = Quaternion.Slerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / 0.9f));
            if (rb != null) rb.MoveRotation(r); else jugador.rotation = r;
            yield return null;
        }
        if (rb != null) rb.rotation = hasta;
        jugador.rotation = hasta;
        yield return new WaitForSeconds(0.35f);

        // La puerta desaparece: la pared parpadea un par de veces y se queda
        if (muroEntrada != null)
        {
            Renderer[] rs = muroEntrada.GetComponentsInChildren<Renderer>(true);
            muroEntrada.SetActive(true);
            float[] tiempos = { 0.06f, 0.08f, 0.05f, 0.12f, 0.05f, 0.2f };
            Sonar(sonidoGlitch, volumenGlitch);   // estatica a tirones, al ritmo del parpadeo
            for (int i = 0; i < tiempos.Length; i++)
            {
                foreach (Renderer r in rs) r.enabled = i % 2 == 0;
                yield return new WaitForSeconds(tiempos[i]);
            }
            foreach (Renderer r in rs) r.enabled = true;
            Jugo.Sacudir(0.3f);
        }
        Pensamiento.Decir("backrooms.entrada", 0.4f, alEntrar);
        yield return new WaitForSeconds(1.1f);
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
        EnCinematica = false;
        // Si muere, reaparece ya adentro (no del otro lado de la pared)
        if (PantallaMuerte.Instance != null && !PantallaMuerte.Muerto) PantallaMuerte.Instance.Guardar();
    }
}
