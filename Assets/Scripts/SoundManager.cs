using UnityEngine;

public class SoundManager : Singleton<SoundManager>
{
    [SerializeField] private AudioSource m_AudioSource;

    [SerializeField] private AudioClip backgroundMusic;

    public void PlaySound(AudioClip clip)
    {
        m_AudioSource.PlayOneShot(clip);
    }


    void Start()
    {
        m_AudioSource.clip = backgroundMusic;
        m_AudioSource.loop = true;
        m_AudioSource.Play();
    }
}
