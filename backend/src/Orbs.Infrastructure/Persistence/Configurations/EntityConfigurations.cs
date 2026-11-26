using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;

namespace Orbs.Infrastructure.Persistence.Configurations;

internal sealed class OrbConfiguration : IEntityTypeConfiguration<Orb>
{
    public void Configure(EntityTypeBuilder<Orb> builder)
    {
        builder.ToTable("orbs");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(Orb.MaxNameLength).IsRequired();
        builder.Property(o => o.Description).HasMaxLength(Orb.MaxDescriptionLength).IsRequired();
        builder.Property(o => o.Instructions).HasMaxLength(Orb.MaxInstructionsLength).IsRequired();

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
        builder.Property(p => p.Name).HasMaxLength(ModelProvider.MaxNameLength).IsRequired();
        builder.Property(p => p.Kind).HasMaxLength(64).IsRequired();
        builder.Property(p => p.BaseUrl).HasMaxLength(2_048);
        builder.Property(p => p.ProtectedApiKey).HasMaxLength(4_096);
        builder.Property(p => p.DefaultModel).HasMaxLength(ModelProvider.MaxModelLength);
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
        builder.Property(s => s.Name).HasMaxLength(ContextSource.MaxNameLength).IsRequired();
        builder.Property(s => s.Kind).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Location).HasMaxLength(ContextSource.MaxLocationLength).IsRequired();
        builder.Property(s => s.Input).IsRequired();
        builder.Property(s => s.Content).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.LastError).HasMaxLength(1_000);
    }
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(Conversation.MaxTitleLength).IsRequired();
        builder.HasIndex(c => new { c.OrbId, c.UpdatedAt });

        builder.HasOne<Orb>().WithMany().HasForeignKey(c => c.OrbId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages).WithOne().HasForeignKey("ConversationId").OnDelete(DeleteBehavior.Cascade);
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
    }
}
