using System.Text;

namespace Fragile.Sample.Basic.Combine
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Combine Sample");
            Console.WriteLine("======================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            //

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}