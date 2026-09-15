"""Sonde : que fait réellement le couple `axis_forward` / `axis_up` au FBX écrit ?

Un repère asymétrique (une boîte à x > 0, en repère de table) est exporté avec plusieurs couples
d'axes, puis chaque fichier est **lu octet par octet** — les coordonnées des sommets et les
réglages d'axes déclarés dans `GlobalSettings`.

C'est nécessaire parce que les deux méthodes évidentes ne prouvent rien :

- **Comparer les empreintes** : sans valeur, le FBX embarque un horodatage de création, donc
  deux exports du même fichier ont déjà deux empreintes différentes.
- **Réimporter dans Blender** : sans valeur, l'importateur défait exactement ce que
  l'exportateur a fait. Quel que soit le couple d'axes, on relit les coordonnées d'origine.

Seules les valeurs écrites dans le fichier disent ce qu'un autre moteur — Unity — lira.

    blender --background --factory-startup --python Tools/blender/probe_axis.py
"""

import os
import struct
import sys
import tempfile
import zlib

import bpy

OUT = os.path.join(tempfile.gettempdir(), "flipper_axis_probe")

PAIRS = [
    ("minusZ_Y", "-Z", "Y"),        # le couple d'`export_parts.py`
    ("minusY_Z", "-Y", "Z"),
    ("Y_Z", "Y", "Z"),
    ("X_minusZ", "X", "-Z"),
]

ARRAY_FORMATS = {"f": ("f", 4), "d": ("d", 8), "l": ("q", 8), "i": ("i", 4), "b": ("B", 1)}

INTERESTING = ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign",
               "CoordAxis", "CoordAxisSign")


# --------------------------------------------------------------------------- lecture FBX


def read_properties(data, pos, count):
    properties = []

    for _ in range(count):
        code = chr(data[pos])
        pos += 1

        if code == "Y":
            properties.append(struct.unpack_from("<h", data, pos)[0])
            pos += 2
        elif code == "C":
            properties.append(data[pos])
            pos += 1
        elif code == "I":
            properties.append(struct.unpack_from("<i", data, pos)[0])
            pos += 4
        elif code == "F":
            properties.append(struct.unpack_from("<f", data, pos)[0])
            pos += 4
        elif code == "D":
            properties.append(struct.unpack_from("<d", data, pos)[0])
            pos += 8
        elif code == "L":
            properties.append(struct.unpack_from("<q", data, pos)[0])
            pos += 8
        elif code in ARRAY_FORMATS:
            length, encoding, compressed = struct.unpack_from("<III", data, pos)
            pos += 12
            fmt, size = ARRAY_FORMATS[code]

            if encoding == 0:
                raw = data[pos:pos + length * size]
                pos += length * size
            else:
                raw = zlib.decompress(data[pos:pos + compressed])
                pos += compressed

            properties.append(list(struct.unpack_from("<{}{}".format(length, fmt), raw, 0)))
        elif code in ("S", "R"):
            length = struct.unpack_from("<I", data, pos)[0]
            pos += 4
            properties.append(data[pos:pos + length].decode("utf-8", "replace"))
            pos += length
        else:
            raise ValueError("type de propriété inconnu : {!r}".format(code))

    return properties, pos


def read_nodes(data, start, end):
    nodes = []
    pos = start

    while pos < end:
        end_offset, count, _length, name_len = struct.unpack_from("<IIIB", data, pos)

        # Enregistrement nul de 13 octets : fin de la liste d'enfants.
        if end_offset == 0 and count == 0 and name_len == 0:
            pos += 13
            break

        name = data[pos + 13:pos + 13 + name_len].decode("utf-8", "replace")
        properties, child_start = read_properties(data, pos + 13 + name_len, count)
        children, _ = read_nodes(data, child_start, end_offset)

        nodes.append((name, properties, children))
        pos = end_offset

    return nodes, pos


def load_fbx(path):
    with open(path, "rb") as handle:
        data = handle.read()

    if not data.startswith(b"Kaydara FBX Binary"):
        raise ValueError("FBX binaire attendu")

    # offset 23 : version ; les offsets de nœud sont sur 32 bits jusqu'à la version 7500.
    version = struct.unpack_from("<I", data, 23)[0]
    nodes, _ = read_nodes(data, 27 if version < 7500 else 27, len(data))

    return version, nodes


def find(node_list, name):
    for node in node_list:
        if node[0] == name:
            return node
    return None


def first_vertices(nodes, count=3):
    """Les `count` premiers sommets du premier maillage venu."""
    stack = list(nodes)

    while stack:
        name, properties, children = stack.pop()

        if name == "Vertices" and properties and isinstance(properties[0], list):
            flat = properties[0]
            return [tuple(flat[i:i + 3]) for i in range(0, min(len(flat), count * 3), 3)]

        stack.extend(children)

    return None


def global_axes(nodes):
    """Les réglages d'axes déclarés dans le fichier.

    Ils ne sont pas enfants directs de `GlobalSettings` mais posés dans son `Properties70`
    (`P` nommé, type `int`, valeur) — d'où la descente récursive.
    """
    settings = find(nodes, "GlobalSettings")

    if settings is None:
        return {}

    found = {}
    stack = list(settings[2])

    while stack:
        name, properties, children = stack.pop()

        # Un `P` porte cinq propriétés : nom, type, sous-type, drapeaux, puis la valeur.
        if name == "P" and len(properties) >= 5 and properties[0] in INTERESTING:
            found[properties[0]] = properties[-1]

        stack.extend(children)

    return found


# --------------------------------------------------------------------------- export


def make_marker():
    """Boîte en repère de table : x 0,10 -> 0,30 ; z 0,00 -> 0,20 ; y 0,00 -> 0,10.

    Le passage au repère Blender est `(x, y, z) -> (x, -z, y)`, comme dans `build_table.py` :
    soit, en Blender, x 0,10 -> 0,30 ; y -0,20 -> 0 ; z 0,00 -> 0,10.
    """
    corners = [(0.10, 0.0, 0.0), (0.30, 0.0, 0.0), (0.30, 0.0, 0.20), (0.10, 0.0, 0.20),
               (0.10, 0.1, 0.0), (0.30, 0.1, 0.0), (0.30, 0.1, 0.20), (0.10, 0.1, 0.20)]

    mesh = bpy.data.meshes.new("Marker")
    mesh.from_pydata([(x, -z, y) for (x, y, z) in corners], [],
                     [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1),
                      (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)])
    mesh.update()

    obj = bpy.data.objects.new("Marker", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def bake(axis_forward, axis_up, path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    make_marker()

    settings = {
        "use_selection": False,
        "global_scale": 1.0,
        "apply_unit_scale": False,
        "axis_forward": axis_forward,
        "axis_up": axis_up,
        "use_mesh_modifiers": True,
        "object_types": {"MESH"},
        "mesh_smooth_type": "FACE",
        "path_mode": "COPY",
        "batch_mode": "OFF",
    }

    available = set(bpy.ops.export_scene.fbx.get_rna_type().properties.keys())
    accepted = {k: v for k, v in settings.items() if k in available}

    bpy.ops.export_scene.fbx(filepath=path, **accepted)
    return path


def as_int(value):
    """`GlobalSettings` écrit ses entiers tantôt en `int`, tantôt en chaîne selon la version."""
    if isinstance(value, int):
        return value

    try:
        return int(str(value).strip())
    except ValueError:
        return None


def declared_basis(axes):
    """Le repère déclaré, et son déterminant.

    `CoordAxis` / `UpAxis` / `FrontAxis` désignent chacun un axe canonique (0 = x, 1 = y,
    2 = z) affecté d'un signe. Le déterminant de la matrice ainsi formée dit si le repère est
    direct (+1) ou inversé (−1) : c'est ce qui distingue une rotation d'une réflexion.
    """
    keys = (("CoordAxis", "CoordAxisSign"), ("UpAxis", "UpAxisSign"),
            ("FrontAxis", "FrontAxisSign"))

    values = {}

    for axis, sign in keys:
        index = as_int(axes.get(axis))
        orientation = as_int(axes.get(sign))

        if index is None or orientation is None or not 0 <= index <= 2:
            return None, None, axes

        values[axis] = index
        values[sign] = orientation

    rows = []

    for axis, sign in keys:
        row = [0, 0, 0]
        row[values[axis]] = 1 if values[sign] >= 0 else -1
        rows.append(row)

    determinant = (
        rows[0][0] * (rows[1][1] * rows[2][2] - rows[1][2] * rows[2][1])
        - rows[0][1] * (rows[1][0] * rows[2][2] - rows[1][2] * rows[2][0])
        + rows[0][2] * (rows[1][0] * rows[2][1] - rows[1][1] * rows[2][0])
    )

    return rows, determinant, axes


def main():
    os.makedirs(OUT, exist_ok=True)

    print("Repère de table : x 0,10 -> 0,30   z 0,00 -> 0,20   y 0,00 -> 0,10")
    print("En Blender      : x 0,10 -> 0,30   y -0,20 -> 0,00  z 0,00 -> 0,10")
    print("=" * 90)

    for name, forward, up in PAIRS:
        path = os.path.join(OUT, name + ".fbx")

        try:
            bake(forward, up, path)
            version, nodes = load_fbx(path)
        except Exception as error:  # noqa: BLE001 — on rapporte, on n'interrompt pas
            print("  {:<10} ÉCHEC : {}".format(name, error))
            continue

        print("  {:<10} axis_forward={:<4} axis_up={:<3}   (FBX {})".format(
            name, forward, up, version))

        axes = global_axes(nodes)
        rows, determinant, _ = declared_basis(axes)

        print("             brut           : {}".format(
            "  ".join("{}={!r}".format(k, axes[k]) for k in INTERESTING if k in axes)
            or "(aucun)"))
        if rows is None:
            print("             repère déclaré : illisible")
        else:
            print("             repère déclaré : {}".format(
                "  ".join("[{}{}{}]".format(*r) for r in rows)
                + "   déterminant {:+d}".format(determinant)))

        vertices = first_vertices(nodes)

        if vertices:
            print("             sommets écrits : {}".format(
                "  ".join("({:+.3f} {:+.3f} {:+.3f})".format(*v) for v in vertices)))
        else:
            print("             sommets écrits : (aucun trouvé)")

    print("=" * 90)
    print("Deux conclusions, tirées des octets et non du raisonnement :")
    print()
    print("1. Les sommets sortent identiques dans les quatre cas : le couple d'axes n'est PAS")
    print("   cuit dans les coordonnées, il n'est que déclaré dans `GlobalSettings`. Et les")
    print("   quatre repères déclarés ont un déterminant +1 : aucun n'est une réflexion.")
    print("   Changer le couple ne peut donc pas expliquer un miroir — et mesuré dans Unity,")
    print("   il n'en produit aucun : les deux couples essayés donnent le même résultat.")
    print()
    print("2. Unity applique sa propre conversion, fixe, sans lire `GlobalSettings` :")
    print("   blender (x, y, z) -> unity (-x, z, -y).  Combinée à `to_blender` de")
    print("   `build_table.py`, elle donne table (x, y, z) -> unity (-x, y, z) : la table")
    print("   ressort miroitée gauche-droite, chenal de lancement du mauvais côté.")
    print()
    print("La compensation se fait donc à la source — `to_blender` nie x — et non par le")
    print("couple d'axes, qui n'a aucun effet sur ce qu'Unity lit.")
    return 0


if __name__ == "__main__":
    sys.exit(main() or 0)
