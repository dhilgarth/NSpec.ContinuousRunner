using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NSpec.ContinuousRunner
{
    internal class Program
    {
        private static FileSystemWatcher _watcher;
        private static SpecRunner _specRunner;

        public static void Main(string[] args)
        {
            if ((args == null) || (args.Length == 0))
            {
                PrintUsage();
                return;
            }

            var pathToSpecDll = args[0];
            string runnerPath = null;
            var runnerArguments = new List<string>();
            if (args.Length > 1)
            {
                if (args[1].StartsWith("--runnerPath=", StringComparison.CurrentCultureIgnoreCase))
                {
                    runnerPath = args[1].Split('=')[1];
                    if (!File.Exists(runnerPath))
                    {
                        runnerPath = Path.Combine(runnerPath, "NSpecRunner.exe");
                        if (!File.Exists(runnerPath))
                            runnerPath = null;
                    }

                    runnerArguments.AddRange(args.Skip(2));
                }
                else
                    runnerArguments.AddRange(args.Skip(1));
            }

            if (runnerPath == null)
                runnerPath = DetectRunnerPath();
            if (runnerPath == null)
            {
                Console.WriteLine(
                    "Unable to detect path of NSpecRunner.exe. Please specify it manually with the --runnerPath argument. If you did, it couldn't be found at that location.");
            }

            _specRunner = CreateSpecRunner(runnerPath, pathToSpecDll, runnerArguments);
            _specRunner.RunSpecs();
            CreateFileSystemWatcher(pathToSpecDll);
            _watcher.EnableRaisingEvents = true;
            Console.ReadLine();
        }

        private static void CreateFileSystemWatcher(string pathToSpecDll)
        {
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(pathToSpecDll), Path.GetFileName(pathToSpecDll))
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Changed += OnSpecDllChanged;
            _watcher.Created += OnSpecDllChanged;
        }

        private static SpecRunner CreateSpecRunner(
            string runnerPath,
            string pathToSpecDll,
            IReadOnlyCollection<string> runnerArguments)
        {
            if (runnerArguments.Any(x => x.Equals("--formatter=HtmlFormatter", StringComparison.OrdinalIgnoreCase)))
                return new HtmlSpecRunner(runnerPath, pathToSpecDll, runnerArguments);
            return new SpecRunner(runnerPath, pathToSpecDll, runnerArguments);
        }

        /// <summary>
        ///     Assumes we have been added as a NuGet package or that NSpecRunner is in the same directory as we are.
        /// </summary>
        /// <returns>The path at which NSpecRunner.exe has been found, or null.</returns>
        private static string DetectRunnerPath()
        {
            var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var runnerPath = Path.Combine(baseDirectory, "NSpecRunner.exe");
            if (File.Exists(runnerPath))
                return runnerPath;

            var nuGetDirectory = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\.."));
            if (!Directory.EnumerateFiles(nuGetDirectory, "repositories.config").Any())
                return null;
            var nspecDirectory = Directory.EnumerateDirectories(nuGetDirectory)
                .FirstOrDefault(x => Regex.IsMatch(x, @"^nspec\.\d\.\d\.\d$"));
            if (nspecDirectory == null)
                return null;
            runnerPath = Path.Combine(nspecDirectory, "tools", "NSpecRunner.exe");
            if (File.Exists(runnerPath))
                return runnerPath;

            return null;
        }

        private static void OnSpecDllChanged(object sender, FileSystemEventArgs e)
        {
            _watcher.EnableRaisingEvents = false;
            Console.WriteLine("Spec dll has changed. Executing tests...");
            _specRunner.RunSpecs();
            _watcher.EnableRaisingEvents = true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine(
                "NSpec.ContinuousRunner <Path to spec dll> [--runnerPath=<Path to NSpecRunner.exe>] [NSpecRunner arguments]");
        }
    }
}