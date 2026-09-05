using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeBot.Domain.Models;

namespace RecipeBot.Infrastructure.EntityTypeConfigurations;

public class CategoryEntityTypeConfiguration: IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(q => q.Id);
    }
}