if(!Application.isPlaying)throw new Exception("Play required");
var t=GameObject.Find("PinballTable").transform;var ball=t.Find("Gameplay/Ball").GetComponent<Rigidbody>();var mode=Physics.simulationMode;var sb=new System.Text.StringBuilder();
var data=JsonUtility.FromJson<RefineNeutralFlow.Routes>(System.IO.File.ReadAllText("Tools/unity/out/refined-paths.json"));
try{Physics.simulationMode=SimulationMode.Script;
foreach(var route in data.routes){
var foreign=new System.Collections.Generic.HashSet<string>();
for(int i=1;i<route.points.Length-1;i++){var pos=t.TransformPoint(route.points[i]+Vector3.up*.24f);foreach(var col in Physics.OverlapSphere(pos,.22f,~0,QueryTriggerInteraction.Ignore))if(!col.name.StartsWith(route.name+"_")&&!col.CompareTag("Ball"))foreign.Add(col.name+" at "+i);}
sb.AppendLine("CLEARANCE "+route.name+" "+string.Join(",",foreign));
foreach(float speed in new[]{25f,35f,50f,70f}){
GameManager.Instance.StartGame();ball.gameObject.SetActive(true);ball.isKinematic=false;ball.useGravity=true;
var dir=(route.points[2]-route.points[0]).normalized;dir.y=0;dir.Normalize();ball.position=t.TransformPoint(route.points[0]-dir*.4f+Vector3.up*.23f);ball.linearVelocity=t.TransformDirection(dir*speed);ball.angularVelocity=Vector3.zero;Physics.SyncTransforms();
var gate=t.Find("Gameplay/Ramp_"+route.name+"_Out").GetComponent<RampGate>();int completed=0;Action<RampGate,Collider> onDone=(g,c)=>completed++;gate.Completed+=onDone;
try{float stall=0,maxStall=0;var last=t.InverseTransformPoint(ball.position);bool escape=false;float maxY=0;for(int n=0;n<4000&&ball.gameObject.activeSelf;n++){Physics.Simulate(.005f);var p=t.InverseTransformPoint(ball.position);if((p-last).magnitude<.002f&&p.x<3.5f)stall+=.005f;else stall=0;maxStall=Mathf.Max(maxStall,stall);maxY=Mathf.Max(maxY,p.y);escape|=Mathf.Abs(p.x)>4.6f||p.z>18.1f||p.y>2.85f;last=p;}
sb.AppendLine("RAMP "+route.name+" speed="+speed+" completed="+completed+" fieldStall="+maxStall.ToString("F2")+" escape="+escape+" maxY="+maxY.ToString("F2")+" end="+last.ToString("F3"));}
finally{gate.Completed-=onDone;}
}}
}finally{Physics.simulationMode=mode;GameManager.Instance.StartGame();}
System.IO.File.WriteAllText("Tools/unity/out/refined-routes.txt",sb.ToString());return sb.ToString();
