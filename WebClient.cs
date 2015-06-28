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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Utilities.JsArray;

namespace GoogleMusic
{
    public class GoogleMusicWebClient : GoogleMusicClient
    {
        private readonly string[] trackProperties = {
            "id", "title", "_albumArtUrl", "artist", "album", "albumArtist", "titleNorm",
            "artistNorm", "albumNorm", "albumArtistNorm", "composer", "genre", null, "durationMillis",
            "track", "totalTracks", "disc", "totalDiscs", "year", "deleted", null, null,
            "playCount", "rating", "_creationTimestamp", "_lastModifiedTimestamp", "subjectToCuration", "storeId",
            "nid", "type", "comment", null, "albumId", "_artistId",
            "bitrate", "_recentTimestamp", "_artistArtUrl", null, "explicitType"
        };
        private readonly string[] playlistProperties = {
            "id", "name", "_creationTimestamp", "_lastModifiedTimestamp", "type", "shareToken",
            null, null, "ownerName", null, "ownerProfilePhotoUrl"
        };

        private JsArray _parser = new JsArray();
        private string _sessionId;

        public bool LoginStatus { get; private set; }


        public GoogleMusicWebClient() : base()
        {
            LoginStatus = false;
            _useragent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1)";
        }


        #region Login

        public void Login(string login, string mastertoken)
        {
            LoginStatus = PerformOAuth(login, mastertoken);

            if (LoginStatus)
            {
                LoginStatus = GetAuthenticationCookies();
                _sessionId = RandomAlphaNumString(12);
            }
        }


        public void Login(Tuple<string, string> mastertoken)
        {
            Login(mastertoken.Item1, mastertoken.Item2);
        }


        public void Login(GoogleMusicMobileClient mobileClient)
        {
            LoginStatus = mobileClient.LoginStatus;

            if (LoginStatus)
            {
                FieldInfo field = GetType().GetField("_credentials", BindingFlags.Instance | BindingFlags.NonPublic);
                _credentials = (Credentials)field.GetValue(mobileClient);

                LoginStatus = GetAuthenticationCookies();
                _sessionId = RandomAlphaNumString(12);
            }
        }


        public void Logout()
        {
            _credentials = null;
            _sessionId = null;
            LoginStatus = false;
        }

        #endregion


        #region GoogleMusicServices

        public Status GetStatus()
        {
            Status status = null;

            string response = GoogleMusicService(Service.getstatus);

            if (!String.IsNullOrEmpty(response))
            {
                status = Json.Deserialize<Status>(response);
            }

            return status;
        }


        public int GetTrackCount()
        {
            Status status = GetStatus();

            if (status == null)
                return 0;
            else
                return status.availableTracks;
        }


        public Settings GetSettings()
        {
            Settings settings = null;

            string response = GoogleMusicService(Service.loadsettings);

            if (!String.IsNullOrEmpty(response))
            {
                settings = Json.Deserialize<GetSettingsResponse>(response).settings;
            }

            return settings;
        }


        public List<Device> GetRegisteredDevices()
        {
            Settings settings = GetSettings();

            if (settings == null)
                return null;
            else
                return settings.devices;
        }


        public Tracklist GetAllTracks()
        {
            Tracklist tracks = null;
            int index = 0;

            string response = GoogleMusicService(Service.streamingloadalltracks);

            if (!String.IsNullOrEmpty(response))
            {
                tracks = new Tracklist();
                tracks.lastUpdatedTimestamp = DateTime.Now;
                while ((index = response.IndexOf("['slat_process']", index)) > 0)
                {
                    index += 17;
                    int length = response.IndexOf("['slat_progress']", index) - index - 17;
                    ArrayList array = _parser.Parse(response.Substring(index, length));

                    foreach (ArrayList t in (array[0] as ArrayList))
                    {
                        Track track = new Track();
                        for (int i = 0; i < trackProperties.Length; i++)
                        {
                            string property = trackProperties[i];
                            if (!String.IsNullOrEmpty(property))
                            {
                                MethodInfo info = typeof(Track).GetMethod("set_" + property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (info != null && i < t.Count)
                                {
                                    object ti = t[i];
                                    if (ti != null) ti = Convert.ChangeType(ti, info.GetParameters()[0].ParameterType);
                                    info.Invoke(track, new[] { ti });
                                }
                                else
                                    ThrowError(String.Format("Track property '{0}' not matched!", property));
                            }
                        }
                        tracks.Add(track);
                    }
                }
            }

            return tracks;
        }


        public Playlists GetPlaylists(bool includeTracks = true)
        {
            Playlists playlists = null;
            string jsArray = String.Format(@"[[""{0}"",1],[""all""]]", _sessionId);

            string response = GoogleMusicService(Service.loadplaylists, jsArray);

            if (!String.IsNullOrEmpty(response))
            {
                ArrayList array = (_parser.Parse(response)[1] as ArrayList)[0] as ArrayList;

                playlists = new Playlists();
                playlists.lastUpdatedTimestamp = DateTime.Now;
                foreach (ArrayList pl in array)
                {
                    Playlist playlist = new Playlist();
                    for (int i = 0; i < playlistProperties.Length; i++)
                    {
                        string property = playlistProperties[i];
                        if (!String.IsNullOrEmpty(property))
                        {
                            MethodInfo info = typeof(Playlist).GetMethod("set_" + property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (info != null && i < pl.Count)
                            {
                                object pli = pl[i];
                                if (pli != null) pli = Convert.ChangeType(pli, info.GetParameters()[0].ParameterType);
                                info.Invoke(playlist, new[] { pli });
                            }
                            else
                                ThrowError(String.Format("Playlist property '{0}' not matched!", property));
                        }
                    }
                    playlists.Add(playlist);
                }

                if (includeTracks)
                {
                    foreach (Playlist playlist in playlists)
                        playlist.entries = GetPlaylistEntries(playlist.id).entries;
                }
            }

            return playlists;
        }


        public Playlist GetPlaylistEntries(string playlist_id)
        {
            if (String.IsNullOrEmpty(playlist_id)) return null;

            Playlist playlist = null;

            string jsArray = String.Format(@"[[""{0}"",1],[""{1}""]]", _sessionId, playlist_id);

            string response = GoogleMusicService(Service.loaduserplaylist, jsArray);

            if (!String.IsNullOrEmpty(response))
            {
                playlist = new Playlist { id = playlist_id };

                ArrayList array = _parser.Parse(response)[1] as ArrayList;

                if (array.Count > 0)
                {
                    array = array[0] as ArrayList;

                    foreach (ArrayList t in array)
                    {
                        Track track = new Track();
                        for (int i = 0; i < trackProperties.Length; i++)
                        {
                            string property = trackProperties[i];
                            if (!String.IsNullOrEmpty(property))
                            {
                                MethodInfo info = typeof(Track).GetMethod("set_" + property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (info != null && i < t.Count)
                                {
                                    object ti = t[i];
                                    if (ti != null) ti = Convert.ChangeType(ti, info.GetParameters()[0].ParameterType);
                                    info.Invoke(track, new[] { ti });
                                }
                                else
                                    ThrowError(String.Format("Track property '{0}' not matched!", property));
                            }
                        }
                        playlist.entries.Add(new PlaylistEntry { track = track });
                    }
                }
            }

            return playlist;
        }


        public string CreatePlaylist(string name = "New Playlist", bool publish = false)
        {
            string playlist_id = null;
            string data = @"[[""" + _sessionId + @""",1],[" + publish.ToString().ToLower() + @",""" + name + @""",null,[]]]";

            string response = GoogleMusicService(Service.createplaylist, data);

            if (!String.IsNullOrEmpty(response))
            {
                ArrayList array = _parser.Parse(response);
                if (array.Count > 1)
                    playlist_id = (array[1] as ArrayList)[0] as string;
            }

            return playlist_id;
        }


        public bool ChangeSongMetadata(string track_id, Dictionary<MetaKey, object> metadata)
        {
            return ChangeSongMetadata(new string[] { track_id }, new Dictionary<MetaKey, object>[] { metadata });
        }


        public bool ChangeSongMetadata(IEnumerable<string> track_ids, IEnumerable<Dictionary<MetaKey, object>> changes)
        {
            if (track_ids == null) throw new ArgumentNullException("Argument 'track_ids' in ChangeSongMetadata must not be NULL!");
            if (changes == null) throw new ArgumentNullException("Argument 'changes' in ChangeSongMetadata must not be NULL!");
            if (track_ids.Count() != changes.Count()) throw new ArgumentException("Inconsistent data count of arguments 'track_ids' and 'changes'!");

            bool success = false;

            ArrayList tracks = new ArrayList();

            using (var changesEnumerator = changes.GetEnumerator())
            {
                foreach (string track_id in track_ids)
                {
                    if (!changesEnumerator.MoveNext()) break;
                    Dictionary<MetaKey, object> metadata = changesEnumerator.Current;

                    ArrayList track = new ArrayList();
                    foreach (string property in trackProperties)
                    {
                        if (property == "id")
                            track.Add(track_id);
                        else
                        {
                            try
                            {
                                MetaKey key = (MetaKey)Enum.Parse(typeof(MetaKey), property);
                                if (metadata.Keys.Contains(key) && Type.GetTypeCode(metadata[key].GetType()) == (TypeCode)(Convert.ToInt32(key) % 0x100))
                                {
                                    track.Add(metadata[key]);
                                }
                                else
                                    track.Add(null);
                            }
                            catch (ArgumentException)
                            {
                                track.Add(null);
                            }

                        }
                    }
                    track.Add(new ArrayList());

                    tracks.Add(track);
                }
            }
            ArrayList array = new ArrayList { new ArrayList { _sessionId, 1 } };
            array.Add(new ArrayList { tracks });

            string data = array.ToJsArray();
            string response = GoogleMusicService(Service.modifytracks, data);

            if (response != null) success = true;

            return success;
        }


        private string GoogleMusicService(Service service, string data = null)
        {
            string response;
            Dictionary<string, string> header = new Dictionary<string, string>();
            CookieCollection cookies = new CookieCollection();

            if (!LoginStatus)
            {
                ThrowError(String.Format("Not logged in: Service '{0}' failed!", service.ToString()));
                return null;
            }

            string query = String.Format("?u=0&xt={0}&format=jsarray", _credentials.xt);

            cookies = _credentials.cookieJar.GetCookies(new Uri("https://play.google.com/music/listen"));

            if (cookies != null)
            {
                string cookieData = "";
                foreach (Cookie cookie in cookies)
                {
                    cookieData += String.Format("{0}={1}; ", cookie.Name, cookie.Value);
                }
                header.Add("Cookie", cookieData);
            }

            try
            {
                response = httpResponse(httpPostRequest("https://play.google.com/music/services/" + service.ToString() + query, data, "application/x-www-form-urlencoded", header));
            }
            catch (WebException error)
            {
                response = null;
                ThrowError(String.Format("Service '{0}' failed!", service.ToString()), error);
            }

            return response;
        }


        public List<StreamUrl> GetStreamUrl(string track_id)
        {
            List<StreamUrl> streamUrls = new List<StreamUrl>();
            HttpWebRequest request;
            string response;
            const string key = "27f7313e-f75d-445a-ac99-56386a5fe879";

            if (String.IsNullOrEmpty(track_id)) return null;

            if (!LoginStatus)
            {
                ThrowError("Not logged in: Obtaining Stream Url failed!");
                return null;
            }

            HMACSHA1 hmac_sha1 = new HMACSHA1(Encoding.ASCII.GetBytes(key));

            byte[] hash = hmac_sha1.ComputeHash(Encoding.ASCII.GetBytes(track_id + _sessionId));
            string sig = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", ".");

            try
            {
                if (track_id.IsGuid())
                {
                    request = httpGetRequest("https://play.google.com/music/play" + String.Format("?u=0&slt={0}&songid={1}&sig={2}&pt=e", _sessionId, track_id, sig));
                }
                else
                {
                    request = httpGetRequest("https://play.google.com/music/play" + String.Format("?u=0&slt={0}&mjck={1}&sig={2}&pt=e", _sessionId, track_id, sig));
                }
                request.CookieContainer = _credentials.cookieJar;
                response = httpResponse(request);
            }
            catch (WebException error)
            {
                ThrowError("Obtaining stream Url failed!", error);
                return null;
            }

            foreach (string url in Json.Deserialize<StreamUrlResponse>(response).urls)
            {
                DateTime expires = new DateTime();
                Match match = Regex.Match(url, @"expire=(?<EPOCH>\d+)");
                if (match.Success)
                {
                    double epoch = Convert.ToDouble(match.Groups["EPOCH"].Value);
                    expires = epoch.FromUnixTime().ToLocalTime();
                }
                streamUrls.Add(new StreamUrl() { url = url, expires = expires });
            }

            return streamUrls;
        }


        public byte[] GetStreamAudio(IEnumerable<StreamUrl> streamUrls)
        {
            if (streamUrls == null) throw new ArgumentNullException("Argument 'streamUrls' in GetStreamAudio must not be NULL!");

            byte[] audio = { };
            List<StreamUrl> urls = streamUrls.ToList();

            if (urls.Count == 1)
                audio = GetStreamAudio(urls[0]);
            else if (urls.Count > 1)
            {
                try
                {
                    object locker = new object();
                    byte[][] audioParts = new byte[urls.Count][];
                    Parallel.For(0, urls.Count, new ParallelOptions { MaxDegreeOfParallelism = 5 }, i =>
                    {
                        using (WebClient client = new WebClient())
                        {
                            client.Proxy = Proxy;
                            client.Headers.Add("user-agent", _useragent);

                            byte[] audioPart = client.DownloadData(urls[i].url);
                            lock (locker)
                            {
                                audioParts[i] = audioPart;
                            }
                        }
                    });
                    Match match = Regex.Match(urls.Last().url, @"range=(\d+)\-(\d+)");
                    int size = Convert.ToInt32(match.Groups[2].Value);
                    audio = new byte[size + 1];
                    for (int i = 0; i < urls.Count; i++)
                    {
                        match = Regex.Match(urls[i].url, @"range=(\d+)\-(\d+)");
                        int start = Convert.ToInt32(match.Groups[1].Value);
                        int stop = Convert.ToInt32(match.Groups[2].Value);
                        if (audio.Length < stop + 1)
                            Array.Resize<byte>(ref audio, stop + 1);
                        Buffer.BlockCopy(audioParts[i], 0, audio, start, audioParts[i].Length);
                    }
                }
                catch (WebException error)
                {
                    ThrowError("Retrieving audio stream failed!", error);
                    audio = new byte[] { };
                }
            }

            return audio;
        }

        #endregion


        private string RandomAlphaNumString(int length)
        {
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            char[] randChars = new char[length];
            Random random = new Random();

            for (int i = 0; i < length; i++)
            {
                randChars[i] = chars[random.Next(chars.Length)];
            }

            return new String(randChars);
        }


        [DataContract]
        private class GetSettingsResponse
        {
            [DataMember]
            public Settings settings { get; set; }
        }


        [DataContract]
        private class StreamUrlResponse
        {
            [DataMember]
            public string url { get { return String.Empty; } set { urls = new List<string>(); urls.Add(value); } }
            [DataMember]
            public List<string> urls { get; set; }
        }


        private enum Service
        {
            getstatus,
            loadsettings,
            streamingloadalltracks,
            loadplaylists,
            loaduserplaylist,
            createplaylist,
            modifytracks,
        }


        public enum MetaKey
        {
            album = TypeCode.String,
            albumArtist = 0x100 + TypeCode.String,
            artist = 0x200 + TypeCode.String,
            composer = 0x300 + TypeCode.String,
            disc = 0x400 + TypeCode.Int32,
            genre = 0x500 + TypeCode.String,
            title = 0x600 + TypeCode.String,
            playCount = 0x700 + TypeCode.Int32,
            rating = 0x800 + TypeCode.Int32,
            totalDiscs = 0x900 + TypeCode.Int32,
            totalTracks = 0xa00 + TypeCode.Int32,
            track = 0xb00 + TypeCode.Int32,
            year = 0xc00 + TypeCode.Int32
        }

    }

}
