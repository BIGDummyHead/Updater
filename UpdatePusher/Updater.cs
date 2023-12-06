using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace UpdatePusher
{
    /// <summary>
    /// This class makes using the components of UpdatePusher seamless.
    /// </summary>
    public sealed class Updater
    {
        /// <summary>
        /// Defaults to the current working Directory
        /// </summary>
        public string WorkingDirectory { get; private set; } = Directory.GetCurrentDirectory();

        /// <summary>
        /// UpdateUnpacker.dll
        /// </summary>
        public string DllFile => $"{WorkingDirectory}\\UpdateUnpacker.dll";
        /// <summary>
        /// UpdateUnpacker.exe
        /// </summary>
        public string ExeFile => $"{WorkingDirectory}\\UpdateUnpacker.exe";
        /// <summary>
        /// UpdateUnpacker.runtimeconfig.json
        /// </summary>
        public string RuntimeConfigFile => $"{WorkingDirectory}\\UpdateUnpacker.runtimeconfig.json";

        /// <summary>
        /// The version we are trying to update to
        /// </summary>
        public Version Update { get; init; }
        /// <summary>
        /// Will the program restart afterwards?
        /// </summary>
        public bool Restarting { get; init; }
        /// <summary>
        /// The name of the update, this differs from the Version name. Mainly because it is the name of the zip file that will be unpacked at the time.
        /// </summary>
        public string UpdateName { get; init; }

        /// <summary>
        /// Create a new Updater. 
        /// </summary>
        /// <param name="update">Version to update to!</param>
        /// <param name="restartProgram">Restart program afterwards?</param>
        /// <param name="workingDir">The directory we are working in. If left null then <see cref="Directory.GetCurrentDirectory"/></param>
        /// <param name="updateName">The name of the update</param>
        public Updater(Version update, bool restartProgram = false, string? workingDir = null, string? updateName = null)
        {
            Update = update;
            WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory();
            UpdateName = updateName ?? "update.zip";
            Restarting = restartProgram;
        }

        /// <summary>
        /// Reports information back about the update in progress. 
        /// </summary>
        public readonly Progress UpdateProgress = new();

        /// <summary>
        /// Starts the update completly. <see cref="ZipData(string, bool, string, IProgress{string}?)"/>, <seealso cref="CreateProgramAsync(string, string, ProcessStartInfo?, IProgress{string}?)"/> and start the program all in one spot.
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            PurgeOldUpdate();
            UpdateProgress.Report("Checking/Deleting old update files.");
            UpdateProgress.Report("Update started.");
            string zip = await ZipData(UpdateName, true, WorkingDirectory, UpdateProgress);
            UpdateProgress.Report("Finished CopyFilesAsync method...");
            Process proc = await CreateProgramAsync(zip, WorkingDirectory, null, UpdateProgress);
            UpdateProgress.Report("Finished CreateProgramAsync method...");
            _ = proc.Start();
            UpdateProgress.Report("Starting program/Closing Environment...");

            Environment.Exit(0);

        }


        /// <summary>
        /// Removes the old UpdateUnpacker files from the Working Directory. Call at begining of Program to ensure those files are gone.
        /// </summary>
        public void PurgeOldUpdate()
        {
            ExistDelete(DllFile);
            ExistDelete(ExeFile);
            ExistDelete(RuntimeConfigFile);
        }

        private static void ExistDelete(string f)
        {
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }

        private static HttpClient Client => SharedHttp.Shared;


        /// <summary>
        /// Creates a temp folder, copy/download all Files/directories, zip, and moves to <paramref name="dest"/>
        /// </summary>
        /// <param name="name">Name of the zip </param>
        /// <param name="unpackZips">Unpack zips?</param>
        /// <param name="dest">Destination to place zip. Working dir by default.</param>
        /// <param name="progress">Progress to be reported</param>
        /// <returns>The path to the zip with all the new data.</returns>
        /// <exception cref="HttpRequestException"/>
        public async Task<string> ZipData(string name, bool unpackZips, string dest = "", IProgress<string>? progress = null)
        {
            if (Update.Files.Length < 1)
            {
                progress?.Report("No files to download/copy");
                return "";
            }

            string tmpDir = $"{Path.GetTempPath()}{Update.VersionName}_{Update.VersionNumber}\\";

            //create tmp folder for download
            if (Directory.Exists(tmpDir))
            {
                Directory.Delete(tmpDir, true);
            }

            _ = Directory.CreateDirectory(tmpDir);
            progress?.Report($"Created temp Directory... {tmpDir}");

            progress?.Report("Parsing Http Links...");
            foreach (string l in Update.HttpFiles)
            {
                progress?.Report("Downloading " + l);
                string linkFileName = Path.Combine(tmpDir, Path.GetFileName(l));

                HttpResponseMessage httpRep = await Client.GetAsync(l);
                _ = httpRep.EnsureSuccessStatusCode();

                byte[] data = await Client.GetByteArrayAsync(l);
                await File.WriteAllBytesAsync(linkFileName, data);

                if (unpackZips)
                {
                    UnpackZip(linkFileName, progress);
                }
            } //handle any links

            progress?.Report("Finished Http downloads... Parsing directories to copy...");
            foreach (string dir in Update.Directories)
            {
                progress?.Report(dir);
                string tDir = Path.Combine(tmpDir, Path.GetFileName(dir));
                _ = Directory.CreateDirectory(tDir);
                CopyFolder(dir, tDir);
            }

            progress?.Report("Finished Directory copy... Parsing files to copy...");
            foreach (string fil in Update.AbsoluteFiles)
            {
                progress?.Report(fil);
                string fPath = Path.Combine(tmpDir, Path.GetFileName(fil));
                File.Copy(fil, fPath, true);

                if (unpackZips)
                {
                    UnpackZip(fPath, progress);
                }
            }

            progress?.Report($"Finished gathering data to Temp directory... Compressing data");
            string createdZip = Path.Combine(dest, name);

            if (File.Exists(createdZip))
                File.Delete(createdZip);

            ZipFile.CreateFromDirectory(tmpDir, createdZip, CompressionLevel.Optimal, false);
            progress?.Report("Deleting Temp directory...");
            Directory.Delete(tmpDir, true); //ensure deletion
            progress?.Report("Complete!");

            return createdZip;
        }

        /// <summary>
        /// Extract a Zip file
        /// </summary>
        /// <param name="f">Path to file</param>
        /// <param name="p"></param>
        /// <exception cref="Exception"></exception>
        public static void UnpackZip(string f, IProgress<string>? p)
        {
            if (Path.GetExtension(f) != ".zip")
            {
                p?.Report("File is not a .zip");
                return;
            }

            string? parent = Directory.GetParent(f)?.FullName;
            if (string.IsNullOrEmpty(parent))
            {
                p?.Report($"Could not unpack '{f}' because the parent was empty.");
                return;
            }
            p?.Report("Zip file unpacking to Temp Dir...");
            ZipFile.ExtractToDirectory(f, parent, true);
            p?.Report("Done! Removing zip...");
            File.Delete(f);
        }

        /// <summary>
        /// Copies a folders content to a source and destination
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        public void CopyFolder(string source, string dest)
        {
            string[] files = Directory.GetFiles(source);
            string[] dirs = Directory.GetDirectories(source);

            foreach (string f in files)
            {
                File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
            }

            foreach (string d in dirs)
            {
                string recurringDest = $"{dest}\\{Path.GetFileName(Path.GetFileName(d))}";

                if (!Directory.Exists(recurringDest))
                {
                    _ = Directory.CreateDirectory(recurringDest);
                }

                CopyFolder(d, recurringDest);
            }
        }

        private static async Task FCreate(string f, byte[] write)
        {
            Stream s = File.Create(f);
            await s.WriteAsync(write);
            s.Close();
        }

        /// <summary>
        /// Creates the UpdateUnpacker program.
        /// </summary>
        /// <param name="zipFile">Zip file we plan on unpacking. A file path</param>
        /// <param name="destination">Destination we plan on placing zip contents</param>
        /// <param name="psi">Process Start Info for UpdateUnpacker we plan on overriding with, if any.</param>
        /// <param name="prog">Progress to report.</param>
        /// <returns></returns>
        public async Task<Process> CreateProgramAsync(string zipFile, string destination, ProcessStartInfo? psi = null, IProgress<string>? prog = null)
        {
            prog?.Report("Creating... " + DllFile);
            await FCreate(DllFile, FileData.Dll);

            prog?.Report("Creating... " + ExeFile);
            await FCreate(ExeFile, FileData.Exe);

            prog?.Report("Creating... " + RuntimeConfigFile);
            string runJson = JsonConvert.SerializeObject(
                    new RunTimeConfig("net6.0", "Microsoft.NETCore.App", "6.0.0"));
            await FCreate(RuntimeConfigFile, Encoding.UTF8.GetBytes(runJson));

            if (psi is null)
            {
                psi = new()
                {
                    FileName = ExeFile,
                    Arguments = $"{zipFile} {destination} {Environment.ProcessId} false {Process.GetCurrentProcess().MainModule?.FileName}",
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                prog?.Report("Generated Process Start Info");
            }

            prog?.Report("Process ready to start...");
            return new Process
            {
                StartInfo = psi
            };
        }


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        internal class RunTimeConfig
        {
            public RunTimeConfig(string tfo, string name, string ver)
            {
                rto = new()
                {
                    tfm = tfo,
                    framework = new()
                    {
                        name = name,
                        version = ver
                    }
                };

            }

            [JsonProperty("runtimeOptions")]
            public RunTimeOptions rto { get; set; }


        }

        internal class RunTimeOptions
        {
            public string tfm;
            [JsonProperty("framework")]
            public Framework framework { get; set; }
        }

        internal class Framework
        {
            public string name;
            public string version;
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        /// <summary>
        /// String progress.
        /// </summary>
        public class Progress : IProgress<string>
        {
            /// <summary>
            /// An event that is called when progress is reported.
            /// </summary>
            public event Action<string>? OnProgress;
            /// <summary>
            /// Report any progress made on update.
            /// </summary>
            /// <param name="value"></param>
            public virtual void Report(string value)
            {
                OnProgress?.Invoke(value);
            }
        }

    }
}
