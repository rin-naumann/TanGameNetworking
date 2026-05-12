using TMPro;
using UnityEngine;

public class PlayerCounter : MonoBehaviour
{
    [SerializeField] public TMP_Text PlayerCountText;

    void Update()
    {
        // Find all objects with the "Player" tag every frame
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        int playerCount = players.Length;

        // Update TMP text
        PlayerCountText.text = "Players: " + playerCount;
    }
}
