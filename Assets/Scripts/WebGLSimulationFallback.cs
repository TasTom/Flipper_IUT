using UnityEngine;
using VisualPinball.Unity;
using VisualPinball.Unity.Simulation;

/// <summary>
/// Sur WebGL, la table Industries demande le thread de simulation VPE, qui exige la
/// bibliothèque native <c>VisualPinball.NativeInput</c> — absente de la plateforme. Son
/// démarrage échoue après avoir coupé le pas de simulation Unity
/// (<c>SetExternalTiming(true)</c>) : la bille et les flippers restent figés.
///
/// <para>On force donc le mode « simulation sur le thread principal », celui que la table
/// Vosges Mania utilise déjà et qui fonctionne dans le navigateur. Le composant est désactivé
/// avant son <c>Start</c> pour qu'il ne coupe pas le timing, et le timing est remis à zéro au
/// cas où un démarrage partiel aurait déjà eu lieu.</para>
/// </summary>
public static class WebGLSimulationFallback
{
    public static void ApplyNow()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        foreach (var component in Object.FindObjectsByType<SimulationThreadComponent>(FindObjectsInactive.Include))
        {
            component.EnableSimulationThread = false;
            component.enabled = false;

            var physics = component.GetComponent<PhysicsEngine>();
            if (physics != null) physics.SetExternalTiming(false);

            Debug.Log("[WebGL] Thread de simulation VPE désactivé : physique sur le thread principal.");
        }
#endif
    }
}
