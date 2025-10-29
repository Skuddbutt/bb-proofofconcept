using System.Collections.Generic;
using UnityEngine;

public class MaterialAssignment : MonoBehaviour
{
    [System.Serializable]
    public class Accessory
    {
        public GameObject objectToToggle; // The accessory object
        public Material accessoryMaterial; // The material to assign
        // Removed isLocked field to centralize lock management in OutfitUnlockManager
    }

    [System.Serializable]
    public class Outfit
    {
        public string outfitName; // Name of the outfit
        public List<GameObject> objectsToToggle; // List of objects for this outfit
        public List<Material> materialsToApply;  // Materials for the outfit
        public List<bool> visibilityStates;      // Visibility states for each object
        // Removed isLocked field to centralize lock management in OutfitUnlockManager
    }

    public List<Outfit> outfits;         // List of outfits
    public List<Accessory> accessories;  // List of accessories

    private string stagedOutfit;                      // Outfit staged for change
    private HashSet<int> stagedAccessories = new HashSet<int>(); // Staged accessory indices
    private string currentOutfit;                     // Currently applied outfit
    private HashSet<int> activeAccessories = new HashSet<int>(); // Currently applied accessory indices
    private BlendShapeController blendShapeController;
    private OutfitUnlockManager unlockManager;

    private void Start()
    {
        // Initialize references
        blendShapeController = FindObjectOfType<BlendShapeController>();
        unlockManager = FindObjectOfType<OutfitUnlockManager>();
        
        // Check if SceneController exists
        if (SceneController.Instance != null)
        {
            // Load outfit data from SceneController
            stagedOutfit = SceneController.Instance.GetCurrentOutfit();
            currentOutfit = SceneController.Instance.GetCurrentOutfit();
            stagedAccessories = SceneController.Instance.GetActiveAccessories();
            activeAccessories = new HashSet<int>(stagedAccessories);
            
            Debug.Log($"Loaded outfit from SceneController: {stagedOutfit}, with {stagedAccessories.Count} accessories");
            
            // Apply the loaded outfit's blendshapes if blend shape controller exists
            if (blendShapeController != null)
            {
                blendShapeController.SetStagedBlendShapeValues(
                    SceneController.Instance.GetWaistBlendShapeValue(),
                    SceneController.Instance.GetTorsoBlendShapeValue()
                );
            }
        }
        else
        {
            // Set default outfit values if no persistence manager exists
            stagedOutfit = "PJs";
            currentOutfit = "PJs";
            Debug.Log("No SceneController found, using default outfit: PJs");
        }
        
        // Sync lock states with OutfitUnlockManager - now simplified
        SyncLockStatesWithUnlockManager();
        
        // Apply the current outfit and accessories
        ApplyChanges();
        
        // Hide all accessory objects at start
        foreach (Accessory accessory in accessories)
        {
            if (accessory.objectToToggle != null)
                accessory.objectToToggle.SetActive(false);
            else
                Debug.LogWarning("An accessory object is not assigned in the inspector!");
        }
    }
    
    // Since lock states are now managed by OutfitUnlockManager,
    // this method just exists for compatibility
    public void SyncLockStatesWithUnlockManager()
    {
        // This method is now simplified - does nothing but exists for compatibility
        Debug.Log("Lock states are now managed by OutfitUnlockManager");
    }

    // Stages an outfit change (does NOT apply it immediately)
    public void SelectOutfit(string outfitName)
    {
        if (!outfits.Exists(o => o.outfitName == outfitName))
        {
            Debug.LogWarning($"Outfit '{outfitName}' not recognized.");
            return;
        }

        Outfit selectedOutfit = outfits.Find(o => o.outfitName == outfitName);

        // Lock checks are now handled in OutfitUnlockManager before this method is called
        
        // Stage the new outfit
        stagedOutfit = outfitName;
        
        // Add this line to stage the blendshape
        if (blendShapeController != null)
        {
            blendShapeController.SetBlendShapeForOutfit(outfitName);
        }
        
        Debug.Log($"Staged outfit: {stagedOutfit}");

        // Update outfit button visuals (only if using the unlock manager)
        if (unlockManager != null)
        {
            unlockManager.UpdateOutfitButtonVisuals(outfitName);
        }
    }

    public void ToggleAccessoryVisibilityAndMaterial(int accessoryIndex, bool isOn)
    {
        if (accessoryIndex < 0 || accessoryIndex >= accessories.Count)
        {
            Debug.LogWarning("Accessory index out of range!");
            return;
        }

        Accessory acc = accessories[accessoryIndex];
        
        // Lock checks are now handled in OutfitUnlockManager before this method is called

        if (isOn)
        {
            if (!stagedAccessories.Contains(accessoryIndex))
            {
                stagedAccessories.Add(accessoryIndex);
                Debug.Log($"Accessory {accessoryIndex} staged.");
            }
        }
        else
        {
            if (stagedAccessories.Contains(accessoryIndex))
            {
                stagedAccessories.Remove(accessoryIndex);
                Debug.Log($"Accessory {accessoryIndex} unstaged.");
            }
        }
        
        // Update UI states if unlock manager exists
        if (unlockManager != null)
        {
            unlockManager.UpdateAccessoryToggleStates(stagedAccessories);
        }
    }

    // Applies all staged outfit/accessory changes
    public void ApplyChanges()
    {
        if (!string.IsNullOrEmpty(stagedOutfit) && stagedOutfit != currentOutfit)
        {
            Outfit outfit = outfits.Find(o => o.outfitName == stagedOutfit);
            if (outfit != null) // No lock check needed anymore
            {
                currentOutfit = stagedOutfit;
                Debug.Log($"Applying outfit: {currentOutfit}");

                if (outfit.objectsToToggle.Count != outfit.materialsToApply.Count ||
                    outfit.objectsToToggle.Count != outfit.visibilityStates.Count)
                {
                    Debug.LogError($"Mismatch in counts for outfit '{currentOutfit}'! Check the inspector.");
                    return;
                }

                // First, disable all outfit objects to avoid overlap
                foreach (var otherOutfit in outfits)
                {
                    foreach (var obj in otherOutfit.objectsToToggle)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(false);
                        }
                    }
                }

                // Now enable only the current outfit objects
                for (int i = 0; i < outfit.objectsToToggle.Count; i++)
                {
                    GameObject obj = outfit.objectsToToggle[i];
                    Material material = outfit.materialsToApply[i];
                    bool isVisible = outfit.visibilityStates[i];

                    if (obj != null)
                    {
                        obj.SetActive(isVisible);
                        if (material != null && obj.GetComponent<Renderer>() != null)
                        {
                            obj.GetComponent<Renderer>().material = material;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Object at index {i} is null in outfit '{currentOutfit}'.");
                    }
                }
            }
            else
            {
                Debug.LogError($"Outfit '{currentOutfit}' not found.");
            }
        }

        // Apply accessories visibility and materials
        foreach (Accessory accessory in accessories)
        {
            int index = accessories.IndexOf(accessory);
            bool shouldBeActive = stagedAccessories.Contains(index); // No lock check needed anymore

            if (accessory.objectToToggle != null)
            {
                accessory.objectToToggle.SetActive(shouldBeActive);
                if (shouldBeActive && accessory.objectToToggle.GetComponent<Renderer>() != null)
                {
                    accessory.objectToToggle.GetComponent<Renderer>().material = accessory.accessoryMaterial;
                }
            }
            else
            {
                Debug.LogWarning($"Accessory object at index {index} is null!");
            }
        }

        activeAccessories = new HashSet<int>(stagedAccessories);
        Debug.Log("Changes applied!");
        
        // Persist the changes to the SceneController
        SaveOutfitToSceneController();
    }

    // Save current outfit state to the SceneController
    private void SaveOutfitToSceneController()
    {
        // Check if SceneController exists - if not, create it
        if (SceneController.Instance == null)
        {
            Debug.Log("Creating SceneController since it's missing");
            GameObject sceneControllerObj = new GameObject("SceneController");
            sceneControllerObj.AddComponent<SceneController>();
            DontDestroyOnLoad(sceneControllerObj);
        }
        
        // If blendShapeController is null, use default values for blend shapes
        float waistValue = 0f;
        float torsoValue = 0f;
        
        if (blendShapeController != null)
        {
            waistValue = blendShapeController.GetCurrentWaistBlendShapeValue();
            torsoValue = blendShapeController.GetCurrentTorsoBlendShapeValue();
        }
        
        if (SceneController.Instance != null)
        {
            SceneController.Instance.SaveOutfitState(
                currentOutfit, 
                activeAccessories,
                waistValue,
                torsoValue
            );
            Debug.Log($"Saved outfit state to SceneController: {currentOutfit}");
        }
        else
        {
            Debug.LogWarning("Failed to save outfit state: SceneController.Instance is still null after creation attempt");
        }
    }

    // Check if there are any pending changes to be applied
    public bool HasPendingChanges()
    {
        // Check if outfit is different
        if (stagedOutfit != currentOutfit)
            return true;

        // Check if accessories are different
        if (!activeAccessories.SetEquals(stagedAccessories))
            return true;

        return false;
    }

    // Public properties to expose the current and staged states
    public string CurrentOutfit
    {
        get { return currentOutfit; }
    }

    public string StagedOutfit
    {
        get { return stagedOutfit; }
    }
    
    // Get accessor for activeAccessories
    public HashSet<int> ActiveAccessories
    {
        get { return activeAccessories; }
    }

    private void InitializeSceneController()
    {
        if (SceneController.Instance == null)
        {
            Debug.Log("Creating SceneController from MaterialAssignment");
            GameObject sceneControllerObj = new GameObject("SceneController");
            SceneController controller = sceneControllerObj.AddComponent<SceneController>();
            DontDestroyOnLoad(sceneControllerObj);
        }
    }
}