using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Los brazos y lo que se tiene en la mano ya no atraviesan paredes ni objetos:
// todo lo que cuelga de la camara del jugador se pasa a la capa "Primera Persona", que la camara principal
// NO dibuja; la dibuja una camara superpuesta (overlay, en el stack de URP) que limpia la profundidad,
// asi siempre queda por delante del mundo. Al soltar un objeto vuelve a su capa original.
// Post-procesado: en un stack de URP solo la ULTIMA camara debe aplicarlo (asi se aplica una vez a todo).
// Antes lo aplicaba la camara principal y los brazos se dibujaban despues, sin el Volume de la zona:
// en el laberinto (saturacion, tonos, vineta, bloom de las lianas) el brazo se veia "ignorando las luces".
// Ahora, mientras la camara de los brazos esta activa, el post-procesado lo hace ella sobre todo el stack.
// Va en la Main Camera del jugador.
[RequireComponent(typeof(Camera))]
public class BrazosPrimeraPersona : MonoBehaviour
{
    public string nombreCapa = "Primera Persona";
    [Tooltip("Plano cercano de la camara de los brazos (bien cerca para que no se corten las manos)")]
    public float planoCercano = 0.01f;

    Camera cam, camBrazos;
    UniversalAdditionalCameraData datosCam, datosBrazos;
    int capa = -1;
    bool postOriginal = true, postGuardado;

    // Si la camara del jugador tenia post-procesado antes de pasarselo a la de los brazos
    // (lo usan las cinematicas que copian la camara del jugador)
    public bool PostOriginal => postGuardado ? postOriginal : (datosCam != null && datosCam.renderPostProcessing);
    readonly Dictionary<GameObject, int> originales = new Dictionary<GameObject, int>();
    readonly List<GameObject> quitar = new List<GameObject>();
    FaintTeleport[] desmayos;

    void OnEnable()
    {
        capa = LayerMask.NameToLayer(nombreCapa);
        if (capa < 0) { Debug.LogWarning("BrazosPrimeraPersona: no existe la capa \"" + nombreCapa + "\".", this); enabled = false; return; }
        // En VR el stack de camaras no se usa (cada ojo tiene su camara)
        if (UnityEngine.XR.XRSettings.isDeviceActive) { enabled = false; return; }

        cam = GetComponent<Camera>();
        cam.cullingMask &= ~(1 << capa);
        datosCam = cam.GetUniversalAdditionalCameraData();
        if (!postGuardado) { postOriginal = datosCam.renderPostProcessing; postGuardado = true; }

        if (camBrazos == null)
        {
            var go = new GameObject("Camara Brazos");
            go.transform.SetParent(transform, false);
            camBrazos = go.AddComponent<Camera>();
            var datos = camBrazos.GetUniversalAdditionalCameraData();
            datos.renderType = CameraRenderType.Overlay;
            datos.renderShadows = false;
        }
        datosBrazos = camBrazos.GetUniversalAdditionalCameraData();
        // Mismo Volume que la camara principal (mismas capas y el mismo punto que activa los Volumes locales)
        datosBrazos.volumeLayerMask = datosCam.volumeLayerMask;
        datosBrazos.volumeTrigger = datosCam.volumeTrigger != null ? datosCam.volumeTrigger : transform;
        datosBrazos.antialiasing = datosCam.antialiasing;
        datosBrazos.antialiasingQuality = datosCam.antialiasingQuality;
        datosBrazos.dithering = datosCam.dithering;
        camBrazos.cullingMask = 1 << capa;
        camBrazos.nearClipPlane = planoCercano;
        camBrazos.farClipPlane = Mathf.Max(20f, cam.farClipPlane);
        var stack = cam.GetUniversalAdditionalCameraData().cameraStack;
        if (!stack.Contains(camBrazos)) stack.Add(camBrazos);
    }

    void OnDisable()
    {
        if (cam != null)
        {
            if (camBrazos != null) cam.GetUniversalAdditionalCameraData().cameraStack.Remove(camBrazos);
            if (capa >= 0) cam.cullingMask |= 1 << capa;
            if (datosCam != null && postGuardado) datosCam.renderPostProcessing = postOriginal;
        }
        foreach (var kv in originales) if (kv.Key != null) kv.Key.layer = kv.Value;
        originales.Clear();
    }

    void LateUpdate()
    {
        if (camBrazos == null) return;
        camBrazos.fieldOfView = cam.fieldOfView;
        // Durante el desmayo de la cabina el negro se dibuja con la camara principal: los brazos se ocultan
        // (si no, la camara de los brazos los pintaria encima del negro)
        if (desmayos == null) desmayos = FindObjectsByType<FaintTeleport>(FindObjectsSortMode.None);
        bool desmayado = false;
        foreach (FaintTeleport f in desmayos) if (f != null && f.IsRunning) desmayado = true;
        camBrazos.enabled = cam.enabled && !desmayado;

        // Post-procesado una sola vez, en la ultima camara del stack: con los brazos activos lo hace su camara
        // (sobre el mundo y los brazos); sin ellos (desmayo, cinematica) vuelve a hacerlo la camara principal
        if (datosCam != null && datosBrazos != null)
        {
            datosCam.renderPostProcessing = postOriginal && !camBrazos.enabled;
            datosBrazos.renderPostProcessing = postOriginal && camBrazos.enabled;
        }

        // Todo lo que cuelga de la camara (brazos, objeto en la mano) va a la capa de primera persona
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            GameObject g = r.gameObject;
            if (g.layer == capa) continue;
            if (!originales.ContainsKey(g)) originales[g] = g.layer;
            g.layer = capa;
        }

        // Lo que ya no cuelga de la camara (se solto o se entrego) vuelve a su capa
        quitar.Clear();
        foreach (var kv in originales)
            if (kv.Key == null || !kv.Key.transform.IsChildOf(transform)) quitar.Add(kv.Key);
        foreach (GameObject g in quitar)
        {
            if (g != null) g.layer = originales[g];
            originales.Remove(g);
        }
    }
}
