using RainbowSix.WebClient.Interfaces;

namespace GameHackingScraper
{
    /// <summary>Logs to the console, supporting verbose mode toggling.</summary>
    internal sealed class ConsoleLogger : IHttpConnectionLogger
    {
        private readonly bool _verbose;

        public ConsoleLogger(bool verbose = false)
        {
            _verbose = verbose;
        }

        public void Write(string message)
        {
            if (_verbose)
                Console.Write(message);
        }

        public void WriteLine(string message)
        {
            if (_verbose)
                Console.WriteLine(message);
        }
    }
}
