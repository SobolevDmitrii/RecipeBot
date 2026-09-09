using RecipeBot.Application.Models;
using RecipeBot.Domain.Models;

namespace RecipeBot.Application.Mappers.Extensions;

public static class StageExtension
{
    public static StageDto ToDto(this Stage stage)
        => new StageDto()
        {
            Number = stage.Number,
            Description = stage.Description,
            UrlImage = stage.UrlImage
        };

    public static List<StageDto> ToDto(this List<Stage> stages)
        => [..stages.Select(ToDto)];

    public static Stage ToEntity(this StageDto stage)
    {
        var stageEntity = new Stage();
        stageEntity.SetNumber(stage.Number);
        if (stage.UrlImage != null)
        {
            stageEntity.SetUrlImage(stage.UrlImage);
        }
        stageEntity.SetDescription(stage.Description);
        return stageEntity;
    }
    
    public static List<Stage> ToEntity(this List<StageDto> stages)
        => [..stages.Select(ToEntity)];
}