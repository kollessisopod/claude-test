namespace OstQuiz.Api.Contracts;

public record GameOption(int Id, string Name);

public record TodayPuzzleResponse(
    int PuzzleId,
    DateOnly Date,
    int TotalGuesses,
    int?[] DurationSteps,
    string[] HintOrder,
    string Token);

public record HintResponse(int Step, string Kind, string Value);

public record GuessRequest(string Token, int GuessGameId);

public record PuzzleDateInfo(DateOnly Date, bool IsToday);

public record AnswerDto(
    int GameId,
    string Name,
    string[] Genres,
    DateOnly? ReleaseDate,
    int? MetacriticScore,
    string? Publisher,
    string? Developer,
    string? GameCoverUrl,
    string? AlbumCoverUrl);

public record GuessResponse(
    bool Correct,
    bool GameOver,
    HintResponse? RevealedHint,
    AnswerDto? Answer,
    string? NextToken,
    string? GuessedGameName,
    bool FranchiseMatch);

// Admin
public record ImportRequest(int? Count);
public record ImportResponse(int Imported);
public record CreatePuzzleResponse(int PuzzleId, DateOnly Date, string AudioKey, int ClipsGenerated);
