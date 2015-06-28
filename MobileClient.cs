/*
Copyright (c) 2015, Lutz Bürkle
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
        private readonly string _sjurl = "https://mclients.googleapis.com/sj/v1.11/";

        public bool LoginStatus { get; private set; }


        public GoogleMusicMobileClient() : base()
        {
            LoginStatus = false;
            _useragent = "Android-Music/1413 (tilapia KOT49H)";
        }


        #region Login

        public void Login(string login, string mastertoken)
        {
            LoginStatus = PerformOAuth(login, mastertoken);
        }


        public void Login(Tuple<string, string> mastertoken)
        {
            Login(mastertoken.Item1, mastertoken.Item2);
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


        public bool UpdateTracks(ref Tracklist tracks)
        {
            bool modified = false;

            if (tracks != null)
            {
                Tracklist newTracks = GetTracks(tracks.lastUpdatedTimestamp);

                if (newTracks != null)
                {
                    GoogleMusicItemlist<Track> tracksUpdate = UpdateItems<Track>(tracks, newTracks);

                    if (tracksUpdate != null)
                    {
                        tracks = new Tracklist(tracksUpdate);
                        modified = true;
                    }

                    tracks.lastUpdatedTimestamp = DateTime.Now;
                }
            }

            return modified;
        }


        public Playlists GetPlaylists(DateTime updateFrom)
        {
            Playlists playlists;

            string response = GoogleMusicService(Service.playlistfeed, null, updateFrom);

            if (String.IsNullOrEmpty(response))
            {
                playlists = null;
            }
            else
            {
                Playlistfeed playlistfeed = Json.Deserialize<Playlistfeed>(response);

                if (playlistfeed.data == null) return new Playlists();

                playlists = playlistfeed.data.items;

                PlaylistEntrylist entries = GetPlaylistEntries(new DateTime());

                if (entries == null)
                {
                    playlists = null;
                }
                else
                {
                    var groupedPlaylists = from entry in entries
                                           group entry by entry.playlistId into groupedEntries
                                           select new Playlist { id = groupedEntries.Key, entries = new PlaylistEntrylist(groupedEntries.OrderBy(e => e.absolutePosition)) };
                    Playlists playlistEntries = new Playlists(groupedPlaylists.ToList());

                    foreach (Playlist playlist in playlists)
                    {
                        Playlist p = playlistEntries[playlist.id];
                        if (p == null)
                            playlist.entries = new PlaylistEntrylist();
                        else
                            playlist.entries = p.entries;
                    }
                }

                var sharedPlaylists = playlists.FindAll(p => p.type == "SHARED").Select(p => new { id = p.id, shareToken = p.shareToken }).ToArray();
                if (sharedPlaylists.Length > 0)
                {
                    List<SharedPlaylistEntrylist> sharedEntries = GetSharedPlaylistEntries(sharedPlaylists.Select(p => p.shareToken));
                    foreach (var sharedPlaylist in sharedPlaylists)
                    {
                        Playlist playlist = playlists[sharedPlaylist.id];
                        SharedPlaylistEntrylist sharedEntry = sharedEntries.Find(e => e.shareToken == sharedPlaylist.shareToken);
                        if (sharedEntry == null)
                            playlist.entries = new PlaylistEntrylist();
                        else
                            playlist.entries = sharedEntry.playlistEntry;
                    }
                }
            }

            return playlists;
        }


        public Playlists GetAllPlaylists()
        {
            Playlists playlists = GetPlaylists(new DateTime());

            if (playlists != null)
            {
                playlists = new Playlists(playlists.FindAll(p => p.deleted == false));

                foreach (Playlist playlist in playlists)
                    playlist.entries = new PlaylistEntrylist(playlist.entries.FindAll(e => e.deleted == false));

                playlists.lastUpdatedTimestamp = DateTime.Now;
            }

            return playlists;
        }


        public bool UpdatePlaylists(ref Playlists playlists)
        {
            bool modified = false;

            if (playlists != null)
            {
                Playlists newPlaylists = GetPlaylists(playlists.lastUpdatedTimestamp);

                if (newPlaylists != null)
                {
                    if (newPlaylists.Count > 0)
                    {
                        Playlists playlistsUpdate = new Playlists(playlists);
                        foreach (Playlist newPlaylist in newPlaylists)
                        {
                            Playlist removePlaylist = playlists[newPlaylist.id];
                            if (removePlaylist != null)
                            {
                                if (newPlaylist.type == "USER_GENERATED")
                                {
                                    GoogleMusicItemlist<PlaylistEntry> entriesUpdate = UpdateItems<PlaylistEntry>(removePlaylist.entries, newPlaylist.entries);

                                    if (entriesUpdate != null)
                                        newPlaylist.entries = new PlaylistEntrylist(entriesUpdate.OrderBy(e => e.absolutePosition));
                                    else
                                        newPlaylist.entries = removePlaylist.entries;
                                }

                                playlistsUpdate.Remove(removePlaylist);
                            }
                            if (newPlaylist.deleted == false)
                                playlistsUpdate.Add(newPlaylist);

                        }

                        playlists = playlistsUpdate;
                        modified = true;
                    }

                    playlists.lastUpdatedTimestamp = DateTime.Now;
                }

            }

            return modified;
        }


        private PlaylistEntrylist GetPlaylistEntries(DateTime updateFrom)
        {
            PlaylistEntrylist entries;

            string response = GoogleMusicService(Service.plentryfeed, null, updateFrom);

            if (String.IsNullOrEmpty(response))
            {
                entries = null;
            }
            else
            {
                Plentryfeed plentryfeed = Json.Deserialize<Plentryfeed>(response);

                if (plentryfeed.data == null) return new PlaylistEntrylist();

                entries = plentryfeed.data.items;
            }

            return entries;
        }


        private List<SharedPlaylistEntrylist> GetSharedPlaylistEntries(IEnumerable<string> shareToken)
        {
            List<SharedPlaylistEntrylist> entries;

            string jsonString = @"{""entries"": [" + String.Join(",", shareToken.Select(s => String.Format("{{\"shareToken\":\"{0}\"}}", s)).ToArray()) + @"]}";

            string response = GoogleMusicService(Service.plentries_shared, jsonString, new DateTime());

            if (String.IsNullOrEmpty(response))
            {
                entries = null;
            }
            else
            {
                SharedPlentryfeed plentryfeed = Json.Deserialize<SharedPlentryfeed>(response);

                if (plentryfeed.entries == null) return new List<SharedPlaylistEntrylist>();

                entries = plentryfeed.entries;
            }

            return entries;
        }


        private GoogleMusicItemlist<T> UpdateItems<T>(GoogleMusicItemlist<T> inputItems, GoogleMusicItemlist<T> newItems) where T : IGoogleMusicItem
        {
            GoogleMusicItemlist<T> items = null;

            if (newItems.Count > 0)
            {
                items = new GoogleMusicItemlist<T>(inputItems);
                foreach (T newItem in newItems)
                {
                    T removeItem = items[newItem.id];
                    if (removeItem != null)
                        items.Remove(removeItem);
                    if (newItem.deleted == false)
                        items.Add(newItem);
                }
            }

            return items;
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
            catch (WebException error)
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

            string serviceString = service.ToString().Replace('_', '/');

            double updatedMin = 1e6 * updateFrom.ToUnixTime();
            if (updatedMin < 0) updatedMin = 0;

            header.Add("Authorization", String.Format("GoogleLogin Auth={0}", _credentials.Auth));

            try
            {
                response = httpResponse(httpPostRequest(_sjurl + serviceString + String.Format("?alt=json&include-tracks=true&updated-min={0}", Convert.ToUInt64(updatedMin)), jsonString, "application/json", header));
            }
            catch (WebException error)
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
                public PlaylistEntrylist items { get; set; }
            }
        }


        [DataContract]
        public class SharedPlentryfeed
        {
            [DataMember]
            public string kind { get; set; }
            [DataMember]
            public List<SharedPlaylistEntrylist> entries { get; set; }
        }


        [DataContract]
        public class SharedPlaylistEntrylist
        {
            [DataMember]
            public string responseCode { get; set; }
            [DataMember]
            public string shareToken { get; set; }
            [DataMember]
            public PlaylistEntrylist playlistEntry { get; set; }
        }

        
        private enum Service
        {
            trackfeed,
            playlistfeed,
            plentryfeed,
            plentries_shared
        }

    }

}
