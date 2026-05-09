using UnityEngine;
using Unity.Netcode;
using TMPro;

public class MultiplayerMenu : MonoBehaviour
{
    public TMP_Text playerCountText;
    public GameObject[] playerCountObjects;
    public int playerCount;
    public GameObject menuUI;

    void Update() {
        playerCountObjects = GameObject.FindGameObjectsWithTag("Player");
        playerCount = playerCountObjects.Length;
        playerCountText.text = "Players: " + playerCount;
    }

    public void StartHost() {
        NetworkManager.Singleton.StartHost();
        menuUI.SetActive(false);
    }

    public void StartClient() {
        NetworkManager.Singleton.StartClient();
        menuUI.SetActive(false);
    }

    public void StartServer() {
        NetworkManager.Singleton.StartServer();
        menuUI.SetActive(false);
    }
}
