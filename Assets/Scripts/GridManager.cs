using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Størrelse")]
    public int columns = 3;
    public int rows = 3;
    public float cellSize = 2f;

    [Header("Visuals (Valgfritt)")]
    public GameObject tilePrefab;

    private GameObject[,] gridArray;

    void Start()
    {
        gridArray = new GameObject[columns, rows];
        GenerateGridVisuals();

        // Automatisk registrer eksisterende minions på brettet ved oppstart
        MinionHealth[] existingMinions = FindObjectsByType<MinionHealth>(FindObjectsSortMode.None);
        foreach (var minion in existingMinions)
        {
            Vector2Int idx = GetGridIndexFromWorldPosition(minion.transform.position);
            if (idx.x >= 0 && idx.x < columns && idx.y >= 0 && idx.y < rows)
            {
                gridArray[idx.x, idx.y] = minion.gameObject;
                Debug.Log("Registrerte " + minion.gameObject.name + " på rute " + idx);
            }
        }
    }

    void GenerateGridVisuals()
    {
        if (tilePrefab == null) return;

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Vector3 worldPos = GetWorldPosition(x, y);
                Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
            }
        }
    }

    public Vector3 GetWorldPosition(int x, int y)
    {
        float startX = -((columns * cellSize) / 2f) + (cellSize / 2f);
        float startY = -((rows * cellSize) / 2f) + (cellSize / 2f);

        return new Vector3(startX + (x * cellSize), startY + (y * cellSize), 0);
    }

    public Vector2Int GetGridIndexFromWorldPosition(Vector3 worldPosition)
    {
        float startX = -((columns * cellSize) / 2f) + (cellSize / 2f);
        float startY = -((rows * cellSize) / 2f) + (cellSize / 2f);

        int x = Mathf.RoundToInt((worldPosition.x - startX) / cellSize);
        int y = Mathf.RoundToInt((worldPosition.y - startY) / cellSize);

        x = Mathf.Clamp(x, 0, columns - 1);
        y = Mathf.Clamp(y, 0, rows - 1);

        return new Vector2Int(x, y);
    }

    public GameObject GetMinionAt(int x, int y)
    {
        if (x >= 0 && x < columns && y >= 0 && y < rows)
        {
            return gridArray[x, y];
        }
        return null;
    }

    public bool PlaceMinion(GameObject minion, int x, int y)
    {
        // Først: Rydd opp og fjern minionen fra dens gamle posisjon i rutenettet
        for (int ix = 0; ix < columns; ix++)
        {
            for (int iy = 0; iy < rows; iy++)
            {
                if (gridArray[ix, iy] == minion)
                {
                    gridArray[ix, iy] = null;
                }
            }
        }

        // Deretter plasserer vi den på den nye ruten og oppdaterer posisjonen
        gridArray[x, y] = minion;
        minion.transform.position = GetWorldPosition(x, y);
        return true;
    }
}