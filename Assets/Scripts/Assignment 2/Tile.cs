using UnityEngine;

public class Tile : MonoBehaviour, IPoolable
{
    public int x_pos;
    public int z_pos;
    [SerializeField] private bool isHighlighted = false;
    [SerializeField] private Renderer m_renderer;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color highlightColor = Color.red;

    private void Awake()
    {
        if (m_renderer == null)
        {
            m_renderer = GetComponent<Renderer>();
        }

        ApplyColor(defaultColor);
    }

    public void Initialize(int x, int z)
    {
        x_pos = x;
        z_pos = z;
        isHighlighted = false;
        ApplyColor(defaultColor);
    }

    public void SetColor(Color color)
    {
        ApplyColor(color);
    }

    public void SetDefaultColor(Color color)
    {
        defaultColor = color;
    }

    public void ToggleHighlighted()
    {
        isHighlighted = !isHighlighted;

        if (isHighlighted)
        {
            ApplyColor(highlightColor);
        }
        else
        {
            ApplyColor(defaultColor);
        }
    }

    public void OnReturn()
    {
        isHighlighted = false;
        ApplyColor(defaultColor);
    }

    private void ApplyColor(Color color)
    {
        if (m_renderer != null)
        {
            m_renderer.material.color = color;
        }
    }

    public override string ToString()
    {
        return $"Tile ({x_pos}, {z_pos})";
    }
}
