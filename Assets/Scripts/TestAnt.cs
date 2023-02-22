using UnityEngine;

public class TestAnt {

    private Vector2Int startCell;

    private Vector2Int currentCell;

    public Vector2Int StartCell { get => startCell; set => startCell = value; }

    public Vector2Int CurrentCell { get => currentCell; set => currentCell = value; }

    public TestAnt(Vector2Int start) {

        startCell = currentCell = start;
    }

    public void PlaceAt(Vector2Int newStart) {

        startCell = currentCell = newStart;
    }
}
