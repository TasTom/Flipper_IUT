#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in Play-mode integration checks, never attached to a saved scene.</summary>
public class NeutralMechanismRuntimeValidation : MonoBehaviour
{
    private readonly List<string> results = new List<string>();
    private int failures;
    private Transform table;
    private BallManager balls;
    private GameManager game;
    private PinballScoop scoop;
    private BallLock ballLock;
    private PlayfieldMagnet magnet;
    private Rigidbody ball;
    private int captures, ejects;
    private bool background;
    private void Check(bool valid, string label) { results.Add((valid?"PASS ":"FAIL ")+label); if(!valid)failures++; }
    public static void Begin()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play required");
        bool previous=Application.runInBackground;Application.runInBackground=true;
        var go=new GameObject("MechanismValidation_OptIn");go.hideFlags=HideFlags.DontSave;go.AddComponent<NeutralMechanismRuntimeValidation>().background=previous;
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
    private IEnumerator Start()
    {
        table=GameObject.Find("PinballTable").transform;balls=BallManager.Instance;game=GameManager.Instance;
        scoop=FindAnyObjectByType<PinballScoop>();ballLock=FindAnyObjectByType<BallLock>();magnet=FindAnyObjectByType<PlayfieldMagnet>();
        scoop.Captured+=Capture;scoop.Ejected+=Eject;
        game.StartGame();game.NotifyBallLaunched();ball=table.Find("Gameplay/Ball").GetComponent<Rigidbody>();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var previous=Physics.simulationMode;
        try
        {
            Physics.simulationMode=SimulationMode.Script;
            ball.position=magnet.transform.position+table.TransformVector(new Vector3(.3f,.207f,0));ball.linearVelocity=Vector3.zero;ball.angularVelocity=Vector3.zero;ball.useGravity=false;Physics.SyncTransforms();
            var initial=table.InverseTransformVector(ball.position-magnet.transform.position);magnet.Pulse();
            var tick=typeof(PlayfieldMagnet).GetMethod("FixedUpdate",flags);for(int i=0;i<40;i++){tick.Invoke(magnet,null);Physics.Simulate(Time.fixedDeltaTime);}
            var final=table.InverseTransformVector(ball.position-magnet.transform.position);
            Check(Mathf.Abs(final.x)<Mathf.Abs(initial.x),"Magnet attracts a surface ball gradually");Check(Mathf.Abs(final.y-initial.y)<.015f,"Magnet adds no vertical movement");Check(!ball.isKinematic,"Magnet keeps a live physical ball");
            ball.position=magnet.transform.position+table.TransformVector(new Vector3(.3f,1.1f,0));ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();tick.Invoke(magnet,null);Physics.Simulate(Time.fixedDeltaTime);
            Check(ball.linearVelocity.sqrMagnitude<.0001f,"Overhead ramp ball unaffected by magnet");magnet.Stop();
            ball.position=magnet.transform.position+table.TransformVector(new Vector3(.3f,.207f,0));ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();tick.Invoke(magnet,null);Physics.Simulate(Time.fixedDeltaTime);
            Check(ball.linearVelocity.sqrMagnitude<.0001f,"Coil off removes attraction");
        }
        finally { ball.useGravity=true;Physics.simulationMode=previous; }
        FireScoop();yield return new WaitForSeconds(.28f);
        Check(captures==1,"Physical shot falls through aperture and is captured");
        yield return new WaitForSeconds(.8f);
        Check(ejects==1 && balls.HeldBallCount==0,"Unlit scoop returns its ball");Check(balls.LiveBallCount==1,"Unlit scoop does not create or lose a ball");
        game.StartGame();game.NotifyBallLaunched();int remaining=game.BallsRemaining;captures=0;ejects=0;ballLock.Arm();FireScoop();
        yield return new WaitForSeconds(.95f);
        Check(ballLock.LockedCount==1,"First lit scoop shot locks one physical ball");Check(balls.HeldBallCount==1,"Stored ball is explicitly excluded from active physics");Check(balls.LiveBallCount==1,"Replacement ball ready in launcher");Check(game.BallsRemaining==remaining,"Lock does not spend a player ball");Check(game.State==GameManager.GameState.ReadyToLaunch,"Replacement accepts launcher input");
        game.NotifyBallLaunched();ballLock.Arm();FireScoop();
        yield return new WaitForSeconds(.2f); game.TogglePause();int held=balls.HeldBallCount;
        yield return new WaitForSecondsRealtime(.75f);
        Check(balls.HeldBallCount==held && ballLock.LockedCount==1,"Pause stops scoop dwell and release timers");game.TogglePause();
        float deadline=Time.time+2.5f;while(balls.LiveBallCount<3 && Time.time<deadline)yield return new WaitForFixedUpdate();
        Check(ballLock.LockedCount==0 && balls.HeldBallCount==0,"Both locked balls released");Check(balls.LiveBallCount==3,"Lock starts exactly three active balls");Check(ejects==2,"Each stored ball ejected once");
        var all=FindObjectsByType<Rigidbody>().Where(b=>b.CompareTag("Ball")&&!b.isKinematic).ToArray();Check(all.All(b=>b.detectCollisions),"Released balls regain collisions");
        var manager=FindAnyObjectByType<MultiballManager>();manager.TriggerMultiball();Check(balls.LiveBallCount==3,"Repeated start cannot exceed three active balls");
        game.StartGame();Check(balls.LiveBallCount==1 && balls.HeldBallCount==0 && ballLock.LockedCount==0,"Restart clears multiball and returns to single-ball play");
        game.NotifyBallLaunched();ballLock.Arm();FireScoop();yield return new WaitForSeconds(.95f);
        Check(ballLock.LockedCount==1 && balls.HeldBallCount==1,"Ball retained for restart-during-lock test");
        game.StartGame();Check(balls.LiveBallCount==1 && balls.HeldBallCount==0 && ballLock.LockedCount==0,"Restart also clears a still-locked physical ball");
        File.WriteAllText("Tools/unity/out/mechanism-runtime.txt",string.Join(Environment.NewLine,results)+Environment.NewLine+"SUMMARY checks="+results.Count+" failures="+failures);
        scoop.Captured-=Capture;scoop.Ejected-=Eject;Application.runInBackground=background;Destroy(gameObject);
    }
    private void Capture()=>captures++;
    private void Eject()=>ejects++;
    private void FireScoop()
    {
        ball=FindObjectsByType<Rigidbody>().First(b=>b.CompareTag("Ball")&&!b.isKinematic&&b.gameObject.activeInHierarchy);
        var p=table.InverseTransformPoint(scoop.transform.position);ball.position=table.TransformPoint(p+new Vector3(0,.207f,-.9f));ball.linearVelocity=table.forward*12;ball.angularVelocity=Vector3.zero;Physics.SyncTransforms();
    }
}
#endif
