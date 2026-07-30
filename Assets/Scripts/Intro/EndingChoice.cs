/// <summary>Player accusation chosen in EndScene.</summary>
public static class EndingChoice
{
    public enum Killer
    {
        None = 0,
        LinFang = 1,
        SuYu = 2
    }

    public static Killer Selected { get; private set; } = Killer.None;

    public static void Set(Killer killer) => Selected = killer;

    public static void Reset() => Selected = Killer.None;
}
