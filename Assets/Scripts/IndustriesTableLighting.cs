using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Unity;

public enum IndustriesLampRole { Ambient, Bumper, Target, Ramp, Delivery, Slingshot }

[Serializable]
public sealed class IndustriesLamp
{
    public IndustriesLampRole role;
    public string sourceName;
    public int index;
    public Color color=Color.white;
    public Renderer insert;
    public Renderer halo;
    [NonSerialized] public float flash;
}

/// <summary>Décor lumineux réactif aux contacts VPE. Aucun score ni collider ; GDD §FX.</summary>
[ExecuteAlways,DisallowMultipleComponent,DefaultExecutionOrder(1100)]
public sealed class IndustriesTableLighting : MonoBehaviour
{
    [SerializeField] private IndustriesLightingConfig config;
    [SerializeField] private IndustriesVpeGame engine;
    [SerializeField] private GameManager game;
    [SerializeField] private IndustriesLamp[] lamps=Array.Empty<IndustriesLamp>();
    [SerializeField] private UnityEngine.Light[] ambientLights=Array.Empty<UnityEngine.Light>();
    [SerializeField] private UnityEngine.Light[] templateSources=Array.Empty<UnityEngine.Light>();
    private readonly List<Action> unbind=new List<Action>();
    private MaterialPropertyBlock block;
    private Player player;
    private float clock, celebration;
    private bool bound;
    private static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
    public int NativeFeedbackCount { get; private set; }
    public int BoundContactCount=>unbind.Count;

    private void Awake()
    {
        block=new MaterialPropertyBlock();
        if(engine==null)engine=FindAnyObjectByType<IndustriesVpeGame>();
        if(game==null)game=FindAnyObjectByType<GameManager>();
        if(engine!=null)player=engine.GetComponent<Player>();
    }
    private void OnEnable()
    {
        if(!Application.isPlaying){LateUpdate();return;}
        if(config==null||engine==null||game==null||player==null)
        {Debug.LogWarning("[Industries FX] Config ou table manquante : éclairage réactif désactivé.",this);return;}
        engine.OnStarted+=OnTableStarted;
        if(engine.IsInitialized)BindContacts();
    }
    private void OnDisable()
    {
        if(engine!=null)engine.OnStarted-=OnTableStarted;
        foreach(var remove in unbind)remove();unbind.Clear();bound=false;
    }
    private void OnTableStarted(object sender,EventArgs args)=>BindContacts();
    private void BindContacts()
    {
        if(bound||player==null||player.TableApi==null)return;
        bound=true;var api=player.TableApi;
        foreach(var lamp in lamps)
        {
            if(lamp==null||string.IsNullOrEmpty(lamp.sourceName))continue;
            EventHandler<HitEventArgs> hit=(_,__)=>Pulse(lamp);
            switch(lamp.role)
            {
                case IndustriesLampRole.Bumper:
                    var bumper=api.Bumper(lamp.sourceName);
                    if(bumper!=null){bumper.Hit+=hit;unbind.Add(()=>bumper.Hit-=hit);}break;
                case IndustriesLampRole.Target:
                    var drop=api.DropTarget(lamp.sourceName);
                    if(drop!=null){drop.Hit+=hit;unbind.Add(()=>drop.Hit-=hit);}
                    else{var target=api.HitTarget(lamp.sourceName);if(target!=null){target.Hit+=hit;unbind.Add(()=>target.Hit-=hit);}}break;
                case IndustriesLampRole.Ramp:
                    var trigger=api.Trigger(lamp.sourceName);
                    if(trigger!=null){trigger.Hit+=hit;unbind.Add(()=>trigger.Hit-=hit);}break;
                case IndustriesLampRole.Delivery:
                    var kicker=api.Kicker(lamp.sourceName);
                    if(kicker!=null){kicker.Hit+=hit;unbind.Add(()=>kicker.Hit-=hit);}break;
                case IndustriesLampRole.Slingshot:
                    var sling=api.Surface(lamp.sourceName);
                    if(sling!=null){EventHandler contact=(_,__)=>Pulse(lamp);sling.Slingshot+=contact;unbind.Add(()=>sling.Slingshot-=contact);}break;
            }
        }
        if(unbind.Count==0)Debug.LogWarning("[Industries FX] Aucun contact VPE relié ; vérifier les noms de pièces.",this);
    }
    private void Pulse(IndustriesLamp lamp)
    {
        if(game==null||game.State!=GameManager.GameState.Playing)return;
        lamp.flash=1;NativeFeedbackCount++;
        if(lamp.role==IndustriesLampRole.Delivery)celebration=1;
    }
    private void LateUpdate()
    {
        // Les émissions des inserts VPE sont conservées. Leurs anciennes sources Unity
        // directionnelles sont remplacées par quatre lumières de zone sans ombres.
        foreach(var source in templateSources)if(source!=null&&source.enabled)source.enabled=false;
        if(config==null)return;
        if(block==null)block=new MaterialPropertyBlock();
        float dt=Application.isPlaying?Time.deltaTime:0;
        clock+=dt;celebration=Mathf.MoveTowards(celebration,0,dt/config.flashSeconds);
        bool attract=!Application.isPlaying||game==null||game.State==GameManager.GameState.Attract||game.State==GameManager.GameState.GameOver;
        int targets=engine!=null?engine.LitTargetMask:0;
        int loaded=engine!=null&&engine.Production!=null?engine.Production.Loaded:0;
        int processed=engine!=null&&engine.Production!=null?engine.Production.Processed:0;
        for(int i=0;i<lamps.Length;i++)
        {
            var lamp=lamps[i];if(lamp==null)continue;
            lamp.flash=Mathf.MoveTowards(lamp.flash,0,dt/config.flashSeconds);
            float gain=config.idleEmission*(1+config.breathingAmount*Mathf.Sin(clock*config.chaseSpeed+i*.5f));
            if(lamp.role==IndustriesLampRole.Target)
                gain=(targets&(1<<lamp.index))!=0?config.activeEmission:config.idleEmission*.28f;
            else if(lamp.role==IndustriesLampRole.Ramp)
                gain=loaded!=0?config.activeEmission*(.4f+.6f*Mathf.Max(0,Mathf.Cos(clock*config.chaseSpeed*3-lamp.index*1.7f))):config.idleEmission*.25f;
            else if(lamp.role==IndustriesLampRole.Delivery)
                gain=processed!=0?config.activeEmission:config.idleEmission*.4f;
            if(attract&&(lamp.role==IndustriesLampRole.Ramp||lamp.role==IndustriesLampRole.Target))
                gain=config.idleEmission*(.4f+.6f*Mathf.Max(0,Mathf.Cos(clock*config.chaseSpeed-lamp.index)));
            gain=Mathf.Lerp(gain,config.flashEmission,Mathf.Max(lamp.flash,celebration*.45f));
            SetColor(lamp.insert,lamp.color*gain);
            SetColor(lamp.halo,lamp.color*gain*config.haloGain);
        }
        foreach(var light in ambientLights)if(light!=null)light.intensity=config.lightIntensity*(1+celebration*.6f);
    }
    private void SetColor(Renderer renderer,Color color)
    {
        if(renderer==null)return;
        color.a=1;block.Clear();block.SetColor(BaseColor,color);renderer.SetPropertyBlock(block);
    }
#if UNITY_EDITOR
    public void RefreshPreview(){if(!Application.isPlaying)LateUpdate();}
#endif
}
