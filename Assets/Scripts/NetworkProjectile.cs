using UnityEngine;
using Unity.Netcode;
public class NetworkProjectile : NetworkBehaviour
{
    //Add this script to projectile
    [SerializeField] float speed = 12.5f;
    [SerializeField] float lifeTime = 10.0f;
    private float despawntime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            despawntime = Time.time + lifeTime;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsServer) { return; }
        transform.position += transform.forward * speed * Time.deltaTime;
        if (Time.time >= despawntime)
        {
            NetworkObject.Despawn();
        }
    }
}