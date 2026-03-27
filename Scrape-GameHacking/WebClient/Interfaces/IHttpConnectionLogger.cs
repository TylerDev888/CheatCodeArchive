namespace RainbowSix.WebClient.Interfaces
{
    public interface IHttpConnectionLogger
    {
        void Write(string message);
        void WriteLine(string message);
    }
}
