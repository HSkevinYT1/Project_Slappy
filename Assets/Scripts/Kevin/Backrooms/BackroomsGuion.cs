using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Los Backrooms segun el guion:
// 1. Al entrar (unos metros adentro) el jugador se gira y ve como la puerta por la que vino desaparece
//    (aparece pared en su lugar): "Es en serio... otra vez."
// 2. En una habitacion hay un cadaver con el gato de sombra encima. Cuando el jugador lo ve: "¡¿QUE MIERDA ES ESO?!",
//    se oye un grito de otra persona y el gato sale corriendo de la habitacion (desde ahi su IA sigue sola).
// 3. Al entrar a la habitacion nota la palanca y la piedra sobre el cadaver: "¿Y esto?"
// Las palancas, el boton y el portal los lleva PuzzlePalancas. La muerte especial del gato: MuerteGato.
// Lo arma Tools > Backrooms > Palancas (BackroomsPalancasSetup).
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

    [Header("La habitacion del cadaver")]
    public BoxCollider habitacion;
    public JefeIndigoAI gato;
    [Tooltip("Donde esta el gato al principio (encima del cadaver)")]
    public Transform puntoGato;
    [Tooltip("Cuanto mas arriba del piso esta el gato al principio (lo que mide el cadaver tirado)")]
    public float alturaCadaver = 0.24f;
    [Tooltip("Hacia donde sale corriendo")]
    public Transform huidaGato;
    [Tooltip("Grito de otra persona (opcional) y desde donde suena")]
    public AudioClip grito;
    public Transform origenGrito;
    [Range(0f, 1f)] public float volumenGrito = 1f;
    [Tooltip("A esta distancia (m) o menos el jugador lo puede ver")]
    public float distanciaVerGato = 16f;
    [TextArea] public string alVerGato = "¡¿QUÉ MIERDA ES ESO?!";
    [TextArea] public string alEntrarHabitacion = "¿Y esto?";

    public bool Entro { get; private set; }
    public bool GatoSuelto { get; private set; }

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
            if (!gato.GetComponent<MuerteGato>()) gato.gameObject.AddComponent<MuerteGato>();
            StartCoroutine(PonerGato());
        }
    }

    // El gato espera quieto encima del cadaver (sin IA) hasta que el jugador lo ve
    IEnumerator PonerGato()
    {
        yield return null;   // despues del Start de su IA
        gato.enabled = false;
        NavMeshAgent a = gato.GetComponent<NavMeshAgent>();
        if (a != null && puntoGato != null && NavMesh.SamplePosition(puntoGato.position, out NavMeshHit h, 3f, NavMesh.AllAreas))
        {
            a.Warp(h.position);
            a.isStopped = true;
            a.ResetPath();
            // Subido encima del cuerpo (el agente se queda en el piso hasta que salga corriendo)
            a.updatePosition = false;
            gato.transform.position = new Vector3(puntoGato.position.x, h.position.y + a.baseOffset + alturaCadaver, puntoGato.position.z);
        }
        if (puntoGato != null) gato.transform.rotation = Quaternion.Euler(0f, puntoGato.eulerAngles.y, 0f);
    }

    void Update()
    {
        if (Time.time < proxima) return;
        proxima = Time.time + 0.1f;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }
        if (cam == null) cam = Camera.main;
        if (PantallaMuerte.Muerto) return;

        // 1. Entrada
        if (!Entro && entrada != null && zona != null && zona.JugadorDentro)
        {
            float adentro = -Vector3.Dot(jugador.position - entrada.position, entrada.forward);
            if (adentro >= metrosEntrada) { Entro = true; entroEn = Time.time; StartCoroutine(CinematicaEntrada()); }
        }

        // 2. El gato
        bool enHabitacion = habitacion != null && habitacion.bounds.Contains(jugador.position + Vector3.up * 0.5f);
        if (!gatoVisto && gato != null && (enHabitacion || VeAlGato()))
        {
            gatoVisto = true;
            StartCoroutine(GatoHuye());
        }

        // 2a. No lo encontro en el cuarto: el gato se baja del cadaver y sale a buscarlo
        if (Entro && !gatoVisto && gato != null && segundosSoltarGato > 0f && Time.time - entroEn > segundosSoltarGato)
        {
            gatoVisto = true;
            StartCoroutine(GatoSale());
        }

        // 2b. El gato acecha
        Acechar();

        // 3. La palanca y la piedra
        if (GatoSuelto && !habitacionDicha && enHabitacion)
        {
            habitacionDicha = true;
            Pensamiento.Decir("backrooms.habitacion", 0.4f, alEntrarHabitacion);
        }
    }

    bool VeAlGato()
    {
        if (cam == null) return false;
        // El centro del cuerpo del gato (es chico: esta cerca del piso, no a la altura del agente)
        NavMeshAgent ag = gato.GetComponent<NavMeshAgent>();
        Vector3 p = gato.transform.position + Vector3.up * (0.3f - (ag != null ? ag.baseOffset : 0f));
        if ((p - cam.transform.position).sqrMagnitude > distanciaVerGato * distanciaVerGato) return false;
        Vector3 vp = cam.WorldToViewportPoint(p);
        if (vp.z <= 0f || vp.x < 0.05f || vp.x > 0.95f || vp.y < 0.05f || vp.y > 0.95f) return false;
        foreach (RaycastHit h in Physics.RaycastAll(cam.transform.position, (p - cam.transform.position).normalized,
                     Vector3.Distance(cam.transform.position, p), ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(gato.transform) || h.collider.transform.IsChildOf(jugador)) continue;
            return false;
        }
        return true;
    }

    IEnumerator GatoHuye()
    {
        Pensamiento.Decir("backrooms.gato", 0f, alVerGato);
        yield return new WaitForSeconds(1.2f);
        if (grito != null)
        {
            Vector3 donde = origenGrito != null ? origenGrito.position : (huidaGato != null ? huidaGato.position : gato.transform.position);
            Jugo.Sacudir(0.15f);
            var go = new GameObject("Grito");
            go.transform.position = donde;
            var a = go.AddComponent<AudioSource>();
            a.clip = grito; a.volume = volumenGrito; a.spatialBlend = 0.7f; a.minDistance = 8f; a.maxDistance = 80f;
            a.rolloffMode = AudioRolloffMode.Linear; a.Play();
            Destroy(go, grito.length + 0.2f);
        }
        yield return new WaitForSeconds(0.25f);

        // Sale corriendo hacia el grito
        NavMeshAgent agent = gato.GetComponent<NavMeshAgent>();
        // Salta del cuerpo al piso
        if (agent != null && !agent.updatePosition)
        {
            Vector3 desde = gato.transform.position, hasta = agent.nextPosition + Vector3.up * agent.baseOffset;
            for (float t = 0f; t < 0.18f; t += Time.deltaTime)
            {
                float k = t / 0.18f;
                gato.transform.position = Vector3.Lerp(desde, hasta, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.25f);
                yield return null;
            }
            agent.updatePosition = true;
        }
        if (agent != null && agent.isOnNavMesh && huidaGato != null)
        {
            agent.isStopped = false;
            agent.speed = gato.chaseSpeed * 1.4f;
            agent.SetDestination(huidaGato.position);
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                if (!agent.pathPending && agent.remainingDistance < 1.5f) break;
                yield return null;
            }
        }
        GatoSuelto = true;
        if (gato != null) gato.enabled = true;   // desde aqui patrulla, oye y persigue solo
        proximoAcecho = Time.time + Random.Range(acechoCada.x, acechoCada.y);
    }

    [Tooltip("Segundos dentro de los Backrooms sin haber visto al gato hasta que se baja del cadaver y sale a buscar al jugador (0 = nunca)")]
    public float segundosSoltarGato = 60f;
    float entroEn;

    // Sale a buscarlo (sin la frase de verlo): salta del cuerpo y va hacia donde anda el jugador
    IEnumerator GatoSale()
    {
        NavMeshAgent agent = gato.GetComponent<NavMeshAgent>();
        if (agent != null && !agent.updatePosition)
        {
            Vector3 desde = gato.transform.position, hasta = agent.nextPosition + Vector3.up * agent.baseOffset;
            for (float t = 0f; t < 0.18f; t += Time.deltaTime)
            {
                gato.transform.position = Vector3.Lerp(desde, hasta, t / 0.18f);
                yield return null;
            }
            agent.updatePosition = true;
        }
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
        GatoSuelto = true;
        gato.enabled = true;
        yield return null;
        Vector3 error = Random.insideUnitSphere * errorAcecho; error.y = 0f;
        if (jugador != null) gato.Investigar(jugador.position + error);
        proximoAcecho = Time.time + Random.Range(acechoCada.x, acechoCada.y);
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
        // Si muere, reaparece ya adentro (no del otro lado de la pared)
        if (PantallaMuerte.Instance != null && !PantallaMuerte.Muerto) PantallaMuerte.Instance.Guardar();
    }
}
