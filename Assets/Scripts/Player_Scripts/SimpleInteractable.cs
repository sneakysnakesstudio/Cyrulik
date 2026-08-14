using UnityEngine;

public class SimpleIteractable : MonoBehaviour, IInteractable
{
    public string InteractionName { get; }

    public void Interact()
    {
        Debug.Log("Odpalono interakcję z: " + gameObject.name);
        
    }
}