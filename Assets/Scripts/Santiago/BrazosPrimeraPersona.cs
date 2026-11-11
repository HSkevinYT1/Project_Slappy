using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Los brazos y lo que se tiene en la mano ya no atraviesan paredes ni objetos:
// todo lo que cuelga de la camara del jugador se pasa a la capa "Primera Persona", que la camara principal
// NO dibuja; la dibuja una camara superpuesta (overlay, en el stack de URP) que limpia la profundidad,
// asi siempre queda por delante del mundo. Al soltar un objeto vuelve a su capa original.
// Va en la Main Camera del jugador.
[RequireComponent(typeof(Camera))]
public class BrazosPrimeraPersona : MonoBehaviour
{
    public string nombreCapa = "Primera Persona";
    [Tooltip("Plano cercano de la camara de los brazos (bien cerca para que no se corten las manos)")]
    public float planoCercano = 0.01f;

    Camera cam, camBrazos;
    int capa = -1;
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

        if (camBrazos == null)
        {
            var go = new GameObject("Camara Brazos");
            go.transform.SetParent(transform, false);
            camBrazos = go.AddComponent<Camera>();
            var datos = camBrazos.GetUniversalAdditionalCameraData();
            datos.renderType = CameraRenderType.Overlay;
            datos.renderShadows = false;
            datos.renderPostProcessing = false; // el post-procesado lo hace la camara base sobre todo el stack
        }
        camBrazos.cullingMask = 1 << capa;
        camBrazos.nearClipPlane = planoCercano;
        camBrazos.farClipPlane = 20f;
        var stack = cam.GetUniversalAdditionalCameraData().cameraStack;
        if (!stack.Contains(camBrazos)) stack.Add(camBrazos);
    }

    void OnDisable()
    {
        if (cam != null)
        {
            if (camBrazos != null) cam.GetUniversalAdditionalCameraData().cameraStack.Remove(camBrazos);
            if (capa >= 0) cam.cullingMask |= 1 << capa;
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
