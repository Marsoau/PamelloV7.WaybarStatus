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
            Client.User, Player, Song
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
        
        Player = await Client.RequiredUser.SelectedPlayer.LoadAsync();
        Song = Player is null ? null : await Player.Queue.CurrentSong.LoadAsync();
        
        var sb = new StringBuilder();

        if (Song is not null) {
            var songString = Song.ToString();
            
            sb.Append(songString.Substring(0, songString.Length < 40 ? songString.Length : 40));
            if (songString.Length > 40) sb.Append("...");
            
            if (Player is not null) {
                sb.Insert(0, $"[{Player.Queue.Position + 1}/{Player.Queue.Entries.Count()}] | ");
                sb.Append(" | ").Append(new AudioTime(Player.Queue.CurrentSongTimePassed).ToShortString());
                sb.Append(" / ").Append(new AudioTime(Player.Queue.CurrentSongTimeTotal).ToShortString());
                if (Player.IsPaused) sb.Append(" (Paused)");
            }
        }
        else {
            sb.Append(Player?.ToString() ?? "No player");
        }

        Console.WriteLine(sb.ToString());
    }
}
