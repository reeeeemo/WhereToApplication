using System;
using Xamarin.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;
using Xamarin.Essentials;
using System.Threading;

[assembly: ExportFont("Lobster-Regular.ttf", Alias = "Lobster")]



namespace Main_App.Views
{
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

    public class Vehicle
    {
        private string[] atts = new string[9];
        int count = 0;
        
        public Vehicle()
        {

        }

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


    public partial class HomePage : ContentPage
    {
        private string connection_string;

        private List<Vehicle> vehicles;
        private List<Label> labels;
        private List<Frame> frames;
        Location userLocation;

        HttpClient client;
        static string text = "https://ttc-recieve-html.azurewebsites.net/api/GetFullTTCtable?code=x-9PcSWxQ36dO5aRkaIUcipz4kGIzXgrBV33TS5flZbzAzFuyyIXbQ==";
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

        /* Function gets location via geolocation, then compares the data in the list by device's location*/
        private void CompareRoutesByLocation(Location user_location)
        {
            vehicles.Sort(delegate(Vehicle x, Vehicle y)
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


        /* Async functions */
        private async Task LoadAttributesIntoClasses(string[] values)
        {
            int count = 1;
            vehicles = new List<Vehicle>(values.Length / 9);
            Vehicle new_vehicle = new Vehicle();
            foreach (string str in values)
            {
                if (str.IsNullOrEmpty()) { return; }
                // Add new vehicle into the list after count is done. Then create a new, temporary vehicle variable
                if (count > 9)
                {
                    vehicles.Add(new_vehicle);
                    count = 1;
                    new_vehicle = new Vehicle();
                }
                new_vehicle.SetAtrribute(count - 1, str);
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
        private async Task GetCurrentLocation()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                userLocation = await Geolocation.GetLastKnownLocationAsync();
                userLocation = await Geolocation.GetLocationAsync(request);
            } catch (FeatureNotSupportedException fnsEx)
            {
                // Not supported on device.
            } catch (FeatureNotEnabledException fneEx)
            {
                // Not enabled on device
            } catch (PermissionException pEx)
            {
                // If no permission
            } catch (Exception ex)
            {
                // Basically, unable to get location!
            }
        }

        private async Task LoadTable(StackLayout stackLayout)
        {
            string content = await GetTable();
            // Split values based on the commas
            if (!content.IsNullOrEmpty())
            {
                await Task.Run(async () =>
                {
                    string[] values = content.Split(',');
                    await LoadAttributesIntoClasses(values);

                    await GetCurrentLocation();
                    if (userLocation != null)
                    {
                        CompareRoutesByLocation(userLocation);
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

            
        }

        private async Task<string> GetTable()
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

    }
}