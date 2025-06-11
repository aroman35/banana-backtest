using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Banana.Backtest.Common.Models;

[StructLayout(LayoutKind.Sequential)]
public partial struct Version
{
    public int Major;
    public int Minor;
    public int Build;

    public static Version Create(int major)
    {
        return new Version
        {
            Major = major,
        };
    }

    public static Version Create(int major, int minor)
    {
        return new Version
        {
            Major = major,
            Minor = minor,
        };
    }

    public static Version Create(int major, int minor, int build)
    {
        return new Version
        {
            Major = major,
            Minor = minor,
            Build = build
        };
    }

    public static Version Parse(ReadOnlySpan<byte> versionRaw)
    {
        var dot = (byte)'.';
        var nextDotIndex = versionRaw.IndexOf(dot);
        if (nextDotIndex == -1)
            return Create(BitConverter.ToInt32(versionRaw));
        var major = BitConverter.ToInt32(versionRaw[..nextDotIndex]);
        var remained = versionRaw[(nextDotIndex + 1)..];
        nextDotIndex = remained.IndexOf(dot);

        if (nextDotIndex == -1)
        {
            return Create(major, BitConverter.ToInt32(remained));
        }

        var minor = BitConverter.ToInt32(remained[..nextDotIndex]);
        remained = remained[(nextDotIndex + 1)..];

        nextDotIndex = remained.IndexOf(dot);
        if (nextDotIndex != -1)
            throw new InvalidOperationException("Version format is invalid");
        var build = BitConverter.ToInt32(remained);
        return Create(major, minor, build);
    }

    public static Version Parse(string value)
    {
        var regex = VersionRegexPattern().Match(value);
        if (!regex.Success)
            throw new FormatException("Version format is invalid");
        var major = int.Parse(regex.Groups["major"].Value);
        var minor = int.Parse(regex.Groups["minor"].Value);
        var build = int.Parse(regex.Groups["build"].Value);
        return Create(major, minor, build);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCompatible(Version other)
    {
        return other.Major == Major && other.Minor == Minor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Version(int value)
    {
        return new Version { Major = value, Minor = 0, Build = 0 };
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Build}";
    }

    [GeneratedRegex(@"^(?<major>\d*).(?<minor>\d*).(?<build>\d*)$", RegexOptions.Compiled)]
    private static partial Regex VersionRegexPattern();
}
