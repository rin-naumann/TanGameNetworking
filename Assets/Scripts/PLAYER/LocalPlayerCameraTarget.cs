using UnityEngine;
using Unity.Netcode;

public class LocalPlayerCameraTarget : NetworkBehaviour
{

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        TopDownCameraFollow camFollow = Camera.main.GetComponent<TopDownCameraFollow>();
        if (camFollow != null )
        {
            camFollow.setTarget(transform);
        }
    }
}
