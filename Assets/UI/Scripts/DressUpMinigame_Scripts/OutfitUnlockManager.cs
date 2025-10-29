using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class OutfitUnlockManager : MonoBehaviour
{
    // ----------------- Outfit Section -----------------
    [System.Serializable]
    public class OutfitButton
    {
        public string outfitName;
        public Button outfitButton; // The main button
        public Image outfitIcon;    // The outfit icon (child)
        public GameObject lockIcon; // The lock overlay
        public bool isUnlocked = false;

        // Sprites for unlocked state
        public Sprite unlockedNormal;
        public Sprite unlockedHighlighted;
        public Sprite unlockedPressed;
        public Sprite unlockedSelected;
        public Sprite unlockedDisabled;

        // Sprites for locked state
        public Sprite lockedNormal;
        public Sprite lockedHighlighted;
        public Sprite lockedPressed;
        public Sprite lockedSelected;
        public Sprite lockedDisabled;
    }

    public List<OutfitButton> outfitButtons;
    private PlayerAnimationController playerAnimationController;
    private MaterialAssignment materialAssignment;

    // ----------------- Accessory Section -----------------
    [System.Serializable]
    public class AccessoryToggle
    {
        public string accessoryName;           // Name of the accessory
        public Toggle accessoryToggle;         // Toggle component for the accessory
        public Image accessoryIcon;            // Icon image (child)
        public Image buttonBackground;         // Background image of the toggle
        public GameObject lockIcon;            // Lock overlay to indicate locked state
        public bool isUnlocked = false;        // Whether the accessory is unlocked

        // Sprites for unlocked state
        public Sprite unlockedNormal;
        public Sprite unlockedHighlighted;
        public Sprite unlockedPressed;
        public Sprite unlockedSelected;        // Sprite used when the toggle is selected
        public Sprite unlockedDisabled;

        // Sprites for locked state
        public Sprite lockedNormal;
        public Sprite lockedHighlighted;
        public Sprite lockedPressed;
        public Sprite lockedSelected;
        public Sprite lockedDisabled;
    }

    public List<AccessoryToggle> accessoryToggles;

    private void Start()
    {
        playerAnimationController = FindObjectOfType<PlayerAnimationController>();
        materialAssignment = FindObjectOfType<MaterialAssignment>();

        // Process Outfit Buttons
        foreach (var outfit in outfitButtons)
        {
            UpdateOutfitButtonState(outfit);

            // Add click listeners to buttons
            outfit.outfitButton.onClick.AddListener(() => OnOutfitButtonClicked(outfit));
        }

        // Process Accessory Toggles
        for (int i = 0; i < accessoryToggles.Count; i++)
        {
            int index = i; // Capture the current index for the lambda
            var accessory = accessoryToggles[index];
            UpdateAccessoryToggleState(accessory);

            accessory.accessoryToggle.onValueChanged.AddListener((bool isOn) =>
            {
                if (!accessory.isUnlocked)
                {
                    if (playerAnimationController != null)
                    {
                        playerAnimationController.TriggerInvalidAnimation();
                    }
                    accessory.accessoryToggle.isOn = false;  // Reset toggle state
                    return;
                }

                if (materialAssignment != null)
                {
                    materialAssignment.ToggleAccessoryVisibilityAndMaterial(index, isOn);
                }

                if (accessory.buttonBackground != null)
                {
                    accessory.buttonBackground.sprite = isOn ? accessory.unlockedSelected : accessory.unlockedNormal;
                    accessory.buttonBackground.enabled = false;
                    accessory.buttonBackground.enabled = true;
                }

                if (!isOn)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            });
        }
        
        // Initialize toggle states based on materialAssignment, if available
        if (materialAssignment != null)
        {
            // Update accessory toggles based on active accessories
            UpdateAccessoryToggleStates(materialAssignment.ActiveAccessories);
        }
        
        // Sync lock states from UI to Material/BlendShape
        PropagateLocksToOtherSystems();
    }
    
    // Propagate lock states from UI to Material/BlendShape systems
    private void PropagateLocksToOtherSystems()
    {
        if (materialAssignment != null)
        {
            materialAssignment.SyncLockStatesWithUnlockManager();
        }
    }

    private void OnOutfitButtonClicked(OutfitButton outfit)
    {
        // Check if outfit is locked BEFORE any staging or visual changes
        if (!outfit.isUnlocked)
        {
            Debug.Log($"Outfit '{outfit.outfitName}' is locked. Triggering Invalid animation.");
            if (playerAnimationController != null)
            {
                playerAnimationController.TriggerInvalidAnimation();
            }
            return; // Exit without staging the outfit
        }

        if (materialAssignment != null)
        {
            // Stage the outfit changes in MaterialAssignment
            materialAssignment.SelectOutfit(outfit.outfitName);
        }

        // Set the new selected sprite for the current outfit
        UpdateOutfitButtonVisuals(outfit.outfitName);
    }

    public void UpdateOutfitButtonState(OutfitButton outfit)
    {
        outfit.outfitButton.interactable = true;

        if (outfit.lockIcon != null)
            outfit.lockIcon.SetActive(!outfit.isUnlocked);

        if (outfit.outfitIcon != null)
            outfit.outfitIcon.color = outfit.isUnlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);

        // Set the button sprite based on current state and lock status
        Image buttonImage = outfit.outfitButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            if (!outfit.isUnlocked)
            {
                // If locked, use locked sprite
                buttonImage.sprite = outfit.lockedNormal;
            }
            else if (materialAssignment != null && outfit.outfitName == materialAssignment.StagedOutfit)
            {
                // If unlocked and selected, use selected sprite
                buttonImage.sprite = outfit.unlockedSelected;
            }
            else
            {
                // If unlocked but not selected, use normal sprite
                buttonImage.sprite = outfit.unlockedNormal;
            }
        }

        // Update button sprite state
        SpriteState newState = new SpriteState();
        if (outfit.isUnlocked)
        {
            newState.highlightedSprite = outfit.unlockedHighlighted;
            newState.pressedSprite = outfit.unlockedPressed;
            newState.selectedSprite = outfit.unlockedSelected;
            newState.disabledSprite = outfit.unlockedDisabled;
        }
        else
        {
            newState.highlightedSprite = outfit.lockedHighlighted;
            newState.pressedSprite = outfit.lockedPressed;
            newState.selectedSprite = outfit.lockedSelected;
            newState.disabledSprite = outfit.lockedDisabled;
        }
        outfit.outfitButton.spriteState = newState;
    }

    public void UpdateAccessoryToggleState(AccessoryToggle accessory)
    {
        // Keep toggle interactable even when locked, so it can show hover states
        accessory.accessoryToggle.interactable = true;

        if (accessory.lockIcon != null)
            accessory.lockIcon.SetActive(!accessory.isUnlocked);

        if (accessory.accessoryIcon != null)
            accessory.accessoryIcon.color = accessory.isUnlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);

        // Update the background sprite based on lock status
        if (accessory.buttonBackground != null)
        {
            if (!accessory.isUnlocked)
            {
                // If locked, use locked sprite
                accessory.buttonBackground.sprite = accessory.lockedNormal;
            }
            else if (accessory.accessoryToggle.isOn)
            {
                // If unlocked and toggled on, use selected sprite
                accessory.buttonBackground.sprite = accessory.unlockedSelected;
            }
            else
            {
                // If unlocked but not toggled, use normal sprite
                accessory.buttonBackground.sprite = accessory.unlockedNormal;
            }
        }

        SpriteState newState = new SpriteState();
        if (accessory.isUnlocked)
        {
            newState.highlightedSprite = accessory.unlockedHighlighted;
            newState.pressedSprite = accessory.unlockedPressed;
            newState.selectedSprite = accessory.unlockedSelected;
            newState.disabledSprite = accessory.unlockedDisabled;
        }
        else
        {
            newState.highlightedSprite = accessory.lockedHighlighted;
            newState.pressedSprite = accessory.lockedPressed;
            newState.selectedSprite = accessory.lockedSelected;
            newState.disabledSprite = accessory.lockedDisabled;
        }
        accessory.accessoryToggle.spriteState = newState;
    }

    // Update toggles based on active accessories from MaterialAssignment
    public void UpdateAccessoryToggleStates(HashSet<int> activeAccessories)
    {
        for (int i = 0; i < accessoryToggles.Count; i++)
        {
            var accessory = accessoryToggles[i];
            bool isActive = activeAccessories.Contains(i);
            
            // Update toggle state without triggering onValueChanged
            accessory.accessoryToggle.SetIsOnWithoutNotify(isActive);
            
            // Update visual state - respect lock status
            if (accessory.buttonBackground != null)
            {
                if (!accessory.isUnlocked)
                {
                    // Always use locked sprite if locked
                    accessory.buttonBackground.sprite = accessory.lockedNormal;
                }
                else
                {
                    // Use selected or normal based on toggle state
                    accessory.buttonBackground.sprite = isActive ? 
                        accessory.unlockedSelected : accessory.unlockedNormal;
                }
            }
        }
    }

    public void UpdateOutfitButtonVisuals(string selectedOutfit)
    {
        foreach (var outfit in outfitButtons)
        {
            if (outfit.outfitButton != null)
            {
                Image buttonImage = outfit.outfitButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    if (!outfit.isUnlocked)
                    {
                        // Always use locked sprite if locked
                        buttonImage.sprite = outfit.lockedNormal;
                    }
                    else if (outfit.outfitName == selectedOutfit)
                    {
                        // If unlocked and selected, use selected sprite
                        buttonImage.sprite = outfit.unlockedSelected;
                    }
                    else
                    {
                        // If unlocked but not selected, use normal sprite
                        buttonImage.sprite = outfit.unlockedNormal;
                    }
                }
            }
        }
    }

    public void ResetUISelections()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // Update outfit buttons based on currently staged outfit
        if (materialAssignment != null)
        {
            UpdateOutfitButtonVisuals(materialAssignment.StagedOutfit);
        }

        ResetAccessorySelections();
    }

    public void ResetAccessorySelections()
    {
        foreach (var accessory in accessoryToggles)
        {
            accessory.accessoryToggle.SetIsOnWithoutNotify(false);
            if (accessory.buttonBackground != null)
            {
                // Respect lock status
                if (!accessory.isUnlocked)
                {
                    accessory.buttonBackground.sprite = accessory.lockedNormal;
                }
                else
                {
                    accessory.buttonBackground.sprite = accessory.unlockedNormal;
                }
            }
        }
    }
    
    // Unlock an outfit by name
    public void UnlockOutfit(string outfitName)
    {
        var outfit = outfitButtons.Find(o => o.outfitName == outfitName);
        if (outfit != null && !outfit.isUnlocked)
        {
            outfit.isUnlocked = true;
            UpdateOutfitButtonState(outfit);
            
            // Update lock state in MaterialAssignment and BlendShapeController
            PropagateLocksToOtherSystems();
            
            Debug.Log($"Outfit '{outfitName}' has been unlocked!");
        }
    }
    
    // Unlock an accessory by index
    public void UnlockAccessory(int accessoryIndex)
    {
        if (accessoryIndex >= 0 && accessoryIndex < accessoryToggles.Count)
        {
            var accessory = accessoryToggles[accessoryIndex];
            if (!accessory.isUnlocked)
            {
                accessory.isUnlocked = true;
                UpdateAccessoryToggleState(accessory);
                
                // Update lock state in MaterialAssignment
                PropagateLocksToOtherSystems();
                
                Debug.Log($"Accessory at index {accessoryIndex} has been unlocked!");
            }
        }
    }
}