using UnityEngine;

// Detecta cuando el jugador entra y ordena cerrar la puerta.
public class DoorCloseTrigger : MonoBehaviour
{
    // Referencia a la puerta que queremos controlar.
    public DoorController door;

    // Guarda si este evento ya ocurrió.
    private bool activated = false;


    private void OnTriggerEnter(Collider other)
    {
        // Comprueba que quien entró sea el Player
        // y que este evento todavía no haya ocurrido.
        if (other.CompareTag("Player") && activated == false)
        {
            // Ordena cerrar la puerta.
            door.CloseDoor();

            // Evita que este Trigger vuelva a ejecutarse.
            activated = true;
        }
    }
}