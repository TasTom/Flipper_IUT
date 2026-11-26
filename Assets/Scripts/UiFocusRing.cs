using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Montre un contour sur le bouton sélectionné. Au clavier comme sur la borne, une simple
/// variation de teinte ne suffit pas à repérer où l'on se trouve. Le survol à la souris
/// déplace la sélection, pour qu'un seul bouton soit mis en avant à la fois.
/// </summary>
public sealed class UiFocusRing : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [SerializeField] private Graphic ring;

    public void Bind(Graphic target)
    {
        ring = target;
        Refresh();
    }

    private void OnEnable() => Refresh();

    private void Refresh() =>
        Show(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject);

    public void OnSelect(BaseEventData eventData) => Show(true);

    public void OnDeselect(BaseEventData eventData) => Show(false);

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TryGetComponent<Selectable>(out var selectable) && selectable.IsInteractable())
            EventSystem.current?.SetSelectedGameObject(gameObject);
    }

    private void Show(bool visible)
    {
        if (ring != null) ring.enabled = visible;
    }
}
