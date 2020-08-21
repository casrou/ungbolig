using HtmlAgilityPack;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using ungdomsbolig.Shared;

namespace ungdomsbolig
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var creds = DetermineCredentials();
            var (client, name) = SearchHelper.GetLoggedInClientAndName(creds);

            var waitingListTask = SearchHelper.GetAllPagesAndParseAsync(client, "/user/apartments");            
            var searchResultsTask = SearchHelper.DetermineSearchResultsAsync(client);
            var waitingList = await waitingListTask;
            var searchResults = await searchResultsTask;
            Console.WriteLine($"Waitinglist: {waitingList.Count()} houses");
            Console.WriteLine($"Total: {searchResults.Count()} houses");

            while (true)
            {
                Console.WriteLine("\n--- NEW SEARCH ---");

                Console.Write("max. rent (fx. 7500)\n> ");
                var temp = Console.ReadLine();
                var maxRent = int.Parse(string.IsNullOrEmpty(temp)? "99999" : temp);

                Console.Write("min. size m2 (fx. 50)\n> ");
                temp = Console.ReadLine();
                var minSize = int.Parse(string.IsNullOrEmpty(temp) ? "0" : temp);

                Console.Write("min. type (https://www.ungdomsboligaarhus.dk/s%C3%B8gekoder-og-boligtyper)\n> ");
                temp = Console.ReadLine();
                var minType = int.Parse(string.IsNullOrEmpty(temp) ? "0" : temp);
                Console.Write($"max. type (https://www.ungdomsboligaarhus.dk/s%C3%B8gekoder-og-boligtyper)\n> ");
                temp = Console.ReadLine();
                var maxType = int.Parse(string.IsNullOrEmpty(temp) ? "100" : temp);


                var results = searchResults
                    .Where(sr => sr.Rent <= maxRent)
                    .Where(sr => sr.Size >= minSize)
                    .Where(sr => sr.Type >= minType && sr.Type <= maxType)
                    .Where(sr => !waitingList.Contains(sr));
                foreach (var r in results)
                {
                    Console.WriteLine($"{r.Description} - {r.Rent} kr. - {r.Size} m2 - {r.WaitingPeriod} - {r.Url}");
                }
            }
        }

        private static Credentials DetermineCredentials()
        {
            string filename = "credentials.json";
            if (File.Exists(filename))
            {
                Console.Write("Use saved credentials? (yes/no)\n> ");
                var searchOrFile = Console.ReadLine();
                if (searchOrFile.ToLower().Trim() == "yes")
                {
                    var json = File.ReadAllText(filename);
                    return JsonSerializer.Deserialize<Credentials>(json);
                }
            }

            var creds = new Credentials();
            Console.WriteLine("Application number:"); Console.Write("> ");
            creds.Name = Console.ReadLine();
            Console.WriteLine("Password:"); Console.Write("> ");
            creds.Password = Console.ReadLine();
            SearchHelper.SaveJson(creds, filename);
            return creds;
        }
    }
}
