namespace ForkForward.Mediator.Examples;

public class Sing : IStreamRequest<Song>
{
    public string Message { get; set; }
}