using UnityEngine;

public class SimpleIteractable : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        Debug.Log("Odpalono interakcję z: " + gameObject.name);
        
    }
}