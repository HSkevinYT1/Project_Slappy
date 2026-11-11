using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Presentacion del enemigo del laberinto: zona justo al entrar (al final del pasillo) y un punto a lo lejos,
// al fondo de un pasillo que se ve desde la entrada, donde el enemigo esta quieto de espaldas.
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
        var vieja = Object.FindFirstObjectByType<PresentacionEnemigo>(FindObjectsInactive.Include);
        GameObject lab = GameObject.Find("Laberinto");
        if (!lab || !EnemigoDelLaberinto()) return;
        // Version anterior (franjas de cine y el enemigo cruzando): se rehace (solo si encuentra el nuevo punto)
        if (vieja && !(vieja.franjasCine && vieja.puntoDestino != null)) return;
        Aplicar(lab.transform, vieja);
    }

    [MenuItem("Tools/Laberinto/Presentacion del enemigo")]
    static void Menu()
    {
        GameObject lab = GameObject.Find("Laberinto");
        if (!lab) { EditorUtility.DisplayDialog("Laberinto", "No se encontro \"Laberinto\".", "OK"); return; }
        Aplicar(lab.transform, Object.FindFirstObjectByType<PresentacionEnemigo>(FindObjectsInactive.Include));
    }

    static JefeIndigoAI EnemigoDelLaberinto()
    {
        GameObject g = GameObject.Find("Laberinto/Enemigos Laberinto");
        if (!g) g = GameObject.Find("Enemigos y Zonas/Enemigos/Enemigos Laberinto");
        return g ? g.GetComponentInChildren<JefeIndigoAI>(true) : null;
    }

    static void Aplicar(Transform lab, PresentacionEnemigo vieja)
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

        // Punto de aparicion: al final del pasillo mas largo que se ve de frente desde la entrada.
        // Se lanzan rayos hacia delante (hasta 30 grados a cada lado); el mas largo es el pasillo,
        // y el enemigo va 1.5 m antes de la pared del fondo.
        Vector3 mejor = Vector3.zero, mirar = dentro; float mejorPuntos = float.MinValue;
        var camino = new NavMeshPath();
        for (float ang = -30f; ang <= 30.01f; ang += 2f)
        {
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * dentro;
            Vector3 desde = pie + Vector3.up * 1.2f;
            if (!Physics.Raycast(desde, dir, out RaycastHit pared, 60f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (pared.distance < 10f) continue;
            Vector3 p = desde + dir * (pared.distance - 1.5f);
            if (!NavMesh.SamplePosition(p, out NavMeshHit h, 2f, NavMesh.AllAreas)) continue;
            Vector3 q = h.position;
            if (!Visible(ojos, q + Vector3.up * 0.5f, enemigo.transform) || !Visible(ojos, q + Vector3.up * 1.4f, enemigo.transform)) continue;
            if (!NavMesh.CalculatePath(pie, q, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;
            float puntos = pared.distance - Mathf.Abs(ang) * 0.3f;
            if (puntos > mejorPuntos) { mejorPuntos = puntos; mejor = q; mirar = dir; }
        }
        if (mejorPuntos == float.MinValue) { Debug.LogWarning("[Laberinto] No encontre ningun pasillo largo visible desde la entrada para presentar al enemigo."); return; }
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);

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

        // De espaldas al jugador, mirando la pared del fondo un poco de lado (no lo ve)
        Transform aparicion = Punto(raiz.transform, "Punto Aparicion", mejor);
        aparicion.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, 35f, 0f) * mirar);
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
        pres.puntoDestino = null;
        pres.franjasCine = false;
        pres.puntoVista = vista;
        pres.luzEnemigo = luz;

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log($"[Laberinto] Presentacion del enemigo: quieto al fondo de un pasillo, a {Vector3.Distance(pie, mejor):0} m de la entrada. Puedes mover/girar \"Punto Aparicion\". Guarda la escena.", raiz);
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
