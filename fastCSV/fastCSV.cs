using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class fastCSV
{
    public delegate bool ToOBJ<T>(T obj, string[] columns);
    public delegate void FromObj<T>(T obj, List<object> columns);
    private static int _COLCOUNT = 50;

    public static List<T> ReadFile<T>(string filename, bool hasheader, char deliminator, ToOBJ<T> mapper) where T : new()
    {
        string[] cols = null;
        List<T> list = new List<T>();
        int linenum = -1;
        StringBuilder sb = new StringBuilder();
        bool insb = false;
        foreach (var line in File.ReadLines(filename))
        {
            try
            {
                linenum++;
                if (linenum == 0)
                {
                    if (hasheader)
                    {
                        // actual col count
                        int cc = CountOccurence(line, deliminator);
                        if (cc == 0)
                            throw new Exception("File does not have '" + deliminator + "' as a deliminator");
                        cols = new string[cc + 1];
                        continue;
                    }
                    else
                        cols = new string[_COLCOUNT];
                }
                var qc = CountOccurence(line, '\"');
                bool multiline = qc % 2 == 1 || insb;

                string cline = line;
                // if multiline add line to sb and continue
                if (multiline)
                {
                    insb = true;
                    sb.Append(line);
                    var s = sb.ToString();
                    qc = CountOccurence(s, '\"');
                    if (qc % 2 == 1)
                    {
                        sb.AppendLine();
                        continue;
                    }
                    cline = s;
                    sb.Clear();
                    insb = false;
                }

                var c = ParseLine(cline, deliminator, cols);

                T o = new T();
                var b = mapper(o, c);
                if (b)
                    list.Add(o);
            }
            catch (Exception ex)
            {
                throw new Exception("error on line " + linenum, ex);
            }
        }

        return list;
    }

    public static void WriteFile<T>(string filename, string[] headers, char deliminator, List<T> list, FromObj<T> mapper)
    {
        using (FileStream f = new FileStream(filename, FileMode.Create, FileAccess.Write))
        {
            using (StreamWriter s = new StreamWriter(f))
            {
                if (headers != null)
                    s.WriteLine(string.Join(deliminator.ToString(), headers));

                foreach (var o in list)
                {
                    List<object> cols = new List<object>();
                    mapper(o, cols);
                    for (int i = 0; i < cols.Count; i++)
                    {
                        // qoute string if needed -> \" \r \n delim 
                        var str = cols[i].ToString();
                        bool quote = false;

                        if (str.IndexOf('\"') >= 0)
                        {
                            quote = true;
                            str = str.Replace("\"", "\"\"");
                        }

                        if (quote == false && str.IndexOf('\n') >= 0)
                            quote = true;

                        if (quote==false && str.IndexOf('\r') >= 0)
                            quote = true;

                        if (quote == false && str.IndexOf(deliminator) >= 0)
                            quote = true;

                        if (quote)
                            s.Write("\"");
                        s.Write(str);
                        if (quote)
                            s.Write("\"");

                        if (i < cols.Count - 1)
                            s.Write(deliminator);
                    }
                    s.WriteLine();
                }
                s.Flush();
            }
            f.Close();
        }
    }

    public static unsafe int ToInt(string s, int index, int count)
    {
        int num = 0;
        int neg = 1;
        fixed (char* v = s)
        {
            char* str = v;
            str += index;
            if (*str == '-')
            {
                neg = -1;
                str++;
                count--;
            }
            if (*str == '+')
            {
                str++;
                count--;
            }
            while (count > 0)
            {
                num = num * 10
                    //(num << 4) - (num << 2) - (num << 1) 
                    + (*str - '0');
                str++;
                count--;
            }
        }
        return num * neg;
    }

    public static DateTime ToDateTimeISO(string value, bool UseUTCDateTime)
    {
        if (value.Length < 19)
            return DateTime.Parse(value);

        bool utc = false;
        //                   0123456789012345678 9012 9/3
        // datetime format = yyyy-MM-ddTHH:mm:ss .nnn  Z

        int year = ToInt(value, 0, 4);
        int month = ToInt(value, 5, 2);
        int day = ToInt(value, 8, 2);
        int hour = ToInt(value, 11, 2);
        int min = ToInt(value, 14, 2);
        int sec = ToInt(value, 17, 2);
        int ms = 0;
        if (value.Length > 21 && value[19] == '.')
            ms = ToInt(value, 20, 3);

        if (value[value.Length - 1] == 'Z')
            utc = true;

        if (UseUTCDateTime == false && utc == false)
            return new DateTime(year, month, day, hour, min, sec, ms);
        else
            return new DateTime(year, month, day, hour, min, sec, ms, DateTimeKind.Utc).ToLocalTime();
    }

    private unsafe static int CountOccurence(string text, char c)
    {
        int count = 0;
        int len = text.Length;
        int index = -1;
        fixed (char* s = text)
        {
            while (index++ < len)
            {
                char ch = *(s + index);
                if (ch == c)
                    count++;
            }
        }
        return count;
    }

    private unsafe static string[] ParseLine(string line, char deliminator, string[] columns)
    {
        //return line.Split(deliminator);
        int col = 0;
        int linelen = line.Length;
        int index = 0;

        fixed (char* l = line)
        {
            while (index < linelen)
            {
                if (*(l + index) != '\"')
                {
                    // non quoted
                    var next = line.IndexOf(deliminator, index);
                    if (next < 0)
                    {
                        columns[col++] = new string(l, index, linelen - index);
                        break;
                    }
                    columns[col++] = new string(l, index, next - index);
                    index = next + 1;
                }
                else
                {
                    // quoted string change "" -> "
                    int qc = 1;
                    int start = index;
                    char c = *(l + ++index);
                    // find matching quote until delim or EOL
                    while (index++ < linelen)
                    {
                        if (c == '\"')
                            qc++;
                        if (c == deliminator && qc % 2 == 0)
                            break;
                        c = *(l + index);
                    }
                    columns[col++] = new string(l, start + 1, index - start - 3).Replace("\"\"", "\"");
                }
            }
        }

        return columns;
    }
}