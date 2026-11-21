using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Cada grupo de enemigos va dentro de su propio nivel:
//   "Laberinto/Enemigos Laberinto" (Fede), "Backrooms/Enemigos Backrooms" (el gato),
//   "Cloacas/Enemigos Cloacas" (las estatuas y la llave) y "Zona Roja/Enemigos Zona Roja" (Enemy 4).
// Antes estaban en "Enemigos y Zonas/Enemigos". Se mueven conservando su posicion en el mundo y las referencias
// entre componentes no cambian. Si queda algun enemigo suelto en "Enemigos y Zonas" (o en la raiz de la escena),
// va al grupo del nivel donde esta parado.
// Cada grupo lleva un NavMeshModifier "Ignore From Build": al rehornear el NavMesh del nivel (que recoge a sus
// hijos) los enemigos no cuentan como obstaculos ni dejan agujeros.
// Se aplica sola si algo esta afuera; rehacer: Tools > Enemigos y Zonas > Enemigos dentro de su nivel.
[InitializeOnLoad]
public static class EnemigosEnSuNivelSetup
{
    internal static readonly string[] Niveles = { "Laberinto", "Backrooms", "Cloacas", "Zona Roja" };
    const string Contenedor = "Enemigos y Zonas";
    const string Viejo = Contenedor + "/Enemigos";

    static EnemigosEnSuNivelSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Pendientes() > 0) Ordenar();
        };
    }

    static Transform Nivel(string nombre)
    {
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == nombre) return g.transform;
        return null;
    }

    // El grupo de enemigos de un nivel, este donde este (dentro del nivel o todavia en el sitio viejo)
    internal static Transform Grupo(string nivel)
    {
        Transform raiz = Nivel(nivel);
        Transform g = raiz ? raiz.Find("Enemigos " + nivel) : null;
        if (g) return g;
        GameObject viejo = GameObject.Find(Viejo + "/Enemigos " + nivel);
        return viejo ? viejo.transform : null;
    }

    // Crea (o devuelve) el grupo dentro del nivel, ya marcado para que el NavMesh lo ignore
    internal static Transform GrupoEnNivel(string nivel)
    {
        Transform raiz = Nivel(nivel);
        if (!raiz) return null;
        Transform g = raiz.Find("Enemigos " + nivel);
        if (!g)
        {
            g = new GameObject("Enemigos " + nivel).transform;
            Undo.RegisterCreatedObjectUndo(g.gameObject, "Enemigos en su nivel");
            Undo.SetTransformParent(g, raiz, false, "Enemigos en su nivel");
        }
        IgnorarEnNavMesh(g);
        return g;
    }

    internal static void IgnorarEnNavMesh(Transform grupo)
    {
        var mod = grupo.GetComponent<NavMeshModifier>();
        if (mod && mod.ignoreFromBuild && mod.applyToChildren) return;
        if (!mod) mod = Undo.AddComponent<NavMeshModifier>(grupo.gameObject);
        Undo.RecordObject(mod, "Enemigos en su nivel");
        mod.ignoreFromBuild = true;
        mod.applyToChildren = true;
    }

    static List<Transform> Sueltos()
    {
        var res = new List<Transform>();
        void Revisar(Component c)
        {
            Transform t = c.transform;
            Transform raiz = t.root;
            // Solo los que estan en la raiz de la escena o en "Enemigos y Zonas" (la tienda, el sotano, etc. no se tocan)
            if (raiz != t && raiz.name != Contenedor) return;
            foreach (string n in Niveles) { Transform g = Grupo(n); if (g && t.IsChildOf(g)) return; }
            if (!res.Contains(t)) res.Add(t);
        }
        foreach (JefeIndigoAI e in Object.FindObjectsByType<JefeIndigoAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Revisar(e);
        foreach (EstatuaAI e in Object.FindObjectsByType<EstatuaAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Revisar(e);
        return res;
    }

    static int Pendientes()
    {
        int n = 0;
        foreach (string nivel in Niveles)
        {
            Transform raiz = Nivel(nivel), grupo = Grupo(nivel);
            if (!raiz || !grupo) continue;
            var mod = grupo.GetComponent<NavMeshModifier>();
            if (grupo.parent != raiz || !mod || !mod.ignoreFromBuild) n++;
        }
        return n + Sueltos().Count;
    }

    [MenuItem("Tools/Enemigos y Zonas/Enemigos dentro de su nivel")]
    static void Ordenar()
    {
        int grupos = 0;
        var movidos = new List<string>();
        var sinNivel = new List<string>();
        foreach (string nivel in Niveles)
        {
            Transform raiz = Nivel(nivel), grupo = Grupo(nivel);
            if (!raiz || !grupo) continue;
            if (grupo.parent != raiz) { Undo.SetTransformParent(grupo, raiz, true, "Enemigos en su nivel"); grupos++; }
            IgnorarEnNavMesh(grupo);
        }

        // Enemigos sueltos: al nivel donde estan parados (el mas chico que los contiene)
        foreach (Transform e in Sueltos())
        {
            string mejor = null; float volumen = float.MaxValue;
            foreach (string nivel in Niveles)
            {
                Transform raiz = Nivel(nivel);
                if (!raiz || !Limites(raiz, out Bounds b)) continue;
                b.Expand(new Vector3(1f, 6f, 1f));
                float v = b.size.x * b.size.y * b.size.z;
                if (b.Contains(e.position) && v < volumen) { volumen = v; mejor = nivel; }
            }
            if (mejor == null) { sinNivel.Add(e.name); continue; }
            Undo.SetTransformParent(e, GrupoEnNivel(mejor), true, "Enemigos en su nivel");
            movidos.Add(e.name + " -> " + mejor);
        }

        // El contenedor viejo, si quedo vacio, sobra
        GameObject viejo = GameObject.Find(Viejo);
        if (viejo && viejo.transform.childCount == 0) Undo.DestroyObjectImmediate(viejo);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Enemigos] Cada grupo de enemigos esta dentro de su nivel (" + grupos + " movidos" +
                  (movidos.Count > 0 ? "; sueltos: " + string.Join(", ", movidos) : "") + "). Guarda la escena (Ctrl+S).");
        if (sinNivel.Count > 0)
            Debug.LogWarning("[Enemigos] No se a que nivel pertenecen: " + string.Join(", ", sinNivel) + ". Muevelos a mano al grupo \"Enemigos <nivel>\".");
    }

    // Limites del nivel (sin contar a los enemigos)
    static bool Limites(Transform nivel, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in nivel.GetComponentsInChildren<Renderer>())
        {
            if (r.GetComponentInParent<JefeIndigoAI>() || r.GetComponentInParent<EstatuaAI>()) continue;
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        return hay;
    }
}
