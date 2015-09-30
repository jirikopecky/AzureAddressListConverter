using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AzureAddressListConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Usage("Required argument not supplied");
                return;
            }

            if (!File.Exists(args[0]))
            {
                Usage("Specified file does not exists");
            }

            XDocument document = null;
            using (var stream = File.Open(args[0], FileMode.Open, FileAccess.Read))
            using (var reader = XmlReader.Create(stream))
            {
                document = XDocument.Load(reader);
            }

            var addresses = document.Element("AzurePublicIpAddresses");

            if (addresses == null)
            {
                Console.Error.WriteLine("ERROR: Wrong input file format! Missing AzurePublicIpAddresses as root element.");
                return;
            }

            foreach (var region in addresses.Elements("Region"))
            {
                var regionName = region.Attribute("Name")?.Value;

                var rangeGroups = region.Elements("IpRange").Select(((element, i) => new {Group = i/150, Value = element}))
                    .GroupBy(item => item.Group, item => item.Value);

                int counter = 0;

                foreach (var group in rangeGroups)
                {
                    var regionFileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Path.GetTempPath(),
                    $"PublicIPs_{regionName}_{group.Key}.csv");

                    using (var file = File.Open(regionFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(file, Encoding.ASCII))
                    {
                        writer.WriteLine(
                            "Source Starting IP,Source Ending IP,Destination Starting IP,Destination Ending IP ,Ports");

                        foreach (var range in group)
                        {
                            var subnetAttribute = range.Attribute("Subnet");
                            var network = IPNetwork.Parse(subnetAttribute.Value);

                            writer.WriteLine("{0},{1},1.1.1.1,1.1.1.1,TCP-111", network.FirstUsable, network.LastUsable);
                            counter++;
                        }
                    }
                }

                Console.WriteLine("Region {0}: {1} address ranges", regionName, counter);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void Usage(string reason = null)
        {
            var exeName = Path.GetFileName(Assembly.GetEntryAssembly().Location);

            if (reason != null)
            {
                Console.WriteLine("ERROR!");
                Console.WriteLine();
                Console.WriteLine(reason);
                Console.WriteLine();
            }

            Console.WriteLine("Usage:");
            Console.WriteLine();
            Console.WriteLine("\t{0} azure-ips.xml", exeName);
            Console.WriteLine();
            Console.WriteLine("\tazure-ips.xml - The publicly available list of Azure address ranges");
        }
    }
}
