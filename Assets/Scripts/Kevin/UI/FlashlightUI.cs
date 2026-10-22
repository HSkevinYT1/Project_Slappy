
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FlashlightUI : MonoBehaviour
{
    public Flashlight flashlight;
    public Image batteryFill;
    public TextMeshProUGUI batteryText;

    public float maxBattery = 100f;
    public Color fullColor = new Color(0.4f, 1f, 0.4f);
    public Color lowColor = new Color(1f, 0.25f, 0.2f);

    void Start()
    {
        if (flashlight == null)
        {
            flashlight = FindFirstObjectByType<Flashlight>();
        }
    }

    void Update()
    {
        if (flashlight == null) return;

        float percent = Mathf.Clamp01(flashlight.battery / maxBattery);

        // Barra de bateria
        if (batteryFill != null)
        {
            batteryFill.fillAmount = percent;
            batteryFill.color = Color.Lerp(lowColor, fullColor, percent);
        }

        if (batteryText != null)
        {
            batteryText.text = Mathf.CeilToInt(percent * 100f) + "%";
        }


    }
}
