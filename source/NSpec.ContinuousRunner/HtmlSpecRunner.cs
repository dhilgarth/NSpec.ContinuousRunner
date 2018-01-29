using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace NSpec.ContinuousRunner
{
    public class HtmlSpecRunner : SpecRunner
    {
        private const string HtmlContainerTemplate = @"<html style=""width: 100%; height: 100%; margin:0; padding: 0;"">
<body style=""width: 100%; height: 100%; margin:0; padding: 0;"">
<iframe style=""width: 100%; height: 100%; border: 0; display:none"" id=""report""></iframe>
<div style=""width: 100%; height: 100%; border: 0;"" id=""fallback"" style=""display:block"">Specs haven't run yet</div>
<script type=""text/javascript"">
    var iframe = document.getElementById('report');
    var iframeSrc = '{0}';
    var fallback = document.getElementById('fallback');
    var connection = new WebSocket('ws://localhost:42123/refresh');
    connection.onmessage = function() {{
        iframe.src = iframeSrc;
        iframe.style.display = 'block';
        fallback.style.display = 'none';
    }}
</script>
</body>
</html>";

        private const string UrlPrefix = "http://localhost:42123";
        private readonly string _reportPath;
        private readonly WebServer _server;
        private readonly NeedsRefreshWebSocketsServer _webSocketsServer;
        private readonly List<string> _htmlReportLines = new List<string>();
        private string _containerUrl;

        public HtmlSpecRunner(string runnerPath, string pathToSpecDll, IEnumerable<string> runnerArguments)
            : base(runnerPath, pathToSpecDll, runnerArguments)
        {
            var reportsPath = Path.Combine(Path.GetTempPath(), "NSpec.ContinuousRunner", "HtmlReports");
            Directory.CreateDirectory(reportsPath);
            var reportFileName = Path.GetFileNameWithoutExtension(pathToSpecDll) + ".html";
            var reportContainerFileName = Path.GetFileNameWithoutExtension(pathToSpecDll) + ".container.html";
            _reportPath = Path.Combine(reportsPath, reportFileName);
            _server = WebServer.Create(UrlPrefix).WithStaticFolderAt(reportsPath, useDirectoryBrowser: true);
            _server.RegisterModule(new WebSocketsModule());
            _webSocketsServer = new NeedsRefreshWebSocketsServer();
            _server.Module<WebSocketsModule>().RegisterWebSocketsServer("/refresh", _webSocketsServer);

            if (File.Exists(_reportPath))
                File.Delete(_reportPath);

            File.WriteAllText(
                Path.Combine(reportsPath, reportContainerFileName),
                string.Format(HtmlContainerTemplate, $"{UrlPrefix}/{reportFileName}"));

            _server.RunAsync();

            _containerUrl = $"{UrlPrefix}/{reportContainerFileName}";
            Process.Start(_containerUrl);
        }

        public override void RunSpecs()
        {
            _htmlReportLines.Clear();
            DeleteIfNecessary();
            base.RunSpecs();
            Console.WriteLine($"Done. See {_containerUrl} for the results.");
            File.WriteAllLines(_reportPath, _htmlReportLines);
            NotifyClientsOfNeededRefresh();
        }

        protected override void OnOutputDataReceived(string data)
        {
            _htmlReportLines.Add(data);
        }

        private void DeleteIfNecessary()
        {
            try
            {
                if (File.Exists(_reportPath))
                    File.Delete(_reportPath);
            }
            catch (IOException)
            {
                try
                {
                    Thread.Sleep(1000);
                    if (File.Exists(_reportPath))
                        File.Delete(_reportPath);
                }
                catch (IOException)
                {

                }
            }
        }

        private void NotifyClientsOfNeededRefresh()
        {
            _webSocketsServer.Refresh();
        }
    }
}