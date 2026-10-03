using System;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity;
using Material = UnityEngine.Material;

namespace VisualPinball.Engine.Unity.Urp.Editor
{
    /// <summary>
    /// Fournit à VPE les prefabs de ses éléments sous URP.
    ///
    /// <para>Ces prefabs ne sont pas décoratifs : ce sont eux qui portent les composants d'auteur
    /// — <c>FlipperComponent</c>, <c>TroughComponent</c>, <c>PlungerComponent</c>… — dont VPE a
    /// besoin pour exister. Le core ne fournit que des versions « Builtin », liées au pipeline
    /// intégré, dont les matériaux sortiraient magenta sous URP. D'où ce jeu-ci, vendoré depuis
    /// le package URP du registre : ses prefabs sont identiques en structure mais liés à des
    /// matériaux URP.</para>
    ///
    /// <para>Trois familles d'éléments manquaient aussi purement et simplement au core :
    /// <c>Flipper</c>, <c>Plunger</c> et <c>Trough</c> ne vivaient que dans les packages de
    /// pipeline. Sans ce fournisseur, créer une table s'arrête net sur
    /// « Could not instantiate prefab for item … Trough ».</para>
    /// </summary>
    public class UrpPrefabProvider : IPrefabProvider
    {
        // Les prefabs sont sous Assets/VpeUrp/Resources/ : le nom du dossier « Resources » est
        // retiré du chemin de chargement, d'où « Prefabs/... » et non « VpeUrp/Resources/... ».
        private const string PrefabPath = "Prefabs/";
        private const string MaterialPath = "Materials/";
        private const string TexturePath = "Textures/";

        public GameObject CreateBumper() => Load(PrefabPath + "Bumper");

        public GameObject CreateGate(int type)
        {
            switch (type)
            {
                case GateType.GateLongPlate: return Load(PrefabPath + "Gate - Long Plate");
                case GateType.GatePlate: return Load(PrefabPath + "Gate - Plate");
                case GateType.GateWireRectangle: return Load(PrefabPath + "Gate - Wire Rectangle");
                case GateType.GateWireW: return Load(PrefabPath + "Gate - Wire W");
                default: throw new ArgumentException(nameof(type), $"Unknown gate type {type}.");
            }
        }

        public GameObject CreateKicker(int type)
        {
            switch (type)
            {
                case KickerType.KickerCup: return Load(PrefabPath + "Kicker - Cup");
                case KickerType.KickerCup2: return Load(PrefabPath + "Kicker - Cup 2");
                case KickerType.KickerGottlieb: return Load(PrefabPath + "Kicker - Gottlieb");
                case KickerType.KickerHole: return Load(PrefabPath + "Kicker - Hole");
                case KickerType.KickerHoleSimple: return Load(PrefabPath + "Kicker - Simple Hole");
                case KickerType.KickerWilliams: return Load(PrefabPath + "Kicker - Williams");
                case KickerType.KickerInvisible: return Load(PrefabPath + "Kicker - Invisible");
                default: throw new ArgumentException(nameof(type), $"Unknown kicker type {type}.");
            }
        }

        public GameObject CreateLight() => Load(PrefabPath + "Light");

        public GameObject CreateInsertLight() => Load(PrefabPath + "Light - Insert");

        public GameObject CreateSpinner() => Load(PrefabPath + "Spinner");

        public GameObject CreateHitTarget(int type)
        {
            switch (type)
            {
                case TargetType.HitFatTargetRectangle: return Load(PrefabPath + "Hit Target - Rectangle Fat");
                case TargetType.HitFatTargetSlim: return Load(PrefabPath + "Hit Target - Rectangle Fat Narrow");
                case TargetType.HitFatTargetSquare: return Load(PrefabPath + "Hit Target - Square Fat");
                case TargetType.HitTargetRectangle: return Load(PrefabPath + "Hit Target - Rectangle");
                case TargetType.HitTargetRound: return Load(PrefabPath + "Hit Target - Round");
                case TargetType.HitTargetSlim: return Load(PrefabPath + "Hit Target - Narrow");
                default: throw new ArgumentException(nameof(type), $"Unknown target type {type}.");
            }
        }

        public GameObject CreateDropTarget(int type)
        {
            switch (type)
            {
                case TargetType.DropTargetBeveled: return Load(PrefabPath + "Drop Target - Beveled");
                case TargetType.DropTargetFlatSimple: return Load(PrefabPath + "Drop Target - Simple Flat");
                case TargetType.DropTargetSimple: return Load(PrefabPath + "Drop Target - Simple");
                default: throw new ArgumentException(nameof(type), $"Unknown target type {type}.");
            }
        }

        public GameObject CreateFlipper() => Load(PrefabPath + "Flipper");

        public GameObject CreatePlunger() => Load(PrefabPath + "Plunger");

        public GameObject CreateTrough() => Load(PrefabPath + "Trough");

        /// <summary>Prefab de slingshot. Hors de <see cref="IPrefabProvider"/>, mais VPE en a un.</summary>
        public GameObject CreateSlingshot() => Load(PrefabPath + "Slingshot");

        /// <summary>Prefab de banc de cibles tombantes, utilisé par les banques de cibles.</summary>
        public GameObject CreateDropTargetBank() => Load(PrefabPath + "DropTargetBank");

        /// <summary>Matériau par défaut d'un mode de fusion, pour la table elle-même.</summary>
        public Material GetTableMaterial(BlendMode blendMode)
        {
            return blendMode switch
            {
                BlendMode.Opaque => LoadMaterial(MaterialPath + "TableOpaque"),
                BlendMode.Cutout => LoadMaterial(MaterialPath + "TableCutout"),
                BlendMode.Translucent => LoadMaterial(MaterialPath + "TableTranslucent"),
                _ => LoadMaterial(MaterialPath + "TableOpaque")
            };
        }

        public Material GetBallMaterial() => LoadMaterial(MaterialPath + "DefaultBall");

        public Texture2D GetTexture(string name) => UnityEngine.Resources.Load<Texture2D>(TexturePath + name);

        /// <summary>
        /// Charge un prefab en nommant l'élément manquant plutôt qu'en rendant null.
        ///
        /// <para>Un null silencieux se manifesterait bien plus loin, sous la forme d'un
        /// « Could not instantiate prefab » sans indiquer lequel : le nommer ici évite d'avoir à
        /// remonter la pile pour savoir ce qui manque.</para>
        /// </summary>
        private static GameObject Load(string path)
        {
            var prefab = UnityEngine.Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[UrpPrefabProvider] Prefab introuvable à « Resources/{path} ». " +
                               "Les assets URP vendorés sous Assets/VpeUrp/Resources sont-ils présents ?");
            }

            return prefab;
        }

        private static Material LoadMaterial(string path)
        {
            var material = UnityEngine.Resources.Load<Material>(path);
            if (material == null)
            {
                Debug.LogError($"[UrpPrefabProvider] Matériau introuvable à « Resources/{path} ».");
            }

            return material;
        }
    }
}
