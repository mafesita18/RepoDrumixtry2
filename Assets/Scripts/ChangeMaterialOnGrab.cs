using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ChangeMaterialOnGrab : MonoBehaviour
{
    public MeshRenderer targetMeshRenderer;
    public Material grabMaterial;
    
    [Header("Señalética del Objeto")]
    public GameObject tooltipUI; // Arrastra aquí el texto (ej: cerealtext)

    private Material originalMaterial;
    private XRBaseInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        
        if (targetMeshRenderer == null)
        {
            targetMeshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (targetMeshRenderer != null)
        {
            originalMaterial = targetMeshRenderer.material;
        }

        // Se asegura de que la señalética arranque oculta al iniciar
        if (tooltipUI != null)
        {
            tooltipUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (interactable != null && interactable.isSelected)
        {
            // 1. Cambia material a morado
            if (targetMeshRenderer != null && grabMaterial != null)
            {
                targetMeshRenderer.material = grabMaterial;
            }

            // 2. MUESTRA LA SEÑALÉTICA AL AGARRAR
            if (tooltipUI != null)
            {
                tooltipUI.SetActive(true);
            }
        }
        else
        {
            // 1. Vuelve al material original
            if (targetMeshRenderer != null && originalMaterial != null)
            {
                targetMeshRenderer.material = originalMaterial;
            }

            // 2. OCULTA LA SEÑALÉTICA AL SOLTAR
            if (tooltipUI != null)
            {
                tooltipUI.SetActive(false);
            }
        }
    }
}