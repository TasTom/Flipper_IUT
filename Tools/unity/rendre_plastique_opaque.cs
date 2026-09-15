// Crée le matériau du plastique de slingshot s'il manque, et l'applique. ÉCRITURE (annulable).
//
// ── Le problème ───────────────────────────────────────────────────────────────────────
// Le plastique de slingshot avait été mappé sur `PlayfieldGlass_Mat` — le VERRE du plateau
// (alpha 0,06). Le slingshot était donc transparent. C'était une erreur de ma part : le nom de
// la pièce (« Plastic - Clear ») a été pris pour une consigne de transparence, alors que sur un
// flipper le plastique de slingshot est une plaque OPAQUE.
//
// ── Le matériau ───────────────────────────────────────────────────────────────────────
// Un plastique injecté : blanc cassé, lisse mais pas brillant, non métallique. Créé une seule
// fois dans `Assets/Materials/`, puis réutilisé — c'est ce qui en fait un réglage unique et
// éditable dans l'Inspector, plutôt qu'une couleur écrite dans un script.
//
// Le DÉCAL (`Plastics Decal`) est laissé tel quel : c'est le sous-maillage imprimé, il porte sa
// propre couleur.

var sb = new System.Text.StringBuilder();

const string cheminMat = "Assets/Materials/SlingshotPlastic_Mat.mat";

// --- 1. le matériau, créé seulement s'il manque ---

var plastique = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(cheminMat);

if (plastique == null)
{
    // On part d'un matériau du projet pour hériter du shader URP/Lit et de ses mots-clés.
    // `new Material(Shader.Find(...))` marcherait aussi, mais un matériau neuf n'a pas les
    // mots-clés URP actifs (GPU instancing, specular, etc.), et le rendu diffère des autres.
    var modele = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
        "Assets/Materials/Playfield_Mat.mat");

    if (modele == null) { return "Playfield_Mat introuvable — je ne crée pas de matériau au hasard."; }

    plastique = new Material(modele);

    plastique.name = "SlingshotPlastic_Mat";

    UnityEditor.AssetDatabase.CreateAsset(plastique, cheminMat);
    UnityEditor.AssetDatabase.SaveAssets();

    sb.AppendLine("matériau CRÉÉ : " + cheminMat);
    sb.AppendLine("  (dérivé de " + modele.name + " : shader " + plastique.shader.name + ")");
}
else
{
    sb.AppendLine("matériau déjà présent : " + cheminMat);
}

// --- 2. ses valeurs ---
// Blanc cassé plutôt que blanc pur : le blanc pur écrase les lumières du plateau et donne un
// aspect « plastique neuf » qui jure avec le reste. 0,88 garde du modelé.
var teinte = new Color(0.88f, 0.88f, 0.86f, 1f);

plastique.SetColor("_BaseColor", teinte);

// Opaque, sans équivoque : `_Surface` à 0, `_Blend` à 0, `_ZWrite` à 1 et l'alpha à 1.
// Les quatre sont nécessaires — URP décide de l'opacité sur la combinaison, et un alpha à 1
// sur une surface transparente laisse quand même passer la lumière.
if (plastique.HasProperty("_Surface")) { plastique.SetFloat("_Surface", 0f); }
if (plastique.HasProperty("_Blend")) { plastique.SetFloat("_Blend", 0f); }
if (plastique.HasProperty("_AlphaClip")) { plastique.SetFloat("_AlphaClip", 0f); }
if (plastique.HasProperty("_ZWrite")) { plastique.SetFloat("_ZWrite", 1f); }
if (plastique.HasProperty("_SrcBlend")) { plastique.SetFloat("_SrcBlend", 1f); }   // One
if (plastique.HasProperty("_DstBlend")) { plastique.SetFloat("_DstBlend", 0f); }   // Zero
if (plastique.HasProperty("_Smoothness")) { plastique.SetFloat("_Smoothness", 0.45f); }
if (plastique.HasProperty("_Metallic")) { plastique.SetFloat("_Metallic", 0f); }

plastique.renderQueue = -1;

UnityEditor.EditorUtility.SetDirty(plastique);

sb.AppendLine();
sb.AppendLine("réglages : couleur (0,88 ; 0,88 ; 0,86 ; 1)   surface OPAQUE   lisse 0,45   métal 0");
sb.AppendLine("  _Surface " + (plastique.HasProperty("_Surface") ? plastique.GetFloat("_Surface").ToString("F0") : "—")
              + "   alpha " + plastique.GetColor("_BaseColor").a.ToString("F2")
              + "   _ZWrite " + (plastique.HasProperty("_ZWrite") ? plastique.GetFloat("_ZWrite").ToString("F0") : "—"));

// --- 3. l'application sur les deux slingshots ---

sb.AppendLine();
sb.AppendLine("=== application ===");

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null)
{
    sb.AppendLine("  'PinballTable/Gameplay' introuvable — rien appliqué.");
}
else
{
    foreach (var nom in new string[] { "Slingshot_Left", "Slingshot_Right" })
    {
        var hote = gameplay.Find(nom);
        var enfant = hote != null ? hote.Find("Plastique_Decal") : null;

        sb.AppendLine();
        sb.AppendLine("  " + nom);

        if (enfant == null) { sb.AppendLine("    pas de 'Plastique_Decal' — rien à faire"); continue; }

        int remplaces = 0;

        foreach (var rendu in enfant.GetComponentsInChildren<MeshRenderer>())
        {
            var mats = rendu.sharedMaterials;
            bool change = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) { continue; }

                // Ce qui est transparent est remplacé ; ce qui est déjà opaque est laissé.
                // Le test porte sur la surface ET sur l'alpha, parce qu'un matériau peut être
                // déclaré transparent avec un alpha à 1 — auquel cas il n'y a rien à corriger.
                bool transparent = (mats[i].HasProperty("_Surface")
                                    && mats[i].GetFloat("_Surface") > 0.5f)
                                   || (mats[i].HasProperty("_BaseColor")
                                       && mats[i].GetColor("_BaseColor").a < 0.99f);

                if (transparent && mats[i] != plastique)
                {
                    sb.AppendLine("    '" + mats[i].name + "' → " + plastique.name);
                    mats[i] = plastique;
                    change = true;
                    remplaces++;
                }
            }

            if (change)
            {
                UnityEditor.Undo.RecordObject(rendu, "Rendre le plastique de slingshot opaque");
                rendu.sharedMaterials = mats;
            }
        }

        if (remplaces == 0) { sb.AppendLine("    aucun matériau transparent — rien changé"); }

        // Le contrôle : que porte le plastique maintenant ?
        foreach (var rendu in enfant.GetComponentsInChildren<MeshRenderer>())
        {
            for (int i = 0; i < rendu.sharedMaterials.Length; i++)
            {
                var m = rendu.sharedMaterials[i];

                if (m == null) { continue; }

                bool transparent = (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f)
                                   || (m.HasProperty("_BaseColor")
                                       && m.GetColor("_BaseColor").a < 0.99f);

                sb.AppendLine("    [" + i + "] '" + m.name + "'   surface "
                              + (m.HasProperty("_Surface") ? m.GetFloat("_Surface").ToString("F0") : "—")
                              + "   alpha " + (m.HasProperty("_BaseColor")
                                  ? m.GetColor("_BaseColor").a.ToString("F2") : "—")
                              + (transparent ? "   ⚠ ENCORE TRANSPARENT" : "   ✓ opaque"));
            }
        }
    }
}

UnityEditor.AssetDatabase.SaveAssets();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");

var sortie = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "plastique_opaque.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sortie));
System.IO.File.WriteAllText(sortie, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(sortie) + "  (" + sb.Length + " car.)";
