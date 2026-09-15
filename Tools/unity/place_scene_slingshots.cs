// Pose les slingshots réactifs — gauche et droit. ÉCRITURE (annulable, idempotent).
//
// ── Ce qui existe déjà, mesuré ────────────────────────────────────────────────────────
// `Pinball_Table.fbx` porte DÉJÀ les deux corps de slingshot (`Slingshots/Body_L`,
// `Slingshots/Body_R`), chacun avec son MeshCollider. Leur face active est correcte :
// elle est le segment `a → b` du prisme, non décalé. Ce qui manque n'est donc pas le
// corps, c'est **la bande réactive** — et `Slingshot.cs` se déclenche en
// `OnCollisionEnter`, donc il lui faut un collider SOLIDE sur son propre GameObject.
//
// ── Pourquoi le collider va sur l'hôte ────────────────────────────────────────────────
// Unity remet les messages de collision au GameObject qui porte le **collider**, et non
// à son parent. Un collider sur un enfant laisserait donc `Slingshot` (sur l'hôte) muet.
// Le contrat de scène dit d'ailleurs « `Slingshot` + collider » sur l'hôte.
// Conséquence à connaître : menu 3 (`NeutralScene.Strip`) retire les composants directs
// des hôtes, donc il effacerait ce collider. C'est un menu de normalisation d'une scène
// neuve, pas une étape du flux de travail courant.
//
// ── Les cotes viennent du générateur, pas d'une mesure au jugé ────────────────────────
// `Tools/blender/build_table.py` : SLING_TOP = (0,1925 ; 0,330) m, SLING_BOT = (0,088 ;
// 0,085) m, SLING_T = 0,022 m, corps de y 0 à 0,022 m. À 16,66667 u/m :
//   SLING_TOP → 3,20833 ; 5,50000      SLING_BOT → 1,46667 ; 1,41667
// Le pied de la face tombe sur le pivot du flipper (0,0857 ; 0,092) m — c'est voulu :
// « la bille qui la descend est déposée sur la base du flipper, pas dans le drain ».
//
// La normale extérieure est écrite **corrigée** (`nz` × côté) : `diagonal_band` oublie ce
// facteur, d'où un corps gauche bombé 7,8 mm du mauvais côté. La face active, elle, est
// juste des deux côtés — c'est pourquoi cette pose n'a pas besoin d'une régénération Blender.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;          // unités Unity par mètre (ancre : bille Ø 0,45 = 27 mm)

// cotes du générateur, en unités Unity — sauf le PIED, qui a suivi le pivot des flippers le
// 2026-09-15 : élargir le drain gap de 1,46 à 1,75 bille a reculé les pivots de 0,06525 u vers
// l'extérieur, et le pied de la face doit rester sous le talon du bat — sinon la bille qui
// descend la face n'est plus déposée sur le flipper. Le générateur posait `SLING_BOT` SUR le pivot
// (`build_table.py` : 0,088 m contre `FLIP_PIVOT` 0,0857 m) ; ce lien est conservé :
//     BOT_X = 1,46666667 + 0,06525000 = 1,53191667 u = 0,0919150 m
// La face s'en trouve raccourcie de 266,4 à 264,8 mm, et son plan reste 0,28 mm en jeu du pivot
// (il l'était de 0,39 mm) : le bat ne mord pas dans le slingshot.
const float TOP_X = 0.1925f * U;      // 3,20833
const float TOP_Z = 0.330f * U;       // 5,50000
const float BOT_X = 0.091915f * U;    // 1,53192  (ex-0,088 → 1,46667)
const float BOT_Z = 0.085f * U;       // 1,41667
const float CORPS_H = 0.022f * U;     // 0,36667  hauteur du corps

// la bande réactive
const float BANDE_EPAIS = 0.12f;      // épaisseur vers l'intérieur du jeu (7,2 mm)
const float BANDE_MARGE = 0.04f;      // retrait à chaque bout (2,4 mm)

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;

Transform Trouver(Transform r, string nom)
{
    if (r.name == nom) { return r; }
    for (int i = 0; i < r.childCount; i++)
    {
        var t = Trouver(r.GetChild(i), nom);
        if (t != null) { return t; }
    }
    return null;
}

// Calcule la pose de la face pour un côté : s = +1 droite, −1 gauche.
// Renvoie : [0] origine (SLING_TOP), [1] extrémité (SLING_BOT), [2] normale EXTÉRIEURE.
Vector3[] Face(int s)
{
    Vector3 a = new Vector3(s * TOP_X, 0f, TOP_Z);   // SLING_TOP, au mur
    Vector3 b = new Vector3(s * BOT_X, 0f, BOT_Z);   // SLING_BOT, au pied du flipper

    float dx = b.x - a.x;
    float dz = b.z - a.z;
    float len = Mathf.Sqrt(dx * dx + dz * dz);

    // normale extérieure (vers le mur) — le `s` sur les DEUX composantes est la correction
    Vector3 n = new Vector3(s * (-dz / len), 0f, s * (dx / len));

    return new[] { a, b, n };
}

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

foreach (var cote in new[] { new { nom = "Slingshot_Right", s = 1 }, new { nom = "Slingshot_Left", s = -1 } })
{
    var hote = Trouver(rt, cote.nom);

    if (hote == null) { sb.AppendLine(cote.nom + " : ABSENT — on ne le crée pas."); continue; }

    var f = Face(cote.s);
    Vector3 a = f[0], b = f[1], nExt = f[2];
    Vector3 nInt = -nExt;

    float L = Vector3.Distance(a, b);
    Vector3 centreFace = (a + b) * 0.5f;
    float y = CORPS_H * 0.5f;                     // mi-hauteur du corps : le collider le couvre
    Vector3 posLocale = new Vector3(centreFace.x, y, centreFace.z);

    // repère local : X le long de la face (du pied vers le mur), Y vertical, Z vers l'intérieur du jeu
    Quaternion rotLocale = Quaternion.LookRotation(nInt, Vector3.up);

    sb.AppendLine("=== " + cote.nom + " (côté " + (cote.s > 0 ? "droit" : "gauche") + ") ===");
    sb.AppendLine("  face : de (" + a.x.ToString("F4") + ", " + a.z.ToString("F4") + ") à ("
        + b.x.ToString("F4") + ", " + b.z.ToString("F4") + ")   longueur " + L.ToString("F4") + " u");
    sb.AppendLine("  normale extérieure (" + nExt.x.ToString("F4") + ", " + nExt.z.ToString("F4") + ")");
    sb.AppendLine("  hôte : local (" + posLocale.x.ToString("F4") + ", " + posLocale.y.ToString("F4")
        + ", " + posLocale.z.ToString("F4") + ")");

    bool hoteDejaPose = hote.localPosition == posLocale
        && Quaternion.Angle(hote.localRotation, rotLocale) < 0.01f;

    UnityEditor.Undo.RecordObject(hote, "Poser le slingshot " + cote.nom);

    // pose monde — passer par la rotation de la racine évite de supposer les parents identité
    hote.SetPositionAndRotation(rt.TransformPoint(posLocale), rt.rotation * rotLocale);
    sb.AppendLine("  pose : " + (hoteDejaPose ? "déjà en place (inchangée)" : "posée"));

    // --- le collider réactif ------------------------------------------------------------
    var col = hote.GetComponent<BoxCollider>();
    bool colCree = false;

    if (col == null)
    {
        col = UnityEditor.Undo.AddComponent<BoxCollider>(hote.gameObject);
        colCree = true;
    }

    // centre décalé vers l'intérieur : la boîte doit être ENTIÈREMENT du côté jeu de la face,
    // sinon elle recouvre le MeshCollider du corps (deux colliders plein volume au même endroit)
    col.center = new Vector3(0f, 0f, BANDE_EPAIS * 0.5f);
    col.size = new Vector3(L - 2f * BANDE_MARGE, CORPS_H, BANDE_EPAIS);
    col.isTrigger = false;

    sb.AppendLine("  BoxCollider " + (colCree ? "créé" : "existant, réglé")
        + " : centre (0, 0, " + col.center.z.ToString("F3") + ")  taille "
        + col.size.x.ToString("F3") + " × " + col.size.y.ToString("F3") + " × " + col.size.z.ToString("F3"));

    var sl = hote.GetComponent("Slingshot");

    if (sl == null) { sb.AppendLine("  ⚠ le script Slingshot est absent de l'hôte."); }

    // --- vérification : ce que la bille rencontre en venant de l'aire de jeu ---------------
    Vector3 milieuMonde = rt.TransformPoint(new Vector3(centreFace.x, y, centreFace.z));
    Vector3 intMonde = (rt.rotation * nInt).normalized;

    Vector3 depart = milieuMonde + intMonde * 0.8f;
    var hits = Physics.RaycastAll(depart, -intMonde, 1.6f, ~0, QueryTriggerInteraction.Ignore);
    System.Array.Sort(hits, (x, y2) => x.distance.CompareTo(y2.distance));

    sb.AppendLine("  rayon depuis l'intérieur (" + depart.x.ToString("F3") + ", " + depart.y.ToString("F3")
        + ", " + depart.z.ToString("F3") + ") vers la face :");

    if (hits.Length == 0) { sb.AppendLine("      ⚠ RIEN touché — la bande ne barre pas la face."); }

    foreach (var h in hits)
    {
        float mm = h.distance * 1000f / U;
        sb.AppendLine("      " + h.collider.name + " (" + h.collider.GetType().Name + ") à "
            + h.distance.ToString("F4") + " u = " + mm.ToString("F2") + " mm"
            + "   normale (" + h.normal.x.ToString("F2") + ", " + h.normal.y.ToString("F2")
            + ", " + h.normal.z.ToString("F2") + ")");
    }

    sb.AppendLine();
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

// --- contrôle de symétrie : la table doit être miroir ------------------------------------
sb.AppendLine("=== symétrie gauche / droite ===");

var g = Trouver(rt, "Slingshot_Left");
var d = Trouver(rt, "Slingshot_Right");

if (g != null && d != null)
{
    Vector3 lg = rt.InverseTransformPoint(g.position);
    Vector3 ld = rt.InverseTransformPoint(d.position);

    sb.AppendLine("  gauche (" + lg.x.ToString("F4") + ", " + lg.y.ToString("F4") + ", " + lg.z.ToString("F4") + ")");
    sb.AppendLine("  droite (" + ld.x.ToString("F4") + ", " + ld.y.ToString("F4") + ", " + ld.z.ToString("F4") + ")");

    float ex = Mathf.Abs(lg.x + ld.x);
    float ey = Mathf.Abs(lg.y - ld.y);
    float ez = Mathf.Abs(lg.z - ld.z);

    sb.AppendLine("  écart miroir : " + (ex * 1000f / U).ToString("F4") + " / "
        + (ey * 1000f / U).ToString("F4") + " / " + (ez * 1000f / U).ToString("F4") + " mm"
        + (ex < 1e-4f && ey < 1e-4f && ez < 1e-4f ? "   ✓ miroir" : "   ⚠ NON miroir"));

    var cg = g.GetComponent<BoxCollider>();
    var cd = d.GetComponent<BoxCollider>();

    if (cg != null && cd != null)
    {
        sb.AppendLine("  colliders : gauche taille " + cg.size.ToString("F4")
            + "   droite taille " + cd.size.ToString("F4")
            + (Vector3.Distance(cg.size, cd.size) < 1e-5f ? "   ✓ identiques" : "   ⚠ différentes"));
    }
}
else
{
    sb.AppendLine("  un des deux hôtes est absent.");
}

sb.AppendLine();
sb.AppendLine("scène modifiée (Ctrl+Z annule, Ctrl+S conserve).");

return sb.ToString();
