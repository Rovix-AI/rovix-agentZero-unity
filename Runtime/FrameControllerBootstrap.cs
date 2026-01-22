using UnityEngine;
public static class FrameControllerBootstrap
{
    private static bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartSDK()
    {
#if ALTTESTER
        if (_initialized)
            return;

        // Double-check if a FrameController already exists
        if (Object.FindObjectOfType<frameController>() != null)
        {
            Debug.Log("[FrameControllerBootstrap] FrameController already exists, skipping creation");
            _initialized = true;
            return;
        }

        _initialized = true;

        var go = new GameObject("FrameController");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<frameController>();
        Debug.Log("[FrameControllerBootstrap] FrameController created");
#endif
    }
}
