using System.Diagnostics;
using System.IO.Compression;

public class Program
{
    public static string? restartingProgram;
    public static string? zip;
    public static string? dest;
    public const int CONFINED_ARG_COUNT = 5;
    public static int Main(params string[] args)
    {
        if (args.Length == 0)
        {
            string? zip = ExpectInput("ZIP File: ", ConsoleColor.Magenta);
            string? dest = ExpectInput("Destination: ", ConsoleColor.Magenta);
            string? pid = ExpectInput("Process ID (Optional): ", ConsoleColor.Magenta);
            string? waitInput = ExpectInput("Wait For Input true/false: ", ConsoleColor.Magenta);
            string? res = ExpectInput("Program to restart (Optional): ", ConsoleColor.Magenta);

            if (res is null)
                res = string.Empty;

            if (zip is null || dest is null)
            {
                printCLR("Cannot leave ZIP or Destination null.", ConsoleColor.Red);
                return -100;
            }

            pid ??= "";
            waitInput ??= "true";


            return UnzipPack(zip, dest, pid, waitInput, res);
        }

        return UnzipPack(args);
    }

    static string? ExpectInput(string msg = "", ConsoleColor clr = ConsoleColor.White)
    {
        Console.ForegroundColor = clr;
        Console.Write(msg);
        Console.ResetColor();
        return Console.ReadLine();
    }

    static bool waitInput;
    static Stopwatch watch = new Stopwatch();
    //0 - zip file
    //1 - where to unpack
    //2 - Process ID Caller to kill
    public static int UnzipPack(params string[] args)
    {
        if (args.Length != CONFINED_ARG_COUNT)
        {
            printCLR($"Cannot begin unzip without correct Arg Length...", ConsoleColor.Red);
            printCLR($"Required: {CONFINED_ARG_COUNT}", ConsoleColor.Green);
            printCLR($"Provided: {args.Length}", ConsoleColor.DarkRed);
            return ExitCode(-1);
        }

        bool.TryParse(args[3], out waitInput);
        restartingProgram = args[4];

        string pidS = args[2];
        if (!string.IsNullOrEmpty(pidS) && int.TryParse(pidS, out int pid))
        {
            try
            {
                Process? caller = Process.GetProcessById(pid);
                caller.EnableRaisingEvents = true;
                caller.Exited += Caller_Exited;


                watch.Start();
                printCLR($"Waiting for '{caller.ProcessName}' to exit...", ConsoleColor.DarkYellow);

                caller.Refresh();
                if (!caller.HasExited)
                    caller.Kill();
            }
            catch
            {
                callerExited = true;
                printCLR($"Did not find '{pid}' as a running process.", ConsoleColor.DarkYellow);
            }

            printCLR($"Check caller exit... {callerExited}", ConsoleColor.Magenta);
        }
        else
            callerExited = true;

        while (!callerExited)
        {
            if (watch.Elapsed.TotalSeconds >= 5)
            {
                printCLR("5 second tick: Waiting for calling process to exit...", ConsoleColor.DarkYellow);
                watch.Restart();
            }
        }
        watch.Stop();


        zip = args[0];

        if (!File.Exists(zip) || Path.GetExtension(zip) != ".zip")
        {
            printCLR($"'{zip}' either does NOT EXIST or is NOT A ZIP file");
            return ExitCode(-3);
        }

        dest = args[1];

        if (!Directory.Exists(dest))
        {
            Directory.CreateDirectory(dest);
            printCLR("Created '{dest}' as it did not exist", ConsoleColor.DarkYellow);
        }

        try
        {
            ZipFile.ExtractToDirectory(zip, dest, true);
        }
        catch (Exception ex)
        {
            printCLR($"Extraction of '{zip}' was NOT successful! \n--------|  {ex.Message}", ConsoleColor.Red);
            return ExitCode(-4);
        }

        printCLR("Extraction success!", ConsoleColor.Green);
        File.Delete(zip);
        printCLR("Deleted .zip file as extraction was successful.", ConsoleColor.Yellow);

        if(File.Exists(restartingProgram))
            Process.Start(restartingProgram);

        return ExitCode(1);
    }

    static int ExitCode(int code)
    {
        Console.Write("Press any key to exit...");
        if (waitInput)
            Console.ReadKey();
        return code;
    }

    private static bool callerExited = false;
    private static void Caller_Exited(object? sender, EventArgs e)
    {
        callerExited = true;
        printCLR("Calling Process exited!", ConsoleColor.Green);
    }

    static void printCLR(object? msg, ConsoleColor clr = ConsoleColor.White)
    {
        Console.ForegroundColor = clr;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

}
