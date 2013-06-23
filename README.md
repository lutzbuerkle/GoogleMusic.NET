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


------------


Copyright 2013, Lutz Bürkle.
Licensed under the 3-clause BSD. See LICENSE.

