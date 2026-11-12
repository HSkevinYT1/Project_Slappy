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

    public UnityEvent onPlayerSpotted;
    public UnityEvent onPlayerCaught;

    [Header("Mandar lejos")]
    [Tooltip("Area por la que se mueve (para mandarlo a la otra punta). Vacio = el area (ZonaVolumen) en la que empieza, o un radio alrededor del inicio.")]
    public Collider areaEnemigo;
    [Tooltip("Sin area: radio alrededor de donde empieza")]
    public float radioArea = 60f;

    public State CurrentState { get; private set; } = State.Patrol;

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

    void Update()
    {
        if (player == null || CurrentState == State.Caught || !agent.isOnNavMesh) return;

        bool sees = CanSeePlayer();
        bool hears = CanHearPlayer();
        if (sees || hears)
        {
            lastKnownPosition = target.position;
            lastSeenTime = Time.time;
            if (CurrentState != State.Chase) EnterState(State.Chase);
        }

        switch (CurrentState)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase: UpdateChase(sees || hears); break;
            case State.Search: UpdateSearch(); break;
        }
    }


    void EnterState(State next)
    {
        CurrentState = next;
        Debug.Log(name + " -> " + next);
        stateTimer = 0f;
        waitTimer = 0f;

        switch (next)
        {
            case State.Patrol:
                agent.speed = patrolSpeed;
                GoToNextPatrolPoint();
                break;
            case State.Chase:
                agent.speed = chaseSpeed;
                onPlayerSpotted?.Invoke();
                break;
            case State.Search:
                agent.speed = searchSpeed;
                agent.SetDestination(lastKnownPosition);
                break;
            case State.Caught:
                agent.isStopped = true;
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
            EnterState(State.Search);
    }

    void UpdateSearch()
    {
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


    void GoToNextPatrolPoint()
    {
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
        // La primera patrulla sale desde alli, sin perseguir a nadie
        lastSeenTime = -999f;
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
