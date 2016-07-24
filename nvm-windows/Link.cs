using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace nvm_windows
{
    class Link
    {
        public static void Run(LinkOptions opts)
        {
            NodeVersion current = Utils.getCurrentNodeVersion();
            LinkWithID(current, opts.LinkID);
            if (current != null)
            {
                Console.WriteLine("Now using Node.JS " + current.Version);
            }
        }

        public static void LinkWithID(NodeVersion v, string linkID)
        {
            string containerPath = Utils.GetContainer();
            string linkPath = Path.Combine(containerPath, ".links", "link-" + linkID);
            Directory.CreateDirectory(linkPath);

            List<string> nodeLink = new List<string>();
            nodeLink.Add("@echo OFF");
            if (v != null) {
                nodeLink.Add(Path.Combine(Utils.GetNodeVersionContainer(v), "node.exe") + " %*");
            } else
            {
                nodeLink.Add("echo No Node.JS version installed.  Use 'nvm install [version]' to install one");
            }
            File.WriteAllLines(Path.Combine(linkPath, "node.cmd"), nodeLink);

            List<String> npmLink = new List<string>();
            npmLink.Add("@echo OFF");
            if (v != null)
            {
                npmLink.Add(Path.Combine(Utils.GetNodeVersionContainer(v), "npm.cmd") + " %*");
            } else
            {
                npmLink.Add("echo No Node.JS version installed.  Use 'nvm install [version]' to install one");
            }

            EnsurePrefixSet();

            File.WriteAllLines(Path.Combine(linkPath, "npm.cmd"), npmLink);
        }

        public static void EnsurePrefixSet()
        {
            string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string npmrcPath = Path.Combine(userDir, ".npmrc");
            string prefixConf = "prefix=" + Path.Combine(Utils.GetNodeContainer(), "bin");

            if (File.Exists(npmrcPath))
            {
                string[] lines = File.ReadAllLines(npmrcPath);
                List<string> newLines = new List<string>();
                bool found = false;
                foreach (string line in lines)
                {
                    if (new Regex(@"^prefix=").IsMatch(line))
                    {
                        found = true;
                        newLines.Add(prefixConf);
                    } else
                    {
                        newLines.Add(line);
                    }
                }
                if (!found)
                {
                    newLines.Add(prefixConf);
                }
                File.WriteAllLines(npmrcPath, newLines);
            } else
            {
                File.WriteAllText(npmrcPath, prefixConf);
            }
        }
    }
}
