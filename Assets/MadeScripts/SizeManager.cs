using UnityEngine;
using UnityEngine.UI;

public sealed class SizeManager : MonoBehaviour
{
    [Header("UI Controls")]
    public Slider sizeSlider; // Use the slider to swap between sizes

    [Header("Hoodies")]
    public GameObject sizeM;
    public GameObject sizeL;

    void Start()
    {
        if (sizeSlider != null)
            sizeSlider.onValueChanged.AddListener(HandleSlider);

        // Default to Medium
        UpdateActiveHoodie(0f);
        sizeSlider.value = 0f;
    }

    public void HandleSlider(float value)
    {
        UpdateActiveHoodie(value);
    }

    private void UpdateActiveHoodie(float value)
    {
        // Hard switch: no more blending, no more "transparent" bone-fighting
        bool isLarge = value >= 0.5f;

        if (sizeM != null) sizeM.SetActive(!isLarge);
        if (sizeL != null) sizeL.SetActive(isLarge);
    }
}