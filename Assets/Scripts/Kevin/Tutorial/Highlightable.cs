using System.Collections.Generic;
using UnityEngine;

// Enciende y apaga el contorno que se ve a traves de las paredes (GlowMaterial).
// Se coloca en el objeto que el tutorial pide buscar. No hay que tocar sus materiales:
// el script añade y quita el contorno como material extra de sus MeshRenderers.
// Desde otro script: GetComponent<Highlightable>().Show() / Hide() / SetHighlighted(bool)
// Desde un UnityEvent (boton, trigger, timeline): elegir Highlightable > Show o Hide.
public class Highlightable : MonoBehaviour
{
    [Tooltip("Material del contorno (GlowMaterial)")]
    [SerializeField] private Material outlineMaterial;

    [Tooltip("Si se marca, el objeto empieza la escena resaltado")]
    [SerializeField] private bool startHighlighted = false;

    public bool IsHighlighted { get; private set; }

    private Renderer[] renderers;

    // El material del contorno (para ponerlo desde otro script, ej. MisionFaro con el de la luz del faro)
    public Material Contorno
    {
        get => outlineMaterial;
        set { bool estaba = IsHighlighted; if (estaba) Hide(); outlineMaterial = value; if (estaba) Show(); }
    }

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Start()
    {
        // (el aviso va aqui y no en Awake: si se agrega por codigo, el material se pone justo despues)
        if (outlineMaterial == null)
            Debug.LogWarning(name + ": falta asignar el material del contorno.", this);
        if (startHighlighted) Show();
    }

    public void Show() => SetHighlighted(true);
    public void Hide() => SetHighlighted(false);

    public void SetHighlighted(bool on)
    {
        if (outlineMaterial == null || on == IsHighlighted) return;

        foreach (Renderer r in renderers)
        {
            // Se usan sharedMaterials para no crear copias de los materiales del objeto
            List<Material> mats = new List<Material>(r.sharedMaterials);

            if (on) mats.Add(outlineMaterial);
            else mats.Remove(outlineMaterial);

            r.sharedMaterials = mats.ToArray();
        }

        IsHighlighted = on;
    }
}
