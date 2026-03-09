namespace PamelloV7.WaybarStatus;

public class Program
{
    public static async Task Main(string[] args) {
        for (var i = 0;; i++) {
            Console.WriteLine($"Hello {i}");
            await Task.Delay(1000);
        }
    }
}
