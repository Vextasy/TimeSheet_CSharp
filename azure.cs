namespace azure;

 /*
  * Credentials required by the TokenAcquirerFactory.
  *  - "Instance": "https://login.microsoftonline.com/",
  *  - "TenantId": "<the azure tenant id>",
  *  - "ClientId": "<the app registration application (client) id>",
  *  - "ClientSecret": "<the azure client secret>" 
  */
 public class Auth {
    public string? Instance { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
 }