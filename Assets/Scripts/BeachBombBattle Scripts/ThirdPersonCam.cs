using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class ThirdPersonCam : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform player;
    public Transform playerObj;
    public Rigidbody rb;

    public float rotationSpeed;

    public Transform combatLookAt;

    public GameObject thirdPersonCam;
    public GameObject combatCam;
    public GameObject topDownCam;

    public CameraStyle currentStyle;
    public enum CameraStyle
    {
        Basic,
        Combat,
        Topdown
    }

    private PauseMenu pauseMenu;  // Reference to the pause menu script
    private CinemachineBrain cinemachineBrain;  // Reference to Cinemachine Brain

    private void Start()
    {
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        // Find PauseMenu script to check for paused state
        pauseMenu = FindObjectOfType<PauseMenu>();

        // Find the CinemachineBrain in the scene
        cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
    }

    private void Update()
    {
        // Check if the game is paused
        if (pauseMenu != null && pauseMenu.IsPaused)
        {
            // Disable the Cinemachine Brain when paused
            if (cinemachineBrain != null)
            {
                cinemachineBrain.enabled = false;  // Disable the Brain to stop camera movement
            }

            return;  // Skip camera movement logic if the game is paused
        }

        // If the game is not paused, re-enable the Cinemachine Brain
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = true;  // Re-enable the Brain to allow camera movement
        }

        // Handle camera style switching
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchCameraStyle(CameraStyle.Basic);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchCameraStyle(CameraStyle.Combat);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchCameraStyle(CameraStyle.Topdown);

        // Handle camera rotation if the game is not paused
        RotateCamera();
    }

    // This method handles the camera rotation
    private void RotateCamera()
    {
        // Rotate orientation (camera rotation)
        Vector3 viewDir = player.position - new Vector3(transform.position.x, player.position.y, transform.position.z);
        orientation.forward = viewDir.normalized;

        // Rotate player object (the actual character)
        if (currentStyle == CameraStyle.Basic || currentStyle == CameraStyle.Topdown)
        {
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");
            Vector3 inputDir = orientation.forward * verticalInput + orientation.right * horizontalInput;

            if (inputDir != Vector3.zero)
                playerObj.forward = Vector3.Slerp(playerObj.forward, inputDir.normalized, Time.deltaTime * rotationSpeed);
        }
        else if (currentStyle == CameraStyle.Combat)
        {
            Vector3 dirToCombatLookAt = combatLookAt.position - new Vector3(transform.position.x, combatLookAt.position.y, transform.position.z);
            orientation.forward = dirToCombatLookAt.normalized;

            playerObj.forward = dirToCombatLookAt.normalized;
        }
    }

    private void SwitchCameraStyle(CameraStyle newStyle)
    {
        combatCam.SetActive(false);
        thirdPersonCam.SetActive(false);
        topDownCam.SetActive(false);

        if (newStyle == CameraStyle.Basic) thirdPersonCam.SetActive(true);
        if (newStyle == CameraStyle.Combat) combatCam.SetActive(true);
        if (newStyle == CameraStyle.Topdown) topDownCam.SetActive(true);

        currentStyle = newStyle;
    }
}
