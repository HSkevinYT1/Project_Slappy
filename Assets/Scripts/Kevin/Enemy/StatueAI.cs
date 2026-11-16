using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
public class EstatuaAI : MonoBehaviour
{
    public enum State { Frozen, Moving, Caught }

    public Transform player;
    public Camera playerCamera;
    public float moveSpeed = 3.5f;
    public int stepFrames = 5;

    public LayerMask obstacleMask = ~0;
    public float screenMargin = 0.05f;
    public float maxSeenDistance = 0f;
    public bool faceTargetWhenMoving = true;

    public float catchDistance = 1.2f;

    public UnityEvent onPlayerCaught;
    public UnityEvent onFreeze;
    public UnityEvent onStartMoving;

    public State CurrentState { get; private set; } = State.Frozen;

    private NavMeshAgent agent;
    private Collider[] ownColliders;
    private Collider playerCollider;
    private Transform target;
    private int frameCounter;
    private NavMeshPath reachPath;
    private float nextReachCheck;
    private bool playerReachable;

    private Vector3 posicionInicial;
    private Quaternion rotacionInicial;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        posicionInicial = transform.position;
        rotacionInicial = transform.rotation;
        ownColliders = GetComponentsInChildren<Collider>();

        // Busca al jugador por la etiqueta de player
        if (player == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }
        if (player != null)
        {
            foreach (var c in player.GetComponentsInChildren<Collider>())
                if (!c.isTrigger) { playerCollider = c; break; }
            Rigidbody rb = playerCollider != null ? playerCollider.attachedRigidbody : player.GetComponentInParent<Rigidbody>();
            target = rb != null ? rb.transform : player;
        }
        else Debug.LogWarning(name + ": no encuentro al jugador. Ponle la etiqueta 'Player'.");

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null && player != null) playerCamera = player.GetComponentInChildren<Camera>();
        if (playerCamera == null) Debug.LogWarning(name + ": no encuentro la camara del jugador. Asignala o ponle la etiqueta 'MainCamera'.");

        // Si el enemigo esta un poco separado del NavMesh, lo pega al punto mas cercano
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        if (!agent.isOnNavMesh)
        {
            Debug.LogError(name + ": no esta sobre un NavMesh. Haz Bake en el NavMesh Surface y coloca al enemigo encima de la zona azul.");
            return;
        }

        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.speed = moveSpeed;

        EnterState(State.Frozen);
    }

    void Update()
    {
        if (player == null || playerCamera == null || CurrentState == State.Caught || !agent.isOnNavMesh) return;

        bool seen = IsSeenByPlayer();

        if (seen && CurrentState != State.Frozen) EnterState(State.Frozen);
        else if (!seen && CurrentState != State.Moving) EnterState(State.Moving);

        if (CurrentState == State.Moving) UpdateMoving();
        else agent.nextPosition = transform.position; 
    }

    void EnterState(State next)
    {
        CurrentState = next;
        frameCounter = 0;

        switch (next)
        {
            case State.Frozen:
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.nextPosition = transform.position;
                onFreeze?.Invoke();
                break;
            case State.Moving:
                agent.isStopped = false;
                onStartMoving?.Invoke();
                break;
            case State.Caught:
                agent.isStopped = true;
                onPlayerCaught?.Invoke();
                Debug.Log(name + " atrapo al jugador");
                break;
        }
    }

    // Despues de atrapar al jugador (al reintentar): vuelve a donde empezo, quieta
    public void Reiniciar()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (posicionInicial != Vector3.zero)
        {
            transform.SetPositionAndRotation(posicionInicial, rotacionInicial);
            if (agent.isOnNavMesh) agent.Warp(posicionInicial);
        }
        if (agent.isOnNavMesh) EnterState(State.Frozen);
        else CurrentState = State.Frozen;
    }

    void UpdateMoving()
    {
        // Si no hay camino hasta el jugador (esta en otra area) se queda quieta donde esta
        if (!PlayerReachable())
        {
            agent.ResetPath();
            agent.nextPosition = transform.position;
            frameCounter = 0;
            return;
        }

        agent.SetDestination(target.position);

        if (Vector3.Distance(transform.position, target.position) <= catchDistance)
        {
            EnterState(State.Caught);
            return;
        }

       // en vez de usar animacion usamos 5 frames randoms
        frameCounter++;
        if (frameCounter < stepFrames) return;
        frameCounter = 0;

        Vector3 next = agent.nextPosition;
        Vector3 old = transform.position;
        transform.position = new Vector3(next.x, old.y, next.z);

        if (faceTargetWhenMoving)
        {
            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }


    // Comprueba cada medio segundo si hay un camino completo por el NavMesh hasta el jugador
    bool PlayerReachable()
    {
        // Escondido (ej: en un armario) no lo persigue
        if (Escondite.JugadorEscondido) return false;
        if (Time.time < nextReachCheck) return playerReachable;
        nextReachCheck = Time.time + 0.5f;

        if (reachPath == null) reachPath = new NavMeshPath();
        playerReachable =
            NavMesh.SamplePosition(transform.position, out NavMeshHit from, 2.5f, agent.areaMask) &&
            NavMesh.SamplePosition(target.position, out NavMeshHit to, 2.5f, agent.areaMask) &&
            NavMesh.CalculatePath(from.position, to.position, agent.areaMask, reachPath) &&
            reachPath.status == NavMeshPathStatus.PathComplete;
        return playerReachable;
    }

    bool IsSeenByPlayer()
    {
        Vector3 camPos = playerCamera.transform.position;

        if (maxSeenDistance > 0f && Vector3.Distance(camPos, transform.position) > maxSeenDistance) return false;

        foreach (Vector3 point in GetCheckPoints())
        {
            Vector3 vp = playerCamera.WorldToViewportPoint(point);
            if (vp.z <= 0f) continue;
            if (vp.x < -screenMargin || vp.x > 1f + screenMargin) continue;
            if (vp.y < -screenMargin || vp.y > 1f + screenMargin) continue;

            if (HasLineOfSight(camPos, point)) return true;
        }
        return false;
    }

    Vector3[] GetCheckPoints()
    {
        Bounds b = GetBounds();
        Vector3 c = b.center;
        return new[]
        {
            c,
            new Vector3(c.x, b.max.y, c.z),
            new Vector3(c.x, b.min.y, c.z)
        };
    }

    Bounds GetBounds()
    {
        // Solo cuenta su propio cuerpo: lo que lleve en la mano (ej: la llave) se puede quitar
        bool found = false;
        Bounds b = default;
        if (ownColliders != null)
        {
            foreach (Collider c in ownColliders)
            {
                if (c == null || !c.enabled || c.attachedRigidbody != null || !c.transform.IsChildOf(transform)) continue;
                if (!found) { b = c.bounds; found = true; }
                else b.Encapsulate(c.bounds);
            }
        }
        return found ? b : new Bounds(transform.position + Vector3.up, Vector3.one);
    }

    bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float distance = dir.magnitude;

        var hits = Physics.RaycastAll(from, dir.normalized, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.transform.IsChildOf(player) || player.IsChildOf(hit.collider.transform)) continue;
            return false;
        }
        return true;
    }

    // Depuracion por si acaso, no afecta al juego
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        if (Application.isPlaying && playerCamera != null)
        {
            Gizmos.color = CurrentState == State.Frozen ? Color.cyan : Color.yellow;
            Gizmos.DrawLine(playerCamera.transform.position, GetBounds().center);
        }
    }
}
