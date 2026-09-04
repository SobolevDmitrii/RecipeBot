using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyRecipeDomain.Models;

namespace RecipeBot.Infrastructure.EntityTypeConfigurations;

public class StageEntityTypeConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.HasKey(q => q.Id);

        builder.HasOne(q => q.Recipe)
            .WithMany(q => q.Stages)
            .HasForeignKey(q => q.RecipeId);
    }
}