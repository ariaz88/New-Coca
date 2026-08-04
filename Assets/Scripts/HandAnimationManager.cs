using UnityEngine;
using System.Collections;
public class HandAnimationManager : MonoBehaviour
{
    public static HandAnimationManager Instance { get; private set; }

    private bool isAnimationRunning = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void TryRestartAnimation(float delay)
    {
        if (!isAnimationRunning)
        {
            StartCoroutine(RestartHandAnimation(delay));
        }
        else
        {
            Debug.Log("Animation already running. Ignoring request.");
        }
    }

    private IEnumerator RestartHandAnimation(float delay)
    {
        isAnimationRunning = true;
        yield return new WaitForSeconds(delay);

        // Insert animation restart logic here
        Debug.Log("Restarting hand animation...");

        isAnimationRunning = false;
    }
}
