using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guidage lumineux pro ("shoot the flashing lights") : crée et pilote des inserts lumineux
/// plats sur le plateau devant les tirs majeurs (Rampe IUT, Rampe Vosges, Loop @, Boss).
///
/// <para>Les inserts clignotent automatiquement selon la mission active (<see cref="MissionManager"/>),
/// l'étape du boss (<see cref="BossTarget"/>) et les jackpots en multiball
/// (<see cref="MultiballManager"/>). Aucun collider n'est ajouté : purement visuel.</para>
/// </summary>
public class MissionShotGuide : MonoBehaviour
{
    [Header("Couleurs des inserts")]
    [SerializeField] private Color couleurMission = new Color(0.20f, 0.88f, 1.00f);
    [SerializeField] private Color couleurJackpot = new Color(1.00f, 0.78f, 0.15f);
    [SerializeField] private Color couleurBoss = new Color(1.00f, 0.25f, 0.20f);
    [SerializeField] private Color couleurCorrection = new Color(1.00f, 0.65f, 0.15f);
    [SerializeField] private Color couleurEteinte = new Color(0.08f, 0.10f, 0.14f);

    [Header("Animation")]
    [SerializeField] private float frequenceClignotement = 3.5f;
    [SerializeField] private float intensiteEmission = 3.2f;
    [SerializeField] private float diametreInsert = 0.55f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private sealed class ShotInsert
    {
        public string targetName;
        public Renderer renderer;
        public MaterialPropertyBlock block;
        public bool flashing;
        public Color activeColor;
    }

    private readonly Dictionary<string, ShotInsert> inserts = new Dictionary<string, ShotInsert>();
    private Material sharedMaterial;
    private bool initialized;

    private void Start()
    {
        InitializeInserts();
        RefreshTargets();

        if (MissionManager.Instance != null)
            MissionManager.Instance.MissionChanged += RefreshTargets;
        if (BossTarget.Instance != null)
            BossTarget.Instance.StateChanged += OnBossStateChanged;
        if (BallManager.Instance != null)
            BallManager.Instance.BallCountChanged += RefreshTargets;
    }

    private void OnDestroy()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.MissionChanged -= RefreshTargets;
        if (BossTarget.Instance != null)
            BossTarget.Instance.StateChanged -= OnBossStateChanged;
        if (BallManager.Instance != null)
            BallManager.Instance.BallCountChanged -= RefreshTargets;
        if (sharedMaterial != null)
            Destroy(sharedMaterial);
    }

    private void OnBossStateChanged(BossTarget boss, BossState state) => RefreshTargets();

    private void InitializeInserts()
    {
        if (initialized) return;
        initialized = true;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            sharedMaterial = new Material(shader);
            sharedMaterial.EnableKeyword("_EMISSION");
            sharedMaterial.SetFloat("_Smoothness", 0.7f);
        }

        // Chaque insert est posé juste devant le tir correspondant, plaqué sur le plateau (sans collider).
        CreateInsertFor("Ramp_IUT_In", new Vector3(0f, -0.02f, -0.75f));
        CreateInsertFor("Ramp_Vosges_In", new Vector3(0f, -0.02f, -0.75f));
        CreateInsertFor("Loop_At_Entry", new Vector3(0f, -0.02f, -0.75f));
        CreateInsertFor("Boss_ProjetFinal", new Vector3(0f, -0.02f, -1.10f));
    }

    private void CreateInsertFor(string hostName, Vector3 localOffset)
    {
        var host = GameObject.Find(hostName);
        if (host == null) return;

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = $"Insert_{hostName}";
        var col = disc.GetComponent<Collider>();
        if (col != null) Destroy(col);

        disc.transform.SetParent(host.transform, false);
        disc.transform.localPosition = localOffset;
        disc.transform.localRotation = Quaternion.identity;
        disc.transform.localScale = new Vector3(diametreInsert, 0.015f, diametreInsert);

        int layerElement = LayerMask.NameToLayer("TableElement");
        if (layerElement >= 0) disc.layer = layerElement;

        var r = disc.GetComponent<Renderer>();
        if (r != null && sharedMaterial != null)
        {
            r.sharedMaterial = sharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        inserts[hostName] = new ShotInsert
        {
            targetName = hostName,
            renderer = r,
            block = new MaterialPropertyBlock(),
            flashing = false,
            activeColor = couleurEteinte
        };
    }

    public void RefreshTargets()
    {
        foreach (var kv in inserts)
        {
            kv.Value.flashing = false;
            kv.Value.activeColor = couleurEteinte;
        }

        bool multiball = MultiballManager.Instance != null && MultiballManager.Instance.IsMultiballActive;
        if (multiball)
        {
            SetFlashing("Ramp_IUT_In", couleurJackpot);
            SetFlashing("Ramp_Vosges_In", couleurJackpot);
            SetFlashing("Loop_At_Entry", couleurJackpot);
            return;
        }

        if (BossTarget.Instance != null && BossTarget.Instance.IsActive)
        {
            if (BossTarget.Instance.State == BossState.Correction)
            {
                SetFlashing("Ramp_IUT_In", couleurCorrection);
                SetFlashing("Loop_At_Entry", couleurCorrection);
            }
            else
            {
                SetFlashing("Boss_ProjetFinal", couleurBoss);
            }
            return;
        }

        var mission = MissionManager.Instance != null ? MissionManager.Instance.Active : null;
        if (mission == null) return;

        if (mission.but == MissionGoal.Rampe || mission.but == MissionGoal.LoopAt || mission.but == MissionGoal.Boss)
        {
            SetFlashing(mission.cible, couleurMission);
        }
    }

    private void SetFlashing(string hostName, Color color)
    {
        if (string.IsNullOrEmpty(hostName)) return;
        if (inserts.TryGetValue(hostName, out var ins))
        {
            ins.flashing = true;
            ins.activeColor = color;
        }
    }

    private void Update()
    {
        if (inserts.Count == 0) return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * frequenceClignotement * Mathf.PI * 2f);

        foreach (var kv in inserts)
        {
            var ins = kv.Value;
            if (ins.renderer == null) continue;

            Color c = ins.flashing ? Color.Lerp(couleurEteinte, ins.activeColor, 0.25f + 0.75f * pulse) : couleurEteinte;
            Color em = ins.flashing ? ins.activeColor * (intensiteEmission * (0.2f + 0.8f * pulse)) : Color.black;

            ins.renderer.GetPropertyBlock(ins.block);
            ins.block.SetColor(BaseColorId, c);
            ins.block.SetColor(EmissionColorId, em);
            ins.renderer.SetPropertyBlock(ins.block);
        }
    }
}
