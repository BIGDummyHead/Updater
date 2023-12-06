using Newtonsoft.Json;
using System.Text;

namespace UpdatePusher
{

    internal static class EasyJson
    {
        internal static async Task<J?> ReadFileAsync<J>(string path, Encoding? encoding = default, CancellationToken canToken = default)
        {
            string content = encoding is not null ? await File.ReadAllTextAsync(path, encoding, canToken) : await File.ReadAllTextAsync(path, canToken);
            return JsonConvert.DeserializeObject<J>(content);
        }
    }
}
