using UnityEngine;

// Dice si un punto esta DENTRO de un recinto cerrado por paredes (ej: las Cloacas).
// Lanza rayos horizontales en 8 direcciones: si casi todos chocan con colliders del propio recinto, el punto esta
// encerrado por sus paredes. Fuera de las paredes (ej: el borde de agua que sobresale alrededor de las Cloacas)
// varios rayos se escapan sin tocar nada del recinto.
// Lo usan EnemigosZonasSetup (para que las estatuas no aparezcan por fuera), LlaveAleatoria y SalidaConLlave.
public static class ZonaCerrada
{
    static readonly RaycastHit[] hits = new RaycastHit[32];

    /// <param name="recinto">Raiz del recinto (sus hijos son las paredes)</param>
    /// <param name="punto">Punto a probar (normalmente a ras del piso)</param>
    /// <param name="altura">Altura sobre el punto a la que se lanzan los rayos</param>
    /// <param name="escapes">Direcciones que pueden no chocar (ej: la que sale justo por la puerta)</param>
    public static bool Dentro(Transform recinto, Vector3 punto, float altura = 1f, int escapes = 1, float distancia = 150f)
    {
        if (recinto == null) return true;
        Vector3 origen = punto + Vector3.up * altura;
        int sinChoque = 0;
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.PI * 0.25f;
            var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            if (!ChocaConRecinto(recinto, origen, dir, distancia) && ++sinChoque > escapes) return false;
        }
        return true;
    }

    static bool ChocaConRecinto(Transform recinto, Vector3 origen, Vector3 dir, float distancia)
    {
        int n = Physics.RaycastNonAlloc(origen, dir, hits, distancia, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            // Solo cuenta lo que es parte fija del recinto (no enemigos ni objetos con fisicas)
            if (c == null || !c.transform.IsChildOf(recinto) || c.attachedRigidbody != null) continue;
            return true;
        }
        return false;
    }
}
