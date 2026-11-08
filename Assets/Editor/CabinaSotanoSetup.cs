using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Cabina del sotano sin puzzle: al entrar se cierra la puerta, parpadean 4 luces y el jugador se desmaya
// (FaintTeleport) hasta la cabina de la Zona Principal.
// - Agrega CabinaTeletransporte a la cabina con su zona, la bisagra de la puerta y una luz.
// - El telefono del sotano deja de ser interactuable y ya no viaja (el de la Zona Principal se queda con su puzzle).
// No mueve nada de la escena. Se aplica sola una vez; rehacer: Tools > Sotano > Cabina sin puzzle.
[InitializeOnLoad]
public static class CabinaSotanoSetup
{
    const string Ruta = "zotano/props/Cabina_Telefonica";

    static CabinaSotanoSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += AutoAplicar;
        };
    }

    internal static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject c = GameObject.Find(Ruta);
        if (!c || c.GetComponent<CabinaTeletransporte>()) return;
        Aplicar(c.transform);
    }

    [MenuItem("Tools/Sotano/Cabina sin puzzle")]
    static void Menu()
    {
        GameObject c = GameObject.Find(Ruta);
        if (!c) { EditorUtility.DisplayDialog("Sotano", "No se encontro \"" + Ruta + "\".", "OK"); return; }
        var viejo = c.GetComponent<CabinaTeletransporte>();
        if (viejo)
        {
            foreach (var n in new[] { "Bisagra Puerta", "Luz Cabina", "Zona Cabina" })
            {
                Transform t = c.transform.Find(n);
                if (t) Undo.DestroyObjectImmediate(t.gameObject);
            }
            Undo.DestroyObjectImmediate(viejo);
        }
        Aplicar(c.transform);
    }

    static void Aplicar(Transform cabina)
    {
        Transform cuerpo = cabina.Find("Cabina");
        Transform puerta = cabina.Find("Puerta_Cabina_Telefonica");
        if (!cuerpo || !puerta) { Debug.LogWarning("[Sotano] La cabina no tiene \"Cabina\" o \"Puerta_Cabina_Telefonica\".", cabina); return; }
        Bounds bc = Limites(cuerpo), bp = Limites(puerta);

        // Bisagra: el extremo de la puerta abierta mas cercano a la cabina
        bool largoEnX = bp.size.x > bp.size.z;
        Vector3 e1 = largoEnX ? new Vector3(bp.min.x, 0f, bp.center.z) : new Vector3(bp.center.x, 0f, bp.min.z);
        Vector3 e2 = largoEnX ? new Vector3(bp.max.x, 0f, bp.center.z) : new Vector3(bp.center.x, 0f, bp.max.z);
        Vector3 c2 = new Vector3(bc.center.x, 0f, bc.center.z);
        Vector3 hinge = (e1 - c2).sqrMagnitude < (e2 - c2).sqrMagnitude ? e1 : e2;
        hinge.y = bp.center.y;

        // Angulo: el que deja la puerta alineada con la cabina y lo mas cerca de su centro (tapando la abertura)
        float yawPuerta = puerta.eulerAngles.y, yawCabina = cuerpo.eulerAngles.y;
        float mejor = 90f, mejorDist = float.MaxValue;
        foreach (float signo in new[] { 1f, -1f })
        {
            float yaw = yawPuerta + signo * 90f;
            float alineado = yawCabina + Mathf.Round(Mathf.DeltaAngle(yawCabina, yaw) / 90f) * 90f;
            float ang = Mathf.DeltaAngle(yawPuerta, alineado);
            Vector3 centro = hinge + Quaternion.Euler(0f, ang, 0f) * (new Vector3(bp.center.x, hinge.y, bp.center.z) - hinge);
            float d = new Vector2(centro.x - bc.center.x, centro.z - bc.center.z).magnitude;
            if (d < mejorDist) { mejorDist = d; mejor = ang; }
        }

        var bisagra = new GameObject("Bisagra Puerta").transform;
        Undo.RegisterCreatedObjectUndo(bisagra.gameObject, "Bisagra");
        bisagra.SetParent(cabina, true);
        bisagra.position = hinge;

        // Luz arriba dentro de la cabina
        var luzGo = new GameObject("Luz Cabina");
        Undo.RegisterCreatedObjectUndo(luzGo, "Luz cabina");
        luzGo.transform.SetParent(cabina, true);
        luzGo.transform.position = new Vector3(bc.center.x, bc.max.y - 0.25f, bc.center.z);
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.range = 4f;
        luz.intensity = 0f;
        luz.shadows = LightShadows.None;
        luz.enabled = false;

        // Zona dentro de la cabina (un poco mas chica que la cabina)
        var zonaGo = new GameObject("Zona Cabina");
        Undo.RegisterCreatedObjectUndo(zonaGo, "Zona cabina");
        zonaGo.layer = 2;
        zonaGo.transform.SetParent(cabina, false);
        zonaGo.transform.position = new Vector3(bc.center.x, bc.min.y + 1.1f, bc.center.z);
        zonaGo.transform.rotation = Quaternion.identity;
        var zona = zonaGo.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        Vector3 s = zonaGo.transform.lossyScale;
        Vector3 tam = new Vector3(Mathf.Max(0.3f, bc.size.x - 0.3f), 2.2f, Mathf.Max(0.3f, bc.size.z - 0.3f));
        zona.size = new Vector3(tam.x / s.x, tam.y / s.y, tam.z / s.z);

        var ct = Undo.AddComponent<CabinaTeletransporte>(cabina.gameObject);
        ct.zona = zona;
        ct.desmayo = Object.FindFirstObjectByType<FaintTeleport>();
        ct.puerta = puerta;
        ct.bisagra = bisagra;
        ct.anguloCerrar = mejor;
        ct.luz = luz;
        EditorUtility.SetDirty(ct);

        // Sin puzzle en el sotano: el telefono ya no se usa ni viaja
        Transform tel = cabina.Find("Telefono");
        if (tel && tel.TryGetComponent(out Interactable inter))
        {
            Undo.RecordObject(inter, "Telefono sin puzzle");
            inter.enabled = false;
        }
        if (ct.desmayo)
        {
            var so = new SerializedObject(ct.desmayo);
            so.FindProperty("carryAlong").arraySize = 0;
            so.FindProperty("hideAtDestination").arraySize = 0;
            so.ApplyModifiedProperties();
        }
        else Debug.LogWarning("[Sotano] No hay FaintTeleport en la escena.");

        EditorSceneManager.MarkSceneDirty(cabina.gameObject.scene);
        Debug.Log($"[Sotano] Cabina sin puzzle: al entrar se cierra la puerta ({mejor:0} grados), parpadean 4 luces y se desmaya hasta la otra cabina. Guarda la escena.", cabina);
    }

    static Bounds Limites(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
