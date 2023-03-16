using UnityEngine;

public sealed class TestAnt {

    public bool HasFood { get; set; }

    public Vector2Int StartingCell { get; private set; }

    public Vector2Int CurrentCell { get; set; }

    public TestAnt(Vector2Int start) {

        StartingCell = CurrentCell = start;
    }

    public void ResetAt(Vector2Int newStart) {

        StartingCell = CurrentCell = newStart;
    }

    // Check if the Ant is (back) at home
    public bool IsHome() => CurrentCell == StartingCell;
}
