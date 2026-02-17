using UnityEngine;

public class Tile : MonoBehaviour, IPoolable
{
    public int x_pos;
    public int z_pos;
    [SerializeField] private bool isHighlighted = false;
    [SerializeField] private Renderer renderer;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color highlightColor = Color.red;

    private void Awake()
    {
        renderer.material.color = defaultColor;
    }

    public void Initialize(int x, int z)
    {
        x_pos = x;
        z_pos = z;
    }

    public void ToggleHighlighted()
    {
        isHighlighted = !isHighlighted;

        if (isHighlighted)
        {
            renderer.material.color = highlightColor;
        }
        else
        {
            renderer.material.color = defaultColor;
        }
    }

    public void OnReturn()
    {
        isHighlighted = false;
        renderer.material.color = defaultColor;
    }

    public override string ToString()
    {
        return $"Tile ({x_pos}, {z_pos})";
    }
}
