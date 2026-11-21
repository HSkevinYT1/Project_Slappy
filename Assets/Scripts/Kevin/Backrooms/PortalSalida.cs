using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Portal que aparece detras del jugador al presionar el boton del tablero de los Backrooms (lo crea PuzzlePalancas).
// Crece girando y, al cruzarlo, una luz blanca tapa todo, el jugador aparece en la salida de la zona y la zona
// queda completada (ProgresoCaminos).
public class PortalSalida : MonoBehaviour
{
    public Transform destino;
    public string zona = "Backrooms";
    public Material material;

    Transform anillo, nucleo;
    Light luz;
    bool usado;

    public static PortalSalida Crear(Vector3 pos, Quaternion rot, Transform destino, string zona, Material material)
    {
        var go = new GameObject("Portal");
        go.transform.SetPositionAndRotation(pos, rot);
        go.layer = 2;   // Ignore Raycast: no estorba al apuntar
        var p = go.AddComponent<PortalSalida>();
        p.destino = destino;
        p.zona = zona;
        p.material = material;
        p.Armar();
        return p;
    }

    void Armar()
    {
        // Disco de luz y un anillo de esferas que gira alrededor
        nucleo = Pieza(PrimitiveType.Cylinder, "Nucleo", new Vector3(0f, 1.15f, 0f), new Vector3(1.5f, 0.01f, 2.1f)).transform;
        nucleo.localRotation = Quaternion.Euler(90f, 0f, 0f);
        anillo = new GameObject("Anillo").transform;
        anillo.SetParent(transform, false);
        anillo.localPosition = new Vector3(0f, 1.15f, 0f);
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            var s = Pieza(PrimitiveType.Sphere, "Chispa " + i, Vector3.zero, Vector3.one * 0.11f);
            s.transform.SetParent(anillo, false);
            s.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.82f, Mathf.Sin(a) * 1.12f, 0f);
        }
        luz = new GameObject("Luz").AddComponent<Light>();
        luz.transform.SetParent(transform, false);
        luz.transform.localPosition = new Vector3(0f, 1.15f, 0.4f);
        luz.type = LightType.Point;
        luz.color = new Color(0.55f, 0.4f, 1f);
        luz.range = 6f;
        luz.intensity = 0f;

        var col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 1.1f, 0f);
        col.size = new Vector3(1.5f, 2.2f, 0.8f);
        StartCoroutine(Aparecer());
    }

    GameObject Pieza(PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala)
    {
        var g = GameObject.CreatePrimitive(tipo);
        g.name = nombre;
        g.layer = 2;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(transform, false);
        g.transform.localPosition = pos;
        g.transform.localScale = escala;
        var r = g.GetComponent<Renderer>();
        if (material != null) r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    IEnumerator Aparecer()
    {
        Jugo.Sacudir(0.25f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            float k = Jugo.SalidaAtras(t / 0.6f, 1.6f);
            transform.localScale = new Vector3(k, k, 1f);
            luz.intensity = 3f * Mathf.Clamp01(t / 0.6f);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        if (anillo != null) anillo.localRotation = Quaternion.Euler(0f, 0f, Time.time * 90f);
        if (nucleo != null)
        {
            float pulso = 1f + Mathf.Sin(Time.time * 4f) * 0.03f;
            nucleo.localScale = new Vector3(1.5f * pulso, 0.01f, 2.1f * pulso);
        }
        if (luz != null && !usado) luz.intensity = 3f + Mathf.Sin(Time.time * 3f) * 0.6f;
    }

    void OnTriggerEnter(Collider other)
    {
        if (usado || other.GetComponentInParent<PlayerMovement>() == null) return;
        usado = true;
        StartCoroutine(Cruzar(other.GetComponentInParent<PlayerMovement>().transform));
    }

    IEnumerator Cruzar(Transform jugador)
    {
        // Luz que tapa todo
        Canvas canvas = HUD.CrearCanvas("Portal Destello", 130);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        Image blanco = HUD.Imagen(HUD.Llenar(canvas.transform, "Destello"), new Color(0.85f, 0.8f, 1f, 0f));
        for (float t = 0f; t < 0.35f; t += Time.deltaTime) { blanco.color = new Color(0.85f, 0.8f, 1f, t / 0.35f); yield return null; }
        blanco.color = new Color(0.85f, 0.8f, 1f, 1f);

        // A la salida de la zona (MouseLook se apaga un momento para tomar el giro nuevo)
        MouseLook[] miradas = jugador.GetComponentsInChildren<MouseLook>();
        foreach (MouseLook m in miradas) m.enabled = false;
        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        if (destino != null)
        {
            Quaternion giro = Quaternion.Euler(0f, destino.eulerAngles.y, 0f);
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.position = destino.position;
                rb.rotation = giro;
            }
            jugador.SetPositionAndRotation(destino.position, giro);
            Physics.SyncTransforms();
        }
        yield return new WaitForFixedUpdate();
        foreach (MouseLook m in miradas) if (m != null) m.enabled = true;

        // La zona queda completada (la nueva se abre cuando vuelva a la estacion)
        ProgresoCaminos p = ProgresoCaminos.Instance;
        if (p != null && p.TramoActual != null && p.TramoActual.nombre == zona) p.CompletarZonaActual();

        yield return new WaitForSeconds(0.25f);
        for (float t = 0f; t < 0.7f; t += Time.deltaTime) { blanco.color = new Color(0.85f, 0.8f, 1f, 1f - t / 0.7f); yield return null; }
        Destroy(canvas.gameObject);
        Destroy(gameObject);
    }
}
