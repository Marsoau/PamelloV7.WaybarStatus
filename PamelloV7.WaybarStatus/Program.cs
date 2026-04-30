using System.Security;
using System.Text;
using PamelloV7.Core.Audio;
using PamelloV7.Wrapper;
using PamelloV7.Wrapper.Entities;
using PamelloV7.Wrapper.Extensions;

namespace PamelloV7.WaybarStatus;

public class Program
{
    public static readonly PamelloClient Client;
    
    public static RemotePlayer? Player;
    public static RemoteSong? Song;
    
    public static string Url = null!;
    public static Guid Token;
    
    static Program() {
        Client = new PamelloClient();
    }
    
    public static async Task Main(string[] args) {
        var configPath = args.Length > 0 ? args[0] : "pamellostatus.config";
        
        if (!File.Exists(configPath)) {
            Console.WriteLine("No config file found");
            return;
        }
        
        var parts = (await File.ReadAllTextAsync(configPath)).Split("\n");
        
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])) {
            Console.WriteLine("Url and token not found in config file");
            return;
        }
        if (parts.Length == 1 || string.IsNullOrWhiteSpace(parts[1])) {
            Console.WriteLine("Token not found in config file");
            return;
        }
        
        Url = parts[0];
        Token = Guid.Parse(parts[1]);
        
        Console.WriteLine($"{Url} : {Token}");
        
        await Update();
        Start();
        
        await Task.Delay(-1);
    }

    public static void Start() {
        _ = Client.StartConnectionAttemptsAsync(Url);
        
        Client.OnConnected += (isAutomatic) => {
            _ = Update();
            _ = Client.AuthorizeAsync(Token);
        };
        Client.OnDisconnected += (isAutomatic) => {
            _ = Update();
            _ = Client.StartConnectionAttemptsAsync(Url);
        };
        
        Client.OnAuthorized += (isAutomatic) => _ = Update();
        Client.OnUnauthorized += (isAutomatic) => _ = Update();

        Client.Events.Watch(Update, () => [
            Client.User, Player, Song, ..Song?.Episodes ?? []
        ]);
    }

    public static async Task Update() {
        if (!Client.Signal.IsConnected) {
            Console.WriteLine("Disconnected");
            return;
        }
        if (!Client.Signal.IsAuthorized) {
            Console.WriteLine("Unauthorized");
            return;
        }

        await Client.RequiredUser.FavoriteSongs.LoadAsync();
        Player = await Client.RequiredUser.SelectedPlayer.LoadAsync();
        Song = Player is null ? null : await Player.Queue.CurrentSong.LoadAsync();

        var episodeLoading = Song?.Episodes.LoadAsync();

        var episodes = episodeLoading is not null
            ? (await episodeLoading).ToList()
            : [];

        var currentEpisodePosition = Player?.Queue.CurrentEpisodePosition ?? -1;
        var currentEpisode = episodes.Count > 0
            ? episodes.ElementAtOrDefault(currentEpisodePosition)
            : null;
        var nextEpisode = episodes.Count > 0
            ? episodes.ElementAtOrDefault(currentEpisodePosition + 1)
            : null;

        var currentEpisodeEndTime = nextEpisode?.Start ?? Player?.Queue.CurrentSongTimeTotal ?? -1;
        
        var sb = new StringBuilder();

        if (Song is not null) {
            var originalName = currentEpisode is not null ? currentEpisode.Name : Song.Name;
            var currentName = originalName;
            const int maxLength = 48;
            
            currentName = currentName[..(currentName.Length < maxLength ? currentName.Length : maxLength)];
            if (originalName.Length > maxLength) currentName = $"{currentName[..(maxLength - 3)]}...";
            
            sb.Append($"[{Song.Id}{(
                Client.RequiredUser.FavoriteSongs.Contains(Song) ? "" /* InWhite('F') */ : ""
            )}]{(currentEpisode is not null
                ? $" &lt;{InWhite(currentEpisodePosition + 1)}/{episodes.Count}&gt;" : ""
            )} {InWhite(SecurityElement.Escape(currentName))}");
            
            if (currentName.Length > maxLength) sb.Append("...");
            
            if (Player is not null) {
                sb.Insert(0, $"[{InWhite(Player.Queue.Position + 1)}/{Player.Queue.Entries.Count()}] | ");
                sb.Append(" | ").Append(InWhite(new AudioTime(Player.Queue.CurrentSongTimePassed).ToShortString()));
                sb.Append(" / ");
                
                if (currentEpisode is not null && currentEpisodeEndTime != -1) {
                    sb.Append(new AudioTime(currentEpisodeEndTime).ToShortString());
                    sb.Append($" ({new AudioTime(Player.Queue.CurrentSongTimeTotal).ToShortString()})");
                }
                else {
                    sb.Append(new AudioTime(Player.Queue.CurrentSongTimeTotal).ToShortString());
                }
                
                if (Player.IsPaused) sb.Append(InWhite(" (Paused)"));
            }
        }
        else {
            sb.Append(Player?.ToString() ?? "No player");
        }

        Console.WriteLine(sb.ToString());
    }

    public static string InWhite(object? obj) => obj is null ? "null" : $@"<span color=""#FFFFFFCC"">{obj}</span>";
}
