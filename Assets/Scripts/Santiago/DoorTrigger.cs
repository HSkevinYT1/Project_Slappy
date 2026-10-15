using UnityEngine;

// Define qué acción realizará el Trigger.
public enum DoorAction
{
    Open,
    Close
}


// Trigger genérico para abrir o cerrar puertas.
public class DoorTrigger : MonoBehaviour
{
    // Puerta que queremos controlar.
    public DoorController door;

    // Decide si este Trigger abre o cierra.
    public DoorAction action;

    // Evita activar el evento varias veces.
    private bool activated = false;


    private void OnTriggerEnter(Collider other)
    {
        // Solo funciona cuando entra el Player
        // y el evento todavía no ha ocurrido.
        if (other.CompareTag("Player") && activated == false)
        {
            if (action == DoorAction.Open)
            {
                door.OpenDoor();
            }

            if (action == DoorAction.Close)
            {
                door.CloseDoor();
            }

            activated = true;
        }
    }
}