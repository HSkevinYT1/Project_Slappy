using UnityEngine;

// Ajusta la velocidad de la animacion de caminar (Animator del modelo) a lo rapido que se mueve el enemigo:
// quieto = la animacion se detiene; mas rapido = las piernas van mas rapido. Mide el movimiento real,
// asi funciona igual con el NavMeshAgent que en cinematicas (PresentacionEnemigo lo mueve con la IA apagada).
public class AnimacionCaminar : MonoBehaviour
{
    [Tooltip("Velocidad (m/s) a la que la caminata se ve natural a velocidad 1")]
    public float velocidadNormal = 1.8f;
    [Tooltip("Velocidad maxima de la animacion")]
    public float maximo = 3f;
    [Tooltip("Por debajo de esta velocidad (m/s) se considera quieto")]
    public float umbralQuieto = 0.08f;

    Animator anim;
    Vector3 ultima;
    float velocidad;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        ultima = transform.position;
    }

    void LateUpdate()
    {
        if (anim == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 delta = transform.position - ultima;
        delta.y = 0f;
        ultima = transform.position;
        float v = delta.magnitude / dt;
        // Un teletransporte (MandarLejos, reintentar) no cuenta como caminar
        if (v > 20f) v = 0f;
        velocidad = Mathf.Lerp(velocidad, v, 1f - Mathf.Exp(-10f * dt));
        anim.speed = velocidad < umbralQuieto ? 0f : Mathf.Min(maximo, velocidad / Mathf.Max(0.01f, velocidadNormal));
    }
}
