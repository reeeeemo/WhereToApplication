using System;
using Xamarin.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xamarin.Essentials;
using Xamarin.Forms.Maps;
using System.Reflection;
using System.Linq;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading;
using System.Collections;
using Android.Media.TV;
using System.Diagnostics;
using Android.Icu.Text;

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

    // Custom tuple class meant to make accessing the stops and routes easier.
    public class Tuple<T1, T2>
    {
        public T1 Stop { get; set; }
        public T2 Route { get; set; }

        public Tuple(T1 stop, T2 route)
        {
            Stop = stop;
            Route = route;
        }
    }

    class GTFSEqualityComparator : IEqualityComparer<Tuple<string[], string[]>>
    {
        private bool Equals(string[] x, string[] y)
        {
            return x.SequenceEqual(y);
        }

        public bool Equals(Tuple<string[], string[]> x, Tuple<string[], string[]> y)
        {
            return Equals(x.Stop, y.Stop) && Equals(x.Route, y.Route);
        }

        public int GetHashCode(string[] obj)
        {
            return obj.Aggregate(string.Empty, (s, i) => s + i).GetHashCode();
        }

        // im going to be honest I have no clue what this does, however it says online it works and I will update this later
        public int GetHashCode(Tuple<string[], string[]> obj)
        {
            int hash = 17;
            hash = hash * 31 + GetHashCode(obj.Stop);
            hash = hash * 31 + GetHashCode(obj.Route);
            return hash;
        }
    }

    public class GTFS_List : IEnumerable<Tuple<string[], string[]>>
    {
        public readonly object GTFS_LOCK = new object();
        public List<Tuple<string[], string[]>> tuples = new List<Tuple<string[], string[]>>();

        public IEnumerator<Tuple<string[], string[]>> GetEnumerator()
        {
            return tuples.GetEnumerator();
        }



        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
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
        // Lists
        private List<string[]> vehicles;
        ObservableCollection<StackLayout> filteredRoutes = new ObservableCollection<StackLayout>();
        List<View> total_layouts;
        GTFS_List full_ttc_list = new GTFS_List();

        // Variables / Values
        Location userLocation;
        bool routeMenuPressed = false;
        private readonly object routeLock = new object();

        // XAML Elements
        ImageButton[] select_buttons;
        StackLayout current_layouts;
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
            LoadRoutes();
        }



        #region Button / Element Commands
        private async Task SetUserMapLocation()
        {
            await GetCurrentLocation();
        }

        public void OnRouteTap(object sender, EventArgs e)
        {
            StackLayout _view = sender as StackLayout;
            Label temp = _view.Children.FirstOrDefault(x => x is Label) as Label; // better than .First so it does not return an exception
            //FindStopsOnRoute(temp.Text);
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

        public void OpenMenu(object sender, EventArgs e)
        {
            var menu = (Frame)Content.FindByName("RouteSelector");
            var button = (ImageButton)Content.FindByName("MenuOpenButton");
            if (menu == null || button == null) { return; }
            if (routeMenuPressed)
            {
                menu.TranslateTo(-1, 0, 100);
                button.TranslateTo(1, 0, 100);
                routeMenuPressed = false;
            }
            else
            {
                menu.TranslateTo(menu.Width, 0, 100);
                button.TranslateTo(menu.Width, 0, 100);
                routeMenuPressed = true;
            }

        }

        private void AddAllRoutesToList()
        {
            filteredRoutes.Clear();
            current_layouts.Children.Clear();
            current_layouts.Children.Add(total_layouts.First()); // Adding the search bar!

            foreach (var view in total_layouts)
            {
                current_layouts.Children.Add(view);
            }
        }

        /*
         * FILTERING ALGORITHM FOR ROUTE LAYOUTS 
        */
        public void FilterAllRoutesOnList(object searchBar)
        {
            if (!(searchBar is SearchBar)) { return; }

            var _bar = searchBar as SearchBar; 

                // Taking each stacklayout in the route search bar, other than the search bar itself or else cast failure!
                foreach (var view in total_layouts.Skip(1)) // Always skip the first as it will always be the searchbar
                {

                    var _view = view as StackLayout;
                    if (_view != null) // View casting nullchecker
                    {
                        Label temp = _view.Children.FirstOrDefault(x => x is Label) as Label; // better than .First so it does not return an exception
                        bool text_match = temp.Text.ToLower().StartsWith(_bar.Text.ToLower());
                        if (temp != null && text_match) // Label casting nullchecker
                        {
                            filteredRoutes.Add((StackLayout)view);
                        } else if (temp != null && !text_match)
                        {
                        filteredRoutes.Remove((StackLayout)view);
                        }
                    }
                }
        }

        /*
         *  FILTER ROUTES BASED ON SEARCH QUERY + AVAILABLE CURRENT LAYOUTS
         *  ADDS / DELETES TO CURRENT LAYOUTS
        */
        public void OnRouteSearchTextChanged(object sender, EventArgs e)
        {
            SearchBar search_bar = (SearchBar)sender;


            if (string.IsNullOrEmpty(search_bar.Text)) // redundant code I know, will put into a function at some point
            {
                AddAllRoutesToList();
                return;
            }

            lock(routeLock)
            {
                ThreadPool.QueueUserWorkItem(FilterAllRoutesOnList, search_bar);
            }

            lock (routeLock)
            {
                current_layouts.Children.Clear();
                current_layouts.Children.Add(total_layouts.First()); // Adding the search bar!
                if (filteredRoutes.Count != 0)
                {
                    foreach (var view in filteredRoutes)
                    {
                        current_layouts.Children.Add(view);
                    }
                }


            }
        }

        private void SetMapPageActive()
        {
             

        }

        private void SetSearchPageActive()
        {


        }

        #endregion

        #region GeoLocation Functions
        /* 
         * GET LOCATION VIA GEOLOCATION 
         * COMPARES AND SORTS DATA BASED ON LOCATION
         */
        private void CompareVehiclesByLocation(Location user_location)
        {
            vehicles.Sort(delegate(string[] x, string[] y)
            {
                if (x == null && y == null) return 0;
                else if (x == null) return -1;
                else if (y == null) return 1;
                else
                {
                    // Essentially, if vector x is greater than vector y, return that
                    if (Location.CalculateDistance(new Location(Convert.ToDouble(x[3]), Convert.ToDouble(x[4])), user_location, DistanceUnits.Kilometers) >
                    Location.CalculateDistance(Convert.ToDouble(y[3]), Convert.ToDouble(y[4]), user_location, DistanceUnits.Kilometers)) return 1;
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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await GetCurrentLocation();

            if (userLocation != null)
                return;
        }

        #endregion



        #region Table Get / Load Functions


        /* 
         *  LOADS LINQ QUERIES & MERGES IN ONE TUPLE
         *  SORTS VEHICLES BASED ON USER LOCATION 
         */
        private async Task LoadTable()
        {
            // temp variables, meant as a way to store the responses of the LINQ queries before merging them in the full_ttc_list
            var stops = GetTable("/api/GetTTCstops?code=56eOTfWgotMIyFyLB7u5XmPhcERvEXa5K_1qaTrKxBhqAzFufNB6ww==").Split('\n').Select(line => line.Split(',')).ToList();
            var routes = GetTable("/api/GetTTCroutes?code=x-9PcSWxQ36dO5aRkaIUcipz4kGIzXgrBV33TS5flZbzAzFuyyIXbQ==").Split('\n').Select(line => line.Split(',')).ToList();
            var stop_times = GetTable("/api/GetTTCstop_times?code=sa2f9pBmyPYDuZCYLJ2jM50YY3bRPJd6AOZIh-TEnElfAzFuR3f7Cg==").Split('\n').Select(line => line.Split(',')).ToList();
            var trips = GetTable("/api/GetTTCtrips?code=7kvGwh5INYtrOmIIQvfS3qGLsj_pjTD1Fpnvr4rS0ktvAzFuYNCAMA==").Split('\n').Select(line => line.Split(',')).ToList();

            lock(full_ttc_list.GTFS_LOCK)
            {
                var query_result = (from stopTime in stop_times
                                   join trip in trips on stopTime[0] equals trip[2]
                                   join route in routes on trip[0] equals route[0]
                                   join stop in stops on stopTime[3] equals stop[0]
                                   select new Tuple<string[], string[]>(stop, route)).ToList();
                full_ttc_list.tuples = query_result.Distinct(new GTFSEqualityComparator()).ToList();

            }

            foreach(var record in full_ttc_list.tuples)
            {
                Debug.WriteLine($"Route: {record.Route[3]}. Stop: {record.Stop[2]}");
            }

            // Gets location and sorts vehicle list by the user location!
            await GetCurrentLocation();
            if (userLocation != null)
            {
                CompareVehiclesByLocation(userLocation);
            }
        }

        /*
         * TAKES ALL STOPS FROM full_ttc_list
         * CONVERTS TO PINS AND ADDS TO MAP PIN LIST
        */
        private void LoadMap()
        {
            map.CustomPins = new List<CustomPin> { };
            try
            {
                lock (full_ttc_list.GTFS_LOCK)
                {
                    foreach (var record in full_ttc_list.tuples)
                    {
                        if (record.Stop != null && record.Stop.Length >= 6)
                        {
                            CustomPin pin = new CustomPin
                            {
                                Type = PinType.Place,
                                Position = new Position(Convert.ToDouble(record.Stop[4]), Convert.ToDouble(record.Stop[5])),
                                Label = record.Stop[2],
                                Address = record.Stop[3].ToLower(),
                                Name = record.Stop[1].ToLower(),
                                Url = "http://xamarin.com/about/",
                            };
                            map.CustomPins.Add(pin);
                        }
                    }
                    map.MoveToRegion(MapSpan.FromCenterAndRadius(new Position(Convert.ToDouble(full_ttc_list.tuples.First().Stop[4]), Convert.ToDouble(full_ttc_list.tuples.First().Stop[5])), Distance.FromMiles(1)));
                }
            } catch(Exception ex)
            {
                DisplayAlert(ex.Source, ex.Message, ex.StackTrace);
            }
        }
        /*
         * TAKES ALL BUTTONS AND LOADS THE IMAGES PLACED ON THEM
        */
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
            //select_buttons[1].Source = (Device.RuntimePlatform == Device.Android ? ImageSource.FromFile("") : ImageSource.FromFile(""));

        }

        private async Task LoadRoutes()
        {
            if (ThreadPool.QueueUserWorkItem(LoadRoutesIntoFrame, Content.FindByName("RouteView")))
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    ScrollView scroll = Content.FindByName("RouteView") as ScrollView;
                    if (scroll != null)
                    {
                        lock(full_ttc_list.GTFS_LOCK)
                        {
                            scroll.Content = current_layouts;
                        }
                    }
                });

            }
        }

        private void LoadRoutesIntoFrame(object scroll_view)
        {
            ScrollView scrollView = scroll_view as ScrollView;
            if (scroll_view == null) { return; }
            List<StackLayout> layouts = new List<StackLayout>();
            total_layouts = new List<View>();


            HashSet<string> seenRoutes = new HashSet<string>();
            // Adding each route into a stacklayout and then to the list of stack layouts
            foreach (var record in full_ttc_list.tuples)
            {
                lock (full_ttc_list.GTFS_LOCK)
                {
                    string routeIdentifier = record.Route[2] + record.Route[3];

                    if (!seenRoutes.Contains(routeIdentifier))
                    {
                        Debug.WriteLine($"Count DURING = {full_ttc_list.tuples.Count}");
                        seenRoutes.Add(routeIdentifier);
                        var sub_layout = new StackLayout
                        {
                            Orientation = StackOrientation.Horizontal,
                            Children = {
                        new Image
                        {
                            BackgroundColor = Color.Red,
                            HeightRequest= 32,
                            WidthRequest= 32,
                            HorizontalOptions = LayoutOptions.CenterAndExpand,
                            VerticalOptions = LayoutOptions.CenterAndExpand,
                        },
                        new Label
                        {
                            Text = record.Route[2],
                            FontSize = 24,
                            HorizontalOptions= LayoutOptions.CenterAndExpand,
                            VerticalOptions= LayoutOptions.CenterAndExpand,
                            ClassId = "RouteText",
                        },
                    },
                        };
                        // Adding a tap gesture to each!
                        TapGestureRecognizer tap = new TapGestureRecognizer();
                        tap.Tapped += OnRouteTap;
                        sub_layout.GestureRecognizers.Add(tap);
                        layouts.Add(sub_layout);
                    }
                }
            }

            current_layouts = new StackLayout
            {
                Padding = 5
            };

            // Create search bar, then add to the layouts
            SearchBar search_bar = new SearchBar
            {
                Placeholder = "Enter Route Num...",
                FontSize = Device.GetNamedSize(NamedSize.Medium, typeof(SearchBar)),
            };

            search_bar.TextChanged += OnRouteSearchTextChanged;

            
            lock(routeLock)
            {
                current_layouts.Children.Add(search_bar);
                total_layouts.Add(search_bar);
            }

            LoadLayouts(layouts);

        }

        // there is over 100000 layouts what the fuuuck
        private void LoadLayouts(List<StackLayout> stack_layout)
        {
            if (stack_layout != null)
            {
                foreach(StackLayout layout in stack_layout)
                {
                    current_layouts.Children.Add(layout);
                    total_layouts.Add(layout);
                }
            }
        }

        /*
         * TAKES SUBSTRING AND ADDS TO BASE AZURE STORAGE STRING
         * RETURNS RESULT STRING (a bunch of .csv information)
         */
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