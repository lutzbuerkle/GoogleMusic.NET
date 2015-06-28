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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace GoogleMusic
{

    public class GPSOAuthClient
    {
        private const string _version = "0.0.2";
        private const string _authUrl = "https://android.clients.google.com/auth";
        private const string _gpsKey = "AAAAgMom/1a/v0lblO2Ubrt60J2gcuXSljGFQXgcyZWveWLEwo6prwgi3iJIZdodyhKZQrNWp5nKJ3srRXcUW+F1BD3baEVGcmEgqaLZUNBjm057pKRI16kB0YppeGx5qIQ5QjKzsR8ETQbKLNWgRY0QRNVz34kMJR3P/LgHax/6rmf5AAAAAwEAAQ==";
        private const string _useragent = "GPSOAuth/" + _version;

        private string _email;
        private string _masterToken;
        private RSACryptoServiceProvider _rsa;

        public IWebProxy Proxy { get; set; }


        public GPSOAuthClient()
        {
            _rsa = CreateRSACryptoService(_gpsKey);
            Proxy = WebRequest.GetSystemWebProxy();
        }


        public GPSOAuthClient(string email, string masterToken) : this()
        {
            _email = email;
            _masterToken = masterToken;
        }


        public GPSOAuthClient(Tuple<string, string> masterToken) : this(masterToken.Item1, masterToken.Item2)
        { }


        public Tuple<string, string> MasterToken
        {
            get
            {
                if (_email == null || _masterToken == null)
                    return null;
                else
                    return new Tuple<string, string>(_email, _masterToken);
            }
        }


        public Dictionary<string, string> MasterLogin(string email, string password, string androidId, string service = "ac2dm", string deviceCountry = "us", string operatorCountry = "us", string lang = "en", int sdkVersion = 21)
        {
            var data = new NameValueCollection()
            {
                {"accountType", "HOSTED_OR_GOOGLE"},
                {"Email", email},
                {"has_permission", 1.ToString()},
                {"add_account", 1.ToString()},
                {"EncryptedPasswd", CreateSignature(email, password)},
                {"service", service},
                {"source", "android"},
                {"androidId", androidId},
                {"device_country", deviceCountry},
                {"operatorCountry", deviceCountry},
                {"lang", lang},
                {"sdk_version", sdkVersion.ToString() }
            };
            
            var response = PerformAuthRequest(data);

            if (response.ContainsKey("Token"))
            {
                _email = email;
                _masterToken = response["Token"];
            }
            else
            {
                _email = null;
                _masterToken = null;
            }

            return response;
        }


        public Dictionary<string, string> PerformOAuth(string service, string app, string clientSig, string deviceCountry = "us", string operatorCountry = "us", string lang = "en", int sdkVersion = 17)
        {
            var data = new NameValueCollection()
            {
                {"accountType", "HOSTED_OR_GOOGLE"},
                {"Email", _email ?? String.Empty},
                {"has_permission", 1.ToString()},
                {"EncryptedPasswd", _masterToken ?? String.Empty},
                {"service", service},
                {"source", "android"},
                {"app", app},
                {"client_sig", clientSig},
                {"device_country", deviceCountry},
                {"operatorCountry", deviceCountry},
                {"lang", lang},
                {"sdk_version", sdkVersion.ToString() }
            };

            return PerformAuthRequest(data);
        }


        private RSACryptoServiceProvider CreateRSACryptoService(string key)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();

            byte[] binaryKey = System.Convert.FromBase64String(key);
            byte[] temp = new byte[4];

            Array.Copy(binaryKey, 0, temp, 0, 4);
            uint i = BitConverter.ToUInt32(temp.Reverse().ToArray(), 0);
            byte[] Modulus = new byte[i];
            Array.Copy(binaryKey, 4, Modulus, 0, i);

            Array.Copy(binaryKey, i + 4, temp, 0, 4);
            uint j = BitConverter.ToUInt32(temp.Reverse().ToArray(), 0);
            byte[] Exponent = new byte[j];
            Array.Copy(binaryKey, i + 8, Exponent, 0, j);

            RSAParameters RSAKeyInfo = new RSAParameters();
            RSAKeyInfo.Modulus = Modulus;
            RSAKeyInfo.Exponent = Exponent;

            rsa.ImportParameters(RSAKeyInfo);

            return rsa;
        }


        private string CreateSignature(string email, string password)
        {
            string signature = String.Empty;

            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0);

                byte[] binaryKey = System.Convert.FromBase64String(_gpsKey);

                SHA1 sha1 = SHA1.Create();
                byte[] hash = sha1.ComputeHash(binaryKey);
                ms.Write(hash, 0, 4);

                byte[] temp = Encoding.UTF8.GetBytes(email + "\0" + password);
                byte[] encrypted_login = _rsa.Encrypt(temp, true);

                ms.Write(encrypted_login, 0, encrypted_login.Length);

                signature = System.Convert.ToBase64String(ms.ToArray()).Replace('+', '-').Replace('/', '_');
            }

            return signature;
        }


        private Dictionary<string, string> PerformAuthRequest(NameValueCollection data)
        {
            Dictionary<string,string> response_data = new Dictionary<string,string>();
            string response;

            using (WebClient client = new WebClient())
            {
                client.Proxy = Proxy;
                client.Headers.Set(HttpRequestHeader.UserAgent, _useragent);
                byte[] temp = client.UploadValues(_authUrl, data);

                response = Encoding.UTF8.GetString(temp);
            }

            if (!String.IsNullOrEmpty(response))
            {
                string[] lines = response.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    string[] temp = line.Split(new char[] { '=' }, 2);
                    if (temp.Length == 2)
                        response_data.Add(temp[0], temp[1]);
                }
            }

            return response_data;
        }
    }
}
