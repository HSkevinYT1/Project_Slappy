using UnityEngine;

// Version ligera del NPC: solo detecta cuando el jugador esta cerca
// y le pide al DialogueManager que muestre el dialogo.
// (Mas adelante este script se reemplaza por un DialogueInteractable basado en IInteractable.)
// El nombre de la clase debe coincidir con el del archivo: Dialogo.cs
public class Dialogo : MonoBehaviour
{
    [SerializeField] private DialogueData dialogueData;
    [SerializeField] private GameObject marca_dialogo;

    [Header("Teclas")]
    // Tecla para iniciar la conversacion
    [SerializeField] private KeyCode startKey = KeyCode.E;

    private bool isPlayerInRange;

    void Update()
    {
        DialogueManager manager = DialogueManager.Instance;
        if (manager == null) return;

        // La marca solo se ve si el jugador esta cerca y no hay un dialogo abierto
        if (marca_dialogo != null)
            marca_dialogo.SetActive(isPlayerInRange && !manager.IsActive);

        if (isPlayerInRange && !manager.IsActive && Input.GetKeyDown(startKey))
            manager.StartDialogue(dialogueData);
    }

    // Entrando a la zona delimitada por el collider
    private void OnTriggerEnter(Collider collision)
    {
        if (collision.CompareTag("Player"))
            isPlayerInRange = true;
    }

    // Saliendo de la zona: si este NPC estaba hablando, se cierra su dialogo
    private void OnTriggerExit(Collider collision)
    {
        if (!collision.CompareTag("Player")) return;

        isPlayerInRange = false;

        DialogueManager manager = DialogueManager.Instance;
        if (manager != null && manager.IsActive && manager.CurrentData == dialogueData)
            manager.EndDialogue();
    }
}