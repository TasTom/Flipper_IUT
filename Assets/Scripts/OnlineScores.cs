using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Classement mondial des parties terminées (GDD § High score et UI).</summary>
internal static class OnlineScores
{
    internal const int Limit = 100;
    private const string Endpoint = "https://wgwiquzdppexpgjxzgcl.supabase.co/rest/v1/rpc/";
    // Clé publiable Supabase : seules les fonctions SQL accordées à anon sont accessibles.
    private const string PublishableKey = "sb_publishable_exzNw1nQBzMT0OHLlbMUrA_nhDdf4g8";

    [Serializable]
    internal sealed class Entry
    {
        public string initials;
        public int score;
    }

    [Serializable]
    private sealed class Entries
    {
        public Entry[] items;
    }

    [Serializable]
    private sealed class Submission
    {
        public string p_run_id;
        public string p_initials;
        public int p_score;
    }

    internal static bool Qualifies(int score, Entry[] ranking)
        => score > 0 && (ranking.Length < Limit || score > ranking[Limit - 1].score);

    internal static bool ValidInitials(string value)
        => value != null && value.Length == 3 && Letter(value[0]) && Letter(value[1]) && Letter(value[2]);

    internal static bool Letter(char value) => value >= 'A' && value <= 'Z';

    internal static IEnumerator Fetch(Action<Entry[]> finished)
    {
        using var request = UnityWebRequest.Get(Endpoint + "flipper_top_scores");
        Configure(request);
        yield return request.SendWebRequest();
        Entry[] entries = null;
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                entries = JsonUtility.FromJson<Entries>("{\"items\":" + request.downloadHandler.text + "}")?.items;
            }
            catch (ArgumentException)
            {
                // Une réponse inattendue est traitée comme un service indisponible.
            }
        }
        finished(entries);
    }

    internal static IEnumerator Submit(string runId, string initials, int score, Action<bool?> finished)
    {
        string body = JsonUtility.ToJson(new Submission { p_run_id = runId, p_initials = initials, p_score = score });
        using var request = new UnityWebRequest(Endpoint + "flipper_submit_score", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        Configure(request);
        yield return request.SendWebRequest();
        string response = request.downloadHandler.text.Trim();
        finished(request.result == UnityWebRequest.Result.Success && bool.TryParse(response, out bool accepted)
            ? accepted
            : null);
    }

    private static void Configure(UnityWebRequest request)
    {
        request.timeout = 6;
        request.SetRequestHeader("apikey", PublishableKey);
    }
}
