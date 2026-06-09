// One-time tool: run this locally to obtain a Google OAuth refresh token.
// The token is then stored in local.settings.json (and Azure Function App settings for production).
// You only need to run this once — the refresh token doesn't expire unless revoked.
//
// Usage:
//   cd tools/get-google-token
//   dotnet run
//
// You'll need your OAuth Client ID and Secret from Google Cloud Console.
// A browser window will open for you to log in and approve calendar read access.

using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Util.Store;

Console.WriteLine("=== Simian Bookings — Google Calendar Token Helper ===");
Console.WriteLine();
Console.WriteLine("Enter your Google OAuth Client ID:");
var clientId = Console.ReadLine()?.Trim();

Console.WriteLine("Enter your Google OAuth Client Secret:");
var clientSecret = Console.ReadLine()?.Trim();

if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
{
    Console.WriteLine("Client ID and Secret are required.");
    return;
}

Console.WriteLine();
Console.WriteLine("A browser window will open. Log in with your personal Google account");
Console.WriteLine("and approve read-only access to your calendar.");
Console.WriteLine();

var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
    new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
    [CalendarService.Scope.CalendarReadonly],
    "user",
    CancellationToken.None,
    new FileDataStore("simian-google-token-helper", fullPath: false));

Console.WriteLine();
Console.WriteLine("=== SUCCESS ===");
Console.WriteLine();
Console.WriteLine("Add these values to api/local.settings.json (Values section):");
Console.WriteLine();
Console.WriteLine($"  \"GoogleClientId\": \"{clientId}\",");
Console.WriteLine($"  \"GoogleClientSecret\": \"{clientSecret}\",");
Console.WriteLine($"  \"GoogleRefreshToken\": \"{credential.Token.RefreshToken}\",");
Console.WriteLine($"  \"GoogleCalendarId\": \"primary\"");
Console.WriteLine();
Console.WriteLine("Note: 'primary' refers to the default calendar for the account you just logged in with.");
Console.WriteLine("Use your full Google email address instead if you want a specific calendar.");
Console.WriteLine();
Console.WriteLine("For Azure production deployment, add these same values as Function App application settings.");
