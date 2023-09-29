using System.Diagnostics;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;

class Program
{
    private static readonly string _jiraUrl = "";
    private static readonly string _jiraUserEmail = "";
    private static readonly string _jiraUserApiKey = "";

    private static readonly string _tempoApiUrl = "https://api.tempo.io/4/worklogs";
    private static readonly string _tempoApiKey = "";

    private static readonly string _repoLocation = "";
    private static readonly string _repoUserName = "";

    static async Task Main(string[] args)
    {
        string commitAuthor = _repoUserName;

        Console.WriteLine("Enter first day to log (yyyy-MM-dd): ");
        string date = Console.ReadLine();

        if (!DateTime.TryParse(date, out DateTime startDate))
        {
            Console.WriteLine("Invalid input for date.");
            return;
        }
        DateTime endDate = startDate.AddDays(DateTime.DaysInMonth(startDate.Year, startDate.Month)).AddMilliseconds(-1);

        Console.WriteLine("Enter Total Hours to Log: ");
        if (!double.TryParse(Console.ReadLine(), out double totalHours))
        {
            Console.WriteLine("Invalid input for hours.");
            return;
        }

        string endDateString = endDate.ToString("yyyy-MM-dd");
        string gitLogCmd = $"cd {_repoLocation} && git log --since='{date}' --until='{endDateString}' --author=\"{commitAuthor}\" --pretty=format:\"%s\"";
        Process? process = null;
        //unix
        if (_repoLocation.Contains("/"))
        {
            process = new Process()
            {

                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = $"-c \"{gitLogCmd}\""
                }
            };
        }
        else //windows
        {
            process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = $"/c \"{gitLogCmd}\""
                }
            };
        }

        Console.WriteLine($"Running command: {gitLogCmd}");

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var regex = new Regex(@"\[(.*?)\] (.*)");
        var matches = regex.Matches(output);

        Console.WriteLine($"Found {matches.Count} commits.");

        var timesheets = new List<TimesheetEntry>();

        Console.WriteLine("Parsing commits...");

        foreach (Match match in matches)
        {
            var jiraTag = match.Groups[1].Value;
            var commitMessage = match.Groups[2].Value;
            timesheets.Add(new TimesheetEntry { JiraTag = jiraTag, CommitMessage = commitMessage });
        }


        var holidays = new List<DateTime>
        {
            // Add known holidays here.
             new DateTime(2023, 12, 25),
             new DateTime(2023, 12, 26),
             new DateTime(2023, 11, 1),
             new DateTime(2023, 11, 11),

        };

        var workdays = GetWorkdays(startDate, endDate, holidays);
        double hoursPerDay = Math.Round(totalHours / workdays.Count, 2); // Equal hours to be logged on each working day
        double remainderHours = totalHours % workdays.Count; // Calculate remainder hours


        int timesheetIndex = 0;

        int totalTasks = timesheets.Count;
        double hoursPerTask = totalHours / totalTasks;
        double[] hoursForEachTask = new double[totalTasks];

        // Assign average hours to each task
        for (int i = 0; i < totalTasks; i++)
        {
            hoursForEachTask[i] = Math.Floor(hoursPerTask * 100) / 100; // Keep only two decimal places
        }

        // Assign remaining hours to the tasks
        double remainingHours = totalHours - (hoursForEachTask.Sum());
        double taskLeftHoursFotNextDay = 0;

        for (int i = 0; remainingHours > 0; i = (i + 1) % totalTasks, remainingHours -= 0.01)
        {
            hoursForEachTask[i] += 0.01;
        }

        var jiraAuthor = await GetAuthorId(_jiraUserEmail);

        // Log hours for each workday
        foreach (DateTime workday in workdays)
        {
            if (timesheetIndex >= totalTasks)
                break;

            TimesheetEntry entry = timesheets[timesheetIndex];

            double hoursToLog = Math.Round(hoursForEachTask[timesheetIndex], 2);

            if (hoursPerDay < hoursForEachTask[timesheetIndex])
            {
                taskLeftHoursFotNextDay = hoursForEachTask[timesheetIndex] - hoursPerDay;
                hoursToLog = hoursPerDay;
                hoursForEachTask[timesheetIndex] = taskLeftHoursFotNextDay;
                Console.WriteLine($"Logging {hoursToLog} hours to {entry.JiraTag} {entry.CommitMessage} on {workday:yyyy-MM-dd}.");

                var issueId = await GetIssueId(entry.JiraTag);
                var json = PrepareTimesheetJson(issueId, hoursToLog, entry.CommitMessage, workday, jiraAuthor);
                await LogTime(json, hoursToLog, entry.JiraTag, workday, entry.CommitMessage);
            }
            else
            {
                Console.WriteLine($"Logging {hoursToLog} hours to {entry.JiraTag} {entry.CommitMessage} on {workday:yyyy-MM-dd}.");
                var issueId = await GetIssueId(entry.JiraTag);
                var json = PrepareTimesheetJson(issueId, hoursToLog, entry.CommitMessage, workday, jiraAuthor);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                await LogTime(json, hoursToLog, entry.JiraTag, workday, entry.CommitMessage);

                if (timesheetIndex < totalTasks - 1)
                    timesheetIndex++;
                entry = timesheets[timesheetIndex];
                hoursToLog = Math.Round(hoursPerDay - hoursToLog, 2);
                taskLeftHoursFotNextDay = hoursForEachTask[timesheetIndex] - hoursToLog;

                hoursForEachTask[timesheetIndex] = taskLeftHoursFotNextDay;
                if (hoursToLog > 0)
                {
                    Console.WriteLine($"Logging {hoursToLog} hours to {entry.JiraTag} {entry.CommitMessage} on {workday:yyyy-MM-dd}.");
                    issueId = await GetIssueId(entry.JiraTag);
                    json = PrepareTimesheetJson(issueId, hoursToLog, entry.CommitMessage, workday, jiraAuthor);
                    await LogTime(json, hoursToLog, entry.JiraTag, workday, entry.CommitMessage);
                }

            }

        }
    }

    public static async Task LogTime(string json, double hoursToLog, string jiraTag, DateTime workday, string commitMessage)
    {
        using (HttpClient httpClient = new HttpClient())
        {
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tempoApiKey);
            var response = await httpClient.PostAsync("https://api.tempo.io/4/worklogs", content);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Successfully logged {hoursToLog} hours to {jiraTag} {commitMessage} on {workday:yyyy-MM-dd}.");
            }
            else
            {
                Console.WriteLine($"Failed to log time to {jiraTag} on {workday:yyyy-MM-dd}. Response: {response.Content.ReadAsStringAsync().Result}");
            }
        }
    }

    public async static Task<int> GetIssueId(string issueKey)
    {
        HttpClient client = new HttpClient();
        string jiraUrl = _jiraUrl;
        string apiUrl = $"{jiraUrl}/rest/api/3/issue/{issueKey}";

        string username = _jiraUserEmail;
        string apiToken = _jiraUserApiKey;


        // Set the authorization header if required
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{apiToken}")));


        HttpResponseMessage response = await client.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();

        // Parse the JSON response to get the issue ID
        // Example: assuming the response is in JSON format and contains an "id" field
        dynamic issueData = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
        string issueId = issueData.id;

        return int.Parse(issueId);
    }

    public async static Task<string> GetAuthorId(string email)
    {
        HttpClient client = new HttpClient();
        string jiraUrl = _jiraUrl;
        string url = $"{jiraUrl}/rest/api/3/user/search?query={email}";

        string username = _jiraUserEmail;
        string apiToken = _jiraUserApiKey;

        // Set the authorization header if required
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{apiToken}")));


        HttpResponseMessage response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();

        // Parse the JSON response to get the issue ID
        // Example: assuming the response is in JSON format and contains an "id" field
        dynamic users = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
        string author = users[0]["accountId"].ToString();

        return author;
    }

    private static string PrepareTimesheetJson(int issueId, double hoursToLog, string commitMessage, DateTime workday, string authorAccountId)

    {
        var tempoTimesheet = new
        {
            authorAccountId = authorAccountId,
            issueId = issueId,
            timeSpentSeconds = (int)(hoursToLog * 3600),
            startDate = workday.ToString("yyyy-MM-dd"),
            startTime = "08:00:00"
        };

        string json = JsonConvert.SerializeObject(tempoTimesheet);

        return json;
    }

    class TimesheetEntry
    {
        public string JiraTag { get; set; }
        public string CommitMessage { get; set; }
    }

    static List<DateTime> GetWorkdays(DateTime start, DateTime end, List<DateTime> holidays)
    {
        var workdays = new List<DateTime>();

        for (DateTime date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday && !holidays.Contains(date))
            {
                workdays.Add(date);
            }
        }

        return workdays;
    }
}
