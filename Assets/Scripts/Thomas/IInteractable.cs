// Contrato que cumple cualquier objeto con el que el jugador pueda interactuar
// (puertas, llaves, cajones, palancas, el muñeco, etc.)
public interface IInteractable
{
    // Texto que se muestra al apuntar al objeto (ej: "Abrir puerta")
    string Prompt { get; }

    // Se llama cuando el jugador presiona la tecla de interaccion mirando al objeto
    void Interact(PlayerInteractor interactor);
}
