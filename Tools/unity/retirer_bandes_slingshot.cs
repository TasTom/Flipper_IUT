// Retire les enfants de slingshot pour permettre leur repose. ÉCRITURE (annulable, non enregistrée).
//
// `place_scene_slingshot_bandes.cs` est idempotent : un `Slingshot_Band` déjà présent est laissé
// intact. C'est la bonne règle pour ne pas écraser un réglage à la main — mais elle empêche aussi
// de reposer après un changement de cotes. Or le pied de la face a bougé le 2026-09-15 (les
// flippers ont reculé de 0,06525 u pour élargir le drain gap à 1,75 bille), donc la bande est
// maintenant décalée : elle ne suit pas le collider réactif qu'elle est censée affleurer.
//
// Ce script lève l'ambiguïté en retirant la bande ; le script de pose la reconstruit aussitôt
// avec les bonnes cotes. Il ne touche à rien d'autre : ni l'hôte, ni son collider, ni le script
// `Slingshot`.
//
// Il retire aussi, s'il existe, un enfant nommé `Plastique_Decal` (la pièce du dépôt posée sous
// l'hôte) : même raison — une pièce assise aux anciennes cotes doit être reposée.

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

sb.AppendLine("=== scène '" + scene.name + "' ===");

var aRetirer = new string[] { "Slingshot_Band", "Plastique_Decal" };

foreach (var nomHote in new string[] { "Slingshot_Left", "Slingshot_Right" })
{
    var hote = gameplay.Find(nomHote);

    sb.AppendLine();
    sb.AppendLine("### " + nomHote);

    if (hote == null) { sb.AppendLine("  ABSENT"); continue; }

    int retires = 0;

    foreach (var nom in aRetirer)
    {
        var enfant = hote.Find(nom);

        if (enfant == null)
        {
            sb.AppendLine("  '" + nom + "' : absent");
            continue;
        }

        UnityEditor.Undo.DestroyObjectImmediate(enfant.gameObject);
        sb.AppendLine("  '" + nom + "' : retiré");
        retires++;
    }

    sb.AppendLine("  " + retires + " enfant(s) retiré(s) — reste " + hote.childCount);

    var col = hote.GetComponent<BoxCollider>();

    sb.AppendLine("  BoxCollider : " + (col != null
        ? "taille " + col.size.ToString("F4") + "   (laisse intact — repose par le script suivant)"
        : "ABSENT"));

    var script = hote.GetComponent("Slingshot");
    sb.AppendLine("  script Slingshot : " + (script != null ? "présent ✓" : "⚠ ABSENT"));
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");
sb.AppendLine("suite : `place_scene_slingshots.cs` puis `place_scene_slingshot_bandes.cs`");

var sortie = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "retrait_bandes.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sortie));
System.IO.File.WriteAllText(sortie, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(sortie) + "  (" + sb.Length + " car.)";
