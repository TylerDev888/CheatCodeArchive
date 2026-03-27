using System.Text;

namespace GameHackingScraper
{
    internal sealed record CheatEntry(string Name, string Author, string Notes, IReadOnlyList<string> Codes);

    internal static class YamlBuilder
    {
        /// <summary>Double-quote-escapes a string value for YAML output.</summary>
        private static string YamlStr(string value)
        {
            var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"\"{escaped}\"";
        }

        /// <summary>
        /// Renders a cheats.yaml string from structured data.
        /// Mirrors the Python build_cheats_yaml() function.
        /// </summary>
        public static string Build(
            string game,
            string console,
            string region,
            string serial,
            string cheatType,
            string sourceUrl,
            IReadOnlyList<CheatEntry> cheats)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {game} cheat data");
            sb.AppendLine($"# Console: {console}");
            sb.AppendLine($"# Region:  {region}");
            if (!string.IsNullOrEmpty(serial))
                sb.AppendLine($"# Serial:  {serial}");
            sb.AppendLine($"# Source:  {sourceUrl}");
            sb.AppendLine();
            sb.AppendLine($"game: {YamlStr(game)}");
            sb.AppendLine($"console: {YamlStr(console)}");
            sb.AppendLine($"region: {YamlStr(region)}");
            if (!string.IsNullOrEmpty(serial))
                sb.AppendLine($"serial: {YamlStr(serial)}");
            sb.AppendLine($"cheat_type: {YamlStr(cheatType)}");
            sb.AppendLine($"source: {YamlStr(sourceUrl)}");
            sb.AppendLine();
            sb.AppendLine("cheats:");

            foreach (var cheat in cheats)
            {
                sb.AppendLine($"  - name: {YamlStr(cheat.Name)}");
                if (!string.IsNullOrEmpty(cheat.Author))
                    sb.AppendLine($"    author: {YamlStr(cheat.Author)}");
                if (!string.IsNullOrEmpty(cheat.Notes))
                    sb.AppendLine($"    notes: {YamlStr(cheat.Notes)}");
                if (cheat.Codes.Count > 0)
                {
                    sb.AppendLine("    codes:");
                    foreach (var code in cheat.Codes)
                        sb.AppendLine($"      - {YamlStr(code)}");
                }
            }

            return sb.ToString();
        }
    }
}
