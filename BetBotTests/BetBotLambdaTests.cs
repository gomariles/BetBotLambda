using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http.Headers;
namespace BetBotTests
{
    [TestClass]
    public class BetBotLambdaTests
    {
        [TestMethod]
        public async Task AskSports_OK()
        {
            string result = await DoGetHttpWebRequest("bets");
            IEnumerable<Sport> sports = JsonConvert.DeserializeObject<IEnumerable<Sport>>(result);
        }

        public async Task<string> DoGetHttpWebRequest(string url)
        {
            HttpClient client = BuildHttpClient();
            string requestUrl = $"https://betbotdemo.azurewebsites.net/api/{url}";
            HttpResponseMessage response = await client.GetAsync(requestUrl);

            return await response.Content.ReadAsStringAsync();
        }

        private HttpClient BuildHttpClient()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        public class Sport
        {
            public string Name { get; set; }
        }
    }
}
