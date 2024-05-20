using Android.Telephony;
using Main_App.Models;
using Main_App.Services;
using Newtonsoft.Json;
using P42.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Maps;
using Xamarin.Forms.Shapes;

namespace Main_App.ViewModels
{
    public class HomePageViewModel : INotifyPropertyChanged
    {
        #region Variables
        /* Public Variables */
        readonly IGoogleMapsApiService googleMapsApi = new GoogleMapsApiService();

        /* Commands for textboxes. */
        public ICommand GetPlacesCommand { get; set; }
        public ICommand GetPlaceDetailCommand { get; set; }

        /* COLLECTIONS FOR AUTO-COMPLETION OF PLACES */
        public ObservableCollection<GooglePlaceAutoCompletePrediction> Places { get; set; }
        public ObservableCollection<GooglePlaceAutoCompletePrediction> RecentPlaces { get; set; } = new
            ObservableCollection<GooglePlaceAutoCompletePrediction>();

        /* Variables for the text boxes */
        bool _isOriginFocused = false;
        public bool _showRecentPlaces;
        public bool ShowRecentPlaces {
            get => _showRecentPlaces;
            
            set
            {
                if (_showRecentPlaces != value)
                {
                    _showRecentPlaces = value;
                    OnPropertyChanged(nameof(ShowRecentPlaces));
                }
            }
        }
        string _originText, _destText;
        public string OriginText
        {
            get => _originText;
            set
            {
                _originText = value;
                if (!string.IsNullOrEmpty(_originText))
                {
                    _isOriginFocused = true;
                    GetPlacesCommand.Execute(_originText);
                }
            }
        }
        public string DestinationText
        {
            get => _destText;
            set
            {
                _destText = value;
                if (!string.IsNullOrEmpty(_destText))
                {
                    _isOriginFocused = false;
                    GetPlacesCommand.Execute(_destText);
                } else
                {
                    ShowRecentPlaces = true;
                }
            }
        }

        GooglePlaceAutoCompletePrediction _placeSelected;
        public GooglePlaceAutoCompletePrediction PlaceSelected
        {
            get
            {
                return _placeSelected;
            }
            set
            {
                _placeSelected = value;
                if (_placeSelected != null)
                GetPlaceDetailCommand.Execute(_placeSelected);
            }
        }
        public string _placeName;
        public string PlaceName
        {
            get => _placeName;
            set
            {
                _placeName = value;
                OnPropertyChanged(nameof(PlaceName));
            }
        }


        /* Lat / Lon Variables */
        public string OriginLat, OriginLon;

        // Route Tracking Variables
        bool _isRouteTracking; // Boolean for XAML values (doesn't really work with events :/)
        public event EventHandler RouteTracking = delegate { }; // event for homepage.cs, since it should not be accessing this view model.
        public bool IsRouteTracking
        {
            get => _isRouteTracking;
            set
            {
                _isRouteTracking = value;
                OnPropertyChanged(nameof(IsRouteTracking));
                try
                {
                    RouteTracking.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {

                }
            }
        }

        public Xamarin.Forms.Maps.Polyline routeLine;

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            var changed = PropertyChanged;
            if (changed == null)
                return;

            changed.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /* MAIN HOME PAGE MODEL */
        public HomePageViewModel()
        {
            GoogleMapsApiService.Initialize("AIzaSyD3a7hyJLIntsMVaPMqw3x-Ptkt5xcYvso");
            GetPlacesCommand = new Command<string>(async (param) => await GetPlacesByName(param));
            GetPlaceDetailCommand = new Command<GooglePlaceAutoCompletePrediction>(async (param) => await GetPlacesDetail(param));
            Places = new ObservableCollection<GooglePlaceAutoCompletePrediction>();
            PlaceName = "WhereTo?";
            IsRouteTracking = false;
        }

        void StartRouteTracking(string dest_lat, string dest_lon)
        {
            routeLine = new Xamarin.Forms.Maps.Polyline()
            {
                StrokeColor = Color.Red,
                StrokeWidth = 12,
            };

            var directions = googleMapsApi.GetDirections(OriginLat, OriginLon, dest_lat, dest_lon).Result;

            foreach (var step in directions.Routes[0].Legs[0].Steps)
            {
                var decodedPolylines = googleMapsApi.DecodePolyline(step.Polyline.Points);
                foreach (var pos in decodedPolylines)
                {
                    routeLine.Geopath.Add(pos);
                }
            }

            /*var p1 = new Position(Convert.ToDouble(OriginLat), Convert.ToDouble(OriginLon));
            var p2 = new Position(Convert.ToDouble(dest_lat), Convert.ToDouble(dest_lon));


            routeLine.Geopath.Add(p1);
            routeLine.Geopath.Add(p2); */
            IsRouteTracking = true;
        }

        public async Task GetPlacesByName(string placeText)
        {
            var places = await googleMapsApi.GetPlaces(placeText);
            var placeResult = places.AutoCompletePlaces;
            if (placeResult != null && placeResult.Count > 0)
            {
                Places.Clear();
                foreach(var result in placeResult)
                {
                    Places.Add(result);
                }
                //Places = new ObservableCollection<GooglePlaceAutoCompletePrediction>(placeResult);
            }

           ShowRecentPlaces = (placeResult == null || placeResult.Count == 0);
        }

        public async Task GetPlacesDetail(GooglePlaceAutoCompletePrediction placeA)
        {
            var place = await googleMapsApi.GetPlaceDetails(placeA.PlaceId);
            if (place != null)
            {
                if (_isOriginFocused) // this doesnt work atm
                {
                    OriginText = place.Name;
                    OriginLat = $"{place.Latitude}";
                    OriginLon = $"{place.Longitude}";
                    _isOriginFocused = false;
                }
                else
                {
                    string _destLat = $"{place.Latitude}";
                    string _destLon = $"{place.Longitude}";

                    PlaceName = $"Selected: {place.Name}";
                    StartRouteTracking(_destLat, _destLon);

                    if (OriginLat == _destLat && OriginLon == _destLon)
                    {
                        await App.Current.MainPage.DisplayAlert("Error", "Origin route should be different than destination route", "Ok");
                    }
                    else if (RecentPlaces.Contains(placeA))
                    {
                        await App.Current.MainPage.Navigation.PopAsync(false);
                        CleanFields();
                    }
                    else
                    {
                        RecentPlaces.Add(placeA);
                        await App.Current.MainPage.Navigation.PopAsync(false);
                        CleanFields();
                    }

                }
            }
        }



        void CleanFields()
        {
            OriginText = DestinationText = string.Empty;
            ShowRecentPlaces = true;
            PlaceSelected = null;
        }

        
    }
}