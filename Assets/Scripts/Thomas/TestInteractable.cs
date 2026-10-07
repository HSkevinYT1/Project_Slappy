using UnityEngine;

// Objeto de prueba: se pone en cualquier cubo con collider para comprobar
// que el sistema de interaccion funciona. Luego se reemplaza por puertas, llaves, etc.
public class TestInteractable : MonoBehaviour, IInteractable
{
    public string prompt = "Examinar";

    public string Prompt => prompt;

    public void Interact(PlayerInteractor interactor)
    {
        Debug.Log("Interactuaste con: " + name);
    }
}
