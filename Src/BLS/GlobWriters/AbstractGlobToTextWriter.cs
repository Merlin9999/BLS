namespace BLS.GlobWriters;

public abstract class AbstractGlobToTextWriter(TextWriter outputWriter) : AbstractGlobWriter(outputWriter)
{
    private const char DefaultPadChar = '.';

    protected static string GetAttribs(FileAttributes fileAttribs)
    {
        char directory = (fileAttribs & FileAttributes.Directory) == 0 ? '-' : 'd';
        char archive = (fileAttribs & FileAttributes.Archive) == 0 ? '-' : 'a';
        char readOnly = (fileAttribs & FileAttributes.ReadOnly) == 0 ? '-' : 'r';
        char hidden = (fileAttribs & FileAttributes.Hidden) == 0 ? '-' : 'h';
        char system = (fileAttribs & FileAttributes.System) == 0 ? '-' : 's';
        char temporary = (fileAttribs & FileAttributes.Temporary) == 0 ? '-' : 't';
        char offline = (fileAttribs & FileAttributes.Offline) == 0 ? '-' : 'o';
        return $"{directory}{archive}{readOnly}{hidden}{system}{temporary}{offline}";
    }

    protected static string FormatDateTime(DateTime dateTime)
    {
        string time = $"{dateTime:h:mm:ss tt}";
        if (time.Length < 11)
            time = " " + time;
        string date = $"{dateTime:yyyy-MM-dd}";
        return $"{date} {time}";
    }

    protected static string PadLeft(string value, int maxLength)
    {
        if (value.Length + 1 <= maxLength)
            return (' ' + value).PadLeft(maxLength, DefaultPadChar);
        return value;
    }

    protected static string PadRight(string value, int maxLength)
    {
        if (value.Length + 1 <= maxLength)
            return (value + ' ').PadRight(maxLength, DefaultPadChar);
        return value;
    }

    protected static string PadRightAndTruncate(string value, int maxLength)
    {
        string newValue = PadRight(value, maxLength);
        if (newValue.Length > maxLength)
            newValue = newValue.Substring(0, maxLength);
        return newValue;
    }
}