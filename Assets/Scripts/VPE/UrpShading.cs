using UnityEngine;
using UnityEngine.Rendering;

namespace VisualPinball.Engine.Unity.Urp
{
    /// <summary>Mode de surface d'un matériau URP Lit.</summary>
    public enum UrpSurface
    {
        Opaque,
        Cutout,
        Transparent,
        Additive,
    }

    /// <summary>
    /// Noms de propriétés et réglages de surface du shader « Universal Render Pipeline/Lit ».
    ///
    /// Ce fichier tient le rôle du <c>MaterialConverter</c> HDRP pour tout ce qui touche au
    /// shader, mais les propriétés n'ont pas les mêmes noms : URP écrit <c>_BaseMap</c> et
    /// <c>_BumpMap</c> là où HDRP écrit <c>_BaseColorMap</c> et <c>_NormalMap</c>, et URP n'a ni
    /// profil de diffusion ni identifiant de matériau. Couleur, métallique, lissage et émission
    /// se transposent, eux, tels quels.
    ///
    /// Un mot-clé compte plus qu'un nom : URP 17 n'affiche l'émission que si <c>_EMISSION</c> est
    /// actif sur le matériau. Or VPE allume ses lampes par <see cref="MaterialPropertyBlock"/>,
    /// ce qui ne touche pas aux mots-clés — d'où <see cref="EnableEmission"/> à la création,
    /// sans quoi aucune lampe ne s'allumerait.
    /// </summary>
    internal static class UrpShading
    {
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        public const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        public static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        public static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int Surface = Shader.PropertyToID("_Surface");
        public static readonly int Blend = Shader.PropertyToID("_Blend");
        public static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        public static readonly int Cutoff = Shader.PropertyToID("_Cutoff");
        public static readonly int AlphaToMask = Shader.PropertyToID("_AlphaToMask");
        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        public static readonly int Cull = Shader.PropertyToID("_Cull");
        public static readonly int MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");

        private static Shader _lit;
        private static Shader _unlit;

        /// <summary>Shader Lit d'URP, résolu une fois. Null si URP est absent du projet.</summary>
        public static Shader LitShader => _lit != null ? _lit : _lit = Shader.Find(LitShaderName);

        public static Shader UnlitShader => _unlit != null ? _unlit : _unlit = Shader.Find(UnlitShaderName);

        /// <summary>
        /// Crée un matériau Lit. Le shader est résolu par son nom : c'est le seul moyen d'obtenir
        /// un shader d'un package sans référencer ses assets, et URP est une dépendance déclarée
        /// de cet assembly, donc il est présent au build.
        /// </summary>
        public static Material NewLitMaterial(string name)
        {
            var shader = LitShader;
            if (shader == null)
            {
                Debug.LogError(
                    "[UrpShading] Shader « Universal Render Pipeline/Lit » introuvable. Vérifie que " +
                    "le package URP est installé et que ce shader est inclus au build (Always " +
                    "Included Shaders, ou un matériau d'assets qui le référence).");
                return null;
            }

            return new Material(shader) { name = name };
        }

        /// <summary>
        /// Applique un mode de surface complet : transparence, test alpha, mélange, écriture de
        /// profondeur et ordre de dessin. URP ne déduit rien de la couleur alpha — sans ces
        /// réglages, un matériau translucide reste opaque malgré un alpha à 0,5.
        /// </summary>
        public static void ApplySurface(Material material, UrpSurface surface, float alphaCutoff,
                                        bool doubleSided, int sortPriority = 0)
        {
            if (material == null)
            {
                return;
            }

            bool transparent = surface == UrpSurface.Transparent || surface == UrpSurface.Additive;

            material.SetFloat(Surface, transparent ? 1f : 0f);
            material.SetFloat(Blend, surface == UrpSurface.Additive ? 2f : 0f);
            material.SetFloat(AlphaClip, surface == UrpSurface.Cutout ? 1f : 0f);
            material.SetFloat(Cutoff, Mathf.Clamp01(alphaCutoff));
            material.SetFloat(AlphaToMask, surface == UrpSurface.Cutout ? 1f : 0f);

            if (surface == UrpSurface.Cutout)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt(ZWrite, 1);
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (transparent)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt(DstBlend, surface == UrpSurface.Additive
                    ? (int)UnityEngine.Rendering.BlendMode.One
                    : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt(ZWrite, 0);

                // Une surface transparente n'a pas de tri en profondeur : son ordre de dessin
                // décide de ce qu'on voit. Un insert doit passer après la vitre qui le recouvre.
                material.renderQueue = (int)RenderQueue.Transparent + Mathf.Clamp(sortPriority, -50, 50);
            }
            else
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt(ZWrite, 1);
                material.renderQueue = (int)RenderQueue.Geometry;
            }

            material.SetInt(Cull, doubleSided ? (int)CullMode.Off : (int)CullMode.Back);
        }

        /// <summary>
        /// Active l'émission. Sans ce mot-clé, URP 17 ignore <c>_EmissionColor</c> — y compris
        /// quand la couleur arrive par un <see cref="MaterialPropertyBlock"/>, ce qui est
        /// précisément la façon dont VPE allume ses lampes.
        /// </summary>
        public static void EnableEmission(Material material)
        {
            if (material != null && material.HasProperty(EmissionColor))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }
    }
}
