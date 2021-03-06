﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace nvm_windows
{
    class List
    {
        public static void Run(ListOptions opts)
        {
            List<NodeVersion> versions;
            if (opts.Remote)
            {
                versions = NodeReq.GetVersions();
            } else
            {
                versions = GetLocalVersions();
            }
            Console.WriteLine("Available Node Versions");
            Console.WriteLine("");
            
            foreach (NodeVersion v in versions)
            {
                if (!opts.Remote || opts.All || v.GetSemVer().Major != 0)
                {
                    Console.WriteLine(v.Version);
                }
            }
        }

        public static List<NodeVersion> GetLocalVersions()
        {
            string[] dirs = Directory.GetDirectories(Utils.GetNodeContainer());
            List<NodeVersion> versions = new List<NodeVersion>();
            foreach (string versionPath in dirs)
            {
                NodeVersion tmp = new NodeVersion();
                tmp.Version = Path.GetFileName(versionPath);
                if (tmp.Version.StartsWith("v"))
                {
                    versions.Add(tmp);
                }
            }
            versions.Sort();
            return versions;
        }
    }
}
