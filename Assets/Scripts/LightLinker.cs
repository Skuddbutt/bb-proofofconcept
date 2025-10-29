using UnityEngine;
using System.Collections.Generic;

public class LightLinker : MonoBehaviour
{
    [System.Serializable]
    public class LightObjectLink
    {
        public Light linkedLight;  // The light to affect the object(s)
        public List<Renderer> objectRenderers;  // List of renderers of the objects to be affected by the light
        public LayerMask lightLayer; // The layer to which the light should apply
        public bool isActive = true;  // Whether the light is active for these objects
        public bool castShadows = true; // Whether objects should cast shadows regardless of the light's influence
        public bool receiveShadows = true; // Whether objects should receive shadows from other objects
    }

    // List of light-object links
    public List<LightObjectLink> lightObjectLinks;

    private void Start()
    {
        // Initialize: Ensure lights are turned off for all objects by default
        UpdateLightInfluence();
    }

    private void Update()
    {
        // Continuously update the light influence
        UpdateLightInfluence();
    }

    private void UpdateLightInfluence()
    {
        foreach (var link in lightObjectLinks)
        {
            if (link.isActive && link.linkedLight != null && link.objectRenderers != null)
            {
                // Enable the light's influence by adjusting its culling mask
                link.linkedLight.cullingMask = link.lightLayer;

                // Ensure the renderers of the objects are affected by the light
                foreach (var renderer in link.objectRenderers)
                {
                    SetLightInfluence(link.linkedLight, renderer, true, link.castShadows, link.receiveShadows);
                }
            }
            else
            {
                // Disable the light's influence for the objects
                foreach (var renderer in link.objectRenderers)
                {
                    SetLightInfluence(link.linkedLight, renderer, false, link.castShadows, link.receiveShadows);
                }
            }
        }
    }

    private void SetLightInfluence(Light light, Renderer renderer, bool enable, bool castShadows, bool receiveShadows)
    {
        if (light != null && renderer != null)
        {
            if (enable)
            {
                // Apply the correct light layer to this object
                SetLayerRecursively(renderer.gameObject, light.cullingMask);

                // Enable shadow casting, regardless of whether the object is affected by the light layer
                renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

                // Set whether the object should receive shadows
                SetReceiveShadows(renderer, receiveShadows);
            }
            else
            {
                // Deactivate the light for this object
                SetLayerRecursively(renderer.gameObject, LayerMask.NameToLayer("IgnoreLights")); // Custom layer to ignore lights
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // Disable shadow casting when not affected
                SetReceiveShadows(renderer, false); // Disable receiving shadows when not affected
            }
        }
    }

    // Set whether the object should receive shadows
    private void SetReceiveShadows(Renderer renderer, bool receive)
    {
        if (renderer != null)
        {
            renderer.receiveShadows = receive;
        }
    }

    // Set the layer recursively for the object and its children
    private void SetLayerRecursively(GameObject obj, LayerMask layer)
    {
        int layerIndex = Mathf.RoundToInt(Mathf.Log(layer.value, 2)); // Convert layer mask to layer index

        // Set the object's layer
        obj.layer = layerIndex;

        // If the object has children, apply the layer to them as well
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // Function to enable or disable receiving shadows for specific objects
    public void EnableReceiveShadows(bool enable)
    {
        foreach (var link in lightObjectLinks)
        {
            foreach (var renderer in link.objectRenderers)
            {
                SetReceiveShadows(renderer, enable);
            }
        }
    }
}
