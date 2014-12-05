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
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace GoogleMusic
{

    #region Track

    [DataContract]
    public class Track : IComparable<Track>
    {
        private static readonly Regex _regex = new Regex(@"^(?<ARTICLE>[T|t]he)\s+(?<BODY>.+)", RegexOptions.Compiled);

        private string _id;
        private string _titleNorm;
        private string _artistNorm;
        private string _albumNorm;
        private string _albumArtistNorm;

        public Track()
        {
            albumArtRef = new List<Url>();
            artistArtRef = new List<Url>();
            artistId = new List<string>();
        }

        [DataMember]
        public string id { get { _id = String.IsNullOrEmpty(_id) ? storeId : _id; return _id; } set { _id = value; } }
        [DataMember]
    	public string clientId { get; set; }
        [DataMember(Name = "creationTimestamp")]
        private long _creationTimestamp { get; set; }
        [DataMember(Name = "lastModifiedTimestamp")]
        private long _lastModifiedTimestamp { get; set; }
        [DataMember(Name = "recentTimestamp")]
        private long _recentTimestamp { get; set; }
        [DataMember]
        public bool deleted { get; set; }
        [DataMember]
        public string title { get; set; }
        [DataMember]
        public string artist { get; set; }
        [DataMember]
        public string composer { get; set; }
        [DataMember]
        public string album { get; set; }
        [DataMember]
        public string albumArtist { get; set; }
        [DataMember]
        public int year { get; set; }
        [DataMember]
        public string comment { get; set; }
        [DataMember(Name = "trackNumber")]
        public int track { get; set; }
        [DataMember]
        public string genre { get; set; }
        [DataMember]
        public int durationMillis { get; set; }
        [DataMember]
        public int beatsPerMinute { get; set; }
        [DataMember]
        public List<Url> albumArtRef { get; set; }
        [DataMember]
        public List<Url> artistArtRef { get; set; }
        [DataMember]
        public int playCount { get; set; }
        [DataMember(Name = "totalTrackCount")]
        public int totalTracks { get; set; }
        [DataMember(Name = "discNumber")]
        public int disc { get; set; }
        [DataMember(Name = "totalDiscCount")]
        public int totalDiscs { get; set; }
        [DataMember]
        public int rating { get; set; }
        [DataMember]
    	public int estimatedSize { get; set; }
        [DataMember]
        public string storeId { get; set; }
        [DataMember]
        public string albumId { get; set; }
        [DataMember]
        public List<string> artistId { get; set; }
        [DataMember]
	    public string nid { get; set; }

        public int bitrate { get; set; }
        public bool explicitType { get; set; }
        public bool subjectToCuration { get; set; }
        public int type { get; set; }

        public string titleNorm { get { _titleNorm = String.IsNullOrEmpty(_titleNorm) ? title.ToLower() : _titleNorm; return _titleNorm; } set { _titleNorm = value; } }
        public string artistNorm { get { _artistNorm = String.IsNullOrEmpty(_artistNorm) ? RearrangeArticle(artistUnified.ToLower()) : _artistNorm; return _artistNorm; } set { _artistNorm = RearrangeArticle(value); } }
        public string albumNorm { get { _albumNorm = String.IsNullOrEmpty(_albumNorm) ? album.ToLower() : _albumNorm; return _albumNorm; } set { _albumNorm = value; } }
        public string albumArtistNorm { get { _albumArtistNorm = String.IsNullOrEmpty(_albumArtistNorm) ? RearrangeArticle(albumArtistUnified.ToLower()) : _albumArtistNorm; return _albumArtistNorm; } set { _albumArtistNorm = RearrangeArticle(value); } }
        public DateTime creationTimestamp { get { return ((1e-6 * _creationTimestamp)).FromUnixTime().ToLocalTime(); } }
        public DateTime lastModifiedTimestamp { get { return ((1e-6 * _lastModifiedTimestamp)).FromUnixTime().ToLocalTime(); } }
        public DateTime recentTimestamp { get { return ((1e-6 * _recentTimestamp)).FromUnixTime().ToLocalTime(); } }
        public string albumArtistUnified { get { return String.IsNullOrEmpty(albumArtist) ? artist : albumArtist; } }
        public string artistUnified { get { return String.IsNullOrEmpty(artist) ? albumArtist : artist; } }

        private string _albumArtUrl { set { albumArtRef = new List<Url>(); if (value != null) albumArtRef.Add(new Url { url = value.StartsWith("http:") ? value : "http:" + value }); } }
        private string _artistArtUrl { set { artistArtRef = new List<Url>(); if (value != null) artistArtRef.Add(new Url { url = value.StartsWith("http:") ? value : "http:" + value }); } }
        private string _artistId { set { artistId = new List<string>(); if (value != null) artistId.Add(value); } }
        
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
            int result = StringCompare(titleNorm, other.titleNorm);
            if (result == 0)
            {
                result = StringCompare(artistNorm, other.artistNorm);
                if (result == 0) result = StringCompare(albumNorm, other.albumNorm);
            }

            return result;
        }

        public static Comparison<Track> CompareByArtist = delegate(Track t1, Track t2)
        {
            int result = StringCompare(t1.artistNorm, t2.artistNorm);
            if (result == 0)
            {
                result = StringCompare(t1.titleNorm, t2.titleNorm);
                if (result == 0) result = StringCompare(t1.albumNorm, t2.albumNorm);
            }

            return result;
        };

        public static Comparison<Track> CompareByAlbumArtist = delegate(Track t1, Track t2)
        {
            int result = StringCompare(t1.albumArtistNorm, t2.albumArtistNorm);
            if (result == 0)
            {
                result = StringCompare(t1.titleNorm, t2.titleNorm);
                if (result == 0) result = StringCompare(t1.albumNorm, t2.albumNorm);
            }

            return result;
        };

        public static Comparison<Track> CompareByAlbum = delegate(Track t1, Track t2)
        {
            int result = StringCompare(t1.albumNorm, t2.albumNorm);

            if (result == 0)
            {
                result = StringCompare(t1.albumArtistNorm, t2.albumArtistNorm);
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
            if (s == null) return s;

            Match match = _regex.Match(s);
            if (match.Success)
                return match.Groups["BODY"].Value + ", " + match.Groups["ARTICLE"].Value;
            else
                return s;
        }
    }


    public class Tracklist : List<Track>
    {
        public Tracklist() : base()
        {
            lastUpdatedTimestamp = new DateTime();
        }

        public Tracklist(IEnumerable<Track> tracks) : this()
        {
            this.AddRange(tracks);
        }

        public Track this[string id]
        {
            get { return this.Find(t => t.id == id); }
        }

        public DateTime lastUpdatedTimestamp { get; set; }

        public void SortByArtist() { this.Sort(Track.CompareByArtist); }
        public void SortByAlbumArtist() { this.Sort(Track.CompareByAlbumArtist); }
        public void SortByAlbum() { this.Sort(Track.CompareByAlbum); }
    }

    #endregion


    #region Playlist

    [DataContract]
    public class Playlist
    {
        public Playlist()
        {
            tracks = new Tracklist();
        }

        [DataMember]
        public string id { get; set; }
        [DataMember(Name = "creationTimestamp")]
        private long _creationTimestamp { get; set; }
        [DataMember(Name = "lastModifiedTimestamp")]
        private long _lastModifiedTimestamp { get; set; }
        [DataMember(Name = "recentTimestamp")]
        private long _recentTimestamp { get; set; }
        [DataMember]
        public bool deleted { get; set; }
        [DataMember]
        public string name { get; set; }
        [DataMember]
        public string type { get; set; }
        [DataMember]
        public string shareToken { get; set; }
        [DataMember]
        public string ownerName { get; set; }
        [DataMember]
        public string ownerProfilePhotoUrl { get; set; }
        [DataMember]
        public bool accessControlled { get; set; }

        public Tracklist tracks { get; set; }

        public DateTime creationTimestamp { get { return ((1e-6 * _creationTimestamp)).FromUnixTime().ToLocalTime(); } }
        public DateTime lastModifiedTimestamp { get { return ((1e-6 * _lastModifiedTimestamp)).FromUnixTime().ToLocalTime(); } }
        public DateTime recentTimestamp { get { return ((1e-6 * _recentTimestamp)).FromUnixTime().ToLocalTime(); } }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public override string ToString()
        {
            return name;
        }
    }


    public class Playlists : List<Playlist>
    {
        public Playlists() : base()
        { }

        public Playlists(IEnumerable<Playlist> playlists) : this()
        {
            this.AddRange(playlists);
        }

        public Playlist this[string id]
        {
            get { return this.Find(p => p.id == id); }
        }

        public DateTime lastUpdatedTimestamp { get; set; }
    }

    #endregion


    #region Album

    public class Album
    {
        private string _album;
        private string _albumArtist;
        private string _albumArtistSort;

        public Album()
        {
            tracks = new Tracklist();
        }

        public string album
        {
            get { _album = _album == null && tracks.Count > 0 ? tracks[0].album : _album; return _album; }
            set { _album = value; }
        }
        public string albumArtist
        {
            get { _albumArtist = _albumArtist == null && tracks.Count > 0 ? tracks[0].albumArtistUnified : _albumArtist; return _albumArtist; }
            set { _albumArtist = value; }
        }
        public string albumArtistSort
        {
            get { _albumArtistSort = _albumArtistSort == null && tracks.Count > 0 ? tracks[0].albumArtistNorm : _albumArtistSort; return _albumArtistSort; }
            set { _albumArtistSort = value; }
        }
        public Tracklist tracks { get; set; }

        public override string ToString()
        {
            return album;
        }
    }


    public class Albumlist : List<Album>
    {
        public Albumlist() : base()
        { }

        public Albumlist(IEnumerable<Track> tracks) : this()
        {
            List<Album> albums = tracks.OrderBy(track => track, new Comparer<Track>(Track.CompareByAlbum))
                                       .GroupBy(track => new { track.album, track.albumArtistNorm })
                                       .Select(groupedTracks => new Album { album = groupedTracks.Key.album, albumArtistSort = groupedTracks.Key.albumArtistNorm, tracks = new Tracklist(groupedTracks.ToList()) }).ToList();
            this.AddRange(albums);
        }

        public Albumlist(IEnumerable<Album> albums) : this()
        {
            this.AddRange(albums);
        }

    }

    #endregion


    #region AlbumArtist

    public class AlbumArtist
    {
        private string _albumArtist;
        private string _albumArtistSort;

        public AlbumArtist()
        {
            tracks = new Tracklist();
        }

        public string albumArtist
        {
            get { _albumArtist = _albumArtist == null && tracks.Count > 0 ? tracks[0].albumArtistUnified : _albumArtist; return _albumArtist; }
            set { _albumArtist = value; }
        }
        public string albumArtistSort
        {
            get { _albumArtistSort = _albumArtistSort == null && tracks.Count > 0 ? tracks[0].albumArtistNorm : _albumArtistSort; return _albumArtistSort; }
            set { _albumArtistSort = value; }
        }
        public Tracklist tracks { get; set; }

        public override string ToString()
        {
            return albumArtist;
        }
    }


    public class AlbumArtistlist : List<AlbumArtist>
    {
        public AlbumArtistlist() : base()
        { }

        public AlbumArtistlist(IEnumerable<Track> tracks) : this()
        {
            List<AlbumArtist> albumArtists = tracks.OrderBy(track => track, new Comparer<Track>(Track.CompareByAlbumArtist))
                                                   .GroupBy(track => track.albumArtistNorm)
                                                   .Select(groupedTracks => new AlbumArtist { albumArtistSort = groupedTracks.Key, tracks = new Tracklist(groupedTracks.ToList()) }).ToList();
            this.AddRange(albumArtists);
        }

        public AlbumArtistlist(IEnumerable<AlbumArtist> albumArtists) : this()
        {
            this.AddRange(albumArtists);
        }
    }

    #endregion

    [DataContract]
    public class Url
    {
        [DataMember]
        public string url { get; set; }
    }


    [DataContract]
    public class StreamUrl : Url
    {
        public DateTime expires { get; set; }
    }


    [DataContract]
    public class Status
    {
        [DataMember]
        public int availableTracks { get; set; }
        [DataMember]
        public List<UploadStatus> uploadStatus { get; set; }

        [DataContract]
        public class UploadStatus
        {
            [DataMember]
            public int clientTotalSongCount { get; set; }
            [DataMember]
            public int currentTotalUploadedCount { get; set; }
            [DataMember]
            public string currentUploadingTrack { get; set; }
            [DataMember]
            public string clientName { get; set; }
        }
    }


    [DataContract]
    public class Settings
    {
        [DataMember]
        public bool isCanceled { get; set; }
        [DataMember(Name = "expirationMillis")]
        private long _expirationMillis { get; set; }
        [DataMember]
        public bool isTrial { get; set; }
        [DataMember]
        public bool subscriptionNewsletter { get; set; }
        [DataMember]
        public List<Device> devices { get; set; }
        [DataMember]
        public bool isSubscription { get; set; }
        [DataMember]
        public int maxTracks { get; set; }

        public DateTime expiration { get { return ((1e-3 * _expirationMillis)).FromUnixTime().ToLocalTime(); } }

        [DataContract]
        public class Device
        {
            [DataMember]
            public string id { get; set; }
            [DataMember]
            public string model { get; set; }
            [DataMember]
            public string manufacturer { get; set; }
            [DataMember]
            public string name { get; set; }
            [DataMember]
            public string carrier { get; set; }
            [DataMember]
            public string type { get; set; }
            [DataMember(Name = "date")]
            private long _date { get; set; }
            [DataMember(Name = "lastUsedMs")]
            private long _lastUsedMs { get; set; }

            public DateTime date { get { return ((1e-3 * _date)).FromUnixTime().ToLocalTime(); } }
            public DateTime lastUsed { get { return ((1e-3 * _lastUsedMs)).FromUnixTime().ToLocalTime(); } }
        }
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