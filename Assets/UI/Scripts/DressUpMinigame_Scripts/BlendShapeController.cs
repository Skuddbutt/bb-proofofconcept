using UnityEngine;
using System.Collections.Generic;

public class BlendShapeController : MonoBehaviour
{
    // The body mesh renderer reference
    public SkinnedMeshRenderer bodyMeshRenderer;

    // The blendshape names (you can use the proper blendshape name as you have it in the inspector)
    public string blendShapeNameWaist = "Shaine:Shaine_Body_Blendshapes:body_blends.thin_waist1";
    public string blendShapeNameTorso = "Shaine:Shaine_Body_Blendshapes:body_blends.thin_torso1";  // New blendshape for the torso

    // List of outfits with corresponding blendshape values for both waist and torso
    [System.Serializable]
    public class OutfitBlendShape
    {
        public string outfitName;
        public float blendShapeValueWaist;  // Waist blendshape value (0 to 100)
        public float blendShapeValueTorso;  // Torso blendshape value (0 to 100)
    }

    // List of outfits and their associated blendshape values for both waist and torso
    public List<OutfitBlendShape> outfitBlendShapes;

    // Staged blendshape values, to be applied on confirm
    private float stagedBlendShapeValueWaist = -1;  // -1 indicates no blendshape value staged for waist
    private float stagedBlendShapeValueTorso = -1;  // -1 indicates no blendshape value staged for torso

    // Current applied blendshape values
    private float currentBlendShapeValueWaist = 0f;
    private float currentBlendShapeValueTorso = 0f;

    private void Start()
    {
        // Check if SceneController exists
        if (SceneController.Instance != null)
        {
            // Load blendshape values from SceneController
            float waistValue = SceneController.Instance.GetWaistBlendShapeValue();
            float torsoValue = SceneController.Instance.GetTorsoBlendShapeValue();
            
            // Set the staged and current values
            stagedBlendShapeValueWaist = waistValue;
            stagedBlendShapeValueTorso = torsoValue;
            currentBlendShapeValueWaist = waistValue;
            currentBlendShapeValueTorso = torsoValue;
            
            // Apply the loaded blendshape values
            ApplyBlendShapeValues(waistValue, torsoValue);
            Debug.Log($"Loaded blendshape values from SceneController: Waist={waistValue}, Torso={torsoValue}");
        }
        else
        {
            // Set the initial blendshape values (default to PJs or your starting outfit)
            SetBlendShapeForOutfit("PJs");
            Debug.Log("No SceneController found, using default PJs blendshape values");
        }
    }

    // This method has been simplified as lock states are managed by OutfitUnlockManager
    public void SetOutfitLockState(string outfitName, bool isLocked)
    {
        // This method now does nothing, but exists for compatibility
        Debug.Log($"Lock states are now managed by OutfitUnlockManager");
    }

    // This method will be used to update the blendshapes based on the selected outfit name
    public void SetBlendShapeForOutfit(string outfitName)
    {
        // Find the outfit in the list
        OutfitBlendShape selectedOutfit = outfitBlendShapes.Find(outfit => outfit.outfitName == outfitName);

        // If the outfit is found, apply the blendshape values
        if (selectedOutfit != null && bodyMeshRenderer != null)
        {
            // Stage the blendshape values for this outfit (do not apply yet)
            stagedBlendShapeValueWaist = selectedOutfit.blendShapeValueWaist;
            stagedBlendShapeValueTorso = selectedOutfit.blendShapeValueTorso;
            Debug.Log($"Staged blendshape values Waist: {stagedBlendShapeValueWaist}, Torso: {stagedBlendShapeValueTorso} for outfit '{outfitName}'.");
        }
        else
        {
            Debug.LogWarning($"Outfit '{outfitName}' not found or bodyMeshRenderer is missing!");
        }
    }

    // Set staged blendshape values directly (used when loading from persistence)
    public void SetStagedBlendShapeValues(float waistValue, float torsoValue)
    {
        stagedBlendShapeValueWaist = waistValue;
        stagedBlendShapeValueTorso = torsoValue;
        Debug.Log($"Directly set staged blendshape values to Waist: {waistValue}, Torso: {torsoValue}");
    }

    // Apply specific blendshape values directly
    private void ApplyBlendShapeValues(float waistValue, float torsoValue)
    {
        if (bodyMeshRenderer != null)
        {
            // Get the blendshape indices for waist and torso
            int blendShapeIndexWaist = bodyMeshRenderer.sharedMesh.GetBlendShapeIndex(blendShapeNameWaist);
            int blendShapeIndexTorso = bodyMeshRenderer.sharedMesh.GetBlendShapeIndex(blendShapeNameTorso);

            if (blendShapeIndexWaist >= 0)
            {
                // Apply the waist blendshape value
                bodyMeshRenderer.SetBlendShapeWeight(blendShapeIndexWaist, waistValue);
                currentBlendShapeValueWaist = waistValue;
                Debug.Log($"Applied blendshape '{blendShapeNameWaist}' with value {waistValue}.");
            }
            else
            {
                Debug.LogWarning($"Blendshape '{blendShapeNameWaist}' not found on {bodyMeshRenderer.name}.");
            }

            if (blendShapeIndexTorso >= 0)
            {
                // Apply the torso blendshape value
                bodyMeshRenderer.SetBlendShapeWeight(blendShapeIndexTorso, torsoValue);
                currentBlendShapeValueTorso = torsoValue;
                Debug.Log($"Applied blendshape '{blendShapeNameTorso}' with value {torsoValue}.");
            }
            else
            {
                Debug.LogWarning($"Blendshape '{blendShapeNameTorso}' not found on {bodyMeshRenderer.name}.");
            }
        }
        else
        {
            Debug.LogWarning("Cannot apply blendshape values: bodyMeshRenderer is missing!");
        }
    }

    // Applies the staged blendshape values to the character
    public void ApplyStagedBlendShape()
    {
        if (stagedBlendShapeValueWaist >= 0)
        {
            ApplyBlendShapeValues(stagedBlendShapeValueWaist, stagedBlendShapeValueTorso);
        }
        else
        {
            Debug.LogWarning("No valid blendshape staged!");
        }
    }

    // Get the current blendshape values (for saving to persistence)
    public float GetCurrentWaistBlendShapeValue()
    {
        return currentBlendShapeValueWaist;
    }

    public float GetCurrentTorsoBlendShapeValue()
    {
        return currentBlendShapeValueTorso;
    }
}