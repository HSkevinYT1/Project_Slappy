using UnityEngine;

// Boton del panel central del puzzle de contrasena (va con un Interactable: On Interact > BotonContrasena.Pulsar).
public class BotonContrasena : MonoBehaviour
{
    public enum Accion { Subir, Bajar, Enviar }

    public PuzzleContrasena puzzle;
    public Accion accion;
    public int columna;

    public void Pulsar(PlayerInteractor interactor)
    {
        if (puzzle == null) return;
        puzzle.Hundir(transform);
        switch (accion)
        {
            case Accion.Subir: puzzle.Subir(columna); break;
            case Accion.Bajar: puzzle.Bajar(columna); break;
            default: puzzle.Enviar(); break;
        }
    }
}
