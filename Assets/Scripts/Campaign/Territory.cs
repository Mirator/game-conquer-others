using System.Collections.Generic;
using UnityEngine;

public enum TerritoryOwner { Player, Enemy }

// A single node on the campaign map. Plain data: position, who holds it, how
// strong its garrison is, and which other nodes it borders.
public sealed class Territory
{
    public int Id;
    public string Name;
    public Vector2 MapPosition;     // 2D layout; the map view maps this to world XZ
    public TerritoryOwner Owner;
    public int Garrison;            // defenders spawned when this node is assaulted
    public float DifficultyScale = 1f;
    public ArenaType Arena;
    public int RewardGold;
    public int Income;
    public int Threat;
    public readonly List<int> AdjacentIds = new();
}
