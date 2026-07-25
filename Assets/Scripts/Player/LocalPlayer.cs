using System;
public static class LocalPlayer
{
    public static NetworkPlayerController Controller { get; private set; }
    public static event Action<NetworkPlayerController> Ready;

    public static void Register(NetworkPlayerController controller)
    {
        Controller = controller;
        Ready?.Invoke(controller);
    }

    // Called by NetworkPlayerController.OnNetworkDespawn so a stale
    // reference doesn't linger past a disconnect/scene reload.
    public static void Clear(NetworkPlayerController controller)
    {
        if (Controller == controller)
        {
            Controller = null;
        }
    }
}