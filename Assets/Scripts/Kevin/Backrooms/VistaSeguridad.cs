using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Vista de camara de seguridad a pantalla completa (la usa PuzzlePalancas): una camara aparte dibuja en una
// textura chica, que se ve con el filtro CCTV (shader Slappy/CamaraSeguridad) y encima los textos del monitor:
// nombre de la camara, "REC" que parpadea y la hora. Entra y sale con un glitch. Se crea sola.
public class VistaSeguridad : MonoBehaviour
{
    static VistaSeguridad instancia;
    public static VistaSeguridad Instancia
    {
        get
        {
            if (instancia == null) instancia = new GameObject("Vista Seguridad").AddComponent<VistaSeguridad>();
            return instancia;
        }
    }

    public bool Abierta { get; private set; }

    Camera cam;
    RenderTexture rt;
    Canvas canvas;
    CanvasGroup grupo;
    RawImage imagen;
    Material material;
    TextMeshProUGUI etiqueta, rec, hora;
    Image punto;
    Transform pose;
    Quaternion rotBase;
    float glitch, tAbierta;
    Coroutine rutina;

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
        if (rt != null) rt.Release();
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void Crear(Material filtro)
    {
        if (canvas != null) return;
        rt = new RenderTexture(640, 360, 24) { name = "Camara Seguridad", filterMode = FilterMode.Bilinear };
        var camGo = new GameObject("Camara Seguridad (render)");
        camGo.transform.SetParent(transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.targetTexture = rt;
        cam.fieldOfView = 64f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 80f;
        int capaBrazos = LayerMask.NameToLayer("Primera Persona");
        if (capaBrazos >= 0) cam.cullingMask &= ~(1 << capaBrazos);
        cam.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
        var datos = cam.GetUniversalAdditionalCameraData();
        datos.renderPostProcessing = true;
        datos.renderShadows = true;
        cam.enabled = false;

        canvas = HUD.CrearCanvas("HUD Camara Seguridad", 120);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        grupo = canvas.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;

        // Fondo negro (fuera de la pantalla curvada) y la imagen de la camara
        HUD.Imagen(HUD.Llenar(canvas.transform, "Negro"), Color.black);
        imagen = HUD.Llenar(canvas.transform, "Imagen").gameObject.AddComponent<RawImage>();
        imagen.texture = rt;
        imagen.raycastTarget = false;
        if (filtro != null) imagen.material = material = new Material(filtro);
        else imagen.color = new Color(0.75f, 0.95f, 0.8f);

        // Textos del monitor
        Color verde = new Color(0.85f, 1f, 0.88f, 0.92f);
        etiqueta = HUD.Texto(HUD.Rect(canvas.transform, "Etiqueta", new Vector2(0f, 1f), new Vector2(60f, -46f), new Vector2(900f, 50f)),
            "Texto", "CAM 04", 34f, TextAlignmentOptions.MidlineLeft, verde);
        hora = HUD.Texto(HUD.Rect(canvas.transform, "Hora", new Vector2(1f, 0f), new Vector2(-60f, 46f), new Vector2(600f, 50f)),
            "Texto", "", 30f, TextAlignmentOptions.MidlineRight, verde);
        RectTransform recR = HUD.Rect(canvas.transform, "Rec", new Vector2(1f, 1f), new Vector2(-60f, -46f), new Vector2(200f, 50f));
        rec = HUD.Texto(recR, "Texto", "REC", 32f, TextAlignmentOptions.MidlineRight, verde);
        punto = HUD.Imagen(HUD.Rect(recR, "Punto", new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(18f, 18f)), new Color(1f, 0.15f, 0.12f), HUD.Circulo());
        foreach (var t in new[] { etiqueta, hora, rec }) { t.fontStyle = FontStyles.Bold; t.characterSpacing = 6f; }
        canvas.enabled = false;
    }

    // Corta a la camara de seguridad (con glitch de entrada)
    public void Abrir(Transform poseCamara, string nombre, Material filtro)
    {
        Crear(filtro);
        pose = poseCamara;
        rotBase = poseCamara.rotation;
        cam.transform.SetPositionAndRotation(poseCamara.position, poseCamara.rotation);
        etiqueta.text = nombre;
        tAbierta = 0f;
        Abierta = true;
        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(Transicion(true));
    }

    public void Cerrar()
    {
        if (!Abierta) return;
        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(Transicion(false));
    }

    IEnumerator Transicion(bool abrir)
    {
        if (abrir)
        {
            cam.enabled = true;
            canvas.enabled = true;
            for (float t = 0f; t < 0.32f; t += Time.unscaledDeltaTime)
            {
                grupo.alpha = Mathf.Clamp01(t / 0.06f);
                glitch = 1f - Jugo.SalidaCubica(t / 0.32f);
                yield return null;
            }
            glitch = 0f;
            grupo.alpha = 1f;
        }
        else
        {
            for (float t = 0f; t < 0.26f; t += Time.unscaledDeltaTime)
            {
                glitch = Jugo.EntradaCubica(t / 0.26f);
                grupo.alpha = 1f - Mathf.Clamp01((t - 0.18f) / 0.08f);
                yield return null;
            }
            grupo.alpha = 0f;
            canvas.enabled = false;
            cam.enabled = false;
            Abierta = false;
        }
        rutina = null;
    }

    void Update()
    {
        if (!Abierta || cam == null) return;
        tAbierta += Time.unscaledDeltaTime;
        // La camara gira despacito, como las de vigilancia
        if (pose != null)
            cam.transform.SetPositionAndRotation(pose.position, rotBase * Quaternion.Euler(0f, Mathf.Sin(tAbierta * 0.5f) * 3f, 0f));
        if (material != null)
        {
            // Un tironcito de senal de vez en cuando
            float chispa = Mathf.PerlinNoise(Time.unscaledTime * 3f, 0.4f) > 0.82f ? 0.25f : 0f;
            material.SetFloat("_Glitch", Mathf.Max(glitch, chispa));
        }
        bool on = Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
        punto.enabled = on;
        rec.alpha = on ? 1f : 0.55f;
        System.DateTime ahora = System.DateTime.Now;
        hora.text = ahora.ToString("dd/MM/yyyy  HH:mm:ss");
    }
}
