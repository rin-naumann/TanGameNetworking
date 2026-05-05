using UnityEngine;
using Unity.Netcode;

public class SpawnpointManager : NetworkBehaviour
{
    private static int nextSpawnIndex;

    public override void OnNetworkSpawn()
    {
        if(!IsServer) return;
        GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("Spawnpoint");
        if (spawnPointObjects.Length == 0) return;

        Transform nextSpawnPoint = spawnPointObjects[nextSpawnIndex].transform;
        CharacterController characterController = GetComponent<CharacterController>();

        if (characterController != null ) characterController.enabled = false;
        transform.position = nextSpawnPoint.position;
        transform.rotation = nextSpawnPoint.rotation;
        if (characterController != null ) characterController.enabled = true;

        if (nextSpawnIndex < spawnPointObjects.Length) nextSpawnIndex++;
        else nextSpawnIndex = 0;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
