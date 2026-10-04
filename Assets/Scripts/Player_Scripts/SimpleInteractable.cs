using UnityEngine;

public class SimpleIteractable : MonoBehaviour, IInteractable
{
    public string InteractionName { get; }

    public void Interact()
    {
        DevLog.Log("Odpalono interakcję z: " + gameObject.name);
        
    }
}