using UnityEngine;
using VisualPinball.Unity;

namespace VisualPinball.Engine.Unity.Urp
{
    /// <summary>
    /// Fournit la bille par défaut de VPE sous URP.
    ///
    /// Le prefab fourni par un package VPE ne porte qu'un maillage, à l'échelle 1 : ni collider,
    /// ni <see cref="Rigidbody"/>, parce que VPE les pose lui-même au moment de la mise en jeu, à
    /// partir du rayon déclaré sur le composant de bille. Le repli procédural suit exactement la
    /// même convention, sans quoi le rayon serait appliqué deux fois.
    ///
    /// <para>Un prefab du projet, déposé dans <c>Resources/VpeUrp/Ball</c>, prend le pas : c'est
    /// ce qui permet de fournir un modèle de bille à soi sans toucher au code.</para>
    /// </summary>
    public class UrpBallConverter : IBallConverter
    {
        private const string BallResourcePath = "VpeUrp/Ball";

        public GameObject CreateDefaultBall()
        {
            var fromResources = UnityEngine.Resources.Load<GameObject>(BallResourcePath);
            if (fromResources != null)
            {
                return fromResources;
            }

            return CreateFallbackBall();
        }

        /// <summary>
        /// Sphère d'un diamètre de 1, échelle 1 : le rayon réel vient du composant de bille de
        /// VPE, pas du maillage. Le matériau est un métal poli, proche du chrome d'une bille
        /// réelle — une couleur blanche rendrait mal le reflet de la table.
        /// </summary>
        private static GameObject CreateFallbackBall()
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "VPE Default Ball";

            var collider = ball.GetComponent<UnityEngine.Collider>();
            if (collider != null)
            {
                // VPE pose ses propres colliders : garder celui de la primitive en ajouterait un
                // second, et la bille rebondirait contre elle-même.
                Object.Destroy(collider);
            }

            var material = UrpShading.NewLitMaterial("VPE Ball Chrome");
            if (material != null)
            {
                material.SetColor(UrpShading.BaseColor, new Color(0.85f, 0.86f, 0.88f));
                material.SetFloat(UrpShading.Metallic, 1f);
                material.SetFloat(UrpShading.Smoothness, 0.95f);
                UrpShading.ApplySurface(material, UrpSurface.Opaque, 0.5f, doubleSided: false);

                var renderer = ball.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }

            return ball;
        }
    }
}
