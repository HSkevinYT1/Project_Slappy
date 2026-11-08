using UnityEngine;

// Pared invisible que bloquea un paso (ej: la escalera al sotano). Abrir() / Cerrar() desde un UnityEvent.
// Con "Zona Cerrar" asignada se cierra sola cuando el jugador entra en esa zona (ej: ya abajo, en el sotano),
// asi no se cierra encima de el.
[RequireComponent(typeof(BoxCollider))]
public class Barrera : MonoBehaviour
{
    public bool cerradaAlIniciar = true;
    [Tooltip("Opcional: cuando el jugador entra en esta caja, la barrera se cierra.")]
    public BoxCollider zonaCerrar;

    public bool Cerrada { get; private set; }

    private BoxCollider pared;
    private Transform jugador;

    void Awake()
    {
        pared = GetComponent<BoxCollider>();
        pared.isTrigger = false;
        if (zonaCerrar != null) zonaCerrar.isTrigger = true;
        SetCerrada(cerradaAlIniciar);
    }

    public void Abrir() => SetCerrada(false);
    public void Cerrar() => SetCerrada(true);

    public void SetCerrada(bool cerrada)
    {
        Cerrada = cerrada;
        if (pared == null) pared = GetComponent<BoxCollider>();
        pared.enabled = cerrada;
    }

    void Update()
    {
        if (Cerrada || zonaCerrar == null) return;
        if (jugador == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm == null) return;
            jugador = pm.transform;
        }
        if (zonaCerrar.bounds.Contains(jugador.position)) Cerrar();
    }

    void OnDrawGizmos()
    {
        BoxCollider b = GetComponent<BoxCollider>();
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawCube(b.center, b.size);
        if (zonaCerrar != null)
        {
            Gizmos.matrix = zonaCerrar.transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(zonaCerrar.center, zonaCerrar.size);
        }
    }
}
