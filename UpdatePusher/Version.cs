using Newtonsoft.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace UpdatePusher
{

    //the update should include a json version of this, 
    //this is the only plausable way of doing it 
    //mainly because it ensures that the update was a success.
    /// <summary>
    /// Json Serializable Version
    /// </summary>
    [JsonSerializable(typeof(Version))]
    public class Version
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        
        /// <summary>
        /// Name of the 
        /// </summary>
        [JsonProperty("productName")]
        public string ProductName { get; set; }

        /// <summary>
        /// Version number: 1.0.0.0
        /// </summary>
        [JsonProperty("version")]
        public string VersionNumber { get; set; }

        /// <summary>
        /// Name of the version.
        /// </summary>
        [JsonProperty("versionName")]
        public string VersionName { get; set; }


        /// <summary>
        /// Description of your update.
        /// </summary>
        [JsonProperty("desc")]
        public string Description { get; set; }

        /// <summary>
        /// Files/Dirs to download/copy
        /// </summary>
        [JsonProperty("files")]
        public string[] Files { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        /// <summary>
        /// Get files that match the required function.
        /// </summary>
        /// <param name="file">Type of Files you are looking for.</param>
        /// <returns></returns>
        public IEnumerable<string> GetFiles(Func<string, bool> file)
        {
            foreach (string f in Files)
            {
                if (file.Invoke(f))
                {
                    yield return f;
                }
            }
        }

        /// <summary>
        /// <see cref="GetFiles(Func{string, bool})"/> that are Uri formatted
        /// </summary>
        public IEnumerable<string> HttpFiles =>
                GetFiles(l => Uri.IsWellFormedUriString(l, UriKind.Absolute)
                              && IsValidFileName(Path.GetFileName(l)));

        /// <summary>
        /// <see cref="GetFiles(Func{string, bool})"/> that are directories.
        /// </summary>
        public IEnumerable<string> Directories =>
                GetFiles(Directory.Exists);

        /// <summary>
        /// <see cref="GetFiles(Func{string, bool})"/> that are files.
        /// </summary>
        public IEnumerable<string> AbsoluteFiles =>
                GetFiles(File.Exists);



        /// <summary>
        /// Retrives that Current working directories Version by reading the version file.
        /// </summary>
        /// <param name="nameNext">Name and Extension of the file. ex: ver.upd</param>
        /// <param name="enc">Encoding done to the JSON file</param>
        /// <param name="cToken">Cancellation token passed into reading the file.</param>
        /// <returns></returns>
        public static Task<Version?> GetWorkingDirectoryVersionAsync(string nameNext, Encoding? enc = null, CancellationToken cToken = default)
        {
            nameNext = Path.Combine(Directory.GetCurrentDirectory(), nameNext);
            if(File.Exists(nameNext))
                return EasyJson.ReadFileAsync<Version>(nameNext, enc, cToken);

            Task<Version?>? x = default;
            return Task.FromResult(x?.Result);
        }


        /// <summary>
        /// Gets the content of a .json file and tries to convert it into <see cref="Version"/>
        /// </summary>
        /// <param name="url">Absolute Url</param>
        /// <param name="webCan">Cancellation to stop reading the .txt file.</param>
        /// <returns>A deserialized <see cref="Version"/></returns>
        public static async Task<Version?> GetWebVersionAsync(string url, CancellationToken webCan = default)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                return null;
            }

            string content = await SharedHttp.Shared.GetStringAsync(url, webCan);

            return JsonConvert.DeserializeObject<Version?>(content);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns>A palatable way of displaying the Versions information</returns>
        public override string ToString()
        {
            return $"{ProductName}\n{VersionName}, {VersionNumber}. \n{Description}";
        }

        /// <summary>
        /// Create a new <see cref="Check"/> between a working and an incoming version
        /// </summary>
        /// <param name="working">The working/current <see cref="Version"/></param>
        /// <param name="incoming">The incoming <see cref="Version"/> to check</param>
        /// <returns>A cross check between two versions</returns>
        public static Check CrossCheck(Version working, Version incoming)
            => new(working, incoming);

        /// <summary>
        /// Create a new <see cref="Check"/> between this and an incoming version
        /// </summary>
        /// <param name="incoming">The incoming <see cref="Version"/> to check</param>
        /// <returns>A cross check between two versions</returns>
        public Check PerformCheck(Version incoming) =>
             CrossCheck(this, incoming);

        /// <summary>
        /// Checks if a file name is valid.
        /// </summary>
        /// <param name="fName"></param>
        /// <returns>True if the name is valid.</returns>
        public static bool IsValidFileName(string fName)
        {
            return fName.IndexOfAny(Path.GetInvalidFileNameChars()) < 1;
        }

        /// <summary>
        /// A cross check between two versions
        /// </summary>
        public struct Check
        {
            /// <summary>
            /// The working <see cref="Version"/>
            /// </summary>
            public Version Working { get; private set; }
            /// <summary>
            /// The incoming <see cref="Version"/>
            /// </summary>
            public Version Incoming { get; private set; }
            /// <summary>
            /// The newest <see cref="Version"/> between the Working and Incoming
            /// </summary>
            public Version Newest { get; private set; }

            /// <summary>
            /// Is the incoming older than the Working?
            /// </summary>
            public bool IsOlder => !IsNewer;
            /// <summary>
            /// Is the incoming newer than the Working?
            /// </summary>
            public bool IsNewer { get; private set; }
            /// <summary>
            /// Are the versions the same, in context to their Version #?
            /// </summary>
            public bool Same { get; private set; }

            /// <summary>
            /// Create a new check between two versions. The same as using <see cref="CrossCheck(Version, Version)"/>
            /// </summary>
            /// <param name="working">The working/current version</param>
            /// <param name="incoming">The incoming version</param>
            public Check(Version working, Version incoming)
            {
                Working = working;
                Incoming = incoming;

                Same = working.VersionNumber == incoming.VersionNumber;

                if (Same)
                {
                    IsNewer = false;
                    Newest = working;
                }
                else
                {
                    IsNewer = QuickCompare;
                    Newest = IsNewer ? Incoming : Working;
                }
            }

            private bool QuickCompare
                => CompareNew(VersionPoints(Working.VersionNumber), VersionPoints(Incoming.VersionNumber));

            private bool CompareNew(int[] currentVer, int[] incomingVer)
            {
                if (currentVer.Length != incomingVer.Length)
                {
                    throw new IndexOutOfRangeException($"Version Lengths must match. \nCurrent Version: {currentVer.Length} \nCompared Version: {incomingVer.Length}");
                }

                for (int i = 0; i < currentVer.Length; i++)
                {
                    if (incomingVer[i] > currentVer[i])
                    {
                        return true;
                    }
                }

                return false;
            }

            private readonly List<int> ls = new();
            private int[] VersionPoints(string x)
            {
                foreach (string verNumString in x.Split("."))
                {
                    if (int.TryParse(verNumString, out int res))
                    {
                        ls.Add(res);
                    }
                }
                int[] rt = ls.ToArray();
                ls.Clear();
                return rt;
            }
        }
    }
}
