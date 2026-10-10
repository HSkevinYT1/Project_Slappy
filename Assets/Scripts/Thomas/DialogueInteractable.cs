using UnityEngine;

// Se coloca en cualquier NPC u objeto con collider (no trigger) para que el jugador
// pueda hablarle o examinarlo mirandolo y presionando la tecla de interaccion (E).
public class DialogueInteractable : MonoBehaviour, IInteractable
{
    [Header("Contenido")]
    [SerializeField] private DialogueData dialogueData;

    [Header("Texto en pantalla al apuntar")]
    [Tooltip("Opcional. Si se deja vacio se usa 'Hablar' o 'Examinar' segun el modo del dialogo.")]
    [SerializeField] private string prompt = "";

    public string Prompt
    {
        get
        {
            if (!string.IsNullOrEmpty(prompt)) return prompt;

            bool examinar = dialogueData != null && dialogueData.modo == DialogueMode.Examinar;
            return examinar ? "Examinar" : "Hablar";
        }
    }

    void Start()
    {
        // Avisos utiles para detectar objetos mal configurados
        if (dialogueData == null)
            Debug.LogWarning(name + ": falta asignar un DialogueData.", this);

        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning(name + ": necesita un Collider (no trigger) para ser detectado.", this);
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("No hay un DialogueManager en la escena.");
            return;
        }

        DialogueManager.Instance.StartDialogue(dialogueData);
    }
}
