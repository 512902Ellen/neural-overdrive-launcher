using System.Text.Json;

namespace CodexBootAnimation;

internal enum BridgeEvent { Rejected, Ready, Started, Completed, SkipRequested, Error }

internal sealed class BridgeSession
{
    public const string Entry = "https://theme.invalid/boot.html";
    private BridgeEvent state;
    private readonly string id = Guid.NewGuid().ToString("N");

    public string StartMessage(LaunchOptions options, bool reducedMotion) => JsonSerializer.Serialize(new
    {
        v = 1,
        type = "start",
        id,
        mode = options.Mode == PlaybackMode.Full ? "full" : "quick",
        reducedMotion,
        respectSystemReducedMotion = options.RespectReducedMotion
    });

    public BridgeEvent Receive(string source, string json)
    {
        if (source != Entry || json.Length > 256)
            return BridgeEvent.Rejected;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("v", out var version) || version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out var value) || value != 1 ||
                !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                return BridgeEvent.Rejected;

            int fields = root.EnumerateObject().Count();
            string? name = type.GetString();
            if (state == BridgeEvent.Rejected && name == "ready" && fields == 2)
                return state = BridgeEvent.Ready;

            if (fields != 3 || !root.TryGetProperty("id", out var token) ||
                token.ValueKind != JsonValueKind.String || token.GetString() != id)
                return BridgeEvent.Rejected;

            var next = (state, name) switch
            {
                (BridgeEvent.Ready, "started") => BridgeEvent.Started,
                (BridgeEvent.Started, "completed") => BridgeEvent.Completed,
                (BridgeEvent.Ready or BridgeEvent.Started, "skipRequested") => BridgeEvent.SkipRequested,
                (BridgeEvent.Ready or BridgeEvent.Started, "error") => BridgeEvent.Error,
                _ => BridgeEvent.Rejected
            };
            if (next != BridgeEvent.Rejected)
                state = next;
            return next;
        }
        catch (JsonException)
        {
            return BridgeEvent.Rejected;
        }
    }
}
