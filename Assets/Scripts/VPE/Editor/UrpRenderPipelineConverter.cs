using VisualPinball.Unity;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Engine.Unity.Urp.Editor
{
    /// <summary>
    /// Déclare URP comme pipeline de rendu de VPE côté éditeur.
    ///
    /// <para>Le runtime a son pendant (<see cref="UrpRenderPipeline"/>), mais l'éditeur en a un
    /// autre, et il n'est pas facultatif : c'est lui qui fournit les prefabs des éléments de
    /// table au moment de leur création. Le core n'expose qu'un jeu « Builtin » lié au pipeline
    /// intégré, et sans ce converter c'est lui qui serait retenu — la table se créerait avec des
    /// matériaux que personne n'affiche.</para>
    ///
    /// <para>Le nom de l'assembly compte ici aussi : VPE balaie les assemblies préfixés de
    /// « VisualPinball. » pour trouver les implémentations de <see cref="IRenderPipelineConverter"/>.</para>
    /// </summary>
    public class UrpRenderPipelineConverter : IRenderPipelineConverter
    {
        public string Name => "Universal Render Pipeline";

        public RenderPipelineType Type => RenderPipelineType.Urp;

        public IPrefabProvider PrefabProvider { get; } = new UrpPrefabProvider();
    }
}
