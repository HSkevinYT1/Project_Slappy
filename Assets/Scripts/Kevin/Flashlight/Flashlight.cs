using UnityEngine;

public class Flashlight : MonoBehaviour
{
    public GameObject lightObject;
    public AudioClip lightSound;
    private AudioSource source;

    void Start()
    {
        source = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1)) LightManager();
    }

    void LightManager()
    {
        lightObject.SetActive(!lightObject.activeSelf);
        source.PlayOneShot(lightSound);
    }

}
