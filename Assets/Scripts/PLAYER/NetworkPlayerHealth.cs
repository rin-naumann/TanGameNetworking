using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerHealth : NetworkBehaviour
{
    public int maxHealth = 1000;
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Vector3 damageTextOffset = new Vector3(0f, 2.2f, 0f);

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

        if (IsOwner) LocalHealthBar.Instance.BindToPlayer(this);
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
        int finaldmg = Mathf.Min(damageVal, currentHealth.Value);
        currentHealth.Value -= finaldmg;
        SpawnDamageTextClientRpc(finaldmg);
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

    // Runs on every client — purely visual, no NetworkObject needed
    [ClientRpc]
    private void SpawnDamageTextClientRpc(int amount)
    {
        Vector3 spawnPos = transform.position + damageTextOffset;

        // Small random X offset so stacked hits don't overlap
        spawnPos.x += Random.Range(-0.3f, 0.3f);

        GameObject obj = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);
        obj.GetComponent<DamageText>().Setup(amount);
    }
}
