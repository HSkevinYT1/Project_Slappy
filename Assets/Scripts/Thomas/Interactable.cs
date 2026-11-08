using UnityEngine;
using UnityEngine.Events;

// Unico script de interaccion. Se coloca en cualquier objeto con collider (no trigger) con el que
// el jugador pueda interactuar (puertas, llaves, cajones, palancas, el muñeco, etc.).
// En el Inspector se escribe el texto y en On Interact se elige que pasa al presionar la tecla:
// Grabbable > PickUp, un asset de dialogo > Play, PhoneMinigame > Open, una animacion...
// Desactivando el componente, el jugador deja de poder interactuar con el objeto.
public class Interactable : MonoBehaviour
{
    // Texto que se muestra al apuntar al objeto (ej: "Abrir puerta")
    [SerializeField] private string prompt = "Interactuar";

    // Recibe al jugador que interactuo, para las acciones que lo necesiten
    public UnityEvent<PlayerInteractor> onInteract;

    public string Prompt => prompt;

    // Para acciones que cambian de texto segun su estado (ej: el armario: "Esconderse" / "Salir")
    public void SetPrompt(string texto) => prompt = texto;

    void Start()
    {
        // Aviso util para detectar objetos mal configurados
        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning(name + ": necesita un Collider (no trigger) para ser detectado.", this);
    }

    // Se llama cuando el jugador presiona la tecla de interaccion mirando al objeto
    public void Interact(PlayerInteractor interactor)
    {
        onInteract.Invoke(interactor);
    }
}
