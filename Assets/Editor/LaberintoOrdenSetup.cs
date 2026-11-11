using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Todo lo del laberinto dentro de "Laberinto":
// - "Trigger Laberinto" (el Volume del nivel), el grupo "Enemigos Laberinto" (Fede),
//   las baterias "Bateria Laberinto N" (en "Laberinto/Baterias") y "Presentacion Enemigo".
// Se mueven conservando su posicion en el mundo; las referencias entre componentes no cambian.
// Se aplica solo si algo esta afuera; rehacer: Tools > Laberinto > Ordenar en Laberinto.
[InitializeOnLoad]
public static class LaberintoOrdenSetup
{
    static LaberintoOrdenSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Pendientes(out _) > 0) Ordenar();
        };
    }

    static int Pendientes(out Transform lab)
    {
        GameObject l = GameObject.Find("Laberinto");
        lab = l ? l.transform : null;
        if (!lab) return 0;
        int n = 0;
        foreach (GameObject g in Afuera()) if (g && !g.transform.IsChildOf(lab)) n++;
        return n;
    }

    static System.Collections.Generic.List<GameObject> Afuera()
    {
        var l = new System.Collections.Generic.List<GameObject>
        {
            GameObject.Find("Enemigos y Zonas/Triggers Volumen/Trigger Laberinto"),
            GameObject.Find("Enemigos y Zonas/Enemigos/Enemigos Laberinto"),
        };
        var pres = Object.FindFirstObjectByType<PresentacionEnemigo>(FindObjectsInactive.Include);
        if (pres) l.Add(pres.gameObject);
        foreach (Bateria b in Object.FindObjectsByType<Bateria>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.name.StartsWith("Bateria Laberinto")) l.Add(b.gameObject);
        return l;
    }

    [MenuItem("Tools/Laberinto/Ordenar en Laberinto")]
    static void Ordenar()
    {
        if (Pendientes(out Transform lab) == 0 || !lab) return;
        Transform baterias = lab.Find("Baterias");
        int n = 0;
        foreach (GameObject g in Afuera())
        {
            if (!g || g.transform.IsChildOf(lab)) continue;
            Transform destino = lab;
            if (g.GetComponent<Bateria>())
            {
                if (!baterias)
                {
                    baterias = new GameObject("Baterias").transform;
                    Undo.RegisterCreatedObjectUndo(baterias.gameObject, "Baterias");
                    baterias.SetParent(lab, false);
                }
                destino = baterias;
            }
            Undo.SetTransformParent(g.transform, destino, true, "Ordenar laberinto");
            n++;
        }
        EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
        Debug.Log($"[Laberinto] {n} objetos movidos dentro de \"Laberinto\" (trigger del Volume, Fede, baterias y presentacion). Guarda la escena (Ctrl+S).", lab);
    }
}
