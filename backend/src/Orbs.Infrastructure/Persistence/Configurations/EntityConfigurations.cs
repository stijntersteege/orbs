using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbs.Domain.Channels;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Jobs;
using Orbs.Domain.Memories;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;
using Orbs.Domain.Schedules;
using Orbs.Domain.Tools;
using Orbs.Domain.Users;

namespace Orbs.Infrastructure.Persistence.Configurations;

internal sealed class OrbConfiguration : IEntityTypeConfiguration<Orb>
{
    public void Configure(EntityTypeBuilder<Orb> builder)
    {
        builder.ToTable("orbs");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.HasIndex(o => o.OwnerId);
        builder.Property(o => o.Name).HasMaxLength(Orb.MaxNameLength).IsRequired();
        builder.Property(o => o.Description).HasMaxLength(Orb.MaxDescriptionLength).IsRequired();
        builder.Property(o => o.Instructions).HasMaxLength(Orb.MaxInstructionsLength).IsRequired();
        builder.Property(o => o.Voice).HasMaxLength(Orb.MaxVoiceLength);

        builder.ComplexProperty(o => o.Appearance, a =>
        {
            a.Property(x => x.Color).HasConversion<string>().HasMaxLength(20).HasColumnName("appearance_color");
            a.Property(x => x.Shape).HasConversion<string>().HasMaxLength(20).HasColumnName("appearance_shape");
            a.Property(x => x.Eyes).HasConversion<string>().HasMaxLength(20).HasColumnName("appearance_eyes");
            a.Property(x => x.Mouth).HasConversion<string>().HasMaxLength(20).HasColumnName("appearance_mouth");
            a.Property(x => x.Accessory).HasConversion<string>().HasMaxLength(20).HasColumnName("appearance_accessory");
            a.Property(x => x.Blush).HasColumnName("appearance_blush");
            a.Property(x => x.Glow).HasColumnName("appearance_glow");
            a.Property(x => x.Floating).HasColumnName("appearance_floating");
        });

        builder.OwnsOne(o => o.Model, m =>
        {
            m.Property(x => x.ProviderId).HasColumnName("model_provider_id");
            m.Property(x => x.Model).HasMaxLength(ModelRef.MaxModelLength).HasColumnName("model_name");
        });

        builder.Property<Dictionary<string, ToolAccess>>("_toolPermissions")
            .HasColumnName("tool_permissions")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, ToolAccess>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, ToolAccess>(),
                new ValueComparer<Dictionary<string, ToolAccess>>(
                    (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                    v => v.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                    v => new Dictionary<string, ToolAccess>(v, StringComparer.Ordinal)))
            .IsRequired();
        builder.Ignore(o => o.ToolPermissions);

        builder.PrimitiveCollection<List<Guid>>("_contextSourceIds")
            .HasColumnName("context_source_ids")
            .IsRequired();
        builder.Ignore(o => o.ContextSourceIds);
    }
}

internal sealed class ModelProviderConfiguration : IEntityTypeConfiguration<ModelProvider>
{
    public void Configure(EntityTypeBuilder<ModelProvider> builder)
    {
        builder.ToTable("providers");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasIndex(p => p.OwnerId);
        builder.Property(p => p.Name).HasMaxLength(ModelProvider.MaxNameLength).IsRequired();
        builder.Property(p => p.Kind).HasMaxLength(64).IsRequired();
        builder.Property(p => p.BaseUrl).HasMaxLength(2_048);
        builder.Property(p => p.ProtectedApiKey).HasMaxLength(4_096);
        builder.Property(p => p.DefaultModel).HasMaxLength(ModelProvider.MaxModelLength);
        builder.Property(p => p.EmbeddingModel).HasMaxLength(ModelProvider.MaxModelLength);
        builder.Property(p => p.SpeechToTextModel).HasMaxLength(ModelProvider.MaxModelLength);
        builder.Property(p => p.TextToSpeechModel).HasMaxLength(ModelProvider.MaxModelLength);
        builder.Ignore(p => p.HasApiKey);
    }
}

internal sealed class ContextSourceConfiguration : IEntityTypeConfiguration<ContextSource>
{
    public void Configure(EntityTypeBuilder<ContextSource> builder)
    {
        builder.ToTable("context_sources");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasIndex(s => s.OwnerId);
        builder.Property(s => s.Name).HasMaxLength(ContextSource.MaxNameLength).IsRequired();
        builder.Property(s => s.Kind).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Location).HasMaxLength(ContextSource.MaxLocationLength).IsRequired();
        builder.Property(s => s.Input).IsRequired();
        builder.Property(s => s.Content).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.LastError).HasMaxLength(1_000);
        builder.Property(s => s.SyncEvery).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => s.NextSyncAt);
    }
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.OwnerId);
        builder.Property(c => c.Title).HasMaxLength(Conversation.MaxTitleLength).IsRequired();
        builder.HasIndex(c => new { c.OrbId, c.UpdatedAt });

        builder.HasOne<Orb>().WithMany().HasForeignKey(c => c.OrbId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages).WithOne().HasForeignKey("ConversationId").OnDelete(DeleteBehavior.Cascade);
        builder.PrimitiveCollection<List<Guid>>("_guestOrbIds").HasColumnName("guest_orb_ids").IsRequired();
        builder.Ignore(c => c.OrbIds);
        builder.Ignore(c => c.IsGroup);
        builder.Navigation(c => c.Messages).HasField("_messages").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Content).HasMaxLength(Message.MaxContentLength).IsRequired();
        builder.Property(m => m.Model).HasMaxLength(Message.MaxModelLength);
        builder.OwnsMany(m => m.ToolCalls, t => t.ToJson("tool_calls"));
        builder.Navigation(m => m.ToolCalls).HasField("_toolCalls").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(User.MaxNameLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Ignore(u => u.HasPassword);
        builder.OwnsOne(u => u.QuietHours, q =>
        {
            q.Property(x => x.From).HasColumnName("quiet_from");
            q.Property(x => x.Until).HasColumnName("quiet_until");
            q.Property(x => x.TimeZone).HasColumnName("quiet_time_zone").HasMaxLength(100);
        });

        builder.OwnsMany(u => u.Passkeys, p =>
        {
            p.ToTable("passkeys");
            p.WithOwner().HasForeignKey("UserId");
            p.HasKey(x => x.Id);
            p.Property(x => x.Id).ValueGeneratedNever();
            p.Property(x => x.CredentialId).HasMaxLength(1_024).IsRequired();
            p.HasIndex(x => x.CredentialId).IsUnique();
            p.Property(x => x.PublicKey).IsRequired();
            p.Property(x => x.Name).HasMaxLength(Passkey.MaxNameLength).IsRequired();
        });
        builder.Navigation(u => u.Passkeys).HasField("_passkeys").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(u => u.ExternalLogins, l =>
        {
            l.ToTable("external_logins");
            l.WithOwner().HasForeignKey("UserId");
            l.HasKey(x => x.Id);
            l.Property(x => x.Id).ValueGeneratedNever();
            l.Property(x => x.Issuer).HasMaxLength(ExternalLogin.MaxIssuerLength).IsRequired();
            l.Property(x => x.Subject).HasMaxLength(ExternalLogin.MaxSubjectLength).IsRequired();
            l.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
        });
        builder.Navigation(u => u.ExternalLogins).HasField("_externalLogins").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(s => s.TokenHash).IsUnique();
        builder.Property(s => s.Description).HasMaxLength(Session.MaxDescriptionLength).IsRequired();
        builder.HasIndex(s => s.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.HasIndex(j => j.OwnerId);
        builder.Property(j => j.Kind).HasMaxLength(Job.MaxKindLength).IsRequired();
        builder.Property(j => j.Payload).HasMaxLength(Job.MaxPayloadLength).IsRequired();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.LastError).HasMaxLength(Job.MaxErrorLength);
        builder.Property(j => j.Result).HasMaxLength(Job.MaxResultLength);
        builder.HasIndex(j => new { j.Status, j.RunAt });
    }
}

internal sealed class ToolApprovalConfiguration : IEntityTypeConfiguration<ToolApproval>
{
    public void Configure(EntityTypeBuilder<ToolApproval> builder)
    {
        builder.ToTable("tool_approvals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.HasIndex(a => a.OwnerId);
        builder.HasIndex(a => new { a.ConversationId, a.Status });
        builder.Property(a => a.CallId).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Tool).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Arguments).IsRequired();
        builder.Property(a => a.State).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Conversation>().WithMany().HasForeignKey(a => a.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class McpServerConfiguration : IEntityTypeConfiguration<McpServer>
{
    public void Configure(EntityTypeBuilder<McpServer> builder)
    {
        builder.ToTable("mcp_servers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasIndex(s => s.OwnerId);
        builder.Property(s => s.Name).HasMaxLength(McpServer.MaxNameLength).IsRequired();
        builder.Property(s => s.Transport).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Location).HasMaxLength(McpServer.MaxLocationLength).IsRequired();
        builder.Property(s => s.ProtectedAuthorization).HasMaxLength(4_096);
        builder.Property(s => s.LastError).HasMaxLength(1_000);
        builder.Ignore(s => s.HasAuthorization);
    }
}

internal sealed class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("integrations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.HasIndex(i => i.OwnerId);
        builder.Property(i => i.Kind).HasMaxLength(64).IsRequired();
        builder.Property(i => i.Name).HasMaxLength(Integration.MaxNameLength).IsRequired();
        builder.Property(i => i.Settings).HasMaxLength(Integration.MaxSettingsLength).IsRequired();
        builder.Property(i => i.ProtectedSecret).HasMaxLength(4_096);
        builder.Ignore(i => i.HasSecret);
    }
}

internal sealed class MemoryConfiguration : IEntityTypeConfiguration<Memory>
{
    public void Configure(EntityTypeBuilder<Memory> builder)
    {
        builder.ToTable("memories");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasIndex(m => m.OwnerId);
        builder.HasIndex(m => new { m.OrbId, m.UpdatedAt });
        builder.Property(m => m.Content).HasMaxLength(Memory.MaxContentLength).IsRequired();
        builder.Property(m => m.Source).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(m => m.IsShared);
        builder.HasOne<Orb>().WithMany().HasForeignKey(m => m.OrbId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ContextChunkConfiguration : IEntityTypeConfiguration<ContextChunk>
{
    public void Configure(EntityTypeBuilder<ContextChunk> builder)
    {
        builder.ToTable("context_chunks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.OwnerId);
        builder.HasIndex(c => new { c.SourceId, c.Index });
        builder.Property(c => c.Text).HasMaxLength(ContextChunk.MaxTextLength).IsRequired();
        builder.Property(c => c.Vector).IsRequired();
        builder.Property(c => c.Model).HasMaxLength(200).IsRequired();
        builder.HasOne<ContextSource>().WithMany().HasForeignKey(c => c.SourceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ContextFileConfiguration : IEntityTypeConfiguration<ContextFile>
{
    public void Configure(EntityTypeBuilder<ContextFile> builder)
    {
        builder.ToTable("context_files");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.HasIndex(f => f.OwnerId);
        builder.HasIndex(f => f.SourceId).IsUnique();
        builder.Property(f => f.FileName).HasMaxLength(ContextFile.MaxFileNameLength).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Data).IsRequired();
        builder.Ignore(f => f.Size);
        builder.Ignore(f => f.IsImage);
        builder.HasOne<ContextSource>().WithMany().HasForeignKey(f => f.SourceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("schedules");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasIndex(s => s.OwnerId);
        builder.HasIndex(s => new { s.IsEnabled, s.NextCheckAt });
        builder.Property(s => s.Name).HasMaxLength(Schedule.MaxNameLength).IsRequired();
        builder.Property(s => s.Prompt).HasMaxLength(Schedule.MaxPromptLength).IsRequired();
        builder.Property(s => s.Trigger).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Cron).HasMaxLength(100);
        builder.Property(s => s.TimeZone).HasMaxLength(100).IsRequired();
        builder.Property(s => s.FeedUrl).HasMaxLength(2_048);
        builder.Property(s => s.WebhookSecretHash).HasMaxLength(128);
        builder.Property(s => s.LastSeen).HasMaxLength(Schedule.MaxLastSeenLength);
        builder.HasOne<Orb>().WithMany().HasForeignKey(s => s.OrbId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("push_subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasIndex(s => s.OwnerId);
        builder.Property(s => s.Endpoint).HasMaxLength(PushSubscription.MaxEndpointLength).IsRequired();
        builder.HasIndex(s => s.Endpoint).IsUnique();
        builder.Property(s => s.P256dh).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Device).HasMaxLength(200).IsRequired();
    }
}

internal sealed class ServerSettingConfiguration : IEntityTypeConfiguration<Notifications.ServerSetting>
{
    public void Configure(EntityTypeBuilder<Notifications.ServerSetting> builder)
    {
        builder.ToTable("server_settings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(100);
        builder.Property(s => s.Value).HasMaxLength(4_096).IsRequired();
    }
}

internal sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToTable("channels");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.OwnerId);
        builder.Property(c => c.Kind).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Settings).HasMaxLength(Channel.MaxSettingsLength).IsRequired();
        builder.Property(c => c.ProtectedSecret).HasMaxLength(4_096);
        builder.Property(c => c.Address).HasMaxLength(200);
        builder.Property(c => c.Cursor).HasMaxLength(500);
        builder.Property(c => c.PairingCodeHash).HasMaxLength(128);
        builder.Property(c => c.LastError).HasMaxLength(1_000);
        builder.HasOne<Orb>().WithMany().HasForeignKey(c => c.OrbId).OnDelete(DeleteBehavior.Cascade);
        builder.OwnsMany(c => c.Chats, chat =>
        {
            chat.ToTable("channel_chats");
            chat.WithOwner().HasForeignKey("ChannelId");
            chat.HasKey(x => x.Id);
            chat.Property(x => x.Id).ValueGeneratedNever();
            chat.Property(x => x.ExternalChatId).HasMaxLength(200).IsRequired();
            chat.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
        builder.Navigation(c => c.Chats).HasField("_chats").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
