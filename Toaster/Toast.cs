namespace Blazr.Demo.Toaster;

public enum MessageColour
{
    Primary, Secondary, Dark, Light, Success, Danger, Warning, Info
}

public record Toast
{
    public Guid Id = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public MessageColour MessageColour { get; init; } = MessageColour.Primary;
    public DateTimeOffset Posted = DateTimeOffset.Now;
    public DateTimeOffset TTD { get; init; } = DateTimeOffset.Now.AddSeconds(30);
    public bool IsBurnt => TTD < DateTimeOffset.Now;
    private TimeSpan elapsedTime => Posted - DateTimeOffset.Now;

    public string ElapsedTimeText =>
        elapsedTime.Seconds > 60
        ? $"posted {-elapsedTime.Minutes} mins ago"
        : $"posted {-elapsedTime.Seconds} secs ago";


    public static Toast NewToast(string title, string message, MessageColour messageColour, int secsToLive)
        => new Toast
        {
            Title = title,
            Message = message,
            MessageColour = messageColour,
            TTD = DateTimeOffset.Now.AddSeconds(secsToLive)
        };
}