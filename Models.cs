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
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace GoogleMusic
{

    #region Track

    [DataContract]
    public class Track : IComparable<Track>
    {
        private static Regex _regex = new Regex(@"^(?<ARTICLE>[T|t]he)\s+(?<BODY>.+)", RegexOptions.Compiled);

        private string _artistSort;
        private string _albumArtistSort;
        private string _artistNorm;
        private string _albumArtistNorm;
        private string _albumArtUrl;

        [DataMember(Name = "genre")]
        public string genre { get; set; }
        [DataMember(Name = "beatsPerMinute")]
        public int beatsPerMinute { get; set; }
        [DataMember(Name = "albumArtistNorm")]
        public string albumArtistNorm { get { return _albumArtistNorm; } set { _albumArtistNorm = value; albumArtistSort = _albumArtistNorm; } }
        [DataMember(Name = "artistNorm")]
        public string artistNorm { get { return _artistNorm; } set { _artistNorm = value; artistSort = _artistNorm; } }
        [DataMember(Name = "album")]
        public string album { get; set; }
        [DataMember(Name = "lastPlayed")]
        internal long lastPlayedInternal { get; set; }
		[DataMember(Name = "artistImageBaseUrl")]
        public string artistImageBaseUrl { get; set; }
        [DataMember(Name = "type")]
        public int type { get; set; }
        [DataMember(Name = "recentTimestamp")]
        internal long recentTimestampInternal { get; set; }
        [DataMember(Name = "disc")]
        public int disc { get; set; }
        [DataMember(Name = "id")]
        public string id { get; set; }
        [DataMember(Name = "composer")]
        public string composer { get; set; }
        [DataMember(Name = "title")]
        public string title { get; set; }
        [DataMember(Name = "albumArtist")]
        public string albumArtist { get; set; }
        [DataMember(Name = "artistMatchedId")]
        public string artistMatchedId { get; set; }
        [DataMember(Name = "totalTracks")]
        public int totalTracks { get; set; }
        [DataMember(Name = "subjectToCuration")]
        public bool subjectToCuration { get; set; }
        [DataMember(Name = "name")]
        public string name { get; set; }
        [DataMember(Name = "totalDiscs")]
        public int totalDiscs { get; set; }
        [DataMember(Name = "year")]
        public int year { get; set; }
        [DataMember(Name = "titleNorm")]
        public string titleSort { get; set; }
        [DataMember(Name = "artist")]
        public string artist { get; set; }
        [DataMember(Name = "albumNorm")]
        public string albumSort { get; set; }
        [DataMember(Name = "track")]
        public int track { get; set; }
        [DataMember(Name = "durationMillis")]
        public int durationMillis { get; set; }
        [DataMember(Name = "albumArtUrl")]
        public string albumArtUrl { get { return (_albumArtUrl != null && !_albumArtUrl.StartsWith("http:")) ? "http:" + _albumArtUrl : _albumArtUrl; } set { _albumArtUrl = value; } }
        [DataMember(Name = "deleted")]
        public bool deleted { get; set; }
        [DataMember(Name = "url")]
        public string url { get; set; }
        [DataMember(Name = "curatedByUser")]
        public bool curatedByUser { get; set; }
        [DataMember(Name = "previewToken")]
        public string previewToken { get; set; }
        [DataMember(Name = "creationDate")]
        internal long creationDateInternal { get; set; }
        [DataMember(Name = "playCount")]
        public int playCount { get; set; }
        [DataMember(Name = "playlistEntryId")]
        public string playlistEntryId { get; set; }
        [DataMember(Name = "curationSuggested")]
        public bool curationSuggested { get; set; }
        [DataMember(Name = "bitrate")]
        public int bitrate { get; set; }
        [DataMember(Name = "rating")]
        public int rating { get; set; }
        [DataMember(Name = "comment")]
        public string comment { get; set; }
        [DataMember(Name = "storeId")]
        public string storeId { get; set; }
        [DataMember(Name = "explicitType")]
        public int explicitType { get; set; }
        [DataMember(Name = "matchedId")]
        public string matchedId { get; set; }
        [DataMember(Name = "albumMatchedId")]
        public string albumMatchedId { get; set; }
        [DataMember(Name = "albumPlaybackTimestamp")]
        public long albumPlaybackTimestamp { get; set; }
        [DataMember(Name = "lastPlaybackTimestamp")]
        public long lastPlaybackTimestamp { get; set; }

        public DateTime lastPlayed { get { return ((long)(1e-6 * lastPlayedInternal)).FromUnixTime().ToLocalTime(); } }

        public DateTime recentTimestamp { get { return ((long)(1e-6 * recentTimestampInternal)).FromUnixTime().ToLocalTime(); } }

        public DateTime creationDate { get { return ((long)(1e-6 * creationDateInternal)).FromUnixTime().ToLocalTime(); } }

        public string albumArtistUnified { get { return String.IsNullOrEmpty(albumArtist) ? artist : albumArtist; } }

        public string artistUnified { get { return String.IsNullOrEmpty(artist) ? albumArtist : artist; } }

        public string albumArtistSort
        {
            get { return _albumArtistSort; }
            set
            {
                if ((value == "") && (!String.IsNullOrEmpty(_artistSort)))
                {
                    _albumArtistSort = _artistSort;
                }
                else
                {
                    _albumArtistSort = RearrangeArticle(value);
                    if (_artistSort == "") _artistSort = _albumArtistSort;
                }
            }
        }

        public string artistSort
        {
            get { return _artistSort; }
            set
            {
                if ((value == "") && (!String.IsNullOrEmpty(_albumArtistSort)))
                {
                    _artistSort = _albumArtistSort;
                }
                else
                {
                    _artistSort = RearrangeArticle(value);
                    if (_albumArtistSort == "") _albumArtistSort = _artistSort;
                }
            }
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public override string ToString()
        {
            return title;
        }

        public int CompareTo(Track other)
        {
            int result = StringCompare(titleSort, other.titleSort);
            if (result == 0)
            {
                result = StringCompare(artistSort, other.artistSort);
                if (result == 0) result = StringCompare(albumSort, other.albumSort);
            }

            return result;
        }

        public static Comparison<Track> CompareByArtist = delegate(Track t1, Track t2)
        {
            int result = StringCompare(t1.artistSort, t2.artistSort);
            if (result == 0)
            {
                result = StringCompare(t1.titleSort, t2.titleSort);
                if (result == 0) result = StringCompare(t1.albumSort, t2.albumSort);
            }

            return result;
        };

        public static Comparison<Track> CompareByAlbumArtist = delegate(Track t1, Track t2)
        {
            int result = StringCompare(t1.albumArtistSort, t2.albumArtistSort);
            if (result == 0)
            {
                result = StringCompare(t1.titleSort, t2.titleSort);
                if (result == 0) result = StringCompare(t1.albumSort, t2.albumSort);
            }

            return result;
        };

        public static Comparison<Track> CompareByAlbum = delegate(Track t1, Track t2)
        {
            int result = StringCompare(t1.albumSort, t2.albumSort);

            if (result == 0)
            {
                result = StringCompare(t1.albumArtistSort, t2.albumArtistSort);
                if (result == 0) result = (1000 * t1.disc + t1.track).CompareTo(1000 * t2.disc + t2.track);
            }

            return result;
        };

        private static int StringCompare(string s1, string s2)
        {
            return String.Compare(s1, s2, CultureInfo.CurrentCulture, CompareOptions.IgnoreSymbols);
        }

        private static string RearrangeArticle(string s)
        {
            Match match = _regex.Match(s);
            if (match.Success)
                return match.Groups["BODY"].Value + ", " + match.Groups["ARTICLE"].Value;
            else
                return s;
        }
    }

    #endregion


    #region Playlist

    [DataContract]
    public class Playlist
    {
        public Playlist()
        {
            tracks = new List<Track>();
        }

        public void Sort() { tracks.Sort(); }
        public void SortByArtist() { tracks.Sort(Track.CompareByArtist); }
        public void SortByAlbumArtist() { tracks.Sort(Track.CompareByAlbumArtist); }
        public void SortByAlbum() { tracks.Sort(Track.CompareByAlbum); }

        [DataMember(Name = "playlistId")]
        public string playlistId { get; set; }
        [DataMember(Name = "title")]
        public string title { get; set; }
        [DataMember(Name = "requestTime")]
        internal long requestTimeInternal { get; set; }
        [DataMember(Name = "token")]
    	public string token { get; set; }
        [DataMember(Name = "continuationToken")]
        public string continuationToken { get; set; }
        [DataMember(Name = "differentialUpdate")]
        public bool differentialUpdate { get; set; }
        [DataMember(Name = "playlist")]
        public List<Track> tracks { get; set; }
        [DataMember(Name = "unavailableTrackCount")]
    	public int unavailableTrackCount { get; set; }
        [DataMember(Name = "continuation")]
        public bool continuation { get; set; }

        public DateTime requestTime { get { return ((long)(1e-6 * requestTimeInternal)).FromUnixTime().ToLocalTime(); } }

        public override int GetHashCode()
        {
            return playlistId.GetHashCode();
        }

        public override string ToString()
        {
            return title;
        }
    }


    [DataContract]
    public class Playlists
    {
        [DataMember(Name = "playlists")]
        public List<Playlist> playlists { get; set; }
    }

    #endregion


    #region Album

    public class Album : Playlist
    {
        public string albumArtist { get { return tracks == null ? null : tracks[0].albumArtistUnified; } }
        public string albumArtistSort { get { return tracks == null ? null : tracks[0].albumArtistSort; } }
    }


    public class Albumlist
    {
        public Albumlist()
        {
            albums = new List<Album>();
        }

        public Albumlist(List<Track> tracks) : this()
        {
            albums = tracks.OrderBy(track => track, new Comparer<Track>(Track.CompareByAlbum))
                           .GroupBy(track => new { track.album, track.albumArtistSort })
                           .Select(groupedTracks => new Album { title = groupedTracks.Key.album, tracks = groupedTracks.ToList() }).ToList();
        }

        public Albumlist(Playlist playlist) : this(playlist.tracks) { }

        public List<Album> albums { get; set; }
    }

    #endregion


    #region AlbumArtist

    public class AlbumArtist : Playlist
    {
        public string albumArtist { get { return tracks == null ? null : tracks[0].albumArtistUnified; } }
        public string albumArtistSort { get { return tracks == null ? null : tracks[0].albumArtistSort; } }

        public override string ToString()
        {
            return albumArtist;
        }
    }


    public class AlbumArtistlist
    {
        public AlbumArtistlist()
        {
            albumArtists = new List<AlbumArtist>();
        }

        public AlbumArtistlist(List<Track> tracks) : this()
        {
            albumArtists = tracks.OrderBy(track => track, new Comparer<Track>(Track.CompareByAlbumArtist))
                                 .GroupBy(track => track.albumArtistSort)
                                 .Select(groupedTracks => new AlbumArtist { title = groupedTracks.First().albumArtistUnified, tracks = groupedTracks.ToList() }).ToList();
        }

        public AlbumArtistlist(Playlist playlist) : this(playlist.tracks) { }

        public List<AlbumArtist> albumArtists { get; set; }
    }

    #endregion


    [DataContract]
    public class StreamUrl
    {
        [DataMember(Name = "url")]
        public String url { get; set; }
        public DateTime expires
        {
            get
            {
                Regex regex = new Regex(@"expire=(?<EPOCH>\d+)");
                Match match = regex.Match(url);
                long epoch = 0;
                if (match.Success) epoch = Convert.ToInt64(match.Groups["EPOCH"].Value);
                return epoch.FromUnixTime().ToLocalTime();
            }
        }
    }


    [DataContract]
    public class Status
    {
        [DataMember(Name = "availableTracks")]
        public int availableTracks { get; set; }
        [DataMember(Name = "uploadStatus")]
        public List<UploadStatus> uploadStatus { get; set; }

        [DataContract]
        public class UploadStatus
        {
            [DataMember(Name = "client_total_song_count")]
            public int clientTotalSongCount { get; set; }
            [DataMember(Name = "current_total_uploaded_count")]
            public int currentTotalUploadedCount { get; set; }
            [DataMember(Name = "current_uploading_track")]
            public string currentUploadingTrack { get; set; }
            [DataMember(Name = "client_name")]
            public string clientName { get; set; }
        }
    }


    [DataContract]
    public class Settings
    {
        //[DataMember(Name = "labs")]
        //public List<Lab> labs { get; set; }
        [DataMember(Name = "isCanceled")]
        public bool isCanceled { get; set; }
        [DataMember(Name = "expirationMillis")]
        public long expirationMillis { get; set; }
        [DataMember(Name = "isTrial")]
        public bool isTrial { get; set; }
        [DataMember(Name = "subscriptionNewsletter")]
        public bool subscriptionNewsletter { get; set; }
        //[DataMember(Name = "devices")]
        //public List<Device> devices { get; set; }
        [DataMember(Name = "isSubscription")]
        public bool isSubscription { get; set; }
        [DataMember(Name = "maxTracks")]
        public int maxTracks { get; set; }
    }
    
    
    internal class Comparer<T> : IComparer<T>
    {
        private readonly Comparison<T> _comparison;

        public Comparer(Comparison<T> comparison)
        {
            _comparison = comparison;
        }

        public int Compare(T x, T y)
        {
            return _comparison(x, y);
        }
    }
}