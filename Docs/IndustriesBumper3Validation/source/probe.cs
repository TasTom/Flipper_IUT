if (!UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Play requis.");
Application.runInBackground=true;
var field = UnityEngine.Object.FindObjectsByType<VisualPinball.Unity.PlayfieldComponent>().Single(f=>f.gameObject.scene.name=="Industries");
var player = field.GetComponentInParent<VisualPinball.Unity.Player>();
var physics = field.GetComponentInParent<VisualPinball.Unity.PhysicsEngine>();
var originType = typeof(IndustriesPlayModeValidation).GetNestedType("ProbeOrigin", System.Reflection.BindingFlags.NonPublic);
if (originType == null) throw new System.InvalidOperationException("Factory de bille de validation absente.");
var report = new System.Text.StringBuilder();
var trace = new System.Text.StringBuilder();
var samples = new System.Collections.Concurrent.ConcurrentQueue<string>();
var nativePositions = new System.Collections.Concurrent.ConcurrentQueue<(int trial, Unity.Mathematics.float3 position)>();
float k = VisualPinball.Unity.Physics.ScaleInv;
// Native positions/velocities captured during the original loop, plus approaches from both sides.
var positions = new[] { new Vector3(142.9733f,524.2507f,25.00625f), new Vector3(185.9857f,733.9042f,24.99368f), new Vector3(400,730,25) };
var velocities = new[] { new Vector3(.9420415f,-12.63146f,.1667048f), new Vector3(4.146333f,8.968661f,.1753327f), new Vector3(-5,0,0) };
int trial=-1, hits=0, wallHits=0, currentId=0;
bool escaped=false;
double trialStart=0, next=0;
float minX=0,maxX=0,minY=0,maxY=0;
var bumper=field.GetComponentsInChildren<VisualPinball.Unity.BumperComponent>().Single(b=>b.name=="Bumper3");
var bApi=player.TableApi.Bumper(bumper); var wallApi=player.TableApi.Surface("Wall2");
System.EventHandler<VisualPinball.Unity.HitEventArgs> onHit=(_,e)=>{if(e.BallId==currentId){hits++;trace.AppendLine("HIT trial="+trial+" time="+(UnityEditor.EditorApplication.timeSinceStartup-trialStart).ToString("F3")+" Bumper3");}};
System.EventHandler<VisualPinball.Unity.HitEventArgs> onWall=(_,e)=>{if(e.BallId==currentId)wallHits++;};
bApi.Hit+=onHit; wallApi.Hit+=onWall;
void Clear()
{
    foreach(var ball in UnityEngine.Object.FindObjectsByType<VisualPinball.Unity.BallComponent>()) player.BallManager.DestroyBall(ball.Id);
}
void Begin()
{
    Clear(); trial++; hits=0;wallHits=0;escaped=false;currentId=0;
    trialStart=UnityEditor.EditorApplication.timeSinceStartup;next=trialStart;
    var p=positions[trial];var v=velocities[trial];
    // CreateBall adds its radius to the supplied height; supply the rolling surface.
    var local=new Vector3(p.x*k,(p.z-25)*k,-p.y*k);
    var direction=new Vector3(v.x*k/.015f,v.z*k/.015f,-v.y*k/.015f);
    var origin=(VisualPinball.Engine.Game.IBallCreationPosition)System.Activator.CreateInstance(originType,new object[]{local,direction});
    currentId=player.BallManager.CreateBall(origin);
    minX=maxX=p.x;minY=maxY=p.y;
    GameManager.Instance.NotifyBallLaunched();
    report.AppendLine("START trial="+trial+" nativePosition="+p.ToString("F5")+" velocity="+v.ToString("F5")+" ball="+currentId);
}
UnityEditor.EditorApplication.CallbackFunction tick=null;
void Finish()
{
    UnityEditor.EditorApplication.update-=tick;bApi.Hit-=onHit;wallApi.Hit-=onWall;
    Clear();
    while(samples.TryDequeue(out var row)) trace.AppendLine(row);
    report.AppendLine("COMPLETED trials="+(trial+1)+"; placement-only change; Force=7 Scatter=0 retained; probes destroyed.");
    System.IO.File.WriteAllText("Docs/IndustriesBumper3Validation/runtime.txt",report.ToString());
    System.IO.File.WriteAllText("Tools/unity/out/bumper3-move/runtime-trace.txt",trace.ToString());
}
tick=()=>
{
    if (!UnityEditor.EditorApplication.isPlaying) { report.AppendLine("INTERRUPTED Play stopped"); Finish(); return; }
    var now=UnityEditor.EditorApplication.timeSinceStartup;
    if(now>=next)
    {
        next=now+.03;
        if(physics.TryGetBall(currentId,out var ball) && ball!=null)
        {
            var p=field.transform.InverseTransformPoint(ball.transform.position);float x=p.x/k,y=-p.z/k;
            minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
            if(x>350 || y>900)escaped=true;
            var id=currentId;var n=trial;var elapsed=now-trialStart;
            physics.VisitBallStates((in VisualPinball.Unity.BallState s,int index,int count,ulong time)=>{if(s.Id==id){
                samples.Enqueue("trial="+n+" t="+elapsed.ToString("F3")+" position="+s.Position+" velocity="+s.Velocity);
                nativePositions.Enqueue((n,s.Position));
            }});
        }
        else escaped=true;
    }
    while(nativePositions.TryDequeue(out var sample)) {
        if(sample.trial!=trial)continue;
        var p=sample.position;minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
        if(p.x>350 || p.y>900)escaped=true;
    }
    if(now-trialStart<10) return;
    report.AppendLine((escaped?"PASS ":"FAIL ")+"trial="+trial+" escapedOriginalZone="+escaped+" bumper3Hits="+hits+" wall2Hits="+wallHits+" nativeX="+minX+".."+maxX+" nativeY="+minY+".."+maxY);
    if(trial==positions.Length-1)Finish();else Begin();
};
Begin();UnityEditor.EditorApplication.update+=tick;
return "Trois trajectoires natives lancees, dix secondes chacune. Rapport Docs/IndustriesBumper3Validation/runtime.txt; observation VPE sans PhysX ni changement de force.";
