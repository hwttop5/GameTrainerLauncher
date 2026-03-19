namespace GameTrainerLauncher.Core.Entities;

public class Game
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Platform { get; set; } // Steam, Epic, Xbox
    public string? AppId { get; set; }
    public string? InstallPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string? CoverUrl { get; set; }
    
    // Navigation Property
    public int? MatchedTrainerId { get; set; }
    public Trainer? MatchedTrainer { get; set; }
    
    public DateTime? AddedDate { get; set; }

    /// <summary>自定义排序序号；为 null 时按 AddedDate 倒序（最新在前）。拖拽排序后会写入 0,1,2,...</summary>
    public int? DisplayOrder { get; set; }
}
