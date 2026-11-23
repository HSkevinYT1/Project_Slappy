using System.Collections.Generic;
using UnityEngine;

// Indicacion pintada en una pared (o el suelo) que solo se ve mirando a traves del caramelo (DulceVidente).
// En el editor se ve normal para poder colocarla; al jugar empieza oculta.
[RequireComponent(typeof(Renderer))]
public class MarcaOculta : MonoBehaviour
{
    private static readonly List<MarcaOculta> todas = new List<MarcaOculta>();
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static float visibilidad;

    // Cuanto se ve ahora lo oculto (0..1): la usa tambien VisionPiedra (el gato a traves de las paredes)
    public static float Visibilidad => visibilidad;

    [Tooltip("Opcional: la marca deja de verse cuando esta palanca ya esta bajada (las flechas que llevan hasta ella)")]
    public Palanca seApagaCon;

    private Renderer rend;
    private MaterialPropertyBlock bloque;
    private Color color = Color.white;
    private bool apagada;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        bloque = new MaterialPropertyBlock();
        if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(ColorId)) color = rend.sharedMaterial.GetColor(ColorId);
        Aplicar();
    }

    void OnEnable()
    {
        todas.Add(this);
        if (rend != null) Aplicar();
    }

    void OnDisable() => todas.Remove(this);

    // 0 = ocultas, 1 = se ven del todo. Lo llama DulceVidente mientras se mira a traves de el
    public static void SetVisibilidad(float v)
    {
        v = Mathf.Clamp01(v);
        if (Mathf.Approximately(v, visibilidad)) return;
        visibilidad = v;
        foreach (MarcaOculta m in todas) m.Aplicar();
    }

    void Update()
    {
        if (seApagaCon == null || seApagaCon.Bajada == apagada) return;
        apagada = seApagaCon.Bajada;
        Aplicar();
    }

    void Aplicar()
    {
        rend.enabled = visibilidad > 0.001f && !apagada;
        if (!rend.enabled) return;
        Color c = color;
        c.a *= visibilidad;
        rend.GetPropertyBlock(bloque);
        bloque.SetColor(ColorId, c);
        rend.SetPropertyBlock(bloque);
    }
}
