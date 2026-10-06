using UnityEngine;

// Clase encargada del movimiento de la camara entre pasos.
public class HeadBob : MonoBehaviour
{
    // El intevalo de tiempo en el que se realiza la animación.
    public float stepInterval = 0.45f;
    // La cantidad de distancia que baja la camara.
    public float bobHeight = 0.10f;
    // Velocidad a la que la camara vuelve a la posición original.
    public float returnSpeed = 8f;

    // Guarda la posición original de la camara.
    private Vector3 startPosition;
    // Guarda una referecia del Player y para comprobar si se esta moviendo.
    private Rigidbody playerRb;
    // Contador que registra el tiempo que ha pasado en el paso.
    private float stepTimer = 0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Guarda la posición origina de la camara.
        startPosition = transform.localPosition;

        // Busca el Rigidbody en el objeto padre.
        playerRb = GetComponentInParent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        // Crea un Vector3 con la velocidad horizontal del jugador.
        // Pone la Y en valor 0 para que la gravedad no afecte.
        Vector3 horizontalVelocity = new Vector3(
            playerRb.linearVelocity.x,
            0f,
            playerRb.linearVelocity.z
        );

        // Comprueba si el jugador se está moviendo.
        bool isMoving = horizontalVelocity.magnitude > 0.1f;

        if (isMoving)
        {
            // Aumenta el contador usando el tiempo real transcurrido.
            stepTimer += Time.deltaTime;

            // Cuando el contador alcanza la duración de un paso, vuelve a comenzar desde cero.
            if (stepTimer >= stepInterval)
            {
                stepTimer = 0f;
            }

            // Calcula qué tan avanzado está el paso actual, el intervalo es entre 0 a 1.
            float stepProgress = stepTimer / stepInterval;

            // Usa el movimiento de la onda del seno.
            float bob =
                Mathf.Sin(stepProgress * Mathf.PI) * bobHeight;

            // Mueve la cámara hacia abajo desde su posición inicial.
            transform.localPosition =
                startPosition + Vector3.down * bob;
        }
        else
        {
            // Reinicia el contador de pasos.
            stepTimer = 0f;

            // Devuelve suavemente la cámara a su posición original.
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                startPosition,
                Time.deltaTime * returnSpeed
            );
        }
    }
}
