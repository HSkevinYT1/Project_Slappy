using UnityEngine;

// Va en la raiz de Player_VR: basta con colocar el prefab activo en una escena para jugarla en VR.
// Apaga el Player normal si tambien esta en la escena y muestra los menus en el visor.
public class VRPlayerSetup : MonoBehaviour
{
    void Awake()
    {
        PlayerMovement pcPlayer = FindFirstObjectByType<PlayerMovement>();
        if (pcPlayer != null) pcPlayer.gameObject.SetActive(false);

        PlayerInteractor interactor = GetComponentInChildren<PlayerInteractor>();
        VRHud hud = new GameObject("VR_HUD").AddComponent<VRHud>();
        hud.Setup(interactor.transform, interactor.pointer);
    }
}
