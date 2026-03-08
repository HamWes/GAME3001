using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TileManager : MonoBehaviour
{
    private const float GrassCost = 1f;
    private const float MudCost = 3f;
    private const float WaterCost = 5f;

    [Header("Prefabs")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private GameObject targetPrefab;

    [Header("Grid")]
    [SerializeField] private LayerMask tileLayer;
    [SerializeField] private Transform tileContainer;
    [SerializeField] private ObjectPool tileObjectPool;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float spacing = 1f;
    public int tileWidth = 10;
    public int tileHeight = 10;

    [Header("Terrain")]
    [SerializeField] private bool useRandomTerrain = true;
    [SerializeField, Range(0f, 1f)] private float mudChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float waterChance = 0.10f;
    [SerializeField, Range(0f, 1f)] private float obstacleChance = 0.10f;

    [Header("Rubric")]
    [SerializeField] private HeuristicType heuristicType = HeuristicType.Euclidean;
    [SerializeField] private bool debugViewActive = false;
    [SerializeField] private float agentMoveSpeed = 4f;

    [Header("Colors")]
    [SerializeField] private Color defaultColor = new Color(0.501f, 0.501f, 0f, 1f);
    [SerializeField] private Color mudColor = new Color(0.55f, 0.27f, 0.07f, 1f);
    [SerializeField] private Color waterColor = Color.cyan;
    [SerializeField] private Color wallColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color visitedColor = new Color(0.68f, 0.85f, 0.9f, 1f);
    [SerializeField] private Color pathColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color smoothedPathColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color debugBorderColor = Color.black;

    [Header("Extensions")]
    [SerializeField] private bool enablePathSmoothing = true;

    private Tile[,] tiles;
    private Node[,] nodes;

    private Node startNode;
    private Node endNode;
    private GameObject agentObject;
    private GameObject targetObject;

    private readonly List<Node> currentPath = new List<Node>();
    private readonly List<Node> currentSmoothedPath = new List<Node>();
    private readonly List<Node> currentVisited = new List<Node>();

    private float currentPathCost = Mathf.Infinity;

    private Camera mainCam;
    private Mouse mouse;
    private Ray ray;
    private GUIStyle debugLabelStyle;
    private Coroutine moveRoutine;

    private void Awake()
    {
        mainCam = Camera.main;
        mouse = Mouse.current;
        debugLabelStyle = new GUIStyle
        {
            normal = { textColor = Color.black },
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnTiles();
        }
    }

    private void Update()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        if (mouse == null)
        {
            mouse = Mouse.current;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.hKey.wasPressedThisFrame)
            {
                debugViewActive = !debugViewActive;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                ResetGrid();
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                RunPathfindingAndVisualize();
            }

            if (keyboard.mKey.wasPressedThisFrame)
            {
                StartAgentMovement();
            }
        }

        if (!debugViewActive || mouse == null || mainCam == null || nodes == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            TrySelectStartTile();
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            TrySelectGoalTile();
        }
    }

    private void OnGUI()
    {
        DrawControlsHud();

        if (!debugViewActive || nodes == null || mainCam == null)
        {
            return;
        }

        DrawTileCostLabels();
        DrawPathInfoHud();
    }

    private void OnDrawGizmos()
    {
        if (!debugViewActive || nodes == null)
        {
            return;
        }

        Gizmos.color = debugBorderColor;
        for (int x = 0; x < nodes.GetLength(0); x++)
        {
            for (int y = 0; y < nodes.GetLength(1); y++)
            {
                Node node = nodes[x, y];
                if (node == null || node.m_visualCube == null)
                {
                    continue;
                }

                Vector3 center = node.m_visualCube.transform.position + Vector3.up * 0.02f;
                Vector3 size = new Vector3(Mathf.Max(0.05f, spacing * 0.95f), 0.02f, Mathf.Max(0.05f, spacing * 0.95f));
                Gizmos.DrawWireCube(center, size);
            }
        }
    }

    public void SpawnTiles()
    {
        ClearTiles();
        DestroyMarkers();
        StopMovement();

        tiles = new Tile[tileWidth, tileHeight];
        nodes = null;
        startNode = null;
        endNode = null;
        ClearPathResults();

        for (int x = 0; x < tileWidth; x++)
        {
            for (int y = 0; y < tileHeight; y++)
            {
                Vector3 position = new Vector3(x * spacing, 0f, y * spacing);
                GameObject tileObject = SpawnTileObject(position);
                if (tileObject == null)
                {
                    continue;
                }

                tileObject.name = $"Node_{x}_{y}";

                Tile tile = tileObject.GetComponent<Tile>();
                if (tile == null)
                {
                    continue;
                }

                tile.SetDefaultColor(defaultColor);
                tile.Initialize(x, y);
                tiles[x, y] = tile;
            }
        }

        BuildNodeGraph();
        AssignTerrain();
        ApplyRandomObstacleLayout();
        RecalculateGoalMapping();
    }

    private GameObject SpawnTileObject(Vector3 position)
    {
        Transform parent = tileContainer != null ? tileContainer : transform;

        if (tileObjectPool != null)
        {
            return tileObjectPool.Get(position, Quaternion.identity, parent);
        }

        if (tilePrefab != null)
        {
            return Instantiate(tilePrefab, position, Quaternion.identity, parent);
        }

        return null;
    }

    private void BuildNodeGraph()
    {
        nodes = new Node[tileWidth, tileHeight];

        for (int x = 0; x < tileWidth; x++)
        {
            for (int y = 0; y < tileHeight; y++)
            {
                Tile tile = tiles[x, y];
                if (tile == null)
                {
                    continue;
                }

                nodes[x, y] = new Node(x, y, tile.gameObject);
            }
        }

        for (int x = 0; x < tileWidth; x++)
        {
            for (int y = 0; y < tileHeight; y++)
            {
                Node node = nodes[x, y];
                if (node == null)
                {
                    continue;
                }

                TryAddNeighbor(node, x - 1, y);
                TryAddNeighbor(node, x + 1, y);
                TryAddNeighbor(node, x, y - 1);
                TryAddNeighbor(node, x, y + 1);
                TryAddNeighbor(node, x - 1, y - 1);
                TryAddNeighbor(node, x - 1, y + 1);
                TryAddNeighbor(node, x + 1, y - 1);
                TryAddNeighbor(node, x + 1, y + 1);
            }
        }
    }

    private void TryAddNeighbor(Node node, int x, int y)
    {
        Node neighbor = GetNode(x, y);
        if (neighbor != null)
        {
            node.m_neighbors.Add(neighbor);
        }
    }

    private void AssignTerrain()
    {
        if (nodes == null)
        {
            return;
        }

        float specialThreshold = Mathf.Clamp01(mudChance + waterChance);

        for (int x = 0; x < nodes.GetLength(0); x++)
        {
            for (int y = 0; y < nodes.GetLength(1); y++)
            {
                Node node = nodes[x, y];
                if (node == null)
                {
                    continue;
                }

                node.m_isWalkable = true;
                node.m_previousNode = null;
                node.m_gCost = Mathf.Infinity;
                node.m_goalHeuristicCost = Mathf.Infinity;
                node.m_goalMappedCost = Mathf.Infinity;

                if (!useRandomTerrain)
                {
                    node.m_terrainCost = GrassCost;
                    SetTileColor(node, defaultColor);
                    continue;
                }

                float roll = Random.value;
                if (roll < mudChance)
                {
                    node.m_terrainCost = MudCost;
                    SetTileColor(node, mudColor);
                }
                else if (roll < specialThreshold)
                {
                    node.m_terrainCost = WaterCost;
                    SetTileColor(node, waterColor);
                }
                else
                {
                    node.m_terrainCost = GrassCost;
                    SetTileColor(node, defaultColor);
                }
            }
        }
    }

    private void ApplyRandomObstacleLayout()
    {
        if (nodes == null)
        {
            return;
        }

        for (int x = 0; x < nodes.GetLength(0); x++)
        {
            for (int y = 0; y < nodes.GetLength(1); y++)
            {
                Node node = nodes[x, y];
                if (node == null || !node.m_isWalkable)
                {
                    continue;
                }

                if (Random.value < obstacleChance)
                {
                    node.m_isWalkable = false;
                    SetTileColor(node, wallColor);
                }
            }
        }
    }

    private void TrySelectStartTile()
    {
        Node clickedNode = TryGetNodeFromMouse();
        if (clickedNode == null || !clickedNode.m_isWalkable)
        {
            return;
        }

        if (endNode != null && clickedNode == endNode)
        {
            Debug.Log("Start tile cannot be the same as the goal tile.");
            return;
        }

        StopMovement();
        startNode = clickedNode;
        SpawnMarker(ref agentObject, agentPrefab, startNode, Color.green, "Agent");
        ClearPathResults();
    }

    private void TrySelectGoalTile()
    {
        Node clickedNode = TryGetNodeFromMouse();
        if (clickedNode == null || !clickedNode.m_isWalkable)
        {
            return;
        }

        if (startNode != null && clickedNode == startNode)
        {
            Debug.Log("Goal tile cannot be the same as the start tile.");
            return;
        }

        StopMovement();
        endNode = clickedNode;
        SpawnMarker(ref targetObject, targetPrefab, endNode, Color.red, "Goal");
        ClearPathResults();
        RecalculateGoalMapping();
    }

    private void RunPathfindingAndVisualize()
    {
        if (startNode == null || endNode == null)
        {
            Debug.Log("Select both a start tile and a goal tile in Debug View before finding a path.");
            return;
        }

        StopMovement();
        ClearPathResults();

        List<Node> path = Pathfinder.FindPathAStar(startNode, endNode, nodes, heuristicType, out List<Node> visitedNodes, out float totalCost);

        currentVisited.AddRange(visitedNodes);
        currentPath.AddRange(path);
        currentPathCost = totalCost;

        for (int i = 0; i < currentVisited.Count; i++)
        {
            Node node = currentVisited[i];
            if (node == null || !node.m_isWalkable)
            {
                continue;
            }

            SetTileColor(node, visitedColor);
        }

        if (currentPath.Count == 0)
        {
            Debug.Log("No path found!");
            return;
        }

        VisualizePath(currentPath, pathColor);

        if (enablePathSmoothing)
        {
            List<Node> smoothedPath = SmoothPath(currentPath, nodes);
            currentSmoothedPath.AddRange(smoothedPath);
            VisualizePath(currentSmoothedPath, smoothedPathColor);
        }
    }

    private void StartAgentMovement()
    {
        if (startNode == null || endNode == null)
        {
            Debug.Log("Select start and goal tiles first.");
            return;
        }

        if (currentPath.Count == 0)
        {
            RunPathfindingAndVisualize();
        }

        if (currentPath.Count == 0)
        {
            return;
        }

        if (agentObject == null)
        {
            SpawnMarker(ref agentObject, agentPrefab, startNode, Color.green, "Agent");
        }

        StopMovement();
        moveRoutine = StartCoroutine(MoveAgentAlongPath(currentPath));
    }

    private IEnumerator MoveAgentAlongPath(List<Node> path)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Node node = path[i];
            if (node == null || node.m_visualCube == null || agentObject == null)
            {
                continue;
            }

            Vector3 targetPos = node.m_visualCube.transform.position + Vector3.up;
            while (Vector3.Distance(agentObject.transform.position, targetPos) > 0.01f)
            {
                agentObject.transform.position = Vector3.MoveTowards(agentObject.transform.position, targetPos, agentMoveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        moveRoutine = null;
    }

    private void StopMovement()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }
    }

    private void VisualizePath(List<Node> path, Color color)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Node node = path[i];
            if (node == null || !node.m_isWalkable)
            {
                continue;
            }

            SetTileColor(node, color);
        }
    }

    private List<Node> SmoothPath(List<Node> path, Node[,] allNodes)
    {
        if (path == null || path.Count < 3)
        {
            return path ?? new List<Node>();
        }

        List<Node> smoothed = new List<Node> { path[0] };
        int checkIndex = 0;

        while (checkIndex < path.Count - 1)
        {
            int farthest = checkIndex + 1;
            for (int i = checkIndex + 2; i < path.Count; i++)
            {
                if (HasLineOfSight(path[checkIndex], path[i], allNodes))
                {
                    farthest = i;
                }
            }

            smoothed.Add(path[farthest]);
            checkIndex = farthest;
        }

        return smoothed;
    }

    private bool HasLineOfSight(Node a, Node b, Node[,] allNodes)
    {
        if (a == null || b == null || allNodes == null)
        {
            return false;
        }

        int dx = Mathf.Abs(b.m_x - a.m_x);
        int dy = Mathf.Abs(b.m_y - a.m_y);
        int steps = Mathf.Max(dx, dy);
        if (steps == 0)
        {
            return true;
        }

        float xStep = (b.m_x - a.m_x) / (float)steps;
        float yStep = (b.m_y - a.m_y) / (float)steps;

        for (int i = 1; i <= steps; i++)
        {
            int checkX = Mathf.RoundToInt(a.m_x + xStep * i);
            int checkY = Mathf.RoundToInt(a.m_y + yStep * i);

            Node node = GetNode(checkX, checkY);
            if (node == null || !node.m_isWalkable)
            {
                return false;
            }
        }

        return true;
    }

    private void RecalculateGoalMapping()
    {
        if (nodes == null)
        {
            return;
        }

        for (int x = 0; x < nodes.GetLength(0); x++)
        {
            for (int y = 0; y < nodes.GetLength(1); y++)
            {
                Node node = nodes[x, y];
                if (node == null || !node.m_isWalkable || endNode == null)
                {
                    if (node != null)
                    {
                        node.m_goalHeuristicCost = Mathf.Infinity;
                        node.m_goalMappedCost = Mathf.Infinity;
                    }
                    continue;
                }

                float heuristicCost = Pathfinder.GetHeuristicCost(node, endNode, heuristicType);
                node.m_goalHeuristicCost = heuristicCost;
                node.m_goalMappedCost = heuristicCost * Mathf.Max(0.01f, node.m_terrainCost);
            }
        }
    }

    private void DrawTileCostLabels()
    {
        for (int x = 0; x < nodes.GetLength(0); x++)
        {
            for (int y = 0; y < nodes.GetLength(1); y++)
            {
                Node node = nodes[x, y];
                if (node == null || node.m_visualCube == null)
                {
                    continue;
                }

                string label;
                if (!node.m_isWalkable)
                {
                    label = "X";
                }
                else if (float.IsInfinity(node.m_goalMappedCost))
                {
                    label = "-";
                }
                else
                {
                    label = node.m_goalMappedCost.ToString("0.0");
                }

                Vector3 screenPos = mainCam.WorldToScreenPoint(node.m_visualCube.transform.position + Vector3.up * 0.2f);
                if (screenPos.z <= 0f)
                {
                    continue;
                }

                Rect rect = new Rect(screenPos.x - 18f, Screen.height - screenPos.y - 8f, 36f, 16f);
                GUI.Label(rect, label, debugLabelStyle);
            }
        }
    }

    private void DrawPathInfoHud()
    {
        string startText = startNode == null ? "None" : $"({startNode.m_x},{startNode.m_y})";
        string goalText = endNode == null ? "None" : $"({endNode.m_x},{endNode.m_y})";
        string pathLengthText = currentPath.Count > 0 ? currentPath.Count.ToString() : "N/A";
        string costText = float.IsInfinity(currentPathCost) ? "N/A" : currentPathCost.ToString("0.00");

        string hud =
            $"Debug View: ON\n" +
            $"Start: {startText}\n" +
            $"Goal: {goalText}\n" +
            $"Visited: {currentVisited.Count}\n" +
            $"Shortest Path Nodes: {pathLengthText}\n" +
            $"Shortest Path Cost: {costText}\n" +
            "Left Click : Select Start\nRight Click : Select Goal";

        GUI.Box(new Rect(10f, 110f, 240f, 150f), hud);
    }

    private void DrawControlsHud()
    {
        string debugState = debugViewActive ? "On" : "Off";
        string hud =
            $"Debug: {debugState}\n" +
            "H : Toggle Debug\n" +
            "F : Find Path\n" +
            "M : March\n" +
            "R : Reset";

        GUI.Box(new Rect(10f, 10f, 240f, 95f), hud);
    }

    private void ClearPathResults()
    {
        currentPath.Clear();
        currentSmoothedPath.Clear();
        currentVisited.Clear();
        currentPathCost = Mathf.Infinity;
        RestoreBaseTileColors();
    }

    private void RestoreBaseTileColors()
    {
        if (nodes == null)
        {
            return;
        }

        for (int x = 0; x < nodes.GetLength(0); x++)
        {
            for (int y = 0; y < nodes.GetLength(1); y++)
            {
                Node node = nodes[x, y];
                if (node == null)
                {
                    continue;
                }

                SetTileColor(node, node.m_isWalkable ? GetTerrainColor(node) : wallColor);
            }
        }
    }

    private Color GetTerrainColor(Node node)
    {
        if (Mathf.Approximately(node.m_terrainCost, MudCost))
        {
            return mudColor;
        }

        if (Mathf.Approximately(node.m_terrainCost, WaterCost))
        {
            return waterColor;
        }

        return defaultColor;
    }

    private void ResetGrid()
    {
        if (nodes == null)
        {
            return;
        }

        StopMovement();
        DestroyMarkers();

        startNode = null;
        endNode = null;

        AssignTerrain();
        ApplyRandomObstacleLayout();
        RecalculateGoalMapping();
        ClearPathResults();
    }

    private void SpawnMarker(ref GameObject markerObject, GameObject markerPrefab, Node node, Color fallbackColor, string fallbackName)
    {
        if (node == null || node.m_visualCube == null)
        {
            return;
        }

        Vector3 markerPosition = node.m_visualCube.transform.position + Vector3.up;

        if (markerObject == null)
        {
            if (markerPrefab != null)
            {
                markerObject = Instantiate(markerPrefab, markerPosition, Quaternion.identity);
            }
            else
            {
                markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                markerObject.name = fallbackName;
                markerObject.transform.localScale = Vector3.one * 0.6f;
                Renderer renderer = markerObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = fallbackColor;
                }
            }
        }

        markerObject.transform.position = markerPosition;
    }

    private void DestroyMarkers()
    {
        if (agentObject != null)
        {
            Destroy(agentObject);
            agentObject = null;
        }

        if (targetObject != null)
        {
            Destroy(targetObject);
            targetObject = null;
        }
    }

    private void SetTileColor(Node node, Color color)
    {
        if (node == null || node.m_visualCube == null)
        {
            return;
        }

        Tile tileComponent = node.m_visualCube.GetComponent<Tile>();
        if (tileComponent != null)
        {
            tileComponent.SetColor(color);
            return;
        }

        Renderer renderer = node.m_visualCube.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    private Node TryGetNodeFromMouse()
    {
        Vector2 screenPos = mouse.position.ReadValue();
        ray = mainCam.ScreenPointToRay(screenPos);

        int mask = tileLayer.value == 0 ? Physics.DefaultRaycastLayers : tileLayer.value;
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, mask))
        {
            return null;
        }

        Tile tile = hit.collider.GetComponent<Tile>();
        if (tile != null)
        {
            return GetNode(tile.x_pos, tile.z_pos);
        }

        return FindNodeByCube(hit.collider.gameObject);
    }

    private Node FindNodeByCube(GameObject cube)
    {
        if (cube == null || nodes == null)
        {
            return null;
        }

        for (int x = 0; x < nodes.GetLength(0); x++)
        {
            for (int y = 0; y < nodes.GetLength(1); y++)
            {
                Node node = nodes[x, y];
                if (node != null && node.m_visualCube == cube)
                {
                    return node;
                }
            }
        }

        return null;
    }

    private void ClearTiles()
    {
        if (tiles == null)
        {
            return;
        }

        for (int x = 0; x < tiles.GetLength(0); x++)
        {
            for (int y = 0; y < tiles.GetLength(1); y++)
            {
                Tile tile = tiles[x, y];
                if (tile == null)
                {
                    continue;
                }

                if (tileObjectPool != null)
                {
                    tileObjectPool.Return(tile.gameObject);
                }
                else
                {
                    Destroy(tile.gameObject);
                }
            }
        }
    }

    public Tile GetTile(Vector2Int pos)
    {
        return GetTile(pos.x, pos.y);
    }

    public Tile GetTile(int x, int y)
    {
        if (tiles == null || x < 0 || x >= tileWidth || y < 0 || y >= tileHeight)
        {
            return null;
        }

        return tiles[x, y];
    }

    private Node GetNode(int x, int y)
    {
        if (nodes == null || x < 0 || x >= tileWidth || y < 0 || y >= tileHeight)
        {
            return null;
        }

        return nodes[x, y];
    }
}
