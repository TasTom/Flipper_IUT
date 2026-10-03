using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VisualPinball.Unity;

namespace VisualPinball.Engine.Unity.Urp
{
    /// <summary>
    /// Branche l'adaptateur URP sur VPE avant le chargement de la première scène.
    ///
    /// VPE résout son pipeline de rendu paresseusement, mais ses matériaux portables le sont plus
    /// tôt : <see cref="VpeMaterialResolver.Active"/> doit déjà répondre quand une table importée
    /// construit ses matériaux. L'enregistrement est donc fait ici, à <c>BeforeSceneLoad</c>, et
    /// non dans un <c>Awake</c> — à ce moment-là il serait trop tard.
    /// </summary>
    public static class UrpVpeBootstrap
    {
        private static bool _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAtRuntime() => EnsureRegistered();

        /// <summary>
        /// Enregistre l'adaptateur une seule fois. Idempotent, et appelable aussi bien au
        /// chargement d'une scène qu'à l'ouverture de l'éditeur : une table importée hors Play
        /// mode doit trouver le resolver, sinon ses matériaux resteraient ceux du glTF.
        /// </summary>
        public static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;

            VpeMaterialResolver.Register(new UrpMaterialResolver());
            VpeGraphics.Register(new UrpGraphicsApplier());

            // Le pipeline lui-même n'est pas instancié ici : VPE le découvre par réflexion au
            // premier accès à RenderPipeline.Current, et forcer la découverte maintenant
            // reviendrait à la résoudre avant que les assemblies soient toutes chargées.
        }
    }

    /// <summary>
    /// Applique les réglages de rendu de l'application sur URP.
    ///
    /// URP n'a pas la même surface de réglages que HDRP, et prétendre le contraire serait
    /// trompeur : l'anticrénelage et le bloom ont un équivalent direct, l'occlusion ambiante est
    /// un réglage du renderer (pas un volume), et les réflexions et l'éclairage global par
    /// lancer de rayons n'existent pas hors HDRP. Ces trois-là sont donc ignorés, sans erreur :
    /// l'appelant ne reçoit pas d'exception pour avoir demandé une fonction qu'URP n'offre pas.
    /// </summary>
    public class UrpGraphicsApplier : IVpeGraphicsApplier
    {
        public void Apply(VpeGraphicsSettings settings)
        {
            ApplyCameraAntialiasing(settings.AntiAliasing);
            ApplyBloom(settings.Bloom);
        }

        private static void ApplyCameraAntialiasing(int aa)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
            {
                return;
            }

            // URP n'a pas d'anticrénelage temporel : le mode TAA de VPE retombe sur la
            // reconstruction subpixel, qui est l'équivalent le plus proche qu'il propose.
            data.antialiasing = (VpeAntiAliasing)Mathf.Clamp(aa, 0, 3) switch
            {
                VpeAntiAliasing.None => AntialiasingMode.None,
                VpeAntiAliasing.Fxaa => AntialiasingMode.FastApproximateAntialiasing,
                _ => AntialiasingMode.SubpixelMorphologicalAntiAliasing
            };
        }

        private static void ApplyBloom(bool enabled)
        {
            var volume = FindGlobalVolume();
            var profile = volume != null ? volume.profile : null;
            if (profile == null)
            {
                return;
            }

            if (profile.TryGet<Bloom>(out var bloom))
            {
                bloom.active = enabled;
            }
        }

        /// <summary>Volume global actif de plus haute priorité, sans modifier l'asset partagé.</summary>
        private static Volume FindGlobalVolume()
        {
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Volume best = null;
            foreach (var volume in volumes)
            {
                if (!volume.isGlobal || !volume.enabled || !volume.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (best == null || volume.priority > best.priority)
                {
                    best = volume;
                }
            }

            return best;
        }
    }
}
