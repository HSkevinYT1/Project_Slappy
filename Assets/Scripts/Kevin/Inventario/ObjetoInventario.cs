using UnityEngine;

// Opcional: como se ve un objeto agarrable en el inventario. Sin este componente se usa el nombre del objeto
// y su inicial como icono (placeholder).
[RequireComponent(typeof(Grabbable))]
public class ObjetoInventario : MonoBehaviour
{
    [Tooltip("Nombre en la barra del inventario. Vacio = el nombre del objeto.")]
    public string nombre;
    [Tooltip("Icono. Vacio = un cuadro con la inicial (placeholder).")]
    public Sprite icono;
    [Tooltip("Texto del placeholder cuando no hay icono (ej: el numero del panel). Vacio = la inicial del nombre.")]
    public string inicial;
}
