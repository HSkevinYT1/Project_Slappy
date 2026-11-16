using UnityEngine;

// Clase encargada de controlar el movimiento del jugador
public class PlayerMovement : MonoBehaviour
{
    //modificar velocidad
    public float speed = 5f;
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
    }
    void FixedUpdate()
    {
        // Multiplicala la direccion del jugador con la velocidad base que se tiene.
        Vector3 velocity = movement * speed;

        // Modifica la velocidad del Rididbody
        rb.linearVelocity = new Vector3(
        velocity.x,
        rb.linearVelocity.y,
        velocity.z
    );
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
