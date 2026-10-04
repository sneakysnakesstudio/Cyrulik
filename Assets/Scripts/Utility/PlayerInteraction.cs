using UnityEngine;

/// <summary>
/// PRZESTARZAŁY — zastąpiony przez PlayerMovement.CheckForInteractable().
/// Skrypt pozostawiony dla kompatybilności wstecznej, ale wyłączony w Awake,
/// aby nie generował GC przez GetComponent w Update (GetComponentNullErrorMessage 0.6 KB/klatkę).
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3.0f;
    [SerializeField] private LayerMask interactLayer;

    private InteractableItem currentTarget;

    private void Awake()
    {
        // Wyłącz ten komponent — logika interakcji jest obsługiwana przez PlayerMovement
        enabled = false;
    }
}