# Jira Time Logger

This application logs work time to Jira tasks by parsing Git commits for Jira task IDs and their associated commit messages. The hours logged are distributed equally across all tasks and working days in a given month.

## Features

- Extracts Jira task IDs and associated commit messages from a local Git repository.
- Logs work time to Jira tasks via the Tempo API.
- Allows specification of non-working days (weekends and holidays) and avoids logging time on those days.
- Distributes specified total hours equally across all tasks and working days in a given month.
  
## Usage

1. Fill in the static string fields `_jiraUrl`, `_jiraUserEmail`, `_jiraUserApiKey`, `_tempoApiUrl`, `_tempoApiKey`, `_repoLocation`, and `_repoUserName` with the appropriate values.
2. Run the program.
3. When prompted, enter the first day of the month to log and the total hours to log.

## Prerequisites

- .NET Core
- Newtonsoft.Json library for JSON operations
- An Atlassian account with necessary permissions to log work time
- A local Git repository

## Dependencies

- System
- System.Diagnostics
- Newtonsoft.Json
- System.Text.RegularExpressions
- System.Net.Http.Headers

## Configuration

- `_jiraUrl`: The URL of your Jira instance.
- `_jiraUserEmail`: The email address associated with your Jira user.
- `_jiraUserApiKey`: Your Jira API key.
- `_tempoApiUrl`: The URL of the Tempo API.
- `_tempoApiKey`: Your Tempo API key.
- `_repoLocation`: The location of your local Git repository.
- `_repoUserName`: The username associated with your Git commits.

## Disclaimer

This software is provided "as is" and any expressed or implied warranties, including, but not limited to, the implied warranties of merchantability and fitness for a particular purpose are disclaimed. In no event shall the contributor(s) be liable for any direct, indirect, incidental, special, exemplary, or consequential damages (including, but not limited to, procurement of substitute goods or services; loss of use, data, or profits; or business interruption) however caused and on any theory of liability, whether in contract, strict liability, or tort (including negligence or otherwise) arising in any way out of the use of this software, even if advised of the possibility of such damage.
