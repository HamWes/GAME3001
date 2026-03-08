using System.Collections.Generic;
using UnityEngine;

public class Node
{
    public int m_x;
    public int m_y;
    public bool m_isWalkable = true;
    public float m_terrainCost = 1f;
    public float m_goalHeuristicCost = Mathf.Infinity;
    public float m_goalMappedCost = Mathf.Infinity;
    public float m_gCost = Mathf.Infinity;
    public Node m_previousNode = null;
    public List<Node> m_neighbors = new List<Node>();
    public GameObject m_visualCube;

    public Node(int x, int y, GameObject cube)
    {
        m_x = x;
        m_y = y;
        m_visualCube = cube;
    }
}
