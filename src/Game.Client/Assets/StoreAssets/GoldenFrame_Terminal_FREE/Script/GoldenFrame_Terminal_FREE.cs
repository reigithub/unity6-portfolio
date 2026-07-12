using UnityEngine;

public class GoldenFrame_Terminal_FREE : MonoBehaviour
{
    public GameObject textMesh;
    public MeshRenderer screenRenderer;
    public Material emissiveMaterial;
    public Material normalMaterial;

    private bool isOn = false;

    private void Start()
    {
        // Upewnij się, że komputer startuje jako wyłączony
        TurnOffComputer();
    }

    private void OnMouseDown()
    {
        if (isOn)
            TurnOffComputer();
        else
            TurnOnComputer();
    }

    private void TurnOnComputer()
    {
        if (screenRenderer != null && emissiveMaterial != null)
        {
            Material[] mats = screenRenderer.materials;
            mats[1] = emissiveMaterial; // index może być inny – zależnie od slotu
            screenRenderer.materials = mats;
        }

        if (textMesh != null)
            textMesh.SetActive(true);

        isOn = true;
    }

    private void TurnOffComputer()
    {
        if (screenRenderer != null && normalMaterial != null)
        {
            Material[] mats = screenRenderer.materials;
            mats[1] = normalMaterial;
            screenRenderer.materials = mats;
        }

        if (textMesh != null)
            textMesh.SetActive(false);

        isOn = false;
    }
}
