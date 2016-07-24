using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace nvm_windows
{
    class Utils
    {
        public static string GetContainer()
        {
            return CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "nvm-windows"));
        }

        public static string GetNodeContainer()
        {
            return CreateDirectory(Path.Combine(GetContainer(), "node"));
        }

        public static string GetNodeVersionContainer(NodeVersion v)
        {
            return CreateDirectory(Path.Combine(GetNodeContainer(), v.Version));
        }

        public static string GetNPMContainer()
        {
            return CreateDirectory(Path.Combine(GetContainer(), "npm"));
        }

        public static string GetNPMVersionContainer(NodeVersion v)
        {
            return CreateDirectory(Path.Combine(GetNPMContainer(), v.Npm));
        }

        public static string CreateDirectory(string dirPath)
        {
            Directory.CreateDirectory(dirPath);
            return dirPath;
        }

        public static bool NodeDownloaded(NodeVersion v)
        {
            return File.Exists(Path.Combine(GetNodeContainer(), v.Version, "node.exe"));
        }

        public static bool NPMDownloaded(NodeVersion v)
        {
            return File.Exists(Path.Combine(GetNodeContainer(), v.Version, "npm.cmd"));
        }

        public static NodeVersion getCurrentNodeVersion()
        {
            string nodeVersionPath = Path.Combine(GetContainer(), "node-version");
            if (!File.Exists(nodeVersionPath))
            {
                return null;
            }
            string lastVersion = File.ReadAllText(nodeVersionPath);
            NodeVersion tmp = new NodeVersion();
            tmp.Version = lastVersion;
            if (NodeDownloaded(tmp))
            {
                return tmp;
            }
            return null;
        }

        public static NodeVersion getLocalNPMRCVersion()
        {
            string npmrc = Path.Combine(Directory.GetCurrentDirectory(), ".npmrc");
            if (!File.Exists(npmrc)) return null;
            NodeVersion tmp = new NodeVersion();
            tmp.Version = File.ReadAllText(npmrc);
            return tmp;
        }
    }
}
