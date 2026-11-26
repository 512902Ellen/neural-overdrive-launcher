namespace CodexBootAnimation;

internal static class AnimationDeadline
{
    public static async Task<AnimationEnd> WaitAsync(Task firstFrame, Task<AnimationEnd> finished,
        TimeSpan startupDeadline, TimeSpan playbackDeadline)
    {
        try
        {
            var first = await Task.WhenAny(firstFrame, finished).WaitAsync(startupDeadline);
            if (first == firstFrame)
                return await finished.WaitAsync(playbackDeadline);
            return await finished;
        }
        catch (TimeoutException)
        {
            return AnimationEnd.TimedOut;
        }
    }
}
