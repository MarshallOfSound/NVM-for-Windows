using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using SevenZipLib;

namespace nvm_windows
{
    class Install
    {
        public static void Run(InstallOptions opts)
        {
            NodeVersion toInstall = VersionResolver.Resolve(opts.Version);
            if (toInstall == null)
            {
                Console.Error.WriteLine("Invalid version provided, couldn't resolve version \"" + opts.Version + "\"");
            } else
            {
                Download(toInstall);
            }
        }

        private static void Download(NodeVersion target)
        {
            if (!Utils.NodeDownloaded(target)) {
                Console.WriteLine("Downloading Node.JS version: " + target.Version);
                bool is64 = Environment.Is64BitOperatingSystem;
                string targetURL = "https://nodejs.org/dist/" + target.Version + "/win-" + (is64 ? "x64" : "x86") + "/node.exe";
                string fileName = Path.Combine(Utils.GetNodeVersionContainer(target), "node.exe");

                WebClient myWebClient = new WebClient();
                myWebClient.DownloadFile(targetURL, fileName);

                Console.WriteLine("Successfully Downloaded");
            }

            if (!Utils.NPMDownloaded(target))
            {
                Console.WriteLine("Downloading NPM version: " + target.Npm);
                string targetURL = "https://registry.npmjs.org/npm/-/npm-" + target.Npm + ".tgz";
                string fileName = Path.Combine(Utils.GetNPMVersionContainer(target), "npm.tgz");
                string targetFolder = Path.Combine(Utils.GetNodeVersionContainer(target), "node_modules");

                WebClient myWebClient = new WebClient();
                myWebClient.DownloadFile(targetURL, fileName);

                ExtractNPM(fileName, targetFolder);

                string npmExecPath = Path.Combine(Utils.GetNodeVersionContainer(target), "npm.cmd");
                if (File.Exists(npmExecPath)) File.Delete(npmExecPath);
                File.Copy(Path.Combine(targetFolder, "npm", "bin", "npm.cmd"), Path.Combine(Utils.GetNodeVersionContainer(target), "npm.cmd"));

                Console.WriteLine("Successfully Downloaded");
            }
        }

        private static void ExtractNPM(string fileName, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);

            using (SevenZipArchive archive = new SevenZipArchive(fileName))
            {
                archive.ExtractAll(targetFolder, ExtractOptions.OverwriteExistingFiles);
            }

            Directory.Move(Path.Combine(targetFolder, "package"), Path.Combine(targetFolder, "npm"));
        }
    }
}
