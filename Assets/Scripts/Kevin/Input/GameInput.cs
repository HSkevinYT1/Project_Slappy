using UnityEngine;
using UnityEngine.InputSystem;

// Unico lugar donde se leen los controles del juego. Los botones no estan aqui, sino en
// Assets/InputSystem_Actions, en el mapa "Juego" (teclado y mouse), que siempre esta activo.
public static class GameInput
{
    private const string MapName = "Juego";
    private const string KeyboardGroup = "Keyboard&Mouse";

    private static InputActionMap map;

    // Botones: solo son true en el frame en que se presionan
    public static bool Interact => Pressed("Interactuar");
    // Soltar lo que se tiene en la mano (Z)
    public static bool Drop => Pressed("Soltar");
    public static bool ExitMinigame => Pressed("Salir");
    public static bool NextDialogue => Pressed("SiguienteDialogo");
    public static bool Flashlight => Pressed("Linterna");
    public static bool Pause => Pressed("Pausa");
    public static bool DragStarted => Pressed("Arrastrar");

    public static bool DragReleased
    {
        get
        {
            Load();
            return map.FindAction("Arrastrar", true).WasReleasedThisFrame();
        }
    }

    public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    // Nombre de la tecla de interaccion para los textos en pantalla (ej: "F")
    public static string InteractKeyName
    {
        get
        {
            Load();
            return map.FindAction("Interactuar", true).GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardGroup));
        }
    }

    private static bool Pressed(string actionName)
    {
        Load();
        return map.FindAction(actionName, true).WasPressedThisFrame();
    }

    private static void Load()
    {
        if (map != null) return;

        map = InputSystem.actions.FindActionMap(MapName, true);
        map.Enable();
    }
}
