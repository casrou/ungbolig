using HtmlAgilityPack;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ungdomsbolig.Shared
{
    public static class SearchHelper
    {
        public static (RestClient, string) GetLoggedInClientAndName(Credentials login)
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

            var doc = new HtmlDocument();
            doc.LoadHtml(response.Content);
            var name = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'name')]/div/span");

            return (client, name.InnerText);
        }

        public static async Task<IEnumerable<ILivable>> DetermineSearchResultsAsync(RestClient client)
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

            List<ILivable> searchResults = await SearchAndSaveAsync(client, filename);

            return searchResults;
        }

        private static async Task<List<ILivable>> SearchAndSaveAsync(RestClient client, string filename)
        {
            var searchResults = await GetAllPagesAndParseAsync(client, "/search");
            SaveJson(searchResults, filename);
            return searchResults;
        }

        public static void SaveJson(object toSerialize, string filename)
        {
            var json = JsonSerializer.Serialize(toSerialize);
            File.WriteAllText(filename, json);
        }

        public static async Task<List<ILivable>> GetAllPagesAndParseAsync(RestClient client, string resource)
        {
            var content = await GetRequestContentAsync(client, resource);

            var nextPage = CheckForNextPage(content);
            while (nextPage != null)
            {
                var nextHref = nextPage.FirstChild.Attributes["href"].Value;
                var temp = await GetRequestContentAsync(client, nextHref);
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

        private static async Task<string> GetRequestContentAsync(RestClient client, string resource)
        {
            var request = new RestRequest(resource, Method.GET);
            IRestResponse response = await client.ExecuteAsync(request);
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
                        tempDesc = temp.InnerText.Trim(new char[] { ' ', '\n' }).Replace("\n", "-").Split("  ")
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
    }
}
