using Main_App.Models;
using Main_App.Services;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace Main_App.ViewModels
{
    public class HomePageViewModel : BaseViewModel
    {
        #region Variables
        /* Public Variables */
        IGoogleMapsApiService googleMapsApi = new GoogleMapsApiService();
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

        /* Commands for textboxes. */
        public ICommand GetPlacesCommand { get; set; }
        public ICommand GetPlaceDetailCommand { get; set; }

        /* COLLECTIONS FOR AUTO-COMPLETION OF PLACES */
        public ObservableCollection<GooglePlaceAutoCompletePrediction> Places { get; set; }
        public ObservableCollection<GooglePlaceAutoCompletePrediction> RecentPlaces { get; set; } = new
            ObservableCollection<GooglePlaceAutoCompletePrediction>();

        /* Variables for the text boxes */
        bool _isOriginFocused = true;
        public bool ShowRecentPlaces { get; set; }
        string _originText, _destText;
        public string OriginText
        {
            get
            {
                return _originText;
            }
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
            get
            {
                return _destText;
            }
            set
            {
                _destText = value;
                if (!string.IsNullOrEmpty(_destText))
                {
                    _isOriginFocused = false;
                    GetPlacesCommand.Execute(_destText);
                }
            }
        }
        /* Lat / Lon Variables */
        string _originLat, _originLon;
        string _destLat, _destLon;  
        #endregion


        /* MAIN HOME PAGE MODEL */
        public HomePageViewModel()
        {
            Title = "Home";
            GoogleMapsApiService.Initialize("AIzaSyD3a7hyJLIntsMVaPMqw3x-Ptkt5xcYvso");
            GetPlacesCommand = new Command<string>(async (param) => await GetPlacesByName(param));
            GetPlaceDetailCommand = new Command<GooglePlaceAutoCompletePrediction>(async (param) => await GetPlacesDetail(param));
            Places = new ObservableCollection<GooglePlaceAutoCompletePrediction>();
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
                if (_isOriginFocused)
                {
                    OriginText = place.Name;
                    _originLat = $"{place.Latitude}";
                    _originLon = $"{place.Longitude}";
                    _isOriginFocused = false;
                    //FocusOriginCommand.Execute(null); Focuses camera on origin location
                }
                else
                {
                    _destLat = $"{place.Latitude}";
                    _destLon = $"{place.Longitude}";

                    RecentPlaces.Add(placeA);

                    if (_originLat == _destLat && _originLon == _destLon)
                    {
                        await App.Current.MainPage.DisplayAlert("Error", "Origin route should be different than destination route", "Ok");
                    }
                    else
                    {
                        //LoadRouteCommand.Execute(null); Preview route 
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