using UnityEngine;

// Puerta que se esconde en el piso al abrirse (o en el techo, segun "desplazamiento").
// Se controla con Abrir() / Cerrar() / SetAbierta(bool), por ejemplo desde ProgresoCaminos o un UnityEvent.
public class PuertaDeslizante : MonoBehaviour
{
    public bool abiertaAlIniciar;
    // Cuanto se mueve (en local) desde la posicion cerrada hasta la abierta
    public Vector3 desplazamiento = new Vector3(0f, -4.5f, 0f);
    // Segundos que tarda en abrirse o cerrarse
    public float duracion = 1.5f;
    public AudioClip sonido;

    public bool Abierta { get; private set; }

    private Vector3 posCerrada;
    private float valor;
    private bool iniciada;

    void Awake() => Iniciar();

    void Iniciar()
    {
        if (iniciada) return;
        iniciada = true;
        posCerrada = transform.localPosition;
        Abierta = abiertaAlIniciar;
        valor = Abierta ? 1f : 0f;
        Aplicar();
    }

    public void Abrir() => SetAbierta(true);
    public void Cerrar() => SetAbierta(false);

    public void SetAbierta(bool abierta, bool instantaneo = false)
    {
        Iniciar();
        if (Abierta != abierta && !instantaneo && sonido != null)
            AudioSource.PlayClipAtPoint(sonido, transform.position);
        Abierta = abierta;
        if (instantaneo) { valor = abierta ? 1f : 0f; Aplicar(); }
    }

    void Update()
    {
        float objetivo = Abierta ? 1f : 0f;
        if (Mathf.Approximately(valor, objetivo)) return;
        valor = Mathf.MoveTowards(valor, objetivo, Time.deltaTime / Mathf.Max(0.01f, duracion));
        Aplicar();
    }

    void Aplicar()
    {
        transform.localPosition = posCerrada + desplazamiento * Mathf.SmoothStep(0f, 1f, valor);
    }
}
