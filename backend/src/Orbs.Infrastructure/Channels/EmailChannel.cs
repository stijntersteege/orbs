using System.Globalization;
using System.Text;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using Orbs.Application.Channels;
using Orbs.Application.Tools;

namespace Orbs.Infrastructure.Channels;

/// <summary>
/// An orb with its own email address: new mail in the inbox is a message, the reply goes back by SMTP.
/// Each sender is a separate chat; pair a sender by mailing the code.
/// </summary>
internal sealed class EmailChannel : IChannelAdapter
{
    public string Kind => "email";

    public string DisplayName => "Email";

    public string Description => "Give your orb its own mailbox. Mail it, and it answers by email.";

    public IReadOnlyList<IntegrationField> Fields { get; } =
    [
        new("imapHost", "IMAP server", Required: true, Placeholder: "imap.example.com"),
        new("imapPort", "IMAP port", Placeholder: "993"),
        new("smtpHost", "SMTP server", Required: true, Placeholder: "smtp.example.com"),
        new("smtpPort", "SMTP port", Placeholder: "587"),
        new("username", "User name", Required: true),
        new("from", "Address", Placeholder: "pip@example.com", Hint: "The address replies come from. Defaults to the user name."),
        new("password", "Password", Required: true, Secret: true),
    ];

    public async Task<string> ConnectAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        using var imap = await ImapAsync(config, cancellationToken);
        await imap.DisconnectAsync(true, cancellationToken);
        return config.Find("from") ?? config.Get("username");
    }

    public async Task<ChannelPoll> PollAsync(ChannelConfig config, string? cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        using var imap = await ImapAsync(config, cancellationToken);
        await imap.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
        var after = uint.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var last) ? last : 0u;
        var ids = (await imap.Inbox.SearchAsync(SearchQuery.All, cancellationToken)).Where(u => u.Id > after).OrderBy(u => u.Id).ToList();
        var messages = new List<ChannelMessage>();

        // The first poll only takes the position: mail that was already there is not answered.
        if (cursor is not null)
        {
            foreach (var uid in ids.Take(20))
            {
                var mail = await imap.Inbox.GetMessageAsync(uid, cancellationToken);
                var from = mail.From.Mailboxes.FirstOrDefault();
                if (from is not null)
                {
                    messages.Add(new ChannelMessage(from.Address, from.Name ?? from.Address, Body(mail)));
                }

                await imap.Inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
            }
        }

        var next = ids.Count == 0 ? cursor ?? "0" : (cursor is null ? ids[^1].Id : ids.Take(20).Last().Id).ToString(CultureInfo.InvariantCulture);
        await imap.DisconnectAsync(true, cancellationToken);
        return new ChannelPoll(messages, next);
    }

    /// <summary>The new part of a mail: the text body without quoted lines and without the signature.</summary>
    internal static string Body(MimeMessage mail)
    {
        var text = mail.TextBody ?? (mail.HtmlBody is { } html ? Context.UrlConnector.HtmlToText(html) : string.Empty);
        var kept = new StringBuilder();
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.TrimStart().StartsWith('>') || line.StartsWith("On ", StringComparison.Ordinal) && line.TrimEnd().EndsWith("wrote:", StringComparison.Ordinal))
            {
                continue;
            }

            if (line == "-- ")
            {
                break;
            }

            kept.AppendLine(line);
        }

        var body = kept.ToString().Trim();
        return string.IsNullOrEmpty(mail.Subject) ? body : $"{mail.Subject}\n\n{body}".Trim();
    }

    public async Task SendAsync(ChannelConfig config, string chatId, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config.Find("from") ?? config.Get("username")));
        message.To.Add(MailboxAddress.Parse(chatId));
        message.Subject = "Re: your message";
        message.Body = new TextPart("plain") { Text = text };

        using var smtp = new SmtpClient();
        var port = Port(config, "smtpPort", 587);
        await Integrations.IntegrationKit.EnsureReachableAsync(AsIntegration(config), config.Get("smtpHost"), cancellationToken);
        await smtp.ConnectAsync(config.Get("smtpHost"), port, port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
        await smtp.AuthenticateAsync(config.Get("username"), config.RequireSecret(), cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }

    public Task<string?> SetProfileAsync(ChannelConfig config, string name, string description, byte[]? avatarPng, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(avatarPng is null ? null : "Email has no profile picture to set.");

    private static async Task<ImapClient> ImapAsync(ChannelConfig config, CancellationToken cancellationToken)
    {
        var imap = new ImapClient();
        try
        {
            var port = Port(config, "imapPort", 993);
            await Integrations.IntegrationKit.EnsureReachableAsync(AsIntegration(config), config.Get("imapHost"), cancellationToken);
            await imap.ConnectAsync(config.Get("imapHost"), port, port == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
            await imap.AuthenticateAsync(config.Get("username"), config.RequireSecret(), cancellationToken);
            return imap;
        }
        catch
        {
            imap.Dispose();
            throw;
        }
    }

    private static IntegrationConfig AsIntegration(ChannelConfig config) => new(config.Id, "Email channel", config.Settings, config.Secret, config.RestrictNetwork);

    private static int Port(ChannelConfig config, string key, int fallback) =>
        int.TryParse(config.Find(key), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and < 65536 ? port : fallback;
}
