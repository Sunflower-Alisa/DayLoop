using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class WordBook
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("level")] public string Level { get; set; } = "intermediate";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("cover_color")] public string CoverColor { get; set; } = "#4f46e5";
    [JsonPropertyName("is_default")] public bool IsDefault { get; set; }
    [JsonPropertyName("word_count")] public int WordCount { get; set; }
    [JsonPropertyName("learned_count")] public int LearnedCount { get; set; }
    [JsonPropertyName("mastered_count")] public int MasteredCount { get; set; }
    [JsonPropertyName("daily_goal")] public int DailyGoal { get; set; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
}

public class Word
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("word")] public string WordText { get; set; } = "";
    [JsonPropertyName("phonetic")] public string Phonetic { get; set; } = "";
    [JsonPropertyName("pos")] public string Pos { get; set; } = "";
    [JsonPropertyName("meaning")] public string Meaning { get; set; } = "";
    [JsonPropertyName("example_en")] public string ExampleEn { get; set; } = "";
    [JsonPropertyName("example_cn")] public string ExampleCn { get; set; } = "";
    [JsonPropertyName("image_url")] public string ImageUrl { get; set; } = "";
    [JsonPropertyName("audio_url")] public string AudioUrl { get; set; } = "";
    [JsonPropertyName("book_id")] public long BookId { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "new";
    [JsonPropertyName("stage")] public int Stage { get; set; }
    [JsonPropertyName("in_wrong_book")] public bool InWrongBook { get; set; }
}

public class WordProgress
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("word_id")] public long WordId { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "new";
    [JsonPropertyName("stage")] public int Stage { get; set; }
    [JsonPropertyName("correct_streak")] public int CorrectStreak { get; set; }
    [JsonPropertyName("wrong_count")] public int WrongCount { get; set; }
    [JsonPropertyName("last_review_at")] public string LastReviewAt { get; set; } = "";
    [JsonPropertyName("next_review_at")] public string NextReviewAt { get; set; } = "";
}

public class DailyWordTask
{
    [JsonPropertyName("new_words")] public List<Word> NewWords { get; set; } = new();
    [JsonPropertyName("review_words")] public List<Word> ReviewWords { get; set; } = new();
    [JsonPropertyName("new_goal")] public int NewGoal { get; set; }
    [JsonPropertyName("new_done")] public int NewDone { get; set; }
    [JsonPropertyName("review_done")] public int ReviewDone { get; set; }
    [JsonPropertyName("has_book")] public bool HasBook { get; set; }
}

public class LearnResultRequest
{
    [JsonPropertyName("word_id")] public long WordId { get; set; }
    [JsonPropertyName("correct")] public bool Correct { get; set; }
    [JsonPropertyName("is_review")] public bool IsReview { get; set; }
    [JsonPropertyName("know")] public bool Know { get; set; }
}

public class Scenario
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("level")] public int Level { get; set; } = 1;
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("line_count")] public int LineCount { get; set; }
    [JsonPropertyName("mastered")] public bool Mastered { get; set; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
}

public class ScenarioDetail
{
    [JsonPropertyName("scenario")] public Scenario Scenario { get; set; } = new();
    [JsonPropertyName("lines")] public List<ScenarioLine> Lines { get; set; } = new();
    [JsonPropertyName("phrases")] public List<ScenarioPhrase> Phrases { get; set; } = new();
    [JsonPropertyName("quizzes")] public List<ScenarioQuiz> Quizzes { get; set; } = new();
}

public class ScenarioLine
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("scenario_id")] public long ScenarioId { get; set; }
    [JsonPropertyName("order")] public int Order { get; set; }
    [JsonPropertyName("speaker")] public string Speaker { get; set; } = "";
    [JsonPropertyName("en_text")] public string EnText { get; set; } = "";
    [JsonPropertyName("cn_text")] public string CnText { get; set; } = "";
    [JsonPropertyName("audio_url")] public string AudioUrl { get; set; } = "";
}

public class ScenarioPhrase
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("scenario_id")] public long ScenarioId { get; set; }
    [JsonPropertyName("phrase")] public string Phrase { get; set; } = "";
    [JsonPropertyName("meaning")] public string Meaning { get; set; } = "";
    [JsonPropertyName("example_en")] public string ExampleEn { get; set; } = "";
    [JsonPropertyName("example_cn")] public string ExampleCn { get; set; } = "";
}

public class ScenarioQuiz
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("scenario_id")] public long ScenarioId { get; set; }
    [JsonPropertyName("question_en")] public string QuestionEn { get; set; } = "";
    [JsonPropertyName("question_cn")] public string QuestionCn { get; set; } = "";
    [JsonPropertyName("options")] public List<string> Options { get; set; } = new();
    [JsonPropertyName("answer_index")] public int AnswerIndex { get; set; }
    [JsonPropertyName("explanation")] public string Explanation { get; set; } = "";
}

public class QuizResultRequest
{
    [JsonPropertyName("scenario_id")] public long ScenarioId { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("correct")] public int Correct { get; set; }
}

public class SpeakingTopic
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "daily";
    [JsonPropertyName("level")] public string Level { get; set; } = "beginner";
    [JsonPropertyName("lines")] public List<SpeakingLine> Lines { get; set; } = new();
    [JsonPropertyName("source_type")] public string SourceType { get; set; } = "topic";
    [JsonPropertyName("source_id")] public long SourceId { get; set; }
    [JsonPropertyName("best_score")] public int BestScore { get; set; }
    [JsonPropertyName("practice_count")] public int PracticeCount { get; set; }
}

public class SpeakingLine
{
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("cn")] public string Cn { get; set; } = "";
    [JsonPropertyName("audio_url")] public string AudioUrl { get; set; } = "";
}

public class SpeakingRecordRequest
{
    [JsonPropertyName("topic_id")] public long TopicId { get; set; }
    [JsonPropertyName("line_index")] public int LineIndex { get; set; }
    [JsonPropertyName("audio_url")] public string? AudioUrl { get; set; }
    [JsonPropertyName("accuracy")] public int Accuracy { get; set; }
    [JsonPropertyName("fluency")] public int Fluency { get; set; }
    [JsonPropertyName("completeness")] public int Completeness { get; set; }
    [JsonPropertyName("overall")] public int Overall { get; set; }
}

public class VideoClip
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("cover_url")] public string CoverUrl { get; set; } = "";
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("duration")] public int Duration { get; set; }
    [JsonPropertyName("level")] public string Level { get; set; } = "medium";
    [JsonPropertyName("tags")] public string Tags { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("line_count")] public int LineCount { get; set; }
    [JsonPropertyName("learned_count")] public int LearnedCount { get; set; }
}

public class ClipLine
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("clip_id")] public long ClipId { get; set; }
    [JsonPropertyName("order")] public int Order { get; set; }
    [JsonPropertyName("speaker")] public string Speaker { get; set; } = "";
    [JsonPropertyName("en_text")] public string EnText { get; set; } = "";
    [JsonPropertyName("cn_text")] public string CnText { get; set; } = "";
    [JsonPropertyName("start_time")] public double StartTime { get; set; }
    [JsonPropertyName("end_time")] public double EndTime { get; set; }
}

public class StudySessionRequest
{
    [JsonPropertyName("module")] public string Module { get; set; } = "";
    [JsonPropertyName("start_time")] public string StartTime { get; set; } = "";
    [JsonPropertyName("end_time")] public string EndTime { get; set; } = "";
    [JsonPropertyName("duration_seconds")] public int DurationSeconds { get; set; }
}

public class EnglishDashboard
{
    [JsonPropertyName("streak")] public int Streak { get; set; }
    [JsonPropertyName("checked_in_today")] public bool CheckedInToday { get; set; }
    [JsonPropertyName("new_goal")] public int NewGoal { get; set; }
    [JsonPropertyName("new_done")] public int NewDone { get; set; }
    [JsonPropertyName("review_done")] public int ReviewDone { get; set; }
    [JsonPropertyName("today_seconds")] public int TodaySeconds { get; set; }
    [JsonPropertyName("week_seconds")] public int WeekSeconds { get; set; }
    [JsonPropertyName("total_seconds")] public int TotalSeconds { get; set; }
    [JsonPropertyName("total_words")] public int TotalWords { get; set; }
    [JsonPropertyName("mastered_words")] public int MasteredWords { get; set; }
    [JsonPropertyName("learning_words")] public int LearningWords { get; set; }
    [JsonPropertyName("wrong_count")] public int WrongCount { get; set; }
    [JsonPropertyName("scenario_count")] public int ScenarioCount { get; set; }
    [JsonPropertyName("scenario_mastered")] public int ScenarioMastered { get; set; }
    [JsonPropertyName("speaking_avg")] public int SpeakingAvg { get; set; }
    [JsonPropertyName("clip_count")] public int ClipCount { get; set; }
}

public class SetGoalRequest
{
    [JsonPropertyName("daily_goal")] public int DailyGoal { get; set; }
}
