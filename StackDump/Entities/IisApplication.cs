using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StackDump.Entities
{
    public class IisApplication
    {
        public string Name { get; set; }
        public string AppPool { get; set; }

        public static Regex CreateFromPattern = new Regex(@"APP ""(?<name>[^""]+)/"" \((?<parameters>[^)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<IisApplication> CreateFrom(IEnumerable<string> appCmdOutput)
        {
            return appCmdOutput.Select(CreateFrom).ToList();
        }

        public static IisApplication CreateFrom(string appCmdOutputLine)
        {
            var match = CreateFromPattern.Match(appCmdOutputLine);

            if (!match.Success) {
                return null;
            }

            var parameters = match.Groups["parameters"].Value.Split(',').Select(keyValuePair => keyValuePair.Split(':')).ToDictionary(keyValuePair => keyValuePair[0], keyValuePair => keyValuePair[1]);

            return new IisApplication
            {
                Name = match.Groups["name"].Value,
                AppPool = parameters["applicationPool"]
            };
        }
    }
}
