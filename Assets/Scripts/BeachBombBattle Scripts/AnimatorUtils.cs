using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AnimatorUtils
{
    public static string GetAnimationStateName(Animator animator, int nameHash)
    {
        // Attempt to get the name from the controller
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            var controller = animator.runtimeAnimatorController;
            
            // Try to get clips from controller
            var clips = controller.animationClips;
            if (clips != null)
            {
                foreach (var clip in clips)
                {
                    if (Animator.StringToHash(clip.name) == nameHash)
                    {
                        return clip.name;
                    }
                }
            }
        }
        
        // Fallback
        return "Unknown (" + nameHash + ")";
    }
}