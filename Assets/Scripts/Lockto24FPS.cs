using UnityEngine;

public class LockAnimationTo24FPS : MonoBehaviour
{
    public Animator animator; // Reference to the Animator component.
    private float timer = 0f; // Tracks time elapsed since the last frame update.
    private float frameInterval = 1f / 24f; // Time per frame for 24 FPS.

    void Start()
    {
        // Validate that the Animator is assigned
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator component not found on this GameObject!");
                enabled = false; // Disable the script if no Animator is found
                return;
            }
        }

        // Ensure the Animator does not run automatically
        animator.updateMode = AnimatorUpdateMode.Normal; // Default update mode.
        animator.speed = 0f; // Stop automatic playback.
    }

    void Update()
    {
        // Increment the timer with the time elapsed since the last frame
        timer += Time.deltaTime;

        // Check if enough time has passed to advance a frame
        if (timer >= frameInterval)
        {
            animator.speed = 1f; // Temporarily enable the Animator to update
            animator.Update(frameInterval); // Advance the animation manually by one frame
            timer -= frameInterval; // Subtract the frame interval from the timer
            animator.speed = 0f; // Pause the Animator again
        }
    }
}
