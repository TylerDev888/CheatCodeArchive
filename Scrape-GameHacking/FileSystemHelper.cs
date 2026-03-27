using System.Text.RegularExpressions;

namespace GameHackingScraper
{
    internal static class FileSystemHelper
    {
        private static readonly Regex InvalidPathChars = new(@"[<>:""/\\|?*\x00-\x1f]");

        /// <summary>
        /// Strips characters that are illegal in directory/file names.
        /// Mirrors the Python safe_name() function.
        /// </summary>
        public static string SafeName(string text)
            => InvalidPathChars.Replace(text, "").Trim(' ', '.');

        /// <summary>
        /// Writes <paramref name="content"/> to <paramref name="path"/> only when
        /// the file does not already contain identical content.
        /// Mirrors the Python ensure_yaml() function.
        /// </summary>
        public static void EnsureYaml(string path, string content, bool dryRun)
        {
            if (dryRun)
            {
                Console.WriteLine($"[DRY-RUN] Would write: {path}");
                return;
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path, System.Text.Encoding.UTF8);
                if (existing == content)
                    return;
            }

            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
            Console.WriteLine($"  Written: {path}");
        }
    }
}
