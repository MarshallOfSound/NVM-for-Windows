using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace nvm_windows
{
    class Link
    {
        public static void Run(LinkOptions opts)
        {
            LinkWithID(Utils.getCurrentNodeVersion(), opts.LinkID);
        }

        public static void LinkWithID(NodeVersion v, string linkID)
        {
            string containerPath = Utils.GetContainer();
            string linkPath = Path.Combine(containerPath, "link-" + linkID);
            Directory.CreateDirectory(linkPath);

            string[] nodeLink =
            {
                "@echo OFF",
                Path.Combine(Utils.GetNodeVersionContainer(v), "node.exe") + " %*"
            };
            File.WriteAllLines(Path.Combine(linkPath, "node.cmd"), nodeLink);

            string[] npmLink =
            {
                "@echo OFF",
                // Path.Combine(Utils.GetNodeVersionContainer(v), "npm.cmd") + " config set prefix %APPDATA%\nvm-windows\bin",
                Path.Combine(Utils.GetNodeVersionContainer(v), "npm.cmd") + " %*"
            };
            File.WriteAllLines(Path.Combine(linkPath, "npm.cmd"), npmLink);
        }
    }
}
