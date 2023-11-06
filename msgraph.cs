using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Duration;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using System.Text.RegularExpressions;

namespace msgraph;


public interface IClient
{
    Task<List<task.Task>> TimesheetItems(string userPrincipalName, DateTime dateFrom, DateTime dateTo);
}

public class Client : IClient
{
    private GraphServiceClient graphclient;

    public static (IClient? client, string? error) New(azure.Auth auth)
    {

        TokenAcquirerFactory tokenAcquirerFactory = TokenAcquirerFactory.GetDefaultInstance();
        IServiceCollection services = tokenAcquirerFactory.Services;
        services
            .AddMicrosoftGraph();

        // Ensure that the tokenAcquirerFactory has the required authentication configuration.
        var C = tokenAcquirerFactory.Configuration;
        C["AzureAd:Instance"] = auth.Instance;
        C["AzureAd:TenantId"] = auth.TenantId;
        C["AzureAd:ClientId"] = auth.ClientId;
        C["AzureAd:ClientCredentials:0:SourceType"] = "ClientSecret";
        C["AzureAd:ClientCredentials:0:ClientSecret"] = auth.ClientSecret;

        var serviceProvider = tokenAcquirerFactory.Build();
        if(serviceProvider == null)
            return (null, "Could not create service provider");
        var graphclient = serviceProvider.GetService<GraphServiceClient>();
        if(graphclient == null)
            return(null, "Could not create graph service client");
        return (new Client(graphclient), null);
    }

    public Client(GraphServiceClient graphclient)
    {
        this.graphclient = graphclient;
    }

    // TimesheetItems returns those MS Graph calendar tasks that
    // start between the given dates and whose description matches
    // the pattern: project (- group) - description.
    public async Task<List<task.Task>> TimesheetItems(string userPrincipalName, DateTime dateFrom, DateTime dateTo){
        var tasks = new List<task.Task>();
        var users = await graphclient.Users.GetAsync(
            r => r.Options.WithAppOnly()
        );
        if(users == null || users.Value == null)
            return tasks;
        User? targetUser = null;
        foreach (var user in users.Value) {
            if (user.UserPrincipalName == userPrincipalName)
                targetUser = user;
        }
        if (targetUser != null) {
            var events = await graphclient
                .Users[$"{targetUser.Id}"]
                .Calendar.Events.GetAsync(r => {
                    r.QueryParameters.Select = new []{"subject", "start", "end"};
                    // Microsoft graph stores datetimes in UTC. So convert our range to UTC.
                    var start = dateFrom.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.0000000");
                    var end = dateTo.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.0000000");
                    r.QueryParameters.Filter = $"start/DateTime ge '{start}' and start/DateTime le '{end}' and IsAllDay eq false";
                    r.Options.WithAppOnly();
                });
            if (events == null || events.Value == null)
                return tasks;
            var eventList = events.Value.ToList();
            foreach (var ev in eventList)
            {
                if(ev == null || ev.Subject == null)
                    continue;
                // proj (- group) - description
                var pat = @"^\s*([\w/]+)(?:\s*-\s*([\w/]+))?\s*-\s*(.*)";
                var m = Regex.Match(ev.Subject, pat) ;
                if (m.Success){
                    string proj = m.Groups[1].Value;
                    string group = m.Groups[2].Value;
                    string desc = m.Groups[3].Value;
                    // Convert back to local time.
                    DateTime start = ev.Start.ToDateTime().ToLocalTime();
                    DateTime end = ev.End.ToDateTime().ToLocalTime();
                    TimeSpan duration = end.Subtract(start); 
                    tasks.Add (new task.Task(proj, group, desc, start, duration));
                }
            }
            return tasks;
        }
        return tasks;
    }
}


