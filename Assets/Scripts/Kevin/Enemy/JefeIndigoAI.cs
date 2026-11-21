using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

//Este script controla el comportamiento de un enemigo que patrulla, persigue y busca al jugador
//Por si acaso deben Bakear un NavMesh Surface para que sirva :v
[RequireComponent(typeof(NavMeshAgent))]
public class JefeIndigoAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Search, Caught }

    // Si se deja vacio, busca automaticamente al objeto con PlayerMovement
    public Transform player;
    // Puntos de patrulla en orden. Si no hay, el enemigo deambula al azar
    public Transform[] patrolPoints;

    public float patrolSpeed = 1.8f;
    public float chaseSpeed = 4.5f;
    public float searchSpeed = 2.5f;

    public float viewDistance = 12f;
    [Range(0, 360)] public float viewAngle = 100f;
    // Altura de los ojos del enemigo
    public float eyeHeight = 1.6f;
    // Capas que bloquean la vision (paredes, muebles...)
    public LayerMask obstacleMask = ~0;

    // Distancia a la que oye al jugador moverse aunque no lo vea
    public float hearingRadius = 4f;
    // Velocidad minima del jugador para hacer ruido
    public float noiseSpeedThreshold = 0.5f;

    public float waitAtPointTime = 2f;
    public float wanderRadius = 10f;
    // Tiempo buscando en la ultima posicion conocida antes de volver a patrullar
    public float searchDuration = 6f;
    // Tiempo que sigue persiguiendo despues de perder de vista al jugador
    public float loseSightGrace = 1.5f;
    public float catchDistance = 1.2f;

    [Header("Mas rapido cuanto mas lejos esta el jugador")]
    [Tooltip("Si el jugador esta lejos, el enemigo se mueve mas rapido (patrulla, busqueda y persecucion) para que no se haga aburrido")]
    public bool acelerarConDistancia = false;
    [Tooltip("A esta distancia (m) o menos se mueve a su velocidad normal")]
    public float distanciaNormal = 8f;
    [Tooltip("A esta distancia (m) o mas usa el multiplicador maximo")]
    public float distanciaRapida = 40f;
    [Tooltip("Cuanto mas rapido va con el jugador lejos (2 = el doble)")]
    public float multiplicadorMaximo = 2.2f;

    [Header("Sonidos")]
    [Tooltip("Grito que suelta de vez en cuando mientras patrulla o busca (3D, desde el enemigo)")]
    public AudioClip sonidoAmbiente;
    [Tooltip("Segundos entre gritos de ambiente (al azar entre x e y)")]
    public Vector2 intervaloAmbiente = new Vector2(18f, 40f);
    [Tooltip("Grito cuando ve (o escucha) al jugador y empieza a perseguirlo")]
    public AudioClip sonidoAlVer;
    [Tooltip("Segundos minimos entre dos gritos de 'te vi'")]
    public float esperaAlVer = 8f;
    [Tooltip("Screamer al atrapar al jugador (lo reproduce la pantalla de muerte)")]
    public AudioClip sonidoScreamer;
    [Range(0f, 1f)] public float volumenSonidos = 1f;
    [Tooltip("Hasta donde se oyen sus gritos (m)")]
    public float distanciaSonido = 35f;

    public UnityEvent onPlayerSpotted;
    public UnityEvent onPlayerCaught;

    [Header("Mandar lejos")]
    [Tooltip("Area por la que se mueve (para mandarlo a la otra punta). Vacio = el area (ZonaVolumen) en la que empieza, o un radio alrededor del inicio.")]
    public Collider areaEnemigo;
    [Tooltip("Sin area: radio alrededor de donde empieza")]
    public float radioArea = 60f;

    [Header("Acercarse si el jugador esta lejos")]
    [Tooltip("Si pasa mucho tiempo patrullando lejos del jugador (en la misma area), reaparece mas cerca, " +
             "siempre fuera de la vista del jugador y fuera del alcance del reloj, para que el reloj no salte de golpe")]
    public bool acercarseSiLejos = false;
    [Tooltip("Segundos patrullando lejos antes de reaparecer mas cerca (al azar entre x e y)")]
    public Vector2 esperaAcercarse = new Vector2(20f, 35f);
    [Tooltip("Metros extra por fuera del alcance del reloj donde reaparece (el reloj empieza a notarlo un poco despues)")]
    public float margenReloj = 4f;
    [Tooltip("Sin reloj en la escena: a esta distancia (m) del jugador reaparece")]
    public float distanciaAcercarse = 30f;

    [Header("Inteligencia (enemigo 1)")]
    [Tooltip("Oye los pasos (poco caminando, mas corriendo, menos a traves de paredes) y va a investigar en vez de " +
             "perseguir directo; al perder al jugador va hacia donde iba, revisa mirando alrededor y despues ronda " +
             "cerca de donde lo vio por ultima vez")]
    public bool inteligente = false;
    [Tooltip("Hasta donde oye los pasos caminando / corriendo (m)")]
    public float oidoCaminando = 4.5f;
    public float oidoCorriendo = 11f;
    [Tooltip("Con una pared en medio oye menos (0.5 = la mitad de distancia)")]
    [Range(0f, 1f)] public float oidoTrasPared = 0.5f;
    [Tooltip("Si lo oye mas cerca que esto, va directo a por el")]
    public float distanciaAlarma = 3f;
    [Tooltip("Al perderlo de vista, segundos hacia adelante que adivina por donde va")]
    public float prediccion = 1.2f;
    public float velocidadInvestigar = 3f;
    [Tooltip("Segundos que se queda mirando alrededor en cada punto que revisa")]
    public float tiempoMirarAlrededor = 1.3f;
    [Tooltip("Segundos que recuerda donde vio al jugador (patrulla cerca de alli)")]
    public float memoria = 40f;
    [HideInInspector] public int versionIA;

    public State CurrentState { get; private set; } = State.Patrol;
    // Va hacia un ruido (no lo vio): camina mas rapido que patrullando pero no corre
    public bool Investigando { get; private set; }

    private NavMeshAgent agent;
    private Rigidbody playerBody;
    private Collider playerCollider;
    // Parte del jugador que realmente se mueve (el Rigidbody). La raiz del prefab se queda quieta
    private Transform target;
    private int patrolIndex;
    private float waitTimer;
    private float stateTimer;
    private float lastSeenTime;
    private Vector3 lastKnownPosition;
    private Vector3 inicio;
    // Velocidad del estado actual, antes del multiplicador por distancia
    private float velocidadBase;
    private AudioSource voz;
    private float proximoAmbiente;
    private float ultimoAlVer = -999f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        inicio = transform.position;

        // Busca al jugador por la etiqueta de player
        if (player == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }
        if (player != null)
        {
            // El collider del cuerpo (no los triggers)
            foreach (var c in player.GetComponentsInChildren<Collider>())
                if (!c.isTrigger) { playerCollider = c; break; }
            if (playerCollider != null) playerBody = playerCollider.attachedRigidbody;
            if (playerBody == null) playerBody = player.GetComponentInParent<Rigidbody>();
            target = playerBody != null ? playerBody.transform : playerCollider != null ? playerCollider.transform : player;
        }
        else Debug.LogWarning(name + ": no encuentro al jugador. Ponle la etiqueta 'Player'.");

        // Si el enemigo esta un poco separado del NavMesh, lo pega al punto mas cercano
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        if (!agent.isOnNavMesh)
        {
            Debug.LogError(name + ": no esta sobre un NavMesh. Haz Bake en el NavMesh Surface y coloca al enemigo encima de la zona azul.");
            return;
        }

        EnterState(State.Patrol);
    }

    AudioSource Voz()
    {
        if (voz != null) return voz;
        voz = gameObject.AddComponent<AudioSource>();
        voz.playOnAwake = false;
        voz.spatialBlend = 1f;
        voz.rolloffMode = AudioRolloffMode.Linear;
        voz.minDistance = 3f;
        voz.maxDistance = distanciaSonido;
        voz.dopplerLevel = 0f;
        return voz;
    }

    void Gritar(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource v = Voz();
        v.maxDistance = distanciaSonido;
        v.Stop();
        v.clip = clip;
        v.volume = volumenSonidos;
        v.Play();
    }

    void SonidoAmbiente()
    {
        if (sonidoAmbiente == null) return;
        if (proximoAmbiente <= 0f) { proximoAmbiente = Time.time + Random.Range(intervaloAmbiente.x, intervaloAmbiente.y) * 0.5f; return; }
        if (Time.time < proximoAmbiente) return;
        proximoAmbiente = Time.time + Random.Range(intervaloAmbiente.x, intervaloAmbiente.y);
        if (CurrentState == State.Chase || (voz != null && voz.isPlaying)) return;
        Gritar(sonidoAmbiente);
    }

    void Update()
    {
        if (player == null || CurrentState == State.Caught || !agent.isOnNavMesh) return;

        bool sees = CanSeePlayer();
        bool hears = inteligente ? Escuchar() : CanHearPlayer();
        if (sees || hears)
        {
            lastKnownPosition = target.position;
            lastSeenTime = Time.time;
            if (playerBody != null) velocidadVista = playerBody.linearVelocity;
            vistoAlguna = true;
            Investigando = false;
            if (CurrentState != State.Chase) EnterState(State.Chase);
        }

        switch (CurrentState)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase: UpdateChase(sees || hears); break;
            case State.Search: UpdateSearch(); break;
        }

        if (CurrentState != State.Caught) agent.speed = velocidadBase * MultiplicadorDistancia();
        SonidoAmbiente();
        AcercarseSiLejos();
    }

    // ------------------------------------------------------------------ Acercarse si esta lejos

    private float tiempoLejos;
    private float esperaActual = -1f;
    private float proximoIntento;
    private Camera camJugador;

    // Distancia a la que reaparece: justo por fuera de lo que detecta el reloj
    float DistanciaAcercarse()
    {
        RelojJugador reloj = RelojJugador.Instance;
        return reloj != null ? reloj.distanciaMaxima + margenReloj : distanciaAcercarse;
    }

    void AcercarseSiLejos()
    {
        if (!acercarseSiLejos || CurrentState != State.Patrol || PantallaMuerte.Muerto || Escondite.JugadorEscondido) { tiempoLejos = 0f; return; }
        // Se revisa cada medio segundo (Area() busca las zonas de la escena)
        if (Time.time < proximoIntento) return;
        proximoIntento = Time.time + 0.5f;

        float lejos = DistanciaAcercarse();
        // Solo si el jugador esta en su misma area (no lo trae al Laberinto si el jugador esta en otra zona)
        Bounds area = Area();
        Vector3 pj = target.position;
        bool mismaArea = pj.x >= area.min.x && pj.x <= area.max.x && pj.z >= area.min.z && pj.z <= area.max.z;
        if (!mismaArea || Vector3.Distance(transform.position, pj) <= lejos + 2f) { tiempoLejos = 0f; return; }

        if (esperaActual < 0f) esperaActual = Random.Range(esperaAcercarse.x, Mathf.Max(esperaAcercarse.x, esperaAcercarse.y));
        tiempoLejos += 0.5f;
        if (tiempoLejos < esperaActual) return;

        // Que el jugador no lo vea desaparecer
        if (VisibleParaJugador(transform.position)) return;
        if (!PuntoCercaDelJugador(lejos, area, out Vector3 punto)) return;

        agent.Warp(punto);
        agent.velocity = Vector3.zero;
        tiempoLejos = 0f;
        esperaActual = -1f;
        // Desde alli camina hacia donde anda el jugador (sin saber exactamente donde esta)
        agent.SetDestination(RandomNavPoint(pj, 8f));
        waitTimer = 0f;
    }

    // Un punto del NavMesh a "distancia" del jugador, dentro del area, al que se llega caminando desde el jugador
    // y que el jugador no esta viendo
    bool PuntoCercaDelJugador(float distancia, Bounds area, out Vector3 punto)
    {
        punto = default;
        var camino = new NavMeshPath();
        Vector3 pj = target.position;
        for (int i = 0; i < 24; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 p = pj + new Vector3(dir.x, 0f, dir.y) * Random.Range(distancia, distancia + 6f);
            if (p.x < area.min.x || p.x > area.max.x || p.z < area.min.z || p.z > area.max.z) continue;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            // Que no quede mas cerca de lo pedido (el reloj lo notaria de golpe)
            if (Vector3.Distance(hit.position, pj) < distancia) continue;
            if (!NavMesh.CalculatePath(hit.position, pj, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;
            if (VisibleParaJugador(hit.position)) continue;
            punto = hit.position;
            return true;
        }
        return false;
    }

    // El jugador podria ver este punto: esta en pantalla y no hay pared en medio
    bool VisibleParaJugador(Vector3 pies)
    {
        if (camJugador == null) camJugador = Camera.main;
        if (camJugador == null) return false;
        Vector3 desde = camJugador.transform.position;
        Vector3[] puntos = { pies + Vector3.up * 0.3f, pies + Vector3.up * eyeHeight };
        foreach (Vector3 p in puntos)
        {
            Vector3 vp = camJugador.WorldToViewportPoint(p);
            if (vp.z <= 0f || vp.x < -0.1f || vp.x > 1.1f || vp.y < -0.1f || vp.y > 1.1f) continue;
            Vector3 d = p - desde;
            bool tapado = false;
            foreach (RaycastHit h in Physics.RaycastAll(desde, d.normalized, d.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform) || IsPlayer(h.collider)) continue;
                tapado = true;
                break;
            }
            if (!tapado) return true;
        }
        return false;
    }

    // 1 con el jugador cerca; sube hasta multiplicadorMaximo cuanto mas lejos esta
    public float MultiplicadorDistancia()
    {
        if (!acelerarConDistancia || target == null) return 1f;
        float d = Vector3.Distance(transform.position, target.position);
        float k = Mathf.InverseLerp(distanciaNormal, Mathf.Max(distanciaNormal + 0.1f, distanciaRapida), d);
        return Mathf.Lerp(1f, Mathf.Max(1f, multiplicadorMaximo), k);
    }


    void EnterState(State next)
    {
        CurrentState = next;
        Debug.Log(name + " -> " + next);
        stateTimer = 0f;
        waitTimer = 0f;
        // Si estaba mirando alrededor (busqueda inteligente), vuelve a caminar normal
        mirandoHasta = -1f;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
        if (agent != null) agent.updateRotation = true;
        if (next != State.Search) Investigando = false;

        switch (next)
        {
            case State.Patrol:
                velocidadBase = patrolSpeed;
                agent.speed = velocidadBase * MultiplicadorDistancia();
                GoToNextPatrolPoint();
                break;
            case State.Chase:
                velocidadBase = chaseSpeed;
                agent.speed = velocidadBase * MultiplicadorDistancia();
                if (Time.time - ultimoAlVer >= esperaAlVer)
                {
                    ultimoAlVer = Time.time;
                    Gritar(sonidoAlVer);
                    // Jugo: te vio. Golpe de camara y zoom hacia adentro (mas fuerte cuanto mas cerca)
                    if (player != null)
                    {
                        float k = 1f - Mathf.Clamp01(Vector3.Distance(transform.position, player.position) / Mathf.Max(1f, viewDistance));
                        Jugo.Sacudir(Mathf.Lerp(0.2f, 0.45f, k));
                        Jugo.PatadaFOV(-Mathf.Lerp(1.5f, 3.5f, k));
                    }
                }
                onPlayerSpotted?.Invoke();
                break;
            case State.Search:
                velocidadBase = searchSpeed;
                agent.speed = velocidadBase * MultiplicadorDistancia();
                agent.SetDestination(lastKnownPosition);
                break;
            case State.Caught:
                agent.isStopped = true;
                if (voz != null) voz.Stop();
                onPlayerCaught?.Invoke();
                Debug.Log(name + " atrapo al jugador");
                break;
        }
    }

    void UpdatePatrol()
    {
        if (!ReachedDestination()) return;

        // Espera un momento en cada punto antes de seguir
        waitTimer += Time.deltaTime;
        if (waitTimer >= waitAtPointTime)
        {
            waitTimer = 0f;
            GoToNextPatrolPoint();
        }
    }

    void UpdateChase(bool detectingPlayer)
    {
        agent.SetDestination(detectingPlayer ? target.position : lastKnownPosition);

        if (!Escondite.JugadorEscondido && Vector3.Distance(transform.position, target.position) <= catchDistance)
        {
            EnterState(State.Caught);
            return;
        }

        // Si lo pierde durante un rato, pasa a buscarlo
        if (Time.time - lastSeenTime > loseSightGrace)
        {
            // Inteligente: no va a donde lo vio, sino a donde iba
            if (inteligente) lastKnownPosition = Predecir(lastKnownPosition, velocidadVista * prediccion);
            EnterState(State.Search);
        }
    }

    void UpdateSearch()
    {
        if (inteligente) { BuscarInteligente(); return; }
        stateTimer += Time.deltaTime;

        // Al llegar a la ultima posicion conocida, revisa puntos cercanos
        if (ReachedDestination())
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= 1f)
            {
                waitTimer = 0f;
                agent.SetDestination(RandomNavPoint(lastKnownPosition, 4f));
            }
        }

        if (stateTimer >= searchDuration)
            EnterState(State.Patrol);
    }


    bool CanSeePlayer()
    {
        // Escondido (ej: en un armario) no se le ve
        if (Escondite.JugadorEscondido) return false;

        Vector3 eyes = EyePosition();
        // Apunta al centro del cuerpo del jugador
        Vector3 aim = playerCollider != null ? playerCollider.bounds.center : target.position;
        Vector3 toPlayer = aim - eyes;
        float distance = toPlayer.magnitude;

        if (distance > viewDistance) return false;

        // El angulo se mide en horizontal para que la diferencia de altura no cuente
        Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
        if (flat.sqrMagnitude > 0.001f && Vector3.Angle(transform.forward, flat) > viewAngle / 2f) return false;

        // Comprueba que no haya paredes en medio, ignorando los colliders del propio enemigo
        var hits = Physics.RaycastAll(eyes, toPlayer.normalized, distance + 0.5f, obstacleMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            return IsPlayer(hit.collider);
        }
        return true;
    }

    // Los ojos se miden desde los pies para que el enemigo pueda ver al jugador siempre
    Vector3 EyePosition()
    {
        float baseOffset = agent != null ? agent.baseOffset : GetComponent<NavMeshAgent>().baseOffset;
        return transform.position + Vector3.up * (eyeHeight - baseOffset);
    }

    bool IsPlayer(Collider c)
    {
        if (c.CompareTag("Player")) return true;
        if (c.attachedRigidbody != null && c.attachedRigidbody.CompareTag("Player")) return true;
        return c.transform.IsChildOf(player) || player.IsChildOf(c.transform);
    }

    bool CanHearPlayer()
    {
        if (playerBody == null || Escondite.JugadorEscondido) return false;
        if (Vector3.Distance(transform.position, target.position) > hearingRadius) return false;

        Vector3 v = playerBody.linearVelocity;
        v.y = 0f;
        return v.magnitude > noiseSpeedThreshold;
    }


    // ------------------------------------------------------------------ Inteligencia

    private Vector3 velocidadVista;
    private bool vistoAlguna;
    private float mirandoHasta = -1f, giroMirar, proximoRuido;
    private Vector3 direccionBusqueda;

    // Oye los pasos del jugador. Devuelve true solo si lo oye tan cerca que va directo a por el;
    // si lo oye mas lejos, va a investigar el ruido (sin saber exactamente donde esta)
    bool Escuchar()
    {
        if (playerBody == null || Escondite.JugadorEscondido) return false;
        Vector3 v = playerBody.linearVelocity; v.y = 0f;
        if (v.magnitude <= noiseSpeedThreshold) return false;

        PlayerMovement pm = playerBody.GetComponent<PlayerMovement>();
        float alcance = pm != null && pm.Corriendo ? oidoCorriendo : oidoCaminando;
        Vector3 desde = EyePosition();
        Vector3 hasta = playerCollider != null ? playerCollider.bounds.center : target.position;
        if (HayParedEntre(desde, hasta)) alcance *= oidoTrasPared;
        float d = Vector3.Distance(transform.position, target.position);
        if (d > alcance) return false;
        if (d <= distanciaAlarma || CurrentState == State.Chase) return true;

        // Lo oyo de lejos: va a ver que fue (cada tanto corrige hacia el ruido nuevo)
        if (Time.time < proximoRuido) return false;
        proximoRuido = Time.time + 0.8f;
        Vector3 error = Random.insideUnitSphere * Mathf.Lerp(0.5f, 2.5f, d / Mathf.Max(0.1f, alcance));
        error.y = 0f;
        lastKnownPosition = Predecir(target.position + error, Vector3.zero);
        direccionBusqueda = (target.position - transform.position).normalized;
        if (CurrentState == State.Patrol || CurrentState == State.Search)
        {
            bool yaBuscaba = CurrentState == State.Search;
            Investigando = true;
            if (!yaBuscaba) EnterState(State.Search);
            else { agent.SetDestination(lastKnownPosition); stateTimer = 0f; mirandoHasta = -1f; }
            velocidadBase = velocidadInvestigar;
        }
        return false;
    }

    bool HayParedEntre(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        foreach (RaycastHit h in Physics.RaycastAll(a, d.normalized, d.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || IsPlayer(h.collider)) continue;
            return true;
        }
        return false;
    }

    // Un punto del NavMesh al que se llega, lo mas cerca posible de desde + adelanto
    Vector3 Predecir(Vector3 desde, Vector3 adelanto)
    {
        adelanto.y = 0f;
        var camino = new NavMeshPath();
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = desde + adelanto * (1f - i * 0.3f);
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) continue;
            if (agent.isOnNavMesh && NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, camino) &&
                camino.status == NavMeshPathStatus.PathComplete) return hit.position;
        }
        return NavMesh.SamplePosition(desde, out NavMeshHit h2, 2.5f, NavMesh.AllAreas) ? h2.position : desde;
    }

    // Busqueda: va al punto, se queda mirando alrededor y revisa puntos cada vez mas lejos,
    // sobre todo hacia donde iba el jugador
    void BuscarInteligente()
    {
        stateTimer += Time.deltaTime;
        if (mirandoHasta > 0f)
        {
            // Mirando alrededor: gira a un lado y al otro
            float k = 1f - (mirandoHasta - Time.time) / Mathf.Max(0.1f, tiempoMirarAlrededor);
            transform.rotation = Quaternion.Euler(0f, giroMirar + Mathf.Sin(k * Mathf.PI * 2f) * 75f, 0f);
            if (Time.time < mirandoHasta) return;
            mirandoHasta = -1f;
            agent.isStopped = false;
            agent.updateRotation = true;
            float radio = Mathf.Lerp(4f, 11f, stateTimer / Mathf.Max(1f, searchDuration * 1.6f));
            Vector3 centro = lastKnownPosition + direccionBusqueda * radio * 0.4f;
            agent.SetDestination(RandomNavPoint(centro, radio));
        }
        else if (ReachedDestination())
        {
            mirandoHasta = Time.time + tiempoMirarAlrededor * Random.Range(0.8f, 1.3f);
            giroMirar = transform.eulerAngles.y;
            agent.isStopped = true;
            agent.updateRotation = false;
            if (velocidadVista.sqrMagnitude > 0.1f) direccionBusqueda = new Vector3(velocidadVista.x, 0f, velocidadVista.z).normalized;
        }

        if (stateTimer >= searchDuration * (Investigando ? 1f : 1.6f))
        {
            mirandoHasta = -1f;
            agent.isStopped = false;
            agent.updateRotation = true;
            Investigando = false;
            EnterState(State.Patrol);
        }
    }

    void GoToNextPatrolPoint()
    {
        // Inteligente: un rato despues de verlo sigue rondando por esa zona
        if (inteligente && vistoAlguna && Time.time - lastSeenTime < memoria && Random.value < 0.6f)
        {
            agent.SetDestination(RandomNavPoint(lastKnownPosition, wanderRadius * 0.6f));
            return;
        }
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            agent.SetDestination(patrolPoints[patrolIndex].position);
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            agent.SetDestination(RandomNavPoint(transform.position, wanderRadius));
        }
    }

    bool ReachedDestination()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
    }

    Vector3 RandomNavPoint(Vector3 center, float radius)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 random = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(random, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return hit.position;
        }
        return center;
    }

    // ------------------------------------------------------------------ Mandar lejos / reiniciar

    // Manda al enemigo a la otra punta de su area, lo mas lejos posible de "desde" (ej: el jugador),
    // y lo deja patrullando por alli. Lo usan la presentacion del enemigo y la pantalla de muerte.
    public void MandarLejos(Vector3 desde)
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (inicio == Vector3.zero) inicio = transform.position;
        Vector3 destino = PuntoLejano(desde);
        if (agent.isOnNavMesh) agent.Warp(destino); else transform.position = destino + Vector3.up * agent.baseOffset;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        // La primera patrulla sale desde alli, sin perseguir a nadie (y sin recordar donde lo vio)
        lastSeenTime = -999f;
        vistoAlguna = false;
        if (enabled && agent.isOnNavMesh) EnterState(State.Patrol);
        else CurrentState = State.Patrol;
    }

    // Despues de atrapar al jugador (al reintentar): vuelve a patrullar, lejos de donde reaparece el jugador
    public void Reiniciar(Vector3 jugador)
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        CurrentState = State.Patrol;
        MandarLejos(jugador);
    }

    public Bounds Area()
    {
        if (areaEnemigo != null) return areaEnemigo.bounds;
        Vector3 casa = inicio != Vector3.zero ? inicio : transform.position;
        // El area (Laberinto, Cloacas...) en la que empezo: la mas chica que lo contiene
        Bounds elegida = default; float menor = float.MaxValue;
        foreach (ZonaVolumen z in FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!z.TryGetComponent(out BoxCollider bc)) continue;
            Bounds b = bc.enabled ? bc.bounds : new Bounds(z.transform.TransformPoint(bc.center), Vector3.Scale(bc.size, z.transform.lossyScale));
            Vector3 c = new Vector3(casa.x, b.center.y, casa.z);
            if (!b.Contains(c)) continue;
            float v = b.size.x * b.size.z;
            if (v < menor) { menor = v; elegida = b; }
        }
        if (menor < float.MaxValue) return elegida;
        return new Bounds(casa, new Vector3(radioArea * 2f, 50f, radioArea * 2f));
    }

    Vector3[] verticesNav;

    Vector3 PuntoLejano(Vector3 desde)
    {
        if (verticesNav == null) verticesNav = NavMesh.CalculateTriangulation().vertices;
        Bounds area = Area();
        Vector3 origen = transform.position;
        var camino = new NavMeshPath();
        Vector3 mejor = origen; float mejorDist = -1f;

        // Los vertices del NavMesh dentro del area, de mas lejano a mas cercano; el primero alcanzable gana
        var candidatos = new System.Collections.Generic.List<Vector3>();
        foreach (Vector3 v in verticesNav)
        {
            if (v.x < area.min.x || v.x > area.max.x || v.z < area.min.z || v.z > area.max.z) continue;
            candidatos.Add(v);
        }
        candidatos.Sort((a, b) => (b - desde).sqrMagnitude.CompareTo((a - desde).sqrMagnitude));
        int probados = 0;
        foreach (Vector3 v in candidatos)
        {
            if (++probados > 40) break;
            // Un poco hacia adentro del borde, sobre el NavMesh
            if (!NavMesh.SamplePosition(v, out NavMeshHit h, 2f, NavMesh.AllAreas)) continue;
            if (agent.isOnNavMesh && (!NavMesh.CalculatePath(origen, h.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete)) continue;
            float d = (h.position - desde).sqrMagnitude;
            if (d > mejorDist) { mejorDist = d; mejor = h.position; break; }
        }
        // Que no quede pegado a una esquina: unos metros hacia el centro del area
        Vector3 haciaCentro = area.center - mejor; haciaCentro.y = 0f;
        if (haciaCentro.magnitude > 4f && NavMesh.SamplePosition(mejor + haciaCentro.normalized * 2f, out NavMeshHit dentro, 2f, NavMesh.AllAreas))
            mejor = dentro.position;
        return mejor;
    }

    // Cono de vision y radio de escucha (es depuracion por si acaso, no afecta al juego)
    void OnDrawGizmosSelected()
    {
        Vector3 eyes = EyePosition();
        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;
        Gizmos.DrawLine(eyes, eyes + left * viewDistance);
        Gizmos.DrawLine(eyes, eyes + right * viewDistance);
        Gizmos.DrawLine(eyes + left * viewDistance, eyes + transform.forward * viewDistance);
        Gizmos.DrawLine(eyes + right * viewDistance, eyes + transform.forward * viewDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        if (Application.isPlaying && CurrentState != State.Patrol)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(lastKnownPosition, 0.2f);
        }
    }
}
