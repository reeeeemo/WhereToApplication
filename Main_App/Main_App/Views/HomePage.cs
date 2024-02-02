using System;
using Xamarin.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xamarin.Essentials;
using Xamarin.Forms.Maps;

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
            if (String.IsNullOrEmpty(atts[index])) { return string.Empty; }
            return atts[index];
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
        private List<Frame> frames;
        Location userLocation;
        HttpClient client;
        ImageButton[] select_buttons;
        Frame search_frame;

        public HomePage()
        {
            InitializeComponent();
            BindingContext = this;

            search_frame = (Frame)Content.FindByName("searchFrame");

            string[] button_names =
            {
                "map_button",
                "search"
            };
            
            select_buttons = new ImageButton[2];
            for (int i = 0; i < button_names.Length; i++)
            {
                select_buttons[i] = (ImageButton)Content.FindByName(button_names[i]);
            }
            select_buttons[0].Source = (Device.RuntimePlatform == Device.Android ? ImageSource.FromFile("map_icon.png") : ImageSource.FromFile("Icons/map_icon.png"));
            select_buttons[1].Source = (Device.RuntimePlatform == Device.Android ? ImageSource.FromFile("") : ImageSource.FromFile(""));
            /* client = new HttpClient();
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
             Content = gridLayout; */
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
                    search_frame.IsEnabled = false;
                } else
                {
                    search_frame.FadeTo(0.7);
                    search_frame.IsEnabled = true;
                }
            }
        }

        private void SetMapPageActive()
        {


        }

        private void SetSearchPageActive()
        {


        }

        /* Creating a frame based on 3 string values */
        Frame CreateFrame(string routeNum, string routeName)
        {
            Console.WriteLine(routeName);
            Frame frame = new Frame {
                BackgroundColor = Color.FromHex("850700"),
                CornerRadius = 10,
                HasShadow = true
            };
            AbsoluteLayout absLayout = new AbsoluteLayout();


            BoxView box = new BoxView
            {
            };
            AbsoluteLayout.SetLayoutBounds(box, new Rectangle(0.5, 0, 100, 25));
            Label label = new Label {
                Text = routeNum,
                FontAttributes = FontAttributes.Bold,
                FontSize = 20,
                WidthRequest = 30,
                HeightRequest = 30,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start
            };
            Label label2 = new Label
            {
                Text = routeName,
                FontAttributes = FontAttributes.Bold,
                FontSize = 20,
                WidthRequest = 120,
                HeightRequest = 60,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End
            };

            absLayout.Children.Add(box);
            absLayout.Children.Add(label);
            absLayout.Children.Add(label2);

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
            string[] values = content.Split(',', '\n');
            list.Capacity = values.Length / 9;
            int count = 1;
            ValueSheet new_value = new ValueSheet();
            foreach (string str in values)
            {
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
            Dictionary<string, List<ValueSheet>> values = new Dictionary<string, List<ValueSheet>>
            {
                { await GetTable("https://ttc-recieve-html.azurewebsites.net/api/GetFullTTCtable?code=x-9PcSWxQ36dO5aRkaIUcipz4kGIzXgrBV33TS5flZbzAzFuyyIXbQ=="), vehicles = new List<ValueSheet>() },
                { await GetTable("https://ttc-recieve-html.azurewebsites.net/api/GetTTCroutes?code=x-9PcSWxQ36dO5aRkaIUcipz4kGIzXgrBV33TS5flZbzAzFuyyIXbQ=="), routes = new List<ValueSheet>() }
            };


            foreach (var value in values)
            {
                if (!String.IsNullOrEmpty(value.Key))
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
                    foreach (ValueSheet vehicle in vehicles)
                    {
                        ValueSheet route = routes.Find(x => int.Parse(x.GetAttribute(2)) == int.Parse(vehicle.GetAttribute(1)));
                        if (route != null)
                        {
                            Frame frame = CreateFrame(vehicle.GetAttribute(1), route.GetAttribute(3));
                            frames.Add(frame);
                            stackLayout.Children.Add(frame);
                        } else
                        {
                            Frame frame = CreateFrame(vehicle.GetAttribute(1), null);
                            frames.Add(frame);
                            stackLayout.Children.Add(frame);
                        }
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