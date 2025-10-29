using UnityEngine;
using UnityEngine.Playables;

public class ForceFrameRate : MonoBehaviour
{
    private PlayableDirector director;
    private const double FPS = 24.0;
    private const double frameTime = 1.0 / FPS;
    private double accumulatedTime = 0;
    private double lastFrameTime = 0;
    private bool isTransitioning = false;
    private int previousVSyncState;

    void OnEnable()
    {
        // Store current VSync state before disabling
        previousVSyncState = QualitySettings.vSyncCount;
        
        // Temporarily disable VSync and set FPS for cutscene
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = (int)FPS;

        if (director == null)
            director = GetComponent<PlayableDirector>();
    }

    void Update()
    {
        if (director != null && director.state == PlayState.Playing && !isTransitioning)
        {
            accumulatedTime += Time.deltaTime;

            if (accumulatedTime >= frameTime)
            {
                int framesToAdvance = Mathf.FloorToInt((float)(accumulatedTime / frameTime));
                double newTime = lastFrameTime + (framesToAdvance * frameTime);

                director.time = newTime;
                lastFrameTime = newTime;

                accumulatedTime -= (framesToAdvance * frameTime);
            }
        }
    }

    public void StartTransition()
    {
        isTransitioning = true;
        accumulatedTime = 0;
        lastFrameTime = 0;
    }

    public void EndTransition()
    {
        isTransitioning = false;
    }

    void OnDisable()
    {
        // Reset timing variables
        accumulatedTime = 0;
        lastFrameTime = 0;
        isTransitioning = false;

        // Restore previous VSync state
        QualitySettings.vSyncCount = previousVSyncState;
        Application.targetFrameRate = -1;
    }
}