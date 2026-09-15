"""Construit le plateau de Vosges Mania (GDD §Conception de la table).

Le dépôt `vbousquet/pinball-parts` ne contient **pas de table** : c'est un magasin de pièces.
Ce script génère donc le plateau lui-même, en procédural, à partir des dimensions réelles du
standard et de l'ancre d'échelle du projet.

**Ancre d'échelle.** Une bille de flipper fait 27,0 mm (1"1/16) ; la table Unity utilise une
bille de 0,45 unité de diamètre, soit 16,667 unités par mètre — même ancre que
`inventory_parts.py` et `export_parts.py`. Un plateau standard mesure 20,25" × 42", ce qui
donne 8,573 × 17,780 unités Unity. **C'est le sens de l'ancre** : les cotes réelles, prises en
mètres, se transposent telles quelles. Un plateau plus étroit que le standard laisserait la
bille — qui est, elle, à l'échelle réelle — trop grosse pour la table ; c'est le défaut de la
table procédurale abandonnée, dont le plateau de 0,417 m était 24 % trop étroit.

**Repère de travail.** Tout le script raisonne dans le repère de la table, identique à celui
d'Unity :

    x : + à droite      y : + vers le haut (hors du plateau)      z : + vers le haut de la table

`to_blender` le convertit vers Blender (Z vers le haut) : `(x, y, z) -> (-x, -z, y)`.

**Pourquoi le `-x`.** Il n'est pas décoratif : il compense une réflexion mesurée chez Unity.
Sans lui, la table ressort miroitée gauche-droite — le chenal de lancement, conçu à droite,
arrive à gauche, à l'inverse de la convention du projet. Le raisonnement naïf (« le couple
d'axes de l'exportateur défait la conversion ») est faux, et `Tools/blender/probe_axis.py` le
prouve par les octets :

- Le couple `axis_forward` / `axis_up` **ne touche pas aux sommets** : il n'est que *déclaré*
  dans `GlobalSettings`, et les quatre couples essayés écrivent exactement les mêmes
  coordonnées, en repère Blender. Les quatre repères déclarés ont d'ailleurs un déterminant
  +1 : aucun n'est une réflexion. Changer le couple ne peut donc rien miroiter — et mesuré
  dans Unity, il ne change effectivement rien.
- Unity applique sa **propre** conversion, fixe, sans lire `GlobalSettings` :
  `blender (x, y, z) -> unity (-x, z, -y)`. Sa composition avec `to_blender` donnait
  `table (x, y, z) -> unity (-x, y, z)` : le miroir observé, sur deux relevés indépendants
  (l'aire de jeu et les cibles, toutes deux inversées en x, `y` et `z` intacts).

Le `-x` annule ce premier `-x`, et la composition redevient l'identité. Il rend en revanche la
géométrie **miroir** dans Blender — donc le sens des faces tel qu'écrit est retourné. C'est sans
conséquence : `recalc_face_normals` recalcule chaque solide, et son volume signé est vérifié.

**Le plateau est construit à plat.** L'inclinaison de 7° est posée dans Unity, sur le root
`PinballTable` (voir `TableGravity.cs`) : c'est une rotation de la table entière, pas une
propriété de sa géométrie.

**Deux fichiers, deux usages.** Le plateau et le caisson sont exportés séparément, parce qu'ils
n'appellent pas le même traitement dans Unity :

    Pinball_Table.fbx     ce que la bille touche  -> MeshCollider
    Pinball_Cabinet.fbx   décor                   -> AUCUN collider

Un collider oublié sur le décor casse la physique validée de la table : les séparer rend la
règle applicable telle quelle.

**Pièges de conception, appris à la dure.** La table procédurale précédente bloquait la bille
dans un coin : un disque et un rail formant un V, équilibre stable dans les trois axes. Quatre
règles en découlent, appliquées ici :

1. Deux murs ne se chevauchent jamais — leur recouvrement crée un coin interne qui coince.
2. Aucun cul-de-sac en V : tout coin concave est rempli d'un congé, tout bout de mur est assez
   dégagé pour que la bille ne s'y engage pas.
3. Aucun passage large d'environ une bille : un intervalle offert à la bille est soit franchement
   plus étroit que son diamètre (0,027 — elle n'y entre pas), soit franchement plus large
   (≥ 0,036 — elle y circule et en ressort). Entre les deux, elle s'engage et se coince.
4. Tout mur qui retient la bille dépasse son rayon (0,0135) ; en deçà, elle passe par-dessus.

Les normales ne sont pas supposées justes : chaque solide est recalculé par
`bmesh.ops.recalc_face_normals`, et son volume signé est reporté — un volume négatif signale un
solide retourné.

    blender --background --factory-startup --python Tools/blender/build_table.py -- \
        --out Assets/Models/Table --blend Tools/blender/table.blend --verify
"""

import argparse
import math
import mathutils
import os
import sys

import bmesh
import bpy

# --------------------------------------------------------------------------- ancres

BALL_D = 0.027                      # 1"1/16, diamètre réel d'une bille de flipper
UNITY_BALL_DIAMETER = 0.45
UNITS_PER_METER = UNITY_BALL_DIAMETER / BALL_D          # ≈ 16,667
BALL_R = BALL_D / 2.0               # 0,0135 — hauteur minimale d'un mur qui retient la bille

# --------------------------------------------------------------------------- cotes

PLAY_W = 0.51435                    # 20,25" — largeur du plateau entre murs
HALF_W = PLAY_W / 2.0               # 0,257175
PLAY_L = 1.06680                    # 42"    — longueur du plateau

WALL_T = 0.006                      # épaisseur des guides
WALL_H = 0.024                      # hauteur : 1,8 rayon de bille, elle ne saute pas
SLAB_T = 0.016                      # épaisseur du plateau

# Couloir de lancement, à droite, hors de l'aire de jeu. Largeurs mesurées entre FACES, pas
# entre axes : mesurer entre axes laisserait 0,021 pour une bille de 0,027, qui s'y coince.
LANE_W = 0.034
LANE_RAISE = 0.006                  # surélévation : la bille ne peut plus y redescendre
LANE_CX = HALF_W + WALL_T + LANE_W / 2.0            # 0,280175 — axe du lanceur

DIV_X0 = HALF_W                     # 0,257175 — face joueuse du mur de droite
DIV_X1 = HALF_W + WALL_T            # 0,263175
LANE_X0 = DIV_X1                    # 0,263175
LANE_X1 = LANE_X0 + LANE_W          # 0,297175
OWALL_X0 = LANE_X1
OWALL_X1 = LANE_X1 + WALL_T         # 0,303175

BOTTOM_Z = 0.020                    # bord bas : au-delà, la bille tombe et est perdue
TOP_Z = PLAY_L

# Congés des coins hauts. Celui de droite renvoie vers la gauche la bille qui monte le couloir ;
# celui de gauche renvoie vers le bas celle qui redescend de l'orbit. Le centre est posé à
# `mur - R`, si bien que l'arc part exactement de la face du mur et se termine sur le mur du
# fond : aucune discontinuité à rattraper.
R_FILLET_R = 0.060
FILLET_R_CX = OWALL_X0 - R_FILLET_R                 # 0,237175
FILLET_R_CZ = TOP_Z - R_FILLET_R                    # 1,0068

R_FILLET_L = 0.070
FILLET_L_CX = -HALF_W + R_FILLET_L                  # -0,187175
FILLET_L_CZ = TOP_Z - R_FILLET_L                    # 0,9968

# Orbit : la voie rapide du haut, entre le mur du fond et un rail intérieur.
ORBIT_W = 0.036                     # largeur du chenal (1,33 bille)
RAIL_Z1 = TOP_Z - ORBIT_W           # 1,0308 — face supérieure du rail
RAIL_Z0 = RAIL_Z1 - WALL_T          # 1,0248
RAIL_X0 = -0.180                    # fin gauche : la bille quitte l'orbit en la dépassant
RAIL_X1 = DIV_X0                    # le rail vient buter sur le mur de droite
R_CORNER = 0.030                    # congé du coin concave rail / mur de droite

# Flippers (3", comme la pièce `Flipper_Williams_3` du dépôt).
FLIP_LEN = 0.0762
FLIP_REST_DEG = 30.0
FLIP_PIVOT_X = 0.0857               # 3,375" du centre
FLIP_PIVOT_Z = 0.092
DRAIN_GAP = 2.0 * (FLIP_PIVOT_X - FLIP_LEN * math.cos(math.radians(FLIP_REST_DEG)))

# Slingshots. La face active est une diagonale unique qui part du mur et vient mourir au pied du
# flipper : la bille qui la descend est déposée sur la base du flipper, pas dans le drain. Le
# haut de la face laisse 0,044 avec le mur — de quoi rendre l'outlane franchissable.
SLING_TOP = (0.1925, 0.330)
SLING_BOT = (0.088, 0.085)
SLING_T = 0.022                     # épaisseur du corps, vers l'extérieur

# Bumpers : trois plots en triangle autour de l'emblème central (GDD §Bumpers).
BUMPER_R = 0.030
BUMPER_H = 0.006                    # simple socle : la jupe et le chapeau sont des pièces
BUMPERS = [(-0.085, 0.660), (0.085, 0.660), (0.000, 0.755)]

# Cibles relevées, groupées comme le demande le GDD §Cibles fixes — 16 cibles en cinq groupes.
TARGET_W = 0.028
TARGET_T = 0.008
TARGET_H = 0.024
TARGETS = []
for _i in range(4):                                  # Matières ×4, à gauche
    TARGETS.append(("Matieres", -0.200 + _i * 0.062, 0.430, "+z"))
for _i in range(3):                                  # Projet ×3, à droite
    TARGETS.append(("Projet", 0.045 + _i * 0.062, 0.430, "+z"))
for _i in range(3):                                  # Java ×3, à gauche
    TARGETS.append(("Java", -0.200 + _i * 0.062, 0.575, "+z"))
for _i in range(3):                                  # Café ×3, à droite
    TARGETS.append(("Cafe", 0.045 + _i * 0.062, 0.575, "+z"))
for _i in range(3):                                  # Réseau ×3, sous l'orbit, face au bas
    TARGETS.append(("Reseau", -0.140 + _i * 0.062, 0.910, "-z"))

# Poteaux caoutchouc. Chacun laisse 0,037 côté centre (franchissable) et 0,021 côté mur (non
# franchissable) : un poteau collé au mur, comme sur une vraie table.
POST_R = 0.008
POST_H = 0.026
POSTS = [
    (-0.228, 0.700), (0.228, 0.700),
    (-0.228, 0.900), (0.228, 0.900),
]

# Caisson et fronton.
CAB_T = 0.020
CAB_TOP = 0.030
CAB_BOT = -0.075
CAB_Z0 = BOTTOM_Z - 0.060
CAB_Z1 = TOP_Z + WALL_T + 0.024
BACKBOX_Z0 = CAB_Z1
BACKBOX_Z1 = CAB_Z1 + 0.120
BACKBOX_TOP = 0.500
TROUGH_Y = -0.046                   # bandeau de drain, sous le bord bas

# --------------------------------------------------------------------------- repère


def to_blender(x, y, z):
    """Repère de la table -> repère Blender.

    Le `-x` compense la réflexion qu'Unity applique à la lecture (`blender -> unity` vaut
    `(-x, z, -y)`), sans quoi la table ressort miroitée gauche-droite. Voir l'en-tête pour la
    mesure qui l'établit. Ce n'est donc pas une rotation : la géométrie engendrée est un miroir,
    et le sens des faces tel qu'écrit est retourné — `recalc_face_normals` le rétablit.
    """
    return (-x, -z, y)


def dedupe(points):
    """Retire les points consécutifs confondus — un contour qui se referme en double crée une
    arête de longueur nulle, que `from_pydata` accepte mais qui fausse le volume signé."""
    out = []

    for point in points:
        if not out or abs(point[0] - out[-1][0]) > 1e-9 or abs(point[1] - out[-1][1]) > 1e-9:
            out.append(point)

    if len(out) > 1 and abs(out[0][0] - out[-1][0]) < 1e-9 and abs(out[0][1] - out[-1][1]) < 1e-9:
        out.pop()

    return out


def signed_area(points_xz):
    """Aire signée (formule du lacet) dans le plan (x, z)."""
    total = 0.0

    for i in range(len(points_xz)):
        x0, z0 = points_xz[i]
        x1, z1 = points_xz[(i + 1) % len(points_xz)]
        total += x0 * z1 - x1 * z0

    return total / 2.0


# --------------------------------------------------------------------------- maillage


def make_solid(name, verts, faces, parent):
    """Crée le maillage, puis laisse Blender recalculer les normales.

    L'orientation des faces est ainsi garantie sur tout solide fermé, plutôt que déduite d'une
    convention d'ordre des sommets qu'il faudrait vérifier à la main sur chaque primitive.
    """
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    if parent is not None:
        obj.parent = parent

    return obj


def prism(name, points_xz, y0, y1, parent):
    """Prisme vertical : un polygone du plan (x, z) extrudé de `y0` à `y1`.

    Le polygone est remis dans le sens direct (aire signée positive) avant génération : une
    liste de points écrite à l'envers produirait sinon un solide retourné, invisible depuis
    l'extérieur. Le recalcul de normales qui suit achève de garantir le résultat.
    """
    pts = dedupe(list(points_xz))

    if signed_area(pts) < 0:
        pts.reverse()

    n = len(pts)
    verts = [to_blender(x, y0, z) for (x, z) in pts] + \
            [to_blender(x, y1, z) for (x, z) in pts]

    faces = [list(range(n)), list(range(2 * n - 1, n - 1, -1))]

    for i in range(n):
        j = (i + 1) % n
        faces.append([n + i, n + j, j, i])

    return make_solid(name, verts, faces, parent)


def box(name, x0, x1, z0, z1, y0, y1, parent):
    """Boîte alignée, exprimée en cotes de table."""
    return prism(name, [(x0, z0), (x1, z0), (x1, z1), (x0, z1)], y0, y1, parent)


def mirror_box(name, side, xa, xb, z0, z1, y0, y1, parent):
    """Boîte d'un demi-plateau, reportée sur le côté demandé."""
    return box(name, min(side * xa, side * xb), max(side * xa, side * xb),
               z0, z1, y0, y1, parent)


def cyl(name, cx, cz, radius, y0, y1, parent, segments=24):
    """Cylindre vertical, approché par un polygone régulier."""
    pts = []

    for i in range(segments):
        angle = 2.0 * math.pi * i / segments
        pts.append((cx + radius * math.cos(angle), cz + radius * math.sin(angle)))

    return prism(name, pts, y0, y1, parent)


def arc_points(cx, cz, r, a0_deg, a1_deg, segments=28):
    """Points d'un arc dans le plan (x, z), pour composer un contour."""
    return [
        (cx + r * math.cos(math.radians(a0_deg + (a1_deg - a0_deg) * i / segments)),
         cz + r * math.sin(math.radians(a0_deg + (a1_deg - a0_deg) * i / segments)))
        for i in range(segments + 1)
    ]


def arc_band(name, cx, cz, r_in, r_out, a0_deg, a1_deg, y0, y1, parent, segments=28):
    """Bande annulaire : mur courbe d'épaisseur `r_out - r_in`, de `a0` à `a1` (degrés)."""
    pts = arc_points(cx, cz, r_out, a0_deg, a1_deg, segments)
    pts += arc_points(cx, cz, r_in, a1_deg, a0_deg, segments)

    return prism(name, pts, y0, y1, parent)


def ring_xy(name, cx, cy, r_in, r_out, z0, z1, parent, segments=32):
    """Anneau vertical : une couronne dans le plan (x, y), extrudée de `z0` à `z1`.

    Sert à l'emblème `@` du fronton. Bâti en quads — un anneau ne s'exprime pas comme un prisme
    à un seul contour, puisqu'il a un trou.
    """
    outer = [(cx + r_out * math.cos(2 * math.pi * i / segments),
              cy + r_out * math.sin(2 * math.pi * i / segments)) for i in range(segments)]
    inner = [(cx + r_in * math.cos(2 * math.pi * i / segments),
              cy + r_in * math.sin(2 * math.pi * i / segments)) for i in range(segments)]

    verts = [to_blender(x, y, z0) for (x, y) in outer]
    verts += [to_blender(x, y, z1) for (x, y) in outer]
    verts += [to_blender(x, y, z0) for (x, y) in inner]
    verts += [to_blender(x, y, z1) for (x, y) in inner]

    o0, o1, i0, i1 = 0, segments, 2 * segments, 3 * segments
    faces = []

    for k in range(segments):
        n = (k + 1) % segments
        faces.append([o1 + k, o1 + n, i1 + n, i1 + k])          # avant
        faces.append([o0 + n, o0 + k, i0 + k, i0 + n])          # arrière
        faces.append([o0 + k, o0 + n, o1 + n, o1 + k])          # pourtour extérieur
        faces.append([i0 + n, i0 + k, i1 + k, i1 + n])          # pourtour intérieur

    return make_solid(name, verts, faces, parent)


def diagonal_band(name, a, b, thickness, y0, y1, parent, side=1):
    """Corps d'un slingshot : la bande entre la face active `a -> b` et son décalage extérieur.

    `a` et `b` sont donnés en cotes du demi-plateau droit ; `side` les reporte au besoin.
    """
    ax, az = side * a[0], a[1]
    bx, bz = side * b[0], b[1]

    dx, dz = bx - ax, bz - az
    length = math.hypot(dx, dz)
    nx, nz = side * (-dz / length), dx / length     # normale extérieure (vers le mur)

    return prism(name, [
        (ax, az), (bx, bz),
        (bx + nx * thickness, bz + nz * thickness),
        (ax + nx * thickness, az + nz * thickness),
    ], y0, y1, parent)


# --------------------------------------------------------------------------- sections

_GROUPS = []


def _group(root, name):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = root
    obj.empty_display_size = 0.02
    _GROUPS.append(name)
    return obj


def build_playfield(root):
    """Surface de jeu et plancher du couloir de lancement."""
    g = _group(root, "Playfield")

    # Contour : mur gauche, congé gauche, mur du fond, congé droit, mur du couloir, bord bas.
    # Le bord bas est **ouvert** sur toute sa longueur : c'est la zone de sortie. Une bille qui
    # le franchit tombe dans le bandeau et est perdue — pas besoin d'encoche, le bord du plateau
    # fait exactement ce qu'une encoche ferait, sans arête où se coincer.
    outline = [(-HALF_W, BOTTOM_Z)]
    outline += arc_points(FILLET_L_CX, FILLET_L_CZ, R_FILLET_L, 180, 90, segments=20)
    outline += [(FILLET_R_CX, TOP_Z)]
    outline += arc_points(FILLET_R_CX, FILLET_R_CZ, R_FILLET_R, 90, 0, segments=20)
    outline += [(OWALL_X0, BOTTOM_Z)]

    prism("Surface", outline, -SLAB_T, 0.0, g)

    # Le plancher du couloir monte jusqu'au mur du fond : sinon la bille arriverait au bout du
    # plancher en pleine trajectoire ascendante, au moment précis où le congé doit la renvoyer.
    box("LaneFloor", LANE_X0, LANE_X1, BOTTOM_Z, TOP_Z, 0.0, LANE_RAISE, g)

    return g


def build_walls(root):
    """Murs de l'aire de jeu, du couloir et du fond."""
    g = _group(root, "Walls")

    box("Left", -HALF_W - WALL_T, -HALF_W, BOTTOM_Z, FILLET_L_CZ, 0.0, WALL_H, g)
    box("Top", FILLET_L_CX, FILLET_R_CX, TOP_Z, TOP_Z + WALL_T, 0.0, WALL_H, g)

    # Mur de droite : il sépare l'aire de jeu du couloir et porte le rail de l'orbit.
    box("Divider", DIV_X0, DIV_X1, BOTTOM_Z, RAIL_Z0, 0.0, WALL_H, g)
    box("LaneOuter", OWALL_X0, OWALL_X1, BOTTOM_Z, FILLET_R_CZ, 0.0, WALL_H, g)

    arc_band("FilletLeft", FILLET_L_CX, FILLET_L_CZ, R_FILLET_L, R_FILLET_L + WALL_T,
             180, 90, 0.0, WALL_H, g)
    arc_band("FilletRight", FILLET_R_CX, FILLET_R_CZ, R_FILLET_R, R_FILLET_R + WALL_T,
             90, 0, 0.0, WALL_H, g)

    # Le congé gauche et le mur gauche se rejoignent en (-0,257175, 0,9968) : le congé *est* le
    # coin, il n'y a pas de segment droit à intercaler entre les deux.

    return g


def build_orbit(root):
    """Orbit : le rail qui borde la voie rapide du haut, et son congé de raccordement.

    Le rail est un mur ; la voie est la bande de plateau comprise entre lui et le mur du fond.
    Sa fin gauche est franche et dégagée : la bille qui la dépasse retombe sur le plateau, et le
    congé gauche la reprend pour l'envoyer vers le bas. C'est la sortie de l'orbit.
    """
    g = _group(root, "Orbit")

    box("Rail", RAIL_X0, RAIL_X1, RAIL_Z0, RAIL_Z1, 0.0, WALL_H, g)

    # Congé du coin concave rail / mur de droite. Sans lui, la bille qui longe le mur de droite
    # vers le haut bute dans un angle à 90° : elle s'y arrête, et rien ne peut l'en déloger.
    cx = RAIL_X1 - R_CORNER
    cz = RAIL_Z0 - R_CORNER
    corner = [(RAIL_X1, cz)] + arc_points(cx, cz, R_CORNER, 0, 90, segments=16) + \
             [(RAIL_X1, RAIL_Z0)]

    prism("RailCorner", corner, 0.0, WALL_H, g)

    return g


def build_slingshots(root):
    """Slingshots. La face active est portée par le corps lui-même : `Slingshot.cs` applique
    l'impulsion depuis cette face."""
    g = _group(root, "Slingshots")

    for side, tag in ((1, "R"), (-1, "L")):
        diagonal_band("Body_" + tag, SLING_TOP, SLING_BOT, SLING_T, 0.0, 0.022, g, side)

    return g


def build_bumpers(root):
    """Socles des bumpers : la jupe et le chapeau sont portés par les pièces du dépôt."""
    g = _group(root, "Bumpers")

    for i, (x, z) in enumerate(BUMPERS):
        cyl("Base_{:02d}".format(i + 1), x, z, BUMPER_R, 0.0, BUMPER_H, g)

    return g


def build_targets(root):
    """Cibles relevées, groupées comme le demande le GDD §Cibles fixes."""
    g = _group(root, "Targets")

    counts = {}

    for group, x, z, facing in TARGETS:
        counts[group] = counts.get(group, 0) + 1
        name = "{}_{:02d}".format(group, counts[group])

        if facing == "+z":
            plate = [(x - TARGET_W / 2, z), (x + TARGET_W / 2, z),
                     (x + TARGET_W / 2, z + TARGET_T), (x - TARGET_W / 2, z + TARGET_T)]
            foot = [(x - TARGET_W / 2 - 0.004, z - 0.008), (x + TARGET_W / 2 + 0.004, z - 0.008),
                    (x + TARGET_W / 2 + 0.004, z), (x - TARGET_W / 2 - 0.004, z)]
        else:
            plate = [(x - TARGET_W / 2, z - TARGET_T), (x + TARGET_W / 2, z - TARGET_T),
                     (x + TARGET_W / 2, z), (x - TARGET_W / 2, z)]
            foot = [(x - TARGET_W / 2 - 0.004, z), (x + TARGET_W / 2 + 0.004, z),
                    (x + TARGET_W / 2 + 0.004, z + 0.008), (x - TARGET_W / 2 - 0.004, z + 0.008)]

        prism(name, plate, 0.0, TARGET_H, g)
        prism(name + "_Foot", foot, 0.0, TARGET_H + 0.004, g)

    return g


def build_posts(root):
    """Poteaux caoutchouc : ils séparent les trajectoires et cassent les angles vifs."""
    g = _group(root, "Posts")

    for i, (x, z) in enumerate(POSTS):
        cyl("Post_{:02d}".format(i + 1), x, z, POST_R, 0.0, POST_H, g)

    return g


def build_emblem(root):
    """Emblème `@` du fronton — le symbole du GDD (Loop @), posé à plat contre le fronton."""
    g = _group(root, "Emblem")

    ring_xy("Loop_At_Emblem", 0.0, 0.150, 0.036, 0.056,
            BACKBOX_Z0 - 0.010, BACKBOX_Z0 - 0.004, g, segments=36)

    return g


def build_cabinet(root):
    """Caisson, fronton, bandeau de drain. **Décor : aucun collider.**

    Les pieds sont volontairement absents : ils sont verticaux dans le repère de la table, et
    l'inclinaison de 7° posée dans Unity les ferait pencher avec elle. Mieux vaut ne pas les
    modeler que les livrer de travers.
    """
    g = _group(root, "Cabinet")

    x0 = -(OWALL_X1 + CAB_T)
    x1 = OWALL_X1 + CAB_T

    mirror_box("Side_R", 1, OWALL_X1, OWALL_X1 + CAB_T, CAB_Z0, CAB_Z1, CAB_BOT, CAB_TOP, g)
    mirror_box("Side_L", -1, OWALL_X1, OWALL_X1 + CAB_T, CAB_Z0, CAB_Z1, CAB_BOT, CAB_TOP, g)
    box("Front", x0, x1, CAB_Z0, CAB_Z0 + CAB_T, CAB_BOT, CAB_TOP, g)
    box("Back", x0, x1, CAB_Z1 - CAB_T, CAB_Z1, CAB_BOT, CAB_TOP, g)
    box("Floor", x0, x1, CAB_Z0, CAB_Z1, CAB_BOT, CAB_BOT + 0.012, g)

    # Bandeau de drain : sous le bord bas du plateau, il recueille la bille perdue. Décor, lui
    # aussi — c'est `DrainZone` qui déclare la perte, par un trigger, pas ce bandeau.
    box("DrainTrough", -HALF_W, HALF_W, CAB_Z0 + CAB_T, BOTTOM_Z, TROUGH_Y, TROUGH_Y + 0.012, g)

    # Fronton : écran, tablette et marquee. La tablette est posée **derrière** le mur du fond,
    # jamais au-dessus de l'orbit, où elle ferait un rebord à hauteur de bille.
    box("Backbox", x0, x1, BACKBOX_Z0, BACKBOX_Z1, CAB_TOP, BACKBOX_TOP, g)
    box("Screen", x0 + 0.030, x1 - 0.030, BACKBOX_Z0 - 0.004, BACKBOX_Z0,
        CAB_TOP + 0.070, BACKBOX_TOP - 0.070, g)
    box("Ledge", x0, x1, TOP_Z + WALL_T, BACKBOX_Z0, CAB_TOP, CAB_TOP + 0.018, g)
    box("Marquee", x0 + 0.010, x1 - 0.010, BACKBOX_Z1, BACKBOX_Z1 + 0.014,
        BACKBOX_TOP - 0.055, BACKBOX_TOP, g)

    return g


TABLE_SECTIONS = ("playfield", "walls", "orbit", "slingshots", "bumpers", "targets", "posts")
CABINET_SECTIONS = ("cabinet", "emblem")

SECTIONS = {
    "playfield": build_playfield,
    "walls": build_walls,
    "orbit": build_orbit,
    "slingshots": build_slingshots,
    "bumpers": build_bumpers,
    "targets": build_targets,
    "posts": build_posts,
    "cabinet": build_cabinet,
    "emblem": build_emblem,
}


# --------------------------------------------------------------------------- rapport


def volume_of(obj):
    """Volume signé du solide. Négatif = normales retournées."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    value = bm.calc_volume(signed=True)
    bm.free()
    return value


def measure(objects):
    """Boîte englobante d'un ensemble d'objets, en coordonnées de table."""
    low = [float("inf")] * 3
    high = [float("-inf")] * 3

    for obj in objects:
        if obj.type != "MESH":
            continue

        for corner in obj.bound_box:
            point = obj.matrix_world @ mathutils.Vector(corner)

            for axis in range(3):
                low[axis] = min(low[axis], point[axis])
                high[axis] = max(high[axis], point[axis])

    if low[0] == float("inf"):
        return None

    size = [high[i] - low[i] for i in range(3)]
    # Blender (x, y, z) = table (x, z, -y) : on remet dans l'ordre de la table pour lire.
    return (low, high, (size[0], size[2], size[1]))


def report(sections):
    print("=" * 78)
    print("Table construite — sections : {}".format(", ".join(sections)))
    print("=" * 78)

    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    measured = measure(meshes)

    if measured is None:
        print("  Aucun maillage construit.")
        return

    low, high, size = measured
    triangles = 0
    inverted = []

    for obj in meshes:
        triangles += sum(len(p.vertices) - 2 for p in obj.data.polygons)

        if volume_of(obj) < -1e-12:
            inverted.append(obj.name)

    print("  Encombrement table  : {:.4f} × {:.4f} × {:.4f} m   (L × H × l)".format(
        size[0], size[1], size[2]))
    print("  Encombrement Unity  : {:.2f} × {:.2f} × {:.2f} u".format(
        size[0] * UNITS_PER_METER, size[1] * UNITS_PER_METER, size[2] * UNITS_PER_METER))
    # Blender y = -table z, donc l'étendue en z de la table se lit à l'envers sur l'axe y.
    print("  Étendue en z (table): {:.4f} → {:.4f} m".format(-high[1], -low[1]))
    print("  Étendue verticale   : {:.4f} → {:.4f} m".format(low[2], high[2]))
    print("  Aire de jeu         : {:.4f} m de large = {:.1f} billes".format(
        PLAY_W, PLAY_W / BALL_D))
    print("  Chenal du lanceur   : {:.4f} m = {:.2f} billes".format(LANE_W, LANE_W / BALL_D))
    print("  Drain, entre flippers: {:.4f} m = {:.2f} billes".format(DRAIN_GAP, DRAIN_GAP / BALL_D))
    print("  Triangles           : {} sur {} maillages".format(triangles, len(meshes)))

    if inverted:
        print("  ⚠ NORMALES RETOURNÉES : {}".format(", ".join(inverted)))
    else:
        print("  Normales            : toutes sortantes (volume signé positif).")


def report_hosts():
    """Positions des hôtes Unity, en unités Unity — de quoi poser la scène sans mesurer."""
    def u(value):
        return value * UNITS_PER_METER

    print("\n" + "=" * 78)
    print("Hôtes de gameplay — positions en unités Unity (repère de la table, à plat)")
    print("=" * 78)

    rows = [
        ("Flipper_Left_Pivot", (-FLIP_PIVOT_X, 0.0, FLIP_PIVOT_Z)),
        ("Flipper_Right_Pivot", (FLIP_PIVOT_X, 0.0, FLIP_PIVOT_Z)),
        ("Slingshot_Left", (SLING_BOT[0] * -0.5, 0.0, 0.215)),
        ("Slingshot_Right", (SLING_BOT[0] * 0.5, 0.0, 0.215)),
        ("BallSpawnPoint", (LANE_CX, LANE_RAISE + BALL_R + 0.004, 0.060)),
        ("Plunger", (LANE_CX, LANE_RAISE + BALL_R + 0.004, BOTTOM_Z - 0.010)),
        ("DrainZone (trigger)", (0.0, -0.020, BOTTOM_Z - 0.020)),
    ]

    for i, (x, z) in enumerate(BUMPERS):
        rows.insert(4 + i, ("Bumper_{:02d}".format(i + 1), (x, 0.0, z)))

    for name, (x, y, z) in rows:
        print("  {:<22} x {:>9.3f}   y {:>8.3f}   z {:>9.3f}".format(name, u(x), u(y), u(z)))

    print("\n  Repère : x = 0 au centre de l'aire de jeu ; z = 0 au bord bas (drain) ;")
    print("           z = {:.2f} u au mur du fond. y = 0 à la surface du plateau.".format(u(TOP_Z)))
    print("  Hauteur de mur      : {:.2f} u  ({:.1f} mm)".format(u(WALL_H), WALL_H * 1000))
    print("  Diamètre de bille   : {:.3f} m = {:.2f} u".format(BALL_D, u(BALL_D)))
    print("  Inclinaison         : à poser dans Unity sur le root PinballTable (rotation X = -7°)")


# --------------------------------------------------------------------------- export


def export(root, path, units_per_meter):
    # Une hiérarchie se met à l'échelle en **une seule écriture** : celle de la racine. Les
    # descendants en héritent, taille et décalage. Toucher en plus à leur `location` ou à leur
    # `scale` appliquerait le facteur deux fois — l'erreur mesurée sur `Plunger` lors de l'export
    # des pièces (annoncé 0,39×2,76×0,39, réimporté 159×251×3,7).
    root.scale = (units_per_meter, units_per_meter, units_per_meter)

    bpy.context.view_layer.update()

    settings = {
        "use_selection": False,
        "global_scale": 1.0,
        "apply_unit_scale": False,
        # Même couple que `export_parts.py`. Il est sans effet sur ce qu'Unity lit : la sonde
        # `probe_axis.py` montre que ces deux réglages ne touchent pas aux sommets — ils ne sont
        # que *déclarés* dans `GlobalSettings`, et Unity applique sa propre conversion sans les
        # lire. On garde donc le couple du projet par cohérence, sans en attendre d'orientation :
        # c'est `to_blender` qui compense le miroir.
        "axis_forward": "-Z",
        "axis_up": "Y",
        "use_mesh_modifiers": True,
        "object_types": {"MESH", "EMPTY"},
        "mesh_smooth_type": "FACE",
        "use_tspace": True,
        "path_mode": "COPY",
        "batch_mode": "OFF",
    }

    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)

    available = set(bpy.ops.export_scene.fbx.get_rna_type().properties.keys())
    accepted = {k: v for k, v in settings.items() if k in available}
    ignored = sorted(set(settings) - set(accepted))

    bpy.ops.export_scene.fbx(filepath=path, **accepted)

    if ignored:
        print("  Paramètres d'export ignorés (inconnus de ce Blender) : {}".format(
            ", ".join(ignored)))

    return path


def verify_fbx(path, expected_objects):
    """Réimporte le FBX dans un fichier vide : ce qui est écrit est constaté, pas supposé."""
    bpy.ops.wm.read_factory_settings(use_empty=True)

    try:
        bpy.ops.import_scene.fbx(filepath=path)
    except Exception as error:  # noqa: BLE001 — on rapporte, on n'interrompt pas
        print("  ÉCHEC   réimport impossible : {}".format(error))
        return False

    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    measured = measure(meshes)

    if measured is None:
        print("  ÉCHEC   le FBX réimporté ne contient aucun maillage.")
        return False

    _low, _high, size = measured
    ok = len(meshes) == expected_objects
    verdict = "OK " if ok else "ÉCART"

    # Pas de facteur ici : l'échelle de la racine a été écrite **dans** le FBX, donc ce qui est
    # réimporté est déjà en unités Unity. Y remultiplier 16,667 afficherait 157 au lieu de 9,44.
    print("  {} {} maillages (attendu {}), encombrement {:.2f} × {:.2f} × {:.2f} u".format(
        verdict, len(meshes), expected_objects, size[0], size[2], size[1]))

    return ok


def build(name, sections, args):
    """Construit un lot dans une scène vide et l'exporte."""
    del _GROUPS[:]

    bpy.ops.wm.read_factory_settings(use_empty=True)

    root = bpy.data.objects.new("PinballTable_Model", None)
    bpy.context.scene.collection.objects.link(root)
    root.empty_display_size = 0.05
    bpy.context.view_layer.objects.active = root

    for section in sections:
        SECTIONS[section](root)

    report(sections)

    if args.no_export:
        return None

    path = os.path.abspath(os.path.join(args.out, name + ".fbx"))
    export(root, path, args.units_per_meter)
    print("\n  FBX écrit : {}   (échelle de racine {:.4f})".format(path, args.units_per_meter))

    if args.verify:
        verify_fbx(path, sum(1 for o in bpy.data.objects if o.type == "MESH"))

    return path


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", default="Assets/Models/Table",
                        help="dossier de sortie des FBX")
    parser.add_argument("--blend", default="",
                        help="enregistre aussi le .blend du plateau à ce chemin")
    parser.add_argument("--verify", action="store_true",
                        help="réimporte chaque FBX écrit et mesure ce qu'il contient")
    parser.add_argument("--units-per-meter", type=float, default=UNITS_PER_METER,
                        help="facteur d'échelle (défaut : ancre de la bille)")
    parser.add_argument("--no-export", action="store_true", help="construire sans exporter")
    parser.add_argument("--only", default="",
                        help="ne construire qu'un lot : table, cabinet (défaut : les deux)")
    return parser.parse_args(argv)


def main():
    args = parse_args()

    lots = [("table", "Pinball_Table", TABLE_SECTIONS),
            ("cabinet", "Pinball_Cabinet", CABINET_SECTIONS)]

    wanted = {s.strip().lower() for s in args.only.split(",") if s.strip()}
    selected = [lot for lot in lots if not wanted or lot[0] in wanted]

    if not selected:
        raise SystemExit("Lot inconnu : {!r}. Connus : table, cabinet".format(args.only))

    for _key, name, sections in selected:
        build(name, sections, args)

    report_hosts()

    if args.blend:
        # Le .blend est enregistré depuis le dernier lot construit ; pour l'éditer à la main,
        # relancer avec --only sur le lot voulu.
        os.makedirs(os.path.dirname(os.path.abspath(args.blend)), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
        print("\nBlend enregistré : {}".format(os.path.abspath(args.blend)))


if __name__ == "__main__":
    main()
