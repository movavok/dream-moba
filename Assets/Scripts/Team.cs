using UnityEngine;
using Unity.Netcode;

public class Team : NetworkBehaviour
{
    public NetworkVariable<short> TeamId = new NetworkVariable<short>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        TeamId.Value = (short)((OwnerClientId % 2) + 1);

        Debug.Log("Player " + OwnerClientId + " Team: " + TeamId.Value);
    }
}
