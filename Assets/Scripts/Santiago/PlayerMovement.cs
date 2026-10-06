using UnityEngine;

// Clase encargada de controlar el movimiento del jugador
public class PlayerMovement : MonoBehaviour
{
    //Variable publica para cambiar la velocidad.
    public float speed = 5f;
    // Guarda una referencia al Rigidbody del Player
    private Rigidbody rb;
    // Guarda la dirección en la cual se esta moviendo el Player.
    private Vector3 movement;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Busca el componente Rigidbody que está en el mismo objeto y este lo guarda en rb.
        rb = GetComponent<Rigidbody>();

    }

    // Update is called once per frame
    void Update()
    {
        // Obtiene el movimento horizontal.
        float x = Input.GetAxisRaw("Horizontal");
        //Obtiene el movimiento Vertical
        float z = Input.GetAxisRaw("Vertical");

        // Calcula la dirección del movimiento tomando como referencia
        // hacia dónde está mirando actualmente el Player.
        // transform.right representa la derecha/izquierda del jugador.
        // transform.forward representa adelante/atrás del jugador.
        // normalized evita que el personaje se mueva más rápido
        // cuando se presionan dos teclas al mismo tiempo.
        movement = (transform.right * x + transform.forward * z).normalized;
    }
    void FixedUpdate()
    {
        // Multiplicala la dirección del jugador con la velocidad base que se tiene.
        Vector3 velocity = movement * speed;

        // Modifica la velocidad del Rididbody
        rb.linearVelocity = new Vector3(
        velocity.x,
        rb.linearVelocity.y,
        velocity.z
    );
    }
}
