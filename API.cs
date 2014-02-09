/*
Copyright (c) 2013, Lutz Bürkle
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
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace GoogleMusic
{
    public class GoogleMusicClient
    {
        private const string _useragent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1)";

        private Credentials _credentials;

        public delegate void ErrorHandlerDelegate(string message, Exception error);

        public bool LoginStatus { get; private set; }
        public WebProxy Proxy { get; set; }
        public ErrorHandlerDelegate ErrorHandler { get; set; }


        public GoogleMusicClient()
        {
            LoginStatus = false;
            Proxy = null;
            ErrorHandler = null;
        }


        #region Login

        public void Login(string login, string passwd)
        {
            bool success = ClientLogin(login, passwd);
            if (success) GetAuthenticationCookies();
        }


        private bool ClientLogin(string login, string passwd)
        {
            string response;
            Dictionary<string, string> data = new Dictionary<string, string>();

            LoginStatus = false;
            _credentials = new Credentials();

            data.Add("accountType", "GOOGLE");
            data.Add("Email", login);
            data.Add("Passwd", passwd);
            data.Add("service", "sj");

            try
            {
                response = httpResponse(httpPostRequest("https://www.google.com/accounts/ClientLogin", data));
            }
            catch(Exception error)
            {
                ThrowError("Login failed!", error);
                return false;
            }

            Regex regex = new Regex("SID=(.+)\nLSID=(.+)\nAuth=(.+)\n");
            Match match = regex.Match(response);
            if (match.Success)
            {
                _credentials.SID = match.Groups[1].Value;
                _credentials.LSID = match.Groups[2].Value;
                _credentials.Auth = match.Groups[3].Value;
            }
            else
            {
                ThrowError("Login failed!");
                return false;
            }

            return true;
        }


        private void GetAuthenticationCookies()
        {
            HttpWebRequest request;
            CookieContainer cookieJar = new CookieContainer();
            Dictionary<string, string> data = new Dictionary<string, string>();

            data.Add("SID", _credentials.SID);
            data.Add("LSID", _credentials.LSID);
            data.Add("service", "gaia");

            try
            {
                request = httpPostRequest("https://www.google.com/accounts/IssueAuthToken", data);
                request.CookieContainer = cookieJar;
                string token = httpResponse(request);

                request = httpGetRequest("https://www.google.com/accounts/TokenAuth" + String.Format("?auth={0}&service=sj&continue=", token) + "https://play.google.com/music/listen?u=0&hl=en&source=jumper");
                request.CookieContainer = cookieJar;
                httpResponse(request);

                request = httpGetRequest("https://play.google.com/music/listen?u=0&hl=en");
                request.CookieContainer = cookieJar;
                httpResponse(request);
            }
            catch(Exception error)
            {
                ThrowError("Authentication failed!", error);
                return;
            }
            
            _credentials.cookieJar = cookieJar;

            CookieCollection cookies = cookieJar.GetCookies(new Uri("https://play.google.com/music/listen"));
            _credentials.xt = cookies["xt"].Value;
            _credentials.Expires = cookies["sjsaid"].Expires;

            LoginStatus = true;
        }


        public void Logout()
        {
            _credentials = null;
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
                status = JsonConvert.DeserializeObject<Status>(response);
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
                settings = JsonConvert.DeserializeObject<Settings>(response);
            }

            return settings;
        }


        public Playlist GetAllTracks()
        {
            Playlist playlist;
            string response = GoogleMusicService(Service.loadalltracks);

            if (String.IsNullOrEmpty(response))
            {
                playlist = null;
            }
            else
            {
                playlist = JsonConvert.DeserializeObject<Playlist>(response);

                string token = playlist.continuationToken;

                while (!String.IsNullOrEmpty(token))
                {
                    string jsonString = "{\"continuationToken\":\"" + token + "\"}";
                    response = GoogleMusicService(Service.loadalltracks, jsonString);
                    if (!String.IsNullOrEmpty(response))
                    {
                        Playlist playlist_contd = JsonConvert.DeserializeObject<Playlist>(response);
                        playlist.tracks.AddRange(playlist_contd.tracks);
                        token = playlist_contd.continuationToken;
                    }
                    else
                    {
                        token = null;
                    }
                }
                playlist.continuationToken = null;
            }

            return playlist;
        }


        public object GetPlaylist(string playlist_id = "all")
        {
            object playlist;
            string jsonString = (playlist_id == "all") ? null : String.Format(@"{{""id"":""{0}""}}", playlist_id);

            string response = GoogleMusicService(Service.loadplaylist, jsonString);

            if (String.IsNullOrEmpty(response))
            {
                playlist = null;
            }
            else if (playlist_id == "all")
            {
                playlist = JsonConvert.DeserializeObject<Playlists>(response);
            }
            else
            {
                playlist = JsonConvert.DeserializeObject<Playlist>(response);
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
                playlist_id = JsonConvert.DeserializeObject<CreatePlaylistResponse>(response).id;
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
                success = JsonConvert.DeserializeObject<GenericServiceResponse>(response).success;
            }

            return success;
        }


        private string GoogleMusicService(Service service, string jsonString = null)
        {
            string response;
            Dictionary<string, string> data = new Dictionary<string, string>();
            CookieCollection cookies = new CookieCollection();

            if (!LoginStatus)
            {
                ThrowError(String.Format("Not logged in: Service '{0}' failed!", service.ToString()));
                return null;
            }

            cookies = _credentials.cookieJar.GetCookies(new Uri("https://play.google.com/music/listen"));

            data.Add("u", "0");
            data.Add("xt", _credentials.xt);

            if (!String.IsNullOrEmpty(jsonString)) data.Add("json", jsonString);

            try
            {
                response = httpResponse(httpPostRequest("https://play.google.com/music/services/" + service.ToString(), data, cookies));
            }
            catch (Exception error)
            {
                response = null;
                ThrowError(String.Format("Service '{0}' failed!", service.ToString()), error);
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

            return JsonConvert.DeserializeObject<StreamUrl>(response);
        }


        public StreamUrl GetStreamUrl(Track track)
        {
            return GetStreamUrl(track.id, track.previewToken);
        }

        #endregion


        #region HttpRequests

        private HttpWebRequest httpGetRequest(string url)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "GET";
            request.Proxy = Proxy;
            request.UserAgent = _useragent;

            return request;
        }


        private HttpWebRequest httpPostRequest(string url, Dictionary<string, string> postParameters, CookieCollection cookies = null)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "POST";
            request.Proxy = Proxy;
            request.UserAgent = _useragent;

            string postData = "";

            foreach (string key in postParameters.Keys)
            {
                postData += HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(postParameters[key]) + "&";
            }
            postData = postData.TrimEnd('&');

            byte[] data = Encoding.ASCII.GetBytes(postData);

            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;

            if (cookies != null)
            {
                string cookieData = "";
                foreach (Cookie cookie in cookies)
                {
                    cookieData += String.Format("{0}={1}; ", cookie.Name, cookie.Value);
                }
                request.Headers["Cookie"] = cookieData;
            }

            using (Stream dataStream = request.GetRequestStream())
            {
                dataStream.Write(data, 0, data.Length);
                dataStream.Close();
            }

            return request;
        }


        private string httpResponse(HttpWebRequest request)
        {
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream dataStream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(dataStream, Encoding.UTF8))
            {
                string pageContent = reader.ReadToEnd();
                reader.Close();

                dataStream.Close();

                response.Close();
                return pageContent;
            }
        }

        #endregion


        private void ThrowError(string message, Exception error = null)
        {
            if (ErrorHandler != null) ErrorHandler(message, error);
        }


        private class Credentials
        {
            public string SID { get; set; }
            public string LSID { get; set; }
            public string Auth { get; set; }
            public CookieContainer cookieJar { get; set; }
            public string xt { get; set; }
            public DateTime Expires { get; set; }
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
            loadalltracks,
            loadplaylist,
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
