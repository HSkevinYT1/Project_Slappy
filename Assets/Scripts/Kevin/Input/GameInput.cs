using UnityEngine;
using UnityEngine.InputSystem;

// Unico lugar donde se leen los controles del juego. Los botones no estan aqui, sino en
// Assets/InputSystem_Actions: el mapa "Juego" (teclado y mouse) y el mapa "JuegoVR" (mandos).
// Los dos estan separados pero siempre activos, asi que funcionan en cualquier escena.
public static class GameInput
{
    private const string PcMapName = "Juego";
    private const string VrMapName = "JuegoVR";
    private const string KeyboardGroup = "Keyboard&Mouse";

    private static InputActionMap pcMap;
    private static InputActionMap vrMap;

    // Botones: solo son true en el frame en que se presionan
    public static bool Interact => Pressed("Interactuar");
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
            return pcMap.FindAction("Arrastrar", true).WasReleasedThisFrame()
                || vrMap.FindAction("Arrastrar", true).WasReleasedThisFrame();
        }
    }

    public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    // Nombre de la tecla de interaccion para los textos en pantalla (ej: "F")
    public static string InteractKeyName
    {
        get
        {
            Load();
            return pcMap.FindAction("Interactuar", true).GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardGroup));
        }
    }

    // Sticks de los mandos ("Mover", "Girar")
    public static Vector2 ReadStick(string actionName)
    {
        Load();
        return vrMap.FindAction(actionName, true).ReadValue<Vector2>();
    }

    private static bool Pressed(string actionName)
    {
        Load();
        return pcMap.FindAction(actionName, true).WasPressedThisFrame()
            || vrMap.FindAction(actionName, true).WasPressedThisFrame();
    }

    private static void Load()
    {
        if (pcMap != null) return;

        pcMap = InputSystem.actions.FindActionMap(PcMapName, true);
        vrMap = InputSystem.actions.FindActionMap(VrMapName, true);
        pcMap.Enable();
        vrMap.Enable();
    }
}
