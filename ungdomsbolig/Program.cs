using HtmlAgilityPack;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;

namespace ungdomsbolig
{
    class Program
    {
        static void Main(string[] args)
        {
            var creds = DetermineCredentials();
            var client = GetLoggedInClient(creds);

            var waitingList = GetAllPagesAndParse(client, "/user/apartments");
            Console.WriteLine($"Waitinglist: {waitingList.Count()} houses");
            var searchResults = DetermineSearchResults(client);

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
            SaveJson(creds, filename);
            return creds;
        }

        private static IEnumerable<ILivable> DetermineSearchResults(RestClient client)
        {
            string filename = "searchresults.json";
            if (File.Exists(filename))
            {
                Console.Write("Update all houses? (yes/no)\n> ");
                var searchOrFile = Console.ReadLine();
                if (searchOrFile.ToLower().Trim() == "no")
                {
                    var json = File.ReadAllText(filename);
                    return JsonSerializer.Deserialize<List<House>>(json).Select(h => (ILivable)h);
                }
            }

            var searchResults = GetAllPagesAndParse(client, "/search");
            SaveJson(searchResults, filename);

            return searchResults;
        }

        private static void SaveJson(object toSerialize, string name)
        {
            var json = JsonSerializer.Serialize(toSerialize);
            File.WriteAllText(name, json);
        }

        public static List<ILivable> GetAllPagesAndParse(RestClient client, string resource)
        {
            var content = GetRequestContent(client, resource);

            var nextPage = CheckForNextPage(content);
            while (nextPage != null)
            {
                var nextHref = nextPage.FirstChild.Attributes["href"].Value;
                var temp = GetRequestContent(client, nextHref);
                nextPage = CheckForNextPage(temp);
                content += temp;
            }

            return ParseResults(content);
        }

        private static HtmlNode CheckForNextPage(string content)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            var next = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'pager-next')]");
            return next;
        }

        private static string GetRequestContent(RestClient client, string resource)
        {
            var request = new RestRequest(resource, Method.GET);
            IRestResponse response = client.Execute(request);
            return response.Content;
        }

        private static List<ILivable> ParseResults(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var result = new List<ILivable>();

            var place = doc.DocumentNode.SelectNodes("//table[contains(@class, 'views-table')]");
            if (place == null || place.Count() == 0) return result;

            foreach (var p in place)
            {
                var tempName = p.SelectSingleNode("caption/a/div").InnerText.Trim();
                var housings = p.SelectNodes("tbody/tr");
                var tempDesc = "";
                var tempUrl = "";
                foreach (var h in housings)
                {
                    if (!h.Attributes["class"].Value.Contains("sub-row"))
                    {
                        var temp = h.SelectSingleNode("td/a");
                        tempUrl = temp.Attributes["href"].Value;
                        tempDesc = temp.InnerText.Replace("\n", "-").Split("  ")
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Aggregate((acc, s) => acc += " " + s.Trim());
                        continue;
                    }

                    var house = new House();
                    house.Name = tempName;
                    house.Description = tempDesc;
                    house.Url = "https://www.ungdomsboligaarhus.dk" + tempUrl;
                    var tempType = h.SelectSingleNode("td[contains(@class, 'row-data-lejtyp')]").LastChild.InnerText.Trim();
                    house.Type = int.Parse(tempType);
                    house.Quantity = int.Parse(h.SelectSingleNode("td[contains(@class, 'row-data-antlej')]").InnerText.Trim());
                    house.WaitingPeriod = h.SelectSingleNode("td[contains(@class, 'row-data-vntmdrafd')]").InnerText.Trim();
                    house.Size = decimal.Parse(h.SelectSingleNode("td[contains(@class, 'row-data-area')]").InnerText.Trim());
                    house.Rent = decimal.Parse(h.SelectSingleNode("td[contains(@class, 'row-data-rent')]").InnerText.Trim());
                    house.DownPayment = decimal.Parse(h.SelectSingleNode("td[contains(@class, 'row-data-downpayment')]").InnerText.Trim());
                    house.FloorPlanUrl = h.SelectSingleNode("td[contains(@class, 'row-data-schematic')]")
                        .FirstChild.Attributes["href"]?.Value;
                    result.Add(house);
                }
            }            

            return result;
        }

        public static RestClient GetLoggedInClient(Credentials login)
        {
            var client = new RestClient("https://www.ungdomsboligaarhus.dk");
            client.CookieContainer = new System.Net.CookieContainer();
            client.Timeout = -1;
            var request = new RestRequest("/user", Method.POST);
            client.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.125 Safari/537.36";
            request.AddParameter("name", login.Name);
            request.AddParameter("pass", login.Password);
            request.AddParameter("form_build_id", "form-BRC8I_mLV5nuQOBnVYd05RApEbyIyXAMsVOPaxCWtaw");
            request.AddParameter("form_id", "user_login");
            request.AddParameter("op", "Log+ind");
            IRestResponse response = client.Execute(request);
            //Console.WriteLine(response.Content);
            return client;
        }        
    }
}
