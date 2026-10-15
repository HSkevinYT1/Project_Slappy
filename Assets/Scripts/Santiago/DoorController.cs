using UnityEngine;

// Controla la apertura y cierre de cualquier puerta.
public class DoorController : MonoBehaviour
{
    // Velocidad con la que gira la puerta.
    public float rotationSpeed = 120f;

    // Rotación de la puerta cuando está abierta.
    public Vector3 openRotation;

    // Rotación de la puerta cuando está cerrada.
    public Vector3 closedRotation;

    // Rotación hacia la que queremos mover la puerta.
    private Quaternion targetRotation;


    void Start()
    {
        // La puerta empieza en la rotación que tenga actualmente.
        targetRotation = transform.localRotation;
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


    // Ordena abrir la puerta.
    public void OpenDoor()
    {
        targetRotation = Quaternion.Euler(openRotation);
    }


    // Ordena cerrar la puerta.
    public void CloseDoor()
    {
        targetRotation = Quaternion.Euler(closedRotation);
    }
}