using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButton : MonoBehaviour
{
    public string m_sceneName;
    [SerializeField] private AudioClip m_hoverClip;

    public void Click()
    {
        MenuManager.Instance.Click(m_sceneName);
    }

    public void PlayHoverSound()
    {
        SoundManager.Instance.PlaySound(m_hoverClip);
    }
}
