using System.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models.ExternalConnectors;

namespace parameter;

public interface IReader
{
    string? UserPrincipalName {get;}
    DateTime? DateFrom {get;}
    DateTime? DateTo {get;}
    azure.Auth Auth {get;}
}

class Reader : IReader
{
    DateTime? dateFrom, dateTo;
    public static IReader New(string[] args) {
        return new Reader(args);
    }
    public Reader(string[] args){
        optArgs(args);
    }
    public string? UserPrincipalName {get {
        return AppSettings.Current.UserPrincipalName;
    }}
    public DateTime? DateFrom {get {
        return AppSettings.Current.DateFrom ?? dateFrom;        // allow the environment to override.
    }}
    public DateTime? DateTo {get {
        return AppSettings.Current.DateTo ?? dateTo;            // allow the environment to override.
    }}
    public azure.Auth Auth {get{return AppSettings.Current.Auth;}} 

    // OptArgs will set dateFrom and dateTo according to the command
    // line arguments passed to it.
    private void optArgs(string[] args) {
        switch(args.Length){
        case 0:
            (this.dateFrom, this.dateTo) = month_offset(DateTime.Today, 0); // default
            break;
        case 1:
            var arg = args[0];
            if (arg.Length > 0){
                if(int.TryParse(arg, out int n)){
                    // n-months back.
                    (this.dateFrom, this.dateTo) = month_offset(DateTime.Today, n);
                } else
                    Usage();
            }
            break;
        default:
            Usage();
            break;    
        }
        if(this.dateFrom != null)
            this.dateFrom = Util.asFromDate(this.dateFrom.Value);
        if(this.dateTo != null)
            this.dateTo = Util.asToDate(this.dateTo.Value);
    }
    // Month_offset returns the start and end date of the month that is n months before
    // the origin date, o.
    // So, if n == 0, it will return the start and end date of the current month.
    // Month_offset retains the Kind of its datetime argument.
    static (DateTime dateFrom, DateTime dateTo) month_offset(DateTime o, int n){
        var dateFrom = new DateTime(o.Year, o.Month, 1).AddMonths(-n);
        dateFrom = DateTime.SpecifyKind(dateFrom, o.Kind);
        var f = dateFrom;
        var dateTo = new DateTime(f.Year, f.Month, DateTime.DaysInMonth(f.Year, f.Month));
        dateTo = DateTime.SpecifyKind(dateTo, o.Kind);
        return (dateFrom, dateTo);
    }
    static void Usage(){
        Action<string> W = Console.WriteLine;
        W("Usage: ");
        W("timesheet");
        W("timesheet n");
        W("");
        W("Timesheet without any arguments will print the timesheet for the current month");
        W("and default user principal name.");
        W("With a single integer argument of 'n' it will print the timesheet for the whole month");
        W("but 'n' months back. So an argument of 1 will print the timesheet for last month.");
        W("UserPrincipalName will typically be read from an entry with the same name");
        W("in the appsettings.json file.");
        W("Supplying environment variables for DateFrom, DateTo and UserPrincipalName");
        W("allows a more specific date range and a different user principal name to be used.");
        System.Environment.Exit(1);
    }
}

/*
 * appsettings.json should have the following format:
 * {
 *     "UserPrincipalName": "[the default user principal name - the calendar owner]",
 * }
 * env.private should have the following JSON format:
 * {
 *      "Instance": "https://login.microsoftonline.com/",
 *      "TenantId": "<the azure tenant id>",
 *      "ClientId": "<the app registration application (client) id>",
 *      "ClientSecret": "<the azure client secret>" 
 * }
 */

public class AppSettings {
    private static AppSettings? _appSettings;
    public string? UserPrincipalName {get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public azure.Auth Auth {get; set;}

    public AppSettings(IConfiguration config) {
        this.UserPrincipalName = config.GetValue<string>("UserPrincipalName");
        this.DateFrom = config.GetValue<DateTime?>("DateFrom");
        this.DateTo = config.GetValue<DateTime?>("DateTo");
        // Ensure that datetimes from the environment are treated as localtime.
        if(this.DateFrom != null){
            this.DateFrom = DateTime.SpecifyKind(this.DateFrom.Value, DateTimeKind.Local);
            this.DateFrom = Util.asFromDate(this.DateFrom.Value);
        }
        if(this.DateTo != null){
            this.DateTo = DateTime.SpecifyKind(this.DateTo.Value, DateTimeKind.Local);
            this.DateTo = Util.asToDate(this.DateTo.Value);
        }
        // Azure Authentication
        this.Auth = new azure.Auth{
            Instance = config.GetValue<string>("Instance"),
            TenantId = config.GetValue<string>("TenantId"),
            ClientId = config.GetValue<string>("ClientId"),
            ClientSecret = config.GetValue<string>("ClientSecret")
        };
        _appSettings = this;
    }

    public static AppSettings Current { get {
        if(_appSettings == null)
            _appSettings = GetCurrentSettings();
        return _appSettings;
    }}

    public static AppSettings GetCurrentSettings() {
        var builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile("env.private", optional:false, reloadOnChange: false)
                        .AddEnvironmentVariables();
        IConfigurationRoot configuration = builder.Build();
        var settings = new AppSettings(configuration);
        return settings;
    }
}


class Util {
    static public DateTime asFromDate(DateTime d){
        return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, d.Kind);
    }
    static public DateTime asToDate(DateTime d){
       return new DateTime(d.Year, d.Month, d.Day, 23, 59, 59, d.Kind);
    }
}
