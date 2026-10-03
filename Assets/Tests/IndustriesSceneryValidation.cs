#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VisualPinball.Unity;

/// <summary>Explicit integration check; never stored in a scene, excluded from builds.</summary>
public sealed class IndustriesSceneryValidation : MonoBehaviour
{
    private readonly List<string> results=new();
    public static void Run()
    {
        if(!UnityEditor.EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Industries")throw new System.InvalidOperationException("Industries en Play attendu.");
        var go=new GameObject("IndustriesSceneryValidation"){hideFlags=HideFlags.DontSave};go.AddComponent<IndustriesSceneryValidation>().StartCoroutine("Validate");
    }
    private IEnumerator Validate()
    {
        yield return new WaitForSecondsRealtime(2f);
        var game=GameManager.Instance;var manager=AudioManager.Instance;
        var music=Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(s=>s.clip!=null && s.clip.name=="IndustriesOrchestralLoop").ToArray();
        Check("transition cleaned up the Neutral music manager",manager!=null && manager.gameObject.scene.name=="Industries");
        Check("one playing stereo orchestral loop",music.Length==1 && music[0].isPlaying && music[0].loop && music[0].clip.channels==2);
        if(music.Length!=1){Finish();yield break;}
        var source=music[0];var clip=source.clip;
        Check("181 second streaming loop / authored mix group / mechanical headroom",Mathf.Abs(clip.length-181)<.02f && clip.loadType==AudioClipLoadType.Streaming && source.outputAudioMixerGroup!=null && Mathf.Abs(source.volume-.28f)<.001f);
        Check("exactly one enabled listener",Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.enabled)==1);
        var credit=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name=="MusicCredit");
        Check("attribution stays out of gameplay",!credit.enabled);
        var effects=Object.FindAnyObjectByType<IndustriesAudio>();
        effects.Play(IndustriesSound.Bumper);
        var voices=manager.GetComponents<AudioSource>().Where(s=>s!=source && s.isPlaying).ToArray();
        Check("native impacts use the shared Mechanical voice pool",voices.Length>0 && voices.All(s=>s.outputAudioMixerGroup!=null && s.outputAudioMixerGroup.name=="Mechanical") && !effects.GetComponent<AudioSource>().isPlaying);
        game.TogglePause();yield return new WaitForSecondsRealtime(.1f);int paused=source.timeSamples;
        yield return new WaitForSecondsRealtime(.4f);
        Check("pause suspends music without restarting the track",!source.isPlaying && Mathf.Abs(source.timeSamples-paused)<64 && Time.timeScale==0);
        Check("pause suspends pooled effects",voices.All(s=>!s.isPlaying));
        Check("attribution visible during pause",credit.enabled && credit.text.Contains("SCOTT BUCKLEY") && credit.text.Contains("CC BY 4.0"));
        game.TogglePause();yield return new WaitForSecondsRealtime(.2f);
        Check("resume continues the same track and hides credit",source.isPlaying && source.timeSamples>paused && !credit.enabled && Time.timeScale==1);
        float volume=source.volume;source.volume=0;yield return new WaitForSecondsRealtime(.1f);
        Check("mute preserves running playback",source.isPlaying && source.volume==0);source.volume=volume;
        var root=Object.FindAnyObjectByType<PlayfieldComponent>().transform.Find("MiniatureScenery");
        Check("decor has no PhysX or VPE collider component",root!=null && root.GetComponentsInChildren<UnityEngine.Collider>(true).Length==0 && !root.GetComponentsInChildren<Component>(true).Any(c=>c!=null && c.GetType().Name.Contains("ColliderComponent")));
        Check("six native reward targets preserved",Object.FindObjectsByType<TargetComponent>(FindObjectsSortMode.None).Length==6);
        var bumpers=Object.FindObjectsByType<BumperComponent>(FindObjectsSortMode.None);
        float min=float.PositiveInfinity;
        for(int i=0;i<bumpers.Length;i++)for(int j=i+1;j<bumpers.Length;j++){
            float radius=(bumpers[i].Radius+bumpers[j].Radius)*VisualPinball.Unity.Physics.ScaleInv;
            float gap=(bumpers[i].transform.localPosition-bumpers[j].transform.localPosition).magnitude-radius;min=Mathf.Min(min,gap);
        }
        Check("bumper passages wider than 1.3 ball diameters (local gap="+min+")",min>50*VisualPinball.Unity.Physics.ScaleInv*1.3f);
        Finish();
    }
    private void Check(string name,bool valid)=>results.Add((valid?"PASS ":"FAIL ")+name);
    private void Finish(){Directory.CreateDirectory("Docs/IndustriesSceneryValidation");File.WriteAllLines("Docs/IndustriesSceneryValidation/audio-runtime.txt",results);Debug.Log("[Scenery validation] "+string.Join(" | ",results));Destroy(gameObject);}
}
#endif
