using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;

public class fastCSV
{
    public delegate bool ToOBJ<T>(T obj, COLUMNS columns);
    public delegate void FromObj<T>(T obj, List<object> columns);
    private static int _COLCOUNT = 50;

    public struct COLUMNS
    {
        public COLUMNS(MGSpan[] cols)
        {
            _cols = cols;
        }
        MGSpan[] _cols;

        public string this[int idx]
        {
            get
            {
                return _cols[idx].ToString();
            }
        }

        public struct MGSpan
        {
            public MGSpan(char[] line, int start, int count)
            {
                buf = line;
                Start = start;
                Count = count;
            }

            public char[] buf;
            public int Start;
            public int Count;

            public new string ToString()
            {
                if (buf != null)
                    return new string(buf, Start, Count);
                else return "";
            }
        }
    }


    class BufReader
    {
        public BufReader(TextReader tr, int bufsize)
        {
            _tr = tr;
            _bufsize = bufsize;
            _buffer = new char[bufsize];
        }

        TextReader _tr;
        int _bufsize = 0;
        int _bufread = 0;
        int _bufidx = 0;
        char[] _buffer;
        bool EOF = false;
        int reload = 0;

        internal int FillBuffer(int offset)
        {
            var len = _bufsize - offset;
            var read = _tr.ReadBlock(_buffer, offset, len);
            read += offset;
            if (read != _bufsize)
                EOF = true;
            _bufidx = 0;
            return read;
        }

        internal COLUMNS.MGSpan ReadLine()
        {
            if (_bufread == 0 || _bufidx >= _bufread)
            {
                if (EOF)
                    return new COLUMNS.MGSpan();
                _bufread = FillBuffer(0);
                if (_bufread == 0)
                    return new COLUMNS.MGSpan();
            }
            int start = _bufidx;
            int end = _bufidx;
            int qc = 0;
            bool read = false;
            while (_bufidx < _bufread)
            {
                var c = _buffer[_bufidx++];

                if (c == '\"')
                    qc++;
                if ((c == '\r' || c == '\n') && qc % 2 == 0)
                {
                    read = true;
                    end = _bufidx - 1;
                    if (_bufidx >= _bufread)
                    {
                        read = false;
                        break;
                    }
                    c = _buffer[_bufidx++];
                    if (c != '\r' && c != '\n')
                        _bufidx--;
                    break;
                }
            }
            if (EOF && _bufidx == _bufread)
            {
                end = _bufread;
            }

            if (EOF == false && read == false)
            {
                reload++;
                if (reload > 1)
                    throw new Exception("line too long for buffer");
                // copy data to start of buffer
                Array.Copy(_buffer, start, _buffer, 0, _bufsize - start);
                var len = _bufsize - start;
                _bufread = FillBuffer(len);
                if (_bufread == 0)
                    return new COLUMNS.MGSpan();
                return ReadLine();
            }
            reload = 0;
            return new COLUMNS.MGSpan(_buffer, start, end - start);
        }
    }

    public static List<T> ReadFile<T>(string filename, char delimiter, ToOBJ<T> mapper)
    {
        return ReadData(File.OpenText(filename), true, _COLCOUNT, delimiter, mapper);
    }

    public static List<T> ReadFile<T>(string filename, bool hasheader, char delimiter, ToOBJ<T> mapper)
    {
        return ReadData(File.OpenText(filename), hasheader, _COLCOUNT, delimiter, mapper);
    }

    public static List<T> ReadFile<T>(string filename, int colcount, char delimiter, ToOBJ<T> mapper)
    {
        return ReadData(File.OpenText(filename), false, colcount, delimiter, mapper);
    }

    public static List<T> ReadStream<T>(TextReader sr, char delimiter, ToOBJ<T> mapper)
    {
        return ReadData(sr, true, _COLCOUNT, delimiter, mapper);
    }

    public static List<T> ReadStream<T>(TextReader sr, bool hasheader, char delimiter, ToOBJ<T> mapper)
    {
        return ReadData(sr, hasheader, _COLCOUNT, delimiter, mapper);
    }

    public static List<T> ReadStream<T>(TextReader sr, int colcount, char delimiter, ToOBJ<T> mapper)
    {
        return ReadData(sr, false, colcount, delimiter, mapper);
    }

    private static List<T> ReadData<T>(TextReader tr, bool hasheader, int colcount, char delimiter, ToOBJ<T> mapper)
    {
        COLUMNS.MGSpan[] cols;
        List<T> list = new List<T>(10000);

        int linenum = 0;
        CreateObject co = FastCreateInstance<T>();
        var br = new BufReader(tr, 64 * 1024);
        COLUMNS.MGSpan line = new COLUMNS.MGSpan();

        if (hasheader)
        {
            line = br.ReadLine();
            if (line.Count == 0)
                return list;
            // actual col count
            int cc = CountOccurence(line, delimiter);
            if (cc == 0)
                throw new Exception("File does not have '" + delimiter + "' as a delimiter");
            cols = new COLUMNS.MGSpan[cc + 1];
        }
        else
            cols = new COLUMNS.MGSpan[colcount];

        while (true)
        {
            try
            {
                line = br.ReadLine();
                linenum++;
                if (line.Count == 0)
                    break;

                var c = ParseLine(line, delimiter, cols);

                T o = (T)co();
                //new T();
                var b = mapper(o, new COLUMNS(c));
                if (b)
                    list.Add(o);
            }
            catch (Exception ex)
            {
                throw new Exception("error on line " + linenum + "\r\n" + line, ex);
            }
        }

        return list;
    }

    public static void WriteFile<T>(string filename, string[] headers, char delimiter, List<T> list, FromObj<T> mapper)
    {
        using (FileStream f = new FileStream(filename, FileMode.Create, FileAccess.Write))
        {
            using (StreamWriter s = new StreamWriter(f))
            {
                if (headers != null)
                    s.WriteLine(string.Join(delimiter.ToString(), headers));

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

                        if (quote == false && str.IndexOf('\r') >= 0)
                            quote = true;

                        if (quote == false && str.IndexOf(delimiter) >= 0)
                            quote = true;

                        if (quote)
                            s.Write("\"");
                        s.Write(str);
                        if (quote)
                            s.Write("\"");

                        if (i < cols.Count - 1)
                            s.Write(delimiter);
                    }
                    s.WriteLine();
                }
                s.Flush();
            }
            f.Close();
        }
    }

    private delegate object CreateObject();

    private static CreateObject FastCreateInstance<T>()
    {
        CreateObject c = null;
        Type objtype = typeof(T);
        try
        {
            if (objtype.IsClass)
            {
                DynamicMethod dynMethod = new DynamicMethod("_fcic", objtype, null, true);
                ILGenerator ilGen = dynMethod.GetILGenerator();
                ilGen.Emit(OpCodes.Newobj, objtype.GetConstructor(Type.EmptyTypes));
                ilGen.Emit(OpCodes.Ret);
                c = (CreateObject)dynMethod.CreateDelegate(typeof(CreateObject));
            }
        }
        catch (Exception exc)
        {
            throw new Exception(string.Format("Failed to fast create instance for type '{0}' from assembly '{1}'",
                objtype.FullName, objtype.AssemblyQualifiedName), exc);
        }
        return c;
    }

    public static int ToInt(string s)
    {
        return ToInt(s, 0, s.Length);
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

    private unsafe static int CountOccurence(COLUMNS.MGSpan text, char c)
    {
        int count = 0;
        int len = text.Count + text.Start;
        int index = text.Start;
        fixed (char* s = text.buf)
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

    private unsafe static COLUMNS.MGSpan[] ParseLine(COLUMNS.MGSpan line, char delimiter, COLUMNS.MGSpan[] columns)
    {
        //return line.Split(delimiter);
        int col = 0;
        int linelen = line.Count + line.Start;
        int index = line.Start;

        fixed (char* l = line.buf)
        {
            while (index < linelen)
            {
                columns[col] = new COLUMNS.MGSpan();
                if (*(l + index) != '\"')
                {
                    // non quoted
                    var next = -1;
                    for (int i = index; i < linelen; i++)
                    {
                        if (*(l + i) == delimiter)
                        {
                            next = i;
                            break;
                        }
                    }

                    if (next < 0)
                    {
                        columns[col++] = new COLUMNS.MGSpan(line.buf, index, linelen - index);
                        break;
                    }
                    columns[col++] = new COLUMNS.MGSpan(line.buf, index, next - index);
                    index = next + 1;
                }
                else
                {
                    // quoted string change "" -> "
                    int qc = 1;
                    int start = index;
                    int lastNonEscapedEndIndex = index + 2;
                    char c = *(l + ++index);
                    // find matching quote until delim or EOL
                    while (index++ < linelen)
                    {
                        if (c == '\"')
                            qc++;
                        if (c != '\r' && c != '\n' && c != '\0')
                            lastNonEscapedEndIndex = index + 1;
                        if (c == delimiter && qc % 2 == 0)
                            break;
                        c = *(l + index);
                    }

                    var s = new string(line.buf, start + 1, lastNonEscapedEndIndex - start - 3).Replace("\"\"", "\""); // ugly
                    columns[col++] = new COLUMNS.MGSpan(s.ToCharArray(), 0, s.Length);
                }
            }
        }

        return columns;
    }
}