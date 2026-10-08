using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;

class Program
{
    static async Task Main()
    {
        using var client = new HttpClient();
        int[] ports = { 18001, 18002, 18003, 18004, 18005, 18006, 18007, 18008, 18010, 18000 };
        string[] names = { "accounts", "transactions", "users", "auth", "aadhar", "company", "notification", "payment", "registry", "gateway" };
        
        for (int i=0; i<ports.Length; i++)
        {
            try
            {
                Console.WriteLine($"Fetching from {ports[i]}...");
                string json = await client.GetStringAsync($"http://127.0.0.1:{ports[i]}/swagger/v1/swagger.json");
                File.WriteAllText($"{names[i]}_swagger.json", json);
                Console.WriteLine($"Saved {names[i]}_swagger.json ({json.Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed for {ports[i]}: {ex.Message}");
            }
        }
    }
}
