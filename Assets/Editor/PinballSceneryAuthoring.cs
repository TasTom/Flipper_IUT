using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VisualPinball.Unity;
using Object = UnityEngine.Object;

/// <summary>GDD §Conception de la table : Vosges/campus, et identité d’Industries.
/// Adds only missing decorative children; explicit layout migration is separate, one-time and Undoable.
/// Never saves scenes. All meshes remain visual only, separate from native VPE and PhysX collisions.</summary>
public static class PinballSceneryAuthoring
{
    private const string Folder = "Assets/Generated/MiniatureScenery";
    private static readonly string[] Names={"EnamelPetrol","Brass","Steel","Ivory","Brick","Slate","GlassBlue","Wood","Forest","Snow","Thread","GlowAmber"};
    private static readonly Color[] Colors={new Color(.035f,.20f,.23f),new Color(.55f,.30f,.08f),new Color(.38f,.45f,.49f),new Color(.82f,.78f,.63f),new Color(.28f,.10f,.065f),new Color(.055f,.09f,.12f),new Color(.06f,.21f,.29f),new Color(.38f,.20f,.075f),new Color(.024f,.15f,.10f),new Color(.86f,.9f,.91f),new Color(.32f,.54f,.48f),new Color(1,.48f,.07f)};

    [MenuItem("Flipper/Décor/Ajouter les maquettes à la scène active")]
    public static void AddScenery()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || (scene.name!="Neutral" && scene.name!="Industries"))throw new InvalidOperationException("Neutral ou Industries en édition attendu.");
        EnsureFolder(Folder);Undo.IncrementCurrentGroup();var group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Maquettes de flipper");
        bool industry=scene.name=="Industries";
        var host=industry?Object.FindAnyObjectByType<PlayfieldComponent>()?.transform:GameObject.Find("PinballTable")?.transform;
        if(host==null)throw new InvalidOperationException("Racine de plateau absente.");
        var root=host.Find("MiniatureScenery");if(root==null)root=New("MiniatureScenery",host);
        // Table-sized coordinates; convert once through the scaled VPE root. Physical items are untouched.
        if(industry){
            Place(root,"FactoryFacade","WorkshopFacade",new Vector3(-.20f,.54f,17.40f),Vector3.one);
            Place(root,"EnergyBoiler","Boiler",new Vector3(-2.79f,.98f,15.25f),Vector3.one*.78f);
            Place(root,"TextileWorkshop","TextileLoom",new Vector3(3.08f,.62f,9.47f),Vector3.one*.85f);
            Place(root,"TimberWorkshop","TimberStack",new Vector3(-3.25f,.60f,9.45f),Vector3.one);
            Plaque(root,"TimberPlate","BOIS",new Vector3(-3.25f,.605f,9.98f),.65f,.19f);
            Plaque(root,"EnergyPlate","ÉNERGIE",new Vector3(-2.85f,.995f,14.77f),.68f,.19f);
            Plaque(root,"TextilePlate","TEXTILE",new Vector3(3.05f,.625f,8.98f),.73f,.19f);
            AddMusic();
        }else{
            Place(root,"CampusDeck","ExhibitionDeck",new Vector3(.90f,.72f,15.48f),Vector3.one*.46f);
            Place(root,"IutCampus","Campus",new Vector3(.90f,.755f,15.48f),Vector3.one*.52f);
            Place(root,"FirLeft","AlpineFir",new Vector3(.31f,.755f,15.51f),Vector3.one*.42f);
            Place(root,"FirRight","AlpineFir",new Vector3(1.49f,.755f,15.51f),Vector3.one*.48f);
            Plaque(root,"CampusPlate","CAMPUS IUT",new Vector3(.90f,.758f,15.19f),.90f,.16f);
        }
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Flipper/Industries/Agencement original des bumpers (une seule fois)")]
    public static void ApplyIndustriesLayout()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().name!="Industries")throw new InvalidOperationException("Industries en édition attendu.");
        var pf=Object.FindAnyObjectByType<PlayfieldComponent>().transform;
        if(pf.Find("OriginalWorkshopLayout")!=null){Debug.Log("[Décor] Agencement déjà appliqué ; ajustements manuels conservés.");return;}
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Agencement atelier");
        MoveBumper("Bumper1",new Vector3(.240f,0,-.371f),"b1l1","b1l2");
        MoveBumper("Bumper2",new Vector3(.335f,0,-.410f),"b2l2","B2L1");
        MoveBumper("Bumper3",new Vector3(.140f,0,-.410f),"b3l2","b3l1");
        New("OriginalWorkshopLayout",pf);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());Undo.CollapseUndoOperations(group);
    }
    private static void MoveBumper(string name,Vector3 position,params string[] lamps)
    {
        var b=Object.FindObjectsByType<BumperComponent>(FindObjectsSortMode.None).Single(x=>x.name==name);
        var nodes=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var followers=lamps.Select(n=>nodes.Single(t=>t.name==n)).Where(t=>!t.IsChildOf(b.transform)).ToArray();
        var old=b.transform.position;Undo.RecordObject(b.transform,"Déplacer bumper");b.transform.localPosition=position;
        var delta=b.transform.position-old;
        foreach(var lamp in followers){Undo.RecordObject(lamp,"Suivre bumper avec lampes");lamp.position+=delta;PrefabUtility.RecordPrefabInstancePropertyModifications(lamp);}
        PrefabUtility.RecordPrefabInstancePropertyModifications(b.transform);
    }
    private static void AddMusic()
    {
        var path="Assets/Audio/Industries/IndustriesOrchestralLoop.wav";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var imp=(AudioImporter)AssetImporter.GetAtPath(path);
        var settings=imp.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
        imp.defaultSampleSettings=settings;imp.forceToMono=false;imp.loadInBackground=true;imp.SaveAndReimport();
        var cfgPath=Folder+"/IndustriesAudioConfig.asset";var cfg=AssetDatabase.LoadAssetAtPath<AudioConfig>(cfgPath);
        if(cfg==null){var template=AssetDatabase.LoadAssetAtPath<AudioConfig>("Assets/Generated/NeutralVpeAudio/NeutralAudioConfig.asset");
            if(template==null)throw new InvalidOperationException("Configuration audio Neutral absente.");cfg=Object.Instantiate(template);
            cfg.musicVolume=.28f;cfg.musicFadeInSeconds=1.2f;AssetDatabase.CreateAsset(cfg,cfgPath);}
        var managers=GameObject.Find("Managers");var am=managers.GetComponent<AudioManager>();if(am==null)am=Undo.AddComponent<AudioManager>(managers);
        SetEmpty(am,"config",cfg);SetEmpty(am,"musicClip",AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        if(managers.GetComponent<PinballGameAudio>()==null)Undo.AddComponent<PinballGameAudio>(managers);
        var audio=Object.FindAnyObjectByType<IndustriesAudio>();if(audio!=null){SetEmpty(audio,"mixerGroup",cfg.mechanicalGroup);SetEmpty(audio,"audioManager",am);}
        // Attribution ships with the game and is visible when paused, beside existing pause instructions.
        var credit=managers.GetComponent<MusicCreditDisplay>();if(credit==null)credit=Undo.AddComponent<MusicCreditDisplay>(managers);
        var root=Object.FindAnyObjectByType<PlayfieldComponent>().transform.Find("MiniatureScenery");
        bool needsCredit=root.Find("MusicCredit")==null;
        Plaque(root,"MusicCredit","MACHINERY OF THE STARS — SCOTT BUCKLEY\nCC BY 4.0 • www.scottbuckley.com.au",new Vector3(-.30f,.57f,2.75f),5.2f,.48f);
        var label=root.Find("MusicCredit").GetComponent<TMPro.TMP_Text>();if(needsCredit){label.fontSize=.85f;label.enabled=false;}
        SetEmpty(credit,"label",label);
    }
    private static void Place(Transform parent,string name,string model,Vector3 position,Vector3 scale)
    {
        if(parent.Find(name)!=null)return;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/MiniatureScenery/"+model+".fbx");
        if(prefab==null)throw new InvalidOperationException("Maquette absente : "+model);
        var go=Object.Instantiate(prefab);go.name=name;Undo.RegisterCreatedObjectUndo(go,"Maquette de table");go.transform.SetParent(parent,false);
        var table=parent.GetComponentInParent<TableComponent>();
        go.transform.position=table!=null?position:parent.TransformPoint(position);
        go.transform.rotation=(table!=null?Quaternion.identity:parent.rotation)*prefab.transform.rotation;
        // Preserve imported FBX scale, compensate only for the table hierarchy's scale.
        go.transform.localScale=Vector3.Scale(go.transform.localScale,scale)/(table!=null?table.transform.lossyScale.x:1f);
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>()){
            // Material slots are preserved by FBX even when automatic extraction is disabled.
            r.sharedMaterials=r.sharedMaterials.Select(m=>Material(m!=null?m.name:"Steel")).ToArray();
        }
    }
    private static void Plaque(Transform parent,string name,string text,Vector3 position,float width,float height)
    {
        if(parent.Find(name)!=null)return;var t=New(name,parent);var table=parent.GetComponentInParent<TableComponent>();
        t.position=table!=null?position:parent.TransformPoint(position);t.rotation=(table!=null?Quaternion.identity:parent.rotation)*Quaternion.Euler(90,0,0);
        t.localScale=Vector3.one/(table!=null?table.transform.lossyScale.x:1);
        var label=Undo.AddComponent<TMPro.TextMeshPro>(t.gameObject);label.font=TMPro.TMP_Settings.defaultFontAsset;label.text=text;label.fontSize=1.8f;label.color=new Color(.93f,.76f,.40f);label.alignment=TMPro.TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(width,height);label.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
    }
    private static Material Material(string name)
    {
        int i=Array.IndexOf(Names,name);if(i<0)i=2;name=Names[i];var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
        m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",Colors[i]);m.SetFloat("_Metallic",i==1||i==2?1:0);m.SetFloat("_Smoothness",i==6?.80f:i==1||i==2?.65f:.40f);AssetDatabase.CreateAsset(m,path);return m;
    }
    private static void SetEmpty(Object target,string name,Object value){var so=new SerializedObject(target);var p=so.FindProperty(name);if(p.objectReferenceValue==null){p.objectReferenceValue=value;so.ApplyModifiedProperties();}}
    private static Transform New(string name,Transform parent){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Décor de table");go.transform.SetParent(parent,false);return go.transform;}
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));}
}
