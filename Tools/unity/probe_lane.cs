// Mesure du couloir de lancement, du bouchon et du plateau. Lecture seule.
//
// Trois questions, dans l'ordre :
//
//   1. **Où repose la bille, exactement, et contre quoi ?** Le couloir est plus large que le
//      bouchon : l'écart entre les deux forme une encoche concave de chaque côté. Une bille qui
//      s'y engage prend un contact d'arête au lieu d'un contact de face — et c'est précisément
//      ce que le générateur de table s'interdit (« aucun coin concave », cf. CLAUDE.md).
//   2. **Le bouchon, ramené en axes de l'hôte.** C'est le repère dans lequel sa taille a un sens ;
//      une boîte alignée sur le monde étale une cote fausse et la noie.
//   3. **Le plateau, ramené en repère du root.** C'est le rectangle à cadrer pour la caméra.
//
// Les rayons partent du centre de la bille : ils la toucheraient elle-même. On les filtre donc
// sur le collider de la bille plutôt que d'aller chercher un masque.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "'PinballTable' introuvable dans la scène."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var hote = gameplay != null ? gameplay.Find("Plunger") : null;
var bille = gameplay != null ? gameplay.Find("Ball") : null;
var piece = hote != null ? hote.Find("Plunger_Rod") : null;

Physics.SyncTransforms();

// --- helpers -------------------------------------------------------------------------------------

// Les huit coins d'une boîte locale, en coordonnées locales. Jamais centre × échelle.
Vector3[] Coins(Vector3 centre, Vector3 taille)
{
    var coins = new Vector3[8];
    int i = 0;

    for (int x = -1; x <= 1; x += 2)
    for (int y = -1; y <= 1; y += 2)
    for (int z = -1; z <= 1; z += 2)
    {
        coins[i++] = centre + Vector3.Scale(taille * 0.5f, new Vector3(x, y, z));
    }

    return coins;
}

// Premier objet touché par un rayon, en ignorant un collider donné.
bool Rayon(Vector3 origine, Vector3 direction, float portee, Collider ignorer,
           out RaycastHit touche)
{
    touche = default(RaycastHit);
    float mieux = float.MaxValue;
    bool trouve = false;

    foreach (var h in Physics.RaycastAll(origine, direction, portee, ~0, QueryTriggerInteraction.Ignore))
    {
        if (ignorer != null && h.collider == ignorer) { continue; }
        if (h.distance >= mieux) { continue; }

        mieux = h.distance;
        touche = h;
        trouve = true;
    }

    return trouve;
}

// --- 1. la bille et le couloir -------------------------------------------------------------------

sb.AppendLine("=== 1. la bille et le couloir ===");

Collider colBille = null;
Vector3 centreBille = Vector3.zero;
float rayon = 0f;

if (bille == null)
{
    sb.AppendLine("  aucune bille sous Gameplay.");
}
else
{
    colBille = bille.GetComponent<Collider>();

    if (colBille == null)
    {
        sb.AppendLine("  la bille n'a pas de collider.");
    }
    else
    {
        centreBille = colBille.bounds.center;
        rayon = colBille.bounds.extents.x;

        sb.AppendLine("  centre   monde " + centreBille.ToString("F4")
                      + "   local " + bille.localPosition.ToString("F4"));
        sb.AppendLine("  rayon    " + rayon.ToString("F4")
                      + "   (diamètre " + (rayon * 2f).ToString("F4") + " u)");

        // Largeur du couloir. L'inclinaison de la table ne porte que sur X, donc la largeur du
        // couloir est exactement l'axe X du monde : pas de repère à convertir ici.
        RaycastHit hg, hd;

        bool gauche = Rayon(centreBille, Vector3.left, 3f, colBille, out hg);
        bool droite = Rayon(centreBille, Vector3.right, 3f, colBille, out hd);

        sb.AppendLine();
        sb.AppendLine("  parois, depuis le centre de la bille :");
        sb.AppendLine("    gauche  " + (gauche
            ? hg.distance.ToString("F4") + "   " + hg.collider.name
            : "aucune"));
        sb.AppendLine("    droite  " + (droite
            ? hd.distance.ToString("F4") + "   " + hd.collider.name
            : "aucune"));

        if (gauche && droite)
        {
            float largeur = hg.distance + hd.distance;

            sb.AppendLine("    largeur du couloir " + largeur.ToString("F4") + " u"
                          + "   = " + (largeur / (rayon * 2f)).ToString("F3") + " bille(s)");

            // Le jeu latéral : de combien la bille peut se décaler avant de toucher une paroi.
            sb.AppendLine("    jeu latéral total " + (largeur - rayon * 2f).ToString("F4") + " u");

            float versGauche = hg.distance;
            float versDroite = hd.distance;

            sb.AppendLine("    la bille est à " + versGauche.ToString("F4") + " de la paroi gauche"
                          + " et " + versDroite.ToString("F4") + " de la droite");

            if (Mathf.Abs(versGauche - rayon) < 0.005f)
            {
                sb.AppendLine("    → elle TOUCHE la paroi gauche");
            }

            if (Mathf.Abs(versDroite - rayon) < 0.005f)
            {
                sb.AppendLine("    → elle TOUCHE la paroi droite");
            }
        }

        // Sol du couloir.
        RaycastHit hsol;

        if (Rayon(centreBille, Vector3.down, 3f, colBille, out hsol))
        {
            sb.AppendLine();
            sb.AppendLine("  sol sous la bille : " + hsol.distance.ToString("F4")
                          + "   " + hsol.collider.name
                          + "   (bille posée si ≈ " + rayon.ToString("F4") + ")");
        }

        // Le long de l'axe du couloir : ce qu'il y a devant (vers le haut) et derrière (le bouchon).
        if (hote != null)
        {
            RaycastHit hav, har;

            bool avant = Rayon(centreBille, hote.forward, 20f, colBille, out hav);
            bool arriere = Rayon(centreBille, -hote.forward, 3f, colBille, out har);

            sb.AppendLine();
            sb.AppendLine("  le long de l'axe du couloir :");
            sb.AppendLine("    devant (+Z) " + (avant
                ? hav.distance.ToString("F4") + "   " + hav.collider.name
                : "rien sur 20 u"));
            sb.AppendLine("    derrière    " + (arriere
                ? har.distance.ToString("F4") + "   " + har.collider.name
                : "rien sur 3 u"));

            // Position de la bille sur l'axe du lanceur, depuis sa position de repos.
            float avance = Vector3.Dot(centreBille - hote.position, hote.forward);

            sb.AppendLine("    avance depuis le repos du lanceur : " + avance.ToString("F4") + " u");
        }
    }
}

// --- 2. le bouchon, en axes de l'hôte ------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 2. le bouchon (collider de la pièce) ===");

if (piece == null)
{
    sb.AppendLine("  'Plunger_Rod' absente sous 'Plunger'.");
}
else
{
    foreach (var c in piece.GetComponents<Collider>())
    {
        var boite = c as BoxCollider;

        sb.AppendLine("  " + c.GetType().Name
                      + "   déclencheur " + c.isTrigger
                      + "   matière " + (c.sharedMaterial != null ? c.sharedMaterial.name : "AUCUNE"));

        if (boite == null) { continue; }

        sb.AppendLine("    centre local de la pièce " + boite.center.ToString("F4"));
        sb.AppendLine("    taille locale de la pièce " + boite.size.ToString("F4"));
        sb.AppendLine("    échelle perdue de la pièce   " + piece.lossyScale.ToString("F6"));

        // Les huit coins, ramenés dans le repère de l'HÔTE : c'est là que la cote se lit.
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var coin in Coins(boite.center, boite.size))
        {
            var monde = piece.TransformPoint(coin);
            var local = hote.InverseTransformPoint(monde);

            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        sb.AppendLine("    en axes de l'hôte :");
        sb.AppendLine("      min        " + min.ToString("F4"));
        sb.AppendLine("      max        " + max.ToString("F4"));
        sb.AppendLine("      taille     " + (max - min).ToString("F4"));
        sb.AppendLine("      centre     " + ((min + max) * 0.5f).ToString("F4"));
        sb.AppendLine("      face avant " + max.z.ToString("F4") + "   (côté couloir)");
    }

    if (bille != null && colBille != null)
    {
        // L'encoche : l'écart latéral entre le bord du bouchon et la paroi du couloir.
        var boite = piece.GetComponent<BoxCollider>();

        if (boite != null)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var coin in Coins(boite.center, boite.size))
            {
                var local = hote.InverseTransformPoint(piece.TransformPoint(coin));
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            RaycastHit hg, hd;
            bool gauche = Rayon(centreBille, Vector3.left, 3f, colBille, out hg);
            bool droite = Rayon(centreBille, Vector3.right, 3f, colBille, out hd);

            if (gauche && droite)
            {
                // Repère monde → repère hôte pour comparer aux bords du bouchon.
                var bordGaucheMonde = hote.TransformPoint(new Vector3(min.x, 0f, 0f));
                var bordDroitMonde = hote.TransformPoint(new Vector3(max.x, 0f, 0f));

                float jeuGauche = Mathf.Abs(bordGaucheMonde.x - (centreBille.x - hg.distance));
                float jeuDroit = Mathf.Abs((centreBille.x + hd.distance) - bordDroitMonde.x);

                sb.AppendLine();
                sb.AppendLine("  encoche entre le bord du bouchon et la paroi :");
                sb.AppendLine("    largeur du bouchon " + (max.x - min.x).ToString("F4") + " u");
                sb.AppendLine("    jeu côté gauche    " + jeuGauche.ToString("F4") + " u");
                sb.AppendLine("    jeu côté droit     " + jeuDroit.ToString("F4") + " u");

                if (Mathf.Min(jeuGauche, jeuDroit) > 1e-4f)
                {
                    sb.AppendLine("    → il RESTE un coin concave des deux côtés ⚠");
                }
                else
                {
                    sb.AppendLine("    → le bouchon ferme le couloir de paroi à paroi ✓");
                }
            }
        }
    }
}

// --- 3. le plateau, en repère du root ------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 3. le plateau, en repère du root ===");

var table = rt.Find("Table");

if (table == null)
{
    sb.AppendLine("  'PinballTable/Table' introuvable.");
}
else
{
    foreach (Transform enfant in table)
    {
        var rendus = enfant.GetComponentsInChildren<MeshRenderer>(true);

        if (rendus.Length == 0)
        {
            sb.AppendLine("  " + enfant.name + " : aucun maillage");
            continue;
        }

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var r in rendus)
        {
            var b = r.bounds;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var coin = b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
                var local = rt.InverseTransformPoint(coin);

                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }

        sb.AppendLine("  " + enfant.name + " : " + rendus.Length + " maillages");
        sb.AppendLine("      min " + min.ToString("F3") + "   max " + max.ToString("F3")
                      + "   taille " + (max - min).ToString("F3"));
    }

    // Le maillage nommé « Playfield », s'il existe : c'est la surface de jeu elle-même.
    foreach (var r in table.GetComponentsInChildren<MeshRenderer>(true))
    {
        if (r.name != "Playfield") { continue; }

        var b = r.bounds;
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            var coin = b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
            var local = rt.InverseTransformPoint(coin);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        sb.AppendLine();
        sb.AppendLine("  PLAISEAU (« " + r.name + " ») en repère du root :");
        sb.AppendLine("      min " + min.ToString("F4") + "   max " + max.ToString("F4"));
        sb.AppendLine("      taille " + (max - min).ToString("F4"));
    }
}

return sb.ToString();
