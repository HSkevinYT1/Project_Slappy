using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Presentacion del enemigo del laberinto: zona justo al entrar, un punto a lo lejos que se ve desde la
// entrada (sin paredes en medio) donde aparece el enemigo, y un destino hacia el que camina hasta perderse.
// Los puntos se pueden mover a mano despues. Se aplica sola una vez; rehacer: Tools > Laberinto > Presentacion del enemigo.
[InitializeOnLoad]
public static class PresentacionEnemigoSetup
{
    const string Nombre = "Presentacion Enemigo";

    static PresentacionEnemigoSetup()
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
        if (Object.FindFirstObjectByType<PresentacionEnemigo>(FindObjectsInactive.Include)) return;
        GameObject lab = GameObject.Find("Laberinto");
        if (!lab || !EnemigoDelLaberinto()) return;
        Aplicar(lab.transform);
    }

    [MenuItem("Tools/Laberinto/Presentacion del enemigo")]
    static void Menu()
    {
        GameObject lab = GameObject.Find("Laberinto");
        if (!lab) { EditorUtility.DisplayDialog("Laberinto", "No se encontro \"Laberinto\".", "OK"); return; }
        var vieja = Object.FindFirstObjectByType<PresentacionEnemigo>(FindObjectsInactive.Include);
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);
        Aplicar(lab.transform);
    }

    static JefeIndigoAI EnemigoDelLaberinto()
    {
        GameObject g = GameObject.Find("Enemigos y Zonas/Enemigos/Enemigos Laberinto");
        return g ? g.GetComponentInChildren<JefeIndigoAI>(true) : null;
    }

    static void Aplicar(Transform lab)
    {
        JefeIndigoAI enemigo = EnemigoDelLaberinto();
        Transform entrada = lab.Find("Entrada Pasillo");
        if (!enemigo || !entrada) { Debug.LogWarning("[Laberinto] Falta el enemigo del laberinto o la \"Entrada Pasillo\"."); return; }

        // "Entrada Pasillo" mira hacia AFUERA: hacia dentro es -forward
        Vector3 dentro = -entrada.forward; dentro.y = 0f; dentro.Normalize();
        Vector3 inicio = entrada.position + dentro * 3f;
        NavMesh.SamplePosition(inicio, out NavMeshHit hi, 5f, NavMesh.AllAreas);
        Vector3 pie = hi.hit ? hi.position : inicio;
        Vector3 ojos = pie + Vector3.up * 1.6f;

        // Punto de aparicion: lejos (12-26 m), por delante, y que se vea desde la entrada
        Bounds b = Limites(lab);
        var rnd = new System.Random(77);
        Vector3 mejor = Vector3.zero; float mejorPuntos = float.MinValue;
        var camino = new NavMeshPath();
        for (int i = 0; i < 4000; i++)
        {
            var p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, (float)rnd.NextDouble()), b.center.y, Mathf.Lerp(b.min.z, b.max.z, (float)rnd.NextDouble()));
            if (!NavMesh.SamplePosition(p, out NavMeshHit h, 3f, NavMesh.AllAreas) || !b.Contains(h.position)) continue;
            Vector3 q = h.position;
            Vector3 d = q - pie; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 12f || dist > 26f) continue;
            float angulo = Vector3.Angle(dentro, d);
            if (angulo > 55f) continue;
            if (!Visible(ojos, q + Vector3.up * 0.4f, enemigo.transform) || !Visible(ojos, q + Vector3.up * 1.3f, enemigo.transform)) continue;
            if (!NavMesh.CalculatePath(pie, q, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;
            float puntos = dist - angulo * 0.15f;
            if (puntos > mejorPuntos) { mejorPuntos = puntos; mejor = q; }
        }
        if (mejorPuntos == float.MinValue) { Debug.LogWarning("[Laberinto] No encontre ningun punto lejano visible desde la entrada para presentar al enemigo."); return; }

        // Destino: a 6-12 m del punto, mas lejos de la entrada y preferiblemente fuera de la vista
        Vector3 destino = mejor; float mejorD = float.MinValue;
        for (int i = 0; i < 3000; i++)
        {
            var p = mejor + new Vector3((float)rnd.NextDouble() * 24f - 12f, 0f, (float)rnd.NextDouble() * 24f - 12f);
            if (!NavMesh.SamplePosition(p, out NavMeshHit h, 2f, NavMesh.AllAreas)) continue;
            Vector3 q = h.position;
            float d = (q - mejor).magnitude;
            if (d < 6f || d > 12f) continue;
            if (!NavMesh.CalculatePath(mejor, q, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;
            float alejarse = Vector3.Distance(q, pie) - Vector3.Distance(mejor, pie);
            float puntos = alejarse + (Visible(ojos, q + Vector3.up, enemigo.transform) ? 0f : 6f) - PathLength(camino) * 0.2f;
            if (puntos > mejorD) { mejorD = puntos; destino = q; }
        }

        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Presentacion enemigo");
        raiz.transform.position = pie;

        var zonaGo = new GameObject("Zona");
        zonaGo.layer = 2;
        zonaGo.transform.SetParent(raiz.transform, false);
        zonaGo.transform.SetPositionAndRotation(pie + Vector3.up * 1.5f, Quaternion.LookRotation(dentro));
        var zona = zonaGo.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.size = new Vector3(9f, 3.5f, 2f);

        Transform aparicion = Punto(raiz.transform, "Punto Aparicion", mejor);
        Transform fin = Punto(raiz.transform, "Punto Destino", destino);
        Transform vista = Punto(raiz.transform, "Punto Vista", ojos + (mejor - pie).normalized * 1.2f);

        var luzGo = new GameObject("Luz Enemigo");
        luzGo.transform.SetParent(raiz.transform, false);
        luzGo.transform.position = mejor + Vector3.up * 2.2f;
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(0.65f, 0.55f, 1f);
        luz.range = 6f;
        luz.intensity = 5f;
        luz.shadows = LightShadows.None;
        luz.enabled = false;

        var pres = raiz.AddComponent<PresentacionEnemigo>();
        pres.zona = zona;
        pres.enemigo = enemigo;
        pres.puntoAparicion = aparicion;
        pres.puntoDestino = fin;
        pres.puntoVista = vista;
        pres.luzEnemigo = luz;

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log($"[Laberinto] Presentacion del enemigo: aparece a {Vector3.Distance(pie, mejor):0} m de la entrada. Puedes mover los puntos. Guarda la escena.", raiz);
    }

    static float PathLength(NavMeshPath p)
    {
        float l = 0f;
        for (int i = 1; i < p.corners.Length; i++) l += Vector3.Distance(p.corners[i - 1], p.corners[i]);
        return l;
    }

    static bool Visible(Vector3 desde, Vector3 hasta, Transform ignorar)
    {
        Vector3 d = hasta - desde;
        foreach (RaycastHit h in Physics.RaycastAll(desde, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(ignorar)) continue;
            if (h.collider.GetComponentInParent<PlayerMovement>()) continue;
            return false;
        }
        return true;
    }

    static Transform Punto(Transform padre, string nombre, Vector3 pos)
    {
        var t = new GameObject(nombre).transform;
        t.SetParent(padre, false);
        t.position = pos;
        return t;
    }

    static Bounds Limites(Transform t)
    {
        Collider[] cs = t.GetComponentsInChildren<Collider>();
        Bounds b = cs.Length > 0 ? cs[0].bounds : new Bounds(t.position, Vector3.one);
        foreach (Collider c in cs) b.Encapsulate(c.bounds);
        return b;
    }
}
