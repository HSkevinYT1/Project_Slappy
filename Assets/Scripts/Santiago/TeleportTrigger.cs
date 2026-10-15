using UnityEngine;

// Clase encargada de teletransportar al jugador
// hacia otro punto del mapa y activar el modo entidad del reloj.
public class TeleportTrigger : MonoBehaviour
{
    // Punto al que será enviado el jugador.
    public Transform teleportDestination;

    // Referencia al reloj del jugador.
    public ClockController clock;

    // Evita que el teletransporte ocurra varias veces.
    private bool activated = false;


    private void OnTriggerEnter(Collider other)
    {
        // Comprueba si quien entró es el Player
        // y si todavía no se ha realizado el teletransporte.
        if (other.CompareTag("Player") && activated == false)
        {
            // Busca el Rigidbody del jugador.
            Rigidbody playerRb = other.GetComponent<Rigidbody>();

            // Comprueba que realmente tenga Rigidbody.
            if (playerRb != null)
            {
                // Teletransporta al jugador.
                playerRb.position = teleportDestination.position;

                // Elimina la velocidad que llevaba antes del TP.
                playerRb.linearVelocity = Vector3.zero;

                // Activa el modo detector de entidad del reloj.
                clock.ActivateEntityMode();
            }

            // Guarda que el evento ya ocurrió.
            activated = true;
        }
    }
}