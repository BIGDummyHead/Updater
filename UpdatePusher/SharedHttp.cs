using System.Net.NetworkInformation;

namespace UpdatePusher
{
    /// <summary>
    /// Used so that several <see cref="HttpClient"/> don't have to be opened.
    /// </summary>
    public static class SharedHttp
    {
        private static HttpClient? _shared;
        /// <summary>
        /// Shared client.
        /// </summary>
        public static HttpClient Shared
        {
            get
            {
                if (_shared is null)
                    _shared = new HttpClient();

                return _shared;
            }
        }

        /// <summary>
        /// google.com, youtube.com, bing.com
        /// </summary>
        public static readonly string[] commonHost = new string[3] { "google.com", "youtube.com", "bing.com" };

        /// <summary>
        /// Uses the CheckInternet method with the <see cref="commonHost"/> as the host.
        /// </summary>
        public static bool HasInternet => CheckInternet(commonHost);

        /// <summary>
        /// Checks if there is an internet connection by performing a series of Pings to the host
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        public static bool CheckInternet(string[]? host)
        {
            host ??= commonHost;
            try
            {
                Ping myPing = new();
                PingOptions pingOptions = new();
                foreach (string h in host)
                {
                    byte[] buffer = new byte[32];
                    PingReply reply = myPing.Send(h, 1000, buffer, pingOptions);

                    if (reply.Status == IPStatus.Success)
                        return true;
                }

                return false;

            }
            catch
            {
                return false;
            }
        }
    }
}
