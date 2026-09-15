// État de l'éditeur — LECTURE SEULE. La scène sauvegardée porte les pivots au contrat
// (-1.428, 0, 1.533) alors que la scène vivante les donne dérivés : d'où vient l'écart ?

var sb = new System.Text.StringBuilder();
sb.AppendLine("isPlaying = " + UnityEditor.EditorApplication.isPlaying);
sb.AppendLine("isPlayingOrWillChangePlaymode = " + UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode);
sb.AppendLine("isCompiling = " + UnityEditor.EditorApplication.isCompiling);
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
sb.AppendLine("scene '" + scene.name + "' path " + scene.path + " isDirty " + scene.isDirty + " isLoaded " + scene.isLoaded);

var piv = GameObject.Find("PinballTable/Gameplay/Flipper_Left_Pivot");
if (piv != null)
{
    sb.AppendLine("Pivot gauche VIVANT local " + piv.transform.localPosition.ToString("F4")
        + " | monde " + piv.transform.position.ToString("F4"));
    var rb = piv.GetComponent<Rigidbody>();
    if (rb != null) { sb.AppendLine("   RB velocity " + rb.linearVelocity.ToString("F4") + " angular " + rb.angularVelocity.ToString("F4") + " sleeping " + rb.IsSleeping()); }
}
var pig = GameObject.Find("PinballTable/Gameplay/Flipper_Right_Pivot");
if (pig != null)
{
    sb.AppendLine("Pivot droit VIVANT local " + pig.transform.localPosition.ToString("F4")
        + " | monde " + pig.transform.position.ToString("F4"));
    var rb = pig.GetComponent<Rigidbody>();
    if (rb != null) { sb.AppendLine("   RB velocity " + rb.linearVelocity.ToString("F4") + " angular " + rb.angularVelocity.ToString("F4") + " sleeping " + rb.IsSleeping()); }
}

sb.AppendLine("temps de jeu : " + Time.time + " s | frame " + Time.frameCount + " | fixedTime " + Time.fixedTime);

return sb.ToString();
