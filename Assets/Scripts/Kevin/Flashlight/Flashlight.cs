
using UnityEngine;

public class Flashlight : MonoBehaviour
{
    public GameObject lightObject;
    public AudioClip lightSound;
    private AudioSource source;

    public float battery = 100f;

    private GrabInteractable grab;
    private PlayerInteractor interactor;

    void Start()
    {
        source = GetComponent<AudioSource>();
        grab = GetComponentInParent<GrabInteractable>();
        interactor = FindFirstObjectByType<PlayerInteractor>();
    }

    void Update()
    {
        // Solo se puede prender/apagar si el jugador la tiene en la mano
        if (Input.GetMouseButtonDown(1) && IsHeld())
        {
            LightManager();
        }

        // Descargar bateria
        if (lightObject.activeSelf)
        {
            battery -= 2f * Time.deltaTime;

            if (battery <= 0)
            {
                battery = 0;
                lightObject.SetActive(false);
            }
        }
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
