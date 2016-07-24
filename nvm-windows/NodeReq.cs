using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Semver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace nvm_windows
{
    class NodeReq
    {
        private static string MakeRequest(string contextPath)
        {
            WebRequest request = WebRequest.Create(
              "https://nodejs.org/dist/" + contextPath);
            WebResponse response = request.GetResponse();

            Stream dataStream = response.GetResponseStream();

            StreamReader reader = new StreamReader(dataStream);

            string responseFromServer = reader.ReadToEnd();

            reader.Close();
            response.Close();
            return responseFromServer;
        }

        private static List<NodeVersion> versions = null;

        public static List<NodeVersion> GetVersions()
        {
            if (versions == null)
            {
                versions = JsonConvert.DeserializeObject<List<NodeVersion>>(MakeRequest("index.json"));
            }
            return versions;
        }
    }

    class NodeVersion
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
    }
}
