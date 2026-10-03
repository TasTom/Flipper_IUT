using UnityEditor;

namespace VisualPinball.Engine.Unity.Urp.Editor
{
    /// <summary>
    /// Enregistre l'adaptateur URP dès l'ouverture de l'éditeur.
    ///
    /// Sans cela, <see cref="UrpVpeBootstrap"/> ne s'exécute qu'à l'entrée en Play mode, et une
    /// table VPE importée depuis l'éditeur construirait ses matériaux avec le resolver par défaut
    /// — donc ceux du glTF, sans les intentions de rendu du profil. Le symptôme serait discret :
    /// la table s'ouvre, mais ses plastiques et ses inserts sortent ternes.
    /// </summary>
    internal static class UrpVpeEditorBootstrap
    {
        [InitializeOnLoadMethod]
        private static void Register() => UrpVpeBootstrap.EnsureRegistered();
    }
}
