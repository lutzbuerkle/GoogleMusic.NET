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

        public Tracklist GetTracks(DateTime updateFrom)
        {
            Tracklist tracks;

            string response = GoogleMusicService(Service.trackfeed, null, updateFrom);

            if (String.IsNullOrEmpty(response))
            {
                tracks = null;
            }
            else
            {
                Trackfeed trackfeed = Json.Deserialize<Trackfeed>(response);

                if (trackfeed.data == null) return new Tracklist();

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


        public Tracklist GetAllTracks()
        {
            Tracklist tracks = GetTracks(new DateTime());

            if (tracks != null)
            {
                tracks = new Tracklist(tracks.FindAll(t => t.deleted == false));
                tracks.lastUpdatedTimestamp = DateTime.Now;
            }

            return tracks;
        }


        public bool UpdateTracks(ref Tracklist tracksInput)
        {
            bool updated = false;

            if (tracksInput != null)
            {
                Tracklist newTracks = GetTracks(tracksInput.lastUpdatedTimestamp);

                if (newTracks != null)
                {
                    if (newTracks.Count > 0)
                    {
                        Tracklist tracks = new Tracklist(tracksInput);
                        foreach (Track newTrack in newTracks)
                        {
                            Track removeTrack = tracks[newTrack.id];
                            if (removeTrack != null)
                                tracks.Remove(removeTrack);
                            if (newTrack.deleted == false)
                                tracks.Add(newTrack);
                        }
                        tracksInput = tracks;
                        updated = true;
                    }

                    tracksInput.lastUpdatedTimestamp = DateTime.Now;
                }
            }

            return updated;
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

                if (playlistfeed.data == null) return new Playlists();

                playlists = new Playlists(playlistfeed.data.items.FindAll(p => p.deleted == false));
                playlists.lastUpdatedTimestamp = DateTime.Now;

                if (includeTracks)
                {
                    Playlists playlistEntries = GetPlaylistEntries();

                    if (playlistEntries != null)
                    {
                        foreach (Playlist playlist in playlists)
                        {
                            Playlist p = playlistEntries[playlist.id];
                            if (p == null)
                                playlist.tracks = new Tracklist();
                            else
                                playlist.tracks = p.tracks;
                        }
                    }
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

                if (plentryfeed.data == null) return new Playlists();

                foreach (Plentryfeed.Item item in plentryfeed.data.items)
                    if (item.track == null) item.track = new Track { id = item.trackId };
                List<Plentryfeed.Item> items = plentryfeed.data.items.FindAll(i => i.deleted == false);
                var groupedPlaylists = from item in items
                                       orderby item.absolutePosition
                                       group item.track by item.playlistId into groupedTracks
                                       select new Playlist { id = groupedTracks.Key, tracks = new Tracklist(groupedTracks) };
                playlists = new Playlists(groupedPlaylists.ToList());
            }

            return playlists;
        }


        public StreamUrl GetStreamUrl(string track_id, ulong device_id)
        {
            HttpWebRequest request;
            StreamUrl streamUrl = new StreamUrl();
            byte[] _s1 = Convert.FromBase64String("VzeC4H4h+T2f0VI180nVX8x+Mb5HiTtGnKgH52Otj8ZCGDz9jRWyHb6QXK0JskSiOgzQfwTY5xgLLSdUSreaLVMsVVWfxfa8Rw==");
            byte[] _s2 = Convert.FromBase64String("ZAPnhUkYwQ6y5DdQxWThbvhJHN8msQ1rqJw0ggKdufQjelrKuiGGJI30aswkgCWTDyHkTGK9ynlqTkJ5L4CiGGUabGeo8M6JTQ==");

            if (String.IsNullOrEmpty(track_id)) return null;

            if (!LoginStatus)
            {
                ThrowError("Not logged in: Obtaining Stream Url failed!");
                return null;
            }

            byte[] key = new byte[_s1.Length];
            for (int i = 0; i < _s1.Length; i++)
                key[i] = (byte)(_s1[i] ^ _s2[i]);

            string salt = (1000.0 * DateTime.UtcNow.ToUnixTime()).ToString("F0");
            HMACSHA1 hmac_sha1 = new HMACSHA1(key);

            byte[] hash = hmac_sha1.ComputeHash(Encoding.ASCII.GetBytes(track_id + salt));
            string sig = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", ".");
            sig = sig.Remove(sig.Length - 1);

            try
            {
                if (track_id.IsGuid())
                    request = httpGetRequest("https://android.clients.google.com/music/mplay" + String.Format("?songid={0}&opt=hi&net=wifi&pt=e&slt={1}&sig={2}", track_id, salt, sig));
                else
                    request = httpGetRequest("https://android.clients.google.com/music/mplay" + String.Format("?mjck={0}&opt=hi&net=wifi&pt=e&slt={1}&sig={2}", track_id, salt, sig));
                request.Headers["Authorization"] = String.Format("GoogleLogin Auth={0}", _credentials.Auth);
                request.Headers["X-Device-ID"] = device_id.ToString();
                request.AllowAutoRedirect = false;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    DateTime expires;
                    if (DateTime.TryParseExact(response.Headers["Expires"], "ddd, dd MMM yyyy HH:mm:ss GMT", CultureInfo.InvariantCulture, DateTimeStyles.None, out expires))
                        streamUrl.expires = expires;
                    streamUrl.url = response.Headers["Location"];
                }
            }
            catch (Exception error)
            {
                ThrowError("Obtaining Stream Url failed!", error);
                return null;
            }

            return streamUrl;
        }


        public StreamUrl GetStreamUrl(string track_id, string device_id)
        {
            ulong id;

            if (String.IsNullOrEmpty(device_id)) return null;
            
            if (device_id.StartsWith("0x"))
                device_id = device_id.Substring(2);

            if (UInt64.TryParse(device_id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id))
                return GetStreamUrl(track_id, id);
            else
                return null;
        }


        private string GoogleMusicService(Service service, string jsonString = null, DateTime updateFrom = new DateTime())
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            string response;

            if (!LoginStatus)
            {
                ThrowError(String.Format("Not logged in: Service '{0}' failed!", service.ToString()));
                return null;
            }

            double updatedMin = 1e6 * updateFrom.ToUnixTime();
            if (updatedMin < 0) updatedMin = 0;

            header.Add("Authorization", String.Format("GoogleLogin Auth={0}", _credentials.Auth));

            try
            {
                response = httpResponse(httpPostRequest("https://www.googleapis.com/sj/v1.5/" + service.ToString() + String.Format("?alt=json&include-tracks=true&updated-min={0}", Convert.ToUInt64(updatedMin)), jsonString, "application/json", header));
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
