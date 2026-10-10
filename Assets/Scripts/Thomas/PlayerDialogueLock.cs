using UnityEngine;

// Bloquea el movimiento y la camara del jugador mientras hay un dialogo abierto.
// Se coloca en el objeto raiz del Player (el que tiene el Rigidbody y PlayerMovement).
// Encuentra solo lo que necesita: no hay que asignar nada en el Inspector.
public class PlayerDialogueLock : MonoBehaviour
{
    private PlayerMovement movement;
    private Rigidbody rb;
    private DialogueManager manager;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();

        manager = DialogueManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("PlayerDialogueLock: no hay un DialogueManager en la escena.");
            return;
        }

        manager.OnDialogueStarted += Lock;
        manager.OnDialogueEnded += Unlock;
    }

    void OnDestroy()
    {
        if (manager == null) return;

        manager.OnDialogueStarted -= Lock;
        manager.OnDialogueEnded -= Unlock;
    }

    private void Lock()
    {
        if (movement != null) movement.enabled = false;
        if (manager.CurrentData != null && manager.CurrentData.modo == DialogueMode.Examinar)
        {
            return;
        }


        // Al desactivar PlayerMovement, el Rigidbody conservaria su ultima velocidad
        // y el jugador seguiria deslizandose: se frena en seco (se conserva la Y por la gravedad).
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void Unlock()
    {
        if (movement != null) movement.enabled = true;
    }
}
