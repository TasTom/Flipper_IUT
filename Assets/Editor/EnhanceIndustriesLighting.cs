using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VisualPinball.Unity;
using Object=UnityEngine.Object;

/// <summary>GDD : direction artistique, indications de cibles/rampes et FX. Undo ; scène non enregistrée.</summary>
public static class EnhanceIndustriesLighting
{
    private const string Folder="Assets/Generated/Industries/Lighting";
    private const string Art="Assets/Art/Industries/Lighting/";
    private static readonly string[] Carters={"Carter_Wall30","Carter_Wall31","Carter_Wall33","Carter_Wall34"};

    [MenuItem("Flipper/Industries/Refaire les carters et ajouter les lumières arcade")]
    public static void Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.name!="Industries"||EditorApplication.isPlaying){Debug.LogWarning("[Industries] Ouvrir Industries hors Play.");return;}
        var engine=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<IndustriesVpeGame>(true)).FirstOrDefault();
        var pf=engine!=null?engine.GetComponentInChildren<PlayfieldComponent>()?.transform:null;
        var camera=Camera.main;
        if(pf==null||camera==null||UniversalRenderPipeline.asset==null){Debug.LogWarning("[Industries] Table, caméra ou URP manquant.");return;}
        if(pf.Find("IndustriesArcadeLighting")!=null){Debug.Log("[Industries] Lumières arcade déjà présentes ; réglages conservés.");return;}
        if(Carters.Any(n=>!pf.GetComponentsInChildren<Transform>(true).Any(t=>t.name==n)))
        {Debug.LogWarning("[Industries] Un des quatre carters manque.");return;}
        EnsureFolder(Folder);
        foreach(var name in Carters.Concat(new[]{"SoftHalo"}))
        {
            string path=Art+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null){Debug.LogWarning("[Industries] Texture manquante : "+path);return;}
            importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }
        var config=AssetDatabase.LoadAssetAtPath<IndustriesLightingConfig>(Folder+"/IndustriesLightingConfig.asset");
        if(config==null){config=ScriptableObject.CreateInstance<IndustriesLightingConfig>();AssetDatabase.CreateAsset(config,Folder+"/IndustriesLightingConfig.asset");}
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Industries : inscriptions et éclairage arcade");
        try
        {
            var root=New("IndustriesArcadeLighting",pf);
            ReprintCarters(pf);
            HideOldCaptions(pf);
            var core=GlowMaterial(false);var halo=GlowMaterial(true);
            var lamps=new List<IndustriesLamp>();
            var gi=pf.GetComponentsInChildren<LightComponent>(true).Where(l=>l.name.StartsWith("gi")).OrderBy(l=>l.name).ToArray();
            foreach(var source in gi)
            {
                var p=pf.InverseTransformPoint(source.transform.position);p.y=.0006f;
                lamps.Add(Lamp(root,"GI_"+source.name,p,.004f,.025f,IndustriesLampRole.Ambient,source.name,0,
                    p.x<.25f?config.amber:config.turquoise,core,halo));
            }
            foreach(var bumper in pf.GetComponentsInChildren<BumperComponent>())
            {
                var p=pf.InverseTransformPoint(bumper.transform.position);p.y=.0007f;
                lamps.Add(Lamp(root,"Halo_"+bumper.name,p,.034f,.048f,IndustriesLampRole.Bumper,bumper.name,0,
                    config.turquoise,core,halo));
            }
            var targetNames=new[]{"sw1","sw2","sw3","sw11","sw12","sw13"};
            for(int i=0;i<targetNames.Length;i++)
            {
                var source=pf.GetComponentsInChildren<LightComponent>(true).FirstOrDefault(l=>l.name=="l"+(i<3?i+1:i+8));
                if(source==null)continue;
                var p=pf.InverseTransformPoint(source.transform.position);p.y=.0012f;
                lamps.Add(Lamp(root,"Insert_"+targetNames[i],p,.0095f,.018f,IndustriesLampRole.Target,targetNames[i],i,
                    i<3?config.amber:config.turquoise,core,halo));
            }
            for(int i=0;i<3;i++)
            {
                var p=new Vector3(.305f+i*.020f,.0008f,-.630f+i*.029f);
                var lamp=Lamp(root,"RampGuide_"+i,p,.011f,.019f,IndustriesLampRole.Ramp,IndustriesProduction.TextileEntry,i,config.ivory,core,halo);
                lamp.insert.GetComponent<MeshFilter>().sharedMesh=Shape("Arrow");
                lamp.insert.transform.localRotation=Quaternion.Euler(0,35,0);lamps.Add(lamp);
            }
            var scoop=pf.GetComponentsInChildren<KickerComponent>().FirstOrDefault(k=>k.name=="Kicker1");
            if(scoop!=null){var p=pf.InverseTransformPoint(scoop.transform.position);p.y=.0008f;
                lamps.Add(Lamp(root,"DeliveryRing",p,.023f,.036f,IndustriesLampRole.Delivery,scoop.name,0,config.amber,core,halo));}
            var slings=pf.GetComponentsInChildren<SurfaceComponent>().Where(s=>s.name.Contains("SlingShot")).OrderBy(s=>s.name).ToArray();
            for(int i=0;i<slings.Length;i++)
            {
                var p=new Vector3(i==0?.149f:.325f,.0008f,-.846f);
                lamps.Add(Lamp(root,"SlingPulse_"+i,p,.006f,.024f,IndustriesLampRole.Slingshot,slings[i].name,i,config.amber,core,halo));
            }
            // LED le long des guides : meshes émissifs, sans nouvelles lumières temps réel.
            foreach(int side in new[]{0,1})for(int i=0;i<8;i++)
            {
                var p=new Vector3(side==0?.018f:.492f,.001f,-.20f-i*.091f);
                lamps.Add(Lamp(root,"RailLED_"+side+"_"+i,p,.0023f,.012f,IndustriesLampRole.Ambient,"",i,
                    side==0?config.amber:config.turquoise,core,halo));
            }
            var points=new[]{new Vector3(.258f,.065f,-.16f),new Vector3(.27f,.065f,-.36f),new Vector3(.245f,.055f,-.83f),new Vector3(.415f,.055f,-.56f)};
            var lights=new List<UnityEngine.Light>();
            for(int i=0;i<points.Length;i++)
            {
                var host=New("ZoneLight_"+i,root);host.localPosition=points[i];
                var light=Undo.AddComponent<UnityEngine.Light>(host.gameObject);light.type=LightType.Point;
                light.color=i==1||i==3?config.turquoise:config.ivory;
                light.intensity=config.lightIntensity;light.range=config.lightRange*pf.lossyScale.x;light.shadows=LightShadows.None;lights.Add(light);
            }
            var oldSources=pf.GetComponentsInChildren<LightComponent>(true).SelectMany(l=>l.LightSources).Distinct().ToArray();
            foreach(var source in oldSources){Undo.RecordObject(source,"Remplacer les sources du template par l'éclairage de zone");source.type=LightType.Point;source.enabled=false;Record(source);}
            foreach(var light in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.Light>(true)))
            {
                if(light.name!="WorkshopKey"&&light.name!="WorkshopFill")continue;
                Undo.RecordObject(light,"Ambiance de flipper");light.intensity=light.name=="WorkshopKey"?config.workshopKeyIntensity:config.workshopFillIntensity;Record(light);
            }
            var fx=Undo.AddComponent<IndustriesTableLighting>(root.gameObject);var so=new SerializedObject(fx);
            so.FindProperty("config").objectReferenceValue=config;so.FindProperty("engine").objectReferenceValue=engine;
            so.FindProperty("game").objectReferenceValue=Object.FindAnyObjectByType<GameManager>();
            var bindings=so.FindProperty("lamps");bindings.arraySize=lamps.Count;
            for(int i=0;i<lamps.Count;i++)
            {
                var dst=bindings.GetArrayElementAtIndex(i);var src=lamps[i];
                dst.FindPropertyRelative("role").enumValueIndex=(int)src.role;dst.FindPropertyRelative("sourceName").stringValue=src.sourceName;
                dst.FindPropertyRelative("index").intValue=src.index;dst.FindPropertyRelative("color").colorValue=src.color;
                dst.FindPropertyRelative("insert").objectReferenceValue=src.insert;dst.FindPropertyRelative("halo").objectReferenceValue=src.halo;
            }
            AssignArray(so.FindProperty("ambientLights"),lights.Cast<Object>().ToArray());AssignArray(so.FindProperty("templateSources"),oldSources.Cast<Object>().ToArray());
            so.ApplyModifiedProperties();Record(fx);
            fx.RefreshPreview();
            AddBloom(root,camera,config);
            EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);Selection.activeGameObject=root.gameObject;
            Debug.Log("[Industries] Quatre carters réimprimés, inserts et halos réactifs, quatre lumières locales et bloom. Ctrl+Z annule ; Ctrl+S conserve.");
        }
        catch(Exception e){Undo.RevertAllDownToGroup(group);Debug.LogError("[Industries] Éclairage annulé : "+e);}
    }
    private static void ReprintCarters(Transform pf)
    {
        foreach(var name in Carters)
        {
            var host=pf.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
            var filter=host.GetComponentsInChildren<MeshFilter>().First(f=>f.GetComponent<Renderer>().sharedMaterial.name=="WorkshopPanel");
            var path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                mesh=Object.Instantiate(filter.sharedMesh);mesh.name=name+"PrintUV";
                var vertices=mesh.vertices.Select(v=>pf.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
                float x0=vertices.Min(v=>v.x),x1=vertices.Max(v=>v.x),z0=vertices.Min(v=>v.z),z1=vertices.Max(v=>v.z);
                mesh.uv=vertices.Select(v=>new Vector2((v.x-x0)/(x1-x0),(v.z-z0)/(z1-z0))).ToArray();
                AssetDatabase.CreateAsset(mesh,path);
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name+"Enamel"};
                material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",.18f);material.SetFloat("_Smoothness",.38f);
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+name+".png"));AssetDatabase.CreateAsset(material,Folder+"/"+name+".mat");}
            var renderer=filter.GetComponent<MeshRenderer>();Undo.RecordObjects(new Object[]{filter,renderer},"Inscription lisible sur le carter");
            filter.sharedMesh=mesh;renderer.sharedMaterial=material;Record(filter);Record(renderer);
        }
    }
    private static void HideOldCaptions(Transform pf)
    {
        var presentation=pf.GetComponentInParent<IndustriesPresentation>();
        if(presentation==null)presentation=Object.FindAnyObjectByType<IndustriesPresentation>();
        if(presentation==null){Debug.LogWarning("[Industries] IndustriesPresentation absent : anciennes légendes conservées.");return;}
        Undo.RecordObject(presentation,"Légendes remplacées par les carters");
        var so=new SerializedObject(presentation);var hidden=so.FindProperty("hiddenRenderers");
        foreach(var name in new[]{"TimberPlate","TextilePlate"})
        {
            var caption=pf.Find("MiniatureScenery/"+name);if(caption==null)continue;
            foreach(var renderer in caption.GetComponentsInChildren<Renderer>(true))
            {
                bool found=false;
                for(int i=0;i<hidden.arraySize;i++)if(hidden.GetArrayElementAtIndex(i).objectReferenceValue==renderer)found=true;
                if(!found){hidden.InsertArrayElementAtIndex(hidden.arraySize);hidden.GetArrayElementAtIndex(hidden.arraySize-1).objectReferenceValue=renderer;}
                Undo.RecordObject(renderer,"Légende remplacée");renderer.enabled=false;Record(renderer);
            }
        }
        so.ApplyModifiedProperties();Record(presentation);
    }
    private static IndustriesLamp Lamp(Transform root,string name,Vector3 p,float radius,float haloRadius,IndustriesLampRole role,string source,int index,Color color,Material core,Material soft)
    {
        var host=New(name,root);host.localPosition=p;
        Renderer Visual(string suffix,Mesh mesh,float size,Material mat,float y)
        {
            var part=New(suffix,host);part.localPosition=Vector3.up*y;part.localScale=Vector3.one*size;
            Undo.AddComponent<MeshFilter>(part.gameObject).sharedMesh=mesh;
            var renderer=Undo.AddComponent<MeshRenderer>(part.gameObject);renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }
        return new IndustriesLamp{role=role,sourceName=source,index=index,color=color,
            insert=Visual("Lens",Shape("Ring"),radius,core,.0001f),halo=Visual("GroundGlow",Shape("Quad"),haloRadius,soft,0)};
    }
    private static Mesh Shape(string name)
    {
        var path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        if(name=="Ring")
        {
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI*2/64;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));vertices.Add(p);vertices.Add(p*.90f);
                int j=(i+1)%64;triangles.AddRange(new[]{2*i,2*i+1,2*j,2*j,2*i+1,2*j+1});
            }
        }
        else if(name=="Arrow")
        {
            vertices.AddRange(new[]{new Vector3(-.35f,0,-1),new Vector3(.35f,0,-1),new Vector3(.35f,0,0),new Vector3(.70f,0,0),new Vector3(0,0,1),new Vector3(-.70f,0,0),new Vector3(-.35f,0,0)});
            triangles.AddRange(new[]{0,2,1,0,6,2,3,5,4});
        }
        else{vertices.AddRange(new[]{new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(1,0,1),new Vector3(-1,0,1)});triangles.AddRange(new[]{0,2,1,0,3,2});}
        uv.AddRange(vertices.Select(v=>new Vector2(v.x,v.z)*.5f+Vector2.one*.5f));
        var mesh=new Mesh{name=name,vertices=vertices.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    private static Material GlowMaterial(bool halo)
    {
        string name=halo?"SoftGroundGlow":"InsertGlow";var path=Folder+"/"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
        var m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name};m.SetColor("_BaseColor",Color.white);m.SetFloat("_Cull",0);
        if(halo){m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"SoftHalo.png"));m.SetFloat("_Surface",1);m.SetFloat("_Blend",2);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",1);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;}
        m.SetShaderPassEnabled("ShadowCaster",false);AssetDatabase.CreateAsset(m,path);return m;
    }
    private static void AddBloom(Transform root,Camera camera,IndustriesLightingConfig config)
    {
        string path=Folder+"/IndustriesArcadeVolume.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(profile==null)
        {
            profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);
            var bloom=profile.Add<Bloom>(true);bloom.intensity.value=config.bloomIntensity;bloom.threshold.value=config.bloomThreshold;bloom.scatter.value=config.bloomScatter;
            var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.ACES;
            var vignette=profile.Add<Vignette>(true);vignette.intensity.value=config.vignetteIntensity;vignette.smoothness.value=.4f;
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }
        var host=New("ArcadeVolume",root);var volume=Undo.AddComponent<Volume>(host.gameObject);volume.isGlobal=true;volume.priority=5;volume.sharedProfile=profile;
        var data=camera.GetComponent<UniversalAdditionalCameraData>();if(data==null)data=Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
        Undo.RecordObject(data,"Post-traitement de la table");data.renderPostProcessing=true;data.volumeLayerMask|=1;Record(data);
    }
    private static Transform New(string name,Transform parent){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Lumières Industries");go.transform.SetParent(parent,false);return go.transform;}
    private static void AssignArray(SerializedProperty dst,Object[] values){dst.arraySize=values.Length;for(int i=0;i<values.Length;i++)dst.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
    private static void Record(Object obj)=>PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));}
}
