# Update Pusher

### Update Pusher, is a simple dll that is able check, compare, and update between two varying versions of a program. 


# Set up

Firstly, we need to make sure that we have a correct Json formatted file for our program to read.

Here is a basic example.

```json
{
	"productName" : "Beta Product",
	"version" : "0.0.0.1",
	"versionName" : "Beta",
	"desc" : "The first release of the beta.",
	"files" : [ "https://github.com/BIGDummyHead/Updater/raw/main/Example_Download_Files/update.zip" ]
}
```

- Note: Make sure you are filling out 'version' and 'files' accurately
- Note: In the files section we can use links, absolute files, and directories. This example utilizies a link.

Now that we have our Json formatted file we can move onto actually updating a program!

# Code 

```cs

using UpdatePusher;

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
```

In this example we are comparing two Json formatted files (ver.upd) and updating to the incoming version's update. Again, in this update we used a link so we checked if there was an internet connection!

And bam! It's done just like that we have introduced an update to our files. Now of course a lot goes on in the background of this! So what does happen?

1. We copy/download all the files we need for an update
2. Files are then placed into a temp folder (this is later deleted)
3. Files are then compressed and placed into the working directory (you get to decide)
4. A program called UpdateUnpacker(a zip file extractor that takes in args) is created
5. The program is then run and the compressed files are placed into the working directory
6. The original program is then restarted
