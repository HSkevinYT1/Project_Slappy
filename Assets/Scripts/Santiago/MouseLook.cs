using UnityEngine;

// Clase encargada del movimieno de la camara
public class MouseLook : MonoBehaviour
{
    // sensibilidad de la camara
    public float mouseSensitivity = 100f;
    // referencia del Transform del Player 
    // Ayuda con el movimiento de izquierda a derecha
    public Transform playerBody;
    private Rigidbody playerRb;

    // Guarda la rotación vertial de la camara en grados 
    private float xRotation = 0f;

    void Start()
    {
        // Bloquea el cursos en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        // Hace invisible el Mouse
        Cursor.visible = false;
        playerRb = playerBody.GetComponent<Rigidbody>();

   //Esto es para que el personaje no empiece mirando al suelo
        Vector3 playerEuler = playerBody.eulerAngles;
        playerBody.rotation = Quaternion.Euler(0f, playerEuler.y, 0f);

        transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
    }

    void Update()
    {
        // Movimiento horizontal del mouse
        float mouseX = Input.GetAxis("Mouse X")
           * mouseSensitivity * Time.deltaTime;

        // Movimiento vertical del mouse
        float mouseY = Input.GetAxis("Mouse Y")
            * mouseSensitivity * Time.deltaTime;

       //Modifica la rotación vertical según el movimiento del mouse
        xRotation -= mouseY;

        // Limitar el movimiento vertical del personaje haciendo que no de una vuelta cuando mira hacia arriba o abajo
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        // Aplica la rotación vertical de la camara solo afecta a X
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        //te agregue esto porque tenias problemas en la fisica
        Quaternion deltaRotation = Quaternion.Euler(0f, mouseX, 0f);
        playerRb.MoveRotation(playerRb.rotation * deltaRotation);
    }
}
