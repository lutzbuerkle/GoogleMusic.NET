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
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace GoogleMusic
{
    public class GoogleMusicClient
    {
        protected Credentials _credentials;
        protected string _useragent;

        public delegate void ErrorHandlerDelegate(string message, Exception error);

        public WebProxy Proxy { get; set; }
        public ErrorHandlerDelegate ErrorHandler { get; set; }


        public GoogleMusicClient()
        {
            Proxy = null;
            ErrorHandler = null;
        }


        #region Login

        protected bool ClientLogin(string login, string passwd)
        {
            string response;
            Dictionary<string, string> data = new Dictionary<string, string>();

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


        protected bool GetAuthenticationCookies()
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
                return false;
            }
            
            _credentials.cookieJar = cookieJar;

            CookieCollection cookies = cookieJar.GetCookies(new Uri("https://play.google.com/music/listen"));
            _credentials.xt = cookies["xt"].Value;
            _credentials.Expires = cookies["sjsaid"].Expires;

            return true;
        }

        #endregion


        #region HttpRequests

        protected HttpWebRequest httpGetRequest(string url)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "GET";
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.KeepAlive = true;
            request.Proxy = Proxy;
            request.UserAgent = _useragent;

            return request;
        }


        protected HttpWebRequest httpPostRequest(string url, string postData, string contentType, Dictionary<string, string> header = null)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "POST";
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.KeepAlive = true;
            request.Proxy = Proxy;
            request.UserAgent = _useragent;

            byte[] data = new byte[] { };

            if (header != null)
            {
                foreach (string key in header.Keys)
                    request.Headers[key] = header[key];
            }

            if (!String.IsNullOrEmpty(postData)) data = Encoding.ASCII.GetBytes(postData);

            if (data.Length > 0)
            {
                request.ContentType = contentType;
                request.ContentLength = data.Length;
            }

            using (Stream dataStream = request.GetRequestStream())
            {
                dataStream.Write(data, 0, data.Length);
                dataStream.Close();
            }

            return request;
        }


        protected HttpWebRequest httpPostRequest(string url, Dictionary<string, string> postParameters, Dictionary<string, string> header = null)
        {
            string postData = "";

            foreach (string key in postParameters.Keys)
            {
                postData += HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(postParameters[key]) + "&";
            }
            postData = postData.TrimEnd('&');

            return httpPostRequest(url, postData, "application/x-www-form-urlencoded", header);
        }


        protected string httpResponse(HttpWebRequest request)
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


        internal void ThrowError(string message, Exception error = null)
        {
            if (ErrorHandler != null) ErrorHandler(message, error);
        }


        protected class Credentials
        {
            public string SID { get; set; }
            public string LSID { get; set; }
            public string Auth { get; set; }
            public CookieContainer cookieJar { get; set; }
            public string xt { get; set; }
            public DateTime Expires { get; set; }
        }

    }

}
