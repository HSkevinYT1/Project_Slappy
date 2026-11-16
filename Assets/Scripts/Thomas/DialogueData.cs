using UnityEngine;

// Forma en que se presenta un dialogo (los dos se muestran como subtitulos estilo GTA V y no bloquean al jugador)
public enum DialogueMode
{
    // Un personaje que habla: cada linea lleva su nombre en color ("Cliente: ...").
    Conversacion,
    // Lore o pensamientos al examinar algo: sin nombre, en gris claro.
    Examinar
}

// Contenido de un dialogo guardado como asset en la carpeta del proyecto
// (no vive en la escena). Se crea desde: clic derecho > Create > Juego > Dialogo
[CreateAssetMenu(fileName = "NuevoDialogo", menuName = "Juego/Dialogo")]
public class DialogueData : ScriptableObject
{
    public DialogueMode modo = DialogueMode.Conversacion;

    [Tooltip("Nombre de quien habla (se muestra en color antes de cada linea). Vacio = sin nombre.\n" +
             "Una linea puede cambiar de hablante empezando con [Nombre], ej: \"[Cultista] Hola\".")]
    public string hablante = "";

    [Tooltip("Color del nombre (como en GTA: cada personaje tiene el suyo)")]
    public Color colorHablante = new Color(0.4f, 0.75f, 1f);

    // Cada elemento es una linea (un subtitulo). Pasan solas segun lo largas que sean.
    [TextArea(3, 8)]
    public string[] lineas;

    [Tooltip("Segundos extra que se suman a cada linea (0 = solo lo que tarda en leerse)")]
    [Min(0f)]
    public float segundosExtra = 0f;

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
