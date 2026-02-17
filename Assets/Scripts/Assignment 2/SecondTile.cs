using UnityEngine;

public class SecondTile : MonoBehaviour, IPoolable
{
    [SerializeField] private bool isHighlighted = false;
    [SerializeField] private Renderer renderer;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color highlightColor = Color.red;

    private void Awake()
    {
        renderer.material.color = defaultColor;
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
        isHighlighted = true;
        renderer.material.color = defaultColor;
    }
}
