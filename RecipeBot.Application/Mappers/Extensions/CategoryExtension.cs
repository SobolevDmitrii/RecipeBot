using RecipeBot.Application.Models;
using RecipeBot.Domain.Models;

namespace RecipeBot.Application.Mappers.Extensions;

public static class CategoryExtension
{
    public static CategoryDto ToDto(this Category category)
        => new CategoryDto()
        {
            Id = category.Id,
            Name = category.Name
        };

    public static List<CategoryDto> ToDto(this List<Category> category)
        => [..category.Select(ToDto)];
    
}