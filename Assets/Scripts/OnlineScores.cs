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
    internal static string RunTicket { get; private set; }
    private static Guid pendingRun;

    internal static IEnumerator BeginRun()
    {
        RunTicket = null;
        var run = pendingRun = Guid.NewGuid();

        // Trois tentatives, pas une : le premier appel part pendant le chargement de la scène,
        // quand le navigateur télécharge encore 90 Mo et que le réseau peut lâcher. Un ticket
        // manquant condamne la partie entière — `RunTicket` sert de clé au score final — donc
        // on réessaie pendant que le joueur commence à jouer plutôt que d'abandonner en silence.
        for (int attempt = 0; attempt < 3 && RunTicket == null; attempt++)
        {
            if (attempt > 0) yield return new WaitForSecondsRealtime(2f);

            using var request = JsonRequest("flipper_begin_run", "{}");
            yield return request.SendWebRequest();

            if (run != pendingRun) yield break;

            if (request.result == UnityWebRequest.Result.Success &&
                Guid.TryParse(request.downloadHandler.text.Trim().Trim('"'), out var ticket))
                RunTicket = ticket.ToString();
        }

        if (RunTicket == null)
            Debug.LogWarning("[OnlineScores] Ticket de partie indisponible : le score final ne pourra " +
                             "pas être classé.");
    }

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

    internal enum SubmissionResult { Accepted, OutsideTop, Rejected, Unavailable }

    internal static IEnumerator Submit(string runId, string initials, int score, Action<SubmissionResult> finished)
    {
        string body = JsonUtility.ToJson(new Submission { p_run_id = runId, p_initials = initials, p_score = score });
        using var request = JsonRequest("flipper_submit_score", body);
        yield return request.SendWebRequest();
        string response = request.downloadHandler.text.Trim();
        if (request.result == UnityWebRequest.Result.Success && bool.TryParse(response, out bool accepted))
            finished(accepted ? SubmissionResult.Accepted : SubmissionResult.OutsideTop);
        else
            finished(request.responseCode == 400 ? SubmissionResult.Rejected : SubmissionResult.Unavailable);
    }

    private static UnityWebRequest JsonRequest(string function, string body)
    {
        var request = new UnityWebRequest(Endpoint + function, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        Configure(request);
        return request;
    }

    private static void Configure(UnityWebRequest request)
    {
        // 6 s mesurées trop courtes en WebGL : le premier appel part pendant le chargement et
        // expire, ce qui laissait la partie sans ticket.
        request.timeout = 12;
        request.SetRequestHeader("apikey", PublishableKey);
    }
}
