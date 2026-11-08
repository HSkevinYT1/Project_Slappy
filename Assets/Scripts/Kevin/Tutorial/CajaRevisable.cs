using UnityEngine;

// Una de las cajas abiertas del sotano. Se usa con su Interactable (On Interact > CajaRevisable > Revisar).
// Lo que pasa al revisarla lo decide RevisionCajas: sacar el objeto, mirarlo y dejarlo, o (si es la ultima)
// encontrar la linterna.
public class CajaRevisable : MonoBehaviour
{
    // Parte visible de la caja (lleva el Highlightable para marcarla)
    public Highlightable marca;
    // Objeto que hay dentro y que el jugador saca para mirarlo
    public Transform objeto;
    // Punto dentro de la caja de donde sale la linterna (vacio = centro de la caja)
    public Transform fondo;

    public RevisionCajas revision;
    public bool Revisada { get; set; }

    // Se elige en el On Interact del Interactable
    public void Revisar(PlayerInteractor interactor)
    {
        if (revision == null) revision = GetComponentInParent<RevisionCajas>();
        if (revision == null) revision = FindFirstObjectByType<RevisionCajas>();
        if (revision != null) revision.Revisar(this, interactor);
    }

    public Vector3 PuntoFondo
    {
        get
        {
            if (fondo != null) return fondo.position;
            Collider c = GetComponent<Collider>();
            return c != null ? c.bounds.center : transform.position;
        }
    }
}
