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
        var engine=Object.FindAnyObjectByType<IndustriesVpeGame>();
        var config=UnityEditor.AssetDatabase.LoadAssetAtPath<IndustriesConfig>("Assets/Generated/Industries/IndustriesConfig.asset");
        Check("six collisions : banques chargées, bonus différé",engine.Production!=null
            ? score.Score-points==6*config.targetPoints && engine.Production.Loaded==3 && engine.Production.Processed==0 && engine.TargetsLit==6
            : score.Score-points==6*config.targetPoints+config.sixTargetsBonus && engine.TargetsLit==0);
        if(engine.Production!=null)
        {
            // Vrais tirs sur le(s) parcours, sans appeler les règles directement.
            var shots=config.sharedProductionRamp ? new[]{new Vector3(.280f,0,-.799f)}
                : new[]{new Vector3(.184f,0,-.73f),new Vector3(.355f,0,-.57f)};
            var directions=config.sharedProductionRamp ? new[]{new Vector3(.32f,0,.95f)}
                : new[]{Vector3.forward,new Vector3(.53f,0,.85f)};
            if(config.sharedProductionRamp)
            {
                var ramp=Array.Find(Object.FindObjectsByType<RampComponent>(),r=>r.name=="Ramp1");
                Vector3 Point(int i){var c=ramp.DragPoints[i].Center;float k=VisualPinball.Unity.Physics.ScaleInv;
                    return pf.InverseTransformPoint(ramp.transform.TransformPoint(new Vector3(c.X*k,c.Z*k,-c.Y*k)));}
                var direction=Point(1)-Point(0);direction.y=0;directions[0]=direction.normalized;
                shots[0]=Point(0)-directions[0]*.06f;shots[0].y=0;
            }
            for(int line=0;line<shots.Length;line++)
            {
                var trace=new System.Text.StringBuilder();
                EventHandler<HitEventArgs> entry=(_,e)=>trace.Append(" ENTRY "+e.BallId);
                EventHandler<HitEventArgs> exit=(_,e)=>trace.Append(" EXIT "+e.BallId);
                var entryApi=api.Trigger(line==0&&!config.sharedProductionRamp?IndustriesProduction.WoodEntry:IndustriesProduction.TextileEntry);
                var exitApi=api.Trigger(line==0&&!config.sharedProductionRamp?IndustriesProduction.WoodExit:IndustriesProduction.TextileExit);
                entryApi.Hit+=entry;exitApi.Hit+=exit;
                ClearBalls();points=score.Score;CreateProbe(shots[line],directions[line]*(config.sharedProductionRamp?3.5f/1.5f:2f));
                float end=Time.time+2f, next=Time.time;
                while(Time.time<end && (engine.Production.Processed&(1<<line))==0){
                    if(Time.time>=next){if(Balls().Length>0)trace.Append(" "+pf.InverseTransformPoint(Balls()[0].transform.position).ToString("F3"));next+=.1f;}yield return null;}
                int mask=config.sharedProductionRamp?3:1<<line;
                Check("tir complet rampe "+(config.sharedProductionRamp?"commune":line==0?"bois":"textile")+trace,
                    (engine.Production.Processed&mask)==mask && score.Score>=points+config.processingBonus*(config.sharedProductionRamp?2:1));
                entryApi.Hit-=entry;exitApi.Hit-=exit;
            }
            if(config.sharedProductionRamp)
            {
                foreach(float speed in new[]{4.5f,5.5f})
                {
                    int entered=-1,exited=-2;
                    var entryApi=api.Trigger(IndustriesProduction.TextileEntry);var exitApi=api.Trigger(IndustriesProduction.TextileExit);
                    EventHandler<HitEventArgs> enter=(_,e)=>entered=e.BallId,leave=(_,e)=>exited=e.BallId;
                    entryApi.Hit+=enter;exitApi.Hit+=leave;
                    ClearBalls();points=score.Score;CreateProbe(shots[0],directions[0]*(speed/1.5f));
                    var trace=new System.Text.StringBuilder();float next=Time.time;
                    float end=Time.time+2.5f;while(Time.time<end&&entered!=exited){
                        if(Time.time>=next){if(Balls().Length>0)trace.Append(" "+pf.InverseTransformPoint(Balls()[0].transform.position).ToString("F3"));next+=.1f;}yield return null;}
                    Check("Ramp1 : traversée à "+speed+" m/s, même bille et sans nouveau bonus (entrée="+entered+", sortie="+exited+")"+trace,
                        entered>=0&&entered==exited&&score.Score==points);
                    entryApi.Hit-=enter;exitApi.Hit-=leave;
                }
                // Un tir insuffisant doit pouvoir redescendre par l'entrée, sans sortir du guide.
                ClearBalls();CreateProbe(shots[0],directions[0]*(2.5f/1.5f));
                float high=0,weakEnd=Time.time+1.8f;bool returned=false;
                while(Time.time<weakEnd){
                    foreach(var probe in Balls()){
                        var p=pf.InverseTransformPoint(probe.transform.position);high=Mathf.Max(high,p.y);
                        if(high>.040f&&p.y<.025f&&p.z<shots[0].z+.055f)returned=true;
                    }
                    if(returned)break;yield return null;
                }
                Check("Ramp1 : tir faible à 2,5 m/s redescend par l'entrée",returned&&Balls().Length==1);
            }
        }
        var ceiling=Array.Find(Object.FindObjectsByType<PrimitiveComponent>(),p=>p.name=="Ramp1TurnCeiling");
        if(ceiling!=null)
        {
            var ramp=Array.Find(Object.FindObjectsByType<RampComponent>(),r=>r.name=="Ramp1");
            var floor=Array.Find(ramp.GetComponentsInChildren<MeshFilter>(),f=>f.name=="Floor");
            var vertices=floor.sharedMesh.vertices;
            foreach(var sample in new[]{new Vector3(.490f,.046f,-.267f),new Vector3(.434f,.046f,-.113f)})
            {
                Vector3 origin=Vector3.zero;float distance=float.MaxValue;
                for(int i=0;i<vertices.Length;i+=2){
                    var p=pf.InverseTransformPoint(floor.transform.TransformPoint((vertices[i]+vertices[i+1])*.5f));
                    float d=(p-sample).sqrMagnitude;if(d<distance){distance=d;origin=p;}
                }
                ClearBalls();int roofHits=0;
                EventHandler<HitEventArgs> hit=(_,e)=>roofHits++;ceiling.PrimitiveApi.Hit+=hit;
                CreateProbe(origin+Vector3.up*.001f,Vector3.up);float top=0,end=Time.time+.25f;
                while(Time.time<end){foreach(var probe in Balls())top=Mathf.Max(top,pf.InverseTransformPoint(probe.transform.position).y+probe.Radius*VisualPinball.Unity.Physics.ScaleInv);yield return null;}
                ceiling.PrimitiveApi.Hit-=hit;
                Check("plafond VPE : rebond vertical contenu à "+origin.ToString("F3")+" (hits="+roofHits+", sommet="+top.ToString("F4")+")",
                    roofHits>0&&Balls().Length==1&&top<=origin.y+.0355f);
            }
        }
        ClearBalls();
        points=score.Score;var scoop=api.Kicker("Kicker1");
        int scoopHits=0;EventHandler<HitEventArgs> observe=(_,e)=>scoopHits++;scoop.Hit+=observe;
        var scoopComponent=Object.FindObjectsByType<KickerComponent>()[0];foreach(var k in Object.FindObjectsByType<KickerComponent>())if(k.name=="Kicker1")scoopComponent=k;
        CreateProbe(pf.InverseTransformPoint(scoopComponent.transform.position)+Vector3.back*.055f,Vector3.forward);
        float captureDeadline=Time.realtimeSinceStartup+3f;
        while(scoopHits==0 && Time.realtimeSinceStartup<captureDeadline)yield return null;
        yield return new WaitForSeconds(1.15f);
        int expectedScoop=engine.Production!=null ? config.scoopPoints+2*config.deliveryBonusPerProduct+config.combinedDeliveryBonus : config.scoopPoints;
        Check("scoop : livraison, score et libération native (points="+(score.Score-points)+", hits="+scoopHits+", captured="+scoop.HasBall()+", balls="+Balls().Length+")",score.Score==points+expectedScoop && !scoop.HasBall() && Balls().Length==1);scoop.Hit-=observe;
        if(engine.Production!=null)
        {
            float rearmDeadline=Time.time+2f;
            while(Time.time<rearmDeadline && Array.Exists(Object.FindObjectsByType<DropTargetComponent>(),t=>api.DropTarget(t).IsDropped))yield return null;
            Check("livraison : commandes consommées et cibles réarmées (loaded="+engine.Production.Loaded+", processed="+engine.Production.Processed+", lit="+engine.TargetsLit+", drops="+string.Join(",",Array.ConvertAll(Object.FindObjectsByType<DropTargetComponent>(),t=>t.name+":"+api.DropTarget(t).IsDropped))+")",engine.Production.Loaded==0 && engine.Production.Processed==0 && engine.TargetsLit==0 && Array.TrueForAll(Object.FindObjectsByType<DropTargetComponent>(),t=>!api.DropTarget(t).IsDropped));
        }
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
        string report=ceiling!=null?"Docs/IndustriesCoveredRamp":config.sharedProductionRamp?"Docs/IndustriesRamp1":engine.Production!=null?"Docs/IndustriesLayout":"Docs/IndustriesValidation";
        Directory.CreateDirectory(report);File.WriteAllLines(report+"/runtime.txt",results);
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
            position=new Vertex3D(p.x/scale,-p.z/scale,p.y/scale);
            // La vitesse VPE est exprimée par pas de référence de 10 ms.
            velocity=new Vertex3D(d.x*.015f/scale,-d.z*.015f/scale,d.y*.015f/scale);
        }
        public Vertex3D GetBallCreationPosition()=>position;
        public Vertex3D GetBallCreationVelocity()=>velocity;
    }
    private void OnDestroy(){if(input!=null)input.enabled=inputEnabled;}
}
#endif
