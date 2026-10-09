using UnityEngine;

public class OpenDoorTrigger : MonoBehaviour
{
    // Referencia a la puerta que se va a abrir.
    public OpenDoor door;

    // Evita que el Trigger se active varias veces.
    private bool activated = false;

    private void OnTriggerEnter(Collider other)
    {
        // Comprueba si quien entró fue el Player.
        if (other.CompareTag("Player") && activated == false)
        {
            // Abre la puerta.
            door.OpenTheDoor();

            // Guarda que el evento ya ocurrió.
            activated = true;
        }
    }
}