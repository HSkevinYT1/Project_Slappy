using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Ajusta las dos cabinas telefonicas al tamano del jugador (capsula de 1.72 m, 0.58 m de ancho, ojos a ~1.46 m):
// - Quita los aplastados distintos que tenia cada pieza (cuerpo, ventanas, puerta) para que todo encaje.
// - Las deja IGUALES en tamano (el desmayo mapea al jugador de una cabina a la otra con sus escalas).
// - El telefono queda de un tamano real y a la altura de la cara.
// Se mantienen el piso y el centro de cada cabina. Se aplica sola si las medidas no coinciden;
// rehacer: Tools > Cabinas > Ajustar al tamano del jugador. Ctrl+Z deshace. Despues guarda la escena.
[InitializeOnLoad]
public static class CabinasTamanoSetup
{
    static readonly string[] Rutas = { "zotano/props/Cabina_Telefonica", "Zona_Principal/Cabina_Telefonica (1)" };
    const float Alto = 2.45f;          // cuerpo de la cabina
    const float Ancho = 1.15f;         // por fuera; deja ~1 m libre adentro para la capsula de 0.58 m
    const float AltoTelefono = 0.6f;   // caja del telefono
    const float CentroTelefono = 1.3f; // altura del centro de la caja sobre el piso (auricular a la altura de la cara)

    static readonly string[] PiezasModelo = { "Cabina", "Ventana", "Puerta_Cabina_Telefonica" };

    static CabinasTamanoSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (string r in Rutas)
        {
            GameObject c = GameObject.Find(r);
            if (!c) continue;
            Transform cuerpo = c.transform.Find("Cabina");
            if (!cuerpo) continue;
            Bounds b = CabinaSotanoSetup.Limites(cuerpo);
            if (Mathf.Abs(b.size.y - Alto) > 0.03f || Mathf.Abs(Mathf.Max(b.size.x, b.size.z) - Ancho) > 0.03f) { Aplicar(); return; }
        }
    }

    [MenuItem("Tools/Cabinas/Ajustar al tamano del jugador")]
    static void Aplicar()
    {
        int n = 0;
        foreach (string r in Rutas)
        {
            GameObject c = GameObject.Find(r);
            if (!c) { Debug.LogWarning("[Cabinas] No se encontro " + r); continue; }
            if (Ajustar(c.transform)) n++;
        }
        if (n > 0) Debug.Log($"[Cabinas] {n} cabinas ajustadas al jugador: {Alto} m de alto, {Ancho} m de ancho, telefono a {CentroTelefono} m. Guarda la escena (Ctrl+S).");
    }

    static bool EsPiezaModelo(Transform t)
    {
        foreach (string p in PiezasModelo) if (t.name.StartsWith(p)) return true;
        return false;
    }

    static bool Ajustar(Transform cabina)
    {
        Transform cuerpo = cabina.Find("Cabina");
        if (!cuerpo) return false;
        Transform tel = cabina.Find("Telefono");

        Bounds antes = CabinaSotanoSetup.Limites(cuerpo);
        Bounds telAntes = tel ? CabinaSotanoSetup.Limites(tel) : default;
        // Que tan pegado estaba el telefono a la pared de atras (para dejarlo igual)
        int eje = 0; float signo = 1f, holgura = 0f;
        Vector3 telMundo = tel ? tel.lossyScale : Vector3.one;
        if (tel)
        {
            Vector3 d = telAntes.center - antes.center;
            eje = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0 : 2;
            signo = Mathf.Sign(d[eje]);
            holgura = signo > 0 ? antes.max[eje] - telAntes.max[eje] : telAntes.min[eje] - antes.min[eje];
        }

        // 1) Mismas proporciones en todas las piezas del modelo (sin aplastados sueltos)
        foreach (Transform h in cabina)
        {
            if (!EsPiezaModelo(h)) continue;
            if (h.localScale == Vector3.one) continue;
            Undo.RecordObject(h, "Cabina proporciones");
            h.localScale = Vector3.one;
        }

        // 2) Escala de la raiz para que el cuerpo mida Alto x Ancho
        Undo.RecordObject(cabina, "Cabina tamano");
        Bounds nat = CabinaSotanoSetup.Limites(cuerpo);
        Vector3 s = cabina.localScale;
        float fxz = Ancho / Mathf.Max(nat.size.x, nat.size.z);
        float fy = Alto / nat.size.y;
        cabina.localScale = new Vector3(s.x * fxz, s.y * fy, s.z * fxz);

        // 3) Mismo piso y mismo centro que antes
        Bounds nuevo = CabinaSotanoSetup.Limites(cuerpo);
        cabina.position += new Vector3(antes.center.x - nuevo.center.x, antes.min.y - nuevo.min.y, antes.center.z - nuevo.center.z);
        nuevo = CabinaSotanoSetup.Limites(cuerpo);

        // 4) Telefono: tamano real (sin deformar) y a la altura de la cara, pegado a la misma pared
        if (tel)
        {
            Undo.RecordObject(tel, "Telefono tamano");
            // Escala mundial uniforme deseada -> local (la raiz solo gira en Y y tiene X = Z)
            float u = telMundo.y * AltoTelefono / telAntes.size.y;
            Vector3 pl = cabina.lossyScale;
            tel.localScale = new Vector3(u / pl.x, u / pl.y, u / pl.z);
            Bounds tb = CabinaSotanoSetup.Limites(tel);
            Vector3 mover = new Vector3(0f, nuevo.min.y + CentroTelefono - tb.center.y, 0f);
            mover[eje] = signo > 0 ? (nuevo.max[eje] - holgura) - tb.max[eje] : (nuevo.min[eje] + holgura) - tb.min[eje];
            tel.position += mover;
        }

        EditorSceneManager.MarkSceneDirty(cabina.gameObject.scene);
        return true;
    }

}
