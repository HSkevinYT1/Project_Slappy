using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Reglas de las cabinas telefonicas:
// 1. Colision solida en los 3 lados sin puerta de cada cabina (antes se podia entrar por los costados).
// 2. La cabina del sotano no teletransporta hasta que se revisen las cajas del sotano (RevisionCajas).
// 3. (La niebla ya no se activa al llegar: ahora la enciende VolumenZonas solo dentro de los niveles.)
// Se aplica sola si falta algo; rehacer: Tools > Cabinas > Colision y reglas.
[InitializeOnLoad]
public static class CabinasReglasSetup
{
    static readonly string[] Cabinas = { "zotano/props/Cabina_Telefonica", "Zona_Principal/Cabina_Telefonica (1)" };
    const string Nombre = "Colision Cabina";

    static CabinasReglasSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (FaltaAlgo()) Aplicar();
            };
    }

    static bool FaltaAlgo()
    {
        foreach (string r in Cabinas)
        {
            GameObject c = GameObject.Find(r);
            if (c && !c.transform.Find(Nombre)) return true;
        }
        var ct = Object.FindFirstObjectByType<CabinaTeletransporte>();
        if (ct && !ct.cajasRequeridas && Object.FindFirstObjectByType<RevisionCajas>()) return true;
        return false;
    }

    [MenuItem("Tools/Cabinas/Colision y reglas")]
    static void Aplicar()
    {
        // 1. Colision
        foreach (string r in Cabinas)
        {
            GameObject c = GameObject.Find(r);
            if (!c) continue;
            Transform vieja = c.transform.Find(Nombre);
            if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);
            Colision(c.transform);
        }

        // 2. Cajas requeridas
        var ct = Object.FindFirstObjectByType<CabinaTeletransporte>();
        var cajas = Object.FindFirstObjectByType<RevisionCajas>();
        if (ct && cajas)
        {
            Undo.RecordObject(ct, "Cajas requeridas");
            ct.cajasRequeridas = cajas;
            EditorUtility.SetDirty(ct);
        }

        // 3. (Ya no) FogVolume al llegar: ahora lo maneja VolumenZonas segun la zona. Se quita el listener viejo.
        var ft = Object.FindFirstObjectByType<FaintTeleport>();
        GameObject fog = BuscarFog();
        if (ft && fog)
            for (int i = ft.onArrived.GetPersistentEventCount() - 1; i >= 0; i--)
                if (ft.onArrived.GetPersistentTarget(i) == fog) { Undo.RecordObject(ft, "Quitar FogVolume"); UnityEventTools.RemovePersistentListener(ft.onArrived, i); EditorUtility.SetDirty(ft); }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Cabinas] Colision en los costados; teletransporte {(ct && cajas ? "despues de revisar las " + cajas.cajas.Length + " cajas" : "SIN cajas (no se encontro RevisionCajas)")}; " +
                  "la niebla la maneja VolumenZonas. Guarda la escena (Ctrl+S).");
    }

    // El objeto puede estar desactivado: se busca entre las raices de la escena (y sus hijos)
    static GameObject BuscarFog()
    {
        foreach (GameObject raiz in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.Replace(" ", "").ToLowerInvariant();
                if (n == "fogvolume" || n == "fogvolumen") return t.gameObject;
            }
        return null;
    }

    static bool TieneListener(FaintTeleport ft, GameObject fog)
    {
        for (int i = 0; i < ft.onArrived.GetPersistentEventCount(); i++)
            if (ft.onArrived.GetPersistentTarget(i) == fog && ft.onArrived.GetPersistentMethodName(i) == "SetActive") return true;
        return false;
    }

    // Tres paredes invisibles (los lados sin puerta), justo por dentro del cuerpo de la cabina
    static void Colision(Transform cabina)
    {
        Transform cuerpo = cabina.Find("Cabina");
        if (!cuerpo) return;
        Bounds bc = CabinaSotanoSetup.Limites(cuerpo);

        // Lado de la puerta: donde queda la hoja una vez cerrada
        Vector3 ladoPuerta = Vector3.zero;
        Transform puerta = null, bisagra = null; float angulo = 90f;
        if (cabina.TryGetComponent(out CabinaTeletransporte ct)) { puerta = ct.puerta; bisagra = ct.bisagra; angulo = ct.anguloCerrar; }
        else if (cabina.TryGetComponent(out CabinaSalida cs)) { puerta = cs.puerta; bisagra = cs.bisagra; angulo = cs.anguloCerrar; }
        if (puerta && bisagra)
        {
            Bounds bp = CabinaSotanoSetup.Limites(puerta);
            Vector3 h = bisagra.position;
            Vector3 cerrada = h + Quaternion.Euler(0f, angulo, 0f) * (new Vector3(bp.center.x, h.y, bp.center.z) - h);
            Vector3 rel = cerrada - bc.center;
            ladoPuerta = Mathf.Abs(rel.x) / bc.extents.x > Mathf.Abs(rel.z) / bc.extents.z
                ? new Vector3(Mathf.Sign(rel.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(rel.z));
        }

        var go = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(go, "Colision cabina");
        go.transform.SetParent(cabina, true);
        go.transform.SetPositionAndRotation(new Vector3(bc.center.x, bc.min.y, bc.center.z), Quaternion.identity);
        Vector3 e = go.transform.lossyScale;
        const float grosor = 0.12f;
        foreach (Vector3 lado in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            if (lado == ladoPuerta) continue;
            var box = go.AddComponent<BoxCollider>();
            bool enX = lado.x != 0f;
            Vector3 centro = new Vector3(lado.x * (bc.extents.x - grosor * 0.5f), bc.size.y * 0.5f, lado.z * (bc.extents.z - grosor * 0.5f));
            Vector3 tam = enX ? new Vector3(grosor, bc.size.y, bc.size.z) : new Vector3(bc.size.x, bc.size.y, grosor);
            box.center = new Vector3(centro.x / e.x, centro.y / e.y, centro.z / e.z);
            box.size = new Vector3(tam.x / e.x, tam.y / e.y, tam.z / e.z);
        }
        // Techo (para no poder subirse ni quedar enganchado arriba)
        var techo = go.AddComponent<BoxCollider>();
        techo.center = new Vector3(0f, (bc.size.y - grosor * 0.5f) / e.y, 0f);
        techo.size = new Vector3(bc.size.x / e.x, grosor / e.y, bc.size.z / e.z);
    }
}
