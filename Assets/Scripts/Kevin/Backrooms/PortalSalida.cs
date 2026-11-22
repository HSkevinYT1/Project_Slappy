using UnityEngine;
using UnityEngine.Rendering.Universal;

// Portal que aparece detras del jugador al presionar el boton del tablero de los Backrooms (lo crea PuzzlePalancas).
// Es un portal "de verdad": una puerta rectangular con marco brillante por la que se ve, en vivo, lo que hay del
// otro lado (la salida de la zona). Se cruza caminando: el jugador aparece en la salida con el mismo paso y la misma
// mirada, sin cortes, y la zona queda completada (ProgresoCaminos).
[DefaultExecutionOrder(10000)]   // despues de que la camara del jugador se mueva (asi la vista del portal no tiembla)
public class PortalSalida : MonoBehaviour
{
    public Transform destino;
    public string zona = "Backrooms";
    public Material material;
    public float ancho = 1.3f, alto = 2.3f;

    Camera camJugador, camPortal;
    RenderTexture rt;
    Material vista;
    Transform jugador;
    float ladoAntes;
    bool usado;
    float tAparecer;

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

    // El frente del portal (su eje azul) mira al jugador; se cruza hacia atras de el
    Quaternion Entrada => transform.rotation * Quaternion.Euler(0f, 180f, 0f);

    void Armar()
    {
        // Superficie: la vista del otro lado
        var sup = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sup.name = "Superficie";
        Destroy(sup.GetComponent<Collider>());
        sup.layer = 2;
        sup.transform.SetParent(transform, false);
        sup.transform.localPosition = new Vector3(0f, alto * 0.5f, 0f);
        sup.transform.localScale = new Vector3(ancho, alto, 1f);
        Shader s = Shader.Find("Slappy/PortalVista");
        vista = new Material(s != null ? s : Shader.Find("Universal Render Pipeline/Unlit"));
        var r = sup.GetComponent<Renderer>();
        r.sharedMaterial = vista;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Marco cuadrado
        const float g = 0.08f, f = 0.14f;
        Barra("Arriba", new Vector3(0f, alto + g * 0.5f, 0f), new Vector3(ancho + g * 2f, g, f));
        Barra("Abajo", new Vector3(0f, g * 0.5f - 0.02f, 0f), new Vector3(ancho + g * 2f, g, f));
        Barra("Izquierda", new Vector3(-(ancho + g) * 0.5f, alto * 0.5f, 0f), new Vector3(g, alto + g, f));
        Barra("Derecha", new Vector3((ancho + g) * 0.5f, alto * 0.5f, 0f), new Vector3(g, alto + g, f));

        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.transform.SetParent(transform, false);
        luz.transform.localPosition = new Vector3(0f, alto * 0.55f, 0.5f);
        luz.type = LightType.Point;
        luz.color = new Color(0.45f, 0.8f, 1f);
        luz.range = 4.5f;
        luz.intensity = 1.6f;
        luz.shadows = LightShadows.None;

        // La camara del otro lado
        var cg = new GameObject("Camara del portal");
        camPortal = cg.AddComponent<Camera>();
        camPortal.enabled = false;
        var datos = camPortal.GetUniversalAdditionalCameraData();
        datos.renderShadows = false;
        datos.renderPostProcessing = false;

        transform.localScale = new Vector3(1f, 0.02f, 1f);
    }

    void Barra(string nombre, Vector3 pos, Vector3 tam)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = nombre;
        b.layer = 2;
        Destroy(b.GetComponent<Collider>());
        b.transform.SetParent(transform, false);
        b.transform.localPosition = pos;
        b.transform.localScale = tam;
        if (material != null) b.GetComponent<Renderer>().sharedMaterial = material;
    }

    void OnDestroy()
    {
        if (camPortal != null) Destroy(camPortal.gameObject);
        if (rt != null) { rt.Release(); Destroy(rt); }
        if (vista != null) Destroy(vista);
    }

    void Update()
    {
        // Se abre: crece desde el piso
        if (tAparecer < 1f)
        {
            tAparecer = Mathf.Min(1f, tAparecer + Time.deltaTime / 0.7f);
            transform.localScale = new Vector3(1f, Mathf.Max(0.02f, Jugo.SalidaAtras(tAparecer, 1.4f)), 1f);
            vista.SetFloat("_Aparecer", tAparecer);
        }

        if (usado || destino == null) return;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }
        // Cruzo el plano de la puerta (de adelante hacia atras) dentro del marco
        Vector3 local = Quaternion.Inverse(transform.rotation) * (jugador.position - transform.position);
        float lado = local.z;
        bool enMarco = Mathf.Abs(local.x) < ancho * 0.5f + 0.25f && local.y > -1f && local.y < alto;
        if (ladoAntes > 0f && lado <= 0f && enMarco && tAparecer >= 1f) Cruzar();
        ladoAntes = lado;
    }

    void LateUpdate()
    {
        if (camJugador == null) camJugador = Camera.main;
        if (camJugador == null || camPortal == null || destino == null) return;
        // Solo se dibuja si el jugador esta cerca y mirando hacia el portal
        Vector3 haciaPortal = transform.position + Vector3.up * alto * 0.5f - camJugador.transform.position;
        bool mirando = haciaPortal.sqrMagnitude < 30f * 30f && Vector3.Dot(camJugador.transform.forward, haciaPortal) > -0.3f;
        camPortal.enabled = mirando && !usado;
        if (!camPortal.enabled) return;

        int w = Mathf.Max(64, camJugador.pixelWidth / 2), h = Mathf.Max(64, camJugador.pixelHeight / 2);
        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24) { name = "Vista del portal" };
            camPortal.targetTexture = rt;
            vista.SetTexture("_MainTex", rt);
        }

        // La camara del otro lado: donde estaria la del jugador respecto a la salida
        Quaternion delta = destino.rotation * Quaternion.Inverse(Entrada);
        camPortal.transform.SetPositionAndRotation(
            destino.position + delta * (camJugador.transform.position - transform.position),
            delta * camJugador.transform.rotation);
        camPortal.fieldOfView = camJugador.fieldOfView;
        camPortal.aspect = camJugador.aspect;
        camPortal.nearClipPlane = camJugador.nearClipPlane;
        camPortal.farClipPlane = camJugador.farClipPlane;
        camPortal.cullingMask = camJugador.cullingMask;
        camPortal.ResetProjectionMatrix();

        // Plano de corte en la salida: no se ve lo que queda detras de ella
        Vector3 n = destino.forward;
        Vector3 pc = camPortal.worldToCameraMatrix.MultiplyPoint(destino.position);
        Vector3 nc = camPortal.worldToCameraMatrix.MultiplyVector(n) * Mathf.Sign(Vector3.Dot(n, destino.position - camPortal.transform.position));
        float dst = -Vector3.Dot(pc, nc) + 0.05f;
        if (Mathf.Abs(dst) > 0.2f) camPortal.projectionMatrix = camPortal.CalculateObliqueMatrix(new Vector4(nc.x, nc.y, nc.z, dst));
    }

    // Pasa al otro lado con la misma posicion relativa, mirada y velocidad
    void Cruzar()
    {
        usado = true;
        Quaternion delta = destino.rotation * Quaternion.Inverse(Entrada);
        Vector3 pos = destino.position + delta * (jugador.position - transform.position);
        Vector3 adelante = delta * jugador.forward; adelante.y = 0f;
        Quaternion rot = adelante.sqrMagnitude > 0.001f ? Quaternion.LookRotation(adelante, Vector3.up) : destino.rotation;

        var miradas = jugador.GetComponentsInChildren<MouseLook>();
        foreach (MouseLook m in miradas) m.enabled = false;
        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        Vector3 vel = rb != null && !rb.isKinematic ? delta * rb.linearVelocity : Vector3.zero;
        jugador.SetPositionAndRotation(pos, rot);
        if (rb != null)
        {
            rb.position = pos;
            rb.rotation = rot;
            if (!rb.isKinematic) rb.linearVelocity = vel;
        }
        Physics.SyncTransforms();
        foreach (MouseLook m in miradas) m.enabled = true;   // toma el giro nuevo del cuerpo
        if (PantallaMuerte.Instance != null) PantallaMuerte.Instance.Guardar();

        ProgresoCaminos p = ProgresoCaminos.Instance;
        if (p != null && p.TramoActual != null && p.TramoActual.nombre == zona) p.CompletarZonaActual();
        Destroy(gameObject);
    }
}
