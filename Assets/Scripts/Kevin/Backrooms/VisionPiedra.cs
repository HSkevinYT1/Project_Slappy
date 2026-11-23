using System.Collections.Generic;
using UnityEngine;

// Mirando a traves de la piedra bruja (DulceVidente) este objeto (el gato de los Backrooms) se ve A TRAVES DE LAS
// PAREDES: un contorno brillante (shader Custom/SeeThroughOutline) que aparece y se apaga con la misma visibilidad que
// las marcas ocultas. Asi la piedra sirve para saber donde anda el gato... pero se gasta mientras se mira.
// Lo pone GatoSombraSetup en el prefab del gato.
public class VisionPiedra : MonoBehaviour
{
    [Tooltip("Material del contorno que se ve a traves de todo (Custom/SeeThroughOutline)")]
    public Material contorno;
    [ColorUsage(true, true)] public Color color = new Color(3.2f, 0.35f, 0.3f, 1f);
    [Tooltip("Que tan rapido aparece y se apaga")]
    public float rapidez = 5f;

    static readonly int GlowId = Shader.PropertyToID("_GlowColor");
    Renderer[] renders;
    MaterialPropertyBlock bloque;
    bool puesto;
    float nivel;

    void Awake()
    {
        renders = GetComponentsInChildren<Renderer>(true);
        bloque = new MaterialPropertyBlock();
    }

    void OnDisable()
    {
        nivel = 0f;
        Poner(false);
    }

    void LateUpdate()
    {
        if (contorno == null) return;
        nivel = Mathf.MoveTowards(nivel, MarcaOculta.Visibilidad, Time.deltaTime * rapidez);
        bool ver = nivel > 0.01f;
        if (ver != puesto) Poner(ver);
        if (!ver) return;
        Color c = color;
        c.a *= nivel;
        foreach (Renderer r in renders)
        {
            if (r == null) continue;
            r.GetPropertyBlock(bloque);
            bloque.SetColor(GlowId, c);
            r.SetPropertyBlock(bloque);
        }

        // La primera vez que lo ve asi (en pantalla), lo dice
        if (!dicho && nivel > 0.8f && EnPantalla())
        {
            dicho = true;
            Pensamiento.Decir("piedra.gato", 0.3f, alVerlo);
        }
    }

    [TextArea] public string alVerlo = "Lo veo... a través de las paredes.";
    bool dicho;

    bool EnPantalla()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 p = transform.position;
        if ((p - cam.transform.position).sqrMagnitude > 45f * 45f) return false;
        Vector3 vp = cam.WorldToViewportPoint(p);
        return vp.z > 0f && vp.x > 0.2f && vp.x < 0.8f && vp.y > 0.15f && vp.y < 0.85f;
    }

    // El contorno va como material extra de cada malla (sin tocar sus materiales)
    void Poner(bool on)
    {
        puesto = on;
        if (renders == null || contorno == null) return;
        foreach (Renderer r in renders)
        {
            if (r == null) continue;
            var mats = new List<Material>(r.sharedMaterials);
            bool tiene = mats.Contains(contorno);
            if (on && !tiene) mats.Add(contorno);
            else if (!on && tiene) mats.RemoveAll(m => m == contorno);
            else continue;
            r.sharedMaterials = mats.ToArray();
        }
    }
}
