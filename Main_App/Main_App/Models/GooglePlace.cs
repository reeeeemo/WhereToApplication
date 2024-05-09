using System;
using Newtonsoft.Json.Linq;

/* FORKED FROM https://gist.github.com/rdelrosario/ec505c66616aec743531ecb2aa05932e*/
namespace Main_App.Models
{
    public class GooglePlace
    {
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Raw { get; set; }

        public GooglePlace(JObject jsonObject)
        {
            Name = (string)jsonObject["result"]["name"];
            Latitude = (double)jsonObject["result"]["geometry"]["location"]["lat"];
            Longitude = (double)jsonObject["result"]["geometry"]["location"]["lng"];
            Raw = jsonObject.ToString();
        }

    }
}