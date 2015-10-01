using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using static System.Console;

namespace AzureAddressListConverter
{
    internal class Program
    {
        private const int MaxRulesPerCsvFile = 135;

        public static void Main(string[] args)
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

            XDocument document;
            using (var stream = File.Open(args[0], FileMode.Open, FileAccess.Read))
            using (var reader = XmlReader.Create(stream))
            {
                document = XDocument.Load(reader);
            }

            var addresses = document.Element("AzurePublicIpAddresses");

            if (addresses == null)
            {
                Error.WriteLine("ERROR: Wrong input file format! Missing AzurePublicIpAddresses as root element.");
                return;
            }

            var inputFile = Path.GetFileNameWithoutExtension(args[0]);
            var outputDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Path.GetTempPath();

            WriteLine("Creating CSV files from XML file {0}.", inputFile);
            WriteLine("CSV record count limit: {0}", MaxRulesPerCsvFile);
            WriteLine("Output directory: {0}", outputDir);
            WriteLine();

            foreach (var region in addresses.Elements("Region"))
            {
                var regionName = region.Attribute("Name")?.Value;

                WriteRegionStatus(regionName, "Processing...");

                var rangeGroups = region.Elements("IpRange").Select(((element, i) => new {Group = i/MaxRulesPerCsvFile, Value = element}))
                    .GroupBy(item => item.Group, item => item.Value);

                int counter = 0;
                int fileCounter = 0;

                foreach (var group in rangeGroups)
                {
                    var outputFile = $"{inputFile}_{regionName}_{group.Key}.csv";
                    WriteRegionStatus(regionName, $"=> {outputFile}", ConsoleColor.Blue);

                    var regionFileName = Path.Combine(outputDir, outputFile);

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

                    fileCounter++;
                }

                WriteRegionStatus(regionName, $"Finished: {counter} address ranges ({fileCounter} file(s))", ConsoleColor.DarkGreen);
                WriteLine();
            }
            
            //WriteLine("Press any key to continue...");
            //ReadKey();
        }

        private static void Usage(string reason = null)
        {
            var exeName = Path.GetFileName(Assembly.GetEntryAssembly().Location);

            if (reason != null)
            {
                WriteLine("ERROR!");
                WriteLine();
                WriteLine(reason);
                WriteLine();
            }

            WriteLine("Usage:");
            WriteLine();
            WriteLine("\t{0} azure-ips.xml", exeName);
            WriteLine();
            WriteLine("\tazure-ips.xml - The publicly available list of Azure address ranges");
        }

        private static void WriteRegionStatus(string regionName, string statusText, ConsoleColor? color = null)
        {
            var origColor = ForegroundColor;
            ForegroundColor = ConsoleColor.DarkYellow;
            Write("[{0}] ", regionName);

            ForegroundColor = color ?? origColor;
            WriteLine(statusText);

            if (color.HasValue)
            {
                ForegroundColor = origColor;
            }
        }
    }
}
