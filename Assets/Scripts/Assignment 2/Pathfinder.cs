using System.Collections.Generic;
using UnityEngine;

public enum HeuristicType
{
    Manhattan,
    Euclidean
}

public static class Pathfinder
{
    private const float DiagonalCost = 1.41421356f;

    public static List<Node> FindPath(Node startNode, Node endNode, Node[,] allNodes)
    {
        List<Node> visitedNodes;
        return FindPath(startNode, endNode, allNodes, out visitedNodes);
    }

    public static List<Node> FindPath(Node startNode, Node endNode, Node[,] allNodes, out List<Node> visitedNodes)
    {
        visitedNodes = new List<Node>();
        if (startNode == null || endNode == null || allNodes == null)
        {
            return new List<Node>();
        }

        List<Node> unvisited = new List<Node>();
        for (int x = 0; x < allNodes.GetLength(0); x++)
        {
            for (int y = 0; y < allNodes.GetLength(1); y++)
            {
                Node node = allNodes[x, y];
                if (node == null)
                {
                    continue;
                }

                node.m_gCost = Mathf.Infinity;
                node.m_previousNode = null;
                unvisited.Add(node);
            }
        }

        if (!startNode.m_isWalkable || !endNode.m_isWalkable)
        {
            return new List<Node>();
        }

        startNode.m_gCost = 0f;

        while (unvisited.Count > 0)
        {
            Node currentNode = null;
            for (int i = 0; i < unvisited.Count; i++)
            {
                Node node = unvisited[i];
                if (currentNode == null || node.m_gCost < currentNode.m_gCost)
                {
                    currentNode = node;
                }
            }

            if (currentNode == null || float.IsInfinity(currentNode.m_gCost))
            {
                break;
            }

            unvisited.Remove(currentNode);
            visitedNodes.Add(currentNode);

            if (currentNode == endNode)
            {
                break;
            }

            for (int i = 0; i < currentNode.m_neighbors.Count; i++)
            {
                Node neighbor = currentNode.m_neighbors[i];
                if (!unvisited.Contains(neighbor) || !neighbor.m_isWalkable)
                {
                    continue;
                }

                bool isDiagonal = neighbor.m_x != currentNode.m_x && neighbor.m_y != currentNode.m_y;
                float baseCost = isDiagonal ? DiagonalCost : 1f;
                float edgeCost = baseCost * Mathf.Max(0.01f, neighbor.m_terrainCost);
                float tentativeCost = currentNode.m_gCost + edgeCost;
                if (tentativeCost < neighbor.m_gCost)
                {
                    neighbor.m_gCost = tentativeCost;
                    neighbor.m_previousNode = currentNode;
                }
            }
        }

        List<Node> path = new List<Node>();
        Node step = endNode;
        while (step != null)
        {
            path.Insert(0, step);
            step = step.m_previousNode;
        }

        if (path.Count == 0 || path[0] != startNode)
        {
            return new List<Node>();
        }

        return path;
    }

    public static List<Node> FindPathOnGraph(Node startNode, Node endNode, List<Node> graphNodes, out List<Node> visitedNodes)
    {
        visitedNodes = new List<Node>();
        if (startNode == null || endNode == null || graphNodes == null || graphNodes.Count == 0)
        {
            return new List<Node>();
        }

        List<Node> unvisited = new List<Node>(graphNodes.Count);
        for (int i = 0; i < graphNodes.Count; i++)
        {
            Node node = graphNodes[i];
            if (node == null)
            {
                continue;
            }

            node.m_gCost = Mathf.Infinity;
            node.m_previousNode = null;
            unvisited.Add(node);
        }

        startNode.m_gCost = 0f;

        while (unvisited.Count > 0)
        {
            Node currentNode = null;
            for (int i = 0; i < unvisited.Count; i++)
            {
                Node node = unvisited[i];
                if (currentNode == null || node.m_gCost < currentNode.m_gCost)
                {
                    currentNode = node;
                }
            }

            if (currentNode == null || float.IsInfinity(currentNode.m_gCost))
            {
                break;
            }

            unvisited.Remove(currentNode);
            visitedNodes.Add(currentNode);

            if (currentNode == endNode)
            {
                break;
            }

            for (int i = 0; i < currentNode.m_neighbors.Count; i++)
            {
                Node neighbor = currentNode.m_neighbors[i];
                if (neighbor == null || !neighbor.m_isWalkable || !unvisited.Contains(neighbor))
                {
                    continue;
                }

                float edgeCost = GetEuclideanCost(currentNode, neighbor);
                float tentativeCost = currentNode.m_gCost + edgeCost;
                if (tentativeCost < neighbor.m_gCost)
                {
                    neighbor.m_gCost = tentativeCost;
                    neighbor.m_previousNode = currentNode;
                }
            }
        }

        List<Node> path = new List<Node>();
        Node step = endNode;
        while (step != null)
        {
            path.Insert(0, step);
            step = step.m_previousNode;
        }

        if (path.Count == 0 || path[0] != startNode)
        {
            return new List<Node>();
        }

        return path;
    }

    public static float GetEuclideanCost(Node a, Node b)
    {
        float dx = b.m_x - a.m_x;
        float dy = b.m_y - a.m_y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    public static float GetHeuristicCost(Node fromNode, Node goalNode, HeuristicType heuristicType)
    {
        if (fromNode == null || goalNode == null)
        {
            return Mathf.Infinity;
        }

        float dx = Mathf.Abs(goalNode.m_x - fromNode.m_x);
        float dy = Mathf.Abs(goalNode.m_y - fromNode.m_y);
        if (heuristicType == HeuristicType.Manhattan)
        {
            return dx + dy;
        }

        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    public static List<Node> FindPathAStar(
        Node startNode,
        Node goalNode,
        Node[,] allNodes,
        HeuristicType heuristicType,
        out List<Node> visitedNodes,
        out float totalCost)
    {
        visitedNodes = new List<Node>();
        totalCost = Mathf.Infinity;

        if (startNode == null || goalNode == null || allNodes == null || !startNode.m_isWalkable || !goalNode.m_isWalkable)
        {
            return new List<Node>();
        }

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        for (int x = 0; x < allNodes.GetLength(0); x++)
        {
            for (int y = 0; y < allNodes.GetLength(1); y++)
            {
                Node node = allNodes[x, y];
                if (node == null)
                {
                    continue;
                }

                node.m_gCost = Mathf.Infinity;
                node.m_previousNode = null;
            }
        }

        startNode.m_gCost = 0f;
        startNode.m_goalHeuristicCost = GetHeuristicCost(startNode, goalNode, heuristicType);
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            float currentFCost = currentNode.m_gCost + GetHeuristicCost(currentNode, goalNode, heuristicType);

            for (int i = 1; i < openSet.Count; i++)
            {
                Node candidate = openSet[i];
                float candidateFCost = candidate.m_gCost + GetHeuristicCost(candidate, goalNode, heuristicType);
                if (candidateFCost < currentFCost || (Mathf.Approximately(candidateFCost, currentFCost) && candidate.m_gCost < currentNode.m_gCost))
                {
                    currentNode = candidate;
                    currentFCost = candidateFCost;
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);
            visitedNodes.Add(currentNode);

            if (currentNode == goalNode)
            {
                totalCost = goalNode.m_gCost;
                return ReconstructPath(startNode, goalNode);
            }

            for (int i = 0; i < currentNode.m_neighbors.Count; i++)
            {
                Node neighbor = currentNode.m_neighbors[i];
                if (neighbor == null || !neighbor.m_isWalkable || closedSet.Contains(neighbor))
                {
                    continue;
                }

                bool isDiagonal = neighbor.m_x != currentNode.m_x && neighbor.m_y != currentNode.m_y;
                float baseCost = isDiagonal ? DiagonalCost : 1f;
                float movementCost = baseCost * Mathf.Max(0.01f, neighbor.m_terrainCost);
                float tentativeGCost = currentNode.m_gCost + movementCost;

                if (tentativeGCost >= neighbor.m_gCost)
                {
                    continue;
                }

                neighbor.m_previousNode = currentNode;
                neighbor.m_gCost = tentativeGCost;
                neighbor.m_goalHeuristicCost = GetHeuristicCost(neighbor, goalNode, heuristicType);

                if (!openSet.Contains(neighbor))
                {
                    openSet.Add(neighbor);
                }
            }
        }

        return new List<Node>();
    }

    private static List<Node> ReconstructPath(Node startNode, Node goalNode)
    {
        List<Node> path = new List<Node>();
        Node step = goalNode;

        while (step != null)
        {
            path.Insert(0, step);
            step = step.m_previousNode;
        }

        if (path.Count == 0 || path[0] != startNode)
        {
            return new List<Node>();
        }

        return path;
    }
}
