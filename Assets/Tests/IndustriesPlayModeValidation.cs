#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using Object = UnityEngine.Object;

/// <summary>Contrôle d'intégration explicite, hors des scènes et exclu du Player.</summary>
public sealed class IndustriesPlayModeValidation : MonoBehaviour
{
    private readonly List<string> results = new List<string>();
    private GameManager game;
    private ScoreManager score;
    private InputRouter input;
    private Player player;
    private TableApi api;
    private bool inputEnabled;

    [MenuItem("Flipper/Validation/Tester Industries en Play")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Industries")
            throw new InvalidOperationException("Lancer Industries en Play avant ce test.");
        if(GameManager.Instance.BallsRemaining!=3 || ScoreManager.Instance.Score!=0 || ScoreManager.Instance.Multiplier!=1)
            throw new InvalidOperationException("Ce scénario exige une nouvelle session Industries (trois billes, score zéro, ×1), ouverte directement hors transition.");
        if(Object.FindAnyObjectByType<IndustriesPlayModeValidation>()!=null)return;
        var go=new GameObject("IndustriesValidation"){hideFlags=HideFlags.DontSave};
        go.AddComponent<IndustriesPlayModeValidation>().StartCoroutine("Validate");
    }

    private IEnumerator Validate()
    {
        game=GameManager.Instance;score=ScoreManager.Instance;input=InputRouter.Instance;
        player=Object.FindAnyObjectByType<Player>();api=player.TableApi;
        inputEnabled=input.enabled;input.enabled=false;
        Application.runInBackground=true;
        yield return new WaitForSeconds(1f);
        Check("initialisation native et distribution d'une bille",Object.FindAnyObjectByType<IndustriesVpeGame>().IsInitialized &&
              game.State==GameManager.GameState.ReadyToLaunch && Balls().Length==1);
        var left=api.Flipper("LeftFlipper");var right=api.Flipper("RightFlipper");
        var flippers=Object.FindObjectsByType<FlipperComponent>();
        var before=new Quaternion[flippers.Length];for(int i=0;i<flippers.Length;i++)before[i]=flippers[i].transform.localRotation;
        SetInput("LeftFlipperHeld",true);SetInput("RightFlipperHeld",true);
        yield return new WaitForSeconds(.3f);
        bool moved=true;for(int i=0;i<flippers.Length;i++)moved &= Quaternion.Angle(before[i],flippers[i].transform.localRotation)>10f;
        Check("commandes des deux flippers depuis InputRouter",moved);
        SetInput("LeftFlipperHeld",false);SetInput("RightFlipperHeld",false);
        var ball=Balls()[0];float startZ=ball.transform.localPosition.z;
        SetInput("PlungerHeld",true);yield return new WaitForSeconds(1.2f);
        SetInput("PlungerHeld",false);
        float maxZ=startZ;float deadline=Time.time+3f;
        while(Time.time<deadline){if(ball!=null)maxZ=Mathf.Max(maxZ,ball.transform.localPosition.z);yield return null;}
        Check("lancement VPE : sortie du chenal vers le haut du plateau",game.State==GameManager.GameState.Playing && maxZ-startZ>.5f);
        game.TogglePause();yield return new WaitForSecondsRealtime(.2f);
        Vector3 pausedPos=ball!=null?ball.transform.position:Vector3.zero;
        yield return new WaitForSecondsRealtime(.4f);
        Check("pause : bille native immobile",Time.timeScale==0f && (ball==null || Vector3.Distance(pausedPos,ball.transform.position)<.0001f));
        game.TogglePause();yield return null;
        ClearBalls();int points=score.Score;
        var pf=Object.FindAnyObjectByType<PlayfieldComponent>().transform;
        foreach(var target in Object.FindObjectsByType<TargetComponent>()) {
            var position=pf.InverseTransformPoint(target.transform.position);
            var normal=pf.InverseTransformDirection(target.transform.TransformDirection(Vector3.back));normal.y=0f;normal.Normalize();
            int beforeHit=score.Score;
            CreateProbe(position+normal*.06f,-normal);
            yield return new WaitForSeconds(.3f);
            Check("collision physique cible "+target.name,score.Score-beforeHit>=500);
            ClearBalls();
        }
        Check("six collisions : bonus de production",score.Score-points==8000 && Object.FindAnyObjectByType<IndustriesVpeGame>().TargetsLit==0);
        ClearBalls();
        points=score.Score;var scoop=api.Kicker("Kicker1");
        int scoopHits=0;EventHandler<HitEventArgs> observe=(_,e)=>scoopHits++;scoop.Hit+=observe;
        var scoopComponent=Object.FindObjectsByType<KickerComponent>()[0];foreach(var k in Object.FindObjectsByType<KickerComponent>())if(k.name=="Kicker1")scoopComponent=k;
        CreateProbe(pf.InverseTransformPoint(scoopComponent.transform.position)+Vector3.back*.055f,Vector3.forward);
        float captureDeadline=Time.realtimeSinceStartup+3f;
        while(scoopHits==0 && Time.realtimeSinceStartup<captureDeadline)yield return null;
        yield return new WaitForSeconds(.9f);
        Check("scoop : collision physique, score et libération native (points="+(score.Score-points)+", hits="+scoopHits+", captured="+scoop.HasBall()+", balls="+Balls().Length+", position="+(Balls().Length>0?pf.InverseTransformPoint(Balls()[0].transform.position).ToString():"absent")+")",score.Score>=points+1000 && !scoop.HasBall() && Balls().Length==1);scoop.Hit-=observe;
        KickerComponent drain=null;foreach(var k in Object.FindObjectsByType<KickerComponent>())if(k.name=="Drain")drain=k;
        for(int expected=2;expected>=0;expected--){
            ClearBalls();CreateProbe(pf.InverseTransformPoint(drain.transform.position)+Vector3.forward*.055f,Vector3.back);
            yield return new WaitForSeconds(1.8f);
            Check("drain : "+expected+" bille(s) restante(s)",game.BallsRemaining==expected &&
                  (expected>0 ? game.State==GameManager.GameState.ReadyToLaunch && Balls().Length==1 : game.State==GameManager.GameState.GameOver && Balls().Length==0));
        }
        ClearBalls();game.StartGame();yield return new WaitForSeconds(.8f);
        Check("nouvelle partie sans relancer la scène",game.BallsRemaining==3 && score.Score==0 && Balls().Length==1);
        SetInput("PlungerHeld",false);input.enabled=inputEnabled;
        Directory.CreateDirectory("Docs/IndustriesValidation");File.WriteAllLines("Docs/IndustriesValidation/runtime.txt",results);
        Debug.Log("[Industries validation] "+string.Join(" | ",results));
        Destroy(gameObject);
    }
    private void Check(string label,bool success){results.Add((success?"PASS ":"FAIL ")+label);}
    private void SetInput(string name,bool value)=>typeof(InputRouter).GetField("<"+name+">k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(input,value);
    private BallComponent[] Balls()=>Object.FindObjectsByType<BallComponent>(FindObjectsSortMode.None);
    private void ClearBalls(){foreach(var ball in Balls())player.BallManager.DestroyBall(ball.Id);}
    private void CreateProbe(Vector3 position,Vector3 direction)=>player.BallManager.CreateBall(new ProbeOrigin(position,direction));
    private sealed class ProbeOrigin : IBallCreationPosition
    {
        private readonly Vertex3D position,velocity;
        public ProbeOrigin(Vector3 p,Vector3 d)
        {
            float scale=VisualPinball.Unity.Physics.ScaleInv;
            position=new Vertex3D(p.x/scale,-p.z/scale,0);
            // La vitesse VPE est exprimée par pas de référence de 10 ms.
            velocity=new Vertex3D(d.x*.015f/scale,-d.z*.015f/scale,0);
        }
        public Vertex3D GetBallCreationPosition()=>position;
        public Vertex3D GetBallCreationVelocity()=>velocity;
    }
    private void OnDestroy(){if(input!=null)input.enabled=inputEnabled;}
}
#endif
