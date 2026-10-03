using System.Text;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity;
using Material = UnityEngine.Material;

namespace VisualPinball.Engine.Unity.Urp
{
    /// <summary>
    /// Convertit un matériau Visual Pinball en matériau URP Lit.
    ///
    /// Reprend la marche de l'adaptateur HDRP — même manipulation des couleurs très claires,
    /// même règle sur le métal translucide, même fusion des matériaux texturés — en remplaçant
    /// ce qui n'existe qu'en HDRP :
    ///
    /// <list type="bullet">
    /// <item>les cartes s'appellent <c>_BaseMap</c> et <c>_BumpMap</c>, pas <c>_BaseColorMap</c>
    /// ni <c>_NormalMap</c> ;</item>
    /// <item>il n'y a pas de profil de diffusion ni d'identifiant de matériau, donc
    /// <see cref="SetDiffusionProfile"/> et <see cref="SetMaterialType"/> n'ont rien à écrire —
    /// seul le cas translucide change quelque chose, et il passe par la surface ;</item>
    /// <item>l'émission n'a qu'une couleur (<c>_EmissionColor</c>), là où HDRP sépare une base
    /// LDR d'une couleur HDR : c'est ce qui explique la forme de
    /// <see cref="GetEmissiveIntensity"/>.</item>
    /// </list>
    /// </summary>
    public class UrpMaterialConverter : IMaterialConverter
    {
        // Le core fournit les matériaux d'affichage en variante « SRP » : ils sont faits pour un
        // pipeline scriptable, donc pour URP, et évitent d'en inventer un.
        private const string DotMatrixResourcePath = "Materials/Dot Matrix Display (SRP)";
        private const string SegmentResourcePath = "Materials/Segment Display (SRP)";

        private Material _dotMatrix;
        private Material _segment;

        /// <summary>
        /// Matériau du point matrix display. Créé à la demande si le projet n'en fournit pas :
        /// les composants d'affichage font <c>Instantiate</c> dessus sans vérifier, donc rendre
        /// null lèverait une exception au lieu de dégrader l'affichage.
        /// </summary>
        public Material DotMatrixDisplay => _dotMatrix != null
            ? _dotMatrix
            : _dotMatrix = UnityEngine.Resources.Load<Material>(DotMatrixResourcePath) ?? CreateDisplayMaterial("VPE Dot Matrix Display", new Color(0f, 0f, 0f, 0f));

        public Material SegmentDisplay => _segment != null
            ? _segment
            : _segment = UnityEngine.Resources.Load<Material>(SegmentResourcePath) ?? CreateDisplayMaterial("VPE Segment Display", new Color(0f, 0f, 0f, 0f));

        public int NormalMapProperty => UrpShading.BumpMap;

        public Material CreateMaterial(PbrMaterial vpxMaterial, ITextureProvider textureProvider, StringBuilder debug = null)
        {
            var unityMaterial = UrpShading.NewLitMaterial(vpxMaterial.Id);
            if (unityMaterial == null)
            {
                return null;
            }

            // Une couleur presque blanche est ramenée à 0,8 : c'est de la marge pour l'éclairage,
            // sans quoi les teintes claires saturent dès qu'une lumière les touche.
            var col = vpxMaterial.Color.ToUnityColor();
            if (vpxMaterial.Color.IsGray() && col.grayscale > 0.8f)
            {
                debug?.AppendLine("Color manipulation performed, brightness reduced.");
                col.r = col.g = col.b = 0.8f;
            }

            bool translucent = vpxMaterial.MapBlendMode == BlendMode.Translucent;
            if (translucent)
            {
                col.a = Mathf.Clamp01(vpxMaterial.Opacity);
            }

            unityMaterial.SetColor(UrpShading.BaseColor, col);

            // Un auteur VPX peut déclarer un matériau métallique *et* translucide. Les deux
            // ensemble ne rendent pas correctement : le métal ne l'emporte que si l'opacité
            // est pleine.
            float metallicValue = 0f;
            if (vpxMaterial.IsMetal && (!vpxMaterial.IsOpacityActive || vpxMaterial.Opacity >= 1f))
            {
                metallicValue = 1f;
                debug?.AppendLine("Metallic set to 1.");
            }

            unityMaterial.SetFloat(UrpShading.Metallic, metallicValue);
            SetSmoothness(unityMaterial, vpxMaterial.Roughness);

            if (vpxMaterial.HasMap && textureProvider != null)
            {
                unityMaterial.SetTexture(UrpShading.BaseMap, textureProvider.GetTexture(vpxMaterial.Map.Name));
            }

            if (vpxMaterial.HasNormalMap && textureProvider != null)
            {
                unityMaterial.EnableKeyword("_NORMALMAP");
                unityMaterial.SetTexture(UrpShading.BumpMap, textureProvider.GetTexture(vpxMaterial.NormalMap.Name));
            }

            SetDiffusionProfile(unityMaterial, vpxMaterial.DiffusionProfile);
            SetMaterialType(unityMaterial, vpxMaterial.MaterialType);

            // Après SetMaterialType : il peut avoir fait basculer un matériau en translucide.
            UrpShading.ApplySurface(unityMaterial, SurfaceOf(vpxMaterial.MapBlendMode), 0.5f, doubleSided: false);
            UrpShading.EnableEmission(unityMaterial);

            return unityMaterial;
        }

        /// <summary>
        /// Reprend la texture du matériau importé et n'écrase que ce qui vient du VPX.
        ///
        /// Sert aux pièces dont la texture est cuite dans le prefab — un chapeau de bumper, par
        /// exemple — et dont seules la couleur et la rugosité doivent suivre la table.
        /// </summary>
        public Material MergeMaterials(PbrMaterial vpxMaterial, Material texturedMaterial)
        {
            if (texturedMaterial == null)
            {
                return CreateMaterial(vpxMaterial, null);
            }

            var shader = texturedMaterial.shader != null && texturedMaterial.shader.name == UrpShading.LitShaderName
                ? texturedMaterial.shader
                : UrpShading.LitShader;

            if (shader == null)
            {
                return null;
            }

            var nonTextured = CreateMaterial(vpxMaterial, null);
            var merged = new Material(shader);
            merged.CopyPropertiesFromMaterial(texturedMaterial);

            if (nonTextured != null)
            {
                merged.name = nonTextured.name;
                merged.SetColor(UrpShading.BaseColor, nonTextured.GetColor(UrpShading.BaseColor));
                merged.SetFloat(UrpShading.Metallic, nonTextured.GetFloat(UrpShading.Metallic));
                merged.SetFloat(UrpShading.Smoothness, nonTextured.GetFloat(UrpShading.Smoothness));
                UrpShading.ApplySurface(merged, SurfaceOf(vpxMaterial.MapBlendMode), 0.5f, doubleSided: false);
                Object.DestroyImmediate(nonTextured);
            }

            UrpShading.EnableEmission(merged);

            return merged;
        }

        public void SetSmoothness(Material unityMaterial, float smoothness)
        {
            unityMaterial.SetFloat(UrpShading.Smoothness, smoothness);
        }

        /// <summary>
        /// Sans effet : le profil de diffusion est une notion HDRP. URP n'a pas d'équivalent, et
        /// les matériaux traversants y sont approchés autrement (voir la surface translucide).
        /// </summary>
        public void SetDiffusionProfile(Material material, DiffusionProfileTemplate template)
        {
        }

        /// <summary>
        /// Sans effet, sauf pour <see cref="MaterialType.Translucent"/> : URP n'a pas
        /// d'identifiant de matériau, mais un matériau déclaré translucide doit au moins basculer
        /// en surface transparente, sinon son opacité est ignorée.
        /// </summary>
        public void SetMaterialType(Material material, MaterialType materialType)
        {
            if (material != null && materialType == MaterialType.Translucent)
            {
                material.SetFloat(UrpShading.Surface, 1f);
                material.SetFloat(UrpShading.SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat(UrpShading.DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat(UrpShading.ZWrite, 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        public void SetEmissiveColor(MaterialPropertyBlock propBlock, Color color)
        {
            // URP n'a qu'une couleur d'émission, et elle est déjà HDR : la valeur reçue est
            // appliquée telle quelle, sans le décalage gamma que le doublon HDRP impose.
            propBlock.SetColor(UrpShading.EmissionColor, color);
        }

        public Color? GetEmissiveColor(Material material)
        {
            return material != null && material.HasProperty(UrpShading.EmissionColor)
                ? material.GetColor(UrpShading.EmissionColor)
                : (Color?)null;
        }

        /// <summary>
        /// Applique une intensité d'émission sur le bloc de propriétés.
        ///
        /// L'appelant passe <c>valeur de lampe × intensité mémorisée</c>, et cette intensité
        /// mémorisée vient de <see cref="GetEmissiveIntensity"/>, donc de la magnitude mêmes de
        /// la couleur d'émission. On la divise pour retrouver le facteur : sans cela le produit
        /// appliquerait la magnitude deux fois et la lampe s'emballerait.
        /// </summary>
        public void SetEmissiveIntensity(Material material, MaterialPropertyBlock propBlock, float intensity)
        {
            if (material == null || propBlock == null || !material.HasProperty(UrpShading.EmissionColor))
            {
                return;
            }

            var authored = material.GetColor(UrpShading.EmissionColor);
            float magnitude = Mathf.Max(authored.r, Mathf.Max(authored.g, Mathf.Max(authored.b, authored.a)));
            propBlock.SetColor(UrpShading.EmissionColor,
                magnitude > 0f ? authored * (intensity / magnitude) : authored * intensity);
        }

        /// <summary>
        /// Magnitude de la couleur d'émission.
        ///
        /// VPE s'en sert surtout comme test : un matériau dont l'émission vaut 0 n'est pas piloté
        /// par une lampe, un matériau qui émet l'est. Rendre 0 pour une émission noire est donc
        /// le comportement attendu, pas un cas dégénéré.
        /// </summary>
        public float GetEmissiveIntensity(Material material)
        {
            if (material == null || !material.HasProperty(UrpShading.EmissionColor))
            {
                return 0f;
            }

            var emissive = material.GetColor(UrpShading.EmissionColor);
            return Mathf.Max(emissive.r, Mathf.Max(emissive.g, Mathf.Max(emissive.b, emissive.a)));
        }

        private static UrpSurface SurfaceOf(BlendMode blendMode)
        {
            return blendMode switch
            {
                BlendMode.Cutout => UrpSurface.Cutout,
                BlendMode.Translucent => UrpSurface.Transparent,
                _ => UrpSurface.Opaque
            };
        }

        private static Material CreateDisplayMaterial(string name, Color color)
        {
            var shader = UrpShading.UnlitShader;
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader) { name = name };
            material.SetColor(UrpShading.BaseColor, color);
            UrpShading.ApplySurface(material, UrpSurface.Transparent, 0.5f, doubleSided: false);
            return material;
        }
    }
}
