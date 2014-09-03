GoogleMusic.NET
===============

GoogleMusic.NET is an unofficial API for Google Music for the .NET framework. It is written in C#. GoogleMusic.NET is partially derived from [gmusicapi](https://github.com/simon-weber/Unofficial-Google-Music-API) by Simon Weber. It is not supported nor endorsed by Google.


```csharp
using GoogleMusic;

...

string login = "user@gmail.com";
string passwd = "my-password";

GoogleMusicWebClient WebClient = new GoogleMusicWebClient();

Console.WriteLine("Login...\n");
WebClient.Login(login, passwd);

Console.WriteLine("Reading library...\n");
Tracklist tracks = WebClient.GetAllTracks();

tracks.SortByAlbumArtist();

foreach (Track track in tracks)
    Console.WriteLine("{0} - {1}", track.albumArtistUnified, track.title);

Console.WriteLine("Reading playlists...\n");
Playlists playlists = WebClient.GetPlaylists();

foreach (Playlist playlist in playlists)
    Console.WriteLine(playlist.ToString());
    
```


GoogleMusic.NET is used by the following project

* [Google Music for Jamcast](https://googlemusicforjamcast.codeplex.com/) adds Google Music browse and playback capabilities to [Jamcast](http://getjamcast.com/), a DLNA media server for Windows.


------------


Copyright 2014, Lutz Bürkle.
Licensed under the 3-clause BSD. See LICENSE.

