using UnityEngine;

// Forma en que se presenta un dialogo
public enum DialogueMode
{
    // Varias lineas. El jugador avanza con la tecla de avanzar (Espacio). Bloquea al jugador.
    Conversacion,
    // Una o pocas lineas de lore. Pasan solas tras unos segundos. No bloquea al jugador.
    Examinar
}

// Contenido de un dialogo guardado como asset en la carpeta del proyecto
// (no vive en la escena). Se crea desde: clic derecho > Create > Juego > Dialogo
[CreateAssetMenu(fileName = "NuevoDialogo", menuName = "Juego/Dialogo")]
public class DialogueData : ScriptableObject
{
    public DialogueMode modo = DialogueMode.Conversacion;

    // Cada elemento es una linea que se muestra con el efecto de maquina de escribir
    [TextArea(3, 8)]
    public string[] lineas;

    [Tooltip("Solo en modo Examinar: segundos que se queda visible cada linea antes de pasar sola")]
    [Min(0.5f)]
    public float segundosPorLinea = 3f;

    // Se elige en el On Interact de un Interactable, arrastrando este asset al evento
    public void Play()
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("No hay un DialogueManager en la escena.");
            return;
        }

        DialogueManager.Instance.StartDialogue(this);
    }
}
