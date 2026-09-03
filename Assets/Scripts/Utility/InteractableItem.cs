using UnityEngine;

public class InteractableItem : MonoBehaviour
{
    [Header("Wizualia")]
    [SerializeField] private Renderer itemRenderer;
    [SerializeField] private Material highlightMaterial; // Twój materiał M_ItemHighlight

    private Material[] originalMaterials;
    private bool isHighlighted = false;

    private void Awake()
    {
        if (itemRenderer == null)
            itemRenderer = GetComponent<Renderer>();

        // Zapisujemy oryginalne materiały
        originalMaterials = itemRenderer.sharedMaterials;
    }

    public void SetHighlight(bool state)
    {
        if (isHighlighted == state) return;
        isHighlighted = state;

        if (state)
        {
            // Dodajemy materiał poświaty jako dodatkową warstwę na modelu
            Material[] highlightedMats = new Material[originalMaterials.Length + 1];
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                highlightedMats[i] = originalMaterials[i];
            }
            highlightedMats[highlightedMats.Length - 1] = highlightMaterial;
            itemRenderer.materials = highlightedMats;
        }
        else
        {
            // Przywracamy oryginalne materiały
            itemRenderer.materials = originalMaterials;
        }
    }

    public void Interact()
    {
        Debug.Log("Podniesiono/użyto: " + gameObject.name);
        // Tutaj dodajesz logikę podnoszenia (np. przyczepienie do rąk gracza)
    }
}