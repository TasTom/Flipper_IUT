using UnityEngine;

/// <summary>Masque les seuls rendus de démonstration après l'activation des composants visuels VPE.</summary>
[ExecuteAlways, DefaultExecutionOrder(1000)]
public sealed class IndustriesPresentation : MonoBehaviour
{
    [SerializeField] private Renderer[] hiddenRenderers;

    private void OnEnable()
    {
        ApplyVisibility();
    }

    private void LateUpdate()
    {
        // VPE réactive ses MeshRenderer quand un composant visuel se recharge.
        // Les références sont sérialisées : aucune recherche ni allocation par frame.
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (hiddenRenderers == null) return;
        foreach (var renderer in hiddenRenderers)
            if (renderer != null && renderer.enabled) renderer.enabled = false;
    }
}
