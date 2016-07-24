using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace nvm_windows
{
    class Program
    {
        static int Main(string[] args)
        {
            string[] tmp = { "use", "6" };
            var result = Parser.Default.ParseArguments<ListOptions, InstallOptions, UseOptions, LinkOptions>(args)
                .WithParsed<ListOptions>(opts => List.Run(opts))
                .WithParsed<InstallOptions>(opts => Install.Run(opts))
                .WithParsed<UseOptions>(opts => Use.Run(opts))
                .WithParsed<LinkOptions>(opts => Link.Run(opts))
                .WithNotParsed(errs => Console.WriteLine(errs));
            // Console.ReadLine();
            return 0;
        }
    }
}
