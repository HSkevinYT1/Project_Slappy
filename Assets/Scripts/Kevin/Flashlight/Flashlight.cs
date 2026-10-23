
using UnityEngine;

public class Flashlight : MonoBehaviour
{
    public GameObject lightObject;
    public AudioClip lightSound;
    private AudioSource source;

    public float battery = 100f;

    void Start()
    {
        source = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
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
