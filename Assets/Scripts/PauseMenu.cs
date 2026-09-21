using UnityEngine;

/// <summary>
/// Menu de pause (GDD §UI) : un voile « PAUSE » tant que le jeu est figé.
///
/// <para><c>Time.timeScale</c> est le signal, pas un état de <see cref="GameManager"/> :
/// la pause est un réglage de temps, et <c>Update</c> continue de tourner quand
/// <c>timeScale</c> vaut zéro (seules la physique et les coroutines sont figées). Le
/// composant vit donc sur un objet <b>toujours actif</b> — le Canvas — sinon son
/// <c>Update</c> ne tournerait plus une fois le panneau masqué.</para>
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Panneau")]
    [Tooltip("Panneau à montrer en pause. Désactivé partout ailleurs.")]
    [SerializeField] private GameObject panel;

    private void Update()
    {
        if (panel == null)
        {
            return;
        }

        bool pause = Time.timeScale <= 0f;

        if (panel.activeSelf != pause)
        {
            panel.SetActive(pause);
        }
    }
}
