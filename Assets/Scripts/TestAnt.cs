using UnityEngine;

public sealed class TestAnt {

    public bool HasFood { get; set; }

    public Vector2Int ColonyCell { get; private set; }

    public Vector2Int CurrentCell { get; set; }

    public TestAnt(Vector2Int colony) {

        ColonyCell = CurrentCell = colony;
    }

    public TestAnt(Vector2Int colony, Vector2Int start) {

        ColonyCell = colony;
        CurrentCell = start;
    }

    // Check if the Ant is (back) at home
    public bool IsHome() => CurrentCell == ColonyCell;
}
