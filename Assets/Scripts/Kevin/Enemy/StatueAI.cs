using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

//Enemigo tipo estatua: si el jugador lo mira se queda quieto, si no lo mira avanza hacia el
//No usa animacion: se mueve a saltitos, actualizando su posicion cada cierta cantidad de frames
//Por si acaso deben Bakear un NavMesh Surface para que sirva :v
[RequireComponent(typeof(NavMeshAgent))]
public class EstatuaAI : MonoBehaviour
{
    public enum State { Frozen, Moving, Caught }

    // Si se deja vacio, busca automaticamente al objeto con la etiqueta Player
    public Transform player;
    // Camara del jugador. Si se deja vacia usa Camera.main
    public Camera playerCamera;

    // Velocidad real con la que el agente recorre el camino (entre saltos)
    public float moveSpeed = 3.5f;
    // Cada cuantos frames se actualiza la posicion visible del enemigo
    public int stepFrames = 5;

    // Capas que bloquean la vision (paredes, muebles...)
    public LayerMask obstacleMask = ~0;
    // Margen extra del borde de la pantalla (0.1 = lo considera visto un poco antes de entrar del todo)
    public float screenMargin = 0.05f;
    // Distancia maxima a la que el jugador "lo ve". 0 = sin limite
    public float maxSeenDistance = 0f;
    // Si es true, el enemigo se gira hacia el jugador en cada salto
    public bool faceTargetWhenMoving = true;

    public float catchDistance = 1.2f;

    public UnityEvent onPlayerCaught;
    // Se llaman al cambiar entre quieto y en movimiento (por si quieres sonidos)
    public UnityEvent onFreeze;
    public UnityEvent onStartMoving;

    public State CurrentState { get; private set; } = State.Frozen;

    private NavMeshAgent agent;
    private Collider[] ownColliders;
    private Collider playerCollider;
    private Transform target;
    private int frameCounter;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
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

        // El agente calcula el camino por su cuenta, pero nosotros movemos el transform a saltos
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
        else agent.nextPosition = transform.position; // Quieto: evita que el agente se adelante
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

    void UpdateMoving()
    {
        agent.SetDestination(target.position);

        if (Vector3.Distance(transform.position, target.position) <= catchDistance)
        {
            EnterState(State.Caught);
            return;
        }

        // Solo aplica la posicion cada X frames, asi se ve que "salta" sin animacion
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


    // Devuelve true si el jugador tiene al enemigo en pantalla y sin nada en medio
    bool IsSeenByPlayer()
    {
        Vector3 camPos = playerCamera.transform.position;

        if (maxSeenDistance > 0f && Vector3.Distance(camPos, transform.position) > maxSeenDistance) return false;

        // Revisa varios puntos del cuerpo: si cualquiera se ve, cuenta como visto
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
        if (ownColliders != null && ownColliders.Length > 0)
        {
            Bounds b = ownColliders[0].bounds;
            for (int i = 1; i < ownColliders.Length; i++) b.Encapsulate(ownColliders[i].bounds);
            return b;
        }
        return new Bounds(transform.position + Vector3.up, Vector3.one);
    }

    bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float distance = dir.magnitude;

        var hits = Physics.RaycastAll(from, dir.normalized, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            // Ignora al propio enemigo y al jugador, solo cuentan las paredes y demas
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
