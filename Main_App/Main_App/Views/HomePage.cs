using System;
using Xamarin.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;
using Xamarin.Essentials;
using System.Threading;
using Android.Text;
using Android.Util;

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
        private string[] atts = new string[9];
        int count = 0;

        public ValueSheet() { }
        public void SetAtrribute(int index, string value)
        {
            if (count >= 9)
            {
                return; // We cannot handle anymore values
            }
            atts[index] = value;
            count += 1;
        }
        public string GetAttribute(int index)
        {
            if (atts[index].IsNullOrEmpty()) { return string.Empty; }
            return atts[index];
        }
    }
    public class Vehicle : ValueSheet
    {
        
    }
    public class Route : ValueSheet
    {
    }
    #endregion

    public partial class HomePage : ContentPage
    {
        private List<ValueSheet> vehicles;
        private List<ValueSheet> routes;
        private List<Frame> frames;
        Location userLocation;
        HttpClient client;

        public HomePage()
        {
            InitializeComponent();
            BindingContext = this;
            client = new HttpClient();
            // Instantiating all the views needed for a view that can scroll (dunno why grid is needed, but it is to prevent cut off at the fold.
            Grid gridLayout = new Grid();
            StackLayout stackLayout = new StackLayout();
            ScrollView scrollView = new ScrollView();

            // Setting stacklayout options + the function to populate stacklayout
            stackLayout.Margin = new Thickness(20);
            stackLayout.Orientation = StackOrientation.Vertical;

            LoadTable(stackLayout);

            // Setting scrollview options and binding stacklayout's content to scrollview
            scrollView.Content = stackLayout;
            scrollView.IsClippedToBounds = false;

            // finally, adding everything to the gridlayout and then adding it to the main content field of the xamarin.forms application
            gridLayout.Children.Add( scrollView );
            Content = gridLayout;
        }

        /* Creating a frame based on 3 string values */
        Frame CreateFrame(string routeNum, string lat, string lon)
        {
            Frame frame = new Frame {
                BackgroundColor = Color.FromHex("850700"),
                CornerRadius = 10,
                HasShadow = true
            };
            AbsoluteLayout absLayout = new AbsoluteLayout();


            BoxView box = new BoxView
            {
                WidthRequest = 50,
                HeightRequest = 30,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            };
            Label label = new Label {
                Text = routeNum,
                FontAttributes = FontAttributes.Bold,
                FontSize = 20,
                WidthRequest = 30,
                HeightRequest = 30,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start
            };

            absLayout.Children.Add(box);
            absLayout.Children.Add(label);

            frame.Content = absLayout;
            return frame;
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
        private async Task LoadAttributesIntoClasses(string content, List<ValueSheet> list)
        {
            string[] values = content.Split(',');
            list = new List<ValueSheet>(values.Length / 9); // Since we know there is 9 values
            int count = 1;
            ValueSheet new_value = new ValueSheet();
            foreach (string str in values)
            {
                if (str.IsNullOrEmpty()) { return; }
                // Add new ValueSheet into the list after count is done. Then create a new, temporary vehicle variable
                if (count > 9)
                {
                    list.Add(new_value);
                    count = 1;
                    new_value = new ValueSheet();
                }
                new_value.SetAtrribute(count - 1, str);
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
        private async Task LoadTable(StackLayout stackLayout)
        {
            int numOfTables = 2;
            // Adding values to table for easier insertion
            Dictionary<string, List<ValueSheet>> values = new Dictionary<string, List<ValueSheet>>();
            values.Add(await GetTable("https://ttc-recieve-html.azurewebsites.net/api/GetFullTTCtable?code=x-9PcSWxQ36dO5aRkaIUcipz4kGIzXgrBV33TS5flZbzAzFuyyIXbQ=="), vehicles);
            values.Add(await GetTable("https://ttc-recieve-html.azurewebsites.net/api/GetFullTTCtable?code=x-9PcSWxQ36dO5aRkaIUcipz4kGIzXgrBV33TS5flZbzAzFuyyIXbQ=="), routes);


            foreach (var value in values)
            {
                if (!value.Key.IsNullOrEmpty())
                {
                    await Task.Run(async () =>
                    {
                        await LoadAttributesIntoClasses(value.Key, value.Value);
                    });
                }
            }
            await Task.Run(async () =>
            {
                await GetCurrentLocation();
                if (userLocation != null)
                {
                    CompareVehiclesByLocation(userLocation);
                }

                Device.BeginInvokeOnMainThread(() => {
                    frames = new List<Frame>();
                    foreach (Vehicle vehicle in vehicles)
                    {
                        Frame frame = CreateFrame(vehicle.GetAttribute(1), null, null);
                        frames.Add(frame);
                        stackLayout.Children.Add(frame);
                    }
                });
            });
        }
        private async Task<string> GetTable(string text)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(text).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            } catch (HttpRequestException ex)
            {
                Console.WriteLine(ex.Message);
            } catch (TaskCanceledException ex)
            {
                Console.WriteLine(ex.Message);
            }
            return "";
        }
        #endregion

    }
}