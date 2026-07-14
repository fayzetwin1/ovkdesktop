using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ovkdesktop.Helpers
{
    public static class ApiPerformanceTracker
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<string, Measurement> Measurements = new();

        public static void Record(string scope, string transport, long elapsedMilliseconds)
        {
            lock (Sync)
            {
                var key = $"{scope}:{transport}";
                if (!Measurements.TryGetValue(key, out var measurement))
                    measurement = new Measurement();

                measurement.TotalMilliseconds += elapsedMilliseconds;
                measurement.Samples++;
                Measurements[key] = measurement;

                var currentAverage = measurement.TotalMilliseconds / (double)measurement.Samples;
                var comparison = BuildComparison(scope);
                Debug.WriteLine($"[API Performance] scope={scope}, transport={transport}, duration_ms={elapsedMilliseconds}, average_ms={currentAverage:0}, samples={measurement.Samples}{comparison}");
            }
        }

        public static string GetSummary()
        {
            lock (Sync)
            {
                var scopes = Measurements.Keys
                    .Select(key => key[..key.LastIndexOf(':')])
                    .Distinct()
                    .OrderBy(scope => scope)
                    .ToArray();
                var result = new StringBuilder();

                foreach (var scope in scopes)
                {
                    if (!Measurements.TryGetValue($"{scope}:execute", out var execute) ||
                        !Measurements.TryGetValue($"{scope}:legacy", out var legacy))
                        continue;

                    var executeAverage = execute.TotalMilliseconds / (double)execute.Samples;
                    var legacyAverage = legacy.TotalMilliseconds / (double)legacy.Samples;
                    if (executeAverage <= 0 || legacyAverage <= 0)
                        continue;

                    var speedup = legacyAverage / executeAverage;
                    var verdict = speedup >= 1
                        ? $"execute быстрее в {speedup:0.00}×"
                        : $"execute медленнее в {1 / speedup:0.00}×";
                    result.AppendLine($"{GetScopeName(scope)}: {executeAverage:0} мс против {legacyAverage:0} мс — {verdict}.");
                }

                return result.Length > 0
                    ? result.ToString().TrimEnd()
                    : "Для сравнения отключите execute, обновите нужный экран несколько раз, затем включите execute и повторите замер.";
            }
        }

        private static string GetScopeName(string scope) => scope switch
        {
            "feed.initial_ready" => "Готовая лента",
            "profile.initial_ready" => "Готовый профиль",
            "group.initial_ready" => "Готовое сообщество",
            "feed.next_page_ready" => "Следующая страница ленты",
            _ => scope
        };

        private static string BuildComparison(string scope)
        {
            if (!Measurements.TryGetValue($"{scope}:execute", out var execute) ||
                !Measurements.TryGetValue($"{scope}:legacy", out var legacy))
                return string.Empty;

            var executeAverage = execute.TotalMilliseconds / (double)execute.Samples;
            var legacyAverage = legacy.TotalMilliseconds / (double)legacy.Samples;
            if (executeAverage <= 0 || legacyAverage <= 0)
                return string.Empty;

            var speedup = legacyAverage / executeAverage;
            return $", execute_avg_ms={executeAverage:0}, legacy_avg_ms={legacyAverage:0}, execute_speedup={speedup:0.00}x";
        }

        private sealed class Measurement
        {
            public long TotalMilliseconds { get; set; }
            public int Samples { get; set; }
        }
    }
}
