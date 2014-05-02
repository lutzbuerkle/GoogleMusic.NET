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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.JsArray;

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


        public GoogleMusicWebClient()
        {
            LoginStatus = false;
            _useragent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1)";
        }


        #region Login

        public void Login(string login, string passwd)
        {
            LoginStatus = ClientLogin(login, passwd);

            if (LoginStatus)
            {
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
            {
                return 0;
            }
            else
            {
                return status.availableTracks;
            }
        }


        public Settings GetSettings()
        {
            Settings settings = null;

            string response = GoogleMusicService(Service.loadsettings);

            if (!String.IsNullOrEmpty(response))
            {
                settings = Json.Deserialize<Settings>(response);
            }

            return settings;
        }


        public Tracklist GetAllTracks()
        {
            Tracklist tracks = new Tracklist();
            int index = 0;

            string response = GoogleMusicCall(Call.streamingloadalltracks);

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

            return tracks;
        }


        public Playlists GetPlaylists(bool includeTracks = true)
        {
            Playlists playlists = new Playlists();
            string jsArray = String.Format(@"[[""{0}"",1],[""all""]]", _sessionId);

            string response = GoogleMusicCall(Call.loadplaylists, jsArray);

            ArrayList array = (_parser.Parse(response)[1] as ArrayList)[0] as ArrayList;

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
                foreach (Playlist pl in playlists)
                    pl.tracks = GetPlaylist(pl.id).tracks;
            }

            return playlists;
        }


        public Playlist GetPlaylist(string playlist_id)
        {
            Playlist playlist = null;
            string jsArray = String.Format(@"[[""{0}"",1],[""{1}""]]", _sessionId, playlist_id);

            string response = GoogleMusicCall(Call.loaduserplaylist, jsArray);

            ArrayList array = (_parser.Parse(response)[1] as ArrayList)[0] as ArrayList;

            if (array.Count > 0)
            {
                playlist = new Playlist { id = playlist_id };
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
                    playlist.tracks.Add(track);
                }
            }

            return playlist;
        }


        public string CreatePlaylist(string name)
        {
            string playlist_id = null;
            string jsonString = String.Format(@"{{""name"":""{0}""}}", name);

            string response = GoogleMusicService(Service.createplaylist, jsonString);

            if (!String.IsNullOrEmpty(response))
            {
                playlist_id = Json.Deserialize<CreatePlaylistResponse>(response).id;
            }

            return playlist_id;
        }


        public bool DeletePlaylist(string playlist_id)
        {
            bool success = false;
            string jsonString = String.Format(@"{{""id"":""{0}""}}", playlist_id);

            string response = GoogleMusicService(Service.deleteplaylist, jsonString);

            if (!String.IsNullOrEmpty(response))
            {
                success = true;
            }

            return success;
        }


        public bool RenamePlaylist(string playlist_id, string new_name)
        {
            bool success = false;
            string jsonString = String.Format(@"{{""id"":""{0}"",""name"":""{1}""}}", playlist_id, new_name);

            string response = GoogleMusicService(Service.editplaylist, jsonString);

            if (!String.IsNullOrEmpty(response))
            {
                success = true;
            }

            return success;
        }


        public bool AddToPlaylist(string playlist_id, string song_id)
        {
            return AddToPlaylist(playlist_id, new string[] {song_id});
        }


        public bool AddToPlaylist(string playlist_id, IEnumerable<string> song_ids)
        {
            bool success = false;
            string song_refs = "";

            foreach (string song_id in song_ids)
                song_refs += String.Format(@"{{""id"":""{0}"",""type"":1}}, ", song_id);

            string jsonString = String.Format(@"{{""playlistId"":""{0}"",""songRefs"":[{1}]}}", playlist_id, song_refs);

            string response = GoogleMusicService(Service.addtoplaylist, jsonString);

            if (!String.IsNullOrEmpty(response))
            {
                success = true;
            }

            return success;
        }


        public bool ChangePlaylistOrder(string playlist_id, IEnumerable<string> song_ids_moving, IEnumerable<string> entry_ids_moving, string after_entry_id = "", string before_entry_id = "")
        {
            bool success = false;
            string jsonString = String.Format(@"{{""playlistId"":""{0}"",""movedSongIds"":[""{1}""],""movedEntryIds"":[""{2}""],""afterEntryId"":""{3}"",""beforeEntryId"":""{4}""}}", playlist_id, String.Join("\",\"", song_ids_moving), String.Join("\",\"", entry_ids_moving), after_entry_id, before_entry_id);

            string response = GoogleMusicService(Service.changeplaylistorder, jsonString);

            if (!String.IsNullOrEmpty(response))
            {
                success = true;
            }

            return success;
        }


        public bool DeleteSongs(IEnumerable<string> song_ids)
        {
            return DeleteSongs("all", song_ids, new string[] {});
        }


        public bool DeleteSongs(string playlist_id, IEnumerable<string> song_ids, IEnumerable<string> entry_ids)
        {
            bool success = false;
            string jsonString = String.Format(@"{{""songIds"":[""{0}""],""entryIds"":[""{1}""],""listId"":""{2}""}}", String.Join("\",\"", song_ids), String.Join("\",\"", entry_ids), playlist_id);

            string response = GoogleMusicService(Service.deletesong, jsonString);

            if (!String.IsNullOrEmpty(response))
            {
                success = true;
            }

            return success;
        }


        public bool ChangeSongMetadata(string song_id, Dictionary<MetaKey, object> metadata)
        {
            bool success = false;
            string data = "";

            foreach (MetaKey key in metadata.Keys)
            {
                if (Type.GetTypeCode(metadata[key].GetType()) == (TypeCode)(Convert.ToInt32(key) % 0x100))
                {
                    string value = metadata[key] is string ? "\"" + metadata[key].ToString() + "\"" : metadata[key].ToString();
                    data += "\"" + key.ToString() + "\":" + value + ",";
                }
            }
            data = data.TrimEnd(',');
            
            string jsonString = String.Format(@"{{""entries"":[{{""id"":""{0}"",{1}}}]}}", song_id, data);

            string response = GoogleMusicService(Service.modifyentries, jsonString);

            if (!String.IsNullOrEmpty(response))
            {
                success = Json.Deserialize<GenericServiceResponse>(response).success;
            }

            return success;
        }


        private string GoogleMusicService(Service service, string jsonString = null)
        {
            string response;
            Dictionary<string, string> data = new Dictionary<string, string>();
            Dictionary<string, string> header = new Dictionary<string, string>();
            CookieCollection cookies = new CookieCollection();

            if (!LoginStatus)
            {
                ThrowError(String.Format("Not logged in: Service '{0}' failed!", service.ToString()));
                return null;
            }

            string query = String.Format("?u=0&xt={0}", _credentials.xt);

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

            if (!String.IsNullOrEmpty(jsonString)) data.Add("json", jsonString);

            try
            {
                response = httpResponse(httpPostRequest("https://play.google.com/music/services/" + service.ToString() + query, data, header));
            }
            catch (Exception error)
            {
                response = null;
                ThrowError(String.Format("Service '{0}' failed!", service.ToString()), error);
            }

            return response;
        }


        private string GoogleMusicCall(Call call, string data = null)
        {
            string response;
            Dictionary<string, string> header = new Dictionary<string, string>();
            CookieCollection cookies = new CookieCollection();

            if (!LoginStatus)
            {
                ThrowError(String.Format("Not logged in: Service '{0}' failed!", call.ToString()));
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
                response = httpResponse(httpPostRequest("https://play.google.com/music/services/" + call.ToString() + query, data, "application/x-www-form-urlencoded", header));
            }
            catch (Exception error)
            {
                response = null;
                ThrowError(String.Format("Service '{0}' failed!", call.ToString()), error);
            }

            return response;
        }

            
        public StreamUrl GetStreamUrl(string song_id, string preview_token = null)
        {
            HttpWebRequest request;
            string response;

            if (!LoginStatus) return null;

            try
            {
                if (!String.IsNullOrEmpty(preview_token))
                    request = httpGetRequest("https://play.google.com/music/playpreview" + String.Format("?mode=streaming&preview={0}&tid={1}&pt=e", preview_token, song_id));
                else
                    request = httpGetRequest("https://play.google.com/music/play" + String.Format("?songid={0}&pt=e", song_id));
                request.CookieContainer = _credentials.cookieJar;
                response = httpResponse(request);
            }
            catch(Exception error)
            {
                ThrowError("Obtaining Stream Url failed!", error);
                return null;
            }

            return Json.Deserialize<StreamUrl>(response);
        }


        public StreamUrl GetStreamUrl(Track track)
        {
            return GetStreamUrl(track.id, track.storeId);
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
        private class GenericServiceResponse
        {
            [DataMember(Name = "success")]
            public bool success { get; set; }
        }


        [DataContract]
        private class CreatePlaylistResponse
        {
            [DataMember(Name = "id")]
            public String id { get; set; }
            [DataMember(Name = "title")]
            public String title { get; set; }
            [DataMember(Name = "success")]
            public bool success { get; set; }
        }


        private enum Call
        {
            streamingloadalltracks,
            loadplaylists,
            loaduserplaylist,
        }


        private enum Service
        {
            addtoplaylist,
            changeplaylistorder,
            createplaylist,
            deleteplaylist,
            deletesong,
            editplaylist,
            fixsongmatch,
            getstatus,
            imageupload,
            loadsettings,
            modifyentries,
            modifysettings,
            multidownload,
            recommendedforyou,
            search,
            sharedwithme
        }


        public enum MetaKey
        {
            album = TypeCode.String,
            albumArtist = 0x100 + TypeCode.String,
            artist = 0x200 + TypeCode.String,
            composer = 0x300 + TypeCode.String,
            disc = 0x400 + TypeCode.Int32,
            genre = 0x500 + TypeCode.String,
            name = 0x600 + TypeCode.String,
            playCount = 0x700 + TypeCode.Int32,
            rating = 0x800 + TypeCode.Int32,
            totalDiscs = 0x900 + TypeCode.Int32,
            totalTracks = 0xa00 + TypeCode.Int32,
            track = 0xb00 + TypeCode.Int32,
            year = 0xc00 + TypeCode.Int32
        }

    }

}
