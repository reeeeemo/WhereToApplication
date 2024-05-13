using Android.Telephony;
using Main_App.Models;
using Main_App.Services;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;

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
        string _originLat, _originLon;

        // Route Tracking Variables
        bool _isRouteTracking;
        public bool IsRouteTracking
        {
            get => _isRouteTracking;
            set
            {
                _isRouteTracking = value;
                OnPropertyChanged(nameof(IsRouteTracking));
            }
        }

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
                    _originLat = $"{place.Latitude}";
                    _originLon = $"{place.Longitude}";
                    _isOriginFocused = false;
                }
                else
                {
                    string _destLat = $"{place.Latitude}";
                    string _destLon = $"{place.Longitude}";

                    PlaceName = $"Selected: {place.Name}";
                    IsRouteTracking = true;

                    if (_originLat == _destLat && _originLon == _destLon)
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