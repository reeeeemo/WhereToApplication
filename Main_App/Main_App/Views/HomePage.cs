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
using System.Threading;
using System.Diagnostics;
using Main_App.ViewModels;
using Javax.Crypto.Interfaces;

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
        public int id;
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
                try
                {
                    if (vehicle.Length > 0)
                    {
                        Vehicle temp = new Vehicle();
                        temp.id = Convert.ToInt32(vehicle[0]);
                        temp.routeShortName = Convert.ToInt32(vehicle[1]);
                        temp.lat = Convert.ToDouble(vehicle[3]);
                        temp.lon = Convert.ToDouble(vehicle[4]);
                        temp.secsSinceReport = Convert.ToInt32(vehicle[5]);
                        temp.predictable = Convert.ToBoolean(vehicle[6]);
                        temp.heading = Convert.ToInt32(vehicle[7]);
                        temp.speed = Convert.ToInt32(vehicle[8]);
                        vehicles.Add(temp);
                    }
                } catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        public void UpdateVehicles(List<string[]> _vehicles)
        {
            foreach (string[] vehicle in _vehicles)
            {
                try
                {
                    if (vehicle.Length > 1)
                    {
                        Vehicle temp = vehicles.Find(x => x.id.ToString().Equals(vehicle[0]));
                        if (temp != null)
                        {
                            temp.lat = Convert.ToDouble(vehicle[3]);
                            temp.lon = Convert.ToDouble(vehicle[4]);
                            temp.secsSinceReport = Convert.ToInt32(vehicle[5]);
                            temp.heading = Convert.ToInt32(vehicle[7]);
                            temp.speed = Convert.ToInt32(vehicle[8]);
                        }
                    }
                } catch(Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }
    }
    #endregion

    #region Custom Map Classes
    public class CustomPin : Xamarin.Forms.Maps.Pin
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
        Location _userLocation;
        public Location UserLocation
        {
            get => _userLocation;
            set
            {
                _userLocation = value;
                userLocationPin.Position = new Position(UserLocation.Latitude, UserLocation.Longitude);
                viewModel.OriginLat = UserLocation.Latitude.ToString();
                viewModel.OriginLon = UserLocation.Longitude.ToString();
            }
        }
        bool routeMenuPressed = false;
        private readonly object routeLock = new object();
        bool active_vehicle_tracking = false;

        // XAML Elements
        ImageButton[] select_buttons;
        CustomPin userLocationPin;
        StackLayout current_layouts;
        Frame search_frame;

        HomePageViewModel viewModel = new HomePageViewModel();

        public HomePage()
        {
            InitializeComponent();
            BindingContext = viewModel;

            userLocationPin = new CustomPin();
            userLocationPin.Label = "";
            search_frame = (Frame)Content.FindByName("searchFrame");


            _ = SetUserMapLocation();

            if (UserLocation != null)
            {
                map.MoveToRegion(MapSpan.FromCenterAndRadius(new Position(UserLocation.Latitude, UserLocation.Longitude), Distance.FromMiles(1)));
                map.Pins.Add(userLocationPin);
            }

            LoadTable();
            LoadMap();
            LoadRoutes();
            ThreadPool.QueueUserWorkItem(StartVehicleTracking);
            ThreadPool.QueueUserWorkItem(StartUserTracking);
            viewModel.RouteTracking += AddPolylineFromRoute;
        }

        public void AddPolylineFromRoute(object sender, EventArgs e)
        {
            map.MapElements.Add(viewModel.routeLine);
        }

        private void StartUserTracking(object state)
        {
            Device.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                Task.Run(async () => {
                    await Device.InvokeOnMainThreadAsync(() =>
                    {
                        _ = SetUserMapLocation();
                    });
                });
                return true;
            });
        }

        /*
         *   STARTS THREAD THAT REPEATS EVERY 35 SECONDS
         *   LOOKS FOR ACTIVE VEHICLE TRACKING
         */
        private async void StartVehicleTracking(object state)
        {
            Device.StartTimer(TimeSpan.FromSeconds(35), () =>
            {
                if (active_vehicle_tracking)
                {
                    Task.Run(async () =>
                    {
                        var vehicles_list = await GetTableAsync("/api/GetVehicles?code=WfeofEpor3nyPSFsHTxl-xRGIqZRhX1C3g2Qh3dfoBPEAzFuRkweYw==");
                        var vehicles = vehicles_list.Split('\n').Select(line => line.Split(',')).ToList();


                        await Device.InvokeOnMainThreadAsync(() =>
                        {
                            full_ttc_list.UpdateVehicles(vehicles);
                        });
                        foreach (var pin in map.CustomPins)
                        {
                            try
                            {
                                var current_vehicle = full_ttc_list.vehicles.Find(x => x.id.ToString().Equals(pin.Name));

                                if (current_vehicle != null)
                                {
                                    await Device.InvokeOnMainThreadAsync(async () =>
                                    {
                                        var startPos = pin.Position;
                                        var endPos = new Position(current_vehicle.lat, current_vehicle.lon);
                                        var animation = new Animation(v =>
                                        {
                                            var lat = startPos.Latitude + (v * (endPos.Latitude - startPos.Latitude));
                                            var lon = startPos.Longitude + (v * (endPos.Longitude - startPos.Longitude));
                                            pin.Position = new Position(lat, lon);
                                        }, 0, 1); // animate from 0 (startPos) to 1 (endPos)
                                        animation.Commit(this, "PinAnimation", 16, 5000, Easing.Linear);
                                    });
                                }
                            } catch(Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }
                            

                        }
                    });
                }
                return true; // If true, keep running.
            });
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
            map.CustomPins.Clear();
            map.Pins.Add(userLocationPin);
            map.CustomPins.Add(userLocationPin);
            foreach (var vehicle in full_ttc_list.vehicles)
            {
                if (vehicle.routeShortName.ToString().Equals(routeNameLabel.Text))
                {
                    CustomPin pin = new CustomPin
                    {
                        Type = PinType.Place,
                        Position = new Position(vehicle.lat, vehicle.lon),
                        Label = vehicle.routeShortName.ToString(),
                        Name = vehicle.id.ToString(),
                        Url = "http://xamarin.com/about/",
                    };
                    map.Pins.Add(pin);
                    map.CustomPins.Add(pin);
                }
            }
            active_vehicle_tracking = true;
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
            if (!(searchBar is SearchBar _bar)) { return; }

            var filterNumber = int.Parse(_bar.Text);
            var matchingRoutes = new HashSet<Route>(full_ttc_list.routes.Where(i => i.routeShortName == filterNumber));

            var matchingLayouts = new HashSet<View>();

            foreach (var view in total_layouts.Skip(1))
            {
                if (view is StackLayout _view)
                {
                    if (_view.Children.FirstOrDefault(x => x is Label) is Label temp && int.TryParse(temp.Text, out var number))
                    {
                        if (matchingRoutes.Any(route => route.routeShortName == number))
                        {
                            matchingLayouts.Add(_view);
                        }
                    }
                }
            }

            // Synchronize ObservableCollection with HashSet
            filteredRoutes.Clear();
            foreach (var item in matchingLayouts)
            {
                filteredRoutes.Add(item as StackLayout);
            }

            /*foreach (var view in total_layouts.Skip(1)) // Always skip the first as it will always be the searchbar
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
                }*/
        }

        /*
         *  FILTER ROUTES BASED ON SEARCH QUERY + AVAILABLE CURRENT LAYOUTS
         *  ADDS / DELETES TO CURRENT LAYOUTS
        */
        public async void OnRouteSearchTextChanged(object sender, EventArgs e)
        {
            SearchBar search_bar = (SearchBar)sender;

            if (string.IsNullOrEmpty(search_bar.Text)) 
            {
                AddAllRoutesToList();
                return;
            }

            await Task.Run(() => FilterAllRoutesOnList(search_bar));

            Device.BeginInvokeOnMainThread(() => 
            {
                // If searchBar is not added already
                if (!current_layouts.Children.Contains(total_layouts.First()))
                {
                    current_layouts.Children.Insert(0, total_layouts.First());
                }

                // If the current layout does not have filtered routes in already, add it!
                foreach (var view in filteredRoutes)
                {
                    if (!current_layouts.Children.Contains(view))
                    {
                        current_layouts.Children.Add(view);
                    }
                }

                // If the current layout already has it, and filtered does not, delete it!
                foreach (var child in current_layouts.Children.ToList())
                {
                    if (child != total_layouts.First() && !filteredRoutes.Contains(child))
                    {
                        current_layouts.Children.Remove(child);
                    }
                }
            });
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
        /* 
         * GET LOCATION VIA GEOLOCATION OF USER
        */
        private async Task GetCurrentLocation()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                UserLocation = await Geolocation.GetLastKnownLocationAsync();
                UserLocation = await Geolocation.GetLocationAsync(request);
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

            if (UserLocation != null)
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
            var stops_list = GetTable("/api/GetStops?code=qpgCdidyJyUqU2vJE66sxyDZYwYcpG3WR9EJ1aFb9F9DAzFu1S90uQ==").ToString().Split('\n').Select(line => line.Split(',')).ToList();
            var routes_list = GetTable("/api/GetRoutes?code=SSJNZE5JtL4Tjah5lr1pi_3-TUBRXEnC4c2KaJ3aP5gHAzFuL2VJ3Q==").ToString().Split('\n').Select(line => line.Split(',')).ToList();
            var vehicles_list = GetTable("/api/GetVehicles?code=WfeofEpor3nyPSFsHTxl-xRGIqZRhX1C3g2Qh3dfoBPEAzFuRkweYw==").ToString().Split('\n').Select(line => line.Split(',')).ToList();

            full_ttc_list.SetStops(stops_list);
            full_ttc_list.SetRoutes(routes_list);
            full_ttc_list.SetVehicles(vehicles_list);

            // Gets location and sorts vehicle list by the user location!
            await GetCurrentLocation();
            if (UserLocation != null)
            {
                CompareVehiclesByLocation(UserLocation);
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
                            };
                        }
                    }
                }
            } catch(Exception ex)
            {
                DisplayAlert(ex.Source, ex.Message, ex.StackTrace);
            }
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

                        Image layoutImage = new Image{ 
                            BackgroundColor = Color.Transparent,
                            HeightRequest = 32,
                            WidthRequest = 32,
                            HorizontalOptions = LayoutOptions.CenterAndExpand,
                            VerticalOptions = LayoutOptions.CenterAndExpand,
                        };

                        // Setup Image beforehand based on the vehicle type.
                        if (route.routeShortName.ToString().StartsWith("5")) // Streetcar!
                        {
                            layoutImage.Source = Device.RuntimePlatform == Device.Android ? ImageSource.FromFile("train.png") : ImageSource.FromFile("Icons/train.png");
                        } else if (route.routeShortName.ToString().StartsWith("3")) // Night Bus!
                        {
                            layoutImage.Source = Device.RuntimePlatform == Device.Android ? ImageSource.FromFile("bloobus.png") : ImageSource.FromFile("Icons/bloobus.png");
                        }
                        else // Bus!
                        {
                            layoutImage.Source = Device.RuntimePlatform == Device.Android ? ImageSource.FromFile("bus.png") : ImageSource.FromFile("Icons/bus.png");
                        }

                        // Setup stacklayout
                        var sub_layout = new StackLayout
                        {
                            Orientation = StackOrientation.Horizontal,
                            Children = {
                        // REMINDER FOR IMAGES: Streetcars start with a "5", Night buses start with a "3"
                        layoutImage,
                        new Label
                        {
                            Text = route.routeShortName.ToString(),
                            FontSize = 24,
                            TextColor = Color.AntiqueWhite,
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
                TextColor = Color.AntiqueWhite,
                CancelButtonColor = Color.AntiqueWhite,
                PlaceholderColor = Color.AntiqueWhite,
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
         * TAKES SUBSTRING AND ADDS TO BASE AZURE STORAGE STRING ASYNC
         * RETURNS RESULT STRING (a bunch of .csv information)
         */
        private async Task<string> GetTableAsync(string text)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://ttc-recieve-html.azurewebsites.net");
                    HttpResponseMessage response = client.GetAsync(text).Result;
                    response.EnsureSuccessStatusCode();
                    Task<string> responsestring = response.Content.ReadAsStringAsync();
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return "";
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
                    Task<string> responsestring = response.Content.ReadAsStringAsync();
                    return responsestring.Result.ToString();
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return "";
        }
        #endregion


        public async void OnEnterAddressTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SearchPage() { BindingContext = this.BindingContext }, false);
        }

        public void OnStopRouteTracking(object sender, EventArgs e)
        {
            viewModel.IsRouteTracking = false;
            viewModel.PlaceName = "WhereTo?";
        }
    }
}