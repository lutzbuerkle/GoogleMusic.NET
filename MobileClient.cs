/*
Copyright (c) 2014, Lutz Bürkle
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright
      notice, this list of conditions and the following disclaimer in the
      documentation and/or other materials provided with the distribution.
    * Neither the name of the copyright holders nor the
      names of its contributors may be used to endorse or promote products
      derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace GoogleMusic
{
    public class GoogleMusicMobileClient : GoogleMusicClient
    {
        public bool LoginStatus { get; private set; }


        public GoogleMusicMobileClient() : base()
        {
            LoginStatus = false;
            _useragent = "Android-Music/1413 (tilapia KOT49H)";
        }


        #region Login

        public void Login(string login, string passwd)
        {
            LoginStatus = ClientLogin(login, passwd);
        }


        public void Login(GoogleMusicWebClient webClient)
        {
            LoginStatus = webClient.LoginStatus;

            if (LoginStatus)
            {
                FieldInfo field = GetType().GetField("_credentials", BindingFlags.Instance | BindingFlags.NonPublic);
                _credentials = (Credentials)field.GetValue(webClient);
            }
        }


        public void Logout()
        {
            _credentials = null;
            LoginStatus = false;
        }

        #endregion


        #region GoogleMusicServices

        public Tracklist GetAllTracks()
        {
            Tracklist tracks;

            string response = GoogleMusicService(Service.trackfeed);

            if (String.IsNullOrEmpty(response))
            {
                tracks = null;
            }
            else
            {
                Trackfeed trackfeed = Json.Deserialize<Trackfeed>(response);
                tracks = trackfeed.data.items;

                string token = trackfeed.nextPageToken;

                while (!String.IsNullOrEmpty(token))
                {
                    string jsonString = @"{""start-token"":""" + token + @"""}";
                    response = GoogleMusicService(Service.trackfeed, jsonString);
                    if (!String.IsNullOrEmpty(response))
                    {
                        trackfeed = Json.Deserialize<Trackfeed>(response);
                        tracks.AddRange(trackfeed.data.items);
                        token = trackfeed.nextPageToken;
                    }
                    else
                    {
                        token = null;
                    }
                }
            }

            return tracks;
        }


        public Playlists GetPlaylists(bool includeTracks = true)
        {
            Playlists playlists;

            string response = GoogleMusicService(Service.playlistfeed);

            if (String.IsNullOrEmpty(response))
            {
                playlists = null;
            }
            else
            {
                Playlistfeed playlistfeed = Json.Deserialize<Playlistfeed>(response);
                playlists = new Playlists(playlistfeed.data.items.FindAll(p => p.deleted == false));

                if (includeTracks)
                {
                    Playlists playlistEntries = GetPlaylistEntries();
                    foreach (Playlist playlist in playlists)
                        playlist.tracks = playlistEntries[playlist.id].tracks;
                }
            }

            return playlists;
        }


        public Playlists GetPlaylistEntries()
        {
            Playlists playlists;

            string response = GoogleMusicService(Service.plentryfeed);

            if (String.IsNullOrEmpty(response))
            {
                playlists = null;
            }
            else
            {
                Plentryfeed plentryfeed = Json.Deserialize<Plentryfeed>(response);
                List<Plentryfeed.Item> items = plentryfeed.data.items.FindAll(i => i.deleted == false);
                var groupedPlaylists = from item in items
                                       orderby item.absolutePosition
                                       group item.track by item.playlistId into groupedTracks
                                       select new Playlist { id = groupedTracks.Key, tracks = new Tracklist(groupedTracks) };
                playlists = new Playlists(groupedPlaylists.ToList());
            }

            return playlists;
        }


        private string GoogleMusicService(Service service, string jsonString = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            string response;

            if (!LoginStatus)
            {
                ThrowError(String.Format("Not logged in: Service '{0}' failed!", service.ToString()));
                return null;
            }

            header.Add("Authorization", String.Format("GoogleLogin Auth={0}", _credentials.Auth));

            try
            {
                response = httpResponse(httpPostRequest("https://www.googleapis.com/sj/v1.4/" + service.ToString() + String.Format("?alt=json&include-tracks=true&updated-min={0}", 0), jsonString, "application/json", header));
            }
            catch (Exception error)
            {
                response = null;
                ThrowError(String.Format("Service '{0}' failed!", service.ToString()), error);
            }

            return response;
        }

        #endregion


        [DataContract]
        private class Trackfeed
        {
            [DataMember]
            internal string kind { get; set; }
            [DataMember]
            internal string nextPageToken { get; set; }
            [DataMember]
            internal Data data { get; set; }

            [DataContract]
            internal class Data
            {
                [DataMember]
                internal Tracklist items { get; set; }
            }
        }


        [DataContract]
        private class Playlistfeed
        {
            [DataMember]
            internal string kind { get; set; }
            [DataMember]
            internal Data data { get; set; }

            [DataContract]
            internal class Data
            {
                [DataMember]
                internal Playlists items { get; set; }
            }
        }


        [DataContract]
        private class Plentryfeed
        {
            [DataMember]
            public string kind { get; set; }
            [DataMember]
            public Data data { get; set; }

            [DataContract]
            public class Data
            {
                [DataMember]
                public List<Item> items { get; set; }
            }

            [DataContract]
            public class Item
            {
                [DataMember]
                public string kind { get; set; }
                [DataMember]
                public string id { get; set; }
                [DataMember]
                public string clientId { get; set; }
                [DataMember]
                public string playlistId { get; set; }
                [DataMember]
                public string absolutePosition { get; set; }
                [DataMember]
                public string trackId { get; set; }
                [DataMember]
                public string creationTimestamp { get; set; }
                [DataMember]
                public string lastModifiedTimestamp { get; set; }
                [DataMember]
                public bool deleted { get; set; }
                [DataMember]
                public string source { get; set; }
                [DataMember]
                public Track track { get; set; }
            }
        }
        
        
        private enum Service
        {
            trackfeed,
            playlistfeed,
            plentryfeed
        }

    }

}
