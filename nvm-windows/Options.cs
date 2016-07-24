using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace nvm_windows
{
    [Verb("list", HelpText = "List available versions")]
    class ListOptions
    {
        [Option]
        public bool Remote { get; set; }
    }

    [Verb("install", HelpText = "Install given version")]
    class InstallOptions
    {
        [Value(0, Required=true, MetaName="Target Version")]
        public string Version { get; set; }
    }

    [Verb("use", HelpText = "Use given version")]
    class UseOptions
    {
        [Value(0, Required = true, MetaName = "Target Version")]
        public string Version { get; set; }
    }

    [Verb("__setup_link__", HelpText = "Set's up a temporary CMD link.  ")]
    class LinkOptions
    {
        [Value(0, Required = true, MetaName = "Link ID")]
        public string LinkID { get; set; }
    }
}
