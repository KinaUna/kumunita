namespace Kumunita.Identity.Infrastructure;

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    public string SenderEmail { get; set; } = "noreply@kumunita.org";
    public string SenderName { get; set; } = "Kumunita";
}
