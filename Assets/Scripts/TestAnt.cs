using UnityEngine;

public class TestAnt {

    public bool HasFood { get; set; }

    public Vector2Int StartCell { get; set; }

    public Vector2Int CurrentCell { get; set; }

    public TestAnt(Vector2Int start) {

        StartCell = CurrentCell = start;
    }

    public void PlaceAt(Vector2Int newStart) {

        StartCell = CurrentCell = newStart;
    }
}
