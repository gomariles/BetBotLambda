using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BetBotLambda.Models
{
    public class SportEvent
    {
        public string Id { get; set; }
        public Sport Sport { get; set; }
        public string Team1 { get; set; }
        public string Team2 { get; set; }
        public string EventName { get; set; }
        public List<string> Synonyms { get; set; }

    }
}
