/*using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyRecipeDomain.Models;

namespace MyRecipeInfraData.EntityTypeConfigurations;

public class RecipeEntityTypeConfiguration: IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.HasKey(q => q.Id);

        builder.HasOne(q=>q.User)
            .WithMany(q => q.Recipes)
            .HasForeignKey(q => q.UserId);
    }
}*/