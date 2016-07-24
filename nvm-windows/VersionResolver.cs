using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace nvm_windows
{
    class VersionResolver
    {
        public static NodeVersion Resolve(string input)
        {
            return Resolve(NodeReq.GetVersions(), input);
        }

        public static NodeVersion Resolve(List<NodeVersion> versions, string input)
        {
            string semVerString = input.TrimStart('v');
            string[] components = semVerString.Split('.');
            if (semVerString.Length == 0)
            {
                return null;
            } else if (new Regex(@"^[0-9]+$").IsMatch(semVerString))
            {
                return Resolve(versions, Int32.Parse(components[0]));
            }
            else if (new Regex(@"^[0-9]+\.[0-9]+$").IsMatch(semVerString))
            {
                return Resolve(versions, Int32.Parse(components[0]), Int32.Parse(components[1]));
            }
            else if (new Regex(@"^[0-9]+\.[0-9]\.[0-9]+$").IsMatch(semVerString))
            {
                return Resolve(versions, Int32.Parse(components[0]), Int32.Parse(components[1]), Int32.Parse(components[2]));
            } else
            {
                return ResolveByName(versions, input);
            }
        }

        private static NodeVersion Resolve(List<NodeVersion> versions, int major)
        {
            NodeVersion target = null;
            foreach (NodeVersion v in versions)
            {
                if (v.GetSemVer().Major == major
                    && (target == null || target.GetSemVer().CompareByPrecedence(v.GetSemVer()) < 0))
                {
                    target = v;
                }
            }
            return target;
        }

        private static NodeVersion Resolve(List<NodeVersion> versions, int major, int minor)
        {
            NodeVersion target = null;
            foreach (NodeVersion v in versions)
            {
                if (v.GetSemVer().Major == major && v.GetSemVer().Minor == minor
                    && (target == null || target.GetSemVer().CompareByPrecedence(v.GetSemVer()) < 0))
                {
                    target = v;
                }
            }
            return target;
        }

        private static NodeVersion Resolve(List<NodeVersion> versions, int major, int minor, int patch)
        {
            NodeVersion target = null;
            foreach (NodeVersion v in versions)
            {
                if (v.GetSemVer().Major == major && v.GetSemVer().Minor == minor && v.GetSemVer().Patch == patch
                    && (target == null || target.GetSemVer().CompareByPrecedence(v.GetSemVer()) < 0))
                {
                    target = v;
                }
            }
            return target;
        }

        private static NodeVersion ResolveByName(List<NodeVersion> versions, string name)
        {
            switch (name)
            {
                case "latest":
                    versions.Sort();
                    return versions[0];
            }
            return null;
        }
    }
}
