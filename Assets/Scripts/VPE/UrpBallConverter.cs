using UnityEngine;
using VisualPinball.Unity;

namespace VisualPinball.Engine.Unity.Urp
{
    /// <summary>
    /// Fournit la bille par défaut de VPE sous URP.
    ///
    /// Le prefab porte un BallComponent à sa racine et un visuel à l'échelle réelle.
    /// Les collisions et le déplacement sont gérés par VPE, sans Rigidbody PhysX.
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
            if (fromResources != null && fromResources.GetComponent<BallComponent>() != null)
            {
                return fromResources;
            }

            var packaged = UnityEngine.Resources.Load<GameObject>("Prefabs/DefaultBall");
            if (packaged != null && packaged.GetComponent<BallComponent>() != null) return packaged;
            throw new System.InvalidOperationException("[VPE URP] Préfab de bille manquant : créer Resources/VpeUrp/Ball avec un BallComponent à la racine.");
        }
    }
}
