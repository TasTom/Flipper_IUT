#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisualPinball.Unity;
using Object = UnityEngine.Object;

public sealed class IndustriesTransitionValidation : MonoBehaviour
{
    private readonly List<string> results=new List<string>();
    [MenuItem("Flipper/Validation/Tester le seuil depuis Neutral en Play")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying || SceneManager.GetActiveScene().name!="Neutral")throw new InvalidOperationException("Neutral en Play attendu.");
        var go=new GameObject("TransitionValidation"){hideFlags=HideFlags.DontSave};DontDestroyOnLoad(go);
        go.AddComponent<IndustriesTransitionValidation>().StartCoroutine("Validate");
    }
    private IEnumerator Validate()
    {
        Application.runInBackground=true;Directory.CreateDirectory("Docs/IndustriesValidation");
        yield return new WaitForSeconds(1f);
        var game=GameManager.Instance;var score=ScoreManager.Instance;
        game.AwardExtraBall(); // La session inclut aussi les bonus de billes de Neutral.
        score.RestoreSession(99999,2);score.BeginBall();
        yield return null;yield return null;
        Check("99999 : Neutral reste active",SceneManager.GetActiveScene().name=="Neutral" && !SceneTransition.IsPlaying);
        score.AddBonus(1);
        int remaining=game.BallsRemaining; // Après les abonnés de score : leurs paliers font partie de la transaction.
        yield return null;yield return null;
        Check("100000 : transition unique, partie et temps suspendus",SceneTransition.IsPlaying && Time.timeScale==0f && game.State==GameManager.GameState.Paused);
        var overlay=GameObject.Find("TransitionOverlay");
        yield return new WaitForSecondsRealtime(1f);
        ScreenCapture.CaptureScreenshot("Docs/IndustriesValidation/transition.png");
        float deadline=Time.realtimeSinceStartup+90f;
        while(SceneManager.GetActiveScene().name!="Industries" && Time.realtimeSinceStartup<deadline)yield return null;
        Check("voile conservé pendant le chargement Single",overlay!=null && GameObject.Find("TransitionOverlay")==overlay && Time.timeScale==0f);
        while(SceneTransition.IsPlaying && Time.realtimeSinceStartup<deadline)yield return null;
        yield return new WaitForSeconds(.6f);
        Check("arrivée : score, billes et multiplicateur conservés",SceneManager.GetActiveScene().name=="Industries" &&
              GameManager.Instance!=null && ScoreManager.Instance.Score==100000 && GameManager.Instance.BallsRemaining==remaining && ScoreManager.Instance.Multiplier==2);
        results.Add("SESSION score="+ScoreManager.Instance.Score+" billes="+GameManager.Instance.BallsRemaining+" attendues="+remaining+" multiplicateur="+ScoreManager.Instance.Multiplier);
        Check("arrivée : temps repris, moteur VPE prêt, une bille distribuée",Time.timeScale==1f &&
              Object.FindAnyObjectByType<IndustriesVpeGame>()?.IsInitialized==true && Object.FindAnyObjectByType<PhysicsEngine>()?.IsInitialized==true &&
              Object.FindObjectsByType<BallComponent>(FindObjectsSortMode.None).Length==1);
        Check("transfert consommé une seule fois",!TableSessionTransfer.TryConsume("Industries",out _));
        ScoreManager.Instance.AddBonus(500);yield return null;yield return null;
        Check("pas de rebouclage depuis Industries",!SceneTransition.IsPlaying && SceneManager.GetActiveScene().name=="Industries");
        File.WriteAllLines("Docs/IndustriesValidation/transition.txt",results);
        ScreenCapture.CaptureScreenshot("Docs/IndustriesValidation/industries-after-transition.png");
        Debug.Log("[Transition validation] "+string.Join(" | ",results));Destroy(gameObject);
    }
    private void Check(string label,bool pass)=>results.Add((pass?"PASS ":"FAIL ")+label);
}
#endif
