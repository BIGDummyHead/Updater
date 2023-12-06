using UpdatePusher;
using Version = UpdatePusher.Version;

public static class Program
{
    public static async Task<int> Main()
    {
        //check if we even have internet to do an update...
        if (SharedHttp.HasInternet)
        {
            //the working/current dir version
            Version? working = await Version.GetWorkingDirectoryVersionAsync("ver.upd");

            Console.WriteLine(working); //display working info.

            if (working is null)
                return exit("Working Version was not found...", -1);

            //get a version from the git
            Version? incoming = await Version
                .GetWebVersionAsync("https://github.com/BIGDummyHead/Updater/raw/main/Example_Download_Files/ver.upd");

            if (incoming is null)
                return exit("Web version you tried to request was not found...", -1);

            //cross check and figure out which version is new/old/same
            Version.Check vc = working.PerformCheck(incoming);

            //is the requested version newer then the working version?
            if (vc.IsNewer)
            {
                //set our params
                Updater up =
                    new(vc.Newest,
                    restartProgram: false,
                    workingDir: Directory.GetCurrentDirectory(),
                    updateName: "update.zip");

                //interested in seeing what is going on?
                up.UpdateProgress.OnProgress += UpdateProgress_OnProgress;

                //start update
                await up.StartAsync();
                //to save any troubles close out of the environment.
                //this isn't required but can save some time from the unpacker program.
                Environment.Exit(0);
            }
            else if (vc.IsOlder || vc.Same) //i know i dont need to use an || statement
                return exit("Update not required.", 1);

        }
        else
            return exit("No internet connection found to complete an update.", -1);

        Console.WriteLine("Update was successful!");
        Console.ReadKey();
        return 0;
    }

    private static void UpdateProgress_OnProgress(string obj)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(obj);
        Console.ResetColor();
    }

    static int exit(string msg, int code)
    {
        Console.WriteLine(msg);
        return code;
    }
}
