using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerOutfit : MonoBehaviour
{
    public static PlayerOutfit instance;
    
    public MaterialAssignment materialAssignment;
    public BlendShapeController blendShapeController;
    
    private void Awake()
    {
        instance = this;
    }
    
    void Start()
    {
        // Use a very short delay to ensure all components are properly initialized
        StartCoroutine(MinimalDelayOutfitInitialization());
    }
    
    private IEnumerator MinimalDelayOutfitInitialization()
    {
        // Minimal delay to ensure components are ready
        yield return null;
        
        // Get references to required components
        if (materialAssignment == null || blendShapeController == null)
        {
            // Try to find them on OutfitManager if not directly assigned
            GameObject outfitManager = GameObject.Find("OutfitManager");
            if (outfitManager != null)
            {
                if (materialAssignment == null)
                    materialAssignment = outfitManager.GetComponent<MaterialAssignment>();
                    
                if (blendShapeController == null)
                    blendShapeController = outfitManager.GetComponent<BlendShapeController>();
            }
        }

        // If we still don't have the necessary components, log an error and exit
        if (materialAssignment == null || blendShapeController == null)
        {
            Debug.LogError("PlayerOutfit: Missing required outfit components!");
            yield break;
        }

        // Find the Shaine object
        GameObject shaineObject = transform.Find("Shaine")?.gameObject;
        
        if (shaineObject == null)
        {
            // Try alternate methods to find Shaine
            shaineObject = GameObject.Find("Shaine");
            if (shaineObject == null && GetComponent<PlayerController>()?.playerModel != null && 
                GetComponent<PlayerController>().playerModel.name == "Shaine")
            {
                shaineObject = GetComponent<PlayerController>().playerModel;
            }
        }
        
        if (shaineObject != null)
        {
            Debug.Log($"Found Shaine object: {shaineObject.name} in hierarchy");
            
            // Update outfit references to point to objects under Shaine
            foreach (var outfit in materialAssignment.outfits)
            {
                // Skip if outfit has no objects to toggle
                if (outfit.objectsToToggle == null) continue;
                
                for (int i = 0; i < outfit.objectsToToggle.Count; i++)
                {
                    if (outfit.objectsToToggle[i] == null) continue;
                    
                    string objectName = outfit.objectsToToggle[i].name;
                    Transform foundObject = FindRecursively(shaineObject.transform, objectName);
                    
                    if (foundObject != null)
                    {
                        outfit.objectsToToggle[i] = foundObject.gameObject;
                        Debug.Log($"Updated reference for {objectName} in outfit {outfit.outfitName}");
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find object named {objectName} for outfit {outfit.outfitName}");
                    }
                }
            }
            
            // Update accessory references
            foreach (var accessory in materialAssignment.accessories)
            {
                if (accessory.objectToToggle == null) continue;
                
                string accessoryName = accessory.objectToToggle.name;
                Transform foundAccessory = FindRecursively(shaineObject.transform, accessoryName);
                
                if (foundAccessory != null)
                {
                    accessory.objectToToggle = foundAccessory.gameObject;
                    Debug.Log($"Updated reference for accessory {accessoryName}");
                }
                else
                {
                    Debug.LogWarning($"Could not find accessory named {accessoryName}");
                }
            }
            
            // Apply the outfit directly from the SceneController
            if (SceneController.Instance != null)
            {
                string currentOutfit = SceneController.Instance.GetCurrentOutfit();
                HashSet<int> activeAccessories = SceneController.Instance.GetActiveAccessories();
                
                Debug.Log($"Directly applying outfit: {currentOutfit}, with {activeAccessories.Count} accessories");
                
                // Apply outfit directly
                ApplyOutfitDirectly(currentOutfit);
                
                // Apply accessories directly
                foreach (int index in activeAccessories)
                {
                    if (index >= 0 && index < materialAssignment.accessories.Count)
                    {
                        var accessory = materialAssignment.accessories[index];
                        if (accessory.objectToToggle != null)
                        {
                            accessory.objectToToggle.SetActive(true);
                            Debug.Log($"Activated accessory at index {index}");
                        }
                    }
                }
                
                // Apply blendshapes
                blendShapeController.SetStagedBlendShapeValues(
                    SceneController.Instance.GetWaistBlendShapeValue(),
                    SceneController.Instance.GetTorsoBlendShapeValue()
                );
                blendShapeController.ApplyStagedBlendShape();
                
                // Also update MaterialAssignment state
                materialAssignment.SelectOutfit(currentOutfit);
                foreach (int index in activeAccessories)
                {
                    materialAssignment.ToggleAccessoryVisibilityAndMaterial(index, true);
                }
                
                Debug.Log("Outfit and accessories initialization complete");
            }
            else
            {
                Debug.LogWarning("SceneController not found! Unable to load saved outfit.");
            }
        }
        else
        {
            Debug.LogError("Could not find Shaine object in hierarchy!");
        }
    }
    
    // Method to directly apply an outfit without using MaterialAssignment.ApplyChanges()
    private void ApplyOutfitDirectly(string outfitName)
    {
        foreach (var outfit in materialAssignment.outfits)
        {
            // For all outfits, first disable all objects
            for (int i = 0; i < outfit.objectsToToggle.Count; i++)
            {
                if (outfit.objectsToToggle[i] != null)
                {
                    outfit.objectsToToggle[i].SetActive(false);
                }
            }
        }
        
        // Now enable only the selected outfit's objects
        foreach (var outfit in materialAssignment.outfits)
        {
            if (outfit.outfitName == outfitName)
            {
                Debug.Log($"Applying outfit: {outfit.outfitName}");
                for (int i = 0; i < outfit.objectsToToggle.Count; i++)
                {
                    if (outfit.objectsToToggle[i] != null)
                    {
                        GameObject obj = outfit.objectsToToggle[i];
                        Material mat = (i < outfit.materialsToApply.Count) ? outfit.materialsToApply[i] : null;
                        bool visible = (i < outfit.visibilityStates.Count) ? outfit.visibilityStates[i] : false;
                        
                        // Apply changes directly
                        obj.SetActive(visible);
                        if (visible && mat != null && obj.GetComponent<Renderer>() != null)
                        {
                            obj.GetComponent<Renderer>().material = mat;
                            Debug.Log($"Set {obj.name} active={visible} with material={mat.name}");
                        }
                    }
                }
                break;
            }
        }
    }
    
    private Transform FindRecursively(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;
        
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform found = FindRecursively(child, name);
            if (found != null)
                return found;
        }
        
        return null;
    }
}