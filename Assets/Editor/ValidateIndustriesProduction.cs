using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity;

/// <summary>Vérification explicite du layout et des règles ; ne modifie ni partie ni scène.</summary>
public static class ValidateIndustriesProduction
{
    [MenuItem("Flipper/Validation/Tester les règles de production Industries")]
    public static string Run()
    {
        var results=new List<string>();
        void Check(string label,bool ok) { results.Add((ok?"PASS ":"FAIL ")+label); }
        var source=AssetDatabase.LoadAssetAtPath<IndustriesConfig>("Assets/Generated/Industries/IndustriesConfig.asset");
        if(source==null)throw new InvalidOperationException("IndustriesConfig manque.");
        var config=UnityEngine.Object.Instantiate(source);
        try
        {
            var r=new IndustriesProduction(config);
            r.Enter(0,1,0);Check("rampe sans matière refusée",!r.Exit(0,1,1));
            Check("index cible hors banque refusé",!r.HitTarget(-1)&&!r.HitTarget(6)&&r.Targets==0);
            r.HitTarget(0);r.HitTarget(0);Check("répétition cible sans chargement prématuré",r.Targets==1&&r.Loaded==0);
            r.HitTarget(1);Check("troisième cible charge le bois",r.HitTarget(2)&&r.Loaded==1);
            Check("même banque chargée une seule fois",!r.HitTarget(2)&&r.Loaded==1);
            Check("sortie seule ou ordre inverse refusé",!r.Exit(0,1,1));
            r.Enter(0,1,2);Check("autre bille refusée",!r.Exit(0,2,3));
            Check("la bille ayant pris l'entrée termine",r.Exit(0,1,3)&&r.Processed==1);
            r.Enter(0,1,4);Check("traitement répété sans bonus",!r.Exit(0,1,5));
            r.HitTarget(3);r.HitTarget(4);Check("livraison consomme seulement produit terminé",r.Deliver()==config.deliveryBonusPerProduct&&r.Loaded==0&&r.Targets==24);
            Check("scoop vide sans bonus de livraison",r.Deliver()==0);
            Check("textile conserve sa progression partielle",r.HitTarget(5)&&r.Loaded==2);
            r.Enter(1,3,10);Check("parcours expiré refusé",!r.Exit(1,3,11+config.productionRouteSeconds));
            r.Enter(1,3,20);r.ForgetBall(3);Check("bille drainée ne termine pas le parcours",!r.Exit(1,3,21));
            r.Enter(1,4,30);Check("temps de pause exclu (horloge de jeu)",r.Exit(1,4,30.5f));
            r.HitTarget(0);r.HitTarget(1);r.HitTarget(2);r.Enter(0,4,31);r.Exit(0,4,32);
            Check("deux matières préparées indépendamment",r.Loaded==3&&r.Processed==3);
            Check("double livraison et bonus",r.Deliver()==2*config.deliveryBonusPerProduct+config.combinedDeliveryBonus&&r.Targets==0&&r.Loaded==0&&r.Processed==0);
            r.HitTarget(0);r.Enter(0,5,40);r.Reset();Check("nouvelle bille réinitialise la production",r.Targets==0&&r.Loaded==0&&r.Processed==0&&!r.Exit(0,5,41));
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
        var table=UnityEngine.Object.FindAnyObjectByType<IndustriesVpeGame>();
        var pf=table?.GetComponentInChildren<PlayfieldComponent>()?.transform;
        Check("Industries : production activée",source.productionEnabled&&pf!=null&&pf.Find("ProductionLayout")!=null);
        if(pf!=null)
        {
            var ramps=pf.GetComponentsInChildren<RampComponent>();
            var ramp=ramps.FirstOrDefault(c=>c.name==(source.sharedProductionRamp?"Ramp1":"ProductionWoodRamp"));
            Check("rampe de transformation : collider VPE actif",ramp!=null&&ramp.GetComponent<RampColliderComponent>().enabled&&ramp.DragPoints.Length>=6
                && (!source.sharedProductionRamp || !ramps.Any(c=>c.name=="ProductionWoodRamp")));
            var names=source.sharedProductionRamp ? new[]{IndustriesProduction.TextileEntry,IndustriesProduction.TextileExit}
                : new[]{IndustriesProduction.WoodEntry,IndustriesProduction.WoodExit,IndustriesProduction.TextileEntry,IndustriesProduction.TextileExit};
            var sensors=pf.GetComponentsInChildren<TriggerComponent>();
            Check("capteurs VPE uniques et actifs",names.All(n=>sensors.Count(t=>t.name==n&&t.GetComponent<TriggerColliderComponent>().enabled)==1)
                && (!source.sharedProductionRamp || !sensors.Any(t=>t.name==IndustriesProduction.WoodEntry||t.name==IndustriesProduction.WoodExit)));
            var wood=pf.GetComponentsInChildren<DropTargetComponent>().Where(t=>t.name=="sw1"||t.name=="sw2"||t.name=="sw3").ToArray();
            Check("cibles bois dans l'aire centrale",wood.Length==3&&wood.All(t=>{var p=pf.InverseTransformPoint(t.transform.position);return p.x>.24f&&p.x<.32f&&p.z<-.50f&&p.z>-.54f;}));
            var scoop=pf.GetComponentsInChildren<KickerComponent>().First(k=>k.name=="Kicker1");
            Check("scoop de livraison à gauche",Mathf.Abs(pf.InverseTransformPoint(scoop.transform.position).x-.119f)<.0001f);
            Check("plateau manuel conserve l'ouverture",!pf.GetComponent<VisualPinball.Unity.Playfield.PlayfieldMeshComponent>().AutoGenerate);
            var roof=pf.GetComponentsInChildren<PrimitiveComponent>().FirstOrDefault(p=>p.name==RefineIndustriesRamp.RoofName);
            if(roof!=null && ramp!=null)
            {
                float k=VisualPinball.Unity.Physics.ScaleInv;
                Vector3 Point(int i){var p=ramp.DragPoints[i].Center;return pf.InverseTransformPoint(ramp.transform.TransformPoint(new Vector3(p.X*k,p.Z*k,-p.Y*k)));}
                var posts=pf.GetComponentsInChildren<PrimitiveComponent>();
                var a=pf.InverseTransformPoint(posts.First(p=>p.name=="Primitive28").transform.position);
                var b=pf.InverseTransformPoint(posts.First(p=>p.name=="Primitive29").transform.position);
                Check("Ramp1 : entrée centrée entre Primitive28/29",Vector3.Distance(Point(0),(a+b)*.5f)<.0001f);
                var normal=Point(1)-Point(0);normal.y=0;normal.Normalize();var across=a-b;across.y=0;across.Normalize();
                Check("Ramp1 : entrée perpendiculaire au portail",Mathf.Abs(Vector3.Dot(normal,across))<.001f);
                var floor=ramp.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Floor");
                Check("Ramp1 : sol abaissé sous 47 mm",floor.sharedMesh.vertices.Max(v=>pf.InverseTransformPoint(floor.transform.TransformPoint(v)).y)<.047f);
                if(pf.Find("ProductionLayout/Ramp1EntryStraightened")!=null)
                {
                    var start=Point(1);var span=Point(6)-start;var vertices=floor.sharedMesh.vertices;
                    float deviation=0;int samples=0;
                    for(int i=0;i<vertices.Length;i+=2)
                    {
                        var centre=pf.InverseTransformPoint(floor.transform.TransformPoint((vertices[i]+vertices[i+1])*.5f));
                        float t=Vector3.Dot(centre-start,span)/span.sqrMagnitude;
                        if(t<=.001f||t>=.999f)continue;
                        deviation=Mathf.Max(deviation,Vector3.Distance(centre,start+span*t));samples++;
                    }
                    Check("Ramp1 : segment 2–6 droit dans le maillage (écart="+(deviation*1000).ToString("F3")+" mm)",samples>=4&&deviation<.0002f);
                }
                var collider=ramp.GetComponent<RampColliderComponent>();
                Check("Ramp1 : parois visuelles et physiques de 20 mm",Mathf.Abs(collider.LeftWallHeight*k-.020f)<.0001f
                    && Mathf.Abs(collider.RightWallHeight*k-.020f)<.0001f && Mathf.Abs(ramp._leftWallHeightVisible*k-.020f)<.0001f
                    && Mathf.Abs(ramp._rightWallHeightVisible*k-.020f)<.0001f);
                Check("Ramp1 : plafond physique natif VPE",roof.GetComponent<PrimitiveColliderComponent>()?.enabled==true&&roof.GetUnityMesh()!=null);
                var cover=pf.Find("ProductionLayout/Ramp1Cover");
                Check("Ramp1 : plafond indépendant du pivot recalculé par VPE",cover!=null&&roof.transform.parent==cover&&roof.GetComponentInParent<RampComponent>()==null);
                var floorVertices=floor.sharedMesh.vertices;
                var pairs=Enumerable.Range(0,floorVertices.Length/2).Select(i=>new[]{
                    pf.InverseTransformPoint(floor.transform.TransformPoint(floorVertices[2*i])),
                    pf.InverseTransformPoint(floor.transform.TransformPoint(floorVertices[2*i+1]))}).ToArray();
                var edges=cover!=null?cover.Find("EdgeRails"):null;
                var edgeMesh=edges!=null?edges.GetComponent<MeshFilter>().sharedMesh:null;
                float edgeError=float.MaxValue;
                if(edgeMesh!=null&&edgeMesh.vertexCount%12==0&&pairs.Length>1)
                {
                    edgeError=0;var vertices=edgeMesh.vertices;int edgeRows=vertices.Length/12;
                    for(int side=0;side<2;side++)for(int row=0;row<edgeRows;row++)
                    {
                        var centre=Vector3.zero;
                        for(int j=0;j<6;j++)centre+=pf.InverseTransformPoint(edges.TransformPoint(vertices[(side*edgeRows+row)*6+j]))/6;
                        centre-=Vector3.up*RefineIndustriesRamp.WallHeight;
                        // VPE peut rééchantillonner la même courbe : comparer le tracé, pas les indices de sommets.
                        float nearest=float.MaxValue;
                        for(int i=1;i<pairs.Length;i++)
                        {
                            var origin=pairs[i-1][side];var span=pairs[i][side]-origin;
                            float t=span.sqrMagnitude>1e-15f?Mathf.Clamp01(Vector3.Dot(centre-origin,span)/span.sqrMagnitude):0;
                            nearest=Mathf.Min(nearest,Vector3.Distance(centre,origin+span*t));
                        }
                        edgeError=Mathf.Max(edgeError,nearest);
                    }
                }
                Check("Ramp1 : bordures centrées sur les parois (écart="+(edgeError*1000).ToString("F3")+" mm)",edgeError<.0001f);
                var covered=pairs.Where(v=>(v[0].y+v[1].y)*.5f>=.004f&&(v[0].x+v[1].x)*.5f>.258f).ToArray();
                var roofMesh=roof.GetComponent<MeshFilter>().sharedMesh;var roofVertices=roofMesh.vertices;
                int roofRows=roofMesh.triangles.Min()/4;float roofPlanError=float.MaxValue,clearance=float.MaxValue;
                if(roofRows==covered.Length)
                {
                    roofPlanError=0;
                    for(int row=0;row<roofRows;row++)for(int side=0;side<2;side++)
                    {
                        var actual=pf.InverseTransformPoint(roof.transform.TransformPoint(roofVertices[row*4+side]));
                        var offset=actual-covered[row][side];clearance=Mathf.Min(clearance,offset.y);offset.y=0;
                        roofPlanError=Mathf.Max(roofPlanError,Mathf.Abs(offset.magnitude-.001f));
                    }
                }
                Check("Ramp1 : plafond aligné et passage de 34 mm (écart="+(roofPlanError*1000).ToString("F3")+" mm)",roofPlanError<.0001f&&clearance>=.0339f);
                var returnRamp=ramps.First(r=>r.name=="Ramp3");var p=returnRamp.DragPoints[0].Center;
                var joint=pf.InverseTransformPoint(returnRamp.transform.TransformPoint(new Vector3(p.X*k,returnRamp._heightBottom*k,-p.Y*k)));
                Check("Ramp1 : raccord au retour sans marche",Vector3.Distance(Point(ramp.DragPoints.Length-1),joint)<.003f
                    && Mathf.Abs(Point(ramp.DragPoints.Length-1).y-joint.y)<.0001f);
            }
        }
        string report=pf!=null&&pf.GetComponentsInChildren<PrimitiveComponent>().Any(p=>p.name==RefineIndustriesRamp.RoofName)
            ?"Docs/IndustriesCoveredRamp":source.sharedProductionRamp?"Docs/IndustriesRamp1":"Docs/IndustriesLayout";
        Directory.CreateDirectory(report);File.WriteAllLines(report+"/editor.txt",results);
        string summary=results.Count(x=>x.StartsWith("PASS"))+" PASS, "+results.Count(x=>x.StartsWith("FAIL"))+" FAIL";
        Debug.Log("[Industries production] "+summary);return summary;
    }
}
