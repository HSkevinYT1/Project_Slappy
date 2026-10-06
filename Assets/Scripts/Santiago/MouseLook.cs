using UnityEngine;

// Clase encargada del movimieno de la camara
public class MouseLook : MonoBehaviour
{
    // Controla que tan rapido se mueve la camra 
    public float mouseSensitivity = 100f;
    // Guarda la referencia del Transform del Player 
    // Aydua con el movimiento de izquierda a derecha
    public Transform playerBody;
    
    // Guarda la rotación vertial de la camara en grados 
    private float xRotation = 5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Bloquea el cursos en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        // Hace invisible el Mouse
        Cursor.visible = false;
    }

    // Update is called once per frame
    void Update()
    {
        // Obtiene el movimiento horizintal del mouse
        // Se multiplica la sensibilidad y el Time.Deltatime
        // Time.deltatime ayuda a compensar el tiempo entre frames
        float mouseX = Input.GetAxis("Mouse X")
           * mouseSensitivity * Time.deltaTime;

        // Obtiene el movimiento vertical del mouse
        float mouseY = Input.GetAxis("Mouse Y")
            * mouseSensitivity * Time.deltaTime;

        // Modifica la rotación vertical según el movimiento del mouse.
        // Se resta mouseY para que mover el mouse hacia arriba
        // haga que la cámara también mire hacia arriba
        xRotation -= mouseY;

        // Limitar el movimiento vertical del personaje haciendo que no deuna vuelta cuando mira hacia arriba o abajo
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        // Aplica la rotación vertical de la camara solo afecta a X
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Hace girar el cuerpo del Player con la camara de izquierda a derecha
        // Vector3.up representa el eje Y.
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
