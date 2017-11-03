using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using Alexa.NET.Response;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response.Directive;
using Alexa.NET;
using BetBotLambda.Models;
using System.Collections;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BetBotLambda
{
    public class Function
    {
        private const string BASE_URL = "https://betbotdemo.azurewebsites.net/api/";        

        public SkillResponse FunctionHandler(SkillRequest input, ILambdaContext context)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                SkillResponse response;
                var log = context.Logger;

                if (input.GetRequestType() == typeof(LaunchRequest))
                {
                    DoPostHttpWebRequest("bets").Wait();
                    response = ResponseBuilder.Tell(new PlainTextOutputSpeech { Text = "Hey! Let's go to bet. You can ask I want to do a bet." });

                    return response;

                }
                else //Intent request
                {
                    builder.AppendLine("IntentRequest");
                    IntentRequest request = input.Request as IntentRequest;
                    if (request.Intent.Name == "Bet")
                    {
                        return BetIntent(request);

                    }
                    else if (request.Intent.Name == "MyBets")
                    {
                        return MyBetsIntent();
                    }

                    response = ResponseBuilder.Tell(new PlainTextOutputSpeech { Text = "This option is not valid" });

                    return response;
                }
            }
            catch (Exception x)
            {
                var response = new ResponseBody
                {
                    ShouldEndSession = true,
                    OutputSpeech = new PlainTextOutputSpeech { Text = $"There was an error: {x.Message}" }
                };

                SkillResponse skillResponse = new SkillResponse();
                skillResponse.Response = response;
                skillResponse.Version = "string";

                return skillResponse;
            }

        }

        private SkillResponse MyBetsIntent()
        {
            IEnumerable<Bet> myBets = GetMyBets().Result;
            SkillResponse response = ResponseBuilder.Tell(new PlainTextOutputSpeech { Text = "These are your bets" + string.Join(", ", myBets.Select(b => $"Event: {b.EventName}. Result: {b.BetResult}").ToArray()) });

            return response;
        }

        private SkillResponse BetIntent(IntentRequest request)
        {
            string responseText = string.Empty;
            string sportName = request.Intent.Slots["sport"].Value;
            string eventName = request.Intent.Slots["eventName"].Value;
            string eventBet = request.Intent.Slots["eventBet"].Value;
            string betResult = request.Intent.Slots["result"].Value;

            if (string.IsNullOrEmpty(sportName)) //If sport doesn't have value, ask.
            {
                return AskForSportSlot(request);
            }
            else //sport has value
            {
                if (string.IsNullOrEmpty(eventName)) //If eventName doesn't have value,  check correct sport and ask for eventName
                {
                    IEnumerable<Sport> sports = GetSportsAsync().Result;
                    Sport selectedSport = GetSelectedSport(sports, sportName);
                    if (selectedSport == null) //No correct sport, ask sport again
                    {
                        responseText = $"We don't have available bets for this sport, please select one from: {string.Join(", ", sports.Select(s => s.Name).ToArray())}";
                        return ResponseBuilder.DialogElicitSlot(new PlainTextOutputSpeech { Text = responseText }, "sport", request.Intent);
                    }
                    else //Correct sport, ask for event name
                    {
                        return AskForEventNameSlot(request, selectedSport.Name);
                    }
                }
                else //EvetName has value
                {
                    if (string.IsNullOrEmpty(eventBet))
                    {
                        IEnumerable<SportEvent> events = GetSportEventsAsync(sportName).Result;
                        SportEvent selectedEvent = GetSelectedEvent(events, eventName);
                        if (selectedEvent == null) //Check valid eventName value. If not valid, ask again
                        {
                            responseText = $"This bet is not available, please select one from: {string.Join(", ", events.Select(s => s.EventName).ToArray())}";
                            return ResponseBuilder.DialogElicitSlot(new PlainTextOutputSpeech { Text = responseText }, "eventName", request.Intent);
                        }
                        else //Correct eventName, ask for eventBet
                        {
                            return AskForEventBetSlot(request, selectedEvent.EventName, selectedEvent.Id);
                        }
                    }
                    else //EventBet has value
                    {
                        if (string.IsNullOrEmpty(betResult))
                        {
                            IEnumerable<SportEvent> events = GetSportEventsAsync(sportName).Result;
                            SportEvent sportEvent = GetSelectedEvent(events, eventName);
                            IEnumerable<string> eventBets = GetEventBets(sportEvent.Id).Result;
                            if (!eventBets.Any(s => string.Equals(s, eventBet, StringComparison.OrdinalIgnoreCase))) //Check valid eventBet value. If not valid, ask again
                            {
                                responseText = $"This bet is not available, please select one from: {string.Join(", ", eventBets.ToArray())}";
                                return ResponseBuilder.DialogElicitSlot(new PlainTextOutputSpeech { Text = responseText }, "eventBet", request.Intent);
                            }
                            else //Correct eventBet, ask for result
                            {
                                Intent updatedIntent = request.Intent;
                                updatedIntent.ConfirmationStatus = ConfirmationStatus.None;
                                return AskForResultSlot(request, eventBet);
                            }
                        }
                        else
                        {
                            if (request.Intent.ConfirmationStatus == ConfirmationStatus.None)
                            {
                                responseText = $"Do you want to confirm the bet for {eventName} with result {betResult}?";
                                return ResponseBuilder.DialogConfirmIntent(new PlainTextOutputSpeech { Text = responseText }, request.Intent);
                            }
                            else if (request.Intent.ConfirmationStatus == ConfirmationStatus.Denied)
                            {
                                Intent updatedIntent = request.Intent;
                                updatedIntent.Slots["result"].Value = null;
                                updatedIntent.ConfirmationStatus = ConfirmationStatus.None;
                                return AskForResultSlot(request, eventBet, updatedIntent);
                            }
                            else
                            {
                                IEnumerable<SportEvent> events = GetSportEventsAsync(sportName).Result;
                                SportEvent sportEvent = events.Single(e => e.EventName.ToUpper().Contains(eventName.ToUpper()));
                                Bet bet = new Bet
                                {
                                    BetResult = betResult,
                                    BetType = eventBet,
                                    EventId = sportEvent.Id,
                                    EventName = eventName
                                };
                                DoPutHttpWebRequest("bets", bet).Wait();

                                return ResponseBuilder.Tell("You have submitted your bet! Thank you!");
                            }
                        }
                    }
                }
            }
        }

        private Sport GetSelectedSport(IEnumerable<Sport> sports, string sportName)
        {
            Sport selectedSport = sports.SingleOrDefault
                (s => s.Name.Trim().ToUpper().Contains(sportName.Trim().ToUpper())
                || s.Synonyms.Any(sym => sym.Trim().ToUpper().Contains(sportName.Trim().ToUpper())));

            return selectedSport;
        }

        private SportEvent GetSelectedEvent(IEnumerable<SportEvent> events, string eventName)
        {
            SportEvent selectedEvent = events.SingleOrDefault
                (s => s.EventName.Trim().ToUpper().Contains(eventName.Trim().ToUpper())
                || s.Synonyms.Any(sym => sym.Trim().ToUpper().Contains(eventName.Trim().ToUpper())));

            return selectedEvent;
        }

        private SkillResponse AskForEventNameSlot(IntentRequest request, string sportName)
        {
            IEnumerable<SportEvent> events = GetSportEventsAsync(sportName).Result;

            Intent updatedIntent = request.Intent;
            updatedIntent.Slots["sport"].Value = sportName;

            string responseText = $"You have chosen {sportName}. These events are avaible for bet: {string.Join(", ", events.Select(s => s.EventName).ToArray())}";
            SkillResponse response = ResponseBuilder.DialogElicitSlot(new PlainTextOutputSpeech { Text = responseText }, "eventName", updatedIntent);

            return response;
        }

        private SkillResponse AskForResultSlot(IntentRequest request, string eventBet, Intent updatedIntent = null)
        {
            Intent intent = request.Intent;
            if (updatedIntent != null)
            {
                intent = updatedIntent;
            }
            string responseText = $"You have chosen {eventBet}. What is you result?";
            SkillResponse response = ResponseBuilder.DialogElicitSlot(new PlainTextOutputSpeech { Text = responseText }, "result", intent);

            return response;
        }

        private SkillResponse AskForEventBetSlot(IntentRequest request, string eventName, string eventId)
        {
            IEnumerable<string> eventBets = GetEventBets(eventId).Result;
            Intent updatedIntent = request.Intent;
            updatedIntent.Slots["eventName"].Value = eventName;
            if (eventBets.Count() == 1)
            {
                updatedIntent.Slots["eventBet"].Value = eventBets.First();
                return AskForResultSlot(request, eventBets.First(), updatedIntent);
            }
            else
            {
                string responseText = $"You have chosen {eventName}. You can select one of these kind of bets: {string.Join(", ", eventBets.ToArray())}";
                SkillResponse response = ResponseBuilder.DialogElicitSlot(new PlainTextOutputSpeech { Text = responseText }, "eventBet", request.Intent);

                return response;
            }
        }

        private SkillResponse AskForSportSlot(IntentRequest request)
        {
            IEnumerable<Sport> sports = GetSportsAsync().Result;
            string responseText = $"Ok. You can select these sports: {string.Join(", ", sports.Select(s => s.Name).ToArray())}";
            return ResponseBuilder.DialogElicitSlot(new PlainTextOutputSpeech { Text = responseText }, "sport", request.Intent);
        }

        private async Task<string> DoGetHttpWebRequest(string url)
        {
            HttpClient client = BuildHttpClient();
            string requestUrl = $"{BASE_URL}/{url}";
            HttpResponseMessage response = await client.GetAsync(requestUrl);

            return await response.Content.ReadAsStringAsync();
        }

        private async Task<IEnumerable<Sport>> GetSportsAsync()
        {
            string result = await DoGetHttpWebRequest("bets");
            IEnumerable<Sport> sports = JsonConvert.DeserializeObject<IEnumerable<Sport>>(result);

            return sports;
        }

        private async Task<IEnumerable<SportEvent>> GetSportEventsAsync(string sportName)
        {
            string result = await DoGetHttpWebRequest($"bets/events/{sportName}");
            IEnumerable<SportEvent> events = JsonConvert.DeserializeObject<IEnumerable<SportEvent>>(result);

            return events;
        }

        private async Task<IEnumerable<string>> GetEventBets(string eventId)
        {
            string result = await DoGetHttpWebRequest($"bets/{eventId}");
            IEnumerable<string> eventBets = JsonConvert.DeserializeObject<IEnumerable<string>>(result);

            return eventBets;
        }

        private async Task<IEnumerable<Bet>> GetMyBets()
        {
            string result = await DoGetHttpWebRequest("me/bets");
            IEnumerable<Bet> eventBets = JsonConvert.DeserializeObject<IEnumerable<Bet>>(result);

            return eventBets;
        }

        private async Task<string> DoPostHttpWebRequest(string url)
        {
            HttpClient client = BuildHttpClient();
            string requestUrl = $"{BASE_URL}/{url}";
            HttpResponseMessage response = await client.PostAsync(requestUrl, null);

            return await response.Content.ReadAsStringAsync();
        }



        private async Task<string> DoPutHttpWebRequest(string url, Bet bet)
        {
            HttpClient client = BuildHttpClient();
            string requestUrl = $"{BASE_URL}/{url}";
            HttpContent content = new StringContent(JsonConvert.SerializeObject(bet), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PutAsync(requestUrl, content);

            return await response.Content.ReadAsStringAsync();
        }

        private HttpClient BuildHttpClient()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }
    }
}
