using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public GameObject characterPrefab;
    public GameObject targetPrefab;
    public GameObject obstaclePrefab;

    GameObject character, target, obstacle;
    AICharacter ai; 
    
    [Header("Sounds")]
    public AudioClip resetClip;

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit0Key.wasPressedThisFrame) ResetScene();
        if (Keyboard.current.digit1Key.wasPressedThisFrame) StartSeek();
        if (Keyboard.current.digit2Key.wasPressedThisFrame) StartFlee();
        if (Keyboard.current.digit3Key.wasPressedThisFrame) StartArrive();
        if (Keyboard.current.digit4Key.wasPressedThisFrame) StartAvoid();
    }

    Vector3 RandomPos() =>
        new Vector3(Random.Range(-10, 10), 0, Random.Range(-10, 10));

    void ResetScene()
    {
        SoundManager.Instance.PlaySound(resetClip);

        if (character) Destroy(character);
        if (target) Destroy(target);
        if (obstacle) Destroy(obstacle);

        character = null;
        target = null;
        obstacle = null;
        ai = null;
    }

    void SetBehaviour(SteeringBehaviour behaviour, Transform target = null, Transform obstacle = null)
    {
        ai.currentBehaviour = behaviour;
        ai.target = target;
        ai.obstacle = obstacle;
    }

    void SpawnCharacter()
    {
        character = Instantiate(characterPrefab, RandomPos(), Quaternion.identity);
        ai = character.GetComponent<AICharacter>();
    }

    void StartSeek()
    {
        ResetScene();
        SpawnCharacter();

        target = Instantiate(targetPrefab, RandomPos(), Quaternion.identity);
        SetBehaviour(SteeringBehaviour.Seek, target.transform);
    }

    void StartFlee()
    {
        ResetScene();
        SpawnCharacter();

        target = Instantiate(targetPrefab, RandomPos(), Quaternion.identity);
        SetBehaviour(SteeringBehaviour.Flee, target.transform);
    }

    void StartArrive()
    {
        ResetScene();
        SpawnCharacter();

        target = Instantiate(targetPrefab, RandomPos(), Quaternion.identity);
        SetBehaviour(SteeringBehaviour.Arrive, target.transform);
    }

    void StartAvoid()
    {
        ResetScene();

        obstacle = Instantiate(obstaclePrefab, Vector3.zero, Quaternion.identity);

        SpawnCharacter();

        Vector3 aiPos = character.transform.position;

        Vector3 targetPos = new Vector3(-aiPos.x, 0f, -aiPos.z);

        targetPos.x = Mathf.Clamp(targetPos.x, -10f, 10f);
        targetPos.z = Mathf.Clamp(targetPos.z, -10f, 10f);

        target = Instantiate(targetPrefab, targetPos, Quaternion.identity);

        SetBehaviour(SteeringBehaviour.Avoid, target.transform, obstacle.transform);
    }
}
