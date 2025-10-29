using UnityEngine;
using System.Collections;

public class OutfitDebugHelper : MonoBehaviour
{
    void Start()
    {
        // Wait a moment for everything to initialize
        StartCoroutine(DebugOutfitApplication());
    }

    private IEnumerator DebugOutfitApplication()
    {
        // Wait 2 seconds to make sure everything else has loaded and initialized
        yield return new WaitForSeconds(0.1f);
        
        // Get references
        MaterialAssignment materialAssignment = FindObjectOfType<MaterialAssignment>();
        BlendShapeController blendShapeController = FindObjectOfType<BlendShapeController>();
        
        if (materialAssignment == null || blendShapeController == null)
        {
            Debug.LogError("Could not find MaterialAssignment or BlendShapeController!");
            yield break;
        }
        
        // Get outfit info from SceneController
        if (SceneController.Instance == null)
        {
            Debug.LogError("SceneController.Instance is null!");
            yield break;
        }
        
        string savedOutfit = SceneController.Instance.GetCurrentOutfit();
        Debug.Log($"DEBUG HELPER: Attempting to apply outfit: {savedOutfit}");
        
        // Now very explicitly walk through the outfit application process
        
        // 1. Find the correct outfit in the list
        bool foundOutfit = false;
        foreach (var outfit in materialAssignment.outfits)
        {
            Debug.Log($"Checking outfit: {outfit.outfitName}");
            
            if (outfit.outfitName == savedOutfit)
            {
                Debug.Log($"Found matching outfit: {outfit.outfitName}");
                foundOutfit = true;
                
                // 2. Log all objects in this outfit
                for (int i = 0; i < outfit.objectsToToggle.Count; i++)
                {
                    if (outfit.objectsToToggle[i] != null)
                    {
                        GameObject obj = outfit.objectsToToggle[i];
                        Material mat = (i < outfit.materialsToApply.Count) ? outfit.materialsToApply[i] : null;
                        bool visible = (i < outfit.visibilityStates.Count) ? outfit.visibilityStates[i] : false;
                        
                        Debug.Log($"Object: {obj.name}, Has Material: {mat != null}, Should be Visible: {visible}");
                        
                        // 3. Manually apply the outfit changes to this object
                        obj.SetActive(visible);
                        if (visible && mat != null && obj.GetComponent<Renderer>() != null)
                        {
                            obj.GetComponent<Renderer>().material = mat;
                            Debug.Log($"Applied material to {obj.name}");
                        }
                    }
                    else
                    {
                        Debug.LogError($"NULL object in outfit {outfit.outfitName} at index {i}");
                    }
                }
                
                // 4. Break once we've found and applied our outfit
                break;
            }
        }
        
        if (!foundOutfit)
        {
            Debug.LogError($"Could not find outfit named '{savedOutfit}' in MaterialAssignment!");
        }
        
        // 5. Apply blendshapes as well
        blendShapeController.SetStagedBlendShapeValues(
            SceneController.Instance.GetWaistBlendShapeValue(),
            SceneController.Instance.GetTorsoBlendShapeValue()
        );
        blendShapeController.ApplyStagedBlendShape();
        
        Debug.Log("DEBUG HELPER: Outfit application complete");
    }
}