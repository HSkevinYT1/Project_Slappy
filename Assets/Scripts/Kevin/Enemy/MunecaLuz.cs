using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Las muñecas de las Cloacas se deshacen con la luz: si la linterna (encendida, en la mano) la alumbra un rato,
// tiembla, se pone al rojo y estalla en pedazos. Si deja de alumbrarla se va recuperando poco a poco.
// Si llevaba la llave (LlaveAleatoria), la suelta en el piso.
// Lo agregan AparicionMunecas (a las que aparecen) y CloacasLuzSetup (a la muñeca de practica de la entrada).
public class MunecaLuz : MonoBehaviour
{
    [Tooltip("Segundos de luz seguidos para destruirla")]
    public float segundosParaDestruir = 2.5f;
    [Tooltip("Cuanto mas rapido se recupera que lo que se dana (0.5 = tarda el doble en recuperarse)")]
    public float recuperacion = 0.5f;
    [Tooltip("Metros maximos a los que la linterna le hace dano")]
    public float alcance = 18f;

    public static event System.Action<MunecaLuz> AlDestruirse;
    public bool Destruida { get; private set; }
    public float Dano { get; private set; }

    Renderer[] partes;
    Transform modelo;
    Vector3 modeloPos;
    MaterialPropertyBlock bloque;
    Collider[] propios;

    void Start()
    {
        partes = GetComponentsInChildren<Renderer>();
        propios = GetComponentsInChildren<Collider>();
        bloque = new MaterialPropertyBlock();
        // Lo que tiembla: el primer hijo con mallas (o la muñeca entera si no tiene)
        foreach (Transform t in transform)
            if (t.GetComponentInChildren<Renderer>() != null && t.GetComponent<LlaveAleatoria>() == null) { modelo = t; break; }
        if (modelo != null) modeloPos = modelo.localPosition;
    }

    void Update()
    {
        if (Destruida) return;
        bool alumbrada = Alumbrada();
        float antes = Dano;
        Dano = alumbrada ? Dano + Time.deltaTime / Mathf.Max(0.1f, segundosParaDestruir)
                         : Mathf.Max(0f, Dano - Time.deltaTime * recuperacion / Mathf.Max(0.1f, segundosParaDestruir));
        if (Dano > 0f || antes > 0f) Aspecto(Dano);
        if (Dano >= 1f) StartCoroutine(Destruirse());
    }

    bool Alumbrada()
    {
        Flashlight f = Flashlight.EnMano;
        if (f == null || !f.Encendida || PantallaMuerte.Muerto) return false;
        Light haz = f.Haz;
        if (haz == null) return false;
        Vector3 c = Centro();
        Vector3 d = c - haz.transform.position;
        float dist = d.magnitude;
        if (dist > Mathf.Min(alcance, haz.range) || dist < 0.01f) return false;
        float medio = (haz.type == LightType.Spot ? haz.spotAngle : 60f) * 0.5f;
        if (Vector3.Angle(haz.transform.forward, d) > medio * 0.85f) return false;
        // Que no haya una pared en medio (lo de la muñeca y el jugador no cuenta)
        Transform jugador = HUD.Jugador();
        foreach (RaycastHit h in Physics.RaycastAll(haz.transform.position, d / dist, dist - 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (jugador != null && h.collider.transform.IsChildOf(jugador.root)) continue;
            return false;
        }
        return true;
    }

    Vector3 Centro()
    {
        if (partes == null || partes.Length == 0) return transform.position;
        Bounds b = partes[0].bounds;
        foreach (Renderer r in partes) if (r != null && r.enabled) b.Encapsulate(r.bounds);
        return b.center;
    }

    // Tiembla cada vez mas y se pone al rojo
    void Aspecto(float k)
    {
        if (modelo != null)
            modelo.localPosition = modeloPos + (k > 0f ? Random.insideUnitSphere * 0.03f * k * k : Vector3.zero);
        foreach (Renderer r in partes)
        {
            if (r == null) continue;
            Material m = r.sharedMaterial;
            Color baseC = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            r.GetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", Color.Lerp(baseC, new Color(0.75f, 0.12f, 0.05f), k * 0.75f));
            bloque.SetColor("_EmissionColor", new Color(2.2f, 0.5f, 0.15f) * (k * k));
            r.SetPropertyBlock(bloque);
        }
    }

    IEnumerator Destruirse()
    {
        Destruida = true;
        if (TryGetComponent(out EstatuaAI ai)) ai.enabled = false;
        if (TryGetComponent(out NavMeshAgent ag) && ag.enabled && ag.isOnNavMesh) { ag.isStopped = true; ag.velocity = Vector3.zero; }
        foreach (Collider c in propios) if (c != null) c.enabled = false;

        // La llave cae al piso
        LlaveAleatoria llave = GetComponentInChildren<LlaveAleatoria>(true);
        if (llave != null) llave.Soltar();

        // Un ultimo temblor fuerte
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            if (modelo != null) modelo.localPosition = modeloPos + Random.insideUnitSphere * 0.06f;
            yield return null;
        }
        Vector3 c0 = Centro();
        Jugo.SacudirEn(c0, 0.3f, 12f);
        Pedazos(c0);
        AlDestruirse?.Invoke(this);
        Destroy(gameObject);
    }

    // Pedazos con fisica, oscuros y todavia al rojo, que se apagan y desaparecen
    void Pedazos(Vector3 centro)
    {
        Material mat = partes.Length > 0 && partes[0] != null ? partes[0].sharedMaterial : null;
        for (int i = 0; i < 14; i++)
        {
            var p = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
            p.name = "Pedazo de muñeca";
            p.layer = 2;
            p.transform.position = centro + Random.insideUnitSphere * 0.3f;
            p.transform.rotation = Random.rotation;
            p.transform.localScale = Vector3.one * Random.Range(0.04f, 0.11f);
            var r = p.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            var b = new MaterialPropertyBlock();
            b.SetColor("_EmissionColor", new Color(1.6f, 0.35f, 0.1f));
            r.SetPropertyBlock(b);
            var rb = p.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.linearVelocity = (Random.insideUnitSphere + Vector3.up * 0.8f) * Random.Range(1.5f, 4f);
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            Destroy(p, Random.Range(1.6f, 2.6f));
        }
    }
}
