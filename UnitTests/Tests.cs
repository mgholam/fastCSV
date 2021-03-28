using NUnit.Framework;
using System;
using System.IO;

public class tests
{
    public class ABC
    {
        public string a { get; set; }
        public string b { get; set; }
        public string c { get; set; }

        public new string ToString()
        {
            return a + "," + b + "," + c;
        }
    }

    [Test]
    public static void trailing_newline()
    {
        var l = fastCSV.ReadStream<ABC>(new StringReader("a,b\r\n"), false, ',', (o, c) =>
        {
            o.a = c[0];
            o.b = c[1];
            return true;
        });
        Assert.AreEqual(1, l.Count);
        Assert.AreEqual("b", l[0].b);
    }

    [Test]
    public static void multi_line()
    {
        var l = fastCSV.ReadStream<ABC>(new StringReader("a,\"b\r\nc \"\r\n"), false, ',', (o, c) =>
        {
            o.a = c[0];
            o.b = c[1];
            return true;
        });
        Assert.AreEqual(1, l.Count);
        Assert.AreEqual("b\r\nc ", l[0].b);
    }

    [Test]
    public static void tail_quote()
    {
        var l = fastCSV.ReadStream<ABC>(new StringReader("a,\" b\r\nc \""), false, ',', (o, c) =>
        {
            o.a = c[0];
            o.b = c[1];
            return true;
        });
        Assert.AreEqual(1, l.Count);
        Assert.AreEqual(" b\r\nc ", l[0].b);
    }

    [Test]
    public static void leading_space()
    {
        var l = fastCSV.ReadStream<ABC>(new StringReader("a, b\r\n"), false, ',', (o, c) =>
        {
            o.a = c[0];
            o.b = c[1];
            return true;
        });
        Assert.AreEqual(1, l.Count);
        Assert.AreEqual(" b", l[0].b);
    }

    [Test]
    public static void with_headers()
    {
        var csv = @"A,B
a,b
a1,b1
";
        var l = fastCSV.ReadStream<ABC>(new StringReader(csv), true, ',', (o, c) =>
        {
            o.a = c[0];
            o.b = c[1];
            return true;
        });
        Assert.AreEqual(2, l.Count);
        Assert.AreEqual("b", l[0].b);
    }

    [Test]
    public static void without_headers()
    {
        var csv = @"A,B
a,b
a1,b1
";
        var l = fastCSV.ReadStream<ABC>(new StringReader(csv), false, ',', (o, c) =>
        {
            o.a = c[0];
            o.b = c[1];
            return true;
        });
        Assert.AreEqual(3, l.Count);
        Assert.AreEqual("B", l[0].b);
    }

    [Test]
    public static void tailing_blank_lines()
    {
        var csv = @"A,B
a,b
a1,b1


";
        var l = fastCSV.ReadStream<ABC>(new StringReader(csv), false, ',', (o, c) =>
        {
            o.a = c[0];
            o.b = c[1];
            return true;
        });
        Assert.AreEqual(5, l.Count);
        Assert.AreEqual("B", l[0].b);
    }

    [Test]
    public static void blank_lines()
    {
        var csv = @"A,B
a,b
a1,b1

c,d
";
        var l = fastCSV.ReadStream<ABC>(new StringReader(csv), false, ',', (o, c) =>
        {
            o.a = c[0];
            o.b = c[1];
            return true;
        });
        Assert.AreEqual(5, l.Count);
        Assert.AreEqual("d", l[4].b);
    }

    public class cars
    {
        public int Year;
        public string Make;
        public string Model;
        public string Description;
        public decimal Price;
    }

    [Test]
    public static void csv_standard()
    {

        var listcars = fastCSV.ReadFile<cars>("csvstandard.csv", true, ',', (o, c) =>
        {
            Console.WriteLine(c.ColumnName(4));
            o.Year = fastCSV.ToInt(c[0]);
            o.Make = c[1];
            o.Model = c[2];
            o.Description = c[3];
            o.Price = decimal.Parse(c[4]);
            return true;
        });
        Assert.AreEqual(6, listcars.Count);

        Assert.AreEqual(" Toyota", listcars[5].Make);
        Assert.AreEqual("ac, abs, \r\nmoon", listcars[0].Description);
        Assert.AreEqual(3000.00, listcars[0].Price);

        Assert.AreEqual("E350\r\nF150", listcars[0].Model);


        Assert.AreEqual("Venture \"Extended Edition\"", listcars[1].Model);
        Assert.AreEqual("Venture \"Extended Edition, Very Large\"", listcars[2].Model);

    }
}
