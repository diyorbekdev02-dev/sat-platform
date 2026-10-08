namespace SatPlatform.Data;

public enum UserRole { SuperAdmin = 0, Admin = 1, Teacher = 2, Assistant = 3, Student = 4, Parent = 5 }

public enum StudentStatus { Applicant = 0, Active = 1, Frozen = 2, Dropped = 3, Graduated = 4 }

public enum Section { RW = 0, Math = 1 }

public enum Difficulty { Easy = 0, Medium = 1, Hard = 2 }

public enum QuestionType { MultipleChoice = 0, SPR = 1 }

public enum QuestionStatus { Draft = 0, Review = 1, Active = 2, Archived = 3 }

public enum TestType { Diagnostic = 0, FullMock = 1, SectionTest = 2, ModuleTest = 3, TopicTest = 4 }

public enum TestStatus { Draft = 0, Published = 1, Archived = 2 }

/// <summary>Standard = module 1 (or a non-adaptive module); Easy/Hard = the two module-2 variants.</summary>
public enum ModuleRoute { Standard = 0, Easy = 1, Hard = 2 }

public enum AttemptStatus { InProgress = 0, Break = 1, AutoSubmitted = 2, Scoring = 3, Completed = 4, Abandoned = 5 }

public enum AttemptModuleStatus { Pending = 0, InProgress = 1, Completed = 2 }

public enum PracticeMode { Quick = 0, Standard = 1, Intensive = 2, Custom = 3, Weakness = 4, Mistake = 5, Homework = 6, Recommended = 7 }

public enum PracticeStatus { InProgress = 0, Completed = 1, Abandoned = 2 }

public enum AttendanceStatus { Present = 0, Absent = 1, Late = 2, Excused = 3 }

public enum EnrollmentStatus { Active = 0, Ended = 1 }

public enum HomeworkKind { Practice = 0, Test = 1, File = 2 }

public enum SubmissionStatus { NotStarted = 0, InProgress = 1, Submitted = 2, Reviewed = 3 }

public enum NotificationType { TestResult = 0, Attendance = 1, Homework = 2, PracticeReminder = 3, System = 4, Assignment = 5 }

public enum MaterialCategory { RW = 0, Math = 1, Grammar = 2, Vocabulary = 3, Practice = 4, Other = 5 }
