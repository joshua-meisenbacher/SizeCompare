using UnityEngine;

public class MaterialForceFixer : MonoBehaviour
{
    public Material redMaterial; // Drag your 'New Material' (Red) here

    void Start()
    {
        // Force the Red Material to only show when 'inside' the hoodie
        redMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Greater);
        redMaterial.SetInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.NotEqual);
        redMaterial.SetInt("_Stencil", 1);
        
        Debug.Log("Red Fail-Safe Armed.");
    }
}