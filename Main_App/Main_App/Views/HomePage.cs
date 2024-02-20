using System;
using Xamarin.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xamarin.Essentials;
using Xamarin.Forms.Maps;
using System.Reflection;
using System.Globalization;
using Android.Icu.Text;
using Android.Provider;

[assembly: ExportFont("Lobster-Regular.ttf", Alias = "Lobster")]



namespace Main_App.Views
{
    #region Custom Classes
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    // Custom Vector2 class for latitude and longitude
    public class Vector2
    {
        private double lat, lon;

        public Vector2(double Ilat, double Ilon)
        {
            lat = Ilat;
            lon = Ilon;
        }
        public double GetLat() { return lat; }
        public double GetLon() { return lon; }
        public void SetLat(double value) { lat = value; }
        public void SetLon(double value) { lon = value; }
    }

    public class ValueSheet
    {
        private Dictionary<int, string> atts = new Dictionary<int, string>();
        int count = 0;
        int maxCount = 0;

        public ValueSheet(int c) 
        { 
            maxCount = c;
        }
        public void SetAtrribute(string value)
        {
            if (count >= maxCount)
            {
                return; // We cannot handle anymore values
            }
            atts.Add(count, value);
            count += 1;
        }
        public string GetAttribute(int index)
        {
            string value;
            atts.TryGetValue(index, out value);
            if (String.IsNullOrEmpty(value)) { return string.Empty; }
            return value;
        }
    }
    #endregion

    #region Custom Map Classes
    public class CustomPin : Pin
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
    public class CustomMap : Xamarin.Forms.Maps.Map
    {
       public List<CustomPin> CustomPins { get; set; }
    }
    #endregion
    public partial class HomePage : ContentPage
    {
        private List<ValueSheet> vehicles;
        private List<ValueSheet> routes;
        private List<ValueSheet> stops;
        Location userLocation;
        ImageButton[] select_buttons;
        Frame search_frame;

        public HomePage()
        {
            InitializeComponent();
            BindingContext = this;

            search_frame = (Frame)Content.FindByName("searchFrame");

            LoadImages();

            _ = SetUserMapLocation();

            if (userLocation != null)
            {
                map.MoveToRegion(MapSpan.FromCenterAndRadius(new Position(userLocation.Latitude, userLocation.Longitude), Distance.FromMiles(1)));
            }


            LoadTable();
            LoadMap();
        }

        private async Task SetUserMapLocation()
        {
            await GetCurrentLocation();
        }

        public void DimCurrentButton(object sender, EventArgs e)
        {
            foreach (ImageButton button in  select_buttons)
            {
                if (button != (ImageButton)sender)
                {
                    button.FadeTo(0.3);
                } else
                {
                    button.FadeTo(1);
                }
                if ((ImageButton)sender == select_buttons[0])
                {
                    search_frame.FadeTo(0);
                    search_frame.IsVisible = false;
                } else
                {
                    search_frame.FadeTo(0.7);
                    search_frame.IsVisible = true;
                }
            }
        }

        public void OpenSwipeMenu(object sender, EventArgs e)
        {
        }

        public void OnRouteTap(object sender, EventArgs e)
        {
            DisplayAlert("yeah", "galago", "cancel");
        }

        private void SetMapPageActive()
        {
             

        }

        private void SetSearchPageActive()
        {


        }

        #region GeoLocation Functions
        /* Function gets location via geolocation, then compares the data in the list by device's location*/
        private void CompareVehiclesByLocation(Location user_location)
        {
            vehicles.Sort(delegate(ValueSheet x, ValueSheet y)
            {
                if (x == null && y == null) return 0;
                else if (x == null) return -1;
                else if (y == null) return 1;
                else
                {
                    // Essentially, if vector x is greater than vector y, return that
                    if (Location.CalculateDistance(new Location(Convert.ToDouble(x.GetAttribute(3)), Convert.ToDouble(x.GetAttribute(4))), user_location, DistanceUnits.Kilometers) >
                    Location.CalculateDistance(Convert.ToDouble(y.GetAttribute(3)), Convert.ToDouble(y.GetAttribute(4)), user_location, DistanceUnits.Kilometers)) return 1;
                    else return -1;
                }
            });
        }

        private async Task GetCurrentLocation()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                userLocation = await Geolocation.GetLastKnownLocationAsync();
                userLocation = await Geolocation.GetLocationAsync(request);
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                // Not supported on device.
            }
            catch (FeatureNotEnabledException fneEx)
            {
                // Not enabled on device
            }
            catch (PermissionException pEx)
            {
                // If no permission
            }
            catch (Exception ex)
            {
                // Basically, unable to get location!
            }
        }
        #endregion

        /* Async functions */
        private async Task LoadAttributesIntoClasses(string content, List<ValueSheet> list, int maxCount)
        {
            string[] values = content.Split(',', '\n');
            list.Capacity = values.Length / maxCount;
            int count = 1;
            ValueSheet new_value = new ValueSheet(maxCount);
            foreach (string str in values)
            {
                // Add new ValueSheet into the list after count is done. Then create a new, temporary vehicle variable
                if (count > maxCount)
                {
                    list.Add(new_value);
                    count = 1;
                    new_value = new ValueSheet(maxCount);
                }
                new_value.SetAtrribute(str);
                count += 1;
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await GetCurrentLocation();

            if (userLocation != null)
                return;
        }

        #region Table Get / Load Functions
        private async Task LoadTable()
        {
            // Adding values to table for easier insertion
            Dictionary<string, List<ValueSheet>> values = new Dictionary<string, List<ValueSheet>>
            {
                { GetTable("/api/GetFullTTCtable?code=rTNwGB-sgjdmdaTemfChZkBENpvSki7wKcuL6CSE0x_SAzFuew33cQ=="), vehicles = new List<ValueSheet>() },
                { GetTable("/api/GetTTCroutes?code=x-9PcSWxQ36dO5aRkaIUcipz4kGIzXgrBV33TS5flZbzAzFuyyIXbQ=="), routes = new List<ValueSheet>() },
                { GetTable("/api/GetTTCstops?code=56eOTfWgotMIyFyLB7u5XmPhcERvEXa5K_1qaTrKxBhqAzFufNB6ww=="), stops = new List<ValueSheet>() }
            };

            int count = 0;
            foreach (var value in values)
            {
                if (!String.IsNullOrEmpty(value.Key))
                {
                    if (count == 2)
                    {
                        await LoadAttributesIntoClasses(value.Key, value.Value, 12);
                    } else
                    {
                        await LoadAttributesIntoClasses(value.Key, value.Value, 9);
                    }
                    count++;
                }
            }

            await GetCurrentLocation();
            if (userLocation != null)
            {
                CompareVehiclesByLocation(userLocation);
            }
        }
        private void LoadMap()
        {
            map.CustomPins = new List<CustomPin> { };
            foreach (var stop in stops)
            {
                if (stop != null)
                {
                    CustomPin pin = new CustomPin
                    {
                        Type = PinType.Place,
                        Position = new Position(Convert.ToDouble(stop.GetAttribute(4)), Convert.ToDouble(stop.GetAttribute(5))),
                        Label = stop.GetAttribute(2),
                        Address = "394 Pacific Ave, San Francisco CA",
                        Name = "Xamarin",
                        Url = "http://xamarin.com/about/"
                    };
                    map.CustomPins.Add(pin);
                }
            }
            map.MoveToRegion(MapSpan.FromCenterAndRadius(new Position(Convert.ToDouble(stops[0].GetAttribute(4)), Convert.ToDouble(stops[0].GetAttribute(5))), Distance.FromMiles(1)));
        }
        private void LoadImages()
        {
            string[] button_names =
            {
                "map_button",
                "search",
            };

            select_buttons = new ImageButton[2];
            for (int i = 0; i < button_names.Length; i++)
            {
                select_buttons[i] = (ImageButton)Content.FindByName(button_names[i]);
            }
            select_buttons[0].Source = (Device.RuntimePlatform == Device.Android ? ImageSource.FromFile("map_icon.png") : ImageSource.FromFile("Icons/map_icon.png"));
            select_buttons[1].Source = (Device.RuntimePlatform == Device.Android ? ImageSource.FromFile("") : ImageSource.FromFile(""));

        }
        private string GetTable(string text)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://ttc-recieve-html.azurewebsites.net");
                    HttpResponseMessage response = client.GetAsync(text).Result;
                    response.EnsureSuccessStatusCode();
                    Task<String> responsestring = response.Content.ReadAsStringAsync();
                    return responsestring.Result;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (TargetInvocationException ex)
            {
                Console.WriteLine(ex.Message);
            }
            return "";
        }
        #endregion

    }
}