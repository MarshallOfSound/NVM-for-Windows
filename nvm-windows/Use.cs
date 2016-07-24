using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace nvm_windows
{
    class Use
    {
        public static void Run(UseOptions opts)
        {
            List<NodeVersion> versions = List.GetLocalVersions();

            NodeVersion target = VersionResolver.Resolve(versions, opts.Version);
            if (target == null)
            {
                Console.Error.WriteLine("No version matching: " + opts.Version + " found in installed versions.  Run 'nvm install " + opts.Version + "' to install it");
            } else
            {
                Link.LinkWithID(target, Environment.GetEnvironmentVariable("LINK_ID"));
                Console.WriteLine("Now using Node.JS " + target.Version);
            }
        }
    }
}
