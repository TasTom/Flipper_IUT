using VisualPinball.Unity;

namespace VisualPinball.Engine.Unity.Urp
{
    /// <summary>
    /// Déclare URP comme pipeline de rendu de VPE.
    ///
    /// VPE découvre son pipeline en balayant les assemblies dont le nom commence par
    /// « VisualPinball. » à la recherche d'un <see cref="IRenderPipeline"/>, puis retient le
    /// premier qui n'est pas le pipeline intégré. Le nom de l'assembly de ce dossier
    /// (« VisualPinball.Unity.Urp ») n'est donc pas décoratif : c'est ce qui rend cette classe
    /// trouvable. La renommer reviendrait à désinstaller l'adaptateur, et VPE n'aurait plus ni
    /// matériaux, ni lumières, ni bille.
    /// </summary>
    public class UrpRenderPipeline : IRenderPipeline
    {
        public string Name => "Universal Render Pipeline";

        public RenderPipelineType Type => RenderPipelineType.Urp;

        public IMaterialConverter MaterialConverter { get; } = new UrpMaterialConverter();

        public ILightConverter LightConverter { get; } = new UrpLightConverter();

        public IBallConverter BallConverter { get; } = new UrpBallConverter();
    }
}
