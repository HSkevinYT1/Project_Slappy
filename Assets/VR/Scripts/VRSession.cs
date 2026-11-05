using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

// Inicia el visor cuando una escena tiene un rig de VR (lo llama VRJoystickMovement).
// Las escenas sin rig no tocan OpenXR, asi que un visor desconectado no puede afectarlas.
public static class VRSession
{
    private static bool quitHandled;

    public static void Start()
    {
        XRManagerSettings xr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (xr == null || xr.isInitializationComplete) return;

#if UNITY_EDITOR
        // Si el software del visor pide salir (ej: Link cerrado), OpenXR terminaria el Play.
        // En el editor se sigue jugando sin visor. En una build se deja salir, como cualquier juego VR
        if (!quitHandled)
        {
            OpenXRRuntime.wantsToQuit += KeepPlaying;
            quitHandled = true;
        }
#endif

        xr.InitializeLoaderSync();
        if (xr.activeLoader == null)
        {
            Debug.LogWarning("VRSession: no se encuentra el visor. Comprueba que Link este abierto dentro del visor.");
            return;
        }

        xr.StartSubsystems();
        Application.quitting += Stop;
    }

    private static bool KeepPlaying()
    {
        Debug.LogWarning("VRSession: el visor no esta disponible, se sigue sin VR. Comprueba que Link este abierto dentro del visor.");
        return false;
    }

    private static void Stop()
    {
        Application.quitting -= Stop;

        XRManagerSettings xr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (xr == null || !xr.isInitializationComplete) return;

        xr.StopSubsystems();
        xr.DeinitializeLoader();
    }
}
