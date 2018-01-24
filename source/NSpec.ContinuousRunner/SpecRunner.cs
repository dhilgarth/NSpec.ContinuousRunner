using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NSpec.ContinuousRunner
{
    public class SpecRunner
    {
        private readonly string _runnerPath;
        private readonly string _pathToSpecDll;
        private readonly IEnumerable<string> _runnerArguments;

        public SpecRunner(string runnerPath, string pathToSpecDll, IEnumerable<string> runnerArguments)
        {
            _runnerPath = runnerPath ?? throw new ArgumentNullException(nameof(runnerPath));
            _pathToSpecDll = pathToSpecDll ?? throw new ArgumentNullException(nameof(pathToSpecDll));
            _runnerArguments = runnerArguments ?? throw new ArgumentNullException(nameof(runnerArguments));
        }

        public void RunSpecs()
        {
            var runnerArguments = new List<string> { _pathToSpecDll };
            runnerArguments.AddRange(_runnerArguments);
            var startInfo = new ProcessStartInfo(_runnerPath, string.Join(" ", runnerArguments.Select(x => $"\"{x}\"")))
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(_runnerPath),
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(startInfo);
            process.OutputDataReceived += (_, eventArgs) => Console.WriteLine(eventArgs.Data);
            process.ErrorDataReceived += (_, eventArgs) => Console.Error.WriteLine(eventArgs.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }
    }
}