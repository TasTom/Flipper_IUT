using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Unity;
using Material = UnityEngine.Material;

namespace VisualPinball.Engine.Unity.Urp
{
    /// <summary>
    /// Construit un matériau URP à partir d'un profil portable de table VPE.
    ///
    /// Un profil décrit une intention de rendu, pas un shader : c'est au pipeline de la traduire.
    /// HDRP y répond avec ses matériaux traversants et ses profils de diffusion ; URP n'a ni l'un
    /// ni l'autre, donc deux familles de profils sont approchées plutôt que traduites :
    ///
    /// <list type="bullet">
    /// <item><b>les types à shader graph</b> (<c>vpe.metal</c>, <c>vpe.rubber</c>,
    /// <c>vpe.fabric.silk</c>) désignent un modèle de matériau du Player HDRP. URP n'a pas ce
    /// modèle : on retombe sur le profil Lit portable que ces types embarquent justement pour les
    /// pipelines qui ne les connaissent pas ;</item>
    /// <item><b>les inserts</b> (<c>vpe.insert</c>) sont des lentilles traversantes éclairées par
    /// dessous. Le schéma prévoit explicitement ce cas — « knowing this is an insert lens lets
    /// pipelines without translucency substitute a purpose-built approximation » — d'où une
    /// surface transparente teintée par la couleur de transmittance, et surtout l'émission
    /// activée, parce que c'est par elle que VPE allume l'insert.</item>
    /// </list>
    /// </summary>
    public class UrpMaterialResolver : IVpeMaterialResolver
    {
        /// <summary>Nom sous lequel un projet peut fournir son propre matériau de point matrix.</summary>
        private const string DotMatrixResourcePath = "VpeUrp/DotMatrixDisplay";

        public bool Supports(string materialType)
        {
            return materialType switch
            {
                VpeMaterialTypes.Lit => true,
                VpeMaterialTypes.Unlit => true,
                VpeMaterialTypes.Decal => true,
                VpeMaterialTypes.Insert => true,
                // Types à shader graph : pris en charge par repli sur leur profil Lit portable.
                VpeMaterialTypes.Metal => true,
                VpeMaterialTypes.Rubber => true,
                VpeMaterialTypes.FabricSilk => true,
                VpeMaterialTypes.Dmd => true,
                _ => false
            };
        }

        public Material CreateMaterial(VpeMaterialProfile profile, IVpeTextureProvider textures, Material importedMaterial)
        {
            if (profile == null)
            {
                return null;
            }

            switch (profile.Type)
            {
                case VpeMaterialTypes.Unlit:
                    return CreateUnlit(profile, profile.Unlit, textures, importedMaterial);

                case VpeMaterialTypes.Insert:
                    return CreateInsert(profile, profile.Insert, textures, importedMaterial);

                case VpeMaterialTypes.Decal:
                    // URP a un système de decals à part ; un matériau Lit transparent rend la même
                    // intention de façon approchée, et se place correctement dans la file de dessin.
                    return profile.Decal != null
                        ? CreateDecalLike(profile, profile.Decal, textures, importedMaterial)
                        : null;

                case VpeMaterialTypes.Dmd:
                    return CreateDmdDisplay(profile);

                case VpeMaterialTypes.Metal:
                case VpeMaterialTypes.Rubber:
                case VpeMaterialTypes.FabricSilk:
                    // Ces profils portent le même payload Lit que vpe.lit : il décrit ce que la
                    // table veut montrer, à défaut du modèle HDRP exact.
                    return CreateLit(profile, profile.Lit, textures, importedMaterial);

                case VpeMaterialTypes.Lit:
                    return CreateLit(profile, profile.Lit, textures, importedMaterial);

                default:
                    return null;
            }
        }

        private static Material CreateLit(VpeMaterialProfile profile, VpeLitProfile lit,
                                          IVpeTextureProvider textures, Material importedMaterial)
        {
            if (lit == null)
            {
                return null;
            }

            var material = UrpShading.NewLitMaterial(profile.Name ?? "vpe.lit");
            if (material == null)
            {
                return null;
            }

            material.SetColor(UrpShading.BaseColor, (Color)lit.BaseColor.Color);
            material.SetFloat(UrpShading.Metallic, Mathf.Clamp01(lit.Metallic));
            material.SetFloat(UrpShading.Smoothness, Mathf.Clamp01(lit.Smoothness));

            ApplyTexture(material, UrpShading.BaseMap, lit.BaseColor.Texture, textures, importedMaterial);

            if (lit.NormalMap != null && (lit.NormalMap.Strength > 0f))
            {
                var normal = ResolveTexture(lit.NormalMap.TextureId, textures, importedMaterial);
                if (normal != null)
                {
                    material.SetTexture(UrpShading.BumpMap, normal);
                    material.SetFloat(UrpShading.BumpScale, lit.NormalMap.Strength);
                    material.EnableKeyword("_NORMALMAP");
                }
            }

            ApplyEmissive(material, lit.Emissive, textures);

            UrpShading.ApplySurface(material, SurfaceOf(lit.SurfaceType, lit.BlendMode), lit.AlphaCutoff,
                                    lit.DoubleSided, lit.SortPriority);
            UrpShading.EnableEmission(material);

            return material;
        }

        private static Material CreateUnlit(VpeMaterialProfile profile, VpeUnlitProfile unlit,
                                            IVpeTextureProvider textures, Material importedMaterial)
        {
            if (unlit == null)
            {
                return null;
            }

            var shader = UrpShading.UnlitShader;
            if (shader == null)
            {
                // Sans shader Unlit, un Lit sans éclairage approché vaut mieux que pas de matériau.
                return CreateLit(profile, new VpeLitProfile { BaseColor = unlit.BaseColor, Smoothness = 0f },
                                 textures, importedMaterial);
            }

            var material = new Material(shader) { name = profile.Name ?? "vpe.unlit" };
            material.SetColor(UrpShading.BaseColor, (Color)unlit.BaseColor.Color);
            ApplyTexture(material, UrpShading.BaseMap, unlit.BaseColor.Texture, textures, importedMaterial);
            UrpShading.ApplySurface(material, SurfaceOf(unlit.SurfaceType, VpeBlendModes.Alpha),
                                    unlit.AlphaCutoff, unlit.DoubleSided);

            return material;
        }

        /// <summary>
        /// Approxime un insert : une lentille translucide teintée par la couleur de transmittance,
        /// dont l'émission est prête à être pilotée par la lampe placée dessous.
        /// </summary>
        private static Material CreateInsert(VpeMaterialProfile profile, VpeInsertProfile insert,
                                             IVpeTextureProvider textures, Material importedMaterial)
        {
            var lit = insert != null ? insert.Lit : null;
            if (lit == null)
            {
                return null;
            }

            var material = CreateLit(profile, lit, textures, importedMaterial);
            if (material == null)
            {
                return null;
            }

            // La couleur de transmittance dit ce que le plastique laisse passer : c'est elle qui
            // doit colorer l'émission, sans quoi tous les inserts s'allument en blanc.
            var tint = (Color)lit.TransmittanceColor;
            material.SetColor(UrpShading.EmissionColor, tint);
            UrpShading.EnableEmission(material);

            // Un insert posé sur le plateau est vu de dessus, et la vitre au-dessus de lui est
            // transparente : le faire passer après les autres surfaces transparentes évite qu'il
            // disparaisse derrière la vitre.
            if (material.renderQueue < (int)RenderQueue.Transparent)
            {
                material.renderQueue = (int)RenderQueue.Transparent + Mathf.Clamp(lit.SortPriority + 10, -50, 50);
            }

            return material;
        }

        private static Material CreateDecalLike(VpeMaterialProfile profile, VpeDecalProfile decal,
                                                IVpeTextureProvider textures, Material importedMaterial)
        {
            var material = UrpShading.NewLitMaterial(profile.Name ?? "vpe.decal");
            if (material == null)
            {
                return null;
            }

            material.SetColor(UrpShading.BaseColor, (Color)decal.BaseColor.Color);
            material.SetFloat(UrpShading.Metallic, Mathf.Clamp01(decal.Metallic));
            material.SetFloat(UrpShading.Smoothness, Mathf.Clamp01(decal.Smoothness));
            ApplyTexture(material, UrpShading.BaseMap, decal.BaseColor.Texture, textures, importedMaterial);

            // Un decal se pose par-dessus la surface qu'il décore : transparent, sans écriture de
            // profondeur, et plutôt tard dans la file de dessin.
            UrpShading.ApplySurface(material, UrpSurface.Transparent, 0.5f, doubleSided: false, sortPriority: 20);

            return material;
        }

        private static Material CreateDmdDisplay(VpeMaterialProfile profile)
        {
            // Un point matrix display n'a pas de profil portable : seul son type est décrit. Le
            // matériau du projet fait foi, et le nom du profil est conservé pour s'y retrouver.
            var material = UnityEngine.Resources.Load<Material>(DotMatrixResourcePath);
            return material != null ? material : UrpShading.NewLitMaterial(profile.Name ?? "vpe.dmd");
        }

        private static void ApplyEmissive(Material material, VpeEmissive emissive, IVpeTextureProvider textures)
        {
            if (emissive == null)
            {
                return;
            }

            // HasLdrColor signale que l'auteur a borné son émission à la plage LDR ; c'est cette
            // valeur-là qu'il faut alors préférer, sinon l'intention est celle de Color.
            var color = emissive.HasLdrColor ? (Color)emissive.LdrColor : (Color)emissive.Color;

            if (emissive.UseIntensity && emissive.Intensity > 0f)
            {
                color *= emissive.Intensity;
            }

            material.SetColor(UrpShading.EmissionColor, color);

            if (emissive.Texture != null && !string.IsNullOrEmpty(emissive.Texture.TextureId) && textures != null)
            {
                var texture = textures.Get(emissive.Texture.TextureId);
                if (texture != null)
                {
                    material.SetTexture(UrpShading.EmissionColor, texture);
                }
            }
        }

        private static void ApplyTexture(Material material, int property, VpeTextureRef reference,
                                         IVpeTextureProvider textures, Material importedMaterial)
        {
            var texture = ResolveTexture(reference?.TextureId, textures, importedMaterial);
            if (texture == null)
            {
                return;
            }

            material.SetTexture(property, texture);

            if (reference != null)
            {
                material.SetTextureOffset(property, reference.Offset);
                material.SetTextureScale(property, reference.Scale);
            }
        }

        /// <summary>
        /// Trouve la texture d'une référence de profil.
        ///
        /// Un identifiant vide n'est pas forcément « pas de texture » : sur les profils relevés
        /// depuis le chemin glTF, il veut dire « prends celle du matériau importé ». C'est la
        /// convention décrite par <see cref="VpeTextureRef"/>, et l'ignorer ferait perdre toutes
        /// les cartes d'une table importée.
        /// </summary>
        private static Texture2D ResolveTexture(string textureId,
                                                IVpeTextureProvider textures, Material importedMaterial)
        {
            if (!string.IsNullOrEmpty(textureId) && textures != null)
            {
                var resolved = textures.Get(textureId);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            if (string.IsNullOrEmpty(textureId) && importedMaterial != null
                && importedMaterial.HasProperty(UrpShading.BaseMap))
            {
                return importedMaterial.GetTexture(UrpShading.BaseMap) as Texture2D;
            }

            return null;
        }

        private static UrpSurface SurfaceOf(string surfaceType, string blendMode)
        {
            if (surfaceType == VpeSurfaceTypes.Transparent)
            {
                return blendMode == VpeBlendModes.Additive ? UrpSurface.Additive : UrpSurface.Transparent;
            }

            return surfaceType == VpeSurfaceTypes.AlphaTest ? UrpSurface.Cutout : UrpSurface.Opaque;
        }
    }
}
