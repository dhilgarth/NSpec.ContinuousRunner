using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
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
    var connection = new WebSocket('ws://localhost:{1}/refresh');
    connection.onmessage = function() {{
        iframe.src = iframeSrc;
        iframe.style.display = 'block';
        fallback.style.display = 'none';
    }}
</script>
</body>
</html>";

        private readonly List<string> _htmlReportLines = new List<string>();
        private readonly string _reportPath;
        private readonly WebServer _server;
        private readonly NeedsRefreshWebSocketsServer _webSocketsServer;
        private readonly string _containerUrl;

        public HtmlSpecRunner(string runnerPath, string pathToSpecDll, IEnumerable<string> runnerArguments)
            : base(runnerPath, pathToSpecDll, runnerArguments)
        {
            var reportsPath = Path.Combine(Path.GetTempPath(), "NSpec.ContinuousRunner", "HtmlReports");
            Directory.CreateDirectory(reportsPath);
            var reportFileName = Path.GetFileNameWithoutExtension(pathToSpecDll) + ".html";
            var reportContainerFileName = Path.GetFileNameWithoutExtension(pathToSpecDll) + ".container.html";
            _reportPath = Path.Combine(reportsPath, reportFileName);
            var port = GetAvailablePort(42123);
            var urlPrefix = $"http://localhost:{port}";
            _server = WebServer.Create(urlPrefix).WithStaticFolderAt(reportsPath, useDirectoryBrowser: true);
            _server.RegisterModule(new WebSocketsModule());
            _webSocketsServer = new NeedsRefreshWebSocketsServer();
            _server.Module<WebSocketsModule>().RegisterWebSocketsServer("/refresh", _webSocketsServer);

            if (File.Exists(_reportPath))
                File.Delete(_reportPath);

            File.WriteAllText(
                Path.Combine(reportsPath, reportContainerFileName),
                string.Format(HtmlContainerTemplate, $"{urlPrefix}/{reportFileName}", port));

            _server.RunAsync();

            _containerUrl = $"{urlPrefix}/{reportContainerFileName}";
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

        private static int GetAvailablePort(int startingPort)
        {
            var portArray = new List<int>();

            var properties = IPGlobalProperties.GetIPGlobalProperties();

            //getting active connections
            var connections = properties.GetActiveTcpConnections();
            portArray.AddRange(from n in connections where n.LocalEndPoint.Port >= startingPort select n.LocalEndPoint.Port);

            //getting active tcp listners - WCF service listening in tcp
            var endPoints = properties.GetActiveTcpListeners();
            portArray.AddRange(endPoints.Where(n => n.Port >= startingPort).Select(n => n.Port));

            //getting active udp listeners
            endPoints = properties.GetActiveUdpListeners();
            portArray.AddRange(endPoints.Where(n => n.Port >= startingPort).Select(n => n.Port));

            portArray.Sort();

            for (var i = startingPort; i < ushort.MaxValue; i++)
            {
                if (!portArray.Contains(i))
                    return i;
            }

            return 0;
        }

        private void NotifyClientsOfNeededRefresh()
        {
            _webSocketsServer.Refresh();
        }
    }
}