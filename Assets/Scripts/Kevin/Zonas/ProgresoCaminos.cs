using UnityEngine;
using UnityEngine.Events;

// Orden de las zonas desde la Zona Principal. Solo esta encendido el color del camino que toca
// y solo esta abierta su puerta; los demas caminos quedan sin color y con la puerta cerrada.
// Para avanzar: CompletarZonaActual() (ej: desde un Interactable o un UnityEvent al terminar una zona).
public class ProgresoCaminos : MonoBehaviour
{
    [System.Serializable]
    public class Tramo
    {
        public string nombre;
        public MarcaCamino marca;
        public PuertaDeslizante puerta;
    }

    public static ProgresoCaminos Instance { get; private set; }

    // En orden: el primero es la primera zona a la que hay que ir
    public Tramo[] tramos;
    // Zona a la que hay que ir ahora (0 = la primera). Igual o mayor que la cantidad = todas completadas
    public int zonaActual;

    // Recibe el numero de la nueva zona
    public UnityEvent<int> alCambiarZona;

    public Tramo TramoActual => zonaActual >= 0 && zonaActual < tramos.Length ? tramos[zonaActual] : null;

    void Awake() => Instance = this;

    void Start() => Aplicar(true);

    public void CompletarZonaActual() => IrAZona(zonaActual + 1);

    public void IrAZona(int indice)
    {
        zonaActual = Mathf.Clamp(indice, 0, tramos.Length);
        Aplicar(false);
        alCambiarZona?.Invoke(zonaActual);
    }

    // Por nombre (ej: "Cloacas"), para usarlo desde un UnityEvent
    public void IrAZona(string nombre)
    {
        for (int i = 0; i < tramos.Length; i++)
            if (tramos[i].nombre == nombre) { IrAZona(i); return; }
        Debug.LogWarning(name + ": no hay ningun tramo llamado " + nombre, this);
    }

    void Aplicar(bool instantaneo)
    {
        for (int i = 0; i < tramos.Length; i++)
        {
            bool activo = i == zonaActual;
            if (tramos[i].marca != null) tramos[i].marca.SetEncendida(activo);
            if (tramos[i].puerta != null) tramos[i].puerta.SetAbierta(activo, instantaneo);
        }
    }
}
