GoogleMusic.NET
===============

GoogleMusic.NET is an unofficial API for Google Music for the .NET framework. It is written in C#.


```csharp
using GoogleMusic;

...

string login = "user@gmail.com";
string passwd = "my-password";

GoogleMusicClient GMClient = new GoogleMusicClient();

Console.WriteLine("Login...\n");
GMClient.Login(login, passwd);

Console.WriteLine("Reading library...\n");
Playlist library = GMClient.GetAllTracks();

library.SortByAlbumArtist();

foreach (Track track in library.tracks)
    Console.WriteLine("{0} - {1}", track.albumArtistUnified, track.title);

Console.WriteLine("Reading playlists...\n");
Playlists playlists = GMClient.GetPlaylist() as Playlists;

foreach (Playlist playlist in playlists.playlists)
    Console.WriteLine(playlist.ToString());
    
```


GoogleMusic.NET is used by the following project

* [Google Music for Jamcast](https://googlemusicforjamcast.codeplex.com/) adds Google Music browse and playback capabilities to [Jamcast](http://getjamcast.com/), a DLNA media server for Windows.


------------


Copyright 2014, Lutz Bürkle.
Licensed under the 3-clause BSD. See LICENSE.

