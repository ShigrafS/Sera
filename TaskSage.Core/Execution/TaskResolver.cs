using System;
using System.Collections.Generic;
using System.Linq;
using TaskSage.Data.Entities;

namespace TaskSage.Core.Execution;

public static class TaskResolver
{
    // A simple Levenshtein distance based fuzzy match
    public static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
            return string.IsNullOrEmpty(t) ? 0 : t.Length;

        if (string.IsNullOrEmpty(t))
            return s.Length;

        int[] v0 = new int[t.Length + 1];
        int[] v1 = new int[t.Length + 1];

        for (int i = 0; i < v0.Length; i++)
            v0[i] = i;

        for (int i = 0; i < s.Length; i++)
        {
            v1[0] = i + 1;
            for (int j = 0; j < t.Length; j++)
            {
                int cost = (s[i] == t[j]) ? 0 : 1;
                v1[j + 1] = Math.Min(v1[j] + 1, Math.Min(v0[j + 1] + 1, v0[j] + cost));
            }
            for (int j = 0; j < v0.Length; j++)
                v0[j] = v1[j];
        }
        return v1[t.Length];
    }

    public static TaskInstance? FindBestMatch(string taskRef, IEnumerable<TaskInstance> candidates)
    {
        if (string.IsNullOrWhiteSpace(taskRef)) return null;

        var normalizedRef = taskRef.Trim().ToLowerInvariant();

        // 1. Exact match
        var exact = candidates.FirstOrDefault(t => t.Title.ToLowerInvariant() == normalizedRef);
        if (exact != null) return exact;

        // 2. Contains match
        var contains = candidates.FirstOrDefault(t => t.Title.ToLowerInvariant().Contains(normalizedRef));
        if (contains != null) return contains;

        // 3. Fuzzy match
        TaskInstance? bestMatch = null;
        int bestDistance = int.MaxValue;

        foreach (var task in candidates)
        {
            int distance = ComputeLevenshteinDistance(normalizedRef, task.Title.ToLowerInvariant());
            if (distance < bestDistance && distance <= 3) // max 3 typos
            {
                bestDistance = distance;
                bestMatch = task;
            }
        }

        return bestMatch;
    }
}
