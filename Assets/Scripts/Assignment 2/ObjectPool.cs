using UnityEngine;
using System.Collections.Generic;

public class ObjectPool : MonoBehaviour
{
    [SerializeField] private Transform inactiveParent;
    [SerializeField] private GameObject prefab;
    [SerializeField] private int preWarmCount;
    [SerializeField] private List<GameObject> inactive = new List<GameObject>();

    private void Awake()
    {
        for(int i = 0; i < preWarmCount; i++)
        {
            GameObject go = Instantiate(prefab, inactiveParent);
            go.SetActive(false);
            inactive.Add(go);
        }
    }

    public GameObject Get(Vector3 position, Quaternion rotation, Transform activeParent)
    {
        GameObject go;
        if (inactive.Count > 0)
        {
            int lastIndex = inactive.Count - 1;
            go = inactive[lastIndex];
            inactive.RemoveAt(lastIndex);
        }
        else
        {
            go = Instantiate(prefab);
        }

        go.transform.SetParent(activeParent);
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    public void Return(GameObject go)
    {
        go.GetComponent<IPoolable>().OnReturn();
        go.SetActive(false);
        go.transform.SetParent(inactiveParent);
        inactive.Add(go);
    }
}
