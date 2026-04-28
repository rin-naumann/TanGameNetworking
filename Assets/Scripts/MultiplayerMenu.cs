using UnityEngine;
using Unity.Netcode;

public class MultiplayerMenu : MonoBehaviour
{
    public void StartHost() {
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient() {
        NetworkManager.Singleton.StartClient();
    }

    public void StartServer() {
        NetworkManager.Singleton.StartServer();
    }
}
