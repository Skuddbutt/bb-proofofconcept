using UnityEngine;

public class ProneController : MonoBehaviour
{
    [Header("References")]
    public Animator playerAnimator;
    
    [Header("Character Controller Settings")]
    public float standingHeight = 2.0f;
    public float proneHeight = 1.0f;
    public float transitionSpeed = 5.0f;
    
    private CharacterController characterController;
    private float originalHeight;
    private Vector3 originalCenter;
    
    void Start()
    {
        // Get the CharacterController component
        characterController = GetComponent<CharacterController>();
        
        if (characterController == null)
        {
            Debug.LogError("No CharacterController found on " + gameObject.name);
            enabled = false;
            return;
        }
        
        // Store original values
        originalHeight = characterController.height;
        originalCenter = characterController.center;
        
        // Find the animator if not assigned
        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
            if (playerAnimator == null)
            {
                Debug.LogError("No Animator found. Please assign one in the inspector.");
                enabled = false;
            }
        }
    }
    
    void Update()
    {
        // Check if we're grounded
        bool isGrounded = characterController.isGrounded;
        
        // Check animator parameter
        bool shouldProne = playerAnimator.GetBool("ShouldProne");
        
        // Determine target height
        float targetHeight = (isGrounded && shouldProne) ? proneHeight : standingHeight;
        
        // Current height
        float currentHeight = characterController.height;
        
        // Smoothly transition height
        float newHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * transitionSpeed);
        
        // Calculate position adjustment to keep feet at the same position
        float heightDifference = originalHeight - newHeight;
        Vector3 newCenter = originalCenter;
        newCenter.y = originalCenter.y - (heightDifference * 0.5f);
        
        // Apply changes to the CharacterController
        characterController.height = newHeight;
        characterController.center = newCenter;
        
        // Debug
        //Debug.Log($"Height: {newHeight:F2}, Center Y: {newCenter.y:F2}, Grounded: {isGrounded}, ShouldProne: {shouldProne}");
    }
    
    void OnDisable()
    {
        // Reset to original values when disabled
        if (characterController != null)
        {
            characterController.height = originalHeight;
            characterController.center = originalCenter;
        }
    }
}