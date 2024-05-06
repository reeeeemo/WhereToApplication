using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Main_App.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Main_App.Services
{
    public interface IGoogleMapsApiService
    {
        Task<GooglePlaceAutoCompleteResult> GetPlaces(string text);
        Task<GooglePlace> GetPlaceDetails(string placeId);
    }

    public class GoogleMapsApiService : IGoogleMapsApiService
    {
        static string _gmapsKey;
        private const string baseAddress = "https://maps.googleapis.com/maps/";

        public static void Initialize(string gmapsKey)
        {
            _gmapsKey = gmapsKey;
        }

        private HttpClient CreateClient()
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseAddress)
            };

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return httpClient;
        }

        public async Task<GooglePlaceAutoCompleteResult> GetPlaces(string text)
        {
            GooglePlaceAutoCompleteResult results = null;

            using (var httpClient = CreateClient())
            {
                var response = await httpClient.GetAsync($"api/place/autocomplete/json?input={Uri.EscapeUriString(text)}&key={_gmapsKey}").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(json) && json != "ERROR")
                    {
                        results = await Task.Run(() =>
                           JsonConvert.DeserializeObject<GooglePlaceAutoCompleteResult>(json)
                        ).ConfigureAwait(false);

                    }
                }
            }

            return results;
        }

        public async Task<GooglePlace> GetPlaceDetails(string placeId)
        {
            GooglePlace result = null;
            using (var httpClient = CreateClient())
            {
                var response = await httpClient.GetAsync($"api/place/details/json?placeid={Uri.EscapeUriString(placeId)}&key={_gmapsKey}").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(json) && json != "ERROR")
                    {
                        result = new GooglePlace(JObject.Parse(json));
                    }
                }
            }

            return result;
        }
    }
}
