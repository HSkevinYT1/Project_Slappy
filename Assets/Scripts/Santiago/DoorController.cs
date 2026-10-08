using UnityEngine;

// Controla la apertura y cierre de la puerta.
public class DoorController : MonoBehaviour
{
    // Velocidad de rotación de la puerta.
    public float rotationSpeed = 200f;

    // Rotación que tendrá cuando esté cerrada.
    public Vector3 closedRotation = new Vector3(0f, 0f, 0f);

    // Guarda la rotación inicial abierta.
    private Quaternion openRotation;

    // Rotación hacia la que queremos mover la puerta.
    private Quaternion targetRotation;

    void Start()
    {
        // Guarda la posición inicial como posición abierta.
        openRotation = transform.localRotation;

        // Al comenzar, la puerta permanece abierta.
        targetRotation = openRotation;
    }

    void Update()
    {
        // Mueve suavemente la puerta hacia la rotación objetivo.
        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    // Cierra la puerta.
    public void CloseDoor()
    {
        targetRotation = Quaternion.Euler(closedRotation);
    }

    // Abre nuevamente la puerta.
    public void OpenDoor()
    {
        targetRotation = openRotation;
    }
}