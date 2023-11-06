using System.Text.RegularExpressions;


namespace task;


public interface IReader {
    Task<List<Project>> Read(string userPrincipalName, DateTime dateFrom, DateTime dateTo);
}

// Reader returns the list of MS Graph calendar projects that contain
// tasks that started between the two given dates for the user given
// by the userPrincipalName argument.
// The tasks for each project are sorted by start time and their total
// duration is summed at the level of individual task, task group and project.
public class Reader: IReader
{
    private msgraph.IClient graph;

    public static IReader New(msgraph.IClient graph) {
        return new Reader(graph);
    }

    public Reader(msgraph.IClient graph)
    {
        this.graph = graph;
    }

    public async Task<List<Project>> Read(string userPrincipalName, DateTime start, DateTime finish){
        var P = new Dictionary<string, Project>();
        foreach(var item in await graph.TimesheetItems(userPrincipalName, start, finish)){
            var proj = P.TryGetValue(item.proj, out Project? p) ? p : P[item.proj] = new Project(item.proj);    
            proj.tasks.Add(item);
        }
        foreach(var key in P.Keys){
            // Arrange tasks in start order and summarize.
            P[key].tasks.Sort((a, b) => a.start.CompareTo(b.start));
            foreach(var task in P[key].tasks){
                P[key].Sum(task);
            }
        }
        // Arrange projects in time order of their first task.
        var projects = P.Values.ToList();
        projects.Sort((a, b) => a.tasks[0].start.CompareTo(b.tasks[0].start));
        return projects;
    }
}


// A task represents an item in the MS Graph calendar.
// We are interested in tasks that have a non-empty subject field
// and which have a subject that matches the pattern: project (- group) - description.
// So, for example:
// ProjectAlpha-Testing-Setup, ProjectAlpha-Testing-Documentation, ProjectAlpha-Administration.
// Where the first two are examples that have groups and the final example does not.
public class Task
{
    public string proj;
    public string group;
    public string desc;
    public DateTime start;
    public TimeSpan duration;
    public Task(string proj, string group, string desc, DateTime start, TimeSpan duration)
    {
        this.proj = proj;
        this.group = group;
        this.desc = desc;
        this.start = start;
        this.duration = duration;
    }
}

// A task summary contains the aggregate duration of all the
// tasks in a project that share the same subject.
public class TaskSummary
{
    public string desc;
    public string group;
    public TimeSpan duration;
    public TaskSummary(string desc, string group)
    {
        this.desc = desc;
        this.group = group;
        duration = new TimeSpan(0, 0, 0);
    }
}

// A group summary contains the aggregate duration of all the
// tasks in a project that share the same group.
public class GroupSummary
{
    public string group;
    public TimeSpan duration;
    public GroupSummary(string group)
    {
        this.group = (group.Length == 0) ? "Ungrouped" : group;
        duration = new TimeSpan(0, 0, 0);
    }
}


// A project holds a collection of tasks all of which share the same project name.
// The member function Sum() sums up the duration of all tasks in the project into a task summary
// and allocates those that have a group to the appropriate group summary.
public class Project
{
    public string name { get; }
    public List<Task> tasks { get; }
    public Dictionary<string, TaskSummary> summary { get; } // summary by task
    public Dictionary<string, GroupSummary> groups { get; } // summary by group

    public Project(string name)
    {
        this.name = name;
        this.tasks = new List<Task>();
        this.summary = new Dictionary<string, TaskSummary>();
        this.groups = new Dictionary<string, GroupSummary>();
    }

    public void Sum(Task t)
    {
        var key = t.group + "_" + t.desc;
        var sum = this.summary.TryGetValue(key, out TaskSummary? taskSummary)
            ? taskSummary
            : this.summary[key] = new TaskSummary(t.desc, t.group);

        var grp = this.groups.TryGetValue(t.group, out GroupSummary? groupSummary)
            ? groupSummary
            : this.groups[t.group] = new GroupSummary(t.group);

        sum.duration += t.duration;
        grp.duration += t.duration;
    }
}
