
using UnityEngine;

public class Flashlight : MonoBehaviour
{
    public GameObject lightObject;
    public AudioClip lightSound;
    private AudioSource source;

    public float battery = 100f;
    public float maxBattery = 100f;
    // Energia que gasta por segundo encendida
    public float drainPerSecond = 2f;

    public float BatteryPercent => maxBattery > 0f ? Mathf.Clamp01(battery / maxBattery) : 0f;

    private Grabbable grab;
    private PlayerInteractor interactor;

    void Start()
    {
        source = GetComponent<AudioSource>();
        grab = GetComponentInParent<Grabbable>();
        interactor = FindFirstObjectByType<PlayerInteractor>();
    }

    void Update()
    {
        // Solo se puede prender/apagar si el jugador la tiene en la mano
        if (GameInput.Flashlight && IsHeld())
        {
            LightManager();
        }

        // Descargar bateria
        if (lightObject.activeSelf)
        {
            battery -= drainPerSecond * Time.deltaTime;

            if (battery <= 0)
            {
                battery = 0;
                lightObject.SetActive(false);
            }
        }
    }

    // La llaman las pilas (Bateria.Recoger)
    public void Recharge(float amount)
    {
        battery = Mathf.Min(maxBattery, battery + amount);
    }

    bool IsHeld()
    {
        // Si la linterna no se puede agarrar, se asume que siempre esta en la mano
        if (grab == null) return true;
        return interactor != null && interactor.Held == grab;
    }

    void LightManager()
    {
        if (lightObject.activeSelf)
        {
            lightObject.SetActive(false);
        }
        else
        {
            if (battery > 0)
            {
                lightObject.SetActive(true);
            }
        }

        if (source != null)
        {
            if (lightSound != null)
            {
                source.PlayOneShot(lightSound);
            }
        }
    }
}
