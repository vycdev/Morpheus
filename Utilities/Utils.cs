namespace Morpheus.Utilities;
public static class Utils
{
    public static readonly Version? AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

    public static string GetAssemblyVersion()
    {
        if (AssemblyVersion is null)
            throw new InvalidOperationException("Assembly version is null.");

        return $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}.{AssemblyVersion.Revision}";
    }
}
