if(!Application.isPlaying)throw new System.Exception("Play required");
var table=GameObject.Find("PinballTable").transform;var ball=table.Find("Gameplay/Ball").GetComponent<Rigidbody>();var mode=Physics.simulationMode;var sb=new System.Text.StringBuilder();
try{Physics.simulationMode=SimulationMode.Script;
foreach(var target in table.GetComponentsInChildren<SubjectTarget>()){
 GameManager.Instance.StartGame();target.Activer();ball.useGravity=false;
 ball.position=table.TransformPoint(new Vector3(0,3,8));ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();Physics.Simulate(.005f);
 typeof(SubjectTarget).GetField("dernierHit",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(target,float.NegativeInfinity);
 ball.position=target.transform.TransformPoint(new Vector3(0,.27f,-3.5f));ball.linearVelocity=target.transform.forward*15;ball.angularVelocity=Vector3.zero;Physics.SyncTransforms();
 for(int i=0;i<100;i++)Physics.Simulate(.005f);
 sb.AppendLine((target.IsValidated?"PASS ":"FAIL ")+target.name+" accessible from centre");ball.useGravity=true;
}}finally{Physics.simulationMode=mode;ball.useGravity=true;GameManager.Instance.StartGame();}
System.IO.File.WriteAllText("Tools/unity/out/v2-target-access.txt",sb.ToString());return sb.ToString();
