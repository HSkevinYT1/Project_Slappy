using TMPro;
using UnityEngine;

// Panel con un numero que se recoge en las salas del laberinto y se coloca en el puzzle del centro.
// Va junto a Grabbable (Interactable > Grabbable.PickUp) y ObjetoInventario. El numero lo pone el PuzzlePaneles.
[RequireComponent(typeof(Grabbable))]
public class PanelNumero : MonoBehaviour
{
    public int numero = 1;
    [Tooltip("Textos 3D que muestran el numero (frente y dorso)")]
    public TextMeshPro[] textos;

    void Awake() => Aplicar();

    public void SetNumero(int n)
    {
        numero = n;
        Aplicar();
    }

    void Aplicar()
    {
        if (textos != null)
            foreach (TextMeshPro t in textos) if (t != null) t.text = numero.ToString();
        ObjetoInventario oi = GetComponent<ObjetoInventario>();
        if (oi != null)
        {
            oi.nombre = "Panel " + numero;
            oi.inicial = numero.ToString();
        }
    }
}
