using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Niebla (FogVolume) solo dentro de los niveles: Laberinto, Cloacas y Zona Roja (el Backrooms no).
// - FogVolume queda activo en la escena pero empieza apagado (activeOnStart = false); VolumenZonas lo
//   enciende/apaga con transicion segun el area en la que esta el jugador.
// - Cada trigger de area (se llaman como su zona) dice si usa niebla; "Backrooms" no.
// - El Volume de las areas entra despacio (transicion de 5 s con curva suave).
// - Se quita el listener viejo que activaba FogVolume al llegar por la cabina.
// Se aplica sola si falta; rehacer: Tools > Zonas > Niebla y volumenes.
[InitializeOnLoad]
public static class NieblaZonasSetup
{
    static NieblaZonasSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var vz = Object.FindFirstObjectByType<VolumenZonas>();
                if (vz && !vz.niebla && Fog()) Aplicar();
            };
    }

    static FogVolume Fog()
    {
        foreach (GameObject raiz in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (FogVolume f in raiz.GetComponentsInChildren<FogVolume>(true))
                return f;
        return null;
    }

    [MenuItem("Tools/Zonas/Niebla y volumenes")]
    static void Aplicar()
    {
        var vz = Object.FindFirstObjectByType<VolumenZonas>();
        FogVolume fog = Fog();
        if (!vz || !fog) { Debug.LogWarning("[Zonas] Falta VolumenZonas o FogVolume en la escena."); return; }

        Undo.RecordObject(fog.gameObject, "Niebla");
        fog.gameObject.SetActive(true);
        Undo.RecordObject(fog, "Niebla");
        fog.activeOnStart = false;
        fog.fadeDuration = 4f;
        EditorUtility.SetDirty(fog);

        Undo.RecordObject(vz, "Volumen zonas");
        vz.niebla = fog;
        vz.transicion = 5f;
        EditorUtility.SetDirty(vz);

        string sin = "";
        foreach (ZonaVolumen z in Object.FindObjectsByType<ZonaVolumen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(z, "Niebla zona");
            z.niebla = !z.name.ToLowerInvariant().Contains("backroom");
            if (!z.niebla) sin += z.name + " ";
            EditorUtility.SetDirty(z);
        }

        var ft = Object.FindFirstObjectByType<FaintTeleport>();
        if (ft)
            for (int i = ft.onArrived.GetPersistentEventCount() - 1; i >= 0; i--)
                if (ft.onArrived.GetPersistentTarget(i) == fog.gameObject)
                {
                    Undo.RecordObject(ft, "Quitar FogVolume al llegar");
                    UnityEventTools.RemovePersistentListener(ft.onArrived, i);
                    EditorUtility.SetDirty(ft);
                }

        EditorSceneManager.MarkSceneDirty(vz.gameObject.scene);
        Debug.Log($"[Zonas] Niebla solo en los niveles (sin niebla: {sin.Trim()}); volumenes con transicion de {vz.transicion} s. Guarda la escena (Ctrl+S).");
    }
}
