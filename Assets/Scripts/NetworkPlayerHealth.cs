using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerHealth : NetworkBehaviour
{
    [SerializeField] int maxHealth = 100;

    // Network synced heakth variable
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        100,                                        // Value
        NetworkVariableReadPermission.Everyone,     // Value Readability (Everyone allows all to view)
        NetworkVariableWritePermission.Server       // Value Writeability (Server allows only server to write)
        );

    public override void OnNetworkSpawn()
    {
        if (IsServer) currentHealth.Value = maxHealth;
        currentHealth.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int previousVal, int newVal)
    {
        Debug.Log($"{gameObject.name} health was changed from {previousVal} to {newVal}");
    }

    public void TakeDamage(int damageVal)
    {
        if(!IsServer) return;
        currentHealth.Value -= damageVal;
        currentHealth.Value = Mathf.Clamp(currentHealth.Value, 0, maxHealth);
        if (currentHealth.Value <= 0 ) Respawn();
    }

    private void Respawn()
    {
        // Health Reset
        currentHealth.Value = maxHealth;

        // Random Spawnpoint Select
        GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("Spawnpoint");
        int randIndex = Random.Range(0, spawnPointObjects.Length);
        Transform selectedSpawn = spawnPointObjects[randIndex].transform;
        CharacterController controller = GetComponent<CharacterController>();

        // Respawn Logic
        if ( controller != null ) controller.enabled = false;
        transform.position = selectedSpawn.position;
        transform.rotation = selectedSpawn.rotation;
        if ( controller != null ) controller.enabled = true;
    }

    private void OnDrawGizmos()
    {
        
    }
}
