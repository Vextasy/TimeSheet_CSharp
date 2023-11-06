// See https://aka.ms/new-console-template for more information

using System.Reflection.Metadata;
using Microsoft.Graph.Models.ExternalConnectors;
using Microsoft.IdentityModel.Abstractions;

class Program
{
    static async Task Main(string[] args)
    {
        // Wire up the components.
        var param = parameter.Reader.New(args);
        var (graph, error) = msgraph.Client.New(param.Auth);
        if(graph == null) { // error will be non-null
            Console.Error.WriteLine(error);
            System.Environment.Exit(1);
        }
        var calendar = task.Reader.New(graph);
        var dumper = dump.Dump.New(calendar);

        // Read the parameters
        var userPrincipalName = param.UserPrincipalName;
        var dateFrom = param.DateFrom;
        var dateTo = param.DateTo;
        if (userPrincipalName == null){
            Console.Error.WriteLine("A value for UserPrincipalName is required");
            System.Environment.Exit(1);
        }
        if (dateFrom == null || dateTo == null){
           Console.Error.WriteLine("A value for both DateFrom and DateTo is required"); 
            System.Environment.Exit(1);
        }

        // Output the results
        var output = await dumper.summary(userPrincipalName, dateFrom.Value, dateTo.Value);
        foreach(var s in output){
            Console.WriteLine(s);
        }
    }

    
    
}

