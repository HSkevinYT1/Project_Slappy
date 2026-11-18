using UnityEngine;

// Clase encargada de controlar el movimiento del jugador
public class PlayerMovement : MonoBehaviour
{
    //modificar velocidad
    public float speed = 5f;
    // Al mantener Shift se multiplica la velocidad (solo yendo hacia adelante)
    public float multiplicadorCorrer = 1.7f;
    // Segundos que tarda en llegar a la velocidad de correr (y en volver a caminar)
    public float aceleracionCorrer = 0.25f;
    public bool Corriendo { get; private set; }

    [Header("Estamina")]
    [Tooltip("Segundos que se puede correr con la estamina llena")]
    public float segundosCorriendo = 6f;
    [Tooltip("Segundos que tarda en llenarse otra vez desde vacia")]
    public float segundosRecuperar = 4f;
    [Tooltip("Segundos sin correr antes de empezar a recuperar")]
    public float esperaRecuperar = 0.8f;
    [Tooltip("Al vaciarse, no se puede volver a correr hasta tener esta fraccion (0..1)")]
    [Range(0f, 1f)] public float minimoTrasAgotarse = 0.3f;
    // 0 = vacia, 1 = llena (la lee la barra del HUD)
    public float Estamina { get; private set; } = 1f;
    // Se vacio y todavia no recupero el minimo
    public bool Agotado { get; private set; }
    private float ultimoCorrer = -999f;

    private float factorCorrer = 1f;
    private float adelante;
    // Guarda una referencia al Rigidbody del Player
    private Rigidbody rb;
    // Guarda la direccion en la cual se esta moviendo el Player.
    private Vector3 movement;


    void Start()
    {
        // Busca el componente Rigidbody que est� en el mismo objeto y este lo guarda en rb.
        rb = GetComponent<Rigidbody>();

        // Sin friccion contra las paredes: al caminar pegado a una pared el roce frenaba al jugador.
        // (La velocidad la pone este script, asi que en el piso no hace falta friccion para no resbalar.)
        var sinFriccion = new PhysicsMaterial("Jugador sin friccion")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        foreach (Collider c in GetComponents<Collider>())
            if (!c.isTrigger) c.material = sinFriccion;

    }

    void Update()
    {
        // Movimiento horizontal 
        float x = Input.GetAxisRaw("Horizontal");
        // Movimiento Vertical
        float z = Input.GetAxisRaw("Vertical");

       //Movimiento horizontal 
        movement = (transform.right * x + transform.forward * z).normalized;
        adelante = z;
    }
    void FixedUpdate()
    {
        // Multiplicala la direccion del jugador con la velocidad base que se tiene.
        // Correr: Shift mantenido y avanzando hacia adelante. Sube y baja la velocidad con suavidad.
        Corriendo = GameInput.Run && adelante > 0.1f && !Agotado;
        ActualizarEstamina(Time.fixedDeltaTime);
        float objetivo = Corriendo ? multiplicadorCorrer : 1f;
        float paso = aceleracionCorrer > 0f ? Time.fixedDeltaTime / aceleracionCorrer * (multiplicadorCorrer - 1f) : 10f;
        factorCorrer = Mathf.MoveTowards(factorCorrer, objetivo, paso);
        Vector3 velocity = movement * speed * factorCorrer;

        // Modifica la velocidad del Rididbody
        rb.linearVelocity = new Vector3(
        velocity.x,
        rb.linearVelocity.y,
        velocity.z
    );
    }

    // Correr gasta estamina; al soltar Shift (tras una pausa corta) se recupera.
    // Si se vacia, queda "agotado" y no puede correr hasta recuperar un minimo.
    void ActualizarEstamina(float dt)
    {
        if (Corriendo)
        {
            ultimoCorrer = Time.time;
            Estamina = Mathf.Max(0f, Estamina - dt / Mathf.Max(0.1f, segundosCorriendo));
            if (Estamina <= 0f) { Agotado = true; Corriendo = false; }
        }
        else if (Time.time - ultimoCorrer >= esperaRecuperar)
        {
            Estamina = Mathf.Min(1f, Estamina + dt / Mathf.Max(0.1f, segundosRecuperar));
        }
        if (Agotado && Estamina >= minimoTrasAgotarse) Agotado = false;
    }

    // Llena la estamina (ej: al reaparecer tras morir)
    public void RellenarEstamina()
    {
        Estamina = 1f;
        Agotado = false;
    }

    //esto es de prueba :v

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
            Debug.Log("Enemigo entra al sphere collider");
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
            Debug.Log("Enemigo sale del sphere collider");
    }
}
