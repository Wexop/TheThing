using System.Linq;
using StaticNetcodeLib;
using Unity.Netcode;
using UnityEngine;

namespace TheThing.Scripts;

[StaticNetcode]
public class NetworkThing
{
    public static ThingEnemyAI GetThingEnemyAI(ulong networkId)
    {
        var objects = Object.FindObjectsByType<ThingEnemyAI>(FindObjectsSortMode.None).ToList();
        var objectFound = objects.Find(e => e.NetworkObjectId == networkId);

        if (objectFound == null)
        {
            Debug.LogError($"ROLLER BALL NOT FOUND {networkId}");
        }
        else
        {
            return objectFound;
        }
        
        return null;
    }

    [ServerRpc]
    public static void SetPlayerIdServerRpc(ulong networkId, ulong playerId)
    {
        SetPlayerIdClientRpc(networkId, playerId);
    }

    [ClientRpc]
    public static void SetPlayerIdClientRpc(ulong networkId, ulong playerId)
    {
        var thing = GetThingEnemyAI(networkId);
        if (thing)
        {
            thing.OnSetPlayerId(playerId);
        }
    }
    
}