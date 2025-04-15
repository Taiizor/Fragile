using Fragile.Core;
using Fragile.Metadata;
using Fragile.Models;
using System.Text;

namespace Fragile.Sample.Basic.Compression
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Compression Sample");
            Console.WriteLine("======================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            ...

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}