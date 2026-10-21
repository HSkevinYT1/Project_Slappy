using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Funciones compartidas por los generadores de blocking (Backrooms, Cloacas).
public static class BlockingUtils
{
    // Crea un grupo con un cubo por caja (blocking normal, cada bloque se puede editar a mano).
    public static GameObject Grupo(Transform root, string nombre, Material mat, List<Bounds> cajas, bool colision, bool sombras)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(root, false);
        GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
        for (int k = 0; k < cajas.Count; k++)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = nombre + " " + k;
            c.transform.SetParent(go.transform, false);
            c.transform.localPosition = cajas[k].center;
            c.transform.localScale = cajas[k].size;
            c.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!sombras) c.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!colision) Object.DestroyImmediate(c.GetComponent<BoxCollider>());
            GameObjectUtility.SetStaticEditorFlags(c, (StaticEditorFlags)~0);
        }
        return go;
    }

    // Caja a partir de sus esquinas, mas comodo para escribir niveles a mano.
    public static Bounds Caja(float x0, float y0, float z0, float x1, float y1, float z1)
    {
        var b = new Bounds();
        b.SetMinMax(new Vector3(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Min(z0, z1)),
                    new Vector3(Mathf.Max(x0, x1), Mathf.Max(y0, y1), Mathf.Max(z0, z1)));
        return b;
    }

    public static Light Luz(Transform padre, string nombre, Vector3 pos, Color color, float rango, float intensidad)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = rango;
        l.intensity = intensidad;
        l.shadows = LightShadows.None;
        return l;
    }

    public static Material CrearMaterial(string nombre, Color color, bool emisivo, float suavidad = 0.5f)
    {
        string path = "Assets/Materials/" + nombre + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.color = color;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", suavidad);
        if (emisivo)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 2f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
