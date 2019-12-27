# fastCSV

Fast CSV reader writer in c#.

## Features

- Fully CSV standard compliant 
  - Multi-line
  - Quoted columns
  - Keeps spaces between delimitors
- Really fast reading and writing of CSV files
- Tiny 8kb DLL
- Ability to get a typed list of objects from a CSV file
- Ability to filter a CSV file while loading
- Ability to specify a custom delimitor

## Usage

```c#
public class cars
{
    // you can use fields or properties
    public string Year;
    public string Make;
    public string Model;
    public string Description;
    public string Price;
}

// listcars = List<cars>
var listcars = fastCSV.ReadFile<cars>(
    "csvstandard.csv", // filename
    true,              // has header
    ',',               // delimitor
    (o, c) =>          // to object function o : cars object, c : columns array read
    {
        o.Year = c[0];
        o.Make = c[1];
        o.Model = c[2];
        o.Description = c[3];
        o.Price = c[4];
        // add to list
        return true;
    });

fastCSV.WriteFile<LocalWeatherData>(
    "filename2.csv",   // filename
    new string[] { "WBAN", "Date", "SkyCondition" }, // headers
    '|',               // delimitor
    list,              // list of LocalWeatherData to save
    (o, c) =>          // from object function 
	{
    	c.Add(o.WBAN);
    	c.Add(o.Date.ToString("yyyyMMdd"));
    	c.Add(o.SkyCondition);
	});
```

## Helper functions for performance

`fastCSV` has the following helper functions:

- `int ToInt(string s, int index, int count)` creates an `int` from a substring 
- `DateTime ToDateTimeISO(string value, bool UseUTCDateTime)` creates an ISO standard `DateTime` i.e. `yyyy-MM-ddTHH:mm:ss`  ( optional part`.nnnZ`)

```c#
public class LocalWeatherData
{
    public string WBAN;
    public DateTime Date;
    public string SkyCondition;
}

var list = fastCSV.ReadFile<LocalWeatherData>("201503hourly.txt", true, ',', (o, c) =>
    {
        bool add = true;
        o.WBAN = c[0];
        o.Date = new DateTime(fastCSV.ToInt(c[1], 0, 4), 
                              fastCSV.ToInt(c[1], 4, 2), 
                              fastCSV.ToInt(c[1], 6, 2));
        o.SkyCondition = c[4];
        //if (o.Date.Day % 2 == 0)
        //    add = false;
        return add;
    });
```

## Performance

Loading the https://www.ncdc.noaa.gov/orders/qclcd/QCLCD201503.zip file which has 4,496,263 rows on my machine as a relative comparison to other libraries:

- **fastcsv** : 11.21s 638Mb used
- **nreco.csv** : 19.05s  800Mb used
- **.net string.Split()** : 11.50s 638Mb used
- **tinycsvparser** : 34s 992Mb used