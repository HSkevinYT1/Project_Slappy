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

    [Tooltip("Al completar una zona su puerta sigue abierta para poder volver; se cierra cuando el jugador ya la cruzo " +
             "hacia la Zona Principal y esta a estos metros de ella")]
    public float metrosParaCerrar = 2f;

    // Tramo recien completado cuya puerta sigue abierta hasta que el jugador sale (-1 = ninguno)
    int pendienteCerrar = -1;
    // Lado de la puerta (segun su eje azul) en el que estaba el jugador al completar la zona = lado de la zona
    float ladoZona;
    Transform jugador;

    void Awake() => Instance = this;

    void Start() => Aplicar(true);

    public void CompletarZonaActual() => IrAZona(zonaActual + 1);

    public void IrAZona(int indice)
    {
        int anterior = zonaActual;
        zonaActual = Mathf.Clamp(indice, 0, tramos.Length);

        // Avanzar: la puerta de la zona que se acaba de terminar se queda abierta hasta que el jugador salga
        pendienteCerrar = -1;
        if (zonaActual > anterior && anterior >= 0 && anterior < tramos.Length && tramos[anterior].puerta != null && Jugador() != null)
        {
            ladoZona = Lado(tramos[anterior].puerta.transform);
            if (Mathf.Abs(ladoZona) > 0.01f) pendienteCerrar = anterior;
        }

        Aplicar(false);
        alCambiarZona?.Invoke(zonaActual);
    }

    void Update()
    {
        if (pendienteCerrar < 0 || Jugador() == null) return;
        PuertaDeslizante puerta = tramos[pendienteCerrar].puerta;
        if (puerta == null) { pendienteCerrar = -1; return; }

        // Ya cruzo la puerta hacia el otro lado (la Zona Principal) y se alejo un poco: se cierra detras de el
        float lado = Lado(puerta.transform);
        if (Mathf.Sign(lado) != Mathf.Sign(ladoZona) && Mathf.Abs(lado) >= metrosParaCerrar)
        {
            pendienteCerrar = -1;
            puerta.Cerrar();
        }
    }

    // Distancia con signo del jugador al plano de la puerta (positivo = delante de su eje azul)
    float Lado(Transform puerta)
    {
        Vector3 d = jugador.position - puerta.position;
        return Vector3.Dot(d, puerta.forward);
    }

    Transform Jugador()
    {
        if (jugador == null) jugador = HUD.Jugador();
        return jugador;
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
            if (tramos[i].puerta != null) tramos[i].puerta.SetAbierta(activo || i == pendienteCerrar, instantaneo);
        }
    }
}
