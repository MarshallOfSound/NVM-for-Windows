using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace nvm_windows
{
    class NodeVersion : IComparable
    {
        public string Version { get; set; }
        public string Npm { get; set; }

        private SemVersion SemVer = null;

        public SemVersion GetSemVer()
        {
            if (SemVer == null)
            {
                SemVer = SemVersion.Parse(Version.TrimStart('v'));
            }
            return SemVer;
        }

        public int CompareTo(object obj)
        {
            NodeVersion nv;
            try
            {
                nv = (NodeVersion)obj;
            }
            catch
            {
                return 0;
            }
            return nv.GetSemVer().CompareByPrecedence(this.GetSemVer());
        }
    }
}
