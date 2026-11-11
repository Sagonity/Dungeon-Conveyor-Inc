using UnityEngine;

public class DraggableMinion : MonoBehaviour
{
    private Camera mainCamera;
    private GridManager gridManager;
    private GameController gameController;
    private MinionStats stats;
    private Vector3 startPosition;
    private bool isDragging = false;
    private Vector3 mouseOffset;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        gridManager = Object.FindFirstObjectByType<GridManager>();
        gameController = Object.FindFirstObjectByType<GameController>();
        stats = GetComponent<MinionStats>();
        startPosition = transform.position;
    }

    void OnMouseDown()
    {
        // Ikke la spilleren dra minions mens en meny er åpen
        if (gameController != null && !gameController.IsGameplayActive) return;

        isDragging = true;
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        mouseOffset = transform.position - mouseWorldPos;
    }

    void OnMouseDrag()
    {
        if (!isDragging) return;

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        transform.position = mouseWorldPos + mouseOffset;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0);
    }

    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        if (gridManager != null)
        {
            // Finn hvilken rute vi slipper på
            Vector2Int targetGridIndex = gridManager.GetGridIndexFromWorldPosition(transform.position);
            
            // Sjekk hvem som står der fra før
            GameObject targetMinion = gridManager.GetMinionAt(targetGridIndex.x, targetGridIndex.y);

            int myDamage = stats != null ? stats.damage : 1;

            if (targetMinion != null && targetMinion != gameObject)
            {
                // DET STÅR EN FIGUR HER: Gi skade KUN til den som står der (target), ikke oss selv!
                MinionHealth targetHealth = targetMinion.GetComponent<MinionHealth>();

                if (targetHealth != null)
                {
                    targetHealth.TakeDamage(myDamage);
                    Debug.Log(gameObject.name + " angrep " + targetMinion.name + " for " + myDamage + " skade!");
                }

                // Send angriperen (objekt 1) tilbake til sin egen startposisjon
                transform.position = startPosition;
            }
            else
            {
                // LEDIG RUTE: Plasser minionen der og snapp den fast
                bool placed = gridManager.PlaceMinion(gameObject, targetGridIndex.x, targetGridIndex.y);

                if (placed)
                {
                    startPosition = transform.position; // Lås inn den nye posisjonen
                }
                else
                {
                    transform.position = startPosition;
                }
            }
        }
        else
        {
            transform.position = startPosition;
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        return mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }
}