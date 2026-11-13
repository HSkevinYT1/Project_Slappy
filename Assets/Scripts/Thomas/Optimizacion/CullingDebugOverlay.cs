using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Overlay de diagnóstico para medir el efecto de la optimización.
///
/// F3: muestra/oculta el overlay (FPS, tiempo de frame, renderers visibles, far clip, occlusion).
/// F4: medición de N segundos. Al terminar muestra y registra en consola:
///     FPS promedio, 1% low (el FPS de los peores frames = los "tirones"), mínimo, máximo
///     y renderers visibles promedio. Usar siempre desde la misma posición para comparar.
///
/// Pensado solo para pruebas: quitarlo o desactivarlo en la build final.
/// </summary>
public class CullingDebugOverlay : MonoBehaviour
{
    [Tooltip("Mostrar el overlay al iniciar.")]
    public bool visible = true;

    [Tooltip("Cada cuántos segundos se refresca el texto del overlay.")]
    [Min(0.1f)] public float intervalo = 0.5f;

    [Tooltip("Duración de la medición con F4 (segundos).")]
    [Min(1f)] public float duracionMedicion = 10f;

    [Tooltip("Cámara a reportar. Si está vacío usa Camera.main.")]
    public Camera camara;

    Renderer[] _renderers;
    float _acumulado, _peorFrame, _timer;
    int _frames;
    float _fps, _msPromedio, _msPeor;
    int _visibles, _total;
    GUIStyle _estilo;

    // Medición F4
    bool _midiendo;
    float _finMedicion;
    readonly List<float> _muestras = new List<float>(4096);
    long _sumaVisibles;
    int _conteosVisibles;
    string _resultado;

    void Start() => RefrescarRenderers();

    /// <summary>Vuelve a buscar los renderers de la escena (llamar si se instancian muchos objetos).</summary>
    public void RefrescarRenderers()
    {
        _renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    void Update()
    {
        if (TeclaPresionada(3)) visible = !visible;
        if (TeclaPresionada(4) && !_midiendo) EmpezarMedicion();

        float dt = Time.unscaledDeltaTime;
        _acumulado += dt;
        _peorFrame = Mathf.Max(_peorFrame, dt);
        _frames++;
        _timer += dt;

        if (_midiendo)
        {
            _muestras.Add(dt);
            if (Time.unscaledTime >= _finMedicion) TerminarMedicion();
        }

        if (_timer < intervalo) return;

        _fps = _frames / _acumulado;
        _msPromedio = _acumulado / _frames * 1000f;
        _msPeor = _peorFrame * 1000f;
        _acumulado = _peorFrame = _timer = 0f;
        _frames = 0;

        if (visible || _midiendo)
        {
            ContarVisibles();
            if (_midiendo) { _sumaVisibles += _visibles; _conteosVisibles++; }
        }
    }

    void EmpezarMedicion()
    {
        _midiendo = true;
        _finMedicion = Time.unscaledTime + duracionMedicion;
        _muestras.Clear();
        _sumaVisibles = 0;
        _conteosVisibles = 0;
        _resultado = null;
    }

    void TerminarMedicion()
    {
        _midiendo = false;
        if (_muestras.Count == 0) return;

        float suma = 0f;
        foreach (var m in _muestras) suma += m;
        var ordenadas = new List<float>(_muestras);
        ordenadas.Sort(); // de frame más rápido a más lento

        // 1% low: promedio del 1% de frames más lentos, expresado en FPS.
        int n1 = Mathf.Max(1, ordenadas.Count / 100);
        float sumaPeores = 0f;
        for (int i = ordenadas.Count - n1; i < ordenadas.Count; i++) sumaPeores += ordenadas[i];

        float fpsProm = _muestras.Count / suma;
        float fps1Low = n1 / sumaPeores;
        float fpsMin = 1f / ordenadas[ordenadas.Count - 1];
        float fpsMax = 1f / ordenadas[0];
        float visProm = _conteosVisibles > 0 ? (float)_sumaVisibles / _conteosVisibles : 0f;

        var cam = CamaraActual();
        string config = cam != null
            ? $"far {cam.farClipPlane:0} m, occlusion {EstadoOcclusion(cam)}"
            : "sin cámara";

        _resultado =
            $"Medición {duracionMedicion:0}s ({config})\n" +
            $"Prom {fpsProm:0} FPS | 1% low {fps1Low:0} | mín {fpsMin:0} | máx {fpsMax:0}\n" +
            $"Renderers visibles prom: {visProm:0}";
        Debug.Log("[Culling] " + _resultado.Replace("\n", " | "));
    }

    void ContarVisibles()
    {
        // isVisible = el renderer pasó el culling de alguna cámara este frame.
        // OJO: en el editor también cuenta la Scene View; para medir bien, tener visible solo la Game View.
        _visibles = _total = 0;
        if (_renderers == null) return;
        foreach (var r in _renderers)
        {
            if (r == null || !r.enabled) continue;
            _total++;
            if (r.isVisible) _visibles++;
        }
    }

    Camera CamaraActual() => camara != null ? camara : Camera.main;

    static string EstadoOcclusion(Camera cam)
    {
        if (!cam.useOcclusionCulling) return "OFF";
#if UNITY_EDITOR
        // En el editor se puede saber si la escena tiene datos horneados; sin bake, el occlusion no hace nada.
        return UnityEditor.StaticOcclusionCulling.umbraDataSize > 0 ? "ON (horneado)" : "ON (SIN BAKE)";
#else
        return "ON";
#endif
    }

    void OnGUI()
    {
        if (!visible) return;
        _estilo ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };

        var cam = CamaraActual();
        string far = cam != null ? $"{cam.farClipPlane:0} m" : "—";
        string occ = cam != null ? EstadoOcclusion(cam) : "—";
        string color = _fps >= 55 ? "#7CFC00" : _fps >= 30 ? "#FFD700" : "#FF5555";

        string texto =
            $"<b>Culling debug</b>  (F3 ocultar, F4 medir)\n" +
            $"FPS: <color={color}>{_fps:0}</color>\n" +
            $"Frame: {_msPromedio:0.0} ms  (peor {_msPeor:0.0} ms)\n" +
            $"Renderers visibles: {_visibles} / {_total}\n" +
            $"Far clip: {far}   Occlusion: {occ}";

        if (_midiendo)
            texto += $"\n<color=#FFD700>Midiendo... {Mathf.Max(0f, _finMedicion - Time.unscaledTime):0.0}s</color>";
        else if (_resultado != null)
            texto += $"\n<color=#87CEFA>{_resultado}</color>";

        float alto = _midiendo ? 140f : _resultado != null ? 190f : 120f;
        GUI.Box(new Rect(10, 10, 420, alto), texto, _estilo);
    }

    static bool TeclaPresionada(int f)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return false;
        return f == 3 ? k.f3Key.wasPressedThisFrame : k.f4Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(f == 3 ? KeyCode.F3 : KeyCode.F4);
#endif
    }
}
