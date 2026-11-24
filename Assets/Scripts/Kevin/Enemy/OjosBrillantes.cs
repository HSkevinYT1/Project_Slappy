using System.Collections.Generic;
using UnityEngine;

// Ojos que brillan en la oscuridad (Slappy, el enemigo de la Zona Roja): dos puntos claros y TENUES (para no competir
// con la luz del faro), con un pulso lento y un parpadeo de vez en cuando. Persiguiendo brillan un poco mas.
// Los ojos son esferas llamadas "Ojo brillante", hijas del hueso de la cabeza de cada pose del modelo (las pone
// Tools > Modelos > Ojos de Slappy, OjosSlappySetup); AnimacionesModelo las muestra solo con la pose visible.
public class OjosBrillantes : MonoBehaviour
{
    public const string NombreOjo = "Ojo brillante";

    [Tooltip("Color del brillo")]
    public Color color = new Color(0.82f, 0.92f, 1f);
    [Tooltip("Brillo normal")]
    public float brillo = 2.4f;
    [Tooltip("Brillo mientras persigue")]
    public float brilloPersiguiendo = 3.6f;
    [Tooltip("Segundos (al azar) entre parpadeos")]
    public Vector2 entreParpadeos = new Vector2(3f, 8f);

    readonly List<Transform> ojos = new List<Transform>();
    readonly List<Vector3> escalas = new List<Vector3>();
    readonly List<Renderer> rends = new List<Renderer>();
    MaterialPropertyBlock bloque;
    JefeIndigoAI ia;
    float proximoParpadeo, parpadeo = -1f, k;

    void Start()
    {
        ia = GetComponent<JefeIndigoAI>();
        bloque = new MaterialPropertyBlock();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == NombreOjo) { ojos.Add(t); escalas.Add(t.localScale); if (t.TryGetComponent(out Renderer r)) rends.Add(r); }
        proximoParpadeo = Time.time + Random.Range(entreParpadeos.x, entreParpadeos.y);
    }

    void LateUpdate()
    {
        if (ojos.Count == 0) return;
        bool caza = ia != null && ia.CurrentState == JefeIndigoAI.State.Chase;
        k = Mathf.MoveTowards(k, caza ? 1f : 0f, Time.deltaTime * 1.5f);

        // Parpadeo: se cierran (aplastados) un instante
        if (parpadeo < 0f && Time.time >= proximoParpadeo) parpadeo = 0f;
        float abierto = 1f;
        if (parpadeo >= 0f)
        {
            parpadeo += Time.deltaTime / 0.16f;
            abierto = Mathf.Abs(1f - Mathf.Clamp01(parpadeo) * 2f);
            if (parpadeo >= 1f)
            {
                parpadeo = -1f;
                proximoParpadeo = Time.time + Random.Range(entreParpadeos.x, entreParpadeos.y) * (caza ? 0.6f : 1f);
            }
        }
        for (int i = 0; i < ojos.Count; i++)
            if (ojos[i] != null) ojos[i].localScale = new Vector3(escalas[i].x, escalas[i].y * Mathf.Max(0.08f, abierto), escalas[i].z);

        // Pulso lento (mas rapido cuando persigue)
        float pulso = 0.85f + 0.15f * Mathf.Sin(Time.time * Mathf.Lerp(1.3f, 3.2f, k));
        Color e = color * (Mathf.Lerp(brillo, brilloPersiguiendo, k) * pulso);
        foreach (Renderer r in rends)
        {
            if (r == null) continue;
            r.GetPropertyBlock(bloque);
            bloque.SetColor("_EmissionColor", e);
            r.SetPropertyBlock(bloque);
        }
    }
}
