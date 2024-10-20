namespace Morpheus.Utilities;
public static class Utils
{
    public static string GetAssemblyVersion()
    {
        Version? version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        if(version is null)
            throw new InvalidOperationException("Assembly version is null.");

        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
