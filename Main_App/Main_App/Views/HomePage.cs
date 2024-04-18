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
using Android.Security.Identity;

[assembly: ExportFont("Lobster-Regular.ttf", Alias = "Lobster")]

// https://webservices.umoiq.com/service/publicXMLFeed?command=vehicleLocations&a=ttc
// THIS IS THE LIVE VEHICLE TRACKER :>

namespace Main_App.Views
{
    #region Custom Classes
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class Stop
    {
        public string tripName;
        public string stopName;
        public double lat;
        public double lon;
        public int routeShortName;
        public string routeLongName;
    }

    public class Route
    {
        public string routeLongName;
        public int routeShortName;
        public int wheelchairBoarding;
    }

    public class Vehicle
    {
        public int routeShortName;
        public double lat;
        public double lon;
        public int secsSinceReport;
        public bool predictable;
        public int heading;
        public int speed; 
    }

    public class GTFS_List
    {
        public readonly object GTFS_LOCK = new object();
        public List<Stop> stops = new List<Stop>();
        public List<Route> routes  = new List<Route>();
        public List<Vehicle> vehicles = new List<Vehicle>();

        public GTFS_List() { }

        // Adds stops in the order: tripName, stopName, lat, lon, routeShortName, routeLongName
        public void SetStops(List<string[]> _stops)
        {
            foreach (string[] stop in _stops.Skip(1))
            {
                if (stop.Length > 0)
                {
                    Stop temp = new Stop();
                    temp.tripName = stop[0];
                    temp.stopName = stop[1];
                    temp.lat = Convert.ToDouble(stop[2]);
                    temp.lon = Convert.ToDouble(stop[3]);
                    temp.routeShortName = Convert.ToInt32(stop[4]);
                    temp.routeLongName = stop[5];
                    stops.Add(temp);
                }
            }
        }
        public void SetRoutes(List<string[]> _routes)
        {
            foreach (string[] route in _routes.Skip(1))
            {
                if (route.Length > 0) 
                {
                    Route temp = new Route();
                    temp.routeShortName = Convert.ToInt32(route[0]);
                    temp.routeLongName = route[1];
                    temp.wheelchairBoarding = Convert.ToInt32(route[2]);
                    routes.Add(temp);
                }
            } 
        }


        public void SetVehicles(List<string[]> _vehicles)
        {
            foreach (string[] vehicle in _vehicles)
            {
                Vehicle temp = new Vehicle();
                temp.routeShortName = Convert.ToInt32(vehicle[1]);
                temp.lat = Convert.ToDouble(vehicle[3]);
                temp.lon = Convert.ToDouble(vehicle[4]);
                temp.secsSinceReport = Convert.ToInt32(vehicle[5]);
                temp.predictable = Convert.ToBoolean(vehicle[6]);
                temp.heading = Convert.ToInt32(vehicle[7]);
                temp.speed = Convert.ToInt32(vehicle[8]);
                vehicles.Add(temp);
            }
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
            Label routeNameLabel = _view.Children.FirstOrDefault(x => x is Label) as Label; // better than .First so it does not return an exception

            map.Pins.Clear();
            foreach (var vehicle in full_ttc_list.vehicles)
            {
                if (vehicle.routeShortName.ToString().Equals(routeNameLabel.Text))
                {
                    CustomPin pin = new CustomPin
                    {
                        Type = PinType.Place,
                        Position = new Position(vehicle.lat, vehicle.lon),
                        Label = vehicle.routeShortName.ToString(),
                        Name = vehicle.routeShortName.ToString(),
                        Url = "http://xamarin.com/about/",
                    };
                    map.Pins.Add(pin);
                }
            }
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
         *  LOADS SQL QUERIES INTO LISTS
         *  SORTS VEHICLES BASED ON USER LOCATION 
         */
        private async Task LoadTable()
        {
            // temp variables, meant as a way to store the responses of the LINQ queries before merging them in the full_ttc_list
            var stops_list = GetTable("/api/GetStops?code=qpgCdidyJyUqU2vJE66sxyDZYwYcpG3WR9EJ1aFb9F9DAzFu1S90uQ==").Split('\n').Select(line => line.Split(',')).ToList();
            var routes_list = GetTable("/api/GetRoutes?code=SSJNZE5JtL4Tjah5lr1pi_3-TUBRXEnC4c2KaJ3aP5gHAzFuL2VJ3Q==").Split('\n').Select(line => line.Split(',')).ToList();
            var vehicles_list = GetTable("/api/GetVehicles?code=WfeofEpor3nyPSFsHTxl-xRGIqZRhX1C3g2Qh3dfoBPEAzFuRkweYw==").Split('\n').Select(line => line.Split(',')).ToList();

            full_ttc_list.SetStops(stops_list);
            full_ttc_list.SetRoutes(routes_list);
            full_ttc_list.SetVehicles(vehicles_list);

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
                    foreach (var stop in full_ttc_list.stops)
                    {
                        if (stop != null)
                        {
                            CustomPin pin = new CustomPin
                            {
                                Type = PinType.Place,
                                Position = new Position(stop.lat, stop.lon),
                                Label = stop.tripName,
                                Address = stop.stopName,
                                Name = stop.tripName,
                                Url = "http://xamarin.com/about/",
                            };
                            map.CustomPins.Add(pin);
                        }
                    }
                    map.MoveToRegion(MapSpan.FromCenterAndRadius(new Position(Convert.ToDouble(full_ttc_list.stops.First().lat), Convert.ToDouble(full_ttc_list.stops.First().lon)), Distance.FromMiles(1)));
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
            foreach (var route in full_ttc_list.routes)
            {
                lock (full_ttc_list.GTFS_LOCK)
                {
                    string routeIdentifier = route.routeLongName;

                    if (!seenRoutes.Contains(routeIdentifier))
                    {
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
                            Text = route.routeShortName.ToString(),
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