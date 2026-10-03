using UnityEngine;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Unity;
using Light = UnityEngine.Light;

namespace VisualPinball.Engine.Unity.Urp
{
    /// <summary>
    /// Applique les données de lumière Visual Pinball à une <see cref="Light"/> URP.
    ///
    /// Deux différences avec l'adaptateur HDRP, toutes deux parce qu'URP n'a pas de composant
    /// additionnel de lumière :
    ///
    /// <list type="bullet">
    /// <item>HDRP range ses réglages dans <c>HDAdditionalLightData</c> ; ici tout vit sur la
    /// <see cref="Light"/> elle-même, donc les méthodes restent valables même si la lumière n'a
    /// pas été configurée par VPE ;</item>
    /// <item>URP ignore les unités photométriques — une intensité y est un nombre sans dimension,
    /// pas des lumens. La conversion se cale donc sur celle du pipeline intégré (÷2) plutôt que
    /// sur l'échelle HDRP (÷4), qui n'a de sens qu'en unités physiques.</item>
    /// </list>
    /// </summary>
    public class UrpLightConverter : ILightConverter
    {
        public void UpdateLight(Light light, LightData data, bool isInsert)
        {
            if (light == null || data == null)
            {
                return;
            }

            light.color = data.Color2.ToUnityColor();

            if (!isInsert)
            {
                light.intensity = data.Intensity / 2f;
                light.range = data.Falloff * 0.001f;

                // Les données VPX ne portent pas de hauteur ; 25 cm au-dessus de l'insert est la
                // valeur qu'emploient les deux adaptateurs de référence.
                light.transform.localPosition = new Vector3(0f, 0f, 25f);
            }
            else
            {
                light.intensity = data.Intensity * 10f;
            }

            light.shadows = LightShadows.None;
        }

        public void SetColor(Light light, Color color)
        {
            if (light != null)
            {
                light.color = color;
            }
        }

        public void SetShadow(Light light, bool enabled, bool isDynamic, float nearPlane = 0.01f)
        {
            if (light == null)
            {
                return;
            }

            light.shadows = enabled ? LightShadows.Soft : LightShadows.None;
            light.shadowNearPlane = nearPlane;
        }

        public void SetRange(Light light, float range)
        {
            if (light != null)
            {
                light.range = range;
            }
        }

        /// <summary>
        /// Fixe l'intensité. Le paramètre est nommé « lumen » par l'interface, héritage de HDRP :
        /// sous URP c'est un nombre sans dimension, appliqué tel quel.
        /// </summary>
        public void SetIntensity(Light light, float intensityLumen)
        {
            if (light != null)
            {
                light.intensity = intensityLumen;
            }
        }

        public void SetTemperature(Light light, float temperature)
        {
            if (light == null)
            {
                return;
            }

            light.useColorTemperature = true;
            light.colorTemperature = Mathf.Max(0f, temperature);
        }

        public void SpotLight(Light light, float outer, float innerPercent)
        {
            if (light == null)
            {
                return;
            }

            light.type = LightType.Spot;
            light.spotAngle = outer;
            light.innerSpotAngle = outer * Mathf.Clamp01(innerPercent * 0.01f);
        }

        /// <summary>
        /// Sans effet : le cône pyramidal est une forme de spot propre à HDRP. URP n'a que le
        /// cône circulaire, et VPE retombe déjà sur un spot ordinaire quand cette méthode ne
        /// change rien.
        /// </summary>
        public void PyramidAngle(Light light, float angle, float aspectRatio)
        {
        }
    }
}
