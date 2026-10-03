using TMPro;
using UnityEngine;

/// <summary>GDD §Direction sonore : crédit du morceau visible dans le jeu en pause.</summary>
public sealed class MusicCreditDisplay : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private TMP_Text label;
    private void Awake()
    {
        if(game==null)game=FindAnyObjectByType<GameManager>();
        if(label==null)Debug.LogWarning("[MusicCreditDisplay] Renseigner le texte de crédit dans l’Inspector.",this);
    }
    private void OnEnable(){if(game!=null)game.StateChanged+=OnState;}
    private void Start(){if(game!=null)OnState(game.State);}
    private void OnDisable(){if(game!=null)game.StateChanged-=OnState;}
    private void OnState(GameManager.GameState state){if(label!=null)label.enabled=state==GameManager.GameState.Paused;}
}
