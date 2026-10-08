using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SatPlatform.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdaptiveRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Section = table.Column<int>(type: "integer", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    Cutoff = table.Column<int>(type: "integer", nullable: false),
                    LowRoute = table.Column<int>(type: "integer", nullable: false),
                    HighRoute = table.Column<int>(type: "integer", nullable: false),
                    DefaultModuleSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdaptiveRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BotStates",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Step = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ChildCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotStates", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Domains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Section = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginTokens",
                columns: table => new
                {
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Used = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginTokens", x => x.Token);
                });

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhoneVerifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    LinkToken = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Consumed = table.Column<bool>(type: "boolean", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneVerifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScoringEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScoringVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Section = table.Column<int>(type: "integer", nullable: false),
                    Route = table.Column<int>(type: "integer", nullable: false),
                    RawScore = table.Column<int>(type: "integer", nullable: false),
                    ScaledScore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramLinks",
                columns: table => new
                {
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramLinks", x => x.Phone);
                });

            migrationBuilder.CreateTable(
                name: "Tests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsAdaptive = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScoringVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BreakSeconds = table.Column<int>(type: "integer", nullable: false),
                    ShuffleQuestions = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TelegramChatId = table.Column<long>(type: "bigint", nullable: true),
                    TelegramUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CanManageQuestions = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyTestResults = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyAttendance = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyHomework = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyPracticeReminder = table.Column<bool>(type: "boolean", nullable: false),
                    AvatarColor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DomainId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    FileId = table.Column<int>(type: "integer", nullable: true),
                    ExternalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materials_MediaFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TestModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestId = table.Column<int>(type: "integer", nullable: false),
                    Section = table.Column<int>(type: "integer", nullable: false),
                    ModuleNumber = table.Column<int>(type: "integer", nullable: false),
                    Route = table.Column<int>(type: "integer", nullable: false),
                    TimeLimitSeconds = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestModules_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CourseType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TeacherUserId = table.Column<int>(type: "integer", nullable: true),
                    AssistantUserId = table.Column<int>(type: "integer", nullable: true),
                    Room = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ScheduleDays = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Groups_Users_AssistantUserId",
                        column: x => x.AssistantUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Groups_Users_TeacherUserId",
                        column: x => x.TeacherUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    SentToTelegram = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TargetScore = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ParentCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnboardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Section = table.Column<int>(type: "integer", nullable: false),
                    DomainId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Passage = table.Column<string>(type: "text", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    VideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrectAnswer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    TotalTimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    Tag = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questions_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LateMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attendances_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollments_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Enrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Homeworks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    TestId = table.Column<int>(type: "integer", nullable: true),
                    Section = table.Column<int>(type: "integer", nullable: true),
                    DomainId = table.Column<int>(type: "integer", nullable: true),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
                    Difficulty = table.Column<int>(type: "integer", nullable: true),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    FileId = table.Column<int>(type: "integer", nullable: true),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Homeworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Homeworks_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Homeworks_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Homeworks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentStudents",
                columns: table => new
                {
                    ParentUserId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentStudents", x => new { x.ParentUserId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_ParentStudents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentStudents_Users_ParentUserId",
                        column: x => x.ParentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Section = table.Column<int>(type: "integer", nullable: true),
                    DomainId = table.Column<int>(type: "integer", nullable: true),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
                    Difficulty = table.Column<int>(type: "integer", nullable: true),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    TimeLimitSeconds = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeadlineAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    AnsweredCount = table.Column<int>(type: "integer", nullable: false),
                    HomeworkId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeSessions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoreHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    AttemptId = table.Column<int>(type: "integer", nullable: false),
                    TestType = table.Column<int>(type: "integer", nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    RwScore = table.Column<int>(type: "integer", nullable: false),
                    MathScore = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreHistories_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentSkillStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    AttemptedCount = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    TotalTimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSkillStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentSkillStatistics_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentSkillStatistics_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptLimit = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestAssignments_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestAssignments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestAssignments_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    TestId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BreakEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentModuleOrder = table.Column<int>(type: "integer", nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: true),
                    RwScore = table.Column<int>(type: "integer", nullable: true),
                    MathScore = table.Column<int>(type: "integer", nullable: true),
                    RwRaw = table.Column<int>(type: "integer", nullable: false),
                    MathRaw = table.Column<int>(type: "integer", nullable: false),
                    RwMax = table.Column<int>(type: "integer", nullable: false),
                    MathMax = table.Column<int>(type: "integer", nullable: false),
                    RwRoute = table.Column<int>(type: "integer", nullable: true),
                    MathRoute = table.Column<int>(type: "integer", nullable: true),
                    ScoringVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RandomSeed = table.Column<int>(type: "integer", nullable: true),
                    IsPreview = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestAttempts_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestAttempts_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionOptions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Passage = table.Column<string>(type: "text", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    VideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrectAnswer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OptionsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionVersions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentMistakes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    MistakeCount = table.Column<int>(type: "integer", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    LastWrongAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSource = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LastAnswer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentMistakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentMistakes_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentMistakes_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestModuleId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestQuestions_TestModules_TestModuleId",
                        column: x => x.TestModuleId,
                        principalTable: "TestModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HomeworkId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    MaxScore = table.Column<int>(type: "integer", nullable: true),
                    PracticeSessionId = table.Column<int>(type: "integer", nullable: true),
                    AttemptId = table.Column<int>(type: "integer", nullable: true),
                    FileId = table.Column<int>(type: "integer", nullable: true),
                    TextAnswer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TeacherComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissions_Homeworks_HomeworkId",
                        column: x => x.HomeworkId,
                        principalTable: "Homeworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkSubmissions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttemptModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttemptId = table.Column<int>(type: "integer", nullable: false),
                    TestModuleId = table.Column<int>(type: "integer", nullable: true),
                    Section = table.Column<int>(type: "integer", nullable: false),
                    ModuleNumber = table.Column<int>(type: "integer", nullable: false),
                    Route = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    TimeLimitSeconds = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadlineAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawScore = table.Column<int>(type: "integer", nullable: true),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptModules_TestAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "TestAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    QuestionVersionId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Answer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    TimeSpentSeconds = table.Column<int>(type: "integer", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsFlagged = table.Column<bool>(type: "boolean", nullable: false),
                    EliminatedOptions = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeQuestions_PracticeSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PracticeSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PracticeQuestions_QuestionVersions_QuestionVersionId",
                        column: x => x.QuestionVersionId,
                        principalTable: "QuestionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PracticeQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttemptQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttemptId = table.Column<int>(type: "integer", nullable: false),
                    AttemptModuleId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    QuestionVersionId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsFlagged = table.Column<bool>(type: "boolean", nullable: false),
                    Answer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    TimeSpentSeconds = table.Column<int>(type: "integer", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EliminatedOptions = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptQuestions_AttemptModules_AttemptModuleId",
                        column: x => x.AttemptModuleId,
                        principalTable: "AttemptModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttemptQuestions_QuestionVersions_QuestionVersionId",
                        column: x => x.QuestionVersionId,
                        principalTable: "QuestionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttemptQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttemptQuestions_TestAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "TestAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveRules_Section",
                table: "AdaptiveRules",
                column: "Section",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptModules_AttemptId",
                table: "AttemptModules",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_AttemptId_Order",
                table: "AttemptQuestions",
                columns: new[] { "AttemptId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_AttemptModuleId",
                table: "AttemptQuestions",
                column: "AttemptModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_QuestionId",
                table: "AttemptQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_QuestionVersionId",
                table: "AttemptQuestions",
                column: "QuestionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_GroupId_StudentId_Date",
                table: "Attendances",
                columns: new[] { "GroupId", "StudentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_StudentId",
                table: "Attendances",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_Code",
                table: "Domains",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_GroupId",
                table: "Enrollments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_GroupId_Status",
                table: "Enrollments",
                columns: new[] { "StudentId", "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Groups_AssistantUserId",
                table: "Groups",
                column: "AssistantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_TeacherUserId",
                table: "Groups",
                column: "TeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Homeworks_CreatedByUserId",
                table: "Homeworks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Homeworks_GroupId",
                table: "Homeworks",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Homeworks_StudentId",
                table: "Homeworks",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissions_HomeworkId_StudentId",
                table: "HomeworkSubmissions",
                columns: new[] { "HomeworkId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSubmissions_StudentId",
                table: "HomeworkSubmissions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_FileId",
                table: "Materials",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_Name",
                table: "MediaFiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudents_StudentId",
                table: "ParentStudents",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_LinkToken",
                table: "PhoneVerifications",
                column: "LinkToken");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_Phone",
                table: "PhoneVerifications",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeQuestions_QuestionId",
                table: "PracticeQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeQuestions_QuestionVersionId",
                table: "PracticeQuestions",
                column: "QuestionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeQuestions_SessionId",
                table: "PracticeQuestions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_StudentId",
                table: "PracticeSessions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_DomainId",
                table: "Questions",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Section_DomainId_SkillId_Difficulty_Status",
                table: "Questions",
                columns: new[] { "Section", "DomainId", "SkillId", "Difficulty", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SkillId",
                table: "Questions",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionVersions_QuestionId_Version",
                table: "QuestionVersions",
                columns: new[] { "QuestionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreHistories_StudentId",
                table: "ScoreHistories",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoringEntries_ScoringVersion_Section_Route_RawScore",
                table: "ScoringEntries",
                columns: new[] { "ScoringVersion", "Section", "Route", "RawScore" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Code",
                table: "Skills",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_DomainId",
                table: "Skills",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMistakes_QuestionId",
                table: "StudentMistakes",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMistakes_StudentId_QuestionId",
                table: "StudentMistakes",
                columns: new[] { "StudentId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_ParentCode",
                table: "Students",
                column: "ParentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_UserId",
                table: "Students",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillStatistics_SkillId",
                table: "StudentSkillStatistics",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillStatistics_StudentId_SkillId",
                table: "StudentSkillStatistics",
                columns: new[] { "StudentId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestAssignments_GroupId",
                table: "TestAssignments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAssignments_StudentId",
                table: "TestAssignments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAssignments_TestId",
                table: "TestAssignments",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_StudentId_Status",
                table: "TestAttempts",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_TestId",
                table: "TestAttempts",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_TestModules_TestId",
                table: "TestModules",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_TestQuestions_QuestionId",
                table: "TestQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_TestQuestions_TestModuleId_QuestionId",
                table: "TestQuestions",
                columns: new[] { "TestModuleId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TelegramChatId",
                table: "Users",
                column: "TelegramChatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdaptiveRules");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AttemptQuestions");

            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BotStates");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropTable(
                name: "HomeworkSubmissions");

            migrationBuilder.DropTable(
                name: "LoginTokens");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ParentStudents");

            migrationBuilder.DropTable(
                name: "PhoneVerifications");

            migrationBuilder.DropTable(
                name: "PracticeQuestions");

            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "ScoreHistories");

            migrationBuilder.DropTable(
                name: "ScoringEntries");

            migrationBuilder.DropTable(
                name: "StudentMistakes");

            migrationBuilder.DropTable(
                name: "StudentSkillStatistics");

            migrationBuilder.DropTable(
                name: "TelegramLinks");

            migrationBuilder.DropTable(
                name: "TestAssignments");

            migrationBuilder.DropTable(
                name: "TestQuestions");

            migrationBuilder.DropTable(
                name: "AttemptModules");

            migrationBuilder.DropTable(
                name: "Homeworks");

            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropTable(
                name: "PracticeSessions");

            migrationBuilder.DropTable(
                name: "QuestionVersions");

            migrationBuilder.DropTable(
                name: "TestModules");

            migrationBuilder.DropTable(
                name: "TestAttempts");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Tests");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Domains");
        }
    }
}
