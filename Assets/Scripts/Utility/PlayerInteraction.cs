using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3.0f;
    [SerializeField] private LayerMask interactLayer; // Ustaw na warstwę z przedmiotami

    private InteractableItem currentTarget;

    void Update()
    {
        CheckForInteractable();

        // Klawisz interakcji (np. E)
        bool ePressed = false;
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            ePressed = UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
        }

        if (ePressed && currentTarget != null)
        {
            currentTarget.Interact();
        }
    }

    private void CheckForInteractable()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        {
            InteractableItem item = hit.collider.GetComponent<InteractableItem>();

            if (item != null)
            {
                if (currentTarget != item)
                {
                    // Przestań podświetlać poprzedni
                    if (currentTarget != null) currentTarget.SetHighlight(false);

                    // Podświetl nowy
                    currentTarget = item;
                    currentTarget.SetHighlight(true);
                }
                return;
            }
        }

        // Jeśli promień w nic nie trafia, wyłącz podświetlenie
        if (currentTarget != null)
        {
            currentTarget.SetHighlight(false);
            currentTarget = null;
        }
    }
}