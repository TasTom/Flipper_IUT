// Sonde : geometrie REELLE du bas du couloir de lancement.
//
// LECTURE SEULE : ne modifie aucun GameObject, aucun composant, aucun asset ; n'enregistre rien ;
// n'entre pas en Play mode.
//
// Mesure faite DEPUIS LE CENTRE DU COULOIR vers chaque paroi (jamais un balayage gauche->droite,
// qui ne releverait que les faces d'ENTREE des colliders). Releve : le profil du couloir, le z
// exact ou le sol et les parois s'arretent, la boite englobante reelle de la table (calculee en
// transformant les HUIT COINS de chaque sharedMesh.bounds), ce qu'il y a sous le couloir, et les
// objets dont le nom contient Plunger ou Lane.
//
//   unity command eval_file D:/Flipper_IUT/Tools/unity/probe_plunger_lane.cs

var sb = new System.Text.StringBuilder();

Physics.SyncTransforms();

float  XC   = 4.670f;            // axe du couloir de lancement
float  YTIR = 0.2168f + 0.225f;  // centre d'une bille posee sur le sol du couloir : 0,4418
float  DIAM = 0.45f;             // diametre de la bille
int    MASQUE = ~0;

sb.AppendLine("=== sonde couloir de lancement (lecture seule) ===");
sb.AppendLine("axe du couloir x = " + XC.ToString("F4")
              + "   hauteur de tir y = " + YTIR.ToString("F4")
              + "   bille = " + DIAM.ToString("F2") + " de diametre");
sb.AppendLine("portees : 3 en x, 6 vers le bas (depuis y " + YTIR.ToString("F4")
              + " jusqu'a y " + (YTIR - 6f).ToString("F2") + ")");
sb.AppendLine("Physics.queriesHitTriggers = " + Physics.queriesHitTriggers
              + "   (les requetes ci-dessous IGNORENT les declencheurs)");

// --------------------------------------------------------------------------- a) sections du couloir

float[] zs = { 1.6f, 1.4f, 1.2f, 1.0f, 0.8f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f, 0.1f, 0.0f, -0.2f, -0.5f, -0.8f };

sb.AppendLine();
sb.AppendLine("=== a) sections du couloir ===");
sb.AppendLine("z | x_gauche (objet) | x_droite (objet) | largeur | y_sol (objet) | y_sol - attendu 7 deg");

for (int i = 0; i < zs.Length; i++)
{
    float z = zs[i];
    Vector3 o = new Vector3(XC, YTIR, z);

    RaycastHit hl, hr, hd;

    bool bl = Physics.Raycast(o, Vector3.left,  out hl, 3f, MASQUE, QueryTriggerInteraction.Ignore);
    bool br = Physics.Raycast(o, Vector3.right, out hr, 3f, MASQUE, QueryTriggerInteraction.Ignore);
    bool bd = Physics.Raycast(o, Vector3.down,  out hd, 6f, MASQUE, QueryTriggerInteraction.Ignore);

    string g   = bl ? hl.point.x.ToString("F4") + " (" + hl.collider.gameObject.name + ")" : "-";
    string d   = br ? hr.point.x.ToString("F4") + " (" + hr.collider.gameObject.name + ")" : "-";
    string lar = (bl && br) ? (hr.point.x - hl.point.x).ToString("F4") : "-";

    float attendu = 0.2162f + (z - 0.94f) * 0.12278f;  // pente 7 deg depuis le point d'apparition
    string sol = bd
        ? hd.point.y.ToString("F4") + " (" + hd.collider.gameObject.name + ")   ecart "
          + (hd.point.y - attendu).ToString("F4")
        : "- AUCUN SOL -";

    sb.AppendLine(z.ToString("F2") + " | " + g + " | " + d + " | " + lar + " | " + sol);
}

// --------------------------------------------------- b) et c) balayage fin au 0,01 (sol / parois)

float zHaut = 1.70f;
int   nFin  = 281;

float[]  fz    = new float[nFin];
bool[]   fsol  = new bool[nFin];
bool[]   fg    = new bool[nFin];
bool[]   fd    = new bool[nFin];
float[]  fxg   = new float[nFin];
float[]  fxd   = new float[nFin];
float[]  fys   = new float[nFin];
string[] fsn   = new string[nFin];

for (int i = 0; i < nFin; i++)
{
    float z = zHaut - i * 0.01f;
    fz[i] = z;

    Vector3 o = new Vector3(XC, YTIR, z);

    RaycastHit hl, hr, hd;

    // sol du couloir : une surface a hauteur de table sous le point de tir
    if (Physics.Raycast(o, Vector3.down, out hd, 6f, MASQUE, QueryTriggerInteraction.Ignore))
    {
        fys[i] = hd.point.y;
        fsn[i] = hd.collider.gameObject.name;
        fsol[i] = hd.point.y > 0.05f;
    }
    else
    {
        fys[i] = float.NaN;
        fsn[i] = "-";
        fsol[i] = false;
    }

    // paroi gauche : une surface dans une fenetre de 0,60 u a gauche de l'axe
    fg[i] = false;
    fxg[i] = float.NaN;
    if (Physics.Raycast(o, Vector3.left, out hl, 3f, MASQUE, QueryTriggerInteraction.Ignore))
    {
        fxg[i] = hl.point.x;
        fg[i] = hl.point.x > XC - 0.60f && hl.point.x < XC - 0.03f;
    }

    // paroi droite : symetrique
    fd[i] = false;
    fxd[i] = float.NaN;
    if (Physics.Raycast(o, Vector3.right, out hr, 3f, MASQUE, QueryTriggerInteraction.Ignore))
    {
        fxd[i] = hr.point.x;
        fd[i] = hr.point.x > XC + 0.03f && hr.point.x < XC + 0.60f;
    }
}

sb.AppendLine();
sb.AppendLine("=== b/c) transitions au 0,01 de z = " + zHaut.ToString("F2") + " a "
              + fz[nFin - 1].ToString("F2") + " ===");
sb.AppendLine("(paroi = surface relevee dans la fenetre +-0,60 u autour de l'axe)");

int transitions = 0;

for (int i = 1; i < nFin; i++)
{
    if (fsol[i] != fsol[i - 1])
    {
        transitions++;
        sb.AppendLine("  SOL     : " + (fsol[i - 1] ? "present" : "absent") + " a z " + fz[i - 1].ToString("F2")
                      + " (y " + fys[i - 1].ToString("F4") + ", " + fsn[i - 1] + ")"
                      + "  ->  " + (fsol[i] ? "present" : "absent") + " a z " + fz[i].ToString("F2")
                      + "     => dernier z AVEC sol = " + fz[i - 1].ToString("F2")
                      + " ; premier z SANS sol = " + fz[i].ToString("F2"));
    }

    if (fg[i] != fg[i - 1])
    {
        transitions++;
        sb.AppendLine("  PAROI G : " + (fg[i - 1] ? "presente" : "absente") + " a z " + fz[i - 1].ToString("F2")
                      + "  ->  " + (fg[i] ? "presente" : "absente") + " a z " + fz[i].ToString("F2")
                      + "     => dernier z AVEC paroi gauche = " + fz[i - 1].ToString("F2"));
    }

    if (fd[i] != fd[i - 1])
    {
        transitions++;
        sb.AppendLine("  PAROI D : " + (fd[i - 1] ? "presente" : "absente") + " a z " + fz[i - 1].ToString("F2")
                      + "  ->  " + (fd[i] ? "presente" : "absente") + " a z " + fz[i].ToString("F2")
                      + "     => dernier z AVEC paroi droite = " + fz[i - 1].ToString("F2"));
    }
}

sb.AppendLine("  nombre de transitions : " + transitions);

// table compacte au 0,10, plus une fenetre autour de la derniere transition
sb.AppendLine();
sb.AppendLine("--- extrait (1 ligne sur 10, soit 0,10) ---");
sb.AppendLine("z | sol y (objet) | gauche x | droite x | largeur");

for (int i = 0; i < nFin; i += 10)
{
    AppendLigne(sb, fz[i], fsol[i], fys[i], fsn[i], fg[i], fxg[i], fd[i], fxd[i]);
}

sb.AppendLine();
sb.AppendLine("--- fenetre 0,25 -> 0,40 au 0,01 (bord bas du couloir) ---");
sb.AppendLine("z | sol y (objet) | gauche x | droite x | largeur");

for (int i = 0; i < nFin; i++)
{
    if (fz[i] <= 0.40f && fz[i] >= 0.25f)
    {
        AppendLigne(sb, fz[i], fsol[i], fys[i], fsn[i], fg[i], fxg[i], fd[i], fxd[i]);
    }
}

// ------------------------------------------------------------------ f) largeur franchissable ?

sb.AppendLine();
sb.AppendLine("=== f) largeur entre parois vs bille (0,45) ===");

int passe = 0, bloque = 0, indetermine = 0;
float largeurMin = float.MaxValue, largeurMax = float.MinValue;
float zPasseMin = float.MaxValue;

for (int i = 0; i < nFin; i++)
{
    if (!fsol[i]) { continue; }

    if (fg[i] && fd[i])
    {
        float lar = fxd[i] - fxg[i];

        if (lar < largeurMin) { largeurMin = lar; }
        if (lar > largeurMax) { largeurMax = lar; }

        if (lar > DIAM) { passe++; if (fz[i] < zPasseMin) { zPasseMin = fz[i]; } }
        else            { bloque++; }
    }
    else
    {
        indetermine++;
    }
}

sb.AppendLine("  z avec sol          : " + (passe + bloque + indetermine));
sb.AppendLine("  largeur > 0,45      : " + passe + " z");
sb.AppendLine("  largeur <= 0,45     : " + bloque + " z");
sb.AppendLine("  largeur indeterminee: " + indetermine + " z");
sb.AppendLine("  largeur min         : " + (largeurMin == float.MaxValue ? "-" : largeurMin.ToString("F4")));
sb.AppendLine("  largeur max         : " + (largeurMax == float.MinValue ? "-" : largeurMax.ToString("F4")));
sb.AppendLine("  rapport min a 0,45  : " + (largeurMin == float.MaxValue ? "-" : (largeurMin / DIAM).ToString("F3")));
sb.AppendLine("  dernier z qui passe : " + (zPasseMin == float.MaxValue ? "-" : zPasseMin.ToString("F2")));

// ------------------------------------------------------------------ d) boite englobante de la table

sb.AppendLine();
sb.AppendLine("=== d) boite englobante de PinballTable/Table/Pinball_Table (MONDE) ===");

Transform tableRacine = GameObject.Find("PinballTable") != null
    ? Trouver(GameObject.Find("PinballTable").transform, "Pinball_Table")
    : null;

if (tableRacine == null)
{
    sb.AppendLine("  Pinball_Table INTROUVABLE");
}
else
{
    sb.AppendLine("  position monde " + tableRacine.position.ToString("F4")
                  + "   locale " + tableRacine.localPosition.ToString("F4")
                  + "   rotation monde " + tableRacine.eulerAngles.ToString("F3")
                  + "   echelle " + tableRacine.lossyScale.ToString("F4"));

    Boite(sb, tableRacine, "Pinball_Table (41 maillages attendus, monde)");

    Transform conteneur = tableRacine.parent;

    if (conteneur != null)
    {
        Boite(sb, conteneur, "conteneur " + conteneur.name + " (monde)");
    }
}

// --- les trois maillages du couloir, mesure directe -------------------------------------------

sb.AppendLine();
sb.AppendLine("=== h) maillages du couloir : boite monde et etendue en z ===");

string[] nomsCouloir = { "LaneFloor", "Divider", "LaneOuter" };

for (int i = 0; i < nomsCouloir.Length; i++)
{
    Transform t = Trouver(tableRacine, nomsCouloir[i]);

    if (t == null) { sb.AppendLine("  " + nomsCouloir[i] + " INTROUVABLE"); continue; }

    Boite(sb, t, nomsCouloir[i] + "  [" + Chemin(t) + "]");
}

// --------------------------------------------------------------------------- e) sous le couloir

sb.AppendLine();
sb.AppendLine("=== e) ce qu'il y a SOUS le couloir (tir vers le bas depuis y -0,5, portee 15) ===");

float[] zsBas = { 0.3f, 1.0f };

for (int i = 0; i < zsBas.Length; i++)
{
    Vector3 o = new Vector3(XC, -0.5f, zsBas[i]);

    RaycastHit h;

    bool b = Physics.Raycast(o, Vector3.down, out h, 15f, MASQUE, QueryTriggerInteraction.Ignore);

    sb.AppendLine("  depuis (x " + XC.ToString("F3") + ", y -0.5000, z " + zsBas[i].ToString("F2") + ") : "
                  + (b ? "y " + h.point.y.ToString("F4") + "   x " + h.point.x.ToString("F4")
                         + "   z " + h.point.z.ToString("F4")
                         + "   objet " + h.collider.gameObject.name
                         + "   type " + h.collider.GetType().Name
                         : "AUCUN COLLIDER DANS 15 u"));
}

// --- toutes les billes de la scene -----------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== i) objets dont le nom contient Ball (scene active) ===");

UnityEngine.SceneManagement.Scene sceneActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
GameObject[] tous = Resources.FindObjectsOfTypeAll<GameObject>();
System.Collections.Generic.HashSet<GameObject> vus = new System.Collections.Generic.HashSet<GameObject>();

for (int i = 0; i < tous.Length; i++)
{
    GameObject go = tous[i];

    if (go == null) { continue; }
    if (!go.scene.IsValid() || go.scene != sceneActive) { continue; }
    if (go.name.IndexOf("Ball", System.StringComparison.OrdinalIgnoreCase) < 0) { continue; }
    if (!vus.Add(go)) { continue; }

    Rigidbody rb = go.GetComponent<Rigidbody>();
    Collider col = go.GetComponent<Collider>();

    sb.AppendLine("  " + go.name
                  + " | chemin " + Chemin(go.transform)
                  + " | monde " + go.transform.position.ToString("F4")
                  + " | actifSelf " + go.activeSelf
                  + " | actifHierarchie " + go.activeInHierarchy
                  + " | echelle " + go.transform.lossyScale.ToString("F4")
                  + " | collider " + (col == null ? "aucun" : col.GetType().Name + " decl " + col.isTrigger)
                  + " | rb " + (rb == null ? "aucun" : "kinematique " + rb.isKinematic + " detectCollisions " + rb.detectCollisions));
}

// --------------------------------------------------------------------------- g) objets Plunger / Lane

sb.AppendLine();
sb.AppendLine("=== g) objets dont le nom contient Plunger ou Lane (scene active) ===");

sb.AppendLine("  scene active : " + sceneActive.name + "   chargee " + sceneActive.isLoaded);

int nTrouves = 0;
int nHorsScene = 0;
System.Collections.Generic.HashSet<GameObject> vus2 = new System.Collections.Generic.HashSet<GameObject>();

for (int i = 0; i < tous.Length; i++)
{
    GameObject go = tous[i];

    if (go == null) { continue; }

    bool nomVise = go.name.IndexOf("Plunger", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || go.name.IndexOf("Lane", System.StringComparison.OrdinalIgnoreCase) >= 0;

    if (!nomVise) { continue; }

    if (!go.scene.IsValid() || go.scene != sceneActive) { nHorsScene++; continue; }

    if (!vus2.Add(go)) { continue; }

    nTrouves++;

    sb.AppendLine("  " + go.name
                  + " | chemin " + Chemin(go.transform)
                  + " | monde " + go.transform.position.ToString("F4")
                  + " | locale " + go.transform.localPosition.ToString("F4")
                  + " | actifSelf " + go.activeSelf
                  + " | actifHierarchie " + go.activeInHierarchy
                  + " | colliders " + go.GetComponents<Collider>().Length
                  + " | meshfilters " + go.GetComponents<MeshFilter>().Length);
}

sb.AppendLine("  total scene active " + nTrouves + "   hors scene active (prefabs/assets) " + nHorsScene);

// -------------------------------------------------- j) bord bas du couloir affine au 0,001

sb.AppendLine();
sb.AppendLine("=== j) bord bas du couloir affine au 0,001 (3 hauteurs de tir) ===");
sb.AppendLine("hauteurs : 0,4418 (centre de bille) | 0,2600 (juste au-dessus du sol) | 0,1000 (sous le sol)");
sb.AppendLine("seules les lignes ou l'un des quatre resultats CHANGE sont imprimees");
sb.AppendLine("z | sol a 0,4418 | sol a 0,2600 | paroi G a 0,1000 | paroi D a 0,1000");

bool p1 = false, p2 = false, pg = false, pd2 = false;
int  imprimees = 0;

for (int i = 0; i <= 80; i++)
{
    float z = 0.360f - i * 0.001f;

    RaycastHit h1, h2, hg, hd2;

    bool b1 = Physics.Raycast(new Vector3(XC, 0.4418f, z), Vector3.down, out h1, 6f, MASQUE, QueryTriggerInteraction.Ignore)
              && h1.point.y > 0.05f;
    bool b2 = Physics.Raycast(new Vector3(XC, 0.2600f, z), Vector3.down, out h2, 2f, MASQUE, QueryTriggerInteraction.Ignore);
    bool bg = Physics.Raycast(new Vector3(XC, 0.1000f, z), Vector3.left,  out hg,  3f, MASQUE, QueryTriggerInteraction.Ignore);
    bool bd2 = Physics.Raycast(new Vector3(XC, 0.1000f, z), Vector3.right, out hd2, 3f, MASQUE, QueryTriggerInteraction.Ignore);

    if (i == 0 || b1 != p1 || b2 != p2 || bg != pg || bd2 != pd2)
    {
        sb.AppendLine(z.ToString("F3")
                      + " | " + (b1 ? h1.point.y.ToString("F4") : "AUCUN")
                      + " | " + (b2 ? h2.point.y.ToString("F4") + " (" + h2.collider.gameObject.name + ")" : "AUCUN")
                      + " | " + (bg ? hg.point.x.ToString("F4") + " (" + hg.collider.gameObject.name + ")" : "rien dans 3")
                      + " | " + (bd2 ? hd2.point.x.ToString("F4") + " (" + hd2.collider.gameObject.name + ")" : "rien dans 3"));
        imprimees++;
    }

    p1 = b1; p2 = b2; pg = bg; pd2 = bd2;
}

sb.AppendLine("  lignes imprimees : " + imprimees);

// ------------------------------------------- k) profil du bord bas : ou s'arrete chaque paroi, par hauteur

sb.AppendLine();
sb.AppendLine("=== k) bord bas du couloir par hauteur de tir ===");
sb.AppendLine("balayage z de 0,600 a 0,200 au 0,001 ; on retient le DERNIER z ou chaque rayon touche");
sb.AppendLine("y tir | dernier z sol (y sol) | dernier z paroi G (x, objet) | dernier z paroi D (x, objet)");

float[] hs = { 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.35f, 0.40f, 0.4418f, 0.50f, 0.60f };

for (int k = 0; k < hs.Length; k++)
{
    float h = hs[k];

    float dernierSol = float.NaN, dernierG = float.NaN, dernierD = float.NaN;
    float ySol = float.NaN, xG = float.NaN, xD = float.NaN;
    string nomSol = "-", nomG = "-", nomD = "-";

    for (int i = 0; i <= 400; i++)
    {
        float z = 0.600f - i * 0.001f;
        Vector3 o = new Vector3(XC, h, z);

        RaycastHit hd, hl, hr;

        if (Physics.Raycast(o, Vector3.down, out hd, 2f, MASQUE, QueryTriggerInteraction.Ignore) && hd.point.y > 0.05f)
        {
            dernierSol = z; ySol = hd.point.y; nomSol = hd.collider.gameObject.name;
        }

        if (Physics.Raycast(o, Vector3.left, out hl, 3f, MASQUE, QueryTriggerInteraction.Ignore))
        {
            dernierG = z; xG = hl.point.x; nomG = hl.collider.gameObject.name;
        }

        if (Physics.Raycast(o, Vector3.right, out hr, 3f, MASQUE, QueryTriggerInteraction.Ignore))
        {
            dernierD = z; xD = hr.point.x; nomD = hr.collider.gameObject.name;
        }
    }

    sb.AppendLine(h.ToString("F4")
                  + " | " + (float.IsNaN(dernierSol) ? "AUCUN" : dernierSol.ToString("F3") + " (y " + ySol.ToString("F4") + ", " + nomSol + ")")
                  + " | " + (float.IsNaN(dernierG) ? "AUCUNE" : dernierG.ToString("F3") + " (x " + xG.ToString("F4") + ", " + nomG + ")")
                  + " | " + (float.IsNaN(dernierD) ? "AUCUNE" : dernierD.ToString("F3") + " (x " + xD.ToString("F4") + ", " + nomD + ")"));
}

return sb.ToString();

// ---------------------------------------------------------------------------------- helpers

Transform Trouver(Transform parent, string nom)
{
    if (parent == null) { return null; }
    if (parent.name == nom) { return parent; }

    foreach (Transform enfant in parent)
    {
        Transform t = Trouver(enfant, nom);

        if (t != null) { return t; }
    }

    return null;
}

string Chemin(Transform t)
{
    string s = t.name;
    Transform p = t.parent;

    while (p != null) { s = p.name + "/" + s; p = p.parent; }

    return s;
}

void AppendLigne(System.Text.StringBuilder b, float z, bool sol, float y, string nom,
                 bool g, float xg, bool d, float xd)
{
    b.AppendLine(z.ToString("F2")
                 + " | " + (sol ? y.ToString("F4") + " (" + nom + ")" : "AUCUN")
                 + " | " + (float.IsNaN(xg) ? "rien" : xg.ToString("F4") + (g ? "" : " hors fenetre"))
                 + " | " + (float.IsNaN(xd) ? "rien" : xd.ToString("F4") + (d ? "" : " hors fenetre"))
                 + " | " + (g && d ? (xd - xg).ToString("F4") : "-"));
}

// boite englobante : les HUIT COINS de chaque sharedMesh.bounds, transformes en monde
void Boite(System.Text.StringBuilder b, Transform racine, string titre)
{
    if (racine == null) { b.AppendLine("  " + titre + " : INTROUVABLE"); return; }

    MeshFilter[] mfs = racine.GetComponentsInChildren<MeshFilter>(true);

    bool    init  = false;
    Vector3 mn    = Vector3.zero;
    Vector3 mx    = Vector3.zero;
    int     nMesh = 0;
    int     nVert = 0;

    for (int i = 0; i < mfs.Length; i++)
    {
        Mesh m = mfs[i].sharedMesh;

        if (m == null) { continue; }

        nMesh++;
        nVert += m.vertexCount;

        Bounds    bb  = m.bounds;
        Matrix4x4 mat = mfs[i].transform.localToWorldMatrix;

        for (int k = 0; k < 8; k++)
        {
            Vector3 coin = new Vector3(
                (k & 1) == 0 ? bb.min.x : bb.max.x,
                (k & 2) == 0 ? bb.min.y : bb.max.y,
                (k & 4) == 0 ? bb.min.z : bb.max.z);

            Vector3 w = mat.MultiplyPoint3x4(coin);

            if (!init) { mn = w; mx = w; init = true; }
            else { mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w); }
        }
    }

    b.AppendLine("  " + titre + " : " + nMesh + " maillages, " + nVert + " sommets");
    b.AppendLine("     min.x " + mn.x.ToString("F4") + "  max.x " + mx.x.ToString("F4")
                 + "   largeur " + (mx.x - mn.x).ToString("F4"));
    b.AppendLine("     min.y " + mn.y.ToString("F4") + "  max.y " + mx.y.ToString("F4")
                 + "   hauteur " + (mx.y - mn.y).ToString("F4"));
    b.AppendLine("     min.z " + mn.z.ToString("F4") + "  max.z " + mx.z.ToString("F4")
                 + "   longueur " + (mx.z - mn.z).ToString("F4"));
}
