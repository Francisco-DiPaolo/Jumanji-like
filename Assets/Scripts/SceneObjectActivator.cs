using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneObjectActivator : MonoBehaviour
{
    [Tooltip("Objects to activate sequentially. Assign them disabled in the Inspector.")]
    [SerializeField] List<GameObject> objectsToActivate = new();

    [Tooltip("Seconds to wait between each object activation.")]
    [SerializeField] float delayBetweenObjects = 0.1f;

    public event Action OnAllObjectsActivated;

    public float Progress { get; private set; }
    public bool IsComplete { get; private set; }

    void Start()
    {
        StartCoroutine(ActivateSequentially());
    }

    IEnumerator ActivateSequentially()
    {
        for (int i = 0; i < objectsToActivate.Count; i++)
        {
            if (objectsToActivate[i] != null)
                objectsToActivate[i].SetActive(true);

            Progress = (float)(i + 1) / objectsToActivate.Count;
            yield return new WaitForSeconds(delayBetweenObjects);
        }

        IsComplete = true;
        OnAllObjectsActivated?.Invoke();
    }
}
