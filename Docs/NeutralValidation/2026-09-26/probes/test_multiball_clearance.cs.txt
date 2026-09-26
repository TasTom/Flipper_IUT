if(!Application.isPlaying)throw new System.Exception("Play required");
var table=GameObject.Find("PinballTable").transform;var gm=GameManager.Instance;var mm=MultiballManager.Instance;var mode=Physics.simulationMode;var sb=new System.Text.StringBuilder();
void Check(string n,bool ok){sb.AppendLine((ok?"PASS ":"FAIL ")+n);}
try{Physics.simulationMode=SimulationMode.Script;gm.StartGame();mm.TriggerMultiball();Physics.SyncTransforms();
var balls=GameObject.FindGameObjectsWithTag("Ball").Select(g=>g.GetComponent<Rigidbody>()).ToArray();
Check("three tracked balls",balls.Length==3&&BallManager.Instance.LiveBallCount==3);
float nearest=float.MaxValue;for(int a=0;a<balls.Length;a++)for(int b=a+1;b<balls.Length;b++)nearest=Mathf.Min(nearest,Vector3.Distance(balls[a].position,balls[b].position));
Check("no ball overlap, minimum distance="+nearest.ToString("F3"),nearest>.45f);
bool allClear=true;foreach(var b in balls.Where(b=>b.name!="Ball"))foreach(var c in Physics.OverlapSphere(b.position,.225f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))if(c.attachedRigidbody!=b)allClear=false;
Check("release volumes clear of solid obstacles",allClear);
Physics.Simulate(.005f);Check("extra balls released toward flippers",balls.Where(b=>b.name!="Ball").All(b=>Vector3.Dot(b.linearVelocity,table.forward)<-1));
for(int n=0;n<80;n++)Physics.Simulate(.005f);
Check("released balls enter open playfield",balls.Where(b=>b.name!="Ball").All(b=>{var p=table.InverseTransformPoint(b.position);return p.z<11.5f&&p.z>1&&Mathf.Abs(p.x)<4&&p.y>0;}));
gm.StartGame();Check("restart removes extra balls",BallManager.Instance.LiveBallCount==1&&GameObject.FindGameObjectsWithTag("Ball").Length==1);
}finally{Physics.simulationMode=mode;gm.StartGame();}
System.IO.File.WriteAllText("Tools/unity/out/multiball-clearance.txt",sb.ToString());return sb.ToString();
